param([string]$WorkPath,[string]$Project,[string]$TestSource,[string]$Entry="Version032FeatureTests")
$ErrorActionPreference='Stop'
if(!$Project){$Project=Split-Path $PSScriptRoot}
if(!$TestSource){$TestSource=Join-Path $PSScriptRoot ($Entry+'.cs')}
if(!$WorkPath){$WorkPath=Join-Path $env:TEMP ('TrayLeaf-ui-'+[Guid]::NewGuid().ToString('N'))}
$WorkPath=[IO.Path]::GetFullPath($WorkPath)
[IO.Directory]::CreateDirectory($WorkPath)|Out-Null
& (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe') /nologo /target:winexe /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll ('/out:'+(Join-Path $WorkPath 'TrayLeaf.ControlledFixture.exe')) (Join-Path $Project 'tests\ControlledFixture.cs')
if($LASTEXITCODE){throw 'Fixture compilation failed'}
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$fx=Split-Path $csc
$flags=@(('/win32manifest:'+(Join-Path $Project 'tests\asInvoker.manifest')),'/nologo','/target:exe','/platform:x64',('/main:'+$Entry),'/utf8output',('/out:'+(Join-Path $WorkPath 'Version032Tests.exe')),('/resource:'+(Join-Path $project 'src\MainWindow.xaml')+',TrayLeaf.MainWindow.xaml'))
if(Test-Path -LiteralPath (Join-Path $project 'assets\TrayLeaf.ico')){
 $flags+='/win32icon:'+(Join-Path $project 'assets\TrayLeaf.ico')
 $flags+='/resource:'+(Join-Path $project 'assets\TrayLeaf.ico')+',TrayLeaf.Assets.TrayLeaf.ico'
 $flags+='/resource:'+(Join-Path $project 'assets\TrayLeaf.png')+',TrayLeaf.Assets.TrayLeaf.png'
}
foreach($dll in @('PresentationFramework','PresentationCore','WindowsBase')){$flags+='/r:'+(Join-Path $fx ('WPF\'+$dll+'.dll'))}
foreach($dll in @('System','System.Core','System.Management','System.Windows.Forms','System.Drawing','Microsoft.CSharp','System.Xaml')){$flags+='/r:'+(Join-Path $fx ($dll+'.dll'))}
$flags+=@(Get-ChildItem -LiteralPath (Join-Path $project 'src') -Filter '*.cs' | ForEach-Object FullName)
$flags+=$TestSource
& $csc @flags
if($LASTEXITCODE){throw 'UI test compilation failed'}
if(Test-Path -LiteralPath (Join-Path $project 'TrayLeaf.exe.config')){Copy-Item -LiteralPath (Join-Path $project 'TrayLeaf.exe.config') -Destination (Join-Path $WorkPath 'Version032Tests.exe.config')}
& (Join-Path $WorkPath 'Version032Tests.exe') $WorkPath
if($LASTEXITCODE){throw ('UI tests failed: '+$LASTEXITCODE)}

