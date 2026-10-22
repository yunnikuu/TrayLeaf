param([string]$WorkPath)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot
if(!$WorkPath){$WorkPath=Join-Path $env:TEMP ('TrayLeaf-ui-'+[Guid]::NewGuid().ToString('N'))}
$WorkPath=[IO.Path]::GetFullPath($WorkPath)
[IO.Directory]::CreateDirectory($WorkPath)|Out-Null
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$fx=Split-Path $csc
& $csc /nologo /target:winexe /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll ('/out:'+(Join-Path $WorkPath 'TrayLeaf.ControlledFixture.exe')) (Join-Path $PSScriptRoot 'ControlledFixture.cs')
if($LASTEXITCODE){throw 'Fixture compilation failed'}
$flags=@(('/win32manifest:'+(Join-Path $PSScriptRoot 'asInvoker.manifest')),'/nologo','/target:exe','/platform:x64','/main:Version088Tests','/utf8output',('/out:'+(Join-Path $WorkPath 'Version088Tests.exe')),('/resource:'+(Join-Path $project 'src\MainWindow.xaml')+',TrayLeaf.MainWindow.xaml'))
if(Test-Path -LiteralPath (Join-Path $project 'assets\TrayLeaf.ico')){
 $flags+='/win32icon:'+(Join-Path $project 'assets\TrayLeaf.ico')
 $flags+='/resource:'+(Join-Path $project 'assets\TrayLeaf.ico')+',TrayLeaf.Assets.TrayLeaf.ico'
 $flags+='/resource:'+(Join-Path $project 'assets\TrayLeaf.png')+',TrayLeaf.Assets.TrayLeaf.png'
}
foreach($dll in @('PresentationFramework','PresentationCore','WindowsBase')){$flags+='/r:'+(Join-Path $fx ('WPF\'+$dll+'.dll'))}
foreach($dll in @('System','System.Core','System.Management','System.Windows.Forms','System.Drawing','Microsoft.CSharp','System.Xaml','System.Xml.Linq')){$flags+='/r:'+(Join-Path $fx ($dll+'.dll'))}
$flags+=@(Get-ChildItem -LiteralPath (Join-Path $project 'src') -Filter '*.cs' | ForEach-Object FullName)
$flags+=Join-Path $PSScriptRoot 'Version088Tests.cs'
& $csc @flags
if($LASTEXITCODE){throw 'UI test compilation failed'}
if(Test-Path -LiteralPath (Join-Path $project 'TrayLeaf.config')){Copy-Item -LiteralPath (Join-Path $project 'TrayLeaf.config') -Destination (Join-Path $WorkPath 'TrayLeaf.config')}
& (Join-Path $WorkPath 'Version088Tests.exe') $WorkPath $project
if($LASTEXITCODE){throw ('UI tests failed: '+$LASTEXITCODE)}

