using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
[assembly: System.Reflection.AssemblyVersion("0.3.2.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.3.2.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("0.3.2")]
namespace TrayLeaf {
 // Decode once; OnLoad and Freeze release streams and share small immutable images.
 internal static class AppIdentity {
  static BitmapSource windowIcon,brandImage;
  static Stream Open(string name) {
   var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("TrayLeaf.Assets."+name);
   if(stream==null)throw new InvalidOperationException("缺少内嵌图标资源："+name);
   return stream;
  }
  internal static ImageSource WindowIcon {
   get {
    if(windowIcon==null)using(var stream=Open("TrayLeaf.ico")) {
     var decoder=new IconBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
     windowIcon=decoder.Frames.OrderByDescending(frame=>frame.PixelWidth).First();windowIcon.Freeze();
    }
    return windowIcon;
   }
  }
  internal static ImageSource BrandImage {
   get {
    if(brandImage==null)using(var stream=Open("TrayLeaf.png")) {
     var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;
     bitmap.DecodePixelWidth=256;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();brandImage=bitmap;
    }
    return brandImage;
   }
  }
  internal static System.Drawing.Icon CreateTrayIcon() {
   using(var stream=Open("TrayLeaf.ico"))
   using(var icon=new System.Drawing.Icon(stream,System.Windows.Forms.SystemInformation.SmallIconSize))
    return (System.Drawing.Icon)icon.Clone();
  }
 }
}
