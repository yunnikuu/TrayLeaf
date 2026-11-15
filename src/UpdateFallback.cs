using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Net;using System.Text.RegularExpressions;using System.Threading;using System.Threading.Tasks;using System.Web.Script.Serialization;
namespace TrayLeaf {
 internal static partial class UpdateService {
  // Mirrors discover public releases and transfer payloads; only GitHub supplies trusted hashes.
  internal const string PublicMirror="https://gh-proxy.org/";
  internal const string ReleaseApi="https://api.github.com/repos/yunnikuu/TrayLeaf/releases/latest";
  internal static readonly string[] DownloadMirrors={PublicMirror,"https://ghfast.top/","https://v4.gh-proxy.org/"};
  internal static Task<string> FetchWithFallback(string official,string mirror,int timeout,int hedge,Func<string,int,CancellationToken,Task<string>> transport,Func<string,string> validate,CancellationToken token){return FetchFromSources(new[]{official,mirror},timeout,hedge,transport,validate,token);}
  internal static async Task<string> FetchFromSources(string[] urls,int timeout,int hedge,Func<string,int,CancellationToken,Task<string>> transport,Func<string,string> validate,CancellationToken token){
   token.ThrowIfCancellationRequested();using(var stop=CancellationTokenSource.CreateLinkedTokenSource(token)){
    stop.CancelAfter(timeout);var pending=new List<Task<string>>();Task<string> previous=null;
    foreach(string url in urls){var task=ReadSourceAfter(previous,url,timeout,hedge,transport,validate,stop.Token);pending.Add(task);previous=task;}
    var all=pending.ToArray();var errors=new List<string>();var limit=Task.Delay(timeout,stop.Token);
    try{while(pending.Count>0){var completed=await Task.WhenAny(pending.Cast<Task>().Concat(new[]{limit}));token.ThrowIfCancellationRequested();if(completed==limit)throw new IOException("连接超时，GitHub 与备用线路未在 10 秒内完成");var item=(Task<string>)completed;pending.Remove(item);try{return await item;}catch(OperationCanceledException){token.ThrowIfCancellationRequested();errors.Add("连接超时或取消");}catch(Exception ex){errors.Add(ex.Message);}}
     throw new IOException("GitHub 与备用线路均不可用："+String.Join("；",errors));
    }finally{stop.Cancel();foreach(var task in all)ObserveFailure(task);}
   }
  }
  static async Task<string> ReadSourceAfter(Task<string> previous,string url,int timeout,int hedge,Func<string,int,CancellationToken,Task<string>> transport,Func<string,string> validate,CancellationToken token){
   if(previous!=null){await Task.WhenAny(previous,Task.Delay(hedge,token));token.ThrowIfCancellationRequested();if(previous.Status==TaskStatus.RanToCompletion)return previous.Result;}
   token.ThrowIfCancellationRequested();return validate(await transport(url,timeout,token));
  }
  internal static string ValidateReleaseNotice(string text){var root=new JavaScriptSerializer{MaxJsonLength=4*1024*1024}.Deserialize<Dictionary<string,object>>(text);object value;if(root==null||!root.TryGetValue("html_url",out value)||!Convert.ToString(value).StartsWith(ProjectUrl+"/releases/tag/",StringComparison.OrdinalIgnoreCase))throw new IOException("更新信息不属于 TrayLeaf 项目");if((root.TryGetValue("draft",out value)&&Convert.ToBoolean(value))||(root.TryGetValue("prerelease",out value)&&Convert.ToBoolean(value)))throw new IOException("此版本不是正式发布");return ExtractTag(text);}
  internal static string ParseOfficialTag(string html){var match=Regex.Match(html,@"https://github\.com/yunnikuu/TrayLeaf/releases/expanded_assets/([^""?<>\s]+)");if(!match.Success)throw new IOException("GitHub 发布页未提供有效的正式版本");string tag=WebUtility.HtmlDecode(Uri.UnescapeDataString(match.Groups[1].Value));if(ParseVersion(tag)==null)throw new IOException("GitHub 发布版本号无效");return tag;}
  static string ValidateDiscovery(string text){return text.TrimStart().StartsWith("{",StringComparison.Ordinal)?ValidateReleaseNotice(text):ParseOfficialTag(text);}
  internal static Task<string> LatestProject(CancellationToken token){return FetchFromSources(new[]{ReleaseApi,ProjectUrl+"/releases/latest",PublicMirror+ReleaseApi,"https://v4.gh-proxy.org/"+ReleaseApi},10000,1500,GetText,ValidateDiscovery,token);}
  internal static Task<string> GetChangelog(string url,CancellationToken token){return FetchFromSources(new[]{url,PublicMirror+url,"https://ghfast.top/"+url,"https://v4.gh-proxy.org/"+url},10000,1500,GetText,text=>{if(String.IsNullOrWhiteSpace(text)||text.TrimStart().StartsWith("<",StringComparison.Ordinal)||!text.Contains("#"))throw new IOException("更新内容不是有效 Markdown");return text;},token);}
 }
}
