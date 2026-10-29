using System;using System.IO;using System.Linq;using System.Collections.Generic;
namespace GlassBoard {
public static class LaunchInfoService {
        public static Entry Read(string path){var entry=new Entry{Name=EntryPathRules.Name(path),Path=EntryPathRules.Normalize(path),IsFolder=Directory.Exists(path)};Enrich(entry);return entry;}
        public static void Enrich(Entry entry){string target=entry.Path??"",arguments="";string ext=System.IO.Path.GetExtension(target).ToLowerInvariant();
            if(Directory.Exists(target)){entry.IsFolder=true;entry.LaunchTarget=target;entry.LaunchArguments="";return;}
            if(ext==".lnk"){object shell=null,link=null;try{var type=Type.GetTypeFromProgID("WScript.Shell");shell=Activator.CreateInstance(type);link=type.InvokeMember("CreateShortcut",System.Reflection.BindingFlags.InvokeMethod,null,shell,new object[]{target});target=(string)link.GetType().InvokeMember("TargetPath",System.Reflection.BindingFlags.GetProperty,null,link,null);arguments=(string)link.GetType().InvokeMember("Arguments",System.Reflection.BindingFlags.GetProperty,null,link,null);}catch{}finally{if(link!=null&&System.Runtime.InteropServices.Marshal.IsComObject(link))System.Runtime.InteropServices.Marshal.ReleaseComObject(link);if(shell!=null&&System.Runtime.InteropServices.Marshal.IsComObject(shell))System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);}}
            else if(ext==".url")target=StartupEntryRules.ReadUrl(target);
            // Electron shortcuts can intentionally start a main app through Update.exe.
            if(StartupEntryRules.Auxiliary(target)&&arguments.IndexOf("--processStart",StringComparison.OrdinalIgnoreCase)>=0){var match=System.Text.RegularExpressions.Regex.Match(arguments,@"--processStart\s+(?:""([^""]+)""|([^\s]+))",System.Text.RegularExpressions.RegexOptions.IgnoreCase);if(match.Success){string main=match.Groups[1].Success?match.Groups[1].Value:match.Groups[2].Value;if(!StartupEntryRules.Auxiliary(main))target=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(target)??"",main);}}
            entry.LaunchTarget=target;entry.LaunchArguments=arguments;entry.IsFolder=entry.IsFolder||Directory.Exists(target);
            try{if(System.IO.Path.GetExtension(target??"").Equals(".exe",StringComparison.OrdinalIgnoreCase)&&File.Exists(target)){var info=System.Diagnostics.FileVersionInfo.GetVersionInfo(target);entry.ProductInfo=(info.ProductName??"")+" "+(info.FileDescription??"");}}catch{}
        }
        public static bool Primary(Entry entry){if(entry.IsFolder||!StartupEntryRules.Candidate(entry.Path)||StartupEntryRules.Auxiliary(entry.LaunchTarget))return false;string target=entry.LaunchTarget;if(string.IsNullOrWhiteSpace(target))return true;if(Directory.Exists(target))return false;string ext=System.IO.Path.GetExtension(target).ToLowerInvariant();return ext.Length==0||StartupEntryRules.GameUri(target)||EntryRules.Importable(target);}
    }
