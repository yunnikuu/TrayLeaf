using System;
using TrayLeaf;
class PolicyTests {
 static int failed=0,passed=0;
 static void Check(string name, bool ok) { Console.WriteLine((ok?"PASS ":"FAIL ")+name); if(ok)passed++;else failed++; }
 static int Main() {
  Check("shared-key-close",Policy.SharedAction(false)=="close");
  Check("shared-key-restore",Policy.SharedAction(true)=="restore");
  Check("svchost-risk",Policy.HighRisk("svchost.exe",false,false));
  Check("case-insensitive-risk",Policy.HighRisk("LSASS.EXE",false,false));
  Check("service-risk",Policy.HighRisk("custom.exe",true,false));
  Check("critical-flag-risk",Policy.HighRisk("custom.exe",false,true));
  Check("normal-app",!Policy.HighRisk("music.exe",false,false));
  Check("args-quoted-path",Policy.Arguments("\"C:\\Program Files\\test.exe\" --flag \"a b\"")=="--flag \"a b\"");
  Check("args-simple",Policy.Arguments("C:\\test.exe /silent")=="/silent");
  Check("args-empty",Policy.Arguments(null)=="");
  Check("args-no-arguments",Policy.Arguments("\"C:\\test.exe\"")=="");
  Console.WriteLine("RESULT passed="+passed+" failed="+failed);
  return failed==0?0:1;
 }
}