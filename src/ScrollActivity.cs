using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
namespace TrayLeaf {
 internal static class ScrollActivity {
  sealed class Activity {
   readonly ScrollViewer viewer;readonly List<ScrollBar> bars;readonly DispatcherTimer timer;
   internal Activity(ScrollViewer viewer){this.viewer=viewer;bars=new List<ScrollBar>();timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(950)};timer.Tick+=delegate{EnsureBars();if(viewer.IsMouseCaptureWithin){timer.Stop();timer.Start();return;}timer.Stop();foreach(var bar in bars)bar.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,TimeSpan.FromMilliseconds(350)));};
    EnsureBars();viewer.IsVisibleChanged+=delegate{if(viewer.IsVisible)viewer.Dispatcher.BeginInvoke(new Action(EnsureBars),DispatcherPriority.Loaded);};
    viewer.ScrollChanged+=delegate(object sender,ScrollChangedEventArgs e){if(e.OriginalSource==viewer&&(e.VerticalChange!=0||e.HorizontalChange!=0))Show();};viewer.PreviewMouseWheel+=delegate{Show();};viewer.PreviewKeyDown+=delegate(object sender,System.Windows.Input.KeyEventArgs e){if(e.Key==System.Windows.Input.Key.PageDown||e.Key==System.Windows.Input.Key.PageUp||e.Key==System.Windows.Input.Key.Down||e.Key==System.Windows.Input.Key.Up||e.Key==System.Windows.Input.Key.Home||e.Key==System.Windows.Input.Key.End)Show();};viewer.Unloaded+=delegate{timer.Stop();};
   }
   void EnsureBars(){viewer.ApplyTemplate();foreach(string name in new[]{"PART_VerticalScrollBar","PART_HorizontalScrollBar"}){var bar=viewer.Template==null?null:viewer.Template.FindName(name,viewer) as ScrollBar;if(bar==null||bars.Contains(bar))continue;bars.Add(bar);bar.Opacity=0;bar.MouseEnter+=delegate{Show();};bar.MouseLeave+=delegate{Restart();};bar.GotMouseCapture+=delegate{Show();};bar.LostMouseCapture+=delegate{Restart();};}}
   internal void Show(){EnsureBars();foreach(var bar in bars)bar.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(1,TimeSpan.FromMilliseconds(130)));Restart();}
   void Restart(){timer.Stop();timer.Start();}
  }
  internal static IEnumerable<T> Find<T>(DependencyObject root) where T:DependencyObject {for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);var match=child as T;if(match!=null)yield return match;foreach(var nested in Find<T>(child))yield return nested;}}
  internal static void Attach(FrameworkElement root){bool attached=false;RoutedEventHandler install=delegate{if(attached)return;root.UpdateLayout();var viewers=new List<ScrollViewer>();var direct=root as ScrollViewer;if(direct!=null)viewers.Add(direct);viewers.AddRange(Find<ScrollViewer>(root));foreach(var viewer in viewers)new Activity(viewer);attached=true;};root.Loaded+=install;if(root.IsLoaded)install(root,new RoutedEventArgs());}
 }
}
