param([string]$WorkPath,[string]$SourcePath=(Split-Path $PSScriptRoot))
$ErrorActionPreference='Stop'
if(!$WorkPath){throw 'Provide an isolated WorkPath'}
[IO.Directory]::CreateDirectory($WorkPath)|Out-Null
$cl=Get-Command cl.exe -ErrorAction Stop
if(!$env:INCLUDE){$vs=($cl.Source -split '\\VC\\')[0];$tool=[regex]::Match($cl.Source,'MSVC\\(\d+\.\d+)').Groups[1].Value;$vc=Join-Path $vs 'VC\Auxiliary\Build\vcvarsall.bat'; & $env:ComSpec /d /c ('call "'+$vc+'" amd64 -vcvars_ver='+$tool+' >nul && set') | ForEach-Object {if($_ -match '^([^=]+)=(.*)$'){[Environment]::SetEnvironmentVariable($matches[1],$matches[2],'Process')}};if($LASTEXITCODE){throw 'MSVC init failed'}}
& $cl.Source /nologo /MT /EHsc /O2 /utf-8 /std:c++14 (Join-Path $SourcePath 'tests\Fix2DesktopTests.cpp') ('/Fo'+(Join-Path $WorkPath 'desktop.obj')) ('/Fe'+(Join-Path $WorkPath 'desktop-tests.exe'))
if($LASTEXITCODE){throw 'Native placement test build failed'}
& (Join-Path $WorkPath 'desktop-tests.exe');if($LASTEXITCODE){exit $LASTEXITCODE}
& $cl.Source /nologo /MT /EHsc /O2 /utf-8 /std:c++14 /DTRAYLEAF_HELPER /DTRAYLEAF_CLEANUP_TEST (Join-Path $SourcePath 'installer\InstallerProcessCloser.cpp') ('/Fo'+(Join-Path $WorkPath 'cleanup.obj')) /link /SUBSYSTEM:WINDOWS ('/OUT:'+(Join-Path $WorkPath 'cleanup-fixture.exe')) msi.lib advapi32.lib shell32.lib user32.lib ole32.lib oleaut32.lib uuid.lib shlwapi.lib taskschd.lib
if($LASTEXITCODE){throw 'Cleanup fixture build failed'}
foreach($case in @(@('--fixture-success',0),@('--fixture-stall',258),@('--remove-startup invalid-sid',1337))){$watch=[Diagnostics.Stopwatch]::StartNew();$p=Start-Process -FilePath (Join-Path $WorkPath 'cleanup-fixture.exe') -ArgumentList $case[0] -WindowStyle Hidden -Wait -PassThru;$watch.Stop();"NATIVE scenario=$($case[0]) exit=$($p.ExitCode) elapsed_ms=$($watch.ElapsedMilliseconds)";if($p.ExitCode -ne $case[1] -or $watch.ElapsedMilliseconds -gt 6000){throw 'Native cleanup deadline/exit incorrect'}}
'RESULT native_policy=6/6 cleanup_fixtures=3/3; no real startup task deleted'
