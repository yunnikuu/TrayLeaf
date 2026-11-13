using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace TrayLeaf {
 internal static class NativeFolderPicker {
  internal static string Choose(Window owner,string initial){IFileDialog dialog=null;IShellItem folder=null,result=null;IntPtr value=IntPtr.Zero;IntPtr dpi=IntPtr.Zero;bool dpiChanged=false;
   try{try{dpi=SetThreadDpiAwarenessContext(new IntPtr(-4));dpiChanged=dpi!=IntPtr.Zero;}catch(EntryPointNotFoundException){}
    dialog=(IFileDialog)new FileOpenDialog();dialog.SetOptions(0x20|0x40|0x800|0x8);dialog.SetTitle(UiLanguage.Text("选择数据目录"));dialog.SetOkButtonLabel(UiLanguage.Text("选择此文件夹"));if(Directory.Exists(initial)){Guid id=typeof(IShellItem).GUID;if(SHCreateItemFromParsingName(initial,IntPtr.Zero,ref id,out folder)==0)dialog.SetFolder(folder);}
    int hr=dialog.Show(owner==null?IntPtr.Zero:new WindowInteropHelper(owner).Handle);if(hr==unchecked((int)0x800704C7))return null;if(hr<0)Marshal.ThrowExceptionForHR(hr);dialog.GetResult(out result);result.GetDisplayName(0x80058000,out value);return Marshal.PtrToStringUni(value);
   }finally{if(value!=IntPtr.Zero)Marshal.FreeCoTaskMem(value);if(result!=null)Marshal.FinalReleaseComObject(result);if(folder!=null)Marshal.FinalReleaseComObject(folder);if(dialog!=null)Marshal.FinalReleaseComObject(dialog);if(dpiChanged)SetThreadDpiAwarenessContext(dpi);}
  }
  [DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
  [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=true)]static extern int SHCreateItemFromParsingName(string path,IntPtr binding,ref Guid iid,out IShellItem item);
  [ComImport,Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]class FileOpenDialog{}
  [ComImport,Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IFileDialog {
   [PreserveSig]int Show(IntPtr owner);void SetFileTypes(uint count,IntPtr types);void SetFileTypeIndex(uint index);void GetFileTypeIndex(out uint index);void Advise(IntPtr events,out uint cookie);void Unadvise(uint cookie);void SetOptions(uint options);void GetOptions(out uint options);void SetDefaultFolder(IShellItem item);void SetFolder(IShellItem item);void GetFolder(out IShellItem item);void GetCurrentSelection(out IShellItem item);void SetFileName([MarshalAs(UnmanagedType.LPWStr)]string name);void GetFileName([MarshalAs(UnmanagedType.LPWStr)]out string name);void SetTitle([MarshalAs(UnmanagedType.LPWStr)]string title);void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)]string text);void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)]string text);void GetResult(out IShellItem item);void AddPlace(IShellItem item,uint location);void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)]string extension);void Close(int hr);void SetClientGuid(ref Guid guid);void ClearClientData();void SetFilter(IntPtr filter);
  }
  [ComImport,Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IShellItem {void BindToHandler(IntPtr context,ref Guid handler,ref Guid iid,out IntPtr result);void GetParent(out IShellItem item);void GetDisplayName(uint kind,out IntPtr name);void GetAttributes(uint mask,out uint attributes);void Compare(IShellItem other,uint hint,out int order);}
 }
}
