param([Parameter(Mandatory=$true)][string]$RepositoryPath)
$ErrorActionPreference='Stop';$pass=0;$fail=0
function Check($n,$ok){if($ok){$script:pass++;"PASS $n"}else{$script:fail++;"FAIL $n"}}
$root=[IO.Path]::GetFullPath($RepositoryPath)
$policyPath=Join-Path $root 'source-export.json'
Check 'export-policy-present' (Test-Path -LiteralPath $policyPath)
if(!(Test-Path -LiteralPath $policyPath)){"RESULT passed=$pass failed=$fail";exit 1}
$policy=Get-Content -LiteralPath $policyPath -Raw -Encoding UTF8|ConvertFrom-Json
$tests=@(Get-ChildItem -LiteralPath (Join-Path $root 'tests') -File|ForEach-Object Name)
Check 'export-helper-present' (Test-Path -LiteralPath (Join-Path $root 'export-source.ps1'))
Check 'test-allowlist-matches-export' (@(Compare-Object @($policy.test_files) $tests).Count -eq 0)
Check 'historical-version-tests-excluded' (@($tests|Where-Object{$_ -match '^(Version0\d+|run-0\d+-tests)'}).Count -eq 0)
Check 'duplicate-desktop-and-maintenance-tools-excluded' (!($tests -contains 'DesktopPlacementTests.cpp') -and !($tests -contains 'update-language-catalog.py'))
foreach($name in @('run-tests.ps1','ControlledFixture.cs','run-policy-tests.ps1','run-100-tests.ps1','run-100-regression-tests.ps1','run-glass-drag-tests.ps1','verify-installer-finish-firstpaint.ps1')){Check ('critical-test-retained-'+$name) ($tests -contains $name)}
$missing=@();foreach($p in Get-ChildItem -LiteralPath (Join-Path $root 'tests') -Filter 'run-*.ps1'){
 $content=[IO.File]::ReadAllText($p.FullName)
 foreach($m in [regex]::Matches($content,"'([\w-]+\.(?:cs|cpp|manifest))'")){
  $name=$m.Groups[1].Value
  if($name -ne 'MainWindow.xaml' -and !(Test-Path -LiteralPath (Join-Path $root ('tests\'+$name))) -and !(Test-Path -LiteralPath (Join-Path $root ('installer\'+$name)))){$missing+=$p.Name+':'+$name}
 }
}
Check 'retained-test-local-dependencies-present' ($missing.Count -eq 0)
if($missing.Count){$missing|ForEach-Object{"MISSING $_"}}
$bad=@(Get-ChildItem -LiteralPath $root -Recurse -File|Where-Object{$rel=$_.FullName.Substring($root.Length+1);$rel -match '(^|\\)docs(\\|$)' -or ($_.Extension -eq '.md' -and $rel -ne 'CHANGELOG.md') -or $_.Extension -in @('.exe','.msi','.zip','.db','.sha256','.obj','.lib','.exp')})
Check 'source-export-has-no-local-or-forbidden-artifacts' ($bad.Count -eq 0)
"RESULT passed=$pass failed=$fail";if($fail){exit 1};exit 0
