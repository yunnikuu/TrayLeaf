using System;using System.IO;using System.Linq;using System.Collections.Generic;using TrayLeaf;
class Version100Tests {
 static int passed,failed;static void Check(bool ok,string name){Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(ok)passed++;else failed++;}
 static string bytesPath(string directory){string file=Path.Combine(directory,"linked-document.txt");File.WriteAllText(file,"inert test fixture");return file;}
 [STAThread]static int Main(string[] a){try{using(var db=new Database(Path.Combine(a[0],"test100-"+Guid.NewGuid()+".db"))){
 var source=new AppRow{Name="Source",Path=@"C:\apps\source.exe",Args="--original",Selected=true};
 var target=new AppRow{Name="Target",Path=@"C:\apps\target.exe",Args="--linked",Admin=true,Selected=true};
 db.SetAlias(source.Path,"My application");db.ApplyName(source);Check(source.Name=="My application","alias-applied");Check(db.Aliases().Count==1,"alias-manager");db.SetAlias(source.Path,"");db.ApplyName(source);Check(source.Name=="Source","alias-restored");
 Check(!db.LinkedProfilesEnabled,"linked-profiles-default-off");db.SaveAssociation(source,target,true);Check(db.LinkedProfilesEnabled,"active-rule-enables-linked-profiles");var mapped=db.RestoreTarget(source);Check(mapped.Path==target.Path&&mapped.Args=="--linked"&&mapped.Admin,"association-target-metadata");
 var id=db.SaveProfile("close","Game",new[]{source},0);long restore=db.LinkedRestoreProfile(id);Check(restore>0&&db.LoadSet(restore).Single().Path==target.Path,"profile-auto-created-and-mapped");
 db.SetAssociationEnabled(source.Path,false);Check(db.RestoreTarget(source).Path==source.Path,"disabled-rule-identity");Check(db.LoadSet(restore).Single().Path==source.Path,"profile-remapped-hot");
 db.SetAssociationEnabled(source.Path,true);var batch=db.Batch(new List<AppRow>{source});var item=db.Query("SELECT id FROM items WHERE batch=?",batch.ToString()).Single()[0];source.ItemId=long.Parse(item);source.ItemStatus="closed";db.MapClosedForRestore(new[]{source});Check(db.Query("SELECT path,args,admin FROM items WHERE id=?",item).Single().SequenceEqual(new[]{target.Path,target.Args,"1"}),"closed-record-mapped");
 Check(db.LoadSet(db.ListSets("history_close").First().Id).Single().Path==source.Path,"close-history-retains-source");
 var manual=db.SaveProfile("restore","Manual",new[]{new AppRow{Name="Manual",Path=@"C:\Apps\manual.exe",Args="--manual",Selected=true}},0);db.LinkProfiles(id,manual);db.SetAssociationEnabled(source.Path,false);db.SetAssociationEnabled(source.Path,true);Check(db.LoadSet(manual).Single().Args=="--manual","manual-profile-mapping-preserves-user-content");db.LinkProfiles(id,restore);
 db.RememberOriginalName(source);db.SetAlias(source.Path,"Renamed again");var reopened=new AppRow{Name="Renamed again",Path=source.Path};db.ApplyName(reopened);db.SetAlias(source.Path,"");db.ApplyName(reopened);Check(reopened.Name=="Source","alias-original-survives-record-reload");
 db.DeleteProfile("restore",restore);Check(db.LinkedRestoreProfile(id)==0,"deleted-link-cleared");
 Check(LaunchMonitor.CanObserve(@"C:\Apps\target.exe")&&!LaunchMonitor.CanObserve(@"C:\Apps\target.lnk")&&!LaunchMonitor.CanObserve(@"C:\Apps\file.txt"),"shell-associations-not-misreported-as-process-failure");
 Check(OperationService.StateText("dispatched")=="成功发送请求","shell-dispatch-readable-status");
 var shellRow=new AppRow{Name="Shell document",Path=bytesPath(a[0]),Selected=true};var shellOps=new OperationService(db);shellOps.PrepareLaunch(new System.Collections.Generic.List<AppRow>{shellRow});shellOps.State(shellRow,"launched","");int observations=0;new LaunchMonitor(()=>{observations++;return new HashSet<string>();},ms=>System.Threading.Tasks.Task.FromResult(0)).VerifyAsync(new[]{shellRow},shellOps).GetAwaiter().GetResult();Check(shellRow.ItemStatus=="dispatched"&&observations==0,"non-executable-dispatch-skips-invalid-process-probes");
 Check(UpdateService.IsNewer("v1.0.1","1.0.0"),"version-comparison");
 var release=UpdateService.ParseRelease("{\"tag_name\":\"v1.0.1\",\"assets\":[{\"name\":\"TrayLeaf-v1.0.1-Setup.msi\",\"browser_download_url\":\"https://github.com/yunnikuu/TrayLeaf/releases/download/v1.0.1/TrayLeaf.msi\",\"digest\":\"sha256:"+new string('a',64)+"\"}]}");Check(release.Digest==new string('a',64)&&release.AssetName.EndsWith(".msi"),"release-digest-parsed");
 var bytes=Path.Combine(a[0],"fixture.bin");File.WriteAllText(bytes,"fixture");Check(!UpdateService.VerifyDigest(bytes,new string('0',64)),"hash-mismatch-blocked");bool rejected=false;try{UpdateService.ParseRelease("{\"tag_name\":\"v2\",\"assets\":[]}");}catch{rejected=true;}Check(rejected,"missing-msi-rejected");
 } }catch(Exception ex){Console.WriteLine(ex);failed++;}Console.WriteLine("RESULT passed="+passed+" failed="+failed);return failed==0?0:1;}
}

