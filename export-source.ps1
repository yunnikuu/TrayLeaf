param([Parameter(Mandatory=$true)][string]$Destination,[switch]$ListOnly)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($PSScriptRoot)
$workspace=(& (Join-Path $root 'resolve-project.ps1') -Json|ConvertFrom-Json).WorkspaceRoot
$exportRoot=[IO.Path]::GetFullPath((Join-Path $workspace 'source\releases')).TrimEnd('\')
$destinationRoot=[IO.Path]::GetFullPath($Destination).TrimEnd('\')
if(!$destinationRoot.StartsWith($exportRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or (Split-Path $destinationRoot -Leaf) -ne 'repository'){throw 'Destination must be a repository folder beneath source/releases.'}
$policy=Get-Content -LiteralPath (Join-Path $root 'source-export.json') -Raw -Encoding UTF8|ConvertFrom-Json
$files=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach($name in $policy.root_files){if($name -match '[\\/]' -or $name -eq '..' -or $name -like '*.md' -or $name -like 'commit-changes*.json'){throw 'Invalid root file in export policy'};[void]$files.Add($name)}
foreach($dir in $policy.source_directories){
 if($dir -notin @('src','assets','installer')){throw 'Invalid source directory in export policy'}
 foreach($p in Get-ChildItem -LiteralPath (Join-Path $root $dir) -Recurse -File){
  $rel=$p.FullName.Substring($root.Length+1).Replace('\','/')
  if($p.Extension -in @('.md','.exe','.msi','.zip','.db','.sha256','.obj','.lib','.exp','.pdb','.wixobj','.wixpdb') -or $rel -match '(^|/)(docs|bin|obj|\.git)(/|$)'){continue}
  [void]$files.Add($rel)
 }
}
foreach($name in $policy.test_files){if($name -match '[\\/]' -or $name -eq '..'){throw 'Invalid test file in export policy'};[void]$files.Add('tests/'+$name)}
foreach($name in $files){if(!(Test-Path -LiteralPath (Join-Path $root $name) -PathType Leaf)){throw ('Required export input missing: '+$name)}}
if($ListOnly){$files|Sort-Object;return}
if(Test-Path -LiteralPath $destinationRoot){
 if(@(Get-ChildItem -LiteralPath $destinationRoot -Recurse -Attributes ReparsePoint).Count){throw 'Refusing an export destination containing reparse points'}
}
[IO.Directory]::CreateDirectory($destinationRoot)|Out-Null
foreach($name in $files){$p=Join-Path $destinationRoot $name;[IO.Directory]::CreateDirectory((Split-Path $p))|Out-Null;Copy-Item -LiteralPath (Join-Path $root $name) -Destination $p -Force}
# Remove stale generated files only; never modify the authoring source tree.
$removed=0
foreach($p in Get-ChildItem -LiteralPath $destinationRoot -Recurse -File){
 $absolute=[IO.Path]::GetFullPath($p.FullName)
 if(!$absolute.StartsWith($destinationRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Export cleanup target escaped repository'}
 $rel=$absolute.Substring($destinationRoot.Length+1).Replace('\','/')
 if(!$files.Contains($rel)){Remove-Item -LiteralPath $absolute -Force;$removed++}
}
Write-Output ('EXPORTED_FILES='+$files.Count)
Write-Output ('EXPORTED_TEST_FILES='+@($policy.test_files).Count)
Write-Output ('REMOVED_STALE_EXPORT_FILES='+$removed)

# Summaries are release sidecars, never source files or distributable payload.
[xml]$definition=Get-Content -LiteralPath (Join-Path $root 'installer\TrayLeaf.wxs') -Raw -Encoding UTF8
$releaseVersion=[string]($definition.Wix.Product.Property|Where-Object Id -eq 'TRAYLEAF_RELEASE_VERSION').Value
if(!$releaseVersion){$releaseVersion=[string]$definition.Wix.Product.Version}
if($releaseVersion -notmatch '^\d+\.\d+\.\d+(?:-fix\d+)?$'){throw 'Invalid release identity for commit summary'}
$summaryDir=Join-Path $workspace ('TrayLeaf\releases\github'+$releaseVersion+'\release')
[IO.Directory]::CreateDirectory($summaryDir)|Out-Null
foreach($entry in $policy.release_changes.changes){
 if($entry.path -match '[\\/]' -or $entry.path -in @('.','..') -or $entry.kind -notin @('file','directory') -or !$entry.change -or !$entry.change_zh){throw 'Commit summaries require bilingual top-level entries only'}
}
$summary=[ordered]@{schema_version=2;version=$releaseVersion;base_version=$policy.release_changes.base_version;commit_title=$policy.release_changes.commit_title;commit_title_zh=$policy.release_changes.commit_title_zh;scope='Top-level files and directory summaries only; nested files intentionally omitted.';scope_zh='仅列根目录独立文件与一级文件夹总体修改，不列文件夹内部文件。';changes=@($policy.release_changes.changes)}
$summaryPath=Join-Path $summaryDir ('commit-changes-v'+$releaseVersion+'.json')
[IO.File]::WriteAllText($summaryPath,($summary|ConvertTo-Json -Depth 8),(New-Object Text.UTF8Encoding($false)))
Write-Output ('COMMIT_SUMMARY='+$summaryPath)
