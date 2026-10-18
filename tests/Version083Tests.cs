using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using TrayLeaf;
class Version083Tests {
 static int passed,failed;static void Check(string n,bool ok){Console.WriteLine((ok?"PASS ":"FAIL ")+n);if(ok)passed++;else failed++;}
 static Exception InvokeRemoval(Action action){var m=typeof(StartupManager).GetMethod("RemoveTaskIfPresent",BindingFlags.Static|BindingFlags.NonPublic);if(m==null)return new MissingMethodException();try{m.Invoke(null,new object[]{action});return null;}catch(TargetInvocationException e){return e.InnerException;}}
 static int Main(string[] args){try{
  Check("missing-task-filenotfound-is-idempotent",InvokeRemoval(()=>{throw new FileNotFoundException();})==null);
  Check("missing-task-com-hresult-is-idempotent",InvokeRemoval(()=>{throw new COMException("missing",unchecked((int)0x80070002));})==null);
  var denied=new UnauthorizedAccessException("denied");Check("access-denied-not-swallowed",Object.ReferenceEquals(denied,InvokeRemoval(()=>{throw denied;})));
  var other=new COMException("rpc unavailable",unchecked((int)0x800706BA));Check("other-task-errors-not-swallowed",Object.ReferenceEquals(other,InvokeRemoval(()=>{throw other;})));
  int removed=0;Check("existing-task-deletion-runs-once",InvokeRemoval(()=>{removed++;})==null&&removed==1);
  var doc=XDocument.Load(Path.Combine(args[1],"installer","TrayLeaf.wxs"));XNamespace n="http://schemas.microsoft.com/wix/2006/wi";var dialog=doc.Descendants(n+"Dialog").Single(x=>(string)x.Attribute("Id")=="OptionsDlg");
  Check("installer-explanatory-notice-removed",!dialog.Elements(n+"Control").Any(x=>(string)x.Attribute("Id")=="Notice"));
  Check("installer-options-title-kept",dialog.Elements(n+"Control").Any(x=>(string)x.Attribute("Id")=="Title"&&((string)x.Attribute("Text")).Contains("安装选项")));
  Check("installer-three-options-kept",dialog.Elements(n+"Control").Count(x=>(string)x.Attribute("Type")=="CheckBox")==3);
  Check("installer-version-083",(string)doc.Descendants(n+"Product").Single().Attribute("Version")=="0.8.3");
  Check("application-version-083",System.Diagnostics.FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion=="0.8.3");
 }catch(Exception e){failed++;Console.Error.WriteLine(e);}Console.WriteLine("RESULT passed="+passed+" failed="+failed);return failed==0?0:1;}
}
