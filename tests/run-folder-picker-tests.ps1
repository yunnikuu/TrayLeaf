param([string]$WorkPath,[string]$WixBin=$env:WIX_BIN)
$ErrorActionPreference='Stop'
if(!$WorkPath){$WorkPath=Join-Path $env:TEMP ('TrayLeaf-native-'+[Guid]::NewGuid().ToString('N'))}
if(!$WixBin){throw 'Specify -WixBin'}
[IO.Directory]::CreateDirectory($WorkPath)|Out-Null
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$dtf=Join-Path $WixBin 'sdk\Microsoft.Deployment.WindowsInstaller.dll'
Copy-Item -LiteralPath $dtf -Destination $WorkPath -Force
& $csc /nologo /target:library /platform:x64 /utf8output ('/r:'+$dtf) /r:System.Windows.Forms.dll ('/out:'+(Join-Path $WorkPath 'FolderPicker.dll')) (Join-Path (Split-Path $PSScriptRoot) 'installer\FolderPicker.cs')
if($LASTEXITCODE){exit $LASTEXITCODE}
& $csc /nologo /target:exe /platform:x64 /utf8output ('/r:'+(Join-Path $WorkPath 'FolderPicker.dll')) ('/r:'+$dtf) /r:System.Windows.Forms.dll ('/out:'+(Join-Path $WorkPath 'FolderPickerTests.exe')) (Join-Path $PSScriptRoot 'FolderPickerTests.cs')
if($LASTEXITCODE){exit $LASTEXITCODE}
& (Join-Path $WorkPath 'FolderPickerTests.exe')
exit $LASTEXITCODE
