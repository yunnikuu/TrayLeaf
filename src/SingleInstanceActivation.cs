using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
namespace TrayLeaf {
 internal static class SingleInstanceActivation {
  // The per-user window property also identifies hidden windows without relying on translated titles.
  static readonly string Identity="TrayLeaf.Activate."+WindowsIdentity.GetCurrent().User.Value+"."+Assembly.GetExecutingAssembly().GetName().Name;
  internal static readonly uint Message=RegisterWindowMessage(Identity);
  [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern uint RegisterWindowMessage(string name);
  [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool SetProp(IntPtr window,string name,IntPtr value);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetProp(IntPtr window,string name);
  [DllImport("user32.dll",SetLastError=true)]static extern bool ChangeWindowMessageFilterEx(IntPtr window,uint message,uint action,IntPtr change);
  [DllImport("user32.dll")]static extern bool AllowSetForegroundWindow(uint processId);
  internal static void Bind(IntPtr window){
   if(Message==0||!SetProp(window,Identity,new IntPtr(1)))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
   // Only the no-payload activation message crosses the integrity boundary; no commands or paths are accepted.
   ChangeWindowMessageFilterEx(window,Message,1,IntPtr.Zero);
  }
  internal static bool TryActivateExisting(bool startup,int waitMilliseconds=0){
   var elapsed=Stopwatch.StartNew();
   do{
    IntPtr existing=IntPtr.Zero;
    Native.EnumWindows(delegate(IntPtr window,IntPtr unused){if(GetProp(window,Identity)==IntPtr.Zero)return true;existing=window;return false;},IntPtr.Zero);
    if(existing!=IntPtr.Zero){
     if(startup)return true;
     uint pid;Native.GetWindowThreadProcessId(existing,out pid);AllowSetForegroundWindow(pid);
     return Native.PostMessage(existing,Message,IntPtr.Zero,IntPtr.Zero);
    }
    if(elapsed.ElapsedMilliseconds>=waitMilliseconds)return false;
    Thread.Sleep(50);
   }while(true);
  }
 }
}
