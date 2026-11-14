param(
 [Parameter(Mandatory=$true)][string]$BaselineMsi,
 [Parameter(Mandatory=$true)][string]$CandidateMsi,
 [Parameter(Mandatory=$true)][string]$WorkDir
)
$ErrorActionPreference='Stop';$pass=0;$fail=0
function Check($name,$ok){if($ok){$script:pass++;"PASS $name"}else{$script:fail++;"FAIL $name"}}
$root=[IO.Path]::GetFullPath($WorkDir);[IO.Directory]::CreateDirectory($root)|Out-Null
$id=[guid]::NewGuid().ToString('N');$upgrade='{'+[guid]::NewGuid().ToString().ToUpper()+'}'
$old='{'+[guid]::NewGuid().ToString().ToUpper()+'}';$new='{'+[guid]::NewGuid().ToString().ToUpper()+'}'
$namespace='Software\TrayLeafInstallerProbe\'+$id
$components=@{};$installer=New-Object -ComObject WindowsInstaller.Installer
function Clone-IsolatedMsi($source,$label,$product){
 $draft=Join-Path $root ($label+'-draft.msi');Copy-Item -LiteralPath $source -Destination $draft
 $db=$installer.OpenDatabase($draft,1)
 function Rows($sql,$n){$v=$db.OpenView($sql);try{$v.Execute()|Out-Null;while($r=$v.Fetch()){$row=@();for($i=1;$i -le $n;$i++){$row+=$r.StringData($i)};Write-Output -NoEnumerate $row}}finally{$v.Close()|Out-Null}}
 function Sql($s){$v=$db.OpenView($s);try{$v.Execute()|Out-Null}catch{throw ('SQL failed: '+$s+' / '+$_.Exception.Message)}finally{$v.Close()|Out-Null}}
 function Prop($k,$v){Sql "DELETE FROM ``Property`` WHERE ``Property``='$k'";Sql "INSERT INTO ``Property`` (``Property``,``Value``) VALUES ('$k','$v')"}
 Prop 'ProductCode' $product;Prop 'UpgradeCode' $upgrade;Prop 'ProductName' ('TrayLeaf Isolated Overwrite Probe '+$label)
 Prop 'ARPSYSTEMCOMPONENT' '1';Prop 'DESKTOP_SHORTCUT' '0';Prop 'STARTMENU_SHORTCUT' '0';Prop 'AUTO_START' '0'
 $upgradeRows=@(Rows 'SELECT `VersionMin`,`VersionMax`,`Language`,`Attributes`,`Remove`,`ActionProperty` FROM `Upgrade`' 6)
 Sql 'DELETE FROM `Upgrade`'
 foreach($row in $upgradeRows){Sql ("INSERT INTO ``Upgrade`` (``UpgradeCode``,``VersionMin``,``VersionMax``,``Language``,``Attributes``,``Remove``,``ActionProperty``) VALUES ('$upgrade','$($row[0])','$($row[1])','$($row[2])',$($row[3]),'$($row[4])','$($row[5])')")}
 foreach($a in @('ConfigureStartup','RemoveStartup')){Sql "DELETE FROM ``InstallExecuteSequence`` WHERE ``Action``='$a'"}
 foreach($c in @('DesktopShortcut','StartMenuShortcut','StartupFallback')){Sql "DELETE FROM ``FeatureComponents`` WHERE ``Component_``='$c'"}
 Sql 'DELETE FROM `Shortcut`';Sql 'DELETE FROM `RemoveFile`'
 foreach($row in @(Rows 'SELECT `Component` FROM `Component`' 1)){
  $key=$row[0];if(!$components.ContainsKey($key)){$components[$key]='{'+[guid]::NewGuid().ToString().ToUpper()+'}'}
  Sql "UPDATE ``Component`` SET ``ComponentId``='$($components[$key])' WHERE ``Component``='$key'"
 }
 foreach($row in @(Rows 'SELECT `Registry`,`Key` FROM `Registry`' 2)){
  $key=if($row[1].StartsWith('Software\TrayLeaf')){$namespace+$row[1].Substring('Software\TrayLeaf'.Length)}else{$namespace+'\Other\'+$row[0]}
  Sql "UPDATE ``Registry`` SET ``Key``='$key' WHERE ``Registry``='$($row[0])'"
 }
 foreach($row in @(Rows 'SELECT `Signature_`,`Root`,`Key` FROM `RegLocator`' 3)){
  if($row[1] -eq '1' -and $row[2].StartsWith('Software\TrayLeaf')){
   $key=$namespace+$row[2].Substring('Software\TrayLeaf'.Length)
   Sql "UPDATE ``RegLocator`` SET ``Key``='$key' WHERE ``Signature_``='$($row[0])'"
  }
 }
 $db.Commit()|Out-Null;$sum=$db.SummaryInformation(3)
 $sum.Property(9)='{'+[guid]::NewGuid().ToString().ToUpper()+'}';$sum.Property(15)=([int]$sum.Property(15) -bor 8);$sum.Persist()|Out-Null;$db.Commit()|Out-Null
 [Runtime.InteropServices.Marshal]::FinalReleaseComObject($sum)|Out-Null
 [Runtime.InteropServices.Marshal]::FinalReleaseComObject($db)|Out-Null
 $run=Join-Path $root ($label+'.msi');Copy-Item -LiteralPath $draft -Destination $run;return $run
}
function Run-Msi($label,$Arguments){
 $log=Join-Path $root ($label+'.log');$sw=[Diagnostics.Stopwatch]::StartNew()
 $process=Start-Process "$env:WINDIR\System32\msiexec.exe" -ArgumentList ($Arguments+' REBOOT=ReallySuppress /l*vx "'+$log+'"') -PassThru -WindowStyle Hidden
 $process.WaitForExit();$sw.Stop();"MSI_RUN $label exit=$($process.ExitCode) elapsed_ms=$($sw.ElapsedMilliseconds)"
 $process.ExitCode | Set-Content -LiteralPath (Join-Path $root ($label+'.exit.txt'))
 Check ($label+'-exit-zero') ($process.ExitCode -eq 0)
 if($process.ExitCode -ne 0){throw ('Probe '+$label+' failed; inspect '+$log)}
}
try{
 $a=Clone-IsolatedMsi $BaselineMsi 'old-098' $old;$b=Clone-IsolatedMsi $CandidateMsi 'new-098' $new
 $target=Join-Path $root 'custom-location\TrayLeaf';$exe=Join-Path $target 'TrayLeaf.exe'
 @{OldProduct=$old;NewProduct=$new;UpgradeCode=$upgrade;InstallPath=$target;RegistryNamespace=$namespace}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $root 'isolation.json')
 Run-Msi 'fresh-install' ('/i "'+$a+'" /qn INSTALLFOLDER="'+$target+'"')
 $sentinel=Join-Path $target 'user-configuration.keep';[IO.File]::WriteAllText($sentinel,'same-version user-data sentinel')
 $expected=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
 Run-Msi 'same-version-upgrade' ('/i "'+$b+'" /qn')
 Check 'same-version-upgrade-keeps-custom-path' (Test-Path -LiteralPath $exe)
 Check 'same-version-upgrade-preserves-user-file' (([IO.File]::ReadAllText($sentinel)) -eq 'same-version user-data sentinel')
 $related=@($installer.RelatedProducts($upgrade));Check 'same-version-upgrade-has-one-product' ($related.Count -eq 1 -and $related[0] -eq $new)
 $data=[IO.File]::ReadAllBytes($exe);$append=[Text.Encoding]::ASCII.GetBytes('OVERWRITE-PROBE-MARKER');[IO.File]::WriteAllBytes($exe,([byte[]]($data+$append)))
 Check 'same-package-test-file-was-changed' ((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $expected)
 Run-Msi 'same-package-overwrite' ('/i "'+$b+'" /qn')
 Check 'same-package-overwrite-restores-pristine-executable' ((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -eq $expected)
 Check 'same-package-overwrite-keeps-custom-path' (Test-Path -LiteralPath $exe)
 Check 'same-package-overwrite-preserves-user-file' (([IO.File]::ReadAllText($sentinel)) -eq 'same-version user-data sentinel')
 $related=@($installer.RelatedProducts($upgrade));Check 'same-package-overwrite-has-one-product' ($related.Count -eq 1 -and $related[0] -eq $new)
}finally{
 foreach($code in @($new,$old)){
  if($installer.ProductState($code) -ge 1){Run-Msi ('cleanup-'+$code.Trim('{}')) ('/x '+$code+' /qn')}
 }
 Check 'isolated-products-uninstalled' (@($installer.RelatedProducts($upgrade)).Count -eq 0)
}
"RESULT passed=$pass failed=$fail";if($fail){exit 1}
