using System;using System.Collections.Generic;using System.Linq;using System.Runtime.Serialization;
namespace GlassBoard {
    public static class BoardIconRules {
        public static int Columns(double width,double icon,double gap){return Math.Max(1,Math.Min(30,(int)(Math.Max(1,width)/(Math.Max(70,icon+18)+gap*2))));}
        public static int Rows(double height,double icon,double gap){return Math.Max(1,Math.Min(20,(int)(Math.Max(1,height)/(icon+40+gap*2))));}
    }

    [DataContract] public class Workspace {
        [DataMember] public string Name{get;set;}
        [DataMember] public List<string> Paths=new List<string>();
        public void Normalize(){Name=(Name??"").Trim();Paths=(Paths??new List<string>()).Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();}
    }
    [DataContract] public class PlacementOptions {
        [DataMember] public double? DockCenter,DockLeft,DockTop,PanelLeft,PanelTop,PanelWidth,PanelHeight,WidgetLeft,WidgetTop;
        [DataMember] public bool DockLocked=true,PanelLocked=false;
        [OnDeserializing] void Defaults(StreamingContext context){DockLocked=true;PanelLocked=false;}
        public PlacementOptions Copy(){return (PlacementOptions)MemberwiseClone();}
        public void Normalize(){DockCenter=Valid(DockCenter);DockLeft=Valid(DockLeft);DockTop=Valid(DockTop);PanelLeft=Valid(PanelLeft);PanelTop=Valid(PanelTop);WidgetLeft=Valid(WidgetLeft);WidgetTop=Valid(WidgetTop);PanelWidth=Valid(PanelWidth);PanelHeight=Valid(PanelHeight);if(PanelWidth<320)PanelWidth=null;if(PanelHeight<320)PanelHeight=null;}
        static double? Valid(double? x){return x.HasValue&&(double.IsNaN(x.Value)||double.IsInfinity(x.Value))?null:x;}
    }
    [DataContract] public class ClockAlarm {
        [DataMember] public string Id=Guid.NewGuid().ToString("N"),Label="闹钟";
        [DataMember] public int Hour,Minute;
        [DataMember] public bool Enabled=true,Daily;
        [DataMember] public DateTime DueLocal;
        public ClockAlarm Copy(){return (ClockAlarm)MemberwiseClone();}
        public void Normalize(){if(string.IsNullOrEmpty(Id))Id=Guid.NewGuid().ToString("N");Label=string.IsNullOrWhiteSpace(Label)?"闹钟":Label.Trim();if(Label.Length>80)Label=Label.Substring(0,80);Hour=Math.Max(0,Math.Min(23,Hour));Minute=Math.Max(0,Math.Min(59,Minute));if(DueLocal.Year<2000)DueLocal=ClockRules.Next(Hour,Minute,DateTime.Now);}
    }
    public static class ClockRules {
        public static DateTime Next(int hour,int minute,DateTime now){var due=now.Date.AddHours(hour).AddMinutes(minute);return due<=now?due.AddDays(1):due;}
        public static double Remaining(WidgetOptions o,DateTime utc){return o.TimerDueUtc.HasValue?Math.Max(0,(o.TimerDueUtc.Value-utc).TotalSeconds):Math.Max(0,o.TimerPausedSeconds);}
        public static void Start(WidgetOptions o,double seconds,DateTime utc){o.TimerPausedSeconds=0;o.TimerDueUtc=utc.AddSeconds(LayoutRules.Clamp(seconds,1,86400,300));}
        public static void Pause(WidgetOptions o,DateTime utc){o.TimerPausedSeconds=Remaining(o,utc);o.TimerDueUtc=null;}
        public static void Resume(WidgetOptions o,DateTime utc){if(o.TimerPausedSeconds>0)Start(o,o.TimerPausedSeconds,utc);}
        public static void Cancel(WidgetOptions o){o.TimerPausedSeconds=0;o.TimerDueUtc=null;}
        public static List<string> Check(WidgetOptions o,DateTime local,DateTime utc){var messages=new List<string>();foreach(var a in o.Alarms){if(!a.Enabled||a.DueLocal>local)continue;messages.Add(a.Label);if(a.Daily)a.DueLocal=Next(a.Hour,a.Minute,local);else a.Enabled=false;}if(o.TimerDueUtc.HasValue&&o.TimerDueUtc.Value<=utc){messages.Add("倒计时结束");Cancel(o);}return messages;}
        public static string Display(WidgetOptions o,DateTime utc){double seconds=Remaining(o,utc);if(seconds>0)return (o.TimerDueUtc.HasValue?"倒计时 ":"已暂停 ")+((int)(Math.Ceiling(seconds)/3600)).ToString("00")+":"+TimeSpan.FromSeconds(Math.Ceiling(seconds)).ToString(@"mm\:ss");var alarm=o.Alarms.Where(a=>a.Enabled).OrderBy(a=>a.DueLocal).FirstOrDefault();return alarm==null?"点击 ••• 设置闹钟和倒计时":"闹钟 "+alarm.DueLocal.ToString("M/d HH:mm")+" · "+alarm.Label;}
    }
    [DataContract] public partial class WidgetOptions {
        [DataMember] public bool Clock=false,Weather=false,Music=false,Notes=false;
        [DataMember] public int ClockPanel=0,WeatherPanel=0,MusicPanel=0,NotesPanel=0;
        [DataMember] public bool CompactMusic=false,Topmost=false,Locked=false;
        [DataMember] public string MusicMode="system";
        [DataMember] public List<string> Playlist=new List<string>();
        [DataMember] public int TrackIndex=0,RepeatMode=0;
        [DataMember] public bool Shuffle=false;
        [DataMember] public bool NativeMediaCompatibility=false;
        [DataMember] public bool AutoNativeMediaCompatibility=true;
        [DataMember] public List<WidgetPanelPosition> Panels=new List<WidgetPanelPosition>();
        [DataMember] public List<ClockAlarm> Alarms=new List<ClockAlarm>();
        [DataMember] public DateTime? TimerDueUtc;
        [DataMember] public double TimerPausedSeconds;
        [DataMember] public string City="";
        [DataMember] public bool WeatherDirectConnection=false;
        [DataMember] public double Volume=.6,OpacityPercent=72;
        [DataMember] public bool HideControls=false;
        [DataMember] public MaterialOptions Material=new MaterialOptions();
        [OnDeserializing] void Defaults(StreamingContext context){LyricTranslation=true;AutoNativeMediaCompatibility=true;City="";Volume=.6;OpacityPercent=72;MusicMode="system";Playlist=new List<string>();Panels=new List<WidgetPanelPosition>();}
        public void Normalize(){if(LyricLeft.HasValue&&(double.IsNaN(LyricLeft.Value)||double.IsInfinity(LyricLeft.Value)))LyricLeft=null;if(LyricTop.HasValue&&(double.IsNaN(LyricTop.Value)||double.IsInfinity(LyricTop.Value)))LyricTop=null;LyricOffset=Math.Max(-10000,Math.Min(10000,LyricOffset));NormalizeStyles();if(Material==null)Material=new MaterialOptions();Material.Normalize();Alarms=(Alarms??new List<ClockAlarm>()).Where(a=>a!=null).Take(50).ToList();foreach(var a in Alarms)a.Normalize();TimerPausedSeconds=LayoutRules.Clamp(TimerPausedSeconds,0,86400,0);City=(City??"").Trim();if(City.Length>100)City=City.Substring(0,100);Volume=LayoutRules.Clamp(Volume,0,1,.6);OpacityPercent=LayoutRules.Clamp(OpacityPercent,0,100,72);ClockPanel=Panel(ClockPanel);WeatherPanel=Panel(WeatherPanel);MusicPanel=Panel(MusicPanel);NotesPanel=Panel(NotesPanel);MusicMode=MusicMode=="local"?"local":"system";Playlist=(Playlist??new List<string>()).Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();TrackIndex=Playlist.Count==0?0:Math.Max(0,Math.Min(Playlist.Count-1,TrackIndex));RepeatMode=Math.Max(0,Math.Min(2,RepeatMode));Panels=(Panels??new List<WidgetPanelPosition>()).Where(p=>p!=null&&p.Id>=0&&p.Id<4).GroupBy(p=>p.Id).Select(g=>g.First()).ToList();foreach(var p in Panels)p.Normalize();}
        static int Panel(int value){return Math.Max(0,Math.Min(3,value));}
        public WidgetOptions Copy(){var c=(WidgetOptions)MemberwiseClone();c.Styles=(Styles??new List<WidgetStyle>()).Select(x=>x.Copy()).ToList();c.Material=(Material??new MaterialOptions()).Copy();c.Alarms=(Alarms??new List<ClockAlarm>()).Select(a=>a.Copy()).ToList();c.Playlist=new List<string>(Playlist??new List<string>());c.Panels=(Panels??new List<WidgetPanelPosition>()).Where(p=>p!=null).Select(p=>p.Copy()).ToList();return c;}
        public WidgetPanelPosition Position(int id){var p=Panels.FirstOrDefault(x=>x.Id==id);if(p==null){p=new WidgetPanelPosition{Id=id};Panels.Add(p);}return p;}
        public void Separate(){ClockPanel=0;WeatherPanel=1;MusicPanel=2;NotesPanel=3;}
        public void Fuse(){ClockPanel=WeatherPanel=MusicPanel=NotesPanel=0;}
    }
    [DataContract] public class WidgetPanelPosition {
        [DataMember(EmitDefaultValue=false)] public bool? HideControls;
        [DataMember] public int Id;
        [DataMember] public double? Left,Top,Width,Height,OpacityPercent,CompactWeatherWidth;
        public void Normalize(){Width=WidgetSizing.Dimension(Width,240,1600);CompactWeatherWidth=WidgetSizing.Dimension(CompactWeatherWidth,96,1600);Height=WidgetSizing.Dimension(Height,140,1600);if(OpacityPercent.HasValue)OpacityPercent=LayoutRules.Clamp(OpacityPercent.Value,0,100,72);if(Left.HasValue&&(double.IsNaN(Left.Value)||double.IsInfinity(Left.Value)))Left=null;if(Top.HasValue&&(double.IsNaN(Top.Value)||double.IsInfinity(Top.Value)))Top=null;}
        public WidgetPanelPosition Copy(){return (WidgetPanelPosition)MemberwiseClone();}
    }
    // A late Closed notification may only remove its own current instance.
    public sealed class WidgetRegistry<T> where T:class {
        readonly Dictionary<int,T> items=new Dictionary<int,T>();
        public List<T> Values{get{return items.Values.ToList();}}
        public List<int> Keys{get{return items.Keys.ToList();}}
        public bool TryGetValue(int id,out T value){return items.TryGetValue(id,out value);}
        public void Add(int id,T value){if(value==null)throw new ArgumentNullException("value");items.Add(id,value);}
        public bool Remove(int id,T expected){T current;if(!items.TryGetValue(id,out current)||!object.ReferenceEquals(current,expected))return false;return items.Remove(id);}
    }
    public static class WidgetRules {
        public static readonly string[] Kinds={"clock","weather","music","notes"};
        public static readonly string[] Names={"时钟","天气","音乐","备忘录"};
        public static bool Enabled(WidgetOptions o,string kind){return kind=="clock"?o.Clock:kind=="weather"?o.Weather:kind=="music"?o.Music:kind=="notes"&&o.Notes;}
        public static int Panel(WidgetOptions o,string kind){return kind=="clock"?o.ClockPanel:kind=="weather"?o.WeatherPanel:kind=="music"?o.MusicPanel:o.NotesPanel;}
        public static void Assign(WidgetOptions o,string kind,int panel){panel=Math.Max(0,Math.Min(3,panel));if(kind=="clock")o.ClockPanel=panel;else if(kind=="weather")o.WeatherPanel=panel;else if(kind=="music")o.MusicPanel=panel;else if(kind=="notes")o.NotesPanel=panel;}
        public static void Enable(WidgetOptions o,string kind,bool value){if(kind=="clock")o.Clock=value;else if(kind=="weather")o.Weather=value;else if(kind=="music")o.Music=value;else if(kind=="notes")o.Notes=value;}
        public static Dictionary<int,List<string>> Layout(WidgetOptions o){return Kinds.Where(k=>Enabled(o,k)).GroupBy(k=>Panel(o,k)).ToDictionary(g=>g.Key,g=>g.ToList());}
        public static void Recover(WidgetOptions o){foreach(string kind in Kinds)Enable(o,kind,true);o.Normalize();}
        public static int FreePanel(WidgetOptions o,string kind){var used=new HashSet<int>(Kinds.Where(k=>k!=kind&&Enabled(o,k)).Select(k=>Panel(o,k)));return Enumerable.Range(0,4).First(p=>!used.Contains(p));}
        public static int NextTrack(int current,int count,int repeat,bool shuffle,int random,bool ended){if(count<=0)return -1;if(ended&&repeat==2)return Math.Max(0,Math.Min(count-1,current));if(shuffle&&count>1){int next=Math.Abs(random%(count-1));return next>=current?next+1:next;}int index=current+1;return index<count?index:ended&&repeat==0?-1:0;}
        public static string Duration(double seconds){seconds=double.IsNaN(seconds)||double.IsInfinity(seconds)?0:Math.Max(0,seconds);var t=TimeSpan.FromSeconds(Math.Min(seconds,359999));return t.TotalHours>=1?((int)t.TotalHours)+":"+t.Minutes.ToString("00")+":"+t.Seconds.ToString("00"):((int)t.TotalMinutes)+":"+t.Seconds.ToString("00");}
    }
    public static class WidgetSizing {
        public static double? Dimension(double? value,double min,double max){if(!value.HasValue||double.IsNaN(value.Value)||double.IsInfinity(value.Value))return null;return Math.Max(min,Math.Min(max,value.Value));}
        public static double MinimumHeight(double natural,double available){return Math.Min(available,Math.Max(140,natural*.5+28));}
        public static double Background(WidgetOptions options,WidgetPanelPosition panel){return LayoutRules.Clamp(panel.OpacityPercent??options.OpacityPercent,0,100,72);}
    }
    public static class StartupEntryRules {
        public static string Leaf(string path){path=(path??"").Replace('\\','/');int slash=path.LastIndexOf('/');return slash>=0?path.Substring(slash+1):path;}
        public static string Stem(string path){string leaf=Leaf(path);int dot=leaf.LastIndexOf('.');return dot>0?leaf.Substring(0,dot):leaf;}
        public static bool Auxiliary(string path){string name=AutoGroupRules.Canonical(Stem(path));if(name.StartsWith("unins")||name.StartsWith("uninstall")||name.Contains("卸载")||name.EndsWith("updater")||name.Contains("crashreport")||name.Contains("crashpad"))return true;string[] helpers={"update","updater","autoupdate","setup","install","installer","crashreporter","crashreportclient","crashpadhandler","cefsubprocess","qtwebengineprocess","unitycrashhandler64","unitycrashhandler32","werfault","vc_redist","vcredist","dxsetup","dxwebsetup","dxdiag","readme","license","help","说明","帮助","repair","修复","reportbug","feedback"};return helpers.Any(x=>name==AutoGroupRules.Canonical(x)||name.StartsWith(AutoGroupRules.Canonical(x)+"64")||name.StartsWith(AutoGroupRules.Canonical(x)+"32"));}
        public static bool GameUri(string value){value=(value??"").Trim().ToLowerInvariant();return value.StartsWith("steam://rungameid/")||value.StartsWith("steam://run/")||value.StartsWith("com.epicgames.launcher://apps/")||value.StartsWith("uplay://launch/")||value.StartsWith("origin://launchgame/")||value.StartsWith("battlenet://");}
        public static bool Candidate(string path){if(!EntryRules.Importable(path)||DocumentRules.Supported(path)||Auxiliary(path))return false;string ext=System.IO.Path.GetExtension(path).ToLowerInvariant();return ext!=".url"||GameUri(ReadUrl(path));}
        public static string ReadUrl(string path){try{var info=new System.IO.FileInfo(path);if(!info.Exists||info.Length>32768)return "";foreach(string line in System.IO.File.ReadLines(path)){string text=line.Trim();if(text.StartsWith("URL=",StringComparison.OrdinalIgnoreCase))return text.Substring(4).Trim();}}catch{}return "";}
    }
    public static class AutoGroupRules {
        static bool Letter(char c){return c>='a'&&c<='z';}
        static bool Match(string text,string term){int start=0;while(start<text.Length){int at=text.IndexOf(term,start,StringComparison.Ordinal);if(at<0)return false;int end=at+term.Length;if((!Letter(term[0])||at==0||!Letter(text[at-1]))&&(!Letter(term[term.Length-1])||end==text.Length||!Letter(text[end])))return true;start=at+1;}return false;}
        public static string Canonical(string name){return new string((name??"").Normalize(System.Text.NormalizationForm.FormKC).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());}
        static string Words(string name){string text=new string((name??"").Normalize(System.Text.NormalizationForm.FormKC).ToLowerInvariant().Select(c=>char.IsLetterOrDigit(c)?c:' ').ToArray());return string.Join(" ",text.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries));}
        public static string Identity(Entry a){string target=Canonical(StartupEntryRules.Stem(a.LaunchTarget));return Canonical(string.IsNullOrWhiteSpace(a.LaunchTarget)||!string.IsNullOrWhiteSpace(a.LaunchArguments)||StartupEntryRules.GameUri(a.LaunchTarget)||target=="launcher"||target=="game"?a.Name:target);}
        static List<string[]> catalog=new List<string[]>();static bool loaded;static readonly object gate=new object();
        public static int LoadCatalog(IEnumerable<string> lines){var entries=new List<string[]>();foreach(string line in lines){if(string.IsNullOrWhiteSpace(line)||line.TrimStart().StartsWith("#"))continue;var columns=line.Split('\t');if(columns.Length!=3)continue;string group=columns[0].Trim(),mode=columns[1].Trim();if(group.Length==0||group=="全部"||group=="未分组"||(mode!="word"&&mode!="exact"))continue;foreach(string alias in columns[2].Split('|')){string key=Canonical(alias);if(key.Length>0)entries.Add(new[]{group,mode,key,Words(alias)});}}lock(gate){catalog=entries.OrderByDescending(e=>e[1]=="exact").ThenByDescending(e=>e[2].Length).ToList();loaded=true;}return entries.Count;}
        public static void ReloadCatalog(){string path=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data","AppGroups.tsv");try{LoadCatalog(System.IO.File.ReadLines(path));}catch{lock(gate){catalog=new List<string[]>();loaded=true;}}}
        public static string Guess(Entry entry,Preferences prefs){if(entry!=null&&entry.IsFolder)return null;string learned;if(prefs!=null&&prefs.LearnedGroups!=null&&prefs.LearnedGroups.TryGetValue(Identity(entry),out learned))return string.IsNullOrEmpty(learned)?null:learned;return Guess(entry);}
        public static string Guess(Entry entry){
            if(entry==null||entry.IsFolder||StartupEntryRules.Auxiliary(entry.Path)||StartupEntryRules.Auxiliary(entry.LaunchTarget))return null;
            string ext=System.IO.Path.GetExtension(entry.Path??"").ToLowerInvariant();if(ext.Length>0&&!EntryRules.Importable(entry.Path))return null;
            if(!loaded)ReloadCatalog();
            var titles=new[]{Canonical(entry.Name),Canonical(StartupEntryRules.Stem(entry.Path)),Canonical(StartupEntryRules.Stem(entry.LaunchTarget))};
            var words=new[]{Words(entry.Name),Words(StartupEntryRules.Stem(entry.Path)),Words(StartupEntryRules.Stem(entry.LaunchTarget)),Words(entry.ProductInfo)};string description=Canonical(entry.ProductInfo);List<string[]> entries;lock(gate)entries=catalog;
            foreach(var rule in entries){if(rule[1]=="exact"?titles.Any(t=>t==rule[2]):titles.Any(t=>Match(t,rule[2]))||Match(description,rule[2])||words.Any(t=>Match(t,rule[3])))return rule[0];}
            string text=((entry.Name??"")+" "+StartupEntryRules.Leaf(entry.Path)+" "+StartupEntryRules.Leaf(entry.LaunchTarget)+" "+(entry.ProductInfo??"")).ToLowerInvariant();
            string[][] rules={
                new[]{"系统工具","uninstall","卸载","winrar","7-zip","7zip","todesk","clash","powershell","任务管理器","注册表","控制面板"},
                new[]{"音乐","qqmusic","qq音乐","网易云音乐","cloudmusic","酷狗","酷我","spotify","foobar","pro tools","protools","ableton","audition","fl studio","reaper","cubase","asio4all"},
                new[]{"开发","visual studio","vscode","code","pycharm","intellij","git","python","unity","unreal","codex","trae"},
                new[]{"创作","photoshop","after effects","premiere","illustrator","lightroom","indesign","blender","maya","3ds max","3dmax","cinema 4d","c4d","marmoset","substance","davinci","resolve","capcut","剪映","canva","houdini","绘画"},
                new[]{"游戏","steam","epic games","wegame","ubisoft","battle.net","rockstar","5e","游戏","对战平台","战网"},
                new[]{"办公","word","excel","powerpoint","onenote","outlook","wps","acrobat","foxit","notion","obsidian","飞书","钉钉"},
                new[]{"浏览器","chrome","msedge","edge","firefox","opera","brave","浏览器"},
                new[]{"社交","qq","微信","wechat","kook","discord","telegram","tt语音"}
            };
            foreach(var rule in rules)for(int i=1;i<rule.Length;i++)if(Match(text,rule[i]))return rule[0];
            if(StartupEntryRules.GameUri(entry.LaunchTarget)||StartupEntryRules.GameUri(entry.LaunchArguments))return "游戏";
            string target=(entry.LaunchTarget??entry.Path??"").Replace('\\','/').ToLowerInvariant();if(target.Contains("/steamapps/common/"))return "游戏";
            return null;
        }
        public static int ApplyUnassigned(Preferences prefs){int count=0;foreach(var entry in prefs.Apps){if(FeatureRules.GroupName(entry)!="未分组")continue;string group=Guess(entry,prefs);if(group==null)continue;entry.Group=group;count++;}prefs.Groups=FeatureRules.Groups(prefs.Groups,prefs.Apps);return count;}
    }

