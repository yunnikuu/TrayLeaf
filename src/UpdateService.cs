<<<<<<< HEAD
using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
namespace TrayLeaf {
 internal static class UpdateService {
 internal const string ProjectUrl="https://github.com/yunnikuu/TrayLeaf";
 internal static Version ParseVersion(string value){Version v;return Version.TryParse((value??"").Trim().TrimStart('v','V'),out v)?new Version(v.Major,v.Minor,Math.Max(0,v.Build),Math.Max(0,v.Revision)):null;}
 internal static bool IsNewer(string tag,string current){var a=ParseVersion(tag);var b=ParseVersion(current);return a!=null&&b!=null&&a>b;}
 internal static string ExtractTag(string json){var m=Regex.Match(json??"","\"tag_name\"\\s*:\\s*\"([^\"\\\\]+)\"");if(!m.Success||ParseVersion(m.Groups[1].Value)==null)throw new IOException("发布信息中没有有效版本号");return m.Groups[1].Value;}
 internal static async Task<string> Latest(){ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var request=(HttpWebRequest)WebRequest.Create("https://api.github.com/repos/yunnikuu/TrayLeaf/releases/latest");request.UserAgent="TrayLeaf/0.8.8";request.Accept="application/vnd.github+json";request.Timeout=12000;request.ReadWriteTimeout=12000;var responseTask=request.GetResponseAsync();if(await Task.WhenAny(responseTask,Task.Delay(12000))!=responseTask){request.Abort();throw new WebException("检查更新超时",WebExceptionStatus.Timeout);}using(var response=await responseTask)using(var reader=new StreamReader(response.GetResponseStream()))return ExtractTag(await reader.ReadToEndAsync());}
 }
}
=======
using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
namespace TrayLeaf {
 internal static class UpdateService {
 internal const string ProjectUrl="https://github.com/yunnikuu/TrayLeaf";
 internal static Version ParseVersion(string value){Version v;return Version.TryParse((value??"").Trim().TrimStart('v','V'),out v)?new Version(v.Major,v.Minor,Math.Max(0,v.Build),Math.Max(0,v.Revision)):null;}
 internal static bool IsNewer(string tag,string current){var a=ParseVersion(tag);var b=ParseVersion(current);return a!=null&&b!=null&&a>b;}
 internal static string ExtractTag(string json){var m=Regex.Match(json??"","\"tag_name\"\\s*:\\s*\"([^\"\\\\]+)\"");if(!m.Success||ParseVersion(m.Groups[1].Value)==null)throw new IOException("发布信息中没有有效版本号");return m.Groups[1].Value;}
 internal static async Task<string> Latest(){ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var request=(HttpWebRequest)WebRequest.Create("https://api.github.com/repos/yunnikuu/TrayLeaf/releases/latest");request.UserAgent="TrayLeaf/0.8.5";request.Accept="application/vnd.github+json";request.Timeout=12000;request.ReadWriteTimeout=12000;var responseTask=request.GetResponseAsync();if(await Task.WhenAny(responseTask,Task.Delay(12000))!=responseTask){request.Abort();throw new WebException("检查更新超时",WebExceptionStatus.Timeout);}using(var response=await responseTask)using(var reader=new StreamReader(response.GetResponseStream()))return ExtractTag(await reader.ReadToEndAsync());}
 }
}
>>>>>>> origin/main
