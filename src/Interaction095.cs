using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
namespace TrayLeaf {
 internal static class ProgramLookup {
  internal static AppRow Find(IEnumerable<AppRow> rows,string path){if(String.IsNullOrWhiteSpace(path))return null;string full=Path.GetFullPath(path);return rows.FirstOrDefault(row=>!String.IsNullOrWhiteSpace(row.Path)&&String.Equals(Path.GetFullPath(row.Path),full,StringComparison.OrdinalIgnoreCase));}
 }
 public sealed partial class Controller {
  TextBox recordingBox;HotkeySpec recordingBefore;bool recordingClose;
  internal Action<Window,string> duplicateNotice=(owner,message)=>ModernDialog.Confirm(owner,message,"",false);
  void SetupInteraction095(){
   Click("ResetSelectionButton",ResetSelection);
   SetupRecording(Get<TextBox>("CloseHotkeyInput"),true);SetupRecording(Get<TextBox>("RestoreHotkeyInput"),false);
   var toolbar=Get<Grid>("SearchToolbar");var commands=Get<StackPanel>("SearchCommands");var search=Get<Grid>("SearchField");
   Action arrange=delegate{commands.Measure(new Size(Double.PositiveInfinity,Double.PositiveInfinity));bool wrap=toolbar.ActualWidth<commands.DesiredSize.Width+220;Grid.SetRow(commands,wrap?1:0);Grid.SetColumn(commands,wrap?0:1);Grid.SetColumnSpan(commands,wrap?2:1);Grid.SetColumnSpan(search,wrap?2:1);commands.HorizontalAlignment=HorizontalAlignment.Right;search.Margin=wrap?new Thickness(0,0,0,8):new Thickness(0,0,18,0);};
   toolbar.SizeChanged+=delegate{arrange();};System.ComponentModel.DependencyPropertyDescriptor.FromProperty(Control.FontSizeProperty,typeof(Window)).AddValueChanged(w,delegate{w.Dispatcher.BeginInvoke(arrange);});
  }
  void ResetSelection(){
   closeHistoryPaths.Clear();ApplyHistoryPins();foreach(var row in current){row.Selected=false;row.HistoryPriority=0;row.ClosePriority=0;}
   loadingCollections=true;try{Get<ComboBox>("CloseHistoryCombo").SelectedIndex=-1;Get<ComboBox>("CloseProfileCombo").SelectedIndex=-1;}finally{loadingCollections=false;}
   Get<TextBox>("Search").Clear();var grid=Get<DataGrid>("ProcessGrid");HistoryPresentation.Reset(grid);ApplyFilter();
   w.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(delegate{if(grid.Items.Count>0)grid.ScrollIntoView(grid.Items[0]);}));Status("已恢复默认排序，并取消勾选所有程序");
  }
  void SetupRecording(TextBox box,bool close){
   box.GotKeyboardFocus+=delegate{if(recordingBox==box)return;EndRecording(false);recordingBox=box;recordingBefore=close?editClose:editRestore;recordingClose=close;box.Text="正在录制操作";};
   box.LostKeyboardFocus+=delegate{if(recordingBox==box)EndRecording(false);};
  }
  void EndRecording(bool cancel){if(recordingBox==null)return;var box=recordingBox;recordingBox=null;if(cancel){if(recordingClose)editClose=recordingBefore;else editRestore=recordingBefore;}box.Text=(recordingClose?editClose:editRestore).ToString();recordingBefore=null;}
  void CaptureKey(TextBox box,KeyEventArgs e,bool close){
   e.Handled=true;Key key=e.Key==Key.System?e.SystemKey:e.Key;
   if(key==Key.Escape){EndRecording(true);box.Text=(close?editClose:editRestore).ToString();Keyboard.ClearFocus();return;}
   if(key==Key.Tab){e.Handled=false;return;}
   if(key==Key.LeftCtrl||key==Key.RightCtrl||key==Key.LeftAlt||key==Key.RightAlt||key==Key.LeftShift||key==Key.RightShift||key==Key.LWin||key==Key.RWin)return;
   var m=Keyboard.Modifiers;uint mods=((m&ModifierKeys.Alt)!=0?1u:0)|((m&ModifierKeys.Control)!=0?2u:0)|((m&ModifierKeys.Shift)!=0?4u:0)|((m&ModifierKeys.Windows)!=0?8u:0);
   var spec=new HotkeySpec{Mods=mods,Key=(uint)KeyInterop.VirtualKeyFromKey(key)};
   if(key==Key.Delete||key==Key.Back)spec=new HotkeySpec();else if((mods&11)==0){Status("快捷键至少包含 Ctrl、Alt 或 Win；Esc 取消录制，Backspace 清空");return;}
   if(close)editClose=spec;else editRestore=spec;EndRecording(false);box.Text=spec.ToString();Keyboard.ClearFocus();
  }
  async Task<bool> FocusExistingProgram(AppRow observed){
   if(observed==null)return false;
   // Search all running processes before applying the user's visible-source filter.
   bool wasInitializing=initializing;initializing=true;try{Get<ComboBox>("ScanMode").SelectedIndex=1;}finally{initializing=wasInitializing;}
   db.Set("allProcesses","1");db.AllowProgram(observed);Get<TextBox>("Search").Clear();await Scan();Navigate(0);
   var row=ProgramLookup.Find(current,observed.Path);if(row==null){row=observed;ProgramPresentation.LoadIcon(row);Wire(row,false);current.Add(row);Get<DataGrid>("ProcessGrid").ItemsSource=null;Get<DataGrid>("ProcessGrid").ItemsSource=current;ApplyFilter();}
   var grid=Get<DataGrid>("ProcessGrid");grid.SelectedItem=row;grid.UpdateLayout();grid.ScrollIntoView(row);
   Status("该应用已在列表内 无需再次添加");duplicateNotice(w,"该应用已在列表内 无需再次添加");
   await w.Dispatcher.InvokeAsync(new Action(delegate{grid.ScrollIntoView(row);}),DispatcherPriority.Loaded);return true;
  }
  async void AddFile(){
   if(busy)return;var dialog=new Microsoft.Win32.OpenFileDialog{Filter="应用程序 (*.exe)|*.exe",CheckFileExists=true};if(dialog.ShowDialog(w)!=true)return;
   SetBusy(true);try{string path=Path.GetFullPath(dialog.FileName);var scan=await Task.Run(()=>scanPrograms(true,false,new string[0]));
    var observed=ProgramLookup.Find(scan.Rows,path)??ProgramLookup.Find(current,path);if(await FocusExistingProgram(observed))return;
    db.SaveManaged(path,Path.GetFileNameWithoutExtension(path),"",false,false);db.Set("custom:"+path,"1");db.AllowProgram(new AppRow{Path=path,ProcessName=Path.GetFileName(path)});await Scan();Navigate(0);
    var added=ProgramLookup.Find(current,path);var grid=Get<DataGrid>("ProcessGrid");grid.SelectedItem=added;if(added!=null)grid.ScrollIntoView(added);Status("已添加："+Path.GetFileName(path)+"；请检查启动信息后勾选");
   }catch(Exception ex){Error(ex);}finally{SetBusy(false);}
  }
 }
}
