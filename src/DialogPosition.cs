using System;using System.Windows;using System.Windows.Interop;using System.Windows.Media;using System.Runtime.InteropServices;
namespace TrayLeaf { internal static class DialogPosition {
 [StructLayout(LayoutKind.Sequential)]struct RectI{public int L,T,R,B;}
 [StructLayout(LayoutKind.Sequential)]struct MonitorInfo{public int Size;public RectI Monitor,Work;public int Flags;}
 [DllImport("user32.dll")]static extern IntPtr MonitorFromWindow(IntPtr h,int flags);
 [DllImport("user32.dll")]static extern bool GetMonitorInfo(IntPtr h,ref MonitorInfo info);
 internal static Rect Bounds(Window owner){var mi=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};if(!GetMonitorInfo(MonitorFromWindow(new WindowInteropHelper(owner).Handle,2),ref mi))return SystemParameters.WorkArea;var source=PresentationSource.FromVisual(owner);var transform=source!=null&&source.CompositionTarget!=null?source.CompositionTarget.TransformFromDevice:Matrix.Identity;var a=transform.Transform(new Point(mi.Work.L,mi.Work.T));var b=transform.Transform(new Point(mi.Work.R,mi.Work.B));return new Rect(a,b);}
 internal static void CenterOnOwner(Window dialog){dialog.WindowStartupLocation=WindowStartupLocation.Manual;dialog.Loaded+=delegate{var owner=dialog.Owner;if(owner==null)return;var area=Bounds(owner);dialog.Width=Math.Min(dialog.Width,area.Width-32);dialog.Height=Math.Min(dialog.Height,area.Height-32);dialog.MinWidth=Math.Min(dialog.MinWidth,dialog.Width);dialog.MinHeight=Math.Min(dialog.MinHeight,dialog.Height);dialog.Left=Math.Max(area.Left,Math.Min(area.Right-dialog.Width,owner.Left+(owner.ActualWidth-dialog.Width)/2));dialog.Top=Math.Max(area.Top,Math.Min(area.Bottom-dialog.Height,owner.Top+(owner.ActualHeight-dialog.Height)/2));};}
} }
