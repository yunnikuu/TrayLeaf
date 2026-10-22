using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
namespace TrayLeaf {
 public sealed class ProgramSelectionDialog:Window {
  readonly List<AppRow> choices;readonly DataGrid grid;readonly TextBox search;readonly TextBlock count;readonly Button confirm;
  public List<AppRow> Chosen{get{return choices.Where(x=>x.Selected).ToList();}}
  public ProgramSelectionDialog(Window owner,List<AppRow> rows,bool restore){
   Title=restore?"管理已隐藏程序":"隐藏程序";Owner=owner;Width=760;Height=550;MinWidth=620;MinHeight=440;WindowStartupLocation=WindowStartupLocation.CenterOwner;ShowInTaskbar=false;Background=(Brush)owner.FindResource("Canvas");Foreground=owner.Foreground;FontFamily=owner.FontFamily;FontSize=13;UseLayoutRounding=true;SnapsToDevicePixels=true;Resources=owner.Resources;
   choices=rows.Select(r=>new AppRow{Name=r.Name,Path=r.Path,ProcessName=r.ProcessName,Icon=r.Icon,Selected=false}).ToList();
   foreach(var row in choices)if(row.Icon==null)ProgramPresentation.LoadIcon(row);
   const string xaml=@"<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
    <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/><RowDefinition Height='*'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
    <StackPanel><TextBlock x:Name='Heading' FontSize='22' FontWeight='SemiBold'/><TextBlock x:Name='Description' Margin='0,8,0,18' Foreground='{DynamicResource Muted}' TextWrapping='Wrap'/></StackPanel>
    <Grid Grid.Row='1' Margin='0,0,0,14'><Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions><TextBox x:Name='Filter' ToolTip='搜索名称、进程名或路径' Margin='0,0,12,0'/><TextBlock x:Name='Placeholder' Text='搜索名称或路径…' IsHitTestVisible='False' Margin='12,0' VerticalAlignment='Center' Foreground='{DynamicResource Muted}'/><StackPanel Grid.Column='1' Orientation='Horizontal'><Button x:Name='ChooseVisible' Content='全选当前结果' Padding='10,7'/><Button x:Name='ChooseNone' Content='取消选择' Padding='10,7' Margin='0'/></StackPanel></Grid>
    <Border Grid.Row='2' Background='{DynamicResource Surface}' CornerRadius='8' BorderBrush='{DynamicResource Line}' BorderThickness='1' ClipToBounds='True'><Grid><DataGrid x:Name='Programs' CanUserSortColumns='True' SelectionMode='Single'><DataGrid.Columns><DataGridTemplateColumn Width='42'><DataGridTemplateColumn.CellTemplate><DataTemplate><CheckBox IsChecked='{Binding Selected,Mode=TwoWay,UpdateSourceTrigger=PropertyChanged}' ToolTip='选择此程序'/></DataTemplate></DataGridTemplateColumn.CellTemplate></DataGridTemplateColumn><DataGridTemplateColumn Header='程序 / 进程' SortMemberPath='Name' Width='*'><DataGridTemplateColumn.CellTemplate><DataTemplate><Grid ToolTip='{Binding Hint}'><Grid.ColumnDefinitions><ColumnDefinition Width='32'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions><Image Source='{Binding Icon}' Width='24' Height='24' HorizontalAlignment='Left' VerticalAlignment='Center'/><StackPanel Grid.Column='1'><TextBlock Text='{Binding Name}' FontWeight='SemiBold' TextTrimming='CharacterEllipsis'/><TextBlock Text='{Binding Hint}' Margin='0,5,0,0' Foreground='{DynamicResource Muted}' FontSize='11' TextTrimming='CharacterEllipsis'/></StackPanel></Grid></DataTemplate></DataGridTemplateColumn.CellTemplate></DataGridTemplateColumn></DataGrid.Columns></DataGrid><TextBlock x:Name='Empty' Text='没有匹配的程序' HorizontalAlignment='Center' VerticalAlignment='Center' Foreground='{DynamicResource Muted}' Visibility='Collapsed' IsHitTestVisible='False'/></Grid></Border>
    <DockPanel Grid.Row='3' Margin='0,18,0,0'><StackPanel DockPanel.Dock='Right' Orientation='Horizontal'><Button x:Name='Cancel' Content='取消' IsCancel='True'/><Button x:Name='Confirm' Style='{DynamicResource Primary}' Content='隐藏所选' Margin='0' IsDefault='True' IsEnabled='False'/></StackPanel><TextBlock x:Name='Count' Foreground='{DynamicResource Muted}' VerticalAlignment='Center'/></DockPanel>
   </Grid>";
   var panel=(Grid)XamlReader.Parse(xaml);Content=new Border{Background=Background,Padding=new Thickness(24),Child=panel};NameScope.SetNameScope(this,new NameScope());
   foreach(string name in new[]{"Programs","Filter","ChooseVisible","ChooseNone","Confirm","Cancel","Count"})RegisterName(name,panel.FindName(name));
   ((TextBlock)panel.FindName("Heading")).Text=restore?"让程序重新显示":"整理你的程序列表";
   ((TextBlock)panel.FindName("Description")).Text=restore?"勾选后恢复到识别列表，不会启动程序，也不会自动加入关闭选择":"选择不再显示的程序，不会关闭或卸载应用；刷新与重启后保留，可在设置中恢复显示";
   grid=(DataGrid)panel.FindName("Programs");search=(TextBox)panel.FindName("Filter");count=(TextBlock)panel.FindName("Count");confirm=(Button)panel.FindName("Confirm");confirm.Content=restore?"恢复显示":"隐藏所选";grid.ItemsSource=choices;
   Action update=delegate{count.Text="已选 "+Chosen.Count+" / "+choices.Count+" 个程序";confirm.IsEnabled=Chosen.Count>0;};
   foreach(var r in choices)r.PropertyChanged+=delegate{update();};
   search.TextChanged+=delegate{((TextBlock)panel.FindName("Placeholder")).Visibility=search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;CollectionViewSource.GetDefaultView(choices).Filter=o=>Matches((AppRow)o);((TextBlock)panel.FindName("Empty")).Visibility=grid.Items.Count==0?Visibility.Visible:Visibility.Collapsed;};
   ((Button)panel.FindName("ChooseVisible")).Click+=delegate{foreach(var r in choices.Where(Matches))r.Selected=true;};
   ((Button)panel.FindName("ChooseNone")).Click+=delegate{foreach(var r in choices)r.Selected=false;};
   confirm.Click+=delegate{if(Chosen.Count>0)DialogResult=true;};update();
   grid.EnableColumnVirtualization=false;TablePresentation.Configure(this,grid);
   HistoryPresentation.Configure(grid);HistoryPresentation.ApplyOrder(grid);
   Chrome.Dialog(this,true);ModernDialog.CompactCaption(this);
  }
  bool Matches(AppRow row){string q=search.Text.Trim();return q.Length==0||((row.Name??"")+" "+row.ProcessName+" "+row.Path).IndexOf(q,StringComparison.CurrentCultureIgnoreCase)>=0;}
 }
}
