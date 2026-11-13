using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
namespace TrayLeaf {
 // Translates presentation only. Process identities, settings keys, commands and
 // persistent model states remain canonical and are never converted in-place.
 internal static class UiLanguage {
  internal static string Code {get;private set;}
  static readonly object gate=new object();
  static readonly Dictionary<string,string> translated=new Dictionary<string,string>(StringComparer.Ordinal);
  static readonly Dictionary<string,string> original=new Dictionary<string,string>(StringComparer.Ordinal);
  static readonly List<BoundSlot> bindings=new List<BoundSlot>();
  static readonly ConditionalWeakTable<DependencyObject,object> hooked=new ConditionalWeakTable<DependencyObject,object>();
  static readonly List<Slot> slots=new List<Slot>();
  static readonly ConditionalWeakTable<DependencyObject,List<Slot>> tracked=new ConditionalWeakTable<DependencyObject,List<Slot>>();
  static readonly HashSet<string> catalog=new HashSet<string>(LanguageCatalog.Values,StringComparer.Ordinal);
  static Regex fragments;
  static readonly Regex paths=new Regex(@"(?:[A-Za-z]:[\\/]|\\\\|https?://)[^\r\n\t""<>|；]+",RegexOptions.Compiled);
  static readonly Dictionary<string,string> terms=new Dictionary<string,string>{
   {"应用程序","應用程式"},{"进程","處理程序"},{"程序","程式"},{"软件","軟體"},{"文件夹","資料夾"},{"文件","檔案"},{"数据","資料"},{"内存","記憶體"},{"鼠标","滑鼠"},{"托盘","系統匣"},{"任务栏","工作列"},{"管理员","系統管理員"},{"保存","儲存"},{"默认","預設"},{"自定义","自訂"},{"添加","新增"},{"恢复","還原"},{"运行","執行"},{"配置","設定檔"},{"设置","設定"},{"刷新","重新整理"},{"菜单","選單"},{"后台","背景"},{"窗口","視窗"},{"界面","介面"},{"路径","路徑"},{"快捷键","快捷鍵"},{"勾选","勾選"},{"选项","選項"},{"屏幕","螢幕"},{"打印","列印"},{"网络","網路"},{"卸载","解除安裝"},{"信息","資訊"},{"后","後"},{"图标","圖示"},{"普通应用","一般應用程式"}
  };
  static readonly Regex terminology=new Regex(String.Join("|",terms.Keys.OrderByDescending(x=>x.Length).Select(Regex.Escape)),RegexOptions.Compiled);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern int LCMapStringEx(string locale,uint flags,string source,int sourceLength,StringBuilder destination,int destinationLength,IntPtr version,IntPtr reserved,IntPtr sortHandle);
  static UiLanguage(){Code="zh-Hans";EventManager.RegisterClassHandler(typeof(FrameworkElement),FrameworkElement.LoadedEvent,new RoutedEventHandler(Loaded));EventManager.RegisterClassHandler(typeof(FrameworkElement),FrameworkElement.UnloadedEvent,new RoutedEventHandler(Unloaded));}
  static void Loaded(object sender,RoutedEventArgs e){Attach(sender as DependencyObject);}
  static void Unloaded(object sender,RoutedEventArgs e){var target=sender as DependencyObject;if(target==null)return;List<Slot> list;if(tracked.TryGetValue(target,out list))foreach(var slot in list)slot.Detach();var grid=target as DataGrid;if(grid!=null)foreach(var column in grid.Columns){if(tracked.TryGetValue(column,out list))foreach(var slot in list)slot.Detach();}}
  internal static string Traditional(string value){if(String.IsNullOrEmpty(value))return value;string termsApplied=terminology.Replace(value,m=>terms[m.Value]);int length=LCMapStringEx("zh-TW",0x04000000,termsApplied,termsApplied.Length,null,0,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);if(length==0)throw new Win32Exception(Marshal.GetLastWin32Error());var result=new StringBuilder(length+1);if(LCMapStringEx("zh-TW",0x04000000,termsApplied,termsApplied.Length,result,length+1,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero)==0)throw new Win32Exception(Marshal.GetLastWin32Error());return result.ToString(0,length);}
  static void Prepare(){lock(gate){if(fragments!=null)return;foreach(string value in catalog){string result=Traditional(value);translated[value]=result;if(!original.ContainsKey(result))original[result]=value;}fragments=new Regex(String.Join("|",catalog.Where(x=>x.Length>=4&&translated[x]!=x).OrderByDescending(x=>x.Length).Select(Regex.Escape)),RegexOptions.Compiled);}}
  internal static string Text(string value){if(value==null||Code!="zh-Hant")return value;Prepare();string exact;if(translated.TryGetValue(value,out exact))return exact;return ProtectPaths(value,part=>fragments.Replace(part,m=>translated[m.Value]));}
  static string Canonical(string value){if(value==null)return null;Prepare();string result;return original.TryGetValue(value,out result)?result:value;}
  static string ProtectPaths(string value,Func<string,string> translate){var result=new StringBuilder();int start=0;foreach(Match match in paths.Matches(value)){result.Append(translate(value.Substring(start,match.Index-start)));result.Append(match.Value);start=match.Index+match.Length;}result.Append(translate(value.Substring(start)));return result.ToString();}
  internal static void Set(string code){string next=code=="zh-Hant"?"zh-Hant":"zh-Hans";if(next==Code)return;Prepare();Code=next;foreach(var slot in slots.ToArray())if(slot.Active)slot.Update();foreach(var binding in bindings.ToArray())binding.Update();slots.RemoveAll(s=>!s.Alive);bindings.RemoveAll(b=>!b.Alive);foreach(Window window in Application.Current==null?new Window[0]:Application.Current.Windows.Cast<Window>().ToArray())window.Language=XmlLanguage.GetLanguage(Code=="zh-Hant"?"zh-TW":"zh-CN");}
  internal static void Apply(DependencyObject root){if(root==null)return;var seen=new HashSet<DependencyObject>();Walk(root,seen);}
  static void Walk(DependencyObject root,HashSet<DependencyObject> seen){if(root==null||!seen.Add(root))return;Attach(root);var content=root as ContentControl;if(content!=null&&content.Content is DependencyObject)Walk((DependencyObject)content.Content,seen);var items=root as ItemsControl;if(items!=null)foreach(object item in items.Items)if(item is DependencyObject)Walk((DependencyObject)item,seen);if(items!=null)for(int i=0;i<items.Items.Count;i++)Walk(items.ItemContainerGenerator.ContainerFromIndex(i),seen);foreach(object child in LogicalTreeHelper.GetChildren(root))if(child is DependencyObject)Walk((DependencyObject)child,seen);if(root is Visual||root is System.Windows.Media.Media3D.Visual3D)for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)Walk(VisualTreeHelper.GetChild(root,i),seen);}
  static void Attach(DependencyObject target){if(target==null)return;
   object marker;if(!hooked.TryGetValue(target,out marker)){
    hooked.Add(target,new object());var owner=target as Window;if(owner!=null){owner.Loaded+=delegate{Apply(owner);};owner.ContentRendered+=delegate{Apply(owner);};owner.Closed+=delegate{foreach(var slot in slots.ToArray())if(slot.BelongsTo(owner))slot.Detach();};}
    var table=target as DataGrid;if(table!=null)table.LoadingRow+=delegate(object sender,DataGridRowEventArgs e){var row=e.Row;row.Dispatcher.BeginInvoke(new Action(()=>Apply(row)),System.Windows.Threading.DispatcherPriority.Loaded);};
    var combo=target as ComboBox;if(combo!=null){Action update=()=>combo.Dispatcher.BeginInvoke(new Action(()=>Apply(combo)),System.Windows.Threading.DispatcherPriority.Loaded);combo.DropDownOpened+=delegate{update();};combo.SelectionChanged+=delegate{update();};}
    var menu=target as ContextMenu;if(menu!=null)menu.Opened+=delegate{Apply(menu);};
   }
   var block=target as TextBlock;if(block!=null)LocalizeBindingOrTrack(block,TextBlock.TextProperty);
   if(target is ContentControl)Track(target,ContentControl.ContentProperty);if(target is HeaderedItemsControl)Track(target,HeaderedItemsControl.HeaderProperty);if(target is FrameworkElement)LocalizeBindingOrTrack(target,FrameworkElement.ToolTipProperty);var window=target as Window;if(window!=null){Track(window,Window.TitleProperty);window.Language=XmlLanguage.GetLanguage(Code=="zh-Hant"?"zh-TW":"zh-CN");}var grid=target as DataGrid;if(grid!=null)foreach(var column in grid.Columns)Track(column,DataGridColumn.HeaderProperty);var textbox=target as TextBox;if(textbox!=null&&(textbox.Name=="CloseHotkeyInput"||textbox.Name=="RestoreHotkeyInput"))Track(textbox,TextBox.TextProperty);
  }
  static void LocalizeBindingOrTrack(DependencyObject target,DependencyProperty property){
   var binding=BindingOperations.GetBinding(target,property);if(binding==null){Track(target,property);return;}
   string path=binding.Path==null?"":binding.Path.Path;
   bool savedLabel=path=="."&&(target is FrameworkElement)&&(((FrameworkElement)target).DataContext is SavedSet||((FrameworkElement)target).DataContext is BatchInfo);
   if((!savedLabel&&!new[]{"State","Source","RiskText","Hint","RiskReason","Error"}.Contains(path))||binding.Converter is LocalizedValueConverter)return;
   var next=new Binding{Path=binding.Path,Mode=binding.Mode,Converter=new LocalizedValueConverter(binding.Converter),ConverterParameter=binding.ConverterParameter,ConverterCulture=binding.ConverterCulture,StringFormat=binding.StringFormat,TargetNullValue=binding.TargetNullValue,FallbackValue=binding.FallbackValue,UpdateSourceTrigger=binding.UpdateSourceTrigger};
   if(binding.RelativeSource!=null)next.RelativeSource=binding.RelativeSource;else if(!String.IsNullOrEmpty(binding.ElementName))next.ElementName=binding.ElementName;else if(binding.Source!=null)next.Source=binding.Source;
   BindingOperations.SetBinding(target,property,next);bindings.Add(new BoundSlot(target,property));
  }
  sealed class BoundSlot{
   readonly WeakReference target;readonly DependencyProperty property;
   internal BoundSlot(DependencyObject target,DependencyProperty property){this.target=new WeakReference(target);this.property=property;}
   internal bool Alive{get{return target.IsAlive;}}
   internal void Update(){var value=target.Target as DependencyObject;if(value==null)return;var binding=BindingOperations.GetBindingExpression(value,property);if(binding!=null)binding.UpdateTarget();}
  }
  static void Track(DependencyObject target,DependencyProperty property){if(BindingOperations.IsDataBound(target,property))return;object value=target.GetValue(property);if(!(value is string))return;var list=tracked.GetOrCreateValue(target);var old=list.FirstOrDefault(x=>x.Property==property);if(old!=null){old.Attach();return;}var slot=new Slot(target,property);list.Add(slot);slots.Add(slot);slot.Attach();}
  sealed class Slot {
   readonly WeakReference target;readonly DependencyPropertyDescriptor descriptor;string raw;bool changing;internal readonly DependencyProperty Property;internal bool Active;
   internal bool Alive {get{return target.IsAlive;}}
   internal bool BelongsTo(Window window){var value=target.Target as DependencyObject;return value==window||(value is FrameworkElement&&Window.GetWindow(value)==window);}
   internal Slot(DependencyObject target,DependencyProperty property){this.target=new WeakReference(target);Property=property;descriptor=DependencyPropertyDescriptor.FromProperty(property,target.GetType());raw=Canonical(target.GetValue(property) as string);}
   internal void Attach(){var value=target.Target as DependencyObject;if(Active||value==null)return;Active=true;descriptor.AddValueChanged(value,Changed);Update();}
   internal void Detach(){var value=target.Target as DependencyObject;if(!Active)return;Active=false;if(value!=null)descriptor.RemoveValueChanged(value,Changed);}
   void Changed(object sender,EventArgs e){if(changing)return;var value=sender as DependencyObject;string current=value.GetValue(Property) as string;if(current==null){Detach();return;}raw=Canonical(current);Update();}
   internal void Update(){var value=target.Target as DependencyObject;if(value==null)return;try{changing=true;value.SetCurrentValue(Property,Text(raw));}finally{changing=false;}}
  }
  sealed class LocalizedValueConverter:IValueConverter {
   readonly IValueConverter inner;internal LocalizedValueConverter(IValueConverter inner){this.inner=inner;}
   public object Convert(object value,Type type,object parameter,CultureInfo culture){object result=inner==null?value:inner.Convert(value,type,parameter,culture);var saved=result as SavedSet;if(saved!=null)return saved.ToString();var batch=result as BatchInfo;if(batch!=null){string label=batch.Label??"";return label.EndsWith(" 项",StringComparison.Ordinal)?label.Substring(0,label.Length-2)+Text(" 项"):label;}return result is string?Text((string)result):result;}
   public object ConvertBack(object value,Type type,object parameter,CultureInfo culture){return Binding.DoNothing;}
  }
 }
}
