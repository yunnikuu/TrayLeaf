param([string]$SourcePath,[string]$WorkPath)
$ErrorActionPreference='Stop';New-Item -ItemType Directory -Path $WorkPath -Force|Out-Null
$csc="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe";$fx=Split-Path $csc
$flags=@('/nologo','/target:exe','/platform:x64','/optimize+','/utf8output',('/win32manifest:'+(Join-Path $SourcePath 'tests\asInvoker.manifest')),('/out:'+(Join-Path $WorkPath 'GlassDragTests.exe')))
foreach($dll in @('PresentationFramework','PresentationCore','WindowsBase')){$flags+='/r:'+(Join-Path $fx ('WPF\'+$dll+'.dll'))}
$flags+='/r:System.dll';$flags+='/r:System.Core.dll';$flags+='/r:System.Xaml.dll';$flags+=Join-Path $SourcePath 'src\DesktopGlass.cs';$flags+=Join-Path $PSScriptRoot 'GlassDragTests.cs'
& $csc @flags;if($LASTEXITCODE){exit $LASTEXITCODE}
& (Join-Path $WorkPath 'GlassDragTests.exe');exit $LASTEXITCODE
