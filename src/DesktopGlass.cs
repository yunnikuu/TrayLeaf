using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace TrayLeaf {
 // Downsample only the window's backdrop. Never persist or transmit desktop pixels.
 internal static class DesktopGlass {
  static readonly List<Session> sessions=new List<Session>();
  static readonly object captureGate=new object();
  static readonly bool supported=CaptureSupported();
  internal static int Frames(Window w){var s=sessions.FirstOrDefault(x=>x.Window==w);return s==null?0:s.FrameCount;}
  internal static bool Active(Window w){return sessions.Any(x=>x.Window==w);}
  internal static int FrameInterval(bool active,int unchanged,bool moving){return moving?16:unchanged>=4?125:active?16:100;}
  internal static void Set(Window w,bool enabled){var s=sessions.FirstOrDefault(x=>x.Window==w);if(!enabled){if(s!=null){s.Stop();sessions.Remove(s);}Restore(w);return;}if(s==null){s=new Session(w);sessions.Add(s);}s.Request(true);}
  static void Restore(Window w){var surface=w.FindName("WindowSurface") as Border;if(surface!=null){surface.SetResourceReference(Border.BackgroundProperty,"Canvas");w.Background=Brushes.Transparent;}else {var panel=w.Content as Panel;if(panel!=null)panel.SetResourceReference(Panel.BackgroundProperty,"Canvas");var border=w.Content as Border;if(border!=null)border.SetResourceReference(Border.BackgroundProperty,"Canvas");w.Background=Brushes.Transparent;}}
  internal static Brush Compose(BitmapSource image,Color tint,bool dark){
   var group=new DrawingGroup();group.Children.Add(new ImageDrawing(image,new Rect(0,0,1,1)));
   tint.A=(byte)(dark?168:198);group.Children.Add(new GeometryDrawing(new SolidColorBrush(tint),null,new RectangleGeometry(new Rect(0,0,1,1))));
   var brush=new DrawingBrush(group){Stretch=Stretch.Fill,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,1,1)};RenderOptions.SetBitmapScalingMode(brush,BitmapScalingMode.HighQuality);brush.Freeze();return brush;
  }
  sealed class Session {
   internal readonly Window Window;internal int FrameCount;readonly DispatcherTimer timer;readonly CaptureBuffer buffer=new CaptureBuffer();
   bool pending,refresh,forcePaint;volatile bool stopped;int geometry,unchanged;long moved;BitmapSource lastImage;
   internal Session(Window w){Window=w;timer=new DispatcherTimer(DispatcherPriority.Render,w.Dispatcher);timer.Tick+=Tick;w.Closed+=Closed;w.LocationChanged+=Moved;w.SizeChanged+=Sized;w.IsVisibleChanged+=VisibilityChanged;w.StateChanged+=StateChanged;Wake();}
   bool Visible {get{return Window.IsVisible&&Window.WindowState!=WindowState.Minimized;}}
   bool Moving {get{return Stopwatch.GetTimestamp()-moved<Stopwatch.Frequency/3;}}
   void Pace(){var interval=TimeSpan.FromMilliseconds(FrameInterval(Window.IsActive,unchanged,Moving));if(timer.Interval!=interval)timer.Interval=interval;}
   void Wake(){if(stopped)return;if(Visible){Pace();timer.Start();Request(true);}else{geometry++;timer.Stop();}}
   void Tick(object sender,EventArgs e){Request(false);}void Moved(object sender,EventArgs e){moved=Stopwatch.GetTimestamp();unchanged=0;Pace();if(pending)refresh=true;else Request(false);}void Sized(object sender,SizeChangedEventArgs e){geometry++;Moved(sender,e);}void VisibilityChanged(object sender,DependencyPropertyChangedEventArgs e){Wake();}void StateChanged(object sender,EventArgs e){Wake();}void Closed(object sender,EventArgs e){Stop();sessions.Remove(this);}
   internal void Stop(){if(stopped)return;stopped=true;geometry++;timer.Stop();Window.Closed-=Closed;Window.LocationChanged-=Moved;Window.SizeChanged-=Sized;Window.IsVisibleChanged-=VisibilityChanged;Window.StateChanged-=StateChanged;lastImage=null;if(!pending)buffer.Dispose();}
   internal void Request(bool force){
    if(stopped||!Visible)return;forcePaint|=force;if(pending){refresh|=force;return;}
    IntPtr hwnd=new WindowInteropHelper(Window).Handle;RectI rect;if(hwnd==IntPtr.Zero||!GetWindowRect(hwnd,out rect)||rect.Right-rect.Left<4||rect.Bottom-rect.Top<4)return;
    var exclude=sessions.Where(x=>!x.stopped&&x.Visible).Select(x=>new WindowInteropHelper(x.Window).Handle).Where(x=>x!=IntPtr.Zero).ToArray();pending=true;refresh=false;int stamp=geometry;
    // Translation does not invalidate a completed preview: continuous motion must not starve publication.
    // Read the latest physical position on the worker; resize/visibility epochs still reject incompatible frames.
    Task.Run(delegate{try{RectI latest;if(!GetWindowRect(hwnd,out latest)||latest.Right-latest.Left!=rect.Right-rect.Left||latest.Bottom-latest.Top!=rect.Bottom-rect.Top)return null;return Capture(latest,exclude,buffer);}finally{if(stopped)buffer.Dispose();}}).ContinueWith(task=>{
     if(Window.Dispatcher.HasShutdownStarted){buffer.Dispose();return;}
     Window.Dispatcher.BeginInvoke(new Action(delegate{
      pending=false;if(stopped){buffer.Dispose();return;}
      if(!Visible||stamp!=geometry){buffer.Valid=false;if(Visible)Request(true);return;}
      var result=task.Status==TaskStatus.RanToCompletion?task.Result:null;
      if(task.IsFaulted){var observed=task.Exception;}
      if(result!=null){unchanged=result.Changed?0:unchanged+1;if(result.Image!=null)lastImage=result.Image;
       if(lastImage!=null&&(result.Changed||forcePaint)){var color=((SolidColorBrush)Window.Resources["Canvas"]).Color;bool dark=((SolidColorBrush)Window.Resources["Text"]).Color.R>128;var brush=Compose(lastImage,color,dark);var surface=Window.FindName("WindowSurface") as Border;if(surface!=null)surface.Background=brush;else {var panel=Window.Content as Panel;if(panel!=null)panel.Background=brush;var border=Window.Content as Border;if(border!=null)border.Background=brush;}Window.Background=Brushes.Transparent;FrameCount++;forcePaint=false;}
      }else unchanged=Math.Max(unchanged,4);
      Pace();if(refresh)Request(false);
     }),DispatcherPriority.Render);
    });
   }
  }
  sealed class CaptureResult {internal BitmapSource Image;internal bool Changed;}
  // One worker owns each buffer. Stop disposes immediately, or the in-flight worker disposes it.
  sealed class CaptureBuffer:IDisposable {
   internal IntPtr Dc,Bitmap,Previous,Bits;internal int Width,Height;internal byte[] Pixels,Last,First,Second;internal int[] Horizontal;internal bool Valid;
   internal bool Ensure(IntPtr screen,int w,int h){if(Dc!=IntPtr.Zero&&Width==w&&Height==h)return true;Dispose();Dc=CreateCompatibleDC(screen);if(Dc==IntPtr.Zero)return false;
    var header=new BitmapHeader{Size=(uint)Marshal.SizeOf(typeof(BitmapHeader)),Width=w,Height=-h,Planes=1,Bits=32};Bitmap=CreateDIBSection(Dc,ref header,0,out Bits,IntPtr.Zero,0);if(Bitmap==IntPtr.Zero||Bits==IntPtr.Zero){Dispose();return false;}Previous=SelectObject(Dc,Bitmap);if(Previous==IntPtr.Zero||Previous==new IntPtr(-1)){Dispose();return false;}SetStretchBltMode(Dc,4);SetBrushOrgEx(Dc,0,0,IntPtr.Zero);Width=w;Height=h;Pixels=new byte[w*h*4];Last=new byte[Pixels.Length];First=new byte[Pixels.Length];Second=new byte[Pixels.Length];Horizontal=new int[w*h*3];return true;
   }
   public void Dispose(){if(Dc!=IntPtr.Zero&&Previous!=IntPtr.Zero&&Previous!=new IntPtr(-1))SelectObject(Dc,Previous);if(Bitmap!=IntPtr.Zero)DeleteObject(Bitmap);if(Dc!=IntPtr.Zero)DeleteDC(Dc);Dc=Bitmap=Previous=Bits=IntPtr.Zero;Pixels=Last=First=Second=null;Horizontal=null;Valid=false;}
  }
  [StructLayout(LayoutKind.Sequential)]internal struct RectI {internal int Left,Top,Right,Bottom;}
  [StructLayout(LayoutKind.Sequential)]struct BitmapHeader {internal uint Size;internal int Width,Height;internal ushort Planes,Bits;internal uint Compression,SizeImage;internal int XPels,YPels;internal uint Used,Important;}
  [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr w,out RectI rect);
  [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr w);
  [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr w,IntPtr dc);
  [DllImport("user32.dll")]static extern bool SetWindowDisplayAffinity(IntPtr w,uint affinity);
  [DllImport("user32.dll")]static extern bool GetWindowDisplayAffinity(IntPtr w,out uint affinity);
  [DllImport("dwmapi.dll")]static extern int DwmFlush();
  [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr dc);
  [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
  [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr o);
  [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr o);
  [DllImport("gdi32.dll")]static extern int SetStretchBltMode(IntPtr dc,int mode);
  [DllImport("gdi32.dll")]static extern bool SetBrushOrgEx(IntPtr dc,int x,int y,IntPtr old);
  [DllImport("gdi32.dll")]static extern IntPtr CreateDIBSection(IntPtr dc,ref BitmapHeader info,uint usage,out IntPtr bits,IntPtr section,uint offset);
  [DllImport("gdi32.dll")]static extern bool StretchBlt(IntPtr dc,int x,int y,int width,int height,IntPtr source,int sx,int sy,int sw,int sh,uint operation);
  static bool CaptureSupported(){try{int build;using(var key=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))return Int32.TryParse(Convert.ToString(key==null?null:key.GetValue("CurrentBuildNumber")),out build)&&build>=19041;}catch{return false;}}
  static CaptureResult Capture(RectI r,IntPtr[] windows,CaptureBuffer buffer){
   if(!supported||!Monitor.TryEnter(captureGate))return null;var affinity=new List<KeyValuePair<IntPtr,uint>>();IntPtr screen=IntPtr.Zero;bool copied=false;
   try{
    screen=GetDC(IntPtr.Zero);if(screen==IntPtr.Zero)return null;
    int width=r.Right-r.Left,height=r.Bottom-r.Top,tw=Math.Max(2,Math.Min(220,width/12)),th=Math.Max(2,Math.Min(150,height/12));if(!buffer.Ensure(screen,tw,th))return null;
    try{foreach(var w in windows){uint prior;if(!GetWindowDisplayAffinity(w,out prior))return null;if(prior==0x11)continue;if(!SetWindowDisplayAffinity(w,0x11))return null;affinity.Add(new KeyValuePair<IntPtr,uint>(w,prior));}
     if(DwmFlush()<0)return null;
     copied=StretchBlt(buffer.Dc,0,0,tw,th,screen,r.Left,r.Top,width,height,0x00CC0020);
     // GDI batches on this worker thread: finish writes before reading DIB memory.
     if(copied)copied=GdiFlush();
     if(copied)Marshal.Copy(buffer.Bits,buffer.Pixels,0,buffer.Pixels.Length);
    }finally{foreach(var pair in affinity)SetWindowDisplayAffinity(pair.Key,pair.Value);affinity.Clear();}
   }catch{return null;}
   finally{if(screen!=IntPtr.Zero)ReleaseDC(IntPtr.Zero,screen);Monitor.Exit(captureGate);}
   if(!copied)return null;
   bool changed=!buffer.Valid;for(int i=0;!changed&&i<buffer.Pixels.Length;i+=4)changed=buffer.Pixels[i]!=buffer.Last[i]||buffer.Pixels[i+1]!=buffer.Last[i+1]||buffer.Pixels[i+2]!=buffer.Last[i+2];
   if(!changed)return new CaptureResult{Changed=false};
   Buffer.BlockCopy(buffer.Pixels,0,buffer.Last,0,buffer.Pixels.Length);buffer.Valid=true;
   BlurPass(buffer.Pixels,buffer.First,buffer.Horizontal,buffer.Width,buffer.Height);BlurPass(buffer.First,buffer.Second,buffer.Horizontal,buffer.Width,buffer.Height);
   var image=BitmapSource.Create(buffer.Width,buffer.Height,96,96,PixelFormats.Bgra32,null,buffer.Second,buffer.Width*4);image.Freeze();return new CaptureResult{Changed=true,Image=image};
  }
  [DllImport("gdi32.dll")]static extern bool GdiFlush();
  // Preserve the exact original two 3x3 box passes, including clamped edges and integer rounding.
  static void BlurPass(byte[] src,byte[] dst,int[] sums,int width,int height){
   for(int y=0;y<height;y++)for(int x=0;x<width;x++){int p=(y*width+x)*4,l=(y*width+Math.Max(0,x-1))*4,r=(y*width+Math.Min(width-1,x+1))*4,h=(y*width+x)*3;for(int c=0;c<3;c++)sums[h+c]=src[l+c]+src[p+c]+src[r+c];}
   for(int y=0;y<height;y++)for(int x=0;x<width;x++){int h=(y*width+x)*3,t=(Math.Max(0,y-1)*width+x)*3,b=(Math.Min(height-1,y+1)*width+x)*3,p=(y*width+x)*4;for(int c=0;c<3;c++)dst[p+c]=(byte)((sums[t+c]+sums[h+c]+sums[b+c])/9);dst[p+3]=255;}
  }
  internal static byte[] Blur(byte[] input,int width,int height){if(input==null)throw new ArgumentNullException("input");if(width<1||height<1||(long)width*height*4!=input.Length)throw new ArgumentException("Invalid blur dimensions");var first=new byte[input.Length];var second=new byte[input.Length];var horizontal=new int[width*height*3];BlurPass(input,first,horizontal,width,height);BlurPass(first,second,horizontal,width,height);return second;}
 }
}
