using System;using System.IO;using System.Text;using System.Linq;using System.Runtime.Serialization.Json;using System.Windows.Threading;using System.Threading.Tasks;using Microsoft.Win32;
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
        public static bool Primary(Entry entry){if(entry.IsFolder||!StartupEntryRules.Candidate(entry.Path)||StartupEntryRules.Auxiliary(entry.LaunchTarget))return false;string target=entry.LaunchTarget;if(string.IsNullOrWhiteSpace(target))return true;if(Directory.Exists(target))return false;string ext=System.IO.Path.GetExtension(target).ToLowerInvariant();return ext.Length==0||StartupEntryRules.GameUri(target)||(EntryRules.Importable(target)&&!DocumentRules.Supported(target));}
    }
    public sealed class NotesRepository:IDisposable {
        readonly string path;readonly DispatcherTimer timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(650)};bool dirty;
        public NoteBook Book;public string Status="已保存";
        public NotesRepository(){path=System.IO.Path.Combine(Store.Folder,"notes.json");Book=Load();Book.Normalize();timer.Tick+=(s,e)=>{timer.Stop();Flush();};}
        NoteBook Load(){return NoteFiles.Read(path,Store.Log);}
        public void Changed(){dirty=true;Book.Selected.Updated=DateTime.UtcNow.Ticks;Status="正在保存…";timer.Stop();timer.Start();}
        public bool Flush(){timer.Stop();if(!dirty&&File.Exists(path))return true;try{NoteFiles.Write(path,Book);dirty=false;Status="已保存 · "+DateTime.Now.ToString("HH:mm");return true;}catch(Exception ex){Store.Log(ex);Status="保存失败，请先导出备忘录";dirty=true;return false;}}
        public void Add(){Flush();var item=new NoteItem();Book.Items.Add(item);Book.SelectedId=item.Id;Changed();Flush();}
        public void Remove(){Book.Items.Remove(Book.Selected);Book.Normalize();Changed();Flush();}
        public void Select(string id){Flush();if(Book.Items.Any(n=>n.Id==id)){Book.SelectedId=id;Changed();Flush();}}
        public void Export(string destination){var note=Book.Selected;File.WriteAllText(destination,note.Title+Environment.NewLine+Environment.NewLine+note.Body,new UTF8Encoding(true));}
        public void TogglePin(){var note=Book.Selected;if(note==null)return;note.Pinned=!note.Pinned;Book.Normalize();Changed();Flush();}
        public void ExportMarkdown(string destination){var note=Book.Selected;if(note==null)return;var text=new System.Text.StringBuilder();text.AppendLine("# "+(string.IsNullOrWhiteSpace(note.Title)?"未命名备忘录":note.Title));text.AppendLine();if(!string.IsNullOrWhiteSpace(note.Body))text.AppendLine(note.Body);File.WriteAllText(destination,text.ToString(),new UTF8Encoding(true));}        public void Reload(){timer.Stop();dirty=false;Book=Load();Book.Normalize();Status="已恢复";}
        public void Dispose(){timer.Stop();Flush();}
    }
    public sealed class WeatherEngine {
        readonly Controller C;bool busy,closed;int serial,failures;string city="";bool direct;DateTime next=DateTime.MinValue;
        public string LastDiagnostic="尚未请求天气。";
        public WeatherSnapshot Snapshot;public string Message="设置城市后获取天气";public event Action Changed;
        public WeatherEngine(Controller c){C=c;}
        public void Update(bool force=false){if(closed||!C.Prefs.Widgets.Weather)return;string current=C.Prefs.Widgets.City;if(current!=city||direct!=C.Prefs.Widgets.WeatherDirectConnection){city=current;direct=C.Prefs.Widgets.WeatherDirectConnection;Snapshot=null;failures=0;next=DateTime.MinValue;serial++;}if(!busy&&(force||DateTime.UtcNow>=next))Fetch();}
        async void Fetch(){if(busy||closed)return;if(string.IsNullOrWhiteSpace(city)){Message="在组件设置中填写天气城市";next=DateTime.UtcNow.AddMinutes(30);Notify();return;}int version=serial;string requested=city;bool useDirect=direct;busy=true;Message="正在查询天气…";Notify();try{var result=await Task.Run(()=>WeatherClient.Fetch(requested,useDirect));if(closed||version!=serial||requested!=city)return;Snapshot=result;failures=0;LastDiagnostic="天气请求成功："+result.Provider+"；"+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+"；连接方式："+(useDirect?"直接连接":"系统代理");Message=result.Provider+" · 更新于 "+DateTime.Now.ToString("HH:mm");}catch(Exception ex){if(!closed&&version==serial){failures++;LastDiagnostic="连接方式："+(useDirect?"直接连接":"系统代理")+"\n"+ex.ToString();Message=(Snapshot==null?"":"显示上次数据 · ")+ex.Message+" · 稍后自动重试，也可点击刷新";Store.Log(ex);}}finally{busy=false;next=DateTime.UtcNow.AddSeconds(failures==0?1800:Math.Min(300,30*Math.Pow(2,Math.Min(4,failures-1))));if(!closed){Notify();if(requested!=city)Fetch();}}}
        void Notify(){if(!closed&&Changed!=null)Changed();}
        public void Stop(){closed=true;serial++;Changed=null;}
    }
}
