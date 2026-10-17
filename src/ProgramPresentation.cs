using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
namespace TrayLeaf {
 internal static class ProgramPresentation {
  internal static void LoadIcon(AppRow row){
   row.Icon=AppIdentity.WindowIcon;
   if(String.IsNullOrEmpty(row.Path)||row.Path.StartsWith(@"\\")||!File.Exists(row.Path))return;
   try{using(var icon=System.Drawing.Icon.ExtractAssociatedIcon(row.Path)){if(icon!=null){var image=Imaging.CreateBitmapSourceFromHIcon(icon.Handle,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(28,28));image.Freeze();row.Icon=image;}}}catch{}
  }
  internal static ContextMenu PathMenu(AppRow row,Action<string> report){
   var menu=new ContextMenu();var group=new MenuItem{Header="程序路径"};menu.Items.Add(group);
   var open=new MenuItem{Header="打开程序路径",IsEnabled=!String.IsNullOrWhiteSpace(row.Path)};
   var copy=new MenuItem{Header="复制路径",IsEnabled=!String.IsNullOrWhiteSpace(row.Path)};
   open.Click+=delegate{try{string directory=Path.GetDirectoryName(row.Path);if(!Directory.Exists(directory))throw new DirectoryNotFoundException("程序目录不存在");using(var p=Process.Start(new ProcessStartInfo("explorer.exe","\""+directory+"\""){UseShellExecute=true})){}report("已打开："+directory);}catch(Exception ex){report("无法打开程序路径："+ex.Message);}};
   copy.Click+=delegate{try{Clipboard.SetText(row.Path);report("已复制程序路径");}catch(Exception ex){report("复制失败："+ex.Message);}};
   group.Items.Add(open);group.Items.Add(copy);return menu;
  }
  internal static void AttachMenus(DataGrid grid,Action<string> report){grid.LoadingRow+=delegate(object sender,DataGridRowEventArgs e){var row=e.Row.Item as AppRow;if(row!=null)e.Row.ContextMenu=PathMenu(row,report);};}
  internal static AppRow Copy(AppRow r){return new AppRow{Name=r.Name,ProcessName=r.ProcessName,Path=r.Path,Args=r.Args,Admin=r.Admin,Selected=r.Selected,ArgsKnown=true,ItemStatus=r.ItemStatus,State=r.State,Icon=r.Icon};}
 }
}
