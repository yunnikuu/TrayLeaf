using System;using System.Windows;using System.Windows.Controls;using System.Windows.Media;
namespace TrayLeaf {
 internal sealed class CloseWindowChoice {internal bool? Minimize;internal CheckBox Suppress;}
 internal static class CloseWindowDialog {
  internal static Window Build(Window owner){
   double scale=Math.Max(.85,owner.FontSize/13d);
   var state=new CloseWindowChoice();
   var d=new Window{Owner=owner,Title="",Width=Math.Min(540*scale,SystemParameters.WorkArea.Width-48),SizeToContent=SizeToContent.Height,MaxHeight=SystemParameters.WorkArea.Height-48,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false,FontFamily=owner.FontFamily,FontSize=owner.FontSize,Resources=owner.Resources,Tag=state};
   var panel=new StackPanel{Margin=new Thickness(26,24,26,24)};
   var heading=new TextBlock{Name="ClosePromptTitle",Text="是否最小化窗口",FontSize=20*scale,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,34,0)};heading.SetResourceReference(TextBlock.ForegroundProperty,"Text");panel.Children.Add(heading);
   var body=new TextBlock{Name="ClosePromptBody",Text="最小化后将隐藏到托盘继续运行\n选择退出将结束程序",TextWrapping=TextWrapping.Wrap,LineHeight=22*scale,Margin=new Thickness(0,18,0,22)};body.SetResourceReference(TextBlock.ForegroundProperty,"Muted");panel.Children.Add(body);
   var footer=new Grid();footer.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});footer.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var suppress=new CheckBox{Name="ClosePromptSuppress",Content="以后不再提示",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,16,0)};suppress.SetResourceReference(Control.ForegroundProperty,"Text");System.Windows.Automation.AutomationProperties.SetName(suppress,"以后不再提示关闭窗口确认");state.Suppress=suppress;footer.Children.Add(suppress);
   var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};Grid.SetColumn(actions,1);footer.Children.Add(actions);
   var exit=FeatureUi.Button("否，退出",delegate{state.Minimize=false;d.DialogResult=true;},false);exit.Name="ExitChoice";exit.MinWidth=94*scale;exit.Margin=new Thickness(0,0,10,0);actions.Children.Add(exit);
   var minimize=FeatureUi.Button("是，最小化",delegate{state.Minimize=true;d.DialogResult=true;},true);minimize.Name="MinimizeChoice";minimize.MinWidth=112*scale;minimize.Margin=new Thickness(0);minimize.IsDefault=true;actions.Children.Add(minimize);panel.Children.Add(footer);
   d.Content=panel;Chrome.Decorate(d,false);UiLanguage.Apply(d);return d;
  }
 }
}
