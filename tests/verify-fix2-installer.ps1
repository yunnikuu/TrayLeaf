param([Parameter(Mandatory=$true)][string]$MsiPath,[string]$ReportPath)
$ErrorActionPreference='Stop';$pass=0;$fail=0
function Check($n,$b){if($b){$script:pass++;"PASS $n"}else{$script:fail++;"FAIL $n"}}
$ins=New-Object -ComObject WindowsInstaller.Installer;$db=$ins.OpenDatabase([IO.Path]::GetFullPath($MsiPath),0)
function Rows($sql,$n){$v=$db.OpenView($sql);try{$v.Execute()|Out-Null;while($r=$v.Fetch()){$row=@();for($i=1;$i -le $n;$i++){$row+=$r.StringData($i)};Write-Output -NoEnumerate $row}}finally{$v.Close()|Out-Null}}
$p=@{};foreach($r in (Rows 'SELECT `Property`,`Value` FROM `Property`' 2)){$p[$r[0]]=$r[1]}
Check 'release-suffix-preserved' ($p.TRAYLEAF_RELEASE_VERSION -eq '1.0.0-fix3')
Check 'msi-numeric-version-compatible' ($p.ProductVersion -eq '1.0.0')
Check 'upgrade-identity-preserved' ($p.UpgradeCode -eq '{49F5C6BF-41D9-5CF1-8FFA-917E45CCB2DC}')
$controls=@(Rows 'SELECT `Dialog_`,`Control`,`Type`,`Text` FROM `Control`' 4)
$c=@($controls|Where-Object{$_[0] -eq 'TrayLeafMaintenanceTypeDlg'})
Check 'no-change-operation' (@($c|Where-Object{$_[1] -match 'Change' -or $_[3] -match '更改'}).Count -eq 0)
Check 'reinstall-caption' (@($c|Where-Object{$_[1] -eq 'RepairButton' -and $_[3] -match '重新安装'}).Count -eq 1)
Check 'normal-remove-caption' (@($c|Where-Object{$_[1] -eq 'RemoveButton' -and $_[3] -match '删除'}).Count -eq 1)
Check 'old-maintenance-dialog-not-linked' (@($controls|Where-Object{$_[0] -eq 'MaintenanceTypeDlg'}).Count -eq 0)
$events=@(Rows 'SELECT `Dialog_`,`Control_`,`Event`,`Argument`,`Condition`,`Ordering` FROM `ControlEvent`' 6)
Check 'reinstall-overwrites-all-files' (@($events|Where-Object{$_[0] -eq 'TrayLeafMaintenanceTypeDlg' -and $_[1] -eq 'RepairButton' -and $_[2] -eq '[REINSTALL]' -and $_[3] -eq 'ALL'}).Count -eq 1)
Check 'remove-clears-reinstall' (@($events|Where-Object{$_[0] -eq 'TrayLeafMaintenanceTypeDlg' -and $_[1] -eq 'RemoveButton' -and $_[2] -eq '[REINSTALL]' -and $_[3] -eq '{}'}).Count -eq 1)
Check 'remove-selects-all' (@($events|Where-Object{$_[0] -eq 'TrayLeafMaintenanceTypeDlg' -and $_[1] -eq 'RemoveButton' -and $_[2] -eq '[REMOVE]' -and $_[3] -eq 'ALL'}).Count -eq 1)
Check 'uninstall-success-message' (@($controls|Where-Object{$_[1] -eq 'RemovedDescription' -and $_[3] -eq 'TrayLeaf 已成功从您的电脑上移除'}).Count -eq 1)
$cc=@(Rows 'SELECT `Dialog_`,`Control_`,`Action`,`Condition` FROM `ControlCondition`' 4)
Check 'uninstall-hides-launch-checkbox' (@($cc|Where-Object{$_[0] -eq 'TrayLeafExitDialog' -and $_[1] -eq 'Launch' -and $_[2] -eq 'Hide' -and $_[3] -eq 'REMOVE~="ALL"'}).Count -eq 1)
$ca=@(Rows 'SELECT `Action`,`Type`,`Source`,`Target` FROM `CustomAction`' 4);$clean=@($ca|Where-Object{$_[0] -eq 'RemoveStartup'})
Check 'native-deferred-nonimpersonated-cleanup' ($clean.Count -eq 1 -and [int]$clean[0][1] -band 1024 -and [int]$clean[0][1] -band 2048 -and $clean[0][2] -eq 'ShutdownHelperBinary' -and $clean[0][3] -eq '--remove-startup "[UserSID]"')
$ui=@(Rows 'SELECT `Action`,`Condition`,`Sequence` FROM `InstallUISequence`' 3)
Check 'desktop-position-captured-before-install' (@($ui|Where-Object{$_[0] -eq 'CaptureDesktopShortcut'}).Count -eq 1)
Check 'finish-placement-skips-uninstall' (@($events|Where-Object{$_[2] -eq 'DoAction' -and $_[3] -eq 'PlaceDesktopShortcutAtEnd' -and $_[4] -match 'NOT REMOVE'}).Count -eq 1)
Check 'launch-skips-uninstall' (@($events|Where-Object{$_[2] -eq 'DoAction' -and $_[3] -eq 'LaunchOnFinish' -and $_[4] -match 'NOT REMOVE'}).Count -eq 1)
if($ReportPath){$p|ConvertTo-Json|Set-Content -LiteralPath $ReportPath -Encoding UTF8}
"RESULT passed=$pass failed=$fail";if($fail){exit 1}