    public static class MediaTimeline {
        public static double Position(double position,double duration,bool playing,double rate,double age){duration=LayoutRules.Clamp(duration,0,359999,0);if(duration<=0)return 0;position=LayoutRules.Clamp(position,0,duration,0);rate=LayoutRules.Clamp(rate,0,16,1);age=LayoutRules.Clamp(age,0,86400,0);return Math.Min(duration,position+(playing?rate*age:0));}
        public static double ClampSeek(double seconds,double minimum,double maximum,double duration){duration=LayoutRules.Clamp(duration,0,359999,0);minimum=LayoutRules.Clamp(minimum,0,duration,0);maximum=LayoutRules.Clamp(maximum,0,duration,duration);if(maximum<=minimum)maximum=duration;return LayoutRules.Clamp(seconds,minimum,maximum,minimum);}
    }
    public sealed class MediaSeekQueue {
        double? value;string key;
        public void Put(double seconds,string track){value=seconds;key=track;}
        public bool Take(string track,out double seconds){seconds=0;if(!value.HasValue)return false;double result=value.Value;value=null;if(key!=track)return false;seconds=result;return true;}
        public void Clear(){value=null;key=null;}
    }
    public static partial class StartupRules {
        public static string Command(string path){if(string.IsNullOrWhiteSpace(path)||path.IndexOf('"')>=0||path.IndexOf('\r')>=0||path.IndexOf('\n')>=0)throw new ArgumentException("启动路径无效");return "\""+path+"\" --startup";}
        public static bool Enabled(string target,byte[] approved){return !string.IsNullOrWhiteSpace(target)&&!(approved!=null&&approved.Length>0&&(approved[0]==3||approved[0]==7));}
    }
    [DataContract] public class NoteItem : System.ComponentModel.INotifyPropertyChanged {
        [DataMember] public string Id=Guid.NewGuid().ToString("N"),Body="";
        string title="新备忘录";
        [DataMember] public string Title{get{return title;}set{if(title==value)return;title=value;var changed=PropertyChanged;if(changed!=null){changed(this,new System.ComponentModel.PropertyChangedEventArgs("Title"));changed(this,new System.ComponentModel.PropertyChangedEventArgs("DisplayTitle"));}}}
        public string DisplayTitle{get{return string.IsNullOrWhiteSpace(Title)?"未命名备忘录":Title;}}
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        [DataMember] public long Updated=DateTime.UtcNow.Ticks;
        public void Normalize(){if(string.IsNullOrWhiteSpace(Id))Id=Guid.NewGuid().ToString("N");Title=string.IsNullOrWhiteSpace(Title)?"未命名备忘录":Title.Trim();if(Title.Length>120)Title=Title.Substring(0,120);Body=Body??"";}
        public override string ToString(){return Title;}
    }
    [DataContract] public class NoteBook {
        [DataMember] public List<NoteItem> Items=new List<NoteItem>();
        [DataMember] public string SelectedId;
        public void Normalize(){Items=(Items??new List<NoteItem>()).Where(n=>n!=null).ToList();foreach(var n in Items)n.Normalize();Items=Items.GroupBy(n=>n.Id).Select(g=>g.First()).ToList();if(Items.Count==0)Items.Add(new NoteItem{Title="随手记"});if(!Items.Any(n=>n.Id==SelectedId))SelectedId=Items[0].Id;}
        public NoteItem Selected{get{return Items.First(n=>n.Id==SelectedId);}}
    }
    [DataContract] public class AppearanceScheme {
        [DataMember] public int Version=1;
        [DataMember] public string Name{get;set;}
        [DataMember] public bool Light;
        [DataMember] public bool BoardHideSearch,BoardHideGroups,BoardHideFolders;
        [DataMember] public int PanelSize;
        [DataMember] public double BoardIconSize=48,BoardIconGap=5;
        [OnDeserializing] void Defaults(StreamingContext context){BoardIconSize=48;BoardIconGap=5;}
        [DataMember] public double OpacityPercent=72,DockGap=12;
        [DataMember] public DockOptions Dock;
        [DataMember] public MaterialOptions BoardMaterial;
        [DataMember] public PlacementOptions Placement;
        [DataMember] public WidgetOptions Widgets;
        [DataMember] public List<Entry> Apps;
        [DataMember] public List<string> Groups;
        [DataMember(EmitDefaultValue=false)] public List<string> DockOrder;
        [DataMember] public Dictionary<string,string> Icons=new Dictionary<string,string>();
    }
    public static class FeatureRules {
        public static string ImportGroup(Preferences prefs,Entry entry,string target){if(target=="未分组")return null;if(!string.IsNullOrWhiteSpace(target)&&target!="全部"&&prefs.Groups.Contains(target))return target;return prefs.AutoGrouping?AutoGroupRules.Guess(entry,prefs):null;}
        public static void NormalizeMemberships(Entry a){a.Group=string.IsNullOrWhiteSpace(a.Group)?null:a.Group.Trim();if(a.Group=="全部"||a.Group=="未分组")a.Group=null;if(a.ExtraGroups==null)a.ExtraGroups=new List<string>();a.ExtraGroups=a.ExtraGroups.Where(g=>!string.IsNullOrWhiteSpace(g)).Select(g=>g.Trim()).Where(g=>g!="全部"&&g!="未分组"&&!string.Equals(g,a.Group,StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();if(string.IsNullOrWhiteSpace(a.Group)&&a.ExtraGroups.Count>0){a.Group=a.ExtraGroups[0];a.ExtraGroups.RemoveAt(0);}}
        public static bool InGroup(Entry a,string group){if(group=="全部")return true;if(group=="未分组")return string.IsNullOrWhiteSpace(a.Group)&&(a.ExtraGroups==null||a.ExtraGroups.Count==0);return string.Equals(a.Group,group,StringComparison.OrdinalIgnoreCase)||(a.ExtraGroups!=null&&a.ExtraGroups.Any(g=>string.Equals(g,group,StringComparison.OrdinalIgnoreCase)));}
        public static IEnumerable<string> Memberships(Entry a){return new[]{a.Group}.Concat(a.ExtraGroups??new List<string>()).Where(g=>!string.IsNullOrWhiteSpace(g));}
        public static void AddMembership(Entry a,string group){NormalizeMemberships(a);if(group=="未分组"){a.Group=null;a.ExtraGroups.Clear();return;}if(InGroup(a,group))return;if(string.IsNullOrWhiteSpace(a.Group))a.Group=group;else a.ExtraGroups.Add(group);}
        public static void RemoveMembership(Entry a,string group){if(string.Equals(a.Group,group,StringComparison.OrdinalIgnoreCase))a.Group=null;if(a.ExtraGroups!=null)a.ExtraGroups.RemoveAll(g=>string.Equals(g,group,StringComparison.OrdinalIgnoreCase));NormalizeMemberships(a);}
        public static int AddShared(Preferences p,IEnumerable<string> paths,string group){if(group=="全部"||string.IsNullOrWhiteSpace(group))throw new ArgumentException("请先选择目标分组");var set=new HashSet<string>(paths,StringComparer.OrdinalIgnoreCase);int count=0;foreach(var a in p.Apps.Where(a=>set.Contains(a.Path))){if(InGroup(a,group))continue;AddMembership(a,group);count++;}p.Groups=Groups(p.Groups,p.Apps);return count;}
        public static int Transfer(Preferences p,IEnumerable<string> paths,string from,string to){if(from==to)return 0;if(to=="全部")throw new ArgumentException("不能移动到全部分类");if(from=="全部")return AddShared(p,paths,to);var set=new HashSet<string>(paths,StringComparer.OrdinalIgnoreCase);int count=0;foreach(var a in p.Apps.Where(a=>set.Contains(a.Path)&&InGroup(a,from))){RemoveMembership(a,from);AddMembership(a,to);count++;}p.Groups=Groups(p.Groups,p.Apps);return count;}
        public static string GroupName(Entry entry){return string.IsNullOrWhiteSpace(entry.Group)?"未分组":entry.Group.Trim();}
        public static List<string> Groups(IEnumerable<string> groups,IEnumerable<Entry> apps){return (groups??new string[]{"创作","办公","游戏"}).Concat(apps.SelectMany(Memberships)).Select(s=>(s??"").Trim()).Where(s=>s.Length>0&&s!="全部"&&s!="未分组").Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();}
        public static int Move(Preferences p,IEnumerable<string> paths,string group){string name=(group??"").Trim();if(name=="全部")throw new ArgumentException("不能移动到全部分类");var set=new HashSet<string>(paths,StringComparer.OrdinalIgnoreCase);var entries=p.Apps.Where(a=>set.Contains(a.Path)).ToList();foreach(var a in entries){a.Group=name=="未分组"?null:name;a.ExtraGroups=new List<string>();string key=AutoGroupRules.Identity(a);if(key.Length>0)p.LearnedGroups[key]=a.Group??"";}p.Groups=Groups(p.Groups,p.Apps);return entries.Count;}
        public static void RenameGroup(Preferences p,string before,string after){after=(after??"").Trim();if(after.Length==0||after=="全部"||after=="未分组")throw new ArgumentException("请输入有效分组名称");if(p.Groups.Any(g=>g.Equals(after,StringComparison.CurrentCultureIgnoreCase)&&!g.Equals(before,StringComparison.CurrentCultureIgnoreCase)))throw new ArgumentException("分组名称重复");foreach(string key in p.LearnedGroups.Keys.ToList())if(p.LearnedGroups[key]==before)p.LearnedGroups[key]=after;foreach(var a in p.Apps){if(GroupName(a)==before)a.Group=after;if(a.ExtraGroups!=null)a.ExtraGroups=a.ExtraGroups.Select(g=>g==before?after:g).ToList();NormalizeMemberships(a);}p.Groups=p.Groups.Select(g=>g==before?after:g).ToList();p.Groups=Groups(p.Groups,p.Apps);if(string.Equals(p.DesktopFolderGroup,before,StringComparison.OrdinalIgnoreCase))p.DesktopFolderGroup=after;if(string.Equals(p.DesktopDocsGroup,before,StringComparison.OrdinalIgnoreCase))p.DesktopDocsGroup=after;}
        public static void DeleteGroup(Preferences p,string group){group=(group??"").Trim();if(group.Length==0||group=="全部"||group=="未分组")return;foreach(string key in p.LearnedGroups.Keys.Where(k=>string.Equals(p.LearnedGroups[k],group,StringComparison.OrdinalIgnoreCase)).ToList())p.LearnedGroups.Remove(key);foreach(var a in p.Apps)RemoveMembership(a,group);p.Groups.RemoveAll(g=>string.Equals((g??"").Trim(),group,StringComparison.OrdinalIgnoreCase));p.Groups=Groups(p.Groups,p.Apps);if(string.Equals(p.DesktopFolderGroup,group,StringComparison.OrdinalIgnoreCase))p.DesktopFolders=false;if(string.Equals(p.DesktopDocsGroup,group,StringComparison.OrdinalIgnoreCase))p.DesktopDocs=false;}
        public static Entry Copy(Entry a){return new Entry{Name=a.Name,Path=a.Path,Pinned=a.Pinned,IsFolder=a.IsFolder,Group=a.Group,ExtraGroups=a.ExtraGroups==null?null:a.ExtraGroups.ToList(),IconPath=a.IconPath,LaunchTarget=a.LaunchTarget,LaunchArguments=a.LaunchArguments,ProductInfo=a.ProductInfo,WindowTarget=a.WindowTarget,DesktopAuto=a.DesktopAuto};}
        public static AppearanceScheme Capture(Preferences p,string name){return new AppearanceScheme{Name=name,Light=p.Light,BoardHideSearch=p.BoardHideSearch,BoardHideGroups=p.BoardHideGroups,BoardHideFolders=p.BoardHideFolders,PanelSize=p.PanelSize,BoardIconSize=p.BoardIconSize,BoardIconGap=p.BoardIconGap,OpacityPercent=p.OpacityPercent??72,DockGap=p.DockGap,Dock=p.Dock.Copy(),BoardMaterial=p.BoardMaterial.Copy(),DockOrder=(p.DockOrder??new List<string>()).ToList(),Placement=p.Placement.Copy(),Widgets=p.Widgets.Copy(),Apps=p.Apps.Select(Copy).ToList(),Groups=p.Groups.ToList()};}
        public static void Apply(Preferences p,AppearanceScheme scheme){
            if(scheme==null||scheme.Version!=1||scheme.Dock==null)throw new ArgumentException("方案格式或版本不支持");
            var layout=EntryRules.NormalizeEntries((scheme.Apps??new List<Entry>()).Where(a=>a!=null&&!string.IsNullOrWhiteSpace(a.Path)).Select(Copy));
            var known=new HashSet<string>(layout.Select(a=>a.Path),StringComparer.OrdinalIgnoreCase);layout.AddRange(p.Apps.Where(a=>!known.Contains(a.Path)));p.Apps=layout;p.Removed.RemoveAll(known.Contains);
            p.Light=scheme.Light;p.BoardHideSearch=scheme.BoardHideSearch;p.BoardHideGroups=scheme.BoardHideGroups;p.BoardHideFolders=scheme.BoardHideFolders;p.PanelSize=scheme.PanelSize;p.BoardIconSize=scheme.BoardIconSize;p.BoardIconGap=scheme.BoardIconGap;p.OpacityPercent=scheme.OpacityPercent;p.DockGap=scheme.DockGap;p.Dock=scheme.Dock.Copy();p.BoardMaterial=(scheme.BoardMaterial??new MaterialOptions()).Copy();p.DockOrder=(scheme.DockOrder??new List<string>()).ToList();p.Placement=(scheme.Placement??new PlacementOptions()).Copy();p.Widgets=(scheme.Widgets??new WidgetOptions()).Copy();p.Groups=(scheme.Groups??new List<string>()).ToList();p.Normalize();
        }
        public static uint AutoHideState(uint original,bool enabled){return enabled?original|1u:original&~1u;}
        public static double Position(double desired,double origin,double extent,double length){return Math.Max(origin,Math.Min(origin+Math.Max(0,extent-length),desired));}
        public static string WeatherLabel(int code){if(code==0)return "晴";if(code>=1&&code<=3)return "多云";if(code==45||code==48)return "雾";if(code>=51&&code<=67)return "雨";if(code>=71&&code<=77)return "雪";if(code>=80&&code<=82)return "阵雨";if(code==85||code==86)return "阵雪";if(code==95||code==96||code==99)return "雷雨";return "天气代码 "+code;}
    }
    public class AutoHidePolicy {
        public bool Hidden;DateTime lastSeen=DateTime.MinValue;
        public bool Update(DateTime now,bool enabled,bool over,bool edge,bool blocked,double delay){if(!enabled||over||edge||blocked){lastSeen=now;Hidden=false;}else if(lastSeen==DateTime.MinValue)lastSeen=now;else if((now-lastSeen).TotalMilliseconds>=delay)Hidden=true;return Hidden;}
    }
}
