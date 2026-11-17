using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections.Generic;using System.Threading.Tasks;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Media.Imaging;using System.Windows.Threading;using TrayLeaf;
class Version112Tests {
 static int pass,fail;static Controller c;static Window w;static BindingFlags inst=BindingFlags.NonPublic|BindingFlags.Instance;
 static void Check(bool ok,string n){Console.WriteLine((ok?"PASS ":"FAIL ")+n);if(ok)pass++;else fail++;}
 static void Pump(int ms){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
 static T El<T>(string name)where T:class{return w.FindName(name)as T;}
 static object Call(string name,params object[] args){return typeof(Controller).GetMethod(name,inst).Invoke(c,args);}
 static IEnumerable<T> Find<T>(DependencyObject o)where T:DependencyObject{if(o is T)yield return(T)o;for(int i=0;i<VisualTreeHelper.GetChildrenCount(o);i++)foreach(var item in Find<T>(VisualTreeHelper.GetChild(o,i)))yield return item;}
 static double Light(Brush brush){var color=((SolidColorBrush)brush).Color;return (color.R+color.G+color.B)/(3d*255);}
 static void Snapshot(Window target,string path){target.UpdateLayout();var b=new RenderTargetBitmap((int)Math.Ceiling(target.ActualWidth),(int)Math.Ceiling(target.ActualHeight),96,96,PixelFormats.Pbgra32);b.Render(target);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(b));using(var stream=File.Create(path))encoder.Save(stream);}
 static ProfileEditor Editor(Func<Window,IEnumerable<AppRow>> picker){return new ProfileEditor(w,"restore","My profile",new[]{new AppRow{Name="Saved app",Path=@"C:\fixture\saved.exe",Args="--saved",Admin=true,Selected=true}},delegate{},picker);}
 [STAThread] static int Main(string[] args){try{
  Directory.CreateDirectory(args[0]);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TrayLeaf.MainWindow.xaml"))w=(Window)System.Windows.Markup.XamlReader.Load(stream);
  using(var db=new Database(Path.Combine(args[0],"ui-"+Guid.NewGuid()+".db"))){c=new Controller(app,w,db,true);c.Start(null);Pump(80);
   Check(AppIdentity.ReleaseVersion=="1.1.2","release-version-112");
   foreach(bool dark in new[]{false,true}){El<ComboBox>("ThemeInput").SelectedIndex=dark?1:0;El<ComboBox>("AccentInput").SelectedIndex=4;El<ComboBox>("WindowTintInput").SelectedIndex=5;Call("PreviewAppearance");
    var dialog=ModernDialog.Build(w,"是否更新到最新版本","",true,true);dialog.Show();Pump(20);var surface=(SolidColorBrush)dialog.Resources["Surface"];Check(surface.Color.A==255&&(dark?Light(surface)<.3:Light(surface)>.9),"confirmation-solid-mode-"+dark);Check(!Object.ReferenceEquals(dialog.Resources,w.Resources)&&!NativeBackdrop.Enabled(dialog),"confirmation-independent-"+dark);if(dark)Snapshot(dialog,Path.Combine(args[0],"dark-confirm112.png"));dialog.Close();
    var closing=CloseWindowDialog.Build(w);Check(dark?Light((Brush)closing.Resources["Surface"])<.3:Light((Brush)closing.Resources["Surface"])>.9,"close-window-mode-"+dark);closing.Close();
    var naming=new ProfileNameDialog(w,"",delegate{});Check(dark?Light((Brush)naming.Resources["Surface"])<.3:Light((Brush)naming.Resources["Surface"])>.9,"profile-name-mode-"+dark);naming.Close();
    Check(Light((Brush)TrayMenuPresentation.DefaultResources(w)["Surface"])>.9,"tray-menu-stays-default-"+dark);
   }
   El<ComboBox>("WindowTintInput").SelectedIndex=0;El<ComboBox>("ScanMode").SelectedIndex=1;
   var picker=(Window)typeof(Controller).GetMethod("CreateProgramPicker",inst).Invoke(c,new object[]{w,"添加程序",new Action<AppRow>(r=>{}),false});Check(Find<ComboBox>((DependencyObject)picker.Content).First().SelectedIndex==0,"shared-picker-defaults-to-tray");picker.Close();
   var editor=Editor(owner=>new[]{new AppRow{Name="Manual app",Path=@"C:\fixture\manual.exe",Args="--manual",Selected=true}});
   editor.Show();Pump(20);var scope=Find<ComboBox>(editor).FirstOrDefault();Check(scope!=null&&scope.Items.Count==2&&scope.SelectedIndex==0,"profile-editor-has-default-tray-scope");
   var provider=typeof(ProfileEditor).GetMethod("SetCandidateProvider",inst);Check(provider!=null,"profile-editor-can-load-other-running-apps");
   if(provider!=null&&scope!=null){var calls=new List<bool>();Func<bool,Task<List<AppRow>>> fetch=all=>{calls.Add(all);return Task.FromResult(new List<AppRow>{new AppRow{Name=all?"All candidate":"Tray candidate",Path=all?@"C:\fixture\all.exe":@"C:\fixture\tray.exe",Args="--candidate",Admin=false}});};provider.Invoke(editor,new object[]{fetch});Pump(50);var table=Find<DataGrid>(editor).First();Check(calls.Count>0&&!calls[0]&&table.Items.OfType<AppRow>().Any(r=>r.Name=="Tray candidate"),"profile-editor-loads-tray-candidates");
    Check(editor.Rows.Count==1&&editor.Rows[0].Args=="--saved"&&editor.Rows[0].Admin,"unselected-candidates-not-saved");
    scope.SelectedIndex=1;Pump(50);Check(calls.Last()&&table.Items.OfType<AppRow>().Any(r=>r.Name=="All candidate"),"profile-editor-all-scope-refresh");
    table.Items.OfType<AppRow>().First(r=>r.Name=="All candidate").Selected=true;Check(editor.Rows.Count==2&&editor.Rows.Any(r=>r.Args=="--saved"&&r.Admin),"chosen-candidate-retains-existing-metadata");
   }
   Find<Button>(editor).First(b=>Convert.ToString(b.Content)=="添加程序").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(editor.Rows.Any(r=>r.Path.EndsWith("manual.exe")&&r.Args=="--manual"),"profile-can-still-add-manually");editor.Close();
   Call("Navigate",2);Call("SelectSettings",0);Pump(40);var language=El<ComboBox>("LanguageInput");Check(language.Items.Count==3,"english-listed-in-language-selector");Check(language.ActualWidth<=((FrameworkElement)language.Parent).ActualWidth*.55,"language-selector-half-width");
   El<TextBlock>("StatusText").Text="";UiLanguage.Set("en");Pump(40);Check(UiLanguage.Code=="en"&&String.Equals(w.Language.IetfLanguageTag,"en-US",StringComparison.OrdinalIgnoreCase),"english-language-code-and-culture");Check(Find<TextBlock>(El<Button>("NavSettings")).Last().Text=="Settings","english-navigation");
   Check(UiLanguage.Text("是否更新到最新版本")=="Update to the latest version?","english-confirmation-copy");
   var formatted=String.Format(UiLanguage.Text("恢复进度{0}/{1}"),2,7);Check(formatted=="Restore progress 2/7","english-restore-progress-template");
   Check(UiLanguage.Text("已勾选3个程序")=="3 apps selected","english-dynamic-selected-count");
   Check(UiLanguage.Text(@"无法打开程序路径：C:\程序\中文.exe").Contains(@"C:\程序\中文.exe"),"english-does-not-translate-paths");
   var linkedSource=new TextBlock();System.Windows.Data.BindingOperations.SetBinding(linkedSource,TextBlock.TextProperty,new System.Windows.Data.Binding("Source.Source"){Source=new {Source=new AppRow{Source="通知区域"}}});UiLanguage.Apply(linkedSource);Check(linkedSource.Text=="Notification area","english-localizes-nested-association-source");
   var userLabel=new TextBlock{Text="设置",Tag="UserText"};UiLanguage.Apply(userLabel);Check(userLabel.Text=="设置","english-preserves-custom-display-names");
   Check(new SavedSet{Name="我的配置",Count=1}.ToString()=="我的配置  ·  1 item","english-profile-count-singular-and-name-preserved");
   var englishEditor=Editor(null);englishEditor.Show();Pump(20);Check(Find<Button>(englishEditor).Any(button=>Convert.ToString(button.Content)=="Add app")&&Find<ComboBox>(englishEditor).First().SelectedIndex==0,"english-profile-editor");Snapshot(englishEditor,Path.Combine(args[0],"english-profile-editor112.png"));englishEditor.Close();
   var menu=ProgramPresentation.PathMenu(new AppRow{Name="设置",Path=@"C:\fixture\app.exe"},delegate{},null,r=>{},r=>{});UiLanguage.Apply(menu);Check(Convert.ToString(((MenuItem)menu.Items[0]).Header)=="App path"&&Convert.ToString(((MenuItem)menu.Items[1]).Header)=="Custom process name","english-fresh-context-menu");
   var nameEntry=new ProfileNameDialog(w,"我的配置",delegate{});nameEntry.Show();Pump(10);Check(Find<TextBox>(nameEntry).First().Text=="我的配置"&&Find<Button>(nameEntry).Any(button=>Convert.ToString(button.Content)=="Confirm"),"english-dialog-retains-user-profile-name");nameEntry.Close();
   int missing=LanguageCatalog.Values.Count(value=>System.Text.RegularExpressions.Regex.IsMatch(UiLanguage.Text(value),@"[\u3400-\u9FFF]"));Check(missing==0,"english-all-inventory-phrases-covered");Console.WriteLine("ENGLISH_UNTRANSLATED="+missing);
   if(language.Items.Count==3){language.SelectedIndex=2;Call("SaveLiveSettings");Check(db.Get("uiLanguage","")=="en","english-preference-persists");}
   Call("Navigate",1);Call("SetRestoreProgress",2,7,(int)typeof(Controller).GetField("pageVisit",inst).GetValue(c));Check(El<TextBlock>("RestoreProgressText").Text=="Restore progress 2/7","english-live-progress");
   Snapshot(w,Path.Combine(args[0],"english-settings112.png"));
   UiLanguage.Set("zh-Hant");Pump(20);Check(Find<TextBlock>(El<Button>("NavSettings")).Last().Text=="設定","switch-english-to-traditional");
   Check(El<TextBlock>("RestoreProgressText").Text=="還原進度2/7","dynamic-progress-switches-to-traditional");
   UiLanguage.Set("zh-Hans");Pump(20);Check(Find<TextBlock>(El<Button>("NavSettings")).Last().Text=="设置","switch-back-to-simplified");
   Check(El<TextBlock>("StatusText").Text!="个","empty-text-stays-empty-after-language-roundtrip");
   var launch=El<CheckBox>("LaunchHidden");var admin=El<CheckBox>("RunAsAdminInput");var stack=launch.Parent as StackPanel;Check(Convert.ToString(launch.Content)=="静默启动"&&stack!=null&&stack.Children.IndexOf(launch)==stack.Children.IndexOf(admin)+1,"silent-start-label-and-position");
   El<Slider>("BackgroundOpacity").Value=.9;El<CheckBox>("LaunchHidden").IsChecked=true;Call("PreviewAppearance");Check(Math.Abs(El<Image>("BackgroundImage").Opacity-.9)<.001,"background-preview-ninety-percent");Call("SaveLiveSettings");Check(db.Get("backgroundOpacity","")=="0.9","background-opacity-persists");Check(db.Get("launchHidden","")=="1","silent-start-existing-preference-preserved");
   using(var restoredDb=new Database(Path.Combine(args[0],"reopened-"+Guid.NewGuid()+".db"))){restoredDb.Set("uiLanguage","en");restoredDb.Set("backgroundOpacity","0.9");restoredDb.Set("launchHidden","1");Window next;using(var source=Assembly.GetExecutingAssembly().GetManifestResourceStream("TrayLeaf.MainWindow.xaml"))next=(Window)System.Windows.Markup.XamlReader.Load(source);var nextController=new Controller(app,next,restoredDb,true);nextController.Start(null);Pump(20);Check(UiLanguage.Code=="en"&&((ComboBox)next.FindName("LanguageInput")).SelectedIndex==2,"english-restored-on-launch");Check(Math.Abs(((Image)next.FindName("BackgroundImage")).Opacity-.9)<.001,"ninety-percent-restored-on-launch");Check(((CheckBox)next.FindName("LaunchHidden")).IsChecked==true,"silent-start-restored-on-launch");typeof(Controller).GetField("exiting",inst).SetValue(nextController,true);next.Close();}
   UiLanguage.Set("zh-Hans");
   Call("Navigate",1);Pump(30);var refresh=El<Button>("ReloadHistoryButton");var save=El<Button>("SaveRestoreProfileButton");Point refreshPoint=refresh.TranslatePoint(new Point(0,refresh.ActualHeight/2),w),savePoint=save.TranslatePoint(new Point(0,save.ActualHeight/2),w);Check(Math.Abs(refreshPoint.Y-savePoint.Y)<2,"restore-refresh-aligned-with-profile-actions");Snapshot(w,Path.Combine(args[0],"restore-toolbar112.png"));
   UiLanguage.Set("en");El<Slider>("FontScaleInput").Value=150;Pump(50);var left=w.FindName("RestoreProfileActions") as FrameworkElement??save.Parent as FrameworkElement;var right=w.FindName("RestoreRefreshActions") as FrameworkElement??refresh.Parent as FrameworkElement;var leftOrigin=left.TranslatePoint(new Point(0,0),w);var rightOrigin=right.TranslatePoint(new Point(0,0),w);Check(leftOrigin.X+left.ActualWidth<=rightOrigin.X+1,"english-large-font-restore-actions-do-not-overlap");Snapshot(w,Path.Combine(args[0],"english-restore150112.png"));UiLanguage.Set("zh-Hans");
   typeof(Controller).GetField("exiting",inst).SetValue(c,true);w.Close();
  }
  Console.WriteLine("RESULT "+pass+" passed "+fail+" failed");return fail==0?0:1;
 }catch(Exception ex){Console.WriteLine("ERROR "+ex);return 2;}}
}
