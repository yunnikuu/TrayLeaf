param([Parameter(Mandatory=$true)][string]$MsiPath)
$ErrorActionPreference='Stop';$pass=0;$fail=0
function Check($n,$b){if($b){$script:pass++;"PASS $n"}else{$script:fail++;"FAIL $n"}}
$installer=New-Object -ComObject WindowsInstaller.Installer;$db=$installer.OpenDatabase([IO.Path]::GetFullPath($MsiPath),0)
function Rows($sql,$count){$view=$db.OpenView($sql);try{$view.Execute()|Out-Null;while($row=$view.Fetch()){$values=@();for($i=1;$i -le $count;$i++){$values+=$row.StringData($i)};Write-Output -NoEnumerate $values}}finally{$view.Close()|Out-Null;[Runtime.InteropServices.Marshal]::FinalReleaseComObject($view)|Out-Null}}
try{
 $controls=@(Rows 'SELECT `Control`,`Type`,`X`,`Y`,`Width`,`Height`,`Attributes`,`Text` FROM `Control` WHERE `Dialog_`=''TrayLeafExitDialog''' 8)
 $launch=@($controls|Where-Object{$_[0] -eq 'Launch'})
 Check 'launch-checkbox-exists-on-completion' ($launch.Count -eq 1 -and $launch[0][1] -eq 'CheckBox')
 $c=$launch[0];Check 'launch-initially-visible-and-enabled' (([int]$c[6] -band 3) -eq 3)
 $overlaps=@($controls|Where-Object{$_[1] -eq 'Bitmap' -and [int]$_[2] -lt ([int]$c[2]+[int]$c[4]) -and ([int]$_[2]+[int]$_[4]) -gt [int]$c[2] -and [int]$_[3] -lt ([int]$c[3]+[int]$c[5]) -and ([int]$_[3]+[int]$_[5]) -gt [int]$c[3]})
 Check 'no-bitmap-can-overpaint-launch-checkbox' ($overlaps.Count -eq 0)
 $art=@($controls|Where-Object{$_[0] -eq 'Bitmap'});Check 'sidebar-art-stays-left-of-content' ($art.Count -eq 1 -and ([int]$art[0][2]+[int]$art[0][4]) -le 125)
 $condition=@(Rows 'SELECT `Action`,`Condition` FROM `ControlCondition` WHERE `Dialog_`=''TrayLeafExitDialog'' AND `Control_`=''Launch''' 2)
 Check 'launch-explicitly-shown-for-install-and-reinstall' (@($condition|Where-Object{$_[0] -eq 'Show' -and $_[1] -eq 'NOT REMOVE~="ALL"'}).Count -eq 1)
 Check 'launch-hidden-for-uninstall' (@($condition|Where-Object{$_[0] -eq 'Hide' -and $_[1] -eq 'REMOVE~="ALL"'}).Count -eq 1)
 $binary=@(Rows 'SELECT `Name` FROM `Binary` WHERE `Name`=''TrayLeafExitSidebar''' 1);Check 'cropped-sidebar-art-is-embedded' ($binary.Count -eq 1)
 $events=@(Rows 'SELECT `Event`,`Argument` FROM `ControlEvent` WHERE `Dialog_`=''TrayLeafExitDialog''' 2)
 Check 'no-dialog-initialization-custom-action-or-delay' (@($events|Where-Object{$_[0] -eq 'DoAction' -and $_[1] -notin @('PlaceDesktopShortcutAtEnd','LaunchOnFinish')}).Count -eq 0)
 # Model install/reinstall/removal visibility. Fail on unrecognized conditions.
 $allConditions=@(Rows 'SELECT `Control_`,`Action`,`Condition` FROM `ControlCondition` WHERE `Dialog_`=''TrayLeafExitDialog''' 3)
 function IsVisible($control,[bool]$removing){
  $visible=(([int]$control[6] -band 1) -eq 1)
  foreach($rule in $allConditions|Where-Object{$_[0] -eq $control[0]}){
   $expression=$rule[2].Trim()
   if($expression -eq 'REMOVE~="ALL"'){$matches=$removing}
   elseif($expression -eq 'NOT REMOVE~="ALL"'){$matches=!$removing}
   else{throw ('Unsupported completion visibility condition: '+$expression)}
   if($matches -and $rule[1] -eq 'Show'){$visible=$true}
   if($matches -and $rule[1] -eq 'Hide'){$visible=$false}
  }
  return $visible
 }
 function Intersects($a,$b){return ([int]$a[2] -lt ([int]$b[2]+[int]$b[4]) -and ([int]$a[2]+[int]$a[4]) -gt [int]$b[2] -and [int]$a[3] -lt ([int]$b[3]+[int]$b[5]) -and ([int]$a[3]+[int]$a[5]) -gt [int]$b[3])}
 foreach($id in @('RemovedTitle','RemovedDescription')){
  $control=@($controls|Where-Object{$_[0] -eq $id})[0]
  Check ($id+'-hidden-before-first-condition-evaluation') (([int]$control[6] -band 1) -eq 0)
 }
 foreach($mode in @('install','reinstall','uninstall')){
  $removing=($mode -eq 'uninstall')
  $visible=@($controls|Where-Object{IsVisible $_ $removing})
  $texts=@($visible|Where-Object{$_[1] -eq 'Text'})
  $titles=@($texts|Where-Object{$_[0] -in @('Title','RemovedTitle')})
  $descriptions=@($texts|Where-Object{$_[0] -in @('Description','RemovedDescription')})
  $expectedTitle=if($removing){'RemovedTitle'}else{'Title'}
  $expectedDescription=if($removing){'RemovedDescription'}else{'Description'}
  Check ($mode+'-exactly-one-correct-title') ($titles.Count -eq 1 -and $titles[0][0] -eq $expectedTitle)
  Check ($mode+'-exactly-one-correct-description') ($descriptions.Count -eq 1 -and $descriptions[0][0] -eq $expectedDescription)
  Check ($mode+'-launch-visible-only-when-installing') (@($visible|Where-Object{$_[0] -eq 'Launch'}).Count -eq $(if($removing){0}else{1}))
  $overlapping=0
  for($a=0;$a -lt $texts.Count;$a++){for($b=$a+1;$b -lt $texts.Count;$b++){if(Intersects $texts[$a] $texts[$b]){$overlapping++}}}
  Check ($mode+'-visible-text-rectangles-do-not-overlap') ($overlapping -eq 0)
  $artOverlaps=0
  foreach($art in $visible|Where-Object{$_[1] -eq 'Bitmap'}){foreach($textControl in $texts){if(Intersects $art $textControl){$artOverlaps++}}}
  Check ($mode+'-artwork-does-not-overpaint-text') ($artOverlaps -eq 0)
  Check ($mode+'-text-contained-in-content-area') (@($texts|Where-Object{[int]$_[2] -lt 125 -or ([int]$_[2]+[int]$_[4]) -gt 370 -or [int]$_[3] -lt 0 -or ([int]$_[3]+[int]$_[5]) -gt 234}).Count -eq 0)
  Write-Output ('VISIBLE_TEXT '+$mode+'='+ (($texts|ForEach-Object{$_[0]+':'+$_[7]}) -join ' | '))
 }
}finally{[Runtime.InteropServices.Marshal]::FinalReleaseComObject($db)|Out-Null;[Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer)|Out-Null}
"RESULT passed=$pass failed=$fail";if($fail){exit 1};exit 0
