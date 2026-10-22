param([string]$Target,[string]$WorkPath)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot
if(!$Target){$Target=Join-Path $project 'TrayLeaf.exe'}
$Target=[IO.Path]::GetFullPath($Target)
if(!$WorkPath){$WorkPath=Join-Path $env:TEMP ('TrayLeaf-settings-probe-'+[Guid]::NewGuid().ToString('N'))}
$WorkPath=[IO.Path]::GetFullPath($WorkPath)
[IO.Directory]::CreateDirectory($WorkPath)|Out-Null
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$fx=Split-Path $csc
$probe=Join-Path $WorkPath 'SettingsSaveProbe.exe'
$flags=@('/nologo','/utf8output','/r:Microsoft.CSharp.dll','/r:System.Xaml.dll',('/out:'+$probe))
foreach($name in @('PresentationFramework','PresentationCore','WindowsBase')){$flags+='/r:'+(Join-Path $fx ('WPF\'+$name+'.dll'))}
& $csc @flags (Join-Path $PSScriptRoot 'SettingsSaveProbe.cs')
if($LASTEXITCODE){throw 'Probe compilation failed'}
$previous=$env:TRAYLEAF_PROBE_SCREENSHOTS
try {
 $env:TRAYLEAF_PROBE_SCREENSHOTS=$WorkPath
 & $probe $Target --scenario settings-save
 if($LASTEXITCODE){throw ('Settings probe failed: '+$LASTEXITCODE)}
}finally{$env:TRAYLEAF_PROBE_SCREENSHOTS=$previous}
