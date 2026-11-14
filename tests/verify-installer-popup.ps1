param([Parameter(Mandatory=$true)][string]$SourcePath)
$ErrorActionPreference='Stop';$pass=0;$fail=0
$t=[IO.File]::ReadAllText([IO.Path]::GetFullPath($SourcePath))
function Check($n,$ok){if($ok){$script:pass++;"PASS $n"}else{$script:fail++;"FAIL $n"}}
Check 'running-process-heading-kept' ($t.Contains('TrayLeaf.exe 仍在运行'))
Check 'force-close-data-loss-warning-kept' ($t.Contains('是否强制关闭并继续安装？未保存的数据可能丢失'))
Check 'redundant-button-instructions-removed' (!$t.Contains('确定：强制关闭并继续') -and !$t.Contains('取消或关闭此窗口：取消安装，保留程序运行'))
Check 'cancel-is-default-and-x-is-cancel' ($t.Contains('MB_OKCANCEL|MB_ICONWARNING|MB_DEFBUTTON2') -and $t.Contains('return answer==IDOK;'))
Check 'silent-install-requires-consent' ($t.Contains('if(Property(session,L"TRAYLEAF_CLOSE_RUNNING")==L"1")return true;') -and $t.Contains('if(_wtoi(Property(session,L"UILevel").c_str())<4)return false;'))
"RESULT passed=$pass failed=$fail";if($fail){exit 1}
