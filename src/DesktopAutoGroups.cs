using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Threading;
namespace GlassBoard {
    public static class DesktopAutoRules {
        public const string DefaultFolderGroup="文件夹";
        public const string DefaultDocsGroup="文档";
        public static readonly string[] DocKinds={"PDF","Word","Excel","其他"};
        static readonly string[] wordExtensions={".doc",".docx",".docm",".dot",".dotx",".wps",".wpt",".rtf",".odt"};
        static readonly string[] excelExtensions={".xls",".xlsx",".xlsm",".xlsb",".xlt",".xltx",".et",".ett",".csv",".ods"};
        public static List<string> Roots(){
            var roots=new List<string>();
            try{string desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);if(!string.IsNullOrWhiteSpace(desktop))roots.Add(desktop);}catch(Exception ex){Store.Log(ex);}
            try{string common=Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);if(!string.IsNullOrWhiteSpace(common)&&!roots.Any(r=>string.Equals(r,common,StringComparison.OrdinalIgnoreCase)))roots.Add(common);}catch(Exception ex){Store.Log(ex);}
            return roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        public static string DocKind(string path){
            if(string.IsNullOrWhiteSpace(path))return null;
            try{
                if(Directory.Exists(path))return null;
                string ext=(Path.GetExtension(path)??"").ToLowerInvariant();
                if(ext.Length==0||!DocumentRules.Supported(path))return null;
                if(ext==".pdf")return "PDF";
                if(Array.IndexOf(wordExtensions,ext)>=0)return "Word";
                if(Array.IndexOf(excelExtensions,ext)>=0)return "Excel";
                return "其他";
            }catch{return null;}
        }
        public static bool IsVisibleItem(string path,bool folder){
            try{
                FileSystemInfo info;if(folder)info=new DirectoryInfo(path);else info=new FileInfo(path);
                if(!info.Exists)return false;
                if((info.Attributes&(FileAttributes.Hidden|FileAttributes.System|FileAttributes.ReparsePoint))!=0)return false;
                if(string.Equals(info.Name,"desktop.ini",StringComparison.OrdinalIgnoreCase))return false;
                if(!folder){
                    string ext=(Path.GetExtension(path)??"").ToLowerInvariant();
                    if(ext==".lnk"||ext==".url"||ext==".appref-ms")return false;
                    return DocKind(path)!=null;
                }
                return true;
            }catch{return false;}
        }
        public static List<KeyValuePair<string,bool>> Scan(){
            var found=new List<KeyValuePair<string,bool>>();
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(string root in Roots()){
                try{foreach(string path in Directory.GetDirectories(root)){if(!IsVisibleItem(path,true))continue;string key=EntryPathRules.Normalize(path);if(seen.Add(key))found.Add(new KeyValuePair<string,bool>(key,true));}}catch(Exception ex){Store.Log(ex);}
                try{foreach(string path in Directory.GetFiles(root)){if(!IsVisibleItem(path,false))continue;string key=EntryPathRules.Normalize(path);if(seen.Add(key))found.Add(new KeyValuePair<string,bool>(key,false));}}catch(Exception ex){Store.Log(ex);}
            }
            found.Sort((a,b)=>string.Compare(a.Key,b.Key,StringComparison.OrdinalIgnoreCase));
            return found;
        }
    }
    public sealed class DesktopAutoSync {
        readonly Controller owner;
        readonly List<FileSystemWatcher> watchers=new List<FileSystemWatcher>();
        DispatcherTimer debounce,periodic;
        bool started;
        public DesktopAutoSync(Controller controller){owner=controller;}
        public void Start(){
            if(started)return;started=true;
            if(!owner.Prefs.DesktopAutoSync)return;
            EnsureGroups();
            try{
                debounce=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(900)};
                debounce.Tick+=(s,e)=>{debounce.Stop();Sync("变化");};
                foreach(string root in DesktopAutoRules.Roots()){
                    try{
                        var watcher=new FileSystemWatcher(root);
                        watcher.IncludeSubdirectories=false;
                        watcher.NotifyFilter=NotifyFilters.FileName|NotifyFilters.DirectoryName;
                        watcher.Created+=OnChanged;watcher.Deleted+=OnChanged;watcher.Renamed+=OnRenamed;
                        watcher.Error+=(s,e)=>Queue();
                        watcher.EnableRaisingEvents=true;watchers.Add(watcher);
                    }catch(Exception ex){Store.Log(ex);}
                }
            }catch(Exception ex){Store.Log(ex);}
            try{
                periodic=new DispatcherTimer{Interval=TimeSpan.FromSeconds(30)};
                periodic.Tick+=(s,e)=>{Sync("定时");};periodic.Start();
            }catch(Exception ex){Store.Log(ex);}
            Sync("启动");
        }
        public void Stop(){
            started=false;
            foreach(var watcher in watchers){try{watcher.EnableRaisingEvents=false;watcher.Dispose();}catch{}}
            watchers.Clear();
            if(debounce!=null){debounce.Stop();debounce=null;}
            if(periodic!=null){periodic.Stop();periodic=null;}
        }
        void OnChanged(object sender,FileSystemEventArgs args){Queue();}
        void OnRenamed(object sender,RenamedEventArgs args){Queue();}
        void Queue(){
            if(!started||debounce==null)return;
            if(!owner.Board.Dispatcher.CheckAccess()){owner.Board.Dispatcher.BeginInvoke(new Action(RestartDebounce));return;}
            RestartDebounce();
        }
        void RestartDebounce(){if(!started||debounce==null)return;debounce.Stop();debounce.Start();}
        public int Sync(string reason){
            if(!owner.Prefs.DesktopAutoSync)return 0;
            if(owner.Board!=null&&owner.Board.ModalBusy)return 0;
            var prefs=owner.Prefs;
            int changed=0;
            if(EnsureGroups())changed++;
            var wanted=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(var item in DesktopAutoRules.Scan()){
                string path=EntryPathRules.Normalize(item.Key);
                if(item.Value){if(!prefs.DesktopFolders)continue;wanted[path]=prefs.DesktopFolderGroup;}
                else{if(!prefs.DesktopDocs)continue;wanted[path]=prefs.DesktopDocsGroup;}
            }
            int removed=prefs.Apps.RemoveAll(a=>a!=null&&a.DesktopAuto&&!wanted.ContainsKey(EntryPathRules.Normalize(a.Path)));
            int added=0;
            foreach(var pair in wanted){
                string path=pair.Key;
                if(prefs.Apps.Any(a=>string.Equals(EntryPathRules.Normalize(a.Path),path,StringComparison.OrdinalIgnoreCase)))continue;
                bool folder=Directory.Exists(path);
                var entry=new Entry{Name=EntryPathRules.Name(path),Path=path,IsFolder=folder,Group=pair.Value,DesktopAuto=true};
                if(RemovalRules.Blocked(prefs,entry))continue;
                prefs.Apps.Add(entry);added++;
            }
            if(added>0||removed>0)changed+=added+removed;
            if(added>0||removed>0||changed>0){
                owner.Save();
                if(owner.Board!=null){owner.Board.SyncGroups();if(owner.Board.HasView)owner.Board.Render();if(added>0||removed>0)owner.Board.SetStatus("桌面同步完成：新增 "+added+"，移除 "+removed);}
            }
            return added+removed;
        }
        bool EnsureGroups(){
            var prefs=owner.Prefs;bool changed=false;
            if(string.IsNullOrWhiteSpace(prefs.DesktopFolderGroup))prefs.DesktopFolderGroup=DesktopAutoRules.DefaultFolderGroup;
            if(string.IsNullOrWhiteSpace(prefs.DesktopDocsGroup))prefs.DesktopDocsGroup=DesktopAutoRules.DefaultDocsGroup;
            if(prefs.DesktopFolders&&prefs.Groups!=null&&!prefs.Groups.Any(g=>string.Equals((g??"").Trim(),prefs.DesktopFolderGroup,StringComparison.OrdinalIgnoreCase))){prefs.Groups.Insert(0,prefs.DesktopFolderGroup);changed=true;}
            if(prefs.DesktopDocs&&prefs.Groups!=null&&!prefs.Groups.Any(g=>string.Equals((g??"").Trim(),prefs.DesktopDocsGroup,StringComparison.OrdinalIgnoreCase))){prefs.Groups.Insert(0,prefs.DesktopDocsGroup);changed=true;}
            return changed;
        }
    }
}
