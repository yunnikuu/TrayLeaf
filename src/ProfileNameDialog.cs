using System;using System.Windows;using System.Windows.Controls;using System.Windows.Media;
namespace TrayLeaf { internal sealed class ProfileNameDialog:Window {
 internal ProfileNameDialog(Window owner,string initial,Action<string> save){Owner=owner;Title="保存配置";Width=460;SizeToContent=SizeToContent.Height;MinWidth=380;WindowStartupLocation=WindowStartupLocation.CenterOwner;Resources=TrayMenuPresentation.ConfirmationResources(owner);FontFamily=owner.FontFamily;FontSize=owner.FontSize;Foreground=(Brush)Resources["Text"];Background=(Brush)Resources["PopupSurface"];UseLayoutRounding=true;SnapsToDevicePixels=true;
 var root=new Grid{Margin=new Thickness(24),Background=(Brush)Resources["Surface"]};for(int i=0;i<4;i++)root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Content=root;
 root.Children.Add(new TextBlock{Text="配置名称",FontWeight=FontWeights.SemiBold,FontSize=18,Margin=new Thickness(0,0,0,16)});
 var name=new TextBox{Name="ProfileNameInput",Text=initial??"",MaxLength=50,MinWidth=240};Grid.SetRow(name,1);root.Children.Add(name);
 var error=new TextBlock{Foreground=(Brush)Resources["Warning"],TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)};Grid.SetRow(error,2);root.Children.Add(error);
 var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,24,0,0)};Grid.SetRow(actions,3);root.Children.Add(actions);
 actions.Children.Add(new Button{Content="取消",IsCancel=true});var ok=new Button{Content="确认",IsDefault=true,Style=(Style)FindResource("Primary"),Margin=new Thickness(0)};actions.Children.Add(ok);ok.Click+=delegate{try{save(name.Text.Trim());DialogResult=true;}catch(Exception ex){error.Text=UiLanguage.Text(ex.Message);}};
 Chrome.Dialog(this,true);UiLanguage.Apply(this);Loaded+=delegate{name.Focus();name.SelectAll();};
 } } }
