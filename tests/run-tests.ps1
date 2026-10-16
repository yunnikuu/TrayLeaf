param([string]$WorkPath)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot
if(!$WorkPath){$WorkPath=Join-Path $env:TEMP ('TrayLeaf-tests-'+[Guid]::NewGuid().ToString('N'))}
$WorkPath=[IO.Path]::GetFullPath($WorkPath)
[IO.Directory]::CreateDirectory($WorkPath)|Out-Null
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$fx=Split-Path $csc
& $csc /nologo /target:winexe /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll ('/out:'+(Join-Path $WorkPath 'TrayLeaf.ControlledFixture.exe')) (Join-Path $PSScriptRoot 'ControlledFixture.cs')
if($LASTEXITCODE){throw 'Fixture compilation failed'}
$flags=@('/nologo','/target:exe','/platform:x64','/main:IntegrationTests','/utf8output',('/out:'+(Join-Path $WorkPath 'IntegrationTests.exe')))
if(Test-Path -LiteralPath (Join-Path $project 'assets\TrayLeaf.ico')){
 $flags+='/win32icon:'+(Join-Path $project 'assets\TrayLeaf.ico')
 $flags+='/resource:'+(Join-Path $project 'assets\TrayLeaf.ico')+',TrayLeaf.Assets.TrayLeaf.ico'
 $flags+='/resource:'+(Join-Path $project 'assets\TrayLeaf.png')+',TrayLeaf.Assets.TrayLeaf.png'
}
foreach($dll in @('PresentationFramework','PresentationCore','WindowsBase')){$flags+='/r:'+(Join-Path $fx ('WPF\'+$dll+'.dll'))}
foreach($dll in @('System','System.Core','System.Management','System.Windows.Forms','System.Drawing','Microsoft.CSharp','System.Xaml')){$flags+='/r:'+(Join-Path $fx ($dll+'.dll'))}
$flags+=@(Get-ChildItem -LiteralPath (Join-Path $project 'src') -Filter '*.cs' | ForEach-Object FullName)
$flags+=Join-Path $PSScriptRoot 'IntegrationTests.cs'
& $csc @flags
if($LASTEXITCODE){throw 'Test compilation failed'}
& (Join-Path $WorkPath 'IntegrationTests.exe') $WorkPath
if($LASTEXITCODE){throw ('Tests failed: '+$LASTEXITCODE)}
