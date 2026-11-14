param([Parameter(Mandatory=$true)][string]$MsiPath)
$ErrorActionPreference='Stop';$pass=0;$fail=0
function Check($n,$b){if($b){$script:pass++;"PASS $n"}else{$script:fail++;"FAIL $n"}}
$ins=New-Object -ComObject WindowsInstaller.Installer;$db=$ins.OpenDatabase([IO.Path]::GetFullPath($MsiPath),0)
function Rows($sql,$cols){$v=$db.OpenView($sql);try{$v.Execute()|Out-Null;while($r=$v.Fetch()){$row=@();for($i=1;$i -le $cols;$i++){$row+=$r.StringData($i)};Write-Output -NoEnumerate $row}}finally{$v.Close()|Out-Null}}
$p=@(Rows 'SELECT `Value` FROM `Property` WHERE `Property`=''WIXUI_EXITDIALOGOPTIONALCHECKBOX''' 1)
Check 'launch-checked-by-default' ($p.Count -eq 1 -and $p[0][0] -eq '1')
$c=@(Rows 'SELECT `Type`,`Property`,`Text` FROM `Control` WHERE `Dialog_`=''TrayLeafExitDialog'' AND `Control`=''Launch''' 3)
Check 'completion-checkbox-text-and-property' ($c.Count -eq 1 -and $c[0][0] -eq 'CheckBox' -and $c[0][1] -eq 'WIXUI_EXITDIALOGOPTIONALCHECKBOX' -and $c[0][2] -eq '启动轻羽归栈')
$events=@(Rows 'SELECT `Event`,`Argument`,`Condition`,`Ordering` FROM `ControlEvent` WHERE `Dialog_`=''TrayLeafExitDialog'' AND `Control_`=''Finish''' 4)
$launch=@($events|Where-Object {$_[1] -eq 'LaunchOnFinish'});$place=@($events|Where-Object {$_[1] -eq 'PlaceDesktopShortcutAtEnd'})
Check 'finish-launch-requires-checked-option' ($launch.Count -eq 1 -and $launch[0][2] -match 'WIXUI_EXITDIALOGOPTIONALCHECKBOX="1"')
Check 'finish-no-silent-uninstall-or-admin-launch' ($launch[0][2] -match 'UILevel >= 5' -and $launch[0][2] -match 'NOT REMOVE' -and $launch[0][2] -match 'ACTION="INSTALL"')
Check 'desktop-action-conditional-and-before-launch' ($place.Count -eq 1 -and $place[0][2] -match 'DESKTOP_SHORTCUT="1"' -and [int]$place[0][3] -lt [int]$launch[0][3])
$execute=@(Rows 'SELECT `Action` FROM `InstallExecuteSequence` WHERE `Action`=''LaunchOnFinish'' OR `Action`=''PlaceDesktopShortcutAtEnd''' 1)
Check 'finish-actions-never-in-execute-sequence' ($execute.Count -eq 0)
$seq=@(Rows 'SELECT `Condition`,`Sequence` FROM `InstallUISequence` WHERE `Action`=''TrayLeafExitDialog''' 2)
Check 'completion-only-on-success' ($seq.Count -eq 1 -and $seq[0][1] -eq '-1')
$hide=@(Rows 'SELECT `Condition` FROM `ControlCondition` WHERE `Dialog_`=''TrayLeafExitDialog'' AND `Control_`=''Launch''' 1)
Check 'checkbox-available-on-same-msi-repair' (@($hide|Where-Object {$_[0] -match 'Installed'}).Count -eq 0)
"RESULT passed=$pass failed=$fail";if($fail){exit 1}
