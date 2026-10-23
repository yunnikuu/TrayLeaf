param([Parameter(Mandatory=$true)][string]$MsiPath)
$ErrorActionPreference='Stop'
$passed=0;$failed=0
function Check([string]$Name,[bool]$Ok){if($Ok){$script:passed++;Write-Output ('PASS '+$Name)}else{$script:failed++;Write-Output ('FAIL '+$Name)}}
$installer=New-Object -ComObject WindowsInstaller.Installer
$db=$installer.OpenDatabase([IO.Path]::GetFullPath($MsiPath),0)
function Rows([string]$Sql,[int]$Count){$v=$db.OpenView($Sql);$null=$v.Execute();try{while($r=$v.Fetch()){$row=@();for($i=1;$i -le $Count;$i++){$row+=$r.StringData($i)};Write-Output -NoEnumerate $row}}finally{$null=$v.Close()}}
$events=@(Rows 'SELECT `Event`,`Argument`,`Ordering` FROM `ControlEvent` WHERE `Dialog_`=''TrayLeafInstallDirDlg'' AND `Control_`=''ChangeFolder'' ORDER BY `Ordering`' 3)
Check 'native-picker-action-on-change-folder' ($events.Count -ge 2 -and $events[0][0] -eq 'DoAction' -and $events[0][1] -eq 'BrowseInstallFolder')
Check 'selected-path-published-by-UI' ($events.Count -eq 3 -and $events[1][0] -eq '[INSTALLFOLDER]' -and $events[1][1] -eq '[TRAYLEAF_SELECTED_FOLDER]' -and $events[1][2] -eq '2')
Check 'selected-target-committed' ($events.Count -eq 3 -and $events[2][0] -eq 'SetTargetPath' -and $events[2][1] -eq '[WIXUI_INSTALLDIR]' -and $events[2][2] -eq '3')
Check 'browse-does-not-recreate-active-dialog-2856' (@($events | Where-Object {$_[0] -in @('NewDialog','SpawnDialog')}).Count -eq 0)
$conditions=@(Rows 'SELECT `Condition` FROM `ControlEvent` WHERE `Dialog_`=''TrayLeafInstallDirDlg'' AND `Control_`=''ChangeFolder'' AND `Ordering`>1' 1)
Check 'cancel-does-not-publish-path' ($conditions.Count -eq 2 -and @($conditions|Where-Object {$_[0] -ne 'TRAYLEAF_SELECTED_FOLDER'}).Count -eq 0)
Check 'browse-never-starts-install' (@($events | Where-Object {$_[0] -eq 'EndDialog'}).Count -eq 0)
$actions=@(Rows 'SELECT `Type`,`Source`,`Target` FROM `CustomAction` WHERE `Action`=''BrowseInstallFolder''' 3)
Check 'native-DLL-custom-action' ($actions.Count -eq 1 -and $actions[0][0] -eq '1' -and $actions[0][1] -eq 'FolderPickerBinary' -and $actions[0][2] -eq 'BrowseInstallFolder')
Check 'picker-embedded-in-MSI' (@(Rows 'SELECT `Name` FROM `Binary` WHERE `Name`=''FolderPickerBinary''' 1).Count -eq 1)
Check 'legacy-picker-not-present' (@(Rows 'SELECT `Dialog` FROM `Dialog` WHERE `Dialog`=''TrayLeafBrowseDlg''' 1).Count -eq 0)
$next=@(Rows 'SELECT `Event`,`Argument`,`Ordering` FROM `ControlEvent` WHERE `Dialog_`=''TrayLeafInstallDirDlg'' AND `Control_`=''Next'' ORDER BY `Ordering`' 3)
Check 'typed-folder-normalizers-kept' (@($next|Where-Object {$_[0] -eq 'DoAction' -and $_[1] -like 'AppendProductFolder*'}).Count -eq 2)
Check 'next-goes-directly-to-install' (@($next|Where-Object {$_[0] -eq 'EndDialog' -and $_[1] -eq 'Return'}).Count -eq 1 -and @($next|Where-Object {$_[0] -eq 'NewDialog'}).Count -eq 0)
Check 'three-install-options-kept' (@(Rows 'SELECT `Control` FROM `Control` WHERE `Dialog_`=''TrayLeafInstallDirDlg'' AND `Type`=''CheckBox''' 1).Count -eq 3)
$props=@(Rows 'SELECT `Property`,`Value` FROM `Property`' 2);$p=@{};foreach($r in $props){$p[$r[0]]=$r[1]}
Check 'version-092' ($p['ProductVersion'] -eq '0.9.5')
Check 'upgrade-code-stable' ($p['UpgradeCode'] -eq '{49F5C6BF-41D9-5CF1-8FFA-917E45CCB2DC}')
Check 'remembered-path-search-kept' (@(Rows 'SELECT `Property` FROM `AppSearch` WHERE `Property`=''INSTALLFOLDER''' 1).Count -eq 1)
Check 'only-three-runtime-payloads' (@(Rows 'SELECT `File` FROM `File`' 1).Count -eq 3)
Write-Output ('RESULT passed='+$passed+' failed='+$failed)
if($failed){exit 1}
