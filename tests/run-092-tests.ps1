param([string]$WorkPath,[string]$SourcePath=(Split-Path $PSScriptRoot))
$ErrorActionPreference='Stop'
if(!$WorkPath){$WorkPath=Join-Path $env:TEMP ('TrayLeaf-092-'+[Guid]::NewGuid().ToString('N'))}
[IO.Directory]::CreateDirectory($WorkPath)|Out-Null
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$fx=Split-Path $csc
$flags=@('/nologo','/target:exe','/platform:x64','/main:Version092Tests','/utf8output',('/win32manifest:'+(Join-Path $PSScriptRoot 'asInvoker.manifest')),('/out:'+(Join-Path $WorkPath 'Version092Tests.exe')),('/resource:'+(Join-Path $SourcePath 'src\MainWindow.xaml')+',TrayLeaf.MainWindow.xaml'))
foreach($dll in @('PresentationFramework','PresentationCore','WindowsBase')){$flags+='/r:'+(Join-Path $fx ('WPF\'+$dll+'.dll'))}
foreach($dll in @('System','System.Core','System.Management','System.Windows.Forms','System.Drawing','Microsoft.CSharp','System.Xaml')){$flags+='/r:'+(Join-Path $fx ($dll+'.dll'))}
$flags+='/resource:'+(Join-Path $SourcePath 'assets\TrayLeaf.ico')+',TrayLeaf.Assets.TrayLeaf.ico'
$flags+='/resource:'+(Join-Path $SourcePath 'assets\TrayLeaf.png')+',TrayLeaf.Assets.TrayLeaf.png'
$flags+=@(Get-ChildItem -LiteralPath (Join-Path $SourcePath 'src') -Filter '*.cs' | ForEach-Object FullName)
$flags+=Join-Path $PSScriptRoot 'Version092Tests.cs'
& $csc @flags
if($LASTEXITCODE){exit $LASTEXITCODE}
Copy-Item -LiteralPath (Join-Path $SourcePath 'TrayLeaf.config') -Destination $WorkPath -Force
& (Join-Path $WorkPath 'Version092Tests.exe') $WorkPath $SourcePath
exit $LASTEXITCODE
