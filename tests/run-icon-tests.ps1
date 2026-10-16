param([Parameter(Mandatory=$true)][string]$ProjectRoot,[Parameter(Mandatory=$true)][string]$WorkPath,[Parameter(Mandatory=$true)][string]$ExpectedIco,[Parameter(Mandatory=$true)][string]$ExpectedPng)
$ErrorActionPreference='Stop'
Import-Module Microsoft.PowerShell.Utility
$ProjectRoot=[IO.Path]::GetFullPath($ProjectRoot)
$WorkPath=[IO.Path]::GetFullPath($WorkPath)
[IO.Directory]::CreateDirectory($WorkPath)|Out-Null
$exe=Join-Path $WorkPath 'TrayLeaf.exe'
& (Join-Path $ProjectRoot 'build.ps1') -Diagnostic -OutputPath $exe
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$fx=Split-Path $csc
$flags=@('/nologo','/target:exe','/platform:x64','/utf8output',('/win32manifest:'+(Join-Path $ProjectRoot 'tests\asInvoker.manifest')),('/out:'+(Join-Path $WorkPath 'IconTests.exe')),('/r:'+$exe))
foreach($dll in @('PresentationFramework','PresentationCore','WindowsBase')){$flags+='/r:'+(Join-Path $fx ('WPF\'+$dll+'.dll'))}
foreach($dll in @('System','System.Core','System.Drawing','System.Windows.Forms','System.Xaml')){$flags+='/r:'+(Join-Path $fx ($dll+'.dll'))}
$flags+=Join-Path $PSScriptRoot 'IconTests.cs'
& $csc @flags
if($LASTEXITCODE){throw 'Icon test compilation failed'}
Copy-Item -LiteralPath ($exe+'.config') -Destination (Join-Path $WorkPath 'IconTests.exe.config') -Force
& (Join-Path $WorkPath 'IconTests.exe') $ExpectedIco $ExpectedPng $WorkPath
exit $LASTEXITCODE
