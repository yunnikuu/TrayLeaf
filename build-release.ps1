param([string]$OutputDir,[string]$PayloadDir,[string]$WixBin=$env:WIX_BIN)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$workspace=(& (Join-Path $root 'resolve-project.ps1') -Json | ConvertFrom-Json).WorkspaceRoot
if(!$WixBin -or !(Test-Path -LiteralPath (Join-Path $WixBin 'candle.exe')) -or !(Test-Path -LiteralPath (Join-Path $WixBin 'light.exe'))){throw '发布必须同时生成 ZIP 和 MSI，请通过 -WixBin 指定 WiX 工具目录'}
$stagingRoot=Join-Path $workspace 'work\packaging'
$stage=Join-Path $stagingRoot ('TrayLeaf-package-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($stage) | Out-Null
try {
 if(!$PayloadDir){
  & (Join-Path $root 'build.ps1') -OutputPath (Join-Path $stage 'TrayLeaf.exe')
 }else{
  $PayloadDir=[IO.Path]::GetFullPath($PayloadDir)
  foreach($name in @('TrayLeaf.exe','TrayLeaf.config')){
   Copy-Item -LiteralPath (Join-Path $PayloadDir $name) -Destination (Join-Path $stage $name)
  }
 }
 Copy-Item -LiteralPath (Join-Path $root 'installer\WiX-LICENSE.txt') -Destination (Join-Path $stage 'WiX-LICENSE.txt')
 $version=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $stage 'TrayLeaf.exe'))
 $v=$version.ProductVersion
 if($v -notmatch '^\d+\.\d+\.\d+$'){throw "Invalid product version: $v"}
 if(!$OutputDir){$OutputDir=Join-Path $workspace ('TrayLeaf\releases\github'+$v+'\release')}
 $OutputDir=[IO.Path]::GetFullPath($OutputDir)
 [IO.Directory]::CreateDirectory($OutputDir) | Out-Null
 $folder='TrayLeaf-v'+$v+'-Windows-x64'
 $files=@('TrayLeaf.exe','TrayLeaf.config','WiX-LICENSE.txt')
 Add-Type -AssemblyName System.IO.Compression
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 $temporaryZip=Join-Path $stage 'package.zip'
 $zip=[IO.Compression.ZipFile]::Open($temporaryZip,[IO.Compression.ZipArchiveMode]::Create)
 try {
  foreach($name in $files){
   [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $stage $name),($folder+'/'+$name),[IO.Compression.CompressionLevel]::Optimal) | Out-Null
  }
 }finally{$zip.Dispose()}
 $archive=Join-Path $OutputDir ($folder+'.zip')
 $installerName=$folder+'-Setup.msi'
 & (Join-Path $root 'installer\build-installer.ps1') -WixBin $WixBin -OutputDir $stage -PayloadDir $stage
 Copy-Item -LiteralPath $temporaryZip -Destination $archive -Force
 Copy-Item -LiteralPath (Join-Path $stage $installerName) -Destination (Join-Path $OutputDir $installerName) -Force
 Write-Output "ARCHIVE=$archive"
 Write-Output ('INSTALLER='+(Join-Path $OutputDir $installerName))
}finally{
 $resolved=[IO.Path]::GetFullPath($stage)
 $temp=[IO.Path]::GetFullPath($stagingRoot).TrimEnd('\')+'\'
 if(!$resolved.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notlike 'TrayLeaf-package-*'){throw 'Unexpected staging path; cleanup stopped.'}
 Remove-Item -LiteralPath $resolved -Recurse -Force
}
