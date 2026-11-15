using System;using System.Collections;using System.Collections.Generic;using System.IO;using System.Linq;using System.Net;using System.Security.Cryptography;using System.Threading;using System.Threading.Tasks;using System.Web.Script.Serialization;
namespace TrayLeaf {
 internal sealed class ReleaseInfo {internal string Tag,AssetName,DownloadUrl,Digest;internal long Size;}
 internal static partial class UpdateService {
  internal static ReleaseInfo ParseRelease(string json){var root=new JavaScriptSerializer{MaxJsonLength=4*1024*1024}.Deserialize<Dictionary<string,object>>(json);object tag,assets;if(!root.TryGetValue("tag_name",out tag)||ParseVersion(Convert.ToString(tag))==null)throw new IOException("发布版本号无效");if(!root.TryGetValue("assets",out assets))throw new IOException("此发布没有 MSI 安装包");
   var list=(assets as IEnumerable);if(list==null)throw new IOException("发布资产格式无效");var candidates=new List<Dictionary<string,object>>();foreach(var item in list){var asset=item as Dictionary<string,object>;if(asset!=null&&asset.ContainsKey("name")&&Convert.ToString(asset["name"]).EndsWith(".msi",StringComparison.OrdinalIgnoreCase))candidates.Add(asset);}
   var selected=candidates.OrderByDescending(x=>Convert.ToString(x["name"]).IndexOf("x64",StringComparison.OrdinalIgnoreCase)>=0).FirstOrDefault();if(selected==null)throw new IOException("此发布未提供 MSI 安装包");string url=selected.ContainsKey("browser_download_url")?Convert.ToString(selected["browser_download_url"]):"";Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.Host!="github.com"||!uri.AbsolutePath.StartsWith("/yunnikuu/TrayLeaf/releases/download/",StringComparison.OrdinalIgnoreCase))throw new IOException("安装包下载地址不属于本项目的 GitHub Releases");
   string digest=selected.ContainsKey("digest")?Convert.ToString(selected["digest"]):"";if(digest.StartsWith("sha256:",StringComparison.OrdinalIgnoreCase))digest=digest.Substring(7);else digest="";if(!System.Text.RegularExpressions.Regex.IsMatch(digest,"^[a-fA-F0-9]{64}$"))digest="";
   long size;long.TryParse(selected.ContainsKey("size")?Convert.ToString(selected["size"]):"0",out size);return new ReleaseInfo{Tag=Convert.ToString(tag),AssetName=Convert.ToString(selected["name"]),DownloadUrl=url,Digest=digest,Size=size};
  }
  internal static async Task<string> GetText(string url,int timeout,CancellationToken token){ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var request=(HttpWebRequest)WebRequest.Create(url);request.UserAgent="TrayLeaf/"+AppIdentity.ReleaseVersion;request.Accept="application/vnd.github+json";request.Timeout=timeout;request.ReadWriteTimeout=timeout;request.CachePolicy=new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);request.Headers[HttpRequestHeader.CacheControl]="no-cache, no-store";using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){deadline.CancelAfter(timeout);using(deadline.Token.Register(request.Abort)){try{using(var response=await request.GetResponseAsync())using(var reader=new StreamReader(response.GetResponseStream())){var text=await reader.ReadToEndAsync();if(text.Length>4*1024*1024)throw new IOException("服务器响应过大");return text;}}catch(WebException ex){token.ThrowIfCancellationRequested();if(deadline.IsCancellationRequested)throw new IOException("连接超时，请稍后重试",ex);var r=ex.Response as HttpWebResponse;if(r!=null&&r.StatusCode==HttpStatusCode.Forbidden)throw new IOException("GitHub 请求次数已达上限或访问被拒绝，请稍后重试",ex);if(r!=null&&r.StatusCode==HttpStatusCode.NotFound)throw new IOException("尚无可用的正式发布或文件已删除",ex);throw;}}}}
  internal static Task<ReleaseInfo> GetRelease(CancellationToken token){return GetTrustedRelease(token,GetText);}
  internal static async Task<ReleaseInfo> GetTrustedRelease(CancellationToken token,Func<string,int,CancellationToken,Task<string>> transport){
   // TLS-validated GitHub endpoints only: mirrors must not supply their own trusted hashes.
   Exception firstError;
   try{string json=await transport("https://api.github.com/repos/yunnikuu/TrayLeaf/releases/latest",3000,token);ValidateReleaseNotice(json);var release=ParseRelease(json);if(String.IsNullOrEmpty(release.Digest))throw new IOException("官方 API 未提供 SHA-256");return release;}
   catch(OperationCanceledException){throw;}catch(Exception ex){firstError=ex;}
   token.ThrowIfCancellationRequested();try{
    string tag=ParseOfficialTag(await transport(ProjectUrl+"/releases/latest",3000,token));
    return ParseOfficialAssets(tag,await transport(ProjectUrl+"/releases/expanded_assets/"+Uri.EscapeDataString(tag),3000,token));
   }catch(OperationCanceledException){throw;}catch(Exception ex){throw new IOException("无法从 GitHub 官方获取可信安装包校验信息："+firstError.Message+"；"+ex.Message);}
  }

  internal static ReleaseInfo ParseOfficialAssets(string tag,string html){
   var list=new List<ReleaseInfo>();foreach(System.Text.RegularExpressions.Match item in System.Text.RegularExpressions.Regex.Matches(html,@"<li\b[^>]*>(.*?)</li>",System.Text.RegularExpressions.RegexOptions.Singleline|System.Text.RegularExpressions.RegexOptions.IgnoreCase)){
    var link=System.Text.RegularExpressions.Regex.Match(item.Value,@"href=""(/yunnikuu/TrayLeaf/releases/download/[^""<>]+\.msi)""",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    var digest=System.Text.RegularExpressions.Regex.Match(item.Value,@"sha256:([a-fA-F0-9]{64})\b");if(!link.Success||!digest.Success)continue;
    string url="https://github.com"+WebUtility.HtmlDecode(link.Groups[1].Value);var uri=new Uri(url);string expected="/yunnikuu/TrayLeaf/releases/download/"+Uri.EscapeDataString(tag)+"/";if(!uri.AbsolutePath.StartsWith(expected,StringComparison.OrdinalIgnoreCase))continue;
    list.Add(new ReleaseInfo{Tag=tag,AssetName=Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath)),DownloadUrl=url,Digest=digest.Groups[1].Value,Size=0});
   }
   var selected=list.OrderByDescending(x=>x.AssetName.IndexOf("x64",StringComparison.OrdinalIgnoreCase)>=0).FirstOrDefault();if(selected==null)throw new IOException("GitHub 官方发布页没有带 SHA-256 的 MSI 安装包");return selected;
  }
  internal static bool VerifyDigest(string file,string expected){if(String.IsNullOrEmpty(expected)||!System.Text.RegularExpressions.Regex.IsMatch(expected,"^[a-fA-F0-9]{64}$"))return false;using(var sha=SHA256.Create())using(var input=File.OpenRead(file)){return String.Equals(BitConverter.ToString(sha.ComputeHash(input)).Replace("-",""),expected,StringComparison.OrdinalIgnoreCase);}}
  internal static Task<string> Download(ReleaseInfo release,string directory,Action<int> progress,CancellationToken token){return DownloadFromSources(release,directory,progress,token,TransferFile,20000);}
  static async Task TransferFile(string url,string path,Action<int> progress,CancellationToken token){
   using(var client=new WebClient()){client.Headers[HttpRequestHeader.UserAgent]="TrayLeaf/"+AppIdentity.ReleaseVersion;client.DownloadProgressChanged+=delegate(object s,DownloadProgressChangedEventArgs e){if(progress!=null)progress(e.ProgressPercentage);};using(token.Register(client.CancelAsync))await client.DownloadFileTaskAsync(new Uri(url),path);}
  }
  internal static async Task<string> DownloadFromSources(ReleaseInfo release,string directory,Action<int> progress,CancellationToken token,Func<string,string,Action<int>,CancellationToken,Task> transfer,int sourceTimeout){
   token.ThrowIfCancellationRequested();if(String.IsNullOrEmpty(release.Digest))throw new IOException("GitHub 未提供此安装包的 SHA-256 摘要，无法验证，已取消自动安装；请联系发布者重新上传安装包");
   Directory.CreateDirectory(directory);string file=Path.Combine(directory,"TrayLeaf-"+Guid.NewGuid().ToString("N")+".msi"),partial=file+".part";var errors=new List<string>();
   var sources=new List<string>{release.DownloadUrl};if(release.DownloadUrl.StartsWith(ProjectUrl+"/releases/download/",StringComparison.OrdinalIgnoreCase))sources.AddRange(DownloadMirrors.Select(prefix=>prefix+release.DownloadUrl));
   try{foreach(string source in sources){token.ThrowIfCancellationRequested();try{
     if(progress!=null)progress(0);using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){
      // Reset the inactivity timeout on progress; retain a ten-minute overall limit.
      deadline.CancelAfter(sourceTimeout);using(var total=new CancellationTokenSource(TimeSpan.FromMinutes(10)))using(total.Token.Register(deadline.Cancel)){
       await transfer(source,partial,percent=>{if(!deadline.IsCancellationRequested)deadline.CancelAfter(sourceTimeout);if(progress!=null)progress(percent);},deadline.Token);
      }
     }
     token.ThrowIfCancellationRequested();if(release.Size>0&&new FileInfo(partial).Length!=release.Size)throw new IOException("安装包长度与发布信息不一致");if(!await Task.Run(()=>VerifyDigest(partial,release.Digest)))throw new IOException("SHA-256 校验失败，已阻止安装");File.Move(partial,file);return file;
    }catch(Exception ex){token.ThrowIfCancellationRequested();errors.Add(new Uri(source).Host+"："+ex.Message);if(File.Exists(partial))File.Delete(partial);}}
    throw new IOException("所有安装包下载线路均失败："+String.Join("；",errors));
   }finally{if(File.Exists(partial))File.Delete(partial);}
  }
 }
}
