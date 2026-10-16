param([switch]$Diagnostic,[string]$OutputPath)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$fx=Split-Path $csc
if(!$OutputPath){$OutputPath=Join-Path $root 'TrayLeaf.exe'}
$OutputPath=[IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory((Split-Path $OutputPath)) | Out-Null
$flags=@('/nologo','/target:winexe','/platform:x64','/optimize+','/utf8output',('/out:'+$OutputPath),('/resource:'+(Join-Path $root 'src\MainWindow.xaml')+',TrayLeaf.MainWindow.xaml'))
$flags+='/win32icon:'+(Join-Path $root 'assets\TrayLeaf.ico')
$flags+='/resource:'+(Join-Path $root 'assets\TrayLeaf.ico')+',TrayLeaf.Assets.TrayLeaf.ico'
$flags+='/resource:'+(Join-Path $root 'assets\TrayLeaf.png')+',TrayLeaf.Assets.TrayLeaf.png'
if($Diagnostic){$flags+='/win32manifest:'+(Join-Path $root 'tests\asInvoker.manifest')}else{$flags+='/win32manifest:'+(Join-Path $root 'app.manifest')}
foreach($dll in @('PresentationFramework','PresentationCore','WindowsBase','System.Xaml')){if($dll -eq 'System.Xaml'){$flags+='/r:'+(Join-Path $fx ($dll+'.dll'))}else{$flags+='/r:'+(Join-Path $fx ('WPF\'+$dll+'.dll'))}}
foreach($dll in @('System','System.Core','System.Management','System.Windows.Forms','System.Drawing','Microsoft.CSharp')){$flags+='/r:'+(Join-Path $fx ($dll+'.dll'))}
$flags+=@(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName)
& $csc @flags
if($LASTEXITCODE -ne 0){throw "编译失败，退出码 $LASTEXITCODE"}
$config=Join-Path $root 'TrayLeaf.exe.config'
if(![String]::Equals([IO.Path]::GetFullPath($config),$OutputPath+'.config',[StringComparison]::OrdinalIgnoreCase)){Copy-Item -LiteralPath $config -Destination ($OutputPath+'.config') -Force}
Get-Item -LiteralPath $OutputPath | Select-Object FullName,Length
Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256
