param([Parameter(Mandatory=$true)][string]$MsiPath,[string]$ExpectedVersion='1.0.0')
$ErrorActionPreference='Stop';$passed=0;$failed=0
function Check($n,$b){if($b){$script:passed++;"PASS $n"}else{$script:failed++;"FAIL $n"}}
$i=New-Object -ComObject WindowsInstaller.Installer;$db=$i.OpenDatabase([IO.Path]::GetFullPath($MsiPath),0)
function Rows($sql,$n){$v=$db.OpenView($sql);try{$v.Execute()|Out-Null;while($r=$v.Fetch()){$row=@();for($x=1;$x -le $n;$x++){$row+=$r.StringData($x)};Write-Output -NoEnumerate $row}}finally{$v.Close()|Out-Null}}
$props=@{};foreach($r in (Rows 'SELECT `Property`,`Value` FROM `Property`' 2)){$props[$r[0]]=$r[1]}
$seq=@{};foreach($r in (Rows 'SELECT `Action`,`Sequence`,`Condition` FROM `InstallExecuteSequence`' 3)){$seq[$r[0]]=$r}
Check 'installer-product-version' ($props.ProductVersion -eq $ExpectedVersion)
$action=@(Rows 'SELECT `Source`,`Target`,`Type` FROM `CustomAction` WHERE `Action`=''CloseTrayLeafForInstall''' 3)
Check 'close-action-is-native-embedded-checked-DLL' ($action.Count -eq 1 -and $action[0][0] -eq 'ProcessCloserBinary' -and $action[0][1] -eq 'CloseTrayLeafForInstall' -and $action[0][2] -eq '1')
Check 'close-runs-before-own-validation' ($seq.ContainsKey('CloseTrayLeafForInstall') -and [int]$seq.CloseTrayLeafForInstall[1] -lt [int]$seq.InstallValidate[1])
Check 'close-runs-before-old-package-uninstall' ($seq.ContainsKey('CloseTrayLeafForInstall') -and [int]$seq.CloseTrayLeafForInstall[1] -lt [int]$seq.RemoveExistingProducts[1])
Check 'elevated-shutdown-helper-embedded-not-installed' (@(Rows 'SELECT `Name` FROM `Binary` WHERE `Name`=''ShutdownHelperBinary''' 1).Count -eq 1)
Check 'old-install-path-resolved-even-when-new-path-changes' (@(Rows 'SELECT `Property` FROM `AppSearch` WHERE `Property`=''TRAYLEAF_PREVIOUS_FOLDER''' 1).Count -eq 1)
$status=@(Rows 'SELECT `Description` FROM `ActionText` WHERE `Action`=''CloseTrayLeafForInstall''' 1)
Check 'close-status-english' ($status.Count -eq 1 -and $status[0][0] -eq 'Closing running TrayLeaf before validation...')
Check 'silent-force-close-requires-explicit-consent-property' ($props.ContainsKey('SecureCustomProperties') -and $props.SecureCustomProperties.Contains('TRAYLEAF_CLOSE_RUNNING'))
$files=@(Rows 'SELECT `File` FROM `File`' 1)
Check 'only-three-user-payload-files' ($files.Count -eq 3)
Check 'transaction-kept' (!$props.ContainsKey('DISABLEROLLBACK') -and $seq.ContainsKey('InstallInitialize') -and $seq.ContainsKey('InstallFinalize'))
"RESULT passed=$passed failed=$failed";if($failed){exit 1}