class BoardStub {public bool IsVisible=true;public string CurrentGroup="游戏";}
class AddController {public Preferences Prefs=new Preferences();public BoardStub Board=new BoardStub();public int Refreshes;readonly BoardEditHistory history=new BoardEditHistory();public BoardEditState BeginBoardEdit(){return BoardEditState.Capture(Prefs);}public void EndBoardEdit(BoardEditState before,string label){history.Remember(before,Prefs,label);}public void Refresh(){Refreshes++;}
        public void Add(IEnumerable<string> paths,bool manual=true,string targetGroup=null) {
            if(manual&&targetGroup==null&&Board.IsVisible)targetGroup=Board.CurrentGroup;
            var before=BeginBoardEdit();int added=0;
            foreach(string rawPath in paths) {
                string path=EntryPathRules.Normalize(rawPath);
                if(!File.Exists(path) && !Directory.Exists(path)) continue;
                if(!LayoutRules.AllowImport(path,Prefs.Removed,manual))continue;
                var existing=Prefs.Apps.FirstOrDefault(a=>string.Equals(EntryPathRules.Normalize(a.Path),path,StringComparison.OrdinalIgnoreCase));if(existing!=null){if(manual&&targetGroup!=null&&targetGroup!="全部")added+=FeatureRules.AddShared(Prefs,new[]{existing.Path},targetGroup);continue;}
                if(manual)Prefs.Removed.RemoveAll(p=>string.Equals(EntryPathRules.Normalize(p),path,StringComparison.OrdinalIgnoreCase));
                var entry=new Entry {Name=EntryPathRules.Name(path),Path=path,IsFolder=Directory.Exists(path)};LaunchInfoService.Enrich(entry);if(!manual&&!LaunchInfoService.Primary(entry))continue;entry.Group=FeatureRules.ImportGroup(Prefs,entry,manual?targetGroup:null);Prefs.Apps.Add(entry); added++;
            }
            if(added>0){EndBoardEdit(before,"添加入口");Refresh();}
        }
}
class AddRegression {static void Check(bool value,string label){if(!value)throw new Exception(label);}static int Main(){string folder=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());Directory.CreateDirectory(folder);try{var c=new AddController();c.Prefs.Groups.Add("游戏");c.Prefs.AutoGrouping=true;string one=Path.Combine(folder,"winword.exe"),two=Path.Combine(folder,"QQ.exe");File.WriteAllText(one,"");File.WriteAllText(two,"");c.Add(new[]{one});Check(c.Prefs.Apps.Single().Group=="游戏"&&c.Refreshes==1,"actual current-group import");c.Add(new[]{two},true,"未分组");Check(FeatureRules.GroupName(c.Prefs.Apps.Last())=="未分组","explicit drag-target import");c.Add(new[]{one});Check(c.Prefs.Apps.Count==2,"duplicate import unchanged");c.Prefs.Groups.Add("办公");c.Board.CurrentGroup="办公";c.Add(new[]{one});Check(c.Prefs.Apps.Count==2&&FeatureRules.InGroup(c.Prefs.Apps[0],"办公")&&FeatureRules.InGroup(c.Prefs.Apps[0],"游戏"),"reimport existing path adds membership without duplicating entry");c.Board.CurrentGroup="游戏";c.Prefs.Removed.Add(one);c.Prefs.Apps.RemoveAt(0);c.Add(new[]{one});Check(!c.Prefs.Removed.Contains(one)&&c.Prefs.Apps.Last().Group=="游戏","manual re-add removed entry into current group");
string library=Path.Combine(folder,"森霖素材.v2"),nested=Path.Combine(library,"子目录");Directory.CreateDirectory(nested);File.WriteAllText(Path.Combine(library,"inside.exe"),"");
int previousCount=c.Prefs.Apps.Count;c.Add(new[]{library});var addedFolder=c.Prefs.Apps.Last();
Check(c.Prefs.Apps.Count==previousCount+1&&addedFolder.IsFolder&&addedFolder.Name=="森霖素材.v2"&&addedFolder.Group=="游戏","folder itself is added with full dotted Chinese name and chosen group; contents not scanned");
c.Add(new[]{library+Path.DirectorySeparatorChar});Check(c.Prefs.Apps.Count==previousCount+1,"folder trailing separator deduplicates");
c.Board.CurrentGroup="办公";c.Add(new[]{library});Check(FeatureRules.InGroup(addedFolder,"游戏")&&FeatureRules.InGroup(addedFolder,"办公"),"existing folder shared across groups without duplicate");
Check(AutoGroupRules.Guess(new Entry{Name="Steam",Path=library,IsFolder=true})==null&&!LaunchInfoService.Primary(addedFolder),"folder excluded from software classification and automatic launch-only imports");
Check(EntryRules.Candidates(new[]{library,library+Path.DirectorySeparatorChar},new Entry[0]).Count==1&&EntryRules.Candidates(new[]{library+Path.DirectorySeparatorChar},new[]{addedFolder}).Count==0,"batch folder candidates normalize and deduplicate");
Check(EntryPathRules.Name(Path.GetPathRoot(library)).Length>0,"root folder has a label");
var migrated=EntryRules.NormalizeEntries(new[]{new Entry{Name="自定义文件夹名",Path=library+Path.DirectorySeparatorChar},new Entry{Name="other",Path=library}});Check(migrated.Count==1&&migrated[0].IsFolder&&migrated[0].Name=="自定义文件夹名","legacy folders deduplicate, acquire folder kind and retain user labels");
var history=new BoardEditHistory();var before=c.BeginBoardEdit();c.Prefs.Apps.Remove(addedFolder);history.Remember(before,c.Prefs,"移除文件夹");history.Undo(c.Prefs);Check(c.Prefs.Apps.Any(a=>a.Path==library&&a.IsFolder),"undo restores folder kind");
using(var stream=new MemoryStream()){new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(Entry)).WriteObject(stream,addedFolder);stream.Position=0;var saved=(Entry)new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(Entry)).ReadObject(stream);Check(saved.IsFolder&&saved.Name==addedFolder.Name,"folder kind survives JSON persistence");}
}finally{Directory.Delete(folder,true);}Console.WriteLine("PASS: production Controller.Add folder/batch normalization, shared groups, JSON/undo folder kind and launch-only filtering");return 0;}}
}
