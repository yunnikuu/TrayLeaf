using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Deployment.WindowsInstaller;

public static class FolderPicker {
 const uint PickFolders=0x20,ForceFileSystem=0x40,PathMustExist=0x800,NoChangeDirectory=8;
 const int Cancelled=unchecked((int)0x800704C7);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=false)]
 static extern void SHCreateItemFromParsingName(string path,IntPtr context,ref Guid iid,out IShellItem item);

 public static string NormalizeTarget(string path){
  if(String.IsNullOrWhiteSpace(path))throw new ArgumentException("请选择安装文件夹");
  string full=Path.GetFullPath(path.Trim()).TrimEnd('\\','/');
  if(!String.Equals(Path.GetFileName(full),"TrayLeaf",StringComparison.OrdinalIgnoreCase))full+=@"\TrayLeaf";
  return full+@"\";
 }
 public static string ChooseFolder(IntPtr owner,string current){
  using(var dpi=new NativeDpiScope())return ChooseFolderCore(owner,current);
 }
 static string ChooseFolderCore(IntPtr owner,string current){
  IFileDialog dialog=null;IShellItem initial=null,result=null;
  try {
   dialog=(IFileDialog)new FileOpenDialog();uint flags;dialog.GetOptions(out flags);
   dialog.SetOptions(flags|PickFolders|ForceFileSystem|PathMustExist|NoChangeDirectory);
   dialog.SetTitle("选择轻羽归栈安装文件夹");dialog.SetOkButtonLabel("选择文件夹");
   string existing=current;
   while(!String.IsNullOrEmpty(existing)&&!Directory.Exists(existing))existing=Path.GetDirectoryName(existing.TrimEnd('\\','/'));
   if(!String.IsNullOrEmpty(existing)){
    Guid iid=typeof(IShellItem).GUID;SHCreateItemFromParsingName(existing,IntPtr.Zero,ref iid,out initial);dialog.SetFolder(initial);
   }
   int hr=dialog.Show(owner);if(hr==Cancelled)return null;Marshal.ThrowExceptionForHR(hr);
   dialog.GetResult(out result);IntPtr p;result.GetDisplayName(0x80058000,out p);
   try{return NormalizeTarget(Marshal.PtrToStringUni(p));}finally{Marshal.FreeCoTaskMem(p);}
  }finally{if(result!=null)Marshal.FinalReleaseComObject(result);if(initial!=null)Marshal.FinalReleaseComObject(initial);if(dialog!=null)Marshal.FinalReleaseComObject(dialog);}
 }
 [CustomAction] public static ActionResult BrowseInstallFolder(Session session){
  string previous=session["INSTALLFOLDER"],selected=null;Exception failure=null;IntPtr owner=GetForegroundWindow();
  // Return a separate value; only a UI property event refreshes PathEdit reliably.
  session["TRAYLEAF_SELECTED_FOLDER"]=String.Empty;
  // Common Item Dialog requires STA; keep MSI session access on its calling thread.
  var thread=new Thread(()=>{try{selected=ChooseFolder(owner,previous);}catch(Exception ex){failure=ex;}});
  thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
  if(failure!=null){session.Log("Native folder picker: {0}",failure);MessageBox.Show("无法打开文件夹选择器，请重试或直接编辑安装路径\n"+failure.Message,"选择安装位置",MessageBoxButtons.OK,MessageBoxIcon.Warning);return ActionResult.Success;}
  if(selected==null)return ActionResult.Success;
  session["TRAYLEAF_SELECTED_FOLDER"]=selected;
  session.Log("Selected install folder: {0}",selected);
  return ActionResult.Success;
 }
 // MSI owns the process. Change only the picker thread, never its host's DPI mode.
 internal sealed class NativeDpiScope:IDisposable {
  IntPtr previous;
  [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
  internal NativeDpiScope(){
   try{previous=SetThreadDpiAwarenessContext(new IntPtr(-4));
    if(previous==IntPtr.Zero)previous=SetThreadDpiAwarenessContext(new IntPtr(-3));
   }catch(EntryPointNotFoundException){}
  }
  public void Dispose(){if(previous!=IntPtr.Zero){SetThreadDpiAwarenessContext(previous);previous=IntPtr.Zero;}}
 }
 [ComImport,Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")] class FileOpenDialog {}
 [ComImport,Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface IFileDialog {
  [PreserveSig] int Show(IntPtr owner);
  void SetFileTypes(uint count,IntPtr types);void SetFileTypeIndex(uint index);void GetFileTypeIndex(out uint index);
  void Advise(IntPtr events,out uint cookie);void Unadvise(uint cookie);
  void SetOptions(uint options);void GetOptions(out uint options);
  void SetDefaultFolder(IShellItem item);void SetFolder(IShellItem item);void GetFolder(out IShellItem item);void GetCurrentSelection(out IShellItem item);
  void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
  void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
  void GetResult(out IShellItem item);void AddPlace(IShellItem item,uint location);void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
  void Close(int hr);void SetClientGuid(ref Guid guid);void ClearClientData();void SetFilter(IntPtr filter);
 }
 [ComImport,Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface IShellItem {
  void BindToHandler(IntPtr context,ref Guid handler,ref Guid iid,out IntPtr result);void GetParent(out IShellItem item);
  void GetDisplayName(uint kind,out IntPtr name);void GetAttributes(uint mask,out uint attributes);void Compare(IShellItem other,uint hint,out int order);
 }
}
