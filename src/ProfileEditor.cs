using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
namespace TrayLeaf {
 internal sealed class ProfileEditor:Window {
  readonly TextBox name;readonly DataGrid table;readonly TextBlock feedback;readonly ObservableCollection<AppRow> entries;
  internal string ProfileName {get{return name.Text.Trim();}}
  internal List<AppRow> Rows {get{return entries.ToList();}}
  static DataTemplate CellTemplate(string markup){return (DataTemplate)XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>"+markup+"</DataTemplate>");}
  internal ProfileEditor(Window owner,string kind,string title,IEnumerable<AppRow> source,Action<string,List<AppRow>> save){
   Owner=owner;Title=kind=="close"?"关闭配置":"恢复配置";Width=940;Height=620;MinWidth=760;MinHeight=500;WindowStartupLocation=WindowStartupLocation.CenterOwner;
   Background=new SolidColorBrush(Color.FromRgb(244,247,251));Foreground=owner.Foreground;FontFamily=owner.FontFamily;FontSize=owner.FontSize;Resources=owner.Resources;UseLayoutRounding=true;SnapsToDevicePixels=true;
   entries=new ObservableCollection<AppRow>(source.Select(ProgramPresentation.Copy));foreach(var r in entries)ProgramPresentation.LoadIcon(r);
   var root=new Grid{Margin=new Thickness(24)};var surface=new Grid{Background=Background};surface.Children.Add(root);Content=surface;foreach(var h in new[]{GridLength.Auto,GridLength.Auto,new GridLength(1,GridUnitType.Star),GridLength.Auto})root.RowDefinitions.Add(new RowDefinition{Height=h});
   var heading=new StackPanel();root.Children.Add(heading);heading.Children.Add(new TextBlock{Text=Title,FontSize=23,FontWeight=FontWeights.SemiBold});heading.Children.Add(new TextBlock{Text=kind=="close"?"勾选要关闭的程序，并保存它们下次启动时使用的参数与权限。":"勾选要启动的程序，分别设置启动参数与管理员权限。",Foreground=Brushes.SlateGray,Margin=new Thickness(0,8,0,14),TextWrapping=TextWrapping.Wrap});
   var tools=new DockPanel{Margin=new Thickness(0,0,0,14)};Grid.SetRow(tools,1);root.Children.Add(tools);
   var buttons=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(buttons,Dock.Right);tools.Children.Add(buttons);
   Action<string,Action> button=(label,action)=>{var b=new Button{Content=label,Padding=new Thickness(10,7,10,7)};b.Click+=delegate{action();};buttons.Children.Add(b);};
   button("添加程序",delegate{var d=new Microsoft.Win32.OpenFileDialog{Filter="应用程序 (*.exe)|*.exe",CheckFileExists=true};if(d.ShowDialog(this)==true&&!entries.Any(x=>String.Equals(x.Path,d.FileName,StringComparison.OrdinalIgnoreCase))){var r=new AppRow{Name=System.IO.Path.GetFileNameWithoutExtension(d.FileName),Path=d.FileName,ProcessName=System.IO.Path.GetFileName(d.FileName),Selected=true,ArgsKnown=true};ProgramPresentation.LoadIcon(r);entries.Add(r);}});
   button("移除选中行",delegate{var r=table.SelectedItem as AppRow;if(r!=null)entries.Remove(r);});
   button("全选",delegate{foreach(var r in entries)r.Selected=true;});button("全不选",delegate{foreach(var r in entries)r.Selected=false;});
   name=new TextBox{Text=title??"",MaxLength=50,MinWidth=150,Margin=new Thickness(0,0,14,0),ToolTip="配置名称，最多 50 个字符"};tools.Children.Add(name);
   table=new DataGrid{AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,ItemsSource=entries,RowHeight=60,Background=Brushes.White};Grid.SetRow(table,2);root.Children.Add(table);
   table.Columns.Add(new DataGridTemplateColumn{Header=kind=="close"?"关闭":"启动",Width=84,CellTemplate=CellTemplate("<CheckBox HorizontalAlignment='Center' VerticalAlignment='Center' IsChecked='{Binding Selected,Mode=TwoWay,UpdateSourceTrigger=PropertyChanged}'/>")});
   table.Columns.Add(new DataGridTemplateColumn{Header="程序",Width=new DataGridLength(1,DataGridLengthUnitType.Star),MinWidth=180,CellTemplate=CellTemplate("<Grid ToolTip='{Binding Path}'><Grid.ColumnDefinitions><ColumnDefinition Width='34'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions><Image Source='{Binding Icon}' Width='26' Height='26'/><StackPanel Grid.Column='1' VerticalAlignment='Center'><TextBlock Text='{Binding Name}' FontWeight='SemiBold' TextTrimming='CharacterEllipsis'/><TextBlock Text='{Binding Path}' FontSize='11' Foreground='#64768A' TextTrimming='CharacterEllipsis'/></StackPanel></Grid>")});
   table.Columns.Add(new DataGridTextColumn{Header="启动参数",Binding=new Binding("Args"){Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged},Width=new DataGridLength(1,DataGridLengthUnitType.Star),MinWidth=170});
   table.Columns.Add(new DataGridTemplateColumn{Header="管理员启动",Width=155,MinWidth=140,CellTemplate=CellTemplate("<CheckBox HorizontalAlignment='Center' VerticalAlignment='Center' IsChecked='{Binding Admin,Mode=TwoWay,UpdateSourceTrigger=PropertyChanged}'/>")});
   TablePresentation.Configure(this,table);ProgramPresentation.AttachMenus(table,delegate(string text){feedback.Text=text;});
   var footer=new DockPanel{Margin=new Thickness(0,16,0,0)};Grid.SetRow(footer,3);root.Children.Add(footer);
   var actions=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(actions,Dock.Right);footer.Children.Add(actions);
   var cancel=new Button{Content="取消",IsCancel=true};actions.Children.Add(cancel);
   var commit=new Button{Content="保存配置",Style=(Style)owner.FindResource("Primary"),Margin=new Thickness(0)};actions.Children.Add(commit);
   feedback=new TextBlock{Text="双击启动参数进行编辑 · 配置只保存设置，应用后由你执行关闭或开启。",Foreground=Brushes.SlateGray,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,14,0)};footer.Children.Add(feedback);
   commit.Click+=delegate{try{table.CommitEdit(DataGridEditingUnit.Cell,true);table.CommitEdit(DataGridEditingUnit.Row,true);save(ProfileName,Rows);DialogResult=true;}catch(Exception ex){feedback.Text=ex.Message;}};
  }
 }
}
