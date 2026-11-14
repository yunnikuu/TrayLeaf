param([Parameter(Mandatory=$true)][string]$MsiPath,[string]$ExpectedVersion='1.0.0')
$ErrorActionPreference='Stop';$pass=0;$fail=0
function Check($n,$b){if($b){$script:pass++;"PASS $n"}else{$script:fail++;"FAIL $n"}}
$ins=New-Object -ComObject WindowsInstaller.Installer;$db=$ins.OpenDatabase([IO.Path]::GetFullPath($MsiPath),0)
function Rows($sql,$cols){$v=$db.OpenView($sql);try{$v.Execute()|Out-Null;while($r=$v.Fetch()){$row=@();for($i=1;$i -le $cols;$i++){$row+=$r.StringData($i)};Write-Output -NoEnumerate $row}}finally{$v.Close()|Out-Null}}
$props=@{};foreach($r in (Rows 'SELECT `Property`,`Value` FROM `Property`' 2)){$props[$r[0]]=$r[1]}
Check 'installer-product-version' ($props.ProductVersion -eq $ExpectedVersion)
$ca=@(Rows 'SELECT `Type` FROM `CustomAction` WHERE `Action`=''ConfigureStartup''' 1)
Check 'startup-configuration-does-not-block-installer' ($ca.Count -eq 1 -and (([int]$ca[0][0] -band 192) -eq 192))
$seq=@{};foreach($r in (Rows 'SELECT `Action`,`Sequence` FROM `InstallExecuteSequence`' 2)){$seq[$r[0]]=[int]$r[1]}
Check 'startup-launched-only-after-commit' ($seq.ConfigureStartup -gt $seq.InstallFinalize)
Check 'transaction-and-costing-retained' (!$props.ContainsKey('DISABLEROLLBACK') -and $seq.ContainsKey('CostFinalize') -and $seq.ContainsKey('InstallInitialize') -and $seq.ContainsKey('InstallFinalize'))
$tables=@(Rows 'SELECT `Name` FROM `_Tables` WHERE `Name`=''ActionText''' 1);$actions=@{}
if($tables.Count){foreach($r in (Rows 'SELECT `Action`,`Description`,`Template` FROM `ActionText`' 3)){$actions[$r[0]]=$r[1]}}
Check 'actiontext-table-present' ($tables.Count -eq 1)
Check 'all-execution-actions-have-status-text' (@($seq.Keys|Where-Object {!$actions.ContainsKey($_)}).Count -eq 0)
Check 'action-status-descriptions-english' ($actions.Count -gt 0 -and @($actions.Values|Where-Object {$_ -match '[\u3400-\u9fff]' -or $_ -notmatch '[A-Za-z]'}).Count -eq 0)
$controls=@{};foreach($r in (Rows 'SELECT `Control`,`Text`,`Y`,`Width`,`Height` FROM `Control` WHERE `Dialog_`=''TrayLeafProgressDlg''' 5)){$controls[$r[0]]=$r}
Check 'initial-status-in-english' ($controls.ActionText[1] -eq 'Preparing installation...')
Check 'status-label-remains-chinese' ($controls.StatusLabel[1] -eq '状态：')
Check 'status-aligned-and-wide' ($controls.StatusLabel[2] -eq $controls.ActionText[2] -and [int]$controls.ActionText[3] -ge 290 -and [int]$controls.ActionText[4] -ge 26)
$mapping=@(Rows 'SELECT `Event`,`Attribute` FROM `EventMapping` WHERE `Dialog_`=''TrayLeafProgressDlg'' AND `Control_`=''ActionText''' 2)
Check 'real-action-notifications-retained' (@($mapping|Where-Object {$_[0] -eq 'ActionText' -and $_[1] -eq 'Text'}).Count -eq 1)
Check 'diagnostic-action-logging-enabled' ($props.MsiLogging -match 'a' -and $props.MsiLogging -match 'e')
"RESULT passed=$pass failed=$fail";if($fail){exit 1}
