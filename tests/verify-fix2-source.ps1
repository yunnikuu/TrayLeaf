param([string]$SourcePath=(Split-Path $PSScriptRoot))
$ErrorActionPreference='Stop';$pass=0;$fail=0
function Check($n,$b){if($b){$script:pass++;"PASS $n"}else{$script:fail++;"FAIL $n"}}
$wx=[IO.File]::ReadAllText((Join-Path $SourcePath 'installer\TrayLeaf.wxs'))
$cpp=[IO.File]::ReadAllText((Join-Path $SourcePath 'installer\InstallerProcessCloser.cpp'))
$chrome=[IO.File]::ReadAllText((Join-Path $SourcePath 'src\ModernChrome.cs'))
$glass=Join-Path $SourcePath 'src\DesktopGlass.cs'
Check 'maintenance-has-purpose-built-two-action-page' ($wx -match 'Dialog Id="TrayLeafMaintenanceTypeDlg"')
Check 'maintenance-reinstall-label' ($wx -match 'Text="重新安装')
Check 'uninstall-completion-message' ($wx -match 'TrayLeaf 已成功从您的电脑上移除')
Check 'uninstall-cleanup-is-native-not-installed-wpf-exe' ($wx -match 'Id="RemoveStartup" BinaryKey="ShutdownHelperBinary"')
Check 'uninstall-cleanup-bounded-native-helper' ($cpp -match 'RemoveStartupWithDeadline' -and $cpp -match 'CleanupStartupTask')
Check 'desktop-existing-position-captured-before-install' ($wx -match 'CaptureDesktopShortcut' -and $cpp -match 'TRAYLEAF_DESKTOP_EXISTS')
Check 'desktop-existing-position-not-moved-to-tail' ($cpp -match 'TRAYLEAF_DESKTOP_X' -and $cpp -match 'Restored existing TrayLeaf shortcut position')
Check 'glass-has-off-thread-desktop-sampler' (Test-Path -LiteralPath $glass)
Check 'rounding-uses-native-physical-window-size' ($chrome -match 'GetWindowRect')
Check 'single-custom-corner-policy' ($chrome.Contains('CornerRadius=new CornerRadius(0)'))
Check 'display-release-fix3' ([IO.File]::ReadAllText((Join-Path $SourcePath 'src\AppIdentity.cs')).Contains('AssemblyInformationalVersion("1.0.0-fix3")'))
Check 'per-pixel-transparent-window' ([IO.File]::ReadAllText((Join-Path $SourcePath 'src\MainWindow.xaml')).Contains('AllowsTransparency="True"'))
Check 'content-sized-close-prompt' (Test-Path -LiteralPath (Join-Path $SourcePath 'src\CloseWindowDialog.cs'))
"RESULT passed=$pass failed=$fail";if($fail){exit 1}
