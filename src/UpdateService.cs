using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
namespace TrayLeaf {
 internal static class UpdatePolicy {
  internal static bool ShouldCheck(Database db,DateTime today){return db.Get("autoCheckUpdates","1")=="1"&&db.Get("lastAutoUpdateDate","")!=today.ToString("yyyy-MM-dd");}
  internal static void MarkAttempt(Database db,DateTime today){db.Set("lastAutoUpdateDate",today.ToString("yyyy-MM-dd"));}
 }
 internal static class UpdateService {
  internal const string ProjectUrl="https://github.com/yunnikuu/TrayLeaf";
  internal static Version ParseVersion(string value){Version v;return Version.TryParse((value??"").Trim().TrimStart('v','V'),out v)?new Version(v.Major,v.Minor,Math.Max(0,v.Build),Math.Max(0,v.Revision)):null;}
  internal static bool IsNewer(string tag,string current){var a=ParseVersion(tag);var b=ParseVersion(current);return a!=null&&b!=null&&a>b;}
  internal static string ExtractTag(string json){var m=Regex.Match(json??"","\"tag_name\"\\s*:\\s*\"([^\"\\\\]+)\"");if(!m.Success||ParseVersion(m.Groups[1].Value)==null)throw new IOException("发布信息中没有有效版本号");return m.Groups[1].Value;}
  internal static Task<string> Latest(){return Latest(new Uri("https://api.github.com/repos/yunnikuu/TrayLeaf/releases/latest"),10000,CancellationToken.None);}
  internal static Task<string> Latest(CancellationToken token){return Latest(new Uri("https://api.github.com/repos/yunnikuu/TrayLeaf/releases/latest"),10000,token);}
  internal static async Task<string> Latest(Uri uri,int timeout,CancellationToken token){
   token.ThrowIfCancellationRequested();ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var request=(HttpWebRequest)WebRequest.Create(uri);request.UserAgent="TrayLeaf/0.9.7";request.Accept="application/vnd.github+json";request.Timeout=timeout;request.ReadWriteTimeout=timeout;
   using(var delayCancellation=CancellationTokenSource.CreateLinkedTokenSource(token))using(token.Register(request.Abort)){
    var fetch=Fetch(request);var delay=Task.Delay(timeout,delayCancellation.Token);var winner=await Task.WhenAny(fetch,delay);
    if(winner!=fetch){request.Abort();ObserveFailure(fetch);token.ThrowIfCancellationRequested();throw new WebException("检查更新超时",WebExceptionStatus.Timeout);}
    delayCancellation.Cancel();try{return await fetch;}catch{token.ThrowIfCancellationRequested();throw;}
   }
  }
  static void ObserveFailure(Task task){task.ContinueWith(t=>{var observed=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);}
  static async Task<string> Fetch(HttpWebRequest request){using(var response=await request.GetResponseAsync())using(var reader=new StreamReader(response.GetResponseStream())){string json=await reader.ReadToEndAsync();return ExtractTag(json);}}
 }
}
