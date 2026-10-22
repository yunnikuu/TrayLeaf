using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
class FolderPickerTests {
 sealed class NoDpiScope:IDisposable {public void Dispose(){}}
 static IDisposable Scope(){var t=typeof(FolderPicker).GetNestedType("NativeDpiScope",BindingFlags.NonPublic);return t==null?new NoDpiScope():(IDisposable)Activator.CreateInstance(t,true);}
 static int passed,failed;
 static void Check(string n,bool ok){Console.WriteLine((ok?"PASS ":"FAIL ")+n);if(ok)passed++;else failed++;}
 [DllImport("user32.dll")]static extern IntPtr GetThreadDpiAwarenessContext();
 [DllImport("user32.dll")]static extern IntPtr GetWindowDpiAwarenessContext(IntPtr h);
 [DllImport("user32.dll")]static extern bool AreDpiAwarenessContextsEqual(IntPtr a,IntPtr b);
 [STAThread]static int Main(){try{
  string[,] cases={{@"I:\",@"I:\TrayLeaf\"},{@"D:\Program Files",@"D:\Program Files\TrayLeaf\"},{@"D:\Program Files\",@"D:\Program Files\TrayLeaf\"},{@"D:\TrayLeaf",@"D:\TrayLeaf\"},{@"d:\trayleaf\",@"d:\trayleaf\"},{@"D:\MyTrayLeaf",@"D:\MyTrayLeaf\TrayLeaf\"},{@"C:\软件\工具",@"C:\软件\工具\TrayLeaf\"},{@"\\server\share\tools\",@"\\server\share\tools\TrayLeaf\"}};
  for(int i=0;i<cases.GetLength(0);i++){string actual=FolderPicker.NormalizeTarget(cases[i,0]);Check("normalize-"+i,actual==cases[i,1]);Check("idempotent-"+i,FolderPicker.NormalizeTarget(actual)==actual);Console.WriteLine("PATH "+cases[i,0]+" -> "+actual);}
  foreach(string empty in new[]{null,"","  "}){bool rejected=false;try{FolderPicker.NormalizeTarget(empty);}catch(ArgumentException){rejected=true;}Check("empty-rejected",rejected);}
  IntPtr before=GetThreadDpiAwarenessContext();
  using(var scope=Scope()){
   Check("picker-thread-PMv2",AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(),new IntPtr(-4)));
   using(var form=new Form()){Check("native-window-inherits-PMv2",AreDpiAwarenessContextsEqual(GetWindowDpiAwarenessContext(form.Handle),new IntPtr(-4)));}
   using(var nested=Scope())Check("nested-context",AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(),new IntPtr(-4)));
   Check("nested-restores-outer",AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(),new IntPtr(-4)));
  }
  Check("host-context-restored",AreDpiAwarenessContextsEqual(before,GetThreadDpiAwarenessContext()));
  try{using(var scope=Scope())throw new IOException("test");}catch(IOException){}
  Check("context-restored-after-error",AreDpiAwarenessContextsEqual(before,GetThreadDpiAwarenessContext()));
  var done=new ManualResetEvent(false);bool isolated=false;var worker=new Thread(()=>{using(var s=Scope())isolated=AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(),new IntPtr(-4));done.Set();});worker.SetApartmentState(ApartmentState.STA);worker.Start();done.WaitOne();worker.Join();done.Dispose();Check("STA-worker-PMv2",isolated);Check("caller-context-unchanged",AreDpiAwarenessContextsEqual(before,GetThreadDpiAwarenessContext()));
 }catch(Exception ex){failed++;Console.Error.WriteLine(ex);}Console.WriteLine("RESULT passed="+passed+" failed="+failed);return failed==0?0:1;}
}
