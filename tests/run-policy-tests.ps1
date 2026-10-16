param([string]$Source,[string]$WorkPath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
if(!$Source){$Source=Join-Path $root 'src\Policy.cs'}
if(!$WorkPath){$WorkPath=Join-Path $env:TEMP ('TrayLeaf-policy-'+[Guid]::NewGuid().ToString('N'))}
[IO.Directory]::CreateDirectory($WorkPath)|Out-Null
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /target:exe /utf8output ('/out:'+(Join-Path $WorkPath 'PolicyTests.exe')) $Source (Join-Path $PSScriptRoot 'PolicyTests.cs')
if($LASTEXITCODE){exit $LASTEXITCODE}
& (Join-Path $WorkPath 'PolicyTests.exe')
exit $LASTEXITCODE
