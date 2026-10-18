using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
namespace TrayLeaf {
 public static class Native {
  [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hwnd);
  public delegate bool EnumProc(IntPtr h,IntPtr p);
  [StructLayout(LayoutKind.Sequential)]public struct POINT {public int X,Y;}
  [StructLayout(LayoutKind.Sequential)]public struct RECT {public int Left,Top,Right,Bottom;}
  [StructLayout(LayoutKind.Sequential)]public struct ICONID {public uint cbSize;public IntPtr hWnd;public uint uID;public Guid guidItem;}
  [DllImport("user32.dll")]public static extern bool EnumWindows(EnumProc callback,IntPtr p);
  [DllImport("user32.dll")]static extern bool EnumChildWindows(IntPtr parent,EnumProc cb,IntPtr p);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern IntPtr FindWindow(string cls,string title);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]public static extern int GetClassName(IntPtr h,StringBuilder text,int n);
  [DllImport("user32.dll")]public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
  [DllImport("user32.dll")]public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")]public static extern IntPtr GetWindow(IntPtr h,uint command);
  [DllImport("user32.dll")]public static extern IntPtr GetAncestor(IntPtr h,uint flags);
  [DllImport("user32.dll")]public static extern IntPtr WindowFromPoint(POINT p);
  [DllImport("user32.dll")]public static extern bool GetCursorPos(out POINT p);
  [DllImport("user32.dll")]public static extern short GetAsyncKeyState(int key);
  [DllImport("user32.dll")]public static extern bool PostMessage(IntPtr h,uint msg,IntPtr w,IntPtr l);
  [DllImport("user32.dll",SetLastError=true)]public static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
  [DllImport("user32.dll")]public static extern bool UnregisterHotKey(IntPtr h,int id);
  [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SendMessageTimeout(IntPtr h,uint msg,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);
  [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
  [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
  [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr VirtualAllocEx(IntPtr h,IntPtr addr,UIntPtr size,uint type,uint protect);
  [DllImport("kernel32.dll")]static extern bool VirtualFreeEx(IntPtr h,IntPtr addr,UIntPtr size,uint type);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool ReadProcessMemory(IntPtr h,IntPtr addr,byte[] buffer,int size,out IntPtr read);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool IsProcessCritical(IntPtr h,out bool critical);
  [DllImport("shell32.dll")]static extern int Shell_NotifyIconGetRect(ref ICONID id,out RECT rect);
  [DllImport("shell32.dll")]static extern int SHGetKnownFolderPath(ref Guid id,uint flags,IntPtr token,out IntPtr path);
  public static uint Pid(IntPtr h){uint id;GetWindowThreadProcessId(h,out id);return id;}
  public static string Class(IntPtr h){var b=new StringBuilder(256);GetClassName(h,b,b.Capacity);return b.ToString();}
  public static bool Critical(uint pid){IntPtr h=OpenProcess(0x1000,false,pid);if(h==IntPtr.Zero)return false;try{bool c;return IsProcessCritical(h,out c)&&c;}catch(EntryPointNotFoundException){return false;}finally{CloseHandle(h);}}
  public static bool IconRect(IntPtr hwnd,uint uid,Guid guid){ICONID n=new ICONID{cbSize=(uint)Marshal.SizeOf(typeof(ICONID)),hWnd=hwnd,uID=uid,guidItem=guid};RECT r;return Shell_NotifyIconGetRect(ref n,out r)==0&&r.Right>r.Left&&r.Bottom>r.Top;}
  public static string ExpandKnownFolder(string path){if(String.IsNullOrEmpty(path))return "";path=Environment.ExpandEnvironmentVariables(path);if(path.StartsWith("{")){int end=path.IndexOf('}');Guid guid;if(end>0&&Guid.TryParse(path.Substring(0,end+1),out guid)){IntPtr p;if(SHGetKnownFolderPath(ref guid,0,IntPtr.Zero,out p)==0){try{return Marshal.PtrToStringUni(p)+path.Substring(end+1);}finally{Marshal.FreeCoTaskMem(p);}}}}return path;}
  public static Dictionary<uint,List<IntPtr>> WindowOwners(){var all=new Dictionary<uint,List<IntPtr>>();Action<IntPtr> add=delegate(IntPtr h){uint p=Pid(h);if(!all.ContainsKey(p))all[p]=new List<IntPtr>();if(all[p].Count<256)all[p].Add(h);};EnumWindows(delegate(IntPtr h,IntPtr p){add(h);EnumChildWindows(h,delegate(IntPtr c,IntPtr q){add(c);return true;},IntPtr.Zero);return true;},IntPtr.Zero);IntPtr last=IntPtr.Zero;for(int i=0;i<4096;i++){last=FindWindowEx(new IntPtr(-3),last,null,null);if(last==IntPtr.Zero)break;add(last);}return all;}
  public static HashSet<int> VisiblePids(){var ids=new HashSet<int>();EnumWindows(delegate(IntPtr h,IntPtr p){string c=Class(h);if(IsWindowVisible(h)&&GetWindow(h,4)==IntPtr.Zero&&c!="Shell_TrayWnd"&&c!="Progman"&&c!="WorkerW"&&c!="NotifyIconOverflowWindow"&&c!="TopLevelWindowForOverflowXamlIsland")ids.Add((int)Pid(h));return true;},IntPtr.Zero);return ids;}
  public static HashSet<int> ToolbarPids(){var ids=new HashSet<int>();foreach(string cls in new[]{"Shell_TrayWnd","NotifyIconOverflowWindow","Shell_SecondaryTrayWnd"}){IntPtr root=FindWindow(cls,null);if(root==IntPtr.Zero)continue;EnumChildWindows(root,delegate(IntPtr h,IntPtr p){if(Class(h)!="ToolbarWindow32")return true;IntPtr count;if(SendMessageTimeout(h,0x418,IntPtr.Zero,IntPtr.Zero,2,400,out count)==IntPtr.Zero)return true;IntPtr process=OpenProcess(0x38|0x1000,false,Pid(h));if(process==IntPtr.Zero)return true;IntPtr mem=VirtualAllocEx(process,IntPtr.Zero,(UIntPtr)32,0x3000,4);try{if(mem==IntPtr.Zero)return true;for(int i=0;i<Math.Min(count.ToInt64(),256);i++){IntPtr result,read;if(SendMessageTimeout(h,0x417,(IntPtr)i,mem,2,400,out result)==IntPtr.Zero||result==IntPtr.Zero)continue;byte[] button=new byte[32];if(!ReadProcessMemory(process,mem,button,32,out read)||read.ToInt64()!=32)continue;long data=BitConverter.ToInt64(button,16);if(data==0)continue;byte[] tray=new byte[16];if(!ReadProcessMemory(process,new IntPtr(data),tray,16,out read))continue;IntPtr owner=new IntPtr(BitConverter.ToInt64(tray,0));uint id=Pid(owner);if(id!=0)ids.Add((int)id);}}finally{if(mem!=IntPtr.Zero)VirtualFreeEx(process,mem,UIntPtr.Zero,0x8000);CloseHandle(process);}return true;},IntPtr.Zero);}return ids;}
 }
}