using System;using System.IO;using System.Text;using System.Linq;using System.Collections.Generic;using System.Runtime.Serialization;using System.Runtime.Serialization.Json;using System.Threading;using System.IO.Compression;using System.Security.Cryptography;
namespace Lumina {
public static class LayoutRules {
        public static int Pages(int count,int capacity) {
            if(count<0 || capacity<1)throw new ArgumentOutOfRangeException();
            return Math.Max(1,(count+capacity-1)/capacity);
        }
        public static bool Matches(string name,string query) {
            return (query??"").Split(new char[]{' '},StringSplitOptions.RemoveEmptyEntries).All(word=>(name??"").IndexOf(word,StringComparison.CurrentCultureIgnoreCase)>=0);
        }
        public static double GlassOpacity(double value) {return double.IsNaN(value)||double.IsInfinity(value)?72:Math.Max(10,Math.Min(95,value));}
        public static bool AllowImport(string path,IEnumerable<string> removed,bool manual) {return manual||!removed.Any(p=>string.Equals(p,path,StringComparison.OrdinalIgnoreCase));}
        public static double Clamp(double value,double min,double max,double fallback) {return double.IsNaN(value)||double.IsInfinity(value)?fallback:Math.Max(min,Math.Min(max,value));}
    }
[DataContract] public class HotkeySetting {
        [DataMember] public int ActionId;
        [DataMember] public uint Modifiers;
        [DataMember] public uint Key;
        [DataMember] public bool Enabled;
        public HotkeySetting Copy() {return new HotkeySetting{ActionId=ActionId,Modifiers=Modifiers,Key=Key,Enabled=Enabled};}
    }
public static class HotkeyRules {
        public static readonly string[] Actions={"展开 / 收起面板","隐藏 / 显示任务栏","隐藏 / 显示桌面图标","添加软件","外观设置","恢复任务栏与图标","Windows 任务栏自动隐藏"};
        public static readonly uint[] ModifierValues={3,6,5,7,2,1,4};
        public static readonly string[] ModifierNames={"Ctrl + Alt","Ctrl + Shift","Alt + Shift","Ctrl + Alt + Shift","Ctrl","Alt","Shift"};
        public static List<HotkeySetting> Defaults() {
            uint[] keys={0x20,0x54,0x44,0x41,0x53,0x52,0x48};return keys.Select((k,i)=>new HotkeySetting{ActionId=i+1,Modifiers=3,Key=k,Enabled=true}).ToList();
        }
        public static Dictionary<string,uint> Keys() {
            var keys=new Dictionary<string,uint>();keys["Space"]=0x20;
            for(uint i=65;i<=90;i++)keys[((char)i).ToString()]=i;
            for(uint i=48;i<=57;i++)keys[((char)i).ToString()]=i;
            for(uint i=1;i<=12;i++)keys["F"+i]=0x6F+i;
            keys["Enter"]=0x0D;keys["Tab"]=0x09;keys["Home"]=0x24;keys["End"]=0x23;keys["PageUp"]=0x21;keys["PageDown"]=0x22;keys["Insert"]=0x2D;keys["Delete"]=0x2E;return keys;
        }
        public static string Label(HotkeySetting key) {
            if(key==null||!key.Enabled)return "已关闭";
            int index=Array.IndexOf(ModifierValues,key.Modifiers);string name=Keys().FirstOrDefault(p=>p.Value==key.Key).Key??("VK "+key.Key);
            return (index>=0?ModifierNames[index]:"?")+" + "+name;
        }
        public static string Validate(List<HotkeySetting> keys) {
            if(keys==null||keys.Count!=Actions.Length||keys.Select(k=>k==null?0:k.ActionId).Distinct().Count()!=Actions.Length||keys.Any(k=>k==null||k.ActionId<1||k.ActionId>Actions.Length))return "快捷键配置必须包含全部七项功能。";
            if(keys.Any(k=>!ModifierValues.Contains(k.Modifiers)||!Keys().ContainsValue(k.Key)))return "请选择有效的组合键和按键。";
            var duplicates=keys.Where(k=>k.Enabled).GroupBy(k=>k.Modifiers+":"+k.Key).FirstOrDefault(g=>g.Count()>1);
            if(duplicates!=null)return "快捷键重复："+string.Join("、",duplicates.Select(k=>Actions[k.ActionId-1]))+"。请换一个组合。";
            return null;
        }
        public static List<HotkeySetting> Normalize(List<HotkeySetting> keys) {
            var result=Defaults();
            if(keys!=null)foreach(var k in keys.Where(k=>k!=null&&k.ActionId>=1&&k.ActionId<=Actions.Length&&ModifierValues.Contains(k.Modifiers)&&Keys().ContainsValue(k.Key)))result[k.ActionId-1]=k.Copy();
            // Keep old user bindings when the new default collides; disable only the added binding.
            if(Validate(result)!=null&&keys!=null&&keys.All(k=>k==null||k.ActionId!=7))result[6].Enabled=false;
            return Validate(result)==null?result:Defaults();
        }
        public static bool Same(List<HotkeySetting> first,List<HotkeySetting> second) {
            if(first==null||second==null||first.Count!=second.Count)return false;
            return first.All(a=>second.Any(b=>a.ActionId==b.ActionId&&a.Modifiers==b.Modifiers&&a.Key==b.Key&&a.Enabled==b.Enabled));
        }
    }
public class HotkeyManager {
        readonly Func<HotkeySetting,bool> register;readonly Action<int> unregister;
        public List<HotkeySetting> Active=new List<HotkeySetting>();
        public HotkeyManager(Func<HotkeySetting,bool> registerKey,Action<int> unregisterKey) {register=registerKey;unregister=unregisterKey;}
        public List<string> Start(IEnumerable<HotkeySetting> keys) {
            var failed=new List<string>();foreach(var k in keys.Where(k=>k.Enabled)) {
                if(register(k))Active.Add(k.Copy());else failed.Add(HotkeyRules.Actions[k.ActionId-1]+"（"+HotkeyRules.Label(k)+"）");
            }return failed;
        }
        public void Release() {foreach(var k in Active)unregister(k.ActionId);Active.Clear();}
        public bool TryApply(List<HotkeySetting> candidate,out string error) {
            error=HotkeyRules.Validate(candidate);if(error!=null)return false;
            var previous=Active.Select(k=>k.Copy()).ToList();Release();
            var failed=Start(candidate);
            if(failed.Count==0)return true;
            Release();var rollbackFailed=Start(previous);
            error="无法注册："+string.Join("、",failed)+"。可能被其他软件或 Windows 占用，原设置未改动。";
            if(rollbackFailed.Count>0)error+="\n原快捷键恢复失败："+string.Join("、",rollbackFailed)+"。对应操作可从托盘执行。";
            return false;
        }
    }
[DataContract] public class Entry {
        [DataMember] public string Name;
        [DataMember] public string Path;
        [DataMember] public bool Pinned;
        [DataMember(EmitDefaultValue=false)] public bool IsFolder;
        [DataMember(EmitDefaultValue=false)] public string Group;
        [DataMember(EmitDefaultValue=false)] public List<string> ExtraGroups;
        [DataMember(EmitDefaultValue=false)] public string IconPath;
        [DataMember(EmitDefaultValue=false)] public string LaunchTarget,LaunchArguments,ProductInfo;
    }
[DataContract] public class Preferences {
        [DataMember] public List<Entry> Apps = new List<Entry>();
        [DataMember] public bool Light = false;
        [DataMember] public double DockGap = 12;
        [DataMember] public bool Imported = false;
        [DataMember] public int PanelSize = 1;
        [DataMember] public double BoardIconSize=48,BoardIconGap=5;
        [DataMember(EmitDefaultValue=false)] public double? OpacityPercent = 72;
        [DataMember(EmitDefaultValue=false)] public List<string> Removed = new List<string>();
        [DataMember(EmitDefaultValue=false)] public List<HotkeySetting> Hotkeys = HotkeyRules.Defaults();
        [DataMember(EmitDefaultValue=false)] public DockOptions Dock = new DockOptions();
        [DataMember(EmitDefaultValue=false)] public List<string> Groups=new List<string>{"创作","办公","游戏"};
        [DataMember(EmitDefaultValue=false)] public List<Workspace> Workspaces=new List<Workspace>();
        [DataMember(EmitDefaultValue=false)] public PlacementOptions Placement=new PlacementOptions();
        [DataMember(EmitDefaultValue=false)] public WidgetOptions Widgets=new WidgetOptions();
        [DataMember] public bool AutoGrouping=true;
        [DataMember] public Dictionary<string,string> LearnedGroups=new Dictionary<string,string>();
        [OnDeserializing] void Defaults(StreamingContext context){AutoGrouping=true;BoardIconSize=48;BoardIconGap=5;}
        public bool HotkeyMigrationConflict;
        public void Normalize() {
            if(LearnedGroups==null)LearnedGroups=new Dictionary<string,string>();
            if(Apps==null)Apps=new List<Entry>();
            Apps=Apps.Where(a=>a!=null&&!string.IsNullOrWhiteSpace(a.Path)).ToList();
            foreach(var a in Apps){if(string.IsNullOrWhiteSpace(a.Name))a.Name=System.IO.Path.GetFileNameWithoutExtension(a.Path);FeatureRules.NormalizeMemberships(a);}
            Apps=EntryRules.NormalizeEntries(Apps);
            BoardIconSize=LayoutRules.Clamp(BoardIconSize,28,80,48);BoardIconGap=LayoutRules.Clamp(BoardIconGap,0,20,5);
            DockGap=LayoutRules.Clamp(DockGap,0,160,12);PanelSize=Math.Max(0,Math.Min(2,PanelSize));
            OpacityPercent=LayoutRules.GlassOpacity(OpacityPercent??72);if(Removed==null)Removed=new List<string>();
            bool oldKeys=Hotkeys!=null&&Hotkeys.All(k=>k==null||k.ActionId!=7);Hotkeys=HotkeyRules.Normalize(Hotkeys);HotkeyMigrationConflict=oldKeys&&!Hotkeys[6].Enabled;
            if(Dock==null)Dock=new DockOptions{OpacityPercent=OpacityPercent.Value};Dock.Normalize();
            Groups=FeatureRules.Groups(Groups,Apps);if(Workspaces==null)Workspaces=new List<Workspace>();
            Workspaces=Workspaces.Where(x=>x!=null&&!string.IsNullOrWhiteSpace(x.Name)).ToList();foreach(var work in Workspaces)work.Normalize();
            if(Placement==null)Placement=new PlacementOptions();Placement.Normalize();if(Widgets==null)Widgets=new WidgetOptions();Widgets.Normalize();
        }
    }
[DataContract] public class DockOptions {
        [DataMember] public double IconSize=44,Spacing=6,Padding=16,Radius=28,OpacityPercent=72,Zoom=1.2,Offset=0,MotionLevel=1;
        [DataMember] public int Alignment=1,Accent=0,MaxPinned=9,ComputerStyle=0;
        [DataMember] public bool ShowComputer=true,ShowLabels=false,ShowSeparator=true,AlwaysOnTop=false,MotionEnabled=true;
        [DataMember] public bool AutoHide=false,ShowRunning=true;
        [DataMember] public double HideDelay=900,EdgeRange=12,BlurRadius=18,EdgeStrength=1;
        [OnDeserializing] void Defaults(StreamingContext context) {
            IconSize=44;Spacing=6;Padding=16;Radius=28;OpacityPercent=72;Zoom=1.2;Offset=0;MotionLevel=1;
            Alignment=1;Accent=0;MaxPinned=9;ComputerStyle=0;ShowComputer=true;ShowLabels=false;ShowSeparator=true;AlwaysOnTop=false;MotionEnabled=true;
            AutoHide=false;ShowRunning=true;HideDelay=900;EdgeRange=12;BlurRadius=18;EdgeStrength=1;
        }
        public DockOptions Copy() {return (DockOptions)MemberwiseClone();}
        public void Normalize() {
            IconSize=LayoutRules.Clamp(IconSize,32,64,44);Spacing=LayoutRules.Clamp(Spacing,0,20,6);Padding=LayoutRules.Clamp(Padding,8,28,16);
            Radius=LayoutRules.Clamp(Radius,0,48,28);OpacityPercent=LayoutRules.GlassOpacity(OpacityPercent);Zoom=LayoutRules.Clamp(Zoom,1,1.6,1.2);Offset=LayoutRules.Clamp(Offset,-500,500,0);
            MotionLevel=LayoutRules.Clamp(MotionLevel,.5,1.5,1);
            HideDelay=LayoutRules.Clamp(HideDelay,200,3000,900);EdgeRange=LayoutRules.Clamp(EdgeRange,3,48,12);BlurRadius=LayoutRules.Clamp(BlurRadius,0,40,18);EdgeStrength=LayoutRules.Clamp(EdgeStrength,.2,2,1);
            Alignment=Math.Max(0,Math.Min(2,Alignment));Accent=Math.Max(0,Math.Min(5,Accent));MaxPinned=Math.Max(3,Math.Min(20,MaxPinned));ComputerStyle=Math.Max(0,Math.Min(1,ComputerStyle));
        }
        public int VisiblePinned(double availableWidth,int count) {
            int fixedSlots=2+(ShowComputer?1:0);double cell=IconSize+16+Spacing;
            int fit=Math.Max(0,(int)Math.Floor((availableWidth-28-HoverPadding()*2-(ShowSeparator?17:0))/cell)-fixedSlots);
            return Math.Min(count,Math.Min(MaxPinned,fit));
        }
        public double HoverPadding() {return Math.Max(Padding,(IconSize+16)*(Math.Min(1.6,1+(Zoom-1)*MotionLevel)-1)/2+2);}
        public double WidthFor(int pinnedCount) {return 28+HoverPadding()*2+(2+(ShowComputer?1:0)+pinnedCount)*(IconSize+16+Spacing)+(ShowSeparator?17:0);}
    }
public class RemovedItem {public Entry Entry;public int Index;public bool WasRemoved,MembershipOnly;public string RemovedGroup;}
public class RemovalBatch {public List<RemovedItem> Items=new List<RemovedItem>();}
public static class EntryRules {
        public static RemovalBatch Remove(Preferences prefs,IEnumerable<string> paths,string group=null) {
            var wanted=new HashSet<string>(paths,StringComparer.OrdinalIgnoreCase);var batch=new RemovalBatch();
            for(int i=0;i<prefs.Apps.Count;i++) {var a=prefs.Apps[i];if(!wanted.Contains(a.Path))continue;if(group!=null&&group!="全部"&&FeatureRules.InGroup(a,group)&&FeatureRules.Memberships(a).Count()>1){batch.Items.Add(new RemovedItem{Entry=FeatureRules.Copy(a),Index=i,MembershipOnly=true,RemovedGroup=group});FeatureRules.RemoveMembership(a,group);wanted.Remove(a.Path);}else batch.Items.Add(new RemovedItem{Entry=a,Index=i,WasRemoved=prefs.Removed.Any(p=>string.Equals(p,a.Path,StringComparison.OrdinalIgnoreCase))});}
            prefs.Apps.RemoveAll(a=>wanted.Contains(a.Path));
            foreach(var item in batch.Items)if(!item.MembershipOnly&&!item.WasRemoved)prefs.Removed.Add(item.Entry.Path);
            return batch;
        }
        public static int Undo(Preferences prefs,RemovalBatch batch) {
            int restored=0;if(batch==null)return 0;
            foreach(var item in batch.Items.OrderBy(x=>x.Index)) {
                if(item.MembershipOnly){var existing=prefs.Apps.FirstOrDefault(a=>string.Equals(a.Path,item.Entry.Path,StringComparison.OrdinalIgnoreCase));if(existing!=null){if(!string.IsNullOrWhiteSpace(item.RemovedGroup))FeatureRules.AddMembership(existing,item.RemovedGroup);else foreach(var name in FeatureRules.Memberships(item.Entry))FeatureRules.AddMembership(existing,name);restored++;continue;}}
                if(!prefs.Apps.Any(a=>string.Equals(a.Path,item.Entry.Path,StringComparison.OrdinalIgnoreCase))) {prefs.Apps.Insert(Math.Min(item.Index,prefs.Apps.Count),item.Entry);restored++;}
                if(!item.WasRemoved)prefs.Removed.RemoveAll(p=>string.Equals(p,item.Entry.Path,StringComparison.OrdinalIgnoreCase));
            }prefs.Groups=FeatureRules.Groups(prefs.Groups,prefs.Apps);return restored;
        }
        public static List<Entry> NormalizeEntries(IEnumerable<Entry> apps){var result=new List<Entry>();var byPath=new Dictionary<string,Entry>(StringComparer.OrdinalIgnoreCase);foreach(var a in apps??new Entry[0]){if(a==null||string.IsNullOrWhiteSpace(a.Path))continue;if(Directory.Exists(a.Path)){a.Path=EntryPathRules.Normalize(a.Path);a.IsFolder=true;}FeatureRules.NormalizeMemberships(a);Entry existing;if(!byPath.TryGetValue(a.Path,out existing)){byPath.Add(a.Path,a);result.Add(a);continue;}foreach(var group in FeatureRules.Memberships(a))FeatureRules.AddMembership(existing,group);existing.Pinned|=a.Pinned;existing.IsFolder|=a.IsFolder;if(string.IsNullOrEmpty(existing.IconPath))existing.IconPath=a.IconPath;if(string.IsNullOrEmpty(existing.LaunchTarget)){existing.LaunchTarget=a.LaunchTarget;existing.LaunchArguments=a.LaunchArguments;}if(string.IsNullOrEmpty(existing.ProductInfo))existing.ProductInfo=a.ProductInfo;}return result;}
        public static bool Importable(string path) {string ext=System.IO.Path.GetExtension(path).ToLowerInvariant();return ext==".lnk"||ext==".appref-ms"||ext==".exe"||ext==".url"||ext==".cmd"||ext==".bat";}
        public static List<string> Candidates(IEnumerable<string> paths,IEnumerable<Entry> existing) {
            var known=new HashSet<string>(existing.Select(a=>a.Path),StringComparer.OrdinalIgnoreCase);
            known=new HashSet<string>(known.Select(EntryPathRules.Normalize),StringComparer.OrdinalIgnoreCase);
            return paths.Select(EntryPathRules.Normalize).Where(p=>!string.IsNullOrWhiteSpace(p)&&!known.Contains(p)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(EntryPathRules.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
        }
    }
public class ScanResult {public List<string> Paths=new List<string>();public int Skipped;public bool Limited;}
public static class ImportScanner {
        public static ScanResult Scan(IEnumerable<string> roots,bool recurse,CancellationToken cancellation,int fileLimit=2500,int directoryLimit=10000,bool launchOnly=false) {
            var result=new ScanResult();var pending=new Stack<string>(roots.Where(Directory.Exists));var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while(pending.Count>0) {
                cancellation.ThrowIfCancellationRequested();string folder=pending.Pop();if(!visited.Add(folder))continue;
                if(visited.Count>directoryLimit||result.Paths.Count>=fileLimit){result.Limited=true;break;}
                try {
                    foreach(var path in Directory.EnumerateFiles(folder)) {cancellation.ThrowIfCancellationRequested();if(EntryRules.Importable(path)&&(!launchOnly||StartupEntryRules.Candidate(path)))result.Paths.Add(path);if(result.Paths.Count>=fileLimit){result.Limited=true;break;}}
                    if(recurse)foreach(var path in Directory.EnumerateDirectories(folder)) {cancellation.ThrowIfCancellationRequested();try{if((File.GetAttributes(path)&FileAttributes.ReparsePoint)==0)pending.Push(path);}catch{result.Skipped++;}}
                }catch(OperationCanceledException){throw;}catch{result.Skipped++;}
            }return result;
        }
    }
[DataContract] public class ShellState {
        [DataMember] public long Handle;
        [DataMember] public string ClassName;
        [DataMember] public bool Visible;
    }
[DataContract] public class Workspace {
        [DataMember] public string Name;
        [DataMember] public List<string> Paths=new List<string>();
        public void Normalize(){Name=(Name??"").Trim();Paths=(Paths??new List<string>()).Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();}
    }
[DataContract] public class PlacementOptions {
        [DataMember] public double? DockLeft,DockTop,PanelLeft,PanelTop,PanelWidth,PanelHeight,WidgetLeft,WidgetTop;
        [DataMember] public bool DockLocked=true,PanelLocked=false;
        [OnDeserializing] void Defaults(StreamingContext context){DockLocked=true;PanelLocked=false;}
        public PlacementOptions Copy(){return (PlacementOptions)MemberwiseClone();}
        public void Normalize(){DockLeft=Valid(DockLeft);DockTop=Valid(DockTop);PanelLeft=Valid(PanelLeft);PanelTop=Valid(PanelTop);WidgetLeft=Valid(WidgetLeft);WidgetTop=Valid(WidgetTop);PanelWidth=Valid(PanelWidth);PanelHeight=Valid(PanelHeight);if(PanelWidth<320)PanelWidth=null;if(PanelHeight<320)PanelHeight=null;}
        static double? Valid(double? x){return x.HasValue&&(double.IsNaN(x.Value)||double.IsInfinity(x.Value))?null:x;}
    }
[DataContract] public class WidgetOptions {
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
        [DataMember] public double Volume=.6,OpacityPercent=72;
        [OnDeserializing] void Defaults(StreamingContext context){AutoNativeMediaCompatibility=true;City="";Volume=.6;OpacityPercent=72;MusicMode="system";Playlist=new List<string>();Panels=new List<WidgetPanelPosition>();}
        public void Normalize(){Alarms=(Alarms??new List<ClockAlarm>()).Where(a=>a!=null).Take(50).ToList();foreach(var a in Alarms)a.Normalize();TimerPausedSeconds=LayoutRules.Clamp(TimerPausedSeconds,0,86400,0);City=(City??"").Trim();if(City.Length>100)City=City.Substring(0,100);Volume=LayoutRules.Clamp(Volume,0,1,.6);OpacityPercent=LayoutRules.Clamp(OpacityPercent,0,100,72);ClockPanel=Panel(ClockPanel);WeatherPanel=Panel(WeatherPanel);MusicPanel=Panel(MusicPanel);NotesPanel=Panel(NotesPanel);MusicMode=MusicMode=="local"?"local":"system";Playlist=(Playlist??new List<string>()).Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();TrackIndex=Playlist.Count==0?0:Math.Max(0,Math.Min(Playlist.Count-1,TrackIndex));RepeatMode=Math.Max(0,Math.Min(2,RepeatMode));Panels=(Panels??new List<WidgetPanelPosition>()).Where(p=>p!=null&&p.Id>=0&&p.Id<4).GroupBy(p=>p.Id).Select(g=>g.First()).ToList();foreach(var p in Panels)p.Normalize();}
        static int Panel(int value){return Math.Max(0,Math.Min(3,value));}
        public WidgetOptions Copy(){var c=(WidgetOptions)MemberwiseClone();c.Alarms=(Alarms??new List<ClockAlarm>()).Select(a=>a.Copy()).ToList();c.Playlist=new List<string>(Playlist??new List<string>());c.Panels=(Panels??new List<WidgetPanelPosition>()).Where(p=>p!=null).Select(p=>p.Copy()).ToList();return c;}
        public WidgetPanelPosition Position(int id){var p=Panels.FirstOrDefault(x=>x.Id==id);if(p==null){p=new WidgetPanelPosition{Id=id};Panels.Add(p);}return p;}
        public void Separate(){ClockPanel=0;WeatherPanel=1;MusicPanel=2;NotesPanel=3;}
        public void Fuse(){ClockPanel=WeatherPanel=MusicPanel=NotesPanel=0;}
    }
[DataContract] public class WidgetPanelPosition {
        [DataMember] public int Id;
        [DataMember] public double? Left,Top,Width,Height,OpacityPercent;
        public void Normalize(){Width=WidgetSizing.Dimension(Width,360,1600);Height=WidgetSizing.Dimension(Height,200,1600);if(OpacityPercent.HasValue)OpacityPercent=LayoutRules.Clamp(OpacityPercent.Value,0,100,72);if(Left.HasValue&&(double.IsNaN(Left.Value)||double.IsInfinity(Left.Value)))Left=null;if(Top.HasValue&&(double.IsNaN(Top.Value)||double.IsInfinity(Top.Value)))Top=null;}
        public WidgetPanelPosition Copy(){return (WidgetPanelPosition)MemberwiseClone();}
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
public sealed class WidgetRegistry<T> where T:class {
        readonly Dictionary<int,T> items=new Dictionary<int,T>();
        public List<T> Values{get{return items.Values.ToList();}}
        public List<int> Keys{get{return items.Keys.ToList();}}
        public bool TryGetValue(int id,out T value){return items.TryGetValue(id,out value);}
        public void Add(int id,T value){if(value==null)throw new ArgumentNullException("value");items.Add(id,value);}
        public bool Remove(int id,T expected){T current;if(!items.TryGetValue(id,out current)||!object.ReferenceEquals(current,expected))return false;return items.Remove(id);}
    }
public static class WidgetSizing {
        public static double? Dimension(double? value,double min,double max){if(!value.HasValue||double.IsNaN(value.Value)||double.IsInfinity(value.Value))return null;return Math.Max(min,Math.Min(max,value.Value));}
        public static double MinimumHeight(double natural,double available){return Math.Min(available,Math.Max(200,natural*.8+28));}
        public static double Background(WidgetOptions options,WidgetPanelPosition panel){return LayoutRules.Clamp(panel.OpacityPercent??options.OpacityPercent,0,100,72);}
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
public static class StartupEntryRules {
        public static string Leaf(string path){path=(path??"").Replace('\\','/');int slash=path.LastIndexOf('/');return slash>=0?path.Substring(slash+1):path;}
        public static string Stem(string path){string leaf=Leaf(path);int dot=leaf.LastIndexOf('.');return dot>0?leaf.Substring(0,dot):leaf;}
        public static bool Auxiliary(string path){string name=AutoGroupRules.Canonical(Stem(path));if(name.StartsWith("unins")||name.StartsWith("uninstall")||name.Contains("卸载")||name.EndsWith("updater")||name.Contains("crashreport")||name.Contains("crashpad"))return true;string[] helpers={"update","updater","autoupdate","setup","install","installer","crashreporter","crashreportclient","crashpadhandler","cefsubprocess","qtwebengineprocess","unitycrashhandler64","unitycrashhandler32","werfault","vc_redist","vcredist","dxsetup","dxwebsetup","dxdiag","readme","license","help","说明","帮助","repair","修复","reportbug","feedback"};return helpers.Any(x=>name==AutoGroupRules.Canonical(x)||name.StartsWith(AutoGroupRules.Canonical(x)+"64")||name.StartsWith(AutoGroupRules.Canonical(x)+"32"));}
        public static bool GameUri(string value){value=(value??"").Trim().ToLowerInvariant();return value.StartsWith("steam://rungameid/")||value.StartsWith("steam://run/")||value.StartsWith("com.epicgames.launcher://apps/")||value.StartsWith("uplay://launch/")||value.StartsWith("origin://launchgame/")||value.StartsWith("battlenet://");}
        public static bool Candidate(string path){if(!EntryRules.Importable(path)||Auxiliary(path))return false;string ext=System.IO.Path.GetExtension(path).ToLowerInvariant();return ext!=".url"||GameUri(ReadUrl(path));}
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
public static class BoardIconRules {
        public static int Columns(double width,double icon,double gap){return Math.Max(1,Math.Min(30,(int)(Math.Max(1,width)/(Math.Max(70,icon+18)+gap*2))));}
        public static int Rows(double height,double icon,double gap){return Math.Max(1,Math.Min(20,(int)(Math.Max(1,height)/(icon+40+gap*2))));}
    }
public static class MediaFallback {
        public static MediaSnapshot Merge(MediaSnapshot standard,MediaSnapshot extra){if(standard==null)return new MediaSnapshot();var result=standard.Copy();if(!standard.Available||extra==null||!extra.Available||string.IsNullOrEmpty(standard.Key)||standard.Key!=extra.Key||standard.Source!=extra.Source)return result;if(string.IsNullOrEmpty(result.Cover)&&!string.IsNullOrEmpty(extra.Cover)){result.Cover=extra.Cover;result.CoverStatus=extra.CoverStatus;}if(extra.Duration>0&&!double.IsNaN(extra.Duration)&&!double.IsInfinity(extra.Duration)&&(result.Duration<=0||(!result.CanSeek&&extra.CanSeek))){result.Duration=extra.Duration;result.Position=extra.Position;result.MinSeek=extra.MinSeek;result.MaxSeek=extra.MaxSeek;result.Age=extra.Age;result.Rate=extra.Rate;result.CanSeek=extra.CanSeek;result.TimelineStatus=extra.TimelineStatus;result.Backend=extra.Backend=="Netease UIA"?extra.Backend:"NPSMLib isolated";}return result;}
    }
public static class MediaWireProtocol {
        public static void Write(Stream stream,object value){using(var buffer=new MemoryStream()){new DataContractJsonSerializer(value.GetType()).WriteObject(buffer,value);byte[] bytes=buffer.ToArray();stream.Write(bytes,0,bytes.Length);stream.WriteByte(10);stream.Flush();}}
        public static MediaRequest ReadRequest(string line){using(var buffer=new MemoryStream(Encoding.UTF8.GetBytes(line)))return (MediaRequest)new DataContractJsonSerializer(typeof(MediaRequest)).ReadObject(buffer);}
        public static void Loop(TextReader input,Stream output,Func<MediaRequest,MediaSnapshot> read){string line;while((line=input.ReadLine())!=null){MediaRequest request=null;MediaSnapshot result;try{request=ReadRequest(line);if(request.Command=="exit")break;result=read(request);}catch(Exception ex){result=new MediaSnapshot{Key=request==null?null:request.Key,Source=request==null?null:request.Source,Backend="Netease UIA",Status=ex.Message};}Write(output,result);}}
    }
public static class NeteaseTimelineRules {
        public static bool Time(string text,out double seconds){seconds=0;var p=text.Trim().Split(':');if(p.Length<2||p.Length>3)return false;int[] v=new int[p.Length];for(int i=0;i<p.Length;i++)if(!int.TryParse(p[i],out v[i])||v[i]<0)return false;if(v[p.Length-1]>59||(p.Length==3&&v[1]>59))return false;seconds=p.Length==3?v[0]*3600.0+v[1]*60+v[2]:v[0]*60.0+v[1];return seconds<=359999;}
        public static bool Progress(string text,out double current,out double total){current=total=0;if(string.IsNullOrWhiteSpace(text))return false;string clean=text.Normalize(NormalizationForm.FormKC).Replace(" ","");var parts=clean.Split(new[]{'/','|'});return parts.Length==2&&Time(parts[0],out current)&&Time(parts[1],out total)&&total>0&&current<=total;}
        public static bool Source(string source){return (source??"").IndexOf("cloudmusic",StringComparison.OrdinalIgnoreCase)>=0;}
        public static bool WindowTitle(string name,string track){return !string.IsNullOrWhiteSpace(track)&&!string.IsNullOrWhiteSpace(name)&&name.IndexOf("MediaPlayer",StringComparison.OrdinalIgnoreCase)<0&&(name.Trim().Equals(track.Trim(),StringComparison.OrdinalIgnoreCase)||new[]{" - "," – "," — "}.Any(separator=>name.Trim().StartsWith(track.Trim()+separator,StringComparison.OrdinalIgnoreCase)));}
        public static double RangeTarget(double seconds,double duration,double min,double max){if(duration<=0||max<=min||double.IsNaN(seconds)||double.IsInfinity(seconds))throw new ArgumentException("Invalid playback range");return min+Math.Max(0,Math.Min(1,seconds/duration))*(max-min);}
    }
public static class StartupRules {
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
public static class NoteFiles {
        public static NoteBook Read(string path,Action<Exception> log=null){
            foreach(string file in new string[]{path,path+".bak"})try{if(File.Exists(file))using(var f=File.OpenRead(file)){var book=(NoteBook)new DataContractJsonSerializer(typeof(NoteBook)).ReadObject(f);if(book==null)throw new InvalidDataException("备忘录文件为空");book.Normalize();return book;}}catch(Exception ex){if(log!=null)log(ex);try{File.Copy(file,file+".damaged-"+DateTime.Now.ToString("yyyyMMddHHmmssfff"),false);}catch{}}
            var fresh=new NoteBook();fresh.Normalize();return fresh;
        }
        public static void Write(string path,NoteBook book){book.Normalize();Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)));string temp=path+".tmp";using(var f=File.Create(temp))new DataContractJsonSerializer(typeof(NoteBook)).WriteObject(f,book);if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}
    }
[DataContract] public class AppearanceScheme {
        [DataMember] public int Version=1;
        [DataMember] public string Name;
        [DataMember] public bool Light;
        [DataMember] public int PanelSize;
        [DataMember] public double BoardIconSize=48,BoardIconGap=5;
        [OnDeserializing] void Defaults(StreamingContext context){BoardIconSize=48;BoardIconGap=5;}
        [DataMember] public double OpacityPercent=72,DockGap=12;
        [DataMember] public DockOptions Dock;
        [DataMember] public PlacementOptions Placement;
        [DataMember] public WidgetOptions Widgets;
        [DataMember] public List<Entry> Apps;
        [DataMember] public List<string> Groups;
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
        public static void RenameGroup(Preferences p,string before,string after){after=(after??"").Trim();if(after.Length==0||after=="全部"||after=="未分组")throw new ArgumentException("请输入有效分组名称");if(p.Groups.Any(g=>g.Equals(after,StringComparison.CurrentCultureIgnoreCase)&&!g.Equals(before,StringComparison.CurrentCultureIgnoreCase)))throw new ArgumentException("分组名称重复");foreach(string key in p.LearnedGroups.Keys.ToList())if(p.LearnedGroups[key]==before)p.LearnedGroups[key]=after;foreach(var a in p.Apps){if(GroupName(a)==before)a.Group=after;if(a.ExtraGroups!=null)a.ExtraGroups=a.ExtraGroups.Select(g=>g==before?after:g).ToList();NormalizeMemberships(a);}p.Groups=p.Groups.Select(g=>g==before?after:g).ToList();p.Groups=Groups(p.Groups,p.Apps);}
        public static void DeleteGroup(Preferences p,string group){foreach(string key in p.LearnedGroups.Keys.Where(k=>p.LearnedGroups[k]==group).ToList())p.LearnedGroups.Remove(key);foreach(var a in p.Apps)RemoveMembership(a,group);p.Groups.RemoveAll(g=>g==group);}
        public static Entry Copy(Entry a){return new Entry{Name=a.Name,Path=a.Path,Pinned=a.Pinned,IsFolder=a.IsFolder,Group=a.Group,ExtraGroups=a.ExtraGroups==null?null:a.ExtraGroups.ToList(),IconPath=a.IconPath,LaunchTarget=a.LaunchTarget,LaunchArguments=a.LaunchArguments,ProductInfo=a.ProductInfo};}
        public static AppearanceScheme Capture(Preferences p,string name){return new AppearanceScheme{Name=name,Light=p.Light,PanelSize=p.PanelSize,BoardIconSize=p.BoardIconSize,BoardIconGap=p.BoardIconGap,OpacityPercent=p.OpacityPercent??72,DockGap=p.DockGap,Dock=p.Dock.Copy(),Placement=p.Placement.Copy(),Widgets=p.Widgets.Copy(),Apps=p.Apps.Select(Copy).ToList(),Groups=p.Groups.ToList()};}
        public static void Apply(Preferences p,AppearanceScheme scheme){
            if(scheme==null||scheme.Version!=1||scheme.Dock==null)throw new ArgumentException("方案格式或版本不支持");
            var layout=EntryRules.NormalizeEntries((scheme.Apps??new List<Entry>()).Where(a=>a!=null&&!string.IsNullOrWhiteSpace(a.Path)).Select(Copy));
            var known=new HashSet<string>(layout.Select(a=>a.Path),StringComparer.OrdinalIgnoreCase);layout.AddRange(p.Apps.Where(a=>!known.Contains(a.Path)));p.Apps=layout;p.Removed.RemoveAll(known.Contains);
            p.Light=scheme.Light;p.PanelSize=scheme.PanelSize;p.BoardIconSize=scheme.BoardIconSize;p.BoardIconGap=scheme.BoardIconGap;p.OpacityPercent=scheme.OpacityPercent;p.DockGap=scheme.DockGap;p.Dock=scheme.Dock.Copy();p.Placement=(scheme.Placement??new PlacementOptions()).Copy();p.Widgets=(scheme.Widgets??new WidgetOptions()).Copy();p.Groups=(scheme.Groups??new List<string>()).ToList();p.Normalize();
        }
        public static uint AutoHideState(uint original,bool enabled){return enabled?original|1u:original&~1u;}
        public static double Position(double desired,double origin,double extent,double length){return Math.Max(origin,Math.Min(origin+Math.Max(0,extent-length),desired));}
        public static string WeatherLabel(int code){if(code==0)return "晴";if(code>=1&&code<=3)return "多云";if(code==45||code==48)return "雾";if(code>=51&&code<=67)return "雨";if(code>=71&&code<=77)return "雪";if(code>=80&&code<=82)return "阵雨";if(code==85||code==86)return "阵雪";if(code==95||code==96||code==99)return "雷雨";return "天气代码 "+code;}
    }
public class AutoHidePolicy {
        public bool Hidden;DateTime lastSeen=DateTime.MinValue;
        public bool Update(DateTime now,bool enabled,bool over,bool edge,bool blocked,double delay){if(!enabled||over||edge||blocked){lastSeen=now;Hidden=false;}else if(lastSeen==DateTime.MinValue)lastSeen=now;else if((now-lastSeen).TotalMilliseconds>=delay)Hidden=true;return Hidden;}
    }
[DataContract] public class GeoPlace{[DataMember(Name="name")]public string Name;[DataMember(Name="country")]public string Country;[DataMember(Name="latitude")]public double Latitude;[DataMember(Name="longitude")]public double Longitude;}
[DataContract] public class GeoReply{[DataMember(Name="results")]public List<GeoPlace> Results;}
[DataContract] public class WeatherCurrent{[DataMember(Name="temperature_2m")]public double Temperature;[DataMember(Name="weather_code")]public int Code;[DataMember(Name="wind_speed_10m")]public double Wind;[DataMember(Name="time")]public string Time;}
[DataContract] public class WeatherReply{[DataMember(Name="current")]public WeatherCurrent Current;}
public class TrackMetadata {
        public string Title,Artist,Album;public byte[] Cover;
        public static TrackMetadata Read(string path){var result=new TrackMetadata{Title=System.IO.Path.GetFileNameWithoutExtension(path),Artist="本地音乐"};try{
            using(var f=File.OpenRead(path)){byte[] header=new byte[10];if(f.Read(header,0,10)==10&&Encoding.ASCII.GetString(header,0,3)=="ID3"&&(header[3]==3||header[3]==4)&&(header[5]&0xC0)==0){int size=Sync(header,6);if(size>0&&size<=8*1024*1024){byte[] data=new byte[size];int used=0;while(used<size){int n=f.Read(data,used,size-used);if(n==0)break;used+=n;}int at=0;while(at+10<=used){string id=Encoding.ASCII.GetString(data,at,4);int count=header[3]==4?Sync(data,at+4):Big(data,at+4);if(count<=0||count>used-at-10)break;int start=at+10;bool plain=(data[at+8]==0&&data[at+9]==0);if(plain&&(id=="TIT2"||id=="TPE1"||id=="TALB")){string text=Text(data,start,count);if(!string.IsNullOrWhiteSpace(text)){if(id=="TIT2")result.Title=text;else if(id=="TPE1")result.Artist=text;else result.Album=text;}}else if(plain&&id=="APIC"&&result.Cover==null){int encoding=data[start],p=start+1,end=start+count;while(p<end&&data[p]!=0)p++;p+=2;if(encoding==1||encoding==2){while(p+1<end&&(data[p]!=0||data[p+1]!=0))p+=2;p+=2;}else{while(p<end&&data[p]!=0)p++;p++;}if(p<end&&end-p<=4*1024*1024){result.Cover=new byte[end-p];Buffer.BlockCopy(data,p,result.Cover,0,end-p);}}at=start+count;}}}}
            if(result.Cover==null){string folder=System.IO.Path.GetDirectoryName(path);foreach(string name in new string[]{System.IO.Path.GetFileNameWithoutExtension(path)+".jpg",System.IO.Path.GetFileNameWithoutExtension(path)+".png","cover.jpg","cover.png","folder.jpg"}){string cover=System.IO.Path.Combine(folder,name);if(File.Exists(cover)&&new FileInfo(cover).Length<=4*1024*1024){result.Cover=File.ReadAllBytes(cover);break;}}}
        }catch{}return result;}
        static int Sync(byte[] b,int p){return ((b[p]&127)<<21)|((b[p+1]&127)<<14)|((b[p+2]&127)<<7)|(b[p+3]&127);}
        static int Big(byte[] b,int p){return (b[p]<<24)|(b[p+1]<<16)|(b[p+2]<<8)|b[p+3];}
        static string Text(byte[] b,int p,int count){if(count<2)return "";var encoding=b[p]==1?Encoding.Unicode:b[p]==2?Encoding.BigEndianUnicode:b[p]==3?Encoding.UTF8:Encoding.GetEncoding(28591);return encoding.GetString(b,p+1,count-1).Trim('\0','\uFEFF',' ','\r','\n');}
    }
[DataContract] public class MediaSnapshot {
        [DataMember] public bool Available,Playing,CanSeek,CanPrevious,CanNext,CanToggle;
        [DataMember] public string Status,Title,Artist,Album,Source,Key,Cover,Backend,CoverStatus,MetadataStatus,TimelineStatus;
        [DataMember] public double Position,Duration,Age,MinSeek,MaxSeek;
        [DataMember] public double Rate=1;
        [OnDeserializing] void Defaults(StreamingContext context){Rate=1;}
        public MediaSnapshot Copy(){return (MediaSnapshot)MemberwiseClone();}
    }
[DataContract] public class MediaRequest {[DataMember]public string Command;[DataMember]public double Seconds;[DataMember]public string Key;[DataMember]public string Source,Title,Artist,Album;[DataMember]public bool Playing,UseNative,CoverOnly;}
[DataContract] public class BoardEditState {
        [DataMember] public List<Entry> Apps;
        [DataMember] public List<string> Groups,Removed;
        [DataMember] public Dictionary<string,string> Learned;
        public static BoardEditState Capture(Preferences p){return new BoardEditState{Apps=p.Apps.Select(FeatureRules.Copy).ToList(),Groups=p.Groups.ToList(),Removed=p.Removed.ToList(),Learned=new Dictionary<string,string>(p.LearnedGroups)};}
        public void Apply(Preferences p){p.Apps=Apps.Select(FeatureRules.Copy).ToList();p.Groups=Groups.ToList();p.Removed=Removed.ToList();p.LearnedGroups=new Dictionary<string,string>(Learned);p.Normalize();}
        public string Signature(){using(var stream=new MemoryStream()){new DataContractJsonSerializer(typeof(BoardEditState)).WriteObject(stream,this);return Encoding.UTF8.GetString(stream.ToArray());}}
    }
public sealed class BoardEditHistory {
        sealed class Item {public BoardEditState State;public string Label;}
        readonly List<Item> undo=new List<Item>(),redo=new List<Item>();const int Limit=40;
        public bool CanUndo{get{return undo.Count>0;}}public bool CanRedo{get{return redo.Count>0;}}
        public string UndoLabel{get{return CanUndo?undo[undo.Count-1].Label:"";}}public string RedoLabel{get{return CanRedo?redo[redo.Count-1].Label:"";}}
        public void Clear(){undo.Clear();redo.Clear();}
        public bool Remember(BoardEditState before,Preferences after,string label){if(before.Signature()==BoardEditState.Capture(after).Signature())return false;undo.Add(new Item{State=before,Label=label});if(undo.Count>Limit)undo.RemoveAt(0);redo.Clear();return true;}
        public string Undo(Preferences p){if(!CanUndo)return null;var item=undo[undo.Count-1];undo.RemoveAt(undo.Count-1);redo.Add(new Item{State=BoardEditState.Capture(p),Label=item.Label});item.State.Apply(p);return item.Label;}
        public string Redo(Preferences p){if(!CanRedo)return null;var item=redo[redo.Count-1];redo.RemoveAt(redo.Count-1);undo.Add(new Item{State=BoardEditState.Capture(p),Label=item.Label});item.State.Apply(p);return item.Label;}
    }
public sealed class TrackArtworkCache<T> where T:class {
        sealed class Item {public T Value;public DateTime At;}
        readonly Dictionary<string,Item> items=new Dictionary<string,Item>(StringComparer.Ordinal);readonly int limit;
        public TrackArtworkCache(int capacity=8){limit=Math.Max(1,capacity);}
        public void Put(string key,T value,DateTime now){if(string.IsNullOrEmpty(key)||value==null)return;items[key]=new Item{Value=value,At=now};while(items.Count>limit)items.Remove(items.OrderBy(p=>p.Value.At).First().Key);}
        public T Get(string key,DateTime now){Item item;if(string.IsNullOrEmpty(key)||!items.TryGetValue(key,out item))return null;if((now-item.At).TotalMinutes>15){items.Remove(key);return null;}return item.Value;}
        public void Clear(){items.Clear();}
    }
public static class MusicMetadataRecovery {
        public static MediaSnapshot Recover(MediaSnapshot previous,MediaSnapshot current){if(current==null)return new MediaSnapshot();if(previous==null||!previous.Available||!current.Available||previous.Source!=current.Source||string.IsNullOrEmpty(previous.Key)||string.IsNullOrEmpty(current.MetadataStatus)||current.MetadataStatus=="OK")return current;var copy=current.Copy();copy.Title=previous.Title;copy.Artist=previous.Artist;copy.Album=previous.Album;copy.Key=previous.Key;copy.Cover=previous.Cover;copy.CoverStatus=previous.CoverStatus;return copy;}
    }
public static class BackupFiles {
        const int MaxFile=8*1024*1024,MaxTotal=32*1024*1024;
        static bool Allowed(string name){if(name=="layout.json"||name=="notes.json"||name=="manifest.json")return true;bool scheme=name.StartsWith("schemes/",StringComparison.Ordinal)&&name.EndsWith(".json",StringComparison.OrdinalIgnoreCase);bool icon=name.StartsWith("icons/",StringComparison.Ordinal)&&new[]{".png",".jpg",".jpeg",".ico",".bmp"}.Contains(Path.GetExtension(name).ToLowerInvariant());if(!scheme&&!icon)return false;string file=name.Substring(scheme?8:6);return file.Length>5&&!file.Contains("..")&&file.IndexOfAny(new[]{'\\','/',':','*','?','"','<','>','|','\0'})<0;}
        static T Json<T>(byte[] bytes){using(var stream=new MemoryStream(bytes))return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);}
        static byte[] Encode<T>(T value){using(var stream=new MemoryStream()){new DataContractJsonSerializer(typeof(T)).WriteObject(stream,value);return stream.ToArray();}}
        public static Dictionary<string,byte[]> Read(string path){var result=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);int total=0;using(var file=File.OpenRead(path))using(var archive=new ZipArchive(file,ZipArchiveMode.Read)){if(archive.Entries.Count>4096)throw new InvalidDataException("备份项目过多");foreach(var entry in archive.Entries){if(!Allowed(entry.FullName)||result.ContainsKey(entry.FullName)||entry.Length>MaxFile)throw new InvalidDataException("备份中含有无效项目");using(var source=entry.Open())using(var buffer=new MemoryStream()){byte[] block=new byte[8192];int count;while((count=source.Read(block,0,block.Length))>0){total+=count;if(buffer.Length+count>MaxFile||total>MaxTotal)throw new InvalidDataException("备份数据过大");buffer.Write(block,0,count);}result.Add(entry.FullName,buffer.ToArray());}}}Validate(result);return result;}
        static void Validate(Dictionary<string,byte[]> data){if(!data.ContainsKey("manifest.json")||!data.ContainsKey("layout.json")||!data.ContainsKey("notes.json"))throw new InvalidDataException("不是完整的 Lumina 备份");var manifest=Json<BackupManifest>(data["manifest.json"]);if(manifest==null||manifest.Version!=1)throw new InvalidDataException("不支持的备份版本");var prefs=Json<Preferences>(data["layout.json"]);var notes=Json<NoteBook>(data["notes.json"]);if(prefs==null||notes==null)throw new InvalidDataException("备份配置为空");prefs.Normalize();notes.Normalize();if(manifest.Icons!=null)foreach(var item in manifest.Icons)if(!item.Value.StartsWith("icons/",StringComparison.Ordinal)||!Allowed(item.Value)||!data.ContainsKey(item.Value))throw new InvalidDataException("图标备份引用无效");foreach(var item in data.Where(x=>x.Key.StartsWith("schemes/"))){var scheme=Json<AppearanceScheme>(item.Value);if(scheme==null||scheme.Version!=1||scheme.Dock==null)throw new InvalidDataException("外观方案无效");}}
        public static void Create(string folder,string destination){var data=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);data["layout.json"]=File.ReadAllBytes(Path.Combine(folder,"layout.json"));string notes=Path.Combine(folder,"notes.json");if(File.Exists(notes))data["notes.json"]=File.ReadAllBytes(notes);else{var book=new NoteBook();book.Normalize();data["notes.json"]=Encode(book);}string schemes=Path.Combine(folder,"schemes");if(Directory.Exists(schemes))foreach(var file in Directory.GetFiles(schemes,"*.json")){string name="schemes/"+Path.GetFileName(file);if(!Allowed(name))throw new InvalidDataException("外观方案文件名无效");data[name]=File.ReadAllBytes(file);}var iconMap=new Dictionary<string,string>();var prefs=Json<Preferences>(data["layout.json"]);var iconEntries=(prefs.Apps??new List<Entry>()).Concat(data.Where(item=>item.Key.StartsWith("schemes/")).SelectMany(item=>Json<AppearanceScheme>(item.Value).Apps??new List<Entry>()));foreach(var app in iconEntries.Where(a=>a!=null&&!string.IsNullOrWhiteSpace(a.IconPath)&&File.Exists(a.IconPath))){string ext=Path.GetExtension(app.IconPath).ToLowerInvariant();if(!new[]{".png",".jpg",".jpeg",".ico",".bmp"}.Contains(ext))continue;var bytes=File.ReadAllBytes(app.IconPath);string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();string name="icons/"+hash+ext;data[name]=bytes;iconMap[app.IconPath]=name;}data["manifest.json"]=Encode(new BackupManifest{Version=1,AppVersion="0.9.3",CreatedUtc=DateTime.UtcNow.ToString("o"),Icons=iconMap});if(data.Count>4096||data.Values.Any(b=>b.Length>MaxFile)||data.Values.Sum(b=>(long)b.Length)>MaxTotal)throw new InvalidDataException("备份数据过大");Validate(data);string temp=destination+".tmp-"+Guid.NewGuid().ToString("N");try{using(var file=File.Create(temp))using(var archive=new ZipArchive(file,ZipArchiveMode.Create))foreach(var item in data){var entry=archive.CreateEntry(item.Key,CompressionLevel.Optimal);using(var output=entry.Open())output.Write(item.Value,0,item.Value.Length);}if(File.Exists(destination))File.Replace(temp,destination,null);else File.Move(temp,destination);}finally{if(File.Exists(temp))File.Delete(temp);}}
        static void Atomic(string path,byte[] bytes){Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".restore-"+Guid.NewGuid().ToString("N");try{File.WriteAllBytes(temp,bytes);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
        public static void Restore(string backup,string folder,Action<string> afterCommit=null){var data=Read(backup);var manifest=Json<BackupManifest>(data["manifest.json"]);if(manifest.Icons!=null&&manifest.Icons.Count>0){var prefs=Json<Preferences>(data["layout.json"]);var iconEntries=(prefs.Apps??new List<Entry>()).Concat(data.Where(item=>item.Key.StartsWith("schemes/")).SelectMany(item=>Json<AppearanceScheme>(item.Value).Apps??new List<Entry>()));foreach(var app in iconEntries.Where(a=>a!=null&&!string.IsNullOrEmpty(a.IconPath))){string name;if(manifest.Icons.TryGetValue(app.IconPath,out name))app.IconPath=Path.GetFullPath(Path.Combine(folder,name.Replace('/',Path.DirectorySeparatorChar)));}data["layout.json"]=Encode(prefs);foreach(var key in data.Keys.Where(k=>k.StartsWith("schemes/",StringComparison.Ordinal)).ToArray()){var scheme=Json<AppearanceScheme>(data[key]);foreach(var app in (scheme.Apps??new List<Entry>()).Where(a=>a!=null&&!string.IsNullOrEmpty(a.IconPath))){string name;if(manifest.Icons.TryGetValue(app.IconPath,out name))app.IconPath=Path.GetFullPath(Path.Combine(folder,name.Replace('/',Path.DirectorySeparatorChar)));}data[key]=Encode(scheme);}}Directory.CreateDirectory(folder);var targets=data.Keys.Where(k=>k!="manifest.json").ToList();string schemes=Path.Combine(folder,"schemes");if(Directory.Exists(schemes))targets=targets.Concat(Directory.GetFiles(schemes,"*.json").Select(f=>"schemes/"+Path.GetFileName(f))).Distinct(StringComparer.OrdinalIgnoreCase).ToList();foreach(var name in targets){if(!Allowed(name))throw new InvalidDataException("配置目录包含无效文件名");if(Directory.Exists(Path.Combine(folder,name.Replace('/',Path.DirectorySeparatorChar))))throw new IOException("恢复目标被文件夹占用："+name);}string stage=Path.Combine(folder,".restore-"+Guid.NewGuid().ToString("N"));bool cleanup=true;var changed=new List<string>();var hadOriginal=new HashSet<string>(StringComparer.OrdinalIgnoreCase);try{
            foreach(var item in data.Where(x=>x.Key!="manifest.json")){string path=Path.Combine(stage,"new",item.Key.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,item.Value);}
            try{foreach(var name in targets){string relative=name.Replace('/',Path.DirectorySeparatorChar);string target=Path.Combine(folder,relative),oldPath=Path.Combine(stage,"old",relative);Directory.CreateDirectory(Path.GetDirectoryName(target));if(File.Exists(target)){Directory.CreateDirectory(Path.GetDirectoryName(oldPath));File.Move(target,oldPath);hadOriginal.Add(name);}changed.Add(name);if(data.ContainsKey(name))File.Move(Path.Combine(stage,"new",relative),target);if(afterCommit!=null)afterCommit(name);}}
            catch{Exception rollbackError=null;foreach(var name in changed.AsEnumerable().Reverse())try{string relative=name.Replace('/',Path.DirectorySeparatorChar),target=Path.Combine(folder,relative);if(File.Exists(target))File.Delete(target);if(hadOriginal.Contains(name))File.Move(Path.Combine(stage,"old",relative),target);}catch(Exception ex){if(rollbackError==null)rollbackError=ex;}if(rollbackError!=null){cleanup=false;throw new IOException("恢复回滚未完成，原始数据仍保存在："+stage,rollbackError);}throw;}
        }finally{if(cleanup&&Directory.Exists(stage))try{Directory.Delete(stage,true);}catch{}}}

    }
[DataContract] public class BackupManifest {[DataMember]public int Version;[DataMember]public string AppVersion,CreatedUtc;[DataMember]public Dictionary<string,string> Icons;}
public static class EntryPathRules {
        public static string Normalize(string path){
            if(string.IsNullOrWhiteSpace(path)||!Directory.Exists(path))return path;
            string full=System.IO.Path.GetFullPath(path),root=System.IO.Path.GetPathRoot(full);
            return string.Equals(full,root,StringComparison.OrdinalIgnoreCase)?root:full.TrimEnd(System.IO.Path.DirectorySeparatorChar,System.IO.Path.AltDirectorySeparatorChar);
        }
        public static string Name(string path){
            if(!Directory.Exists(path))return System.IO.Path.GetFileNameWithoutExtension(path);
            var folder=new DirectoryInfo(Normalize(path));return string.IsNullOrEmpty(folder.Name)?folder.FullName:folder.Name;
        }
    }
public static class Check {
 static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
 public static int Main(){
  Require(BoardIconRules.Columns(892,28,2)*BoardIconRules.Rows(354,28,2)>BoardIconRules.Columns(892,80,20)*BoardIconRules.Rows(354,80,20),"smaller icons increase page capacity");
  Require(BoardIconRules.Columns(0,48,5)==1&&BoardIconRules.Rows(0,48,5)==1,"minimum board grid");
  var note=new NoteItem{Title="旧备忘录"};int changed=0;note.PropertyChanged+=(o,e)=>{if(e.PropertyName=="DisplayTitle")changed++;};note.Title="修改后的标题";
  Require(changed==1&&System.ComponentModel.TypeDescriptor.GetProperties(note)["DisplayTitle"].GetValue(note).ToString()=="修改后的标题","note title binding and notification");
  using(var stream=new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"Title\":\"历史便签\",\"Body\":\"内容\"}"))){var restored=(NoteItem)new DataContractJsonSerializer(typeof(NoteItem)).ReadObject(stream);Require(restored.Title=="历史便签"&&restored.Body=="内容","legacy note title field migration");}
  var importPrefs=new Preferences{AutoGrouping=true};importPrefs.Groups.Add("游戏");var imported=new Entry{Name="Microsoft Word",Path="winword.exe"};Require(FeatureRules.ImportGroup(importPrefs,imported,"游戏")=="游戏","current group takes precedence over automatic classification");Require(FeatureRules.ImportGroup(importPrefs,imported,"未分组")==null,"explicit ungrouped import");Require(FeatureRules.ImportGroup(importPrefs,imported,"全部")==AutoGroupRules.Guess(imported,importPrefs),"all view uses automatic grouping");
  importPrefs.Apps.Add(new Entry{Name="A",Path="a.exe"});importPrefs.Apps.Add(new Entry{Name="B",Path="b.exe"});Require(FeatureRules.Move(importPrefs,new[]{"a.exe","b.exe"},"游戏")==2&&importPrefs.Apps.All(a=>a.Group=="游戏"),"drag selection moves as a batch");FeatureRules.Move(importPrefs,new[]{"a.exe"},"未分组");Require(FeatureRules.GroupName(importPrefs.Apps[0])=="未分组","drag back to ungrouped");
  var vendorParser=typeof(NeteaseMusicHelper).GetMethod("TryParseProgressText",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);object[] vendorArgs={"01:23 / 04:56",0,0};Require((bool)vendorParser.Invoke(null,vendorArgs)&&(int)vendorArgs[1]==83&&(int)vendorArgs[2]==296,"bundled upstream COM helper parser executes");vendorArgs=new object[]{"bad progress",0,0};Require(!(bool)vendorParser.Invoke(null,vendorArgs),"upstream helper rejects nonprogress");
  Action<string,object> setVendor=(name,value)=>typeof(NeteaseMusicHelper).GetField(name,System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).SetValue(null,value);setVendor("_requestedTrack","test");setVendor("_requestedGeneration",23L);setVendor("_resultGeneration",23L);setVendor("_currentSec",20);setVendor("_totalSec",180);setVendor("_progressAt",DateTime.UtcNow);NeteaseMusicHelper.SetPlaying(true);int vendorCurrent,vendorTotal;Require(NeteaseMusicHelper.TryGetCachedProgress(out vendorCurrent,out vendorTotal)&&vendorCurrent==20&&vendorTotal==180,"upstream measured cache retained between worker ticks");setVendor("_progressAt",DateTime.UtcNow.AddSeconds(-10));Require(!NeteaseMusicHelper.TryGetCachedProgress(out vendorCurrent,out vendorTotal),"playing stale vendor progress expires");NeteaseMusicHelper.SetPlaying(false);Require(NeteaseMusicHelper.TryGetCachedProgress(out vendorCurrent,out vendorTotal),"paused measured progress retained");setVendor("_requestedGeneration",24L);Require(!NeteaseMusicHelper.TryGetCachedProgress(out vendorCurrent,out vendorTotal),"old vendor track generation rejected");
  var sharedPrefs=new Preferences();sharedPrefs.Apps.Add(new Entry{Name="Shared",Path="shared.exe",Group="游戏",Pinned=true});var shared=sharedPrefs.Apps[0];Require(FeatureRules.AddShared(sharedPrefs,new[]{shared.Path},"办公")==1&&FeatureRules.InGroup(shared,"游戏")&&FeatureRules.InGroup(shared,"办公")&&sharedPrefs.Apps.Count==1,"cross-group addition retains origin and unique all-view entry");Require(FeatureRules.AddShared(sharedPrefs,new[]{shared.Path},"办公")==0,"membership addition is idempotent");var captured=FeatureRules.Copy(shared);shared.ExtraGroups.Add("创作");Require(!captured.ExtraGroups.Contains("创作"),"appearance/removal membership snapshot deep copy");FeatureRules.RenameGroup(sharedPrefs,"办公","工作");Require(FeatureRules.InGroup(shared,"工作")&&!FeatureRules.InGroup(shared,"办公"),"rename secondary membership");FeatureRules.Transfer(sharedPrefs,new[]{shared.Path},"工作","学习阅读");Require(FeatureRules.InGroup(shared,"游戏")&&FeatureRules.InGroup(shared,"学习阅读")&&!FeatureRules.InGroup(shared,"工作"),"move within one group preserves other memberships");var removedMembership=EntryRules.Remove(sharedPrefs,new[]{shared.Path},"游戏");Require(sharedPrefs.Apps.Count==1&&shared.Pinned&&!FeatureRules.InGroup(shared,"游戏")&&FeatureRules.InGroup(shared,"学习阅读")&&sharedPrefs.Removed.Count==0,"remove one shared group keeps other groups and dock");Require(EntryRules.Undo(sharedPrefs,removedMembership)==1&&FeatureRules.InGroup(shared,"游戏")&&FeatureRules.InGroup(shared,"学习阅读"),"undo restores removed membership");using(var groupStream=new MemoryStream()){new DataContractJsonSerializer(typeof(Preferences)).WriteObject(groupStream,sharedPrefs);groupStream.Position=0;var restored=(Preferences)new DataContractJsonSerializer(typeof(Preferences)).ReadObject(groupStream);restored.Normalize();Require(restored.Apps.Count==1&&FeatureRules.InGroup(restored.Apps[0],"游戏")&&FeatureRules.InGroup(restored.Apps[0],"学习阅读"),"multi-group settings persist and normalize");}FeatureRules.DeleteGroup(sharedPrefs,"学习阅读");Require(!FeatureRules.InGroup(shared,"学习阅读")&&FeatureRules.InGroup(shared,"游戏"),"delete secondary group preserves primary");var removedAll=EntryRules.Remove(sharedPrefs,new[]{shared.Path},"全部");Require(sharedPrefs.Apps.Count==0&&EntryRules.Undo(sharedPrefs,removedAll)==1,"all-view removal and undo");
  var p=new Preferences{Light=true,PanelSize=2,DockGap=45,Imported=true,OpacityPercent=35};
  p.Apps.Add(new Entry{Name="剪映专业版",Path="C:\\中文目录\\软件.lnk",Pinned=true});
  p.BoardIconSize=32;p.BoardIconGap=2;
  p.Removed.Add("C:\\removed.lnk");
  var json=new DataContractJsonSerializer(typeof(Preferences));
  using(var stream=new MemoryStream()){
   json.WriteObject(stream,p);stream.Position=0;
   var q=(Preferences)json.ReadObject(stream);
   Require(q.Apps[0].Name==p.Apps[0].Name&&q.Apps[0].Path==p.Apps[0].Path&&q.Apps[0].Pinned&&q.PanelSize==2&&q.Light&&q.Imported&&q.DockGap==45&&q.OpacityPercent==35&&q.Removed.Count==1&&q.Hotkeys.Count==7&&q.BoardIconSize==32&&q.BoardIconGap==2,"JSON round trip");
  }
  Require(LayoutRules.Pages(0,35)==1,"empty page");
  Require(LayoutRules.Pages(35,35)==1,"exact boundary");
  Require(LayoutRules.Pages(36,35)==2,"overflow page");
  Require(LayoutRules.Pages(70,35)==2,"two complete pages");
  Require(LayoutRules.Pages(71,35)==3,"third page");
  Require(LayoutRules.Pages(26,25)==2,"responsive capacity");
  Require(LayoutRules.Matches("Microsoft Visual Studio Code","code MICROSOFT"),"multiple words/case");
  Require(LayoutRules.Matches("剪映专业版","剪映"),"Chinese search");
  Require(LayoutRules.Matches("Steam","   "),"blank search");
  Require(!LayoutRules.Matches("Steam","maya"),"unmatched query");
  Require(!LayoutRules.Matches("Visual Studio","code studio"),"all words required");
  bool rejected=false;try{LayoutRules.Pages(2,0);}catch(ArgumentOutOfRangeException){rejected=true;}
  Require(rejected,"invalid capacity");
  
  using(var legacy=new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"Apps\":[],\"PanelSize\":1,\"Light\":true}"))){
   var migrated=(Preferences)json.ReadObject(legacy);migrated.Normalize();
   Require(migrated.OpacityPercent==72&&migrated.Removed.Count==0&&migrated.Apps.Count==0&&migrated.Light&&migrated.Hotkeys.Count==7,"v0.1/v0.2 migration");Require(migrated.BoardIconSize==48&&migrated.BoardIconGap==5,"legacy icon density defaults");
  }
  Require(LayoutRules.GlassOpacity(-5)==10&&LayoutRules.GlassOpacity(200)==95,"opacity bounds");
  Require(LayoutRules.GlassOpacity(double.NaN)==72&&LayoutRules.GlassOpacity(double.PositiveInfinity)==72,"invalid opacity");
  Require(!LayoutRules.AllowImport("c:\\REMOVED.lnk",p.Removed,false),"removed entry must not be reimported");
  Require(LayoutRules.AllowImport("c:\\REMOVED.lnk",p.Removed,true),"manual re-add allowed");
  Require(LayoutRules.AllowImport("c:\\new.lnk",p.Removed,false),"new import allowed");
  var journal=new List<ShellState>{new ShellState{Handle=1234567890123L,ClassName="Shell_TrayWnd",Visible=false},new ShellState{Handle=456,ClassName="SysListView32",Visible=true}};
  using(var stream=new MemoryStream()){
   var codec=new DataContractJsonSerializer(typeof(List<ShellState>));codec.WriteObject(stream,journal);stream.Position=0;
   var restored=(List<ShellState>)codec.ReadObject(stream);
   Require(restored.Count==2&&restored[0].Handle==journal[0].Handle&&!restored[0].Visible&&restored[1].Visible,"recovery journal serialization");
  }

  
  var defaults=HotkeyRules.Defaults();
  Require(HotkeyRules.Validate(defaults)==null,"default hotkey validity");
  Require(HotkeyRules.Normalize(null).Count==7,"legacy default hotkeys");
  var duplicate=defaults.Select(k=>k.Copy()).ToList();duplicate[1].Modifiers=duplicate[0].Modifiers;duplicate[1].Key=duplicate[0].Key;
  Require(HotkeyRules.Validate(duplicate)!=null,"duplicate rejection");
  duplicate[1].Enabled=false;Require(HotkeyRules.Validate(duplicate)==null,"disabled binding may duplicate");
  var registered=new Dictionary<int,string>();var occupied=new HashSet<string>();
  var manager=new HotkeyManager(k=>{string chord=k.Modifiers+":"+k.Key;if(occupied.Contains(chord)||registered.ContainsKey(k.ActionId)||registered.Values.Contains(chord))return false;registered[k.ActionId]=chord;return true;},id=>registered.Remove(id));
  Require(manager.Start(defaults).Count==0&&registered.Count==7,"startup registration");
  string error;duplicate[1].Enabled=true;
  Require(!manager.TryApply(duplicate,out error)&&registered.Count==7&&HotkeyRules.Same(manager.Active,defaults),"reject before unregistering originals");
  var custom=defaults.Select(k=>k.Copy()).ToList();custom[1].Modifiers=6;custom[1].Key=0x41;occupied.Add("6:65");
  Require(!manager.TryApply(custom,out error)&&registered.Count==7&&HotkeyRules.Same(manager.Active,defaults),"external conflict rollback");
  occupied.Clear();Require(manager.TryApply(custom,out error)&&registered[2]=="6:65"&&HotkeyRules.Same(manager.Active,custom),"live custom rebinding");
  var disabled=custom.Select(k=>k.Copy()).ToList();foreach(var k in disabled)k.Enabled=false;
  Require(manager.TryApply(disabled,out error)&&registered.Count==0&&manager.Active.Count==0,"disable all actions");
  Require(manager.TryApply(defaults,out error)&&registered.Count==7,"restore defaults");
  manager.Release();Require(registered.Count==0,"release all registered keys");

  // Batch removal preserves exact identities and undo does not erase later additions.
  var bulk=new Preferences();for(int i=0;i<100;i++)bulk.Apps.Add(new Entry{Name="软件 "+i,Path="entry-"+i+".lnk",Pinned=i%3==0});
  bulk.Removed.Add("entry-49.lnk");var original=bulk.Apps.ToList();
  var removal=EntryRules.Remove(bulk,new string[]{"ENTRY-0.LNK","entry-49.lnk","entry-99.lnk","entry-99.lnk","missing.lnk"});
  Require(removal.Items.Count==3&&bulk.Apps.Count==97&&bulk.Removed.Count==3,"case insensitive batch removal and tombstone deduplication");
  Require(!LayoutRules.AllowImport("ENTRY-99.LNK",bulk.Removed,false),"batch removal prevents auto reimport");
  bulk.Apps.Add(new Entry{Name="新增",Path="new.lnk"});
  Require(EntryRules.Undo(bulk,removal)==3&&bulk.Apps.Count==101&&bulk.Apps.Take(100).SequenceEqual(original)&&bulk.Apps.Last().Path=="new.lnk","undo restores order/pins and keeps later additions");
  Require(bulk.Removed.Count==1&&bulk.Removed[0]=="entry-49.lnk","undo preserves earlier tombstones");
  removal=EntryRules.Remove(bulk,new string[]{"entry-0.lnk","entry-1.lnk"});
  bulk.Apps.Add(new Entry{Name="手动恢复",Path="ENTRY-0.LNK"});
  Require(EntryRules.Undo(bulk,removal)==1&&bulk.Apps.Count(a=>a.Path.Equals("entry-0.lnk",StringComparison.OrdinalIgnoreCase))==1,"undo avoids duplicate manual readd");
  var picks=EntryRules.Candidates(new string[]{"NEW.LNK","fresh.exe","FRESH.EXE","another.lnk",""},bulk.Apps);
  Require(picks.Count==2&&!picks.Contains("NEW.LNK"),"batch candidate deduplication");
  Require(EntryRules.Importable("UPPER.EXE")&&EntryRules.Importable("快捷方式.lnk")&&!EntryRules.Importable("readme.txt"),"scan extension filter");
  var v3="{\"Apps\":[{\"Name\":\"原软件\",\"Path\":\"old.lnk\",\"Pinned\":true}],\"OpacityPercent\":45,\"DockGap\":38,\"Hotkeys\":[{\"ActionId\":2,\"Modifiers\":6,\"Key\":65,\"Enabled\":true}]}";
  using(var stream=new MemoryStream(System.Text.Encoding.UTF8.GetBytes(v3))){var migrated=(Preferences)json.ReadObject(stream);migrated.Normalize();Require(migrated.Dock.OpacityPercent==45&&migrated.Dock.IconSize==44&&migrated.Dock.ShowComputer&&migrated.Dock.MotionEnabled&&migrated.DockGap==38&&migrated.Apps[0].Pinned&&migrated.Hotkeys[1].Modifiers==6,"v0.3 migration retains panel/Dock opacity, layout and custom key");}
  using(var stream=new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"Dock\":{\"IconSize\":60}}"))){var partial=(Preferences)json.ReadObject(stream);partial.Normalize();Require(partial.Dock.IconSize==60&&partial.Dock.ShowComputer&&partial.Dock.ShowSeparator&&partial.Dock.OpacityPercent==72&&partial.Dock.MaxPinned==9,"partial Dock data contract defaults");}
  p.Dock=new DockOptions{IconSize=62,Spacing=13,Padding=23,Radius=42,OpacityPercent=31,Zoom=1.45,Offset=-70,MotionLevel=1.3,Alignment=2,Accent=4,MaxPinned=17,ComputerStyle=1,ShowComputer=false,ShowLabels=true,ShowSeparator=false,AlwaysOnTop=true,MotionEnabled=false};
  using(var stream=new MemoryStream()){json.WriteObject(stream,p);stream.Position=0;var round=(Preferences)json.ReadObject(stream);round.Normalize();Require(round.Dock.IconSize==62&&round.Dock.OpacityPercent==31&&round.Dock.MotionLevel==1.3&&!round.Dock.MotionEnabled&&!round.Dock.ShowComputer&&!round.Dock.ShowSeparator&&round.Dock.ShowLabels&&round.Dock.AlwaysOnTop&&round.Dock.Alignment==2&&round.Dock.Accent==4&&round.Dock.MaxPinned==17&&round.Dock.ComputerStyle==1,"all Dock options round trip including false booleans");}
  var invalidDock=new DockOptions{IconSize=double.NaN,Spacing=double.PositiveInfinity,Padding=-5,Radius=200,OpacityPercent=double.NaN,Zoom=20,Offset=-5000,MotionLevel=double.NaN,Alignment=8,Accent=-4,MaxPinned=100,ComputerStyle=3};invalidDock.Normalize();
  Require(invalidDock.IconSize==44&&invalidDock.Spacing==6&&invalidDock.Padding==8&&invalidDock.Radius==48&&invalidDock.OpacityPercent==72&&invalidDock.Zoom==1.6&&invalidDock.Offset==-500&&invalidDock.MotionLevel==1&&invalidDock.Alignment==2&&invalidDock.Accent==0&&invalidDock.MaxPinned==20&&invalidDock.ComputerStyle==1,"Dock invalid values and range bounds");
  foreach(double available in new double[]{640,800,1024,1280,1920})foreach(int count in new int[]{0,1,9,20,50})foreach(double size in new double[]{32,44,64}) {
   var geometry=new DockOptions{IconSize=size,Spacing=20,Padding=28,MaxPinned=20};int visible=geometry.VisiblePinned(available,count);
   Require(visible>=0&&visible<=Math.Min(count,20)&&geometry.WidthFor(visible)<=available,"Dock fits work area");
  }
  string folder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"LuminaScan-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  try {
   File.WriteAllText(System.IO.Path.Combine(folder,"one.lnk"),"");File.WriteAllText(System.IO.Path.Combine(folder,"ignore.txt"),"");Directory.CreateDirectory(System.IO.Path.Combine(folder,"sub"));File.WriteAllText(System.IO.Path.Combine(folder,"sub","two.EXE"),"");
   Require(ImportScanner.Scan(new string[]{folder},false,System.Threading.CancellationToken.None).Paths.Count==1,"nonrecursive folder scan");
   Require(ImportScanner.Scan(new string[]{folder,folder},true,System.Threading.CancellationToken.None).Paths.Count==2,"recursive scan and overlapping roots");
   var limited=ImportScanner.Scan(new string[]{folder},true,System.Threading.CancellationToken.None,1);Require(limited.Paths.Count==1&&limited.Limited,"scan result limit");
   var stopped=new System.Threading.CancellationTokenSource();stopped.Cancel();bool cancelled=false;try{ImportScanner.Scan(new string[]{folder},true,stopped.Token);}catch(OperationCanceledException){cancelled=true;}Require(cancelled,"scan cancellation");
  }finally{Directory.Delete(folder,true);}

  var legacyKeys=HotkeyRules.Defaults().Take(6).Select(k=>k.Copy()).ToList();legacyKeys[0].Key=0x48;
  var upgraded=HotkeyRules.Normalize(legacyKeys);Require(upgraded.Count==7&&upgraded[0].Key==0x48&&!upgraded[6].Enabled,"new shortcut collision preserves old custom bindings");
  legacyKeys[0].Key=0x20;upgraded=HotkeyRules.Normalize(legacyKeys);Require(upgraded[6].Enabled&&upgraded[6].Key==0x48,"append seventh shortcut");
  var migration=new Preferences{Hotkeys=legacyKeys,Groups=null,Workspaces=null,Placement=null,Widgets=null};migration.Normalize();Require(migration.Groups.Count==3&&migration.Workspaces.Count==0&&migration.Placement.DockLocked&&!migration.Widgets.Clock&&!migration.Widgets.Weather&&!migration.Widgets.Music,"feature defaults and legacy migration");
  var organized=new Preferences();organized.Apps.Add(new Entry{Name="Maya",Path="maya.exe",Pinned=true,IconPath="custom.png"});organized.Apps.Add(new Entry{Name="项目",Path="project"});
  Require(FeatureRules.Move(organized,new string[]{"MAYA.EXE","maya.exe"},"创作")==1&&FeatureRules.GroupName(organized.Apps[0])=="创作","case-insensitive batch grouping");
  FeatureRules.RenameGroup(organized,"创作","三维制作");Require(organized.Apps[0].Group=="三维制作"&&organized.Groups.Contains("三维制作"),"rename group updates membership");
  FeatureRules.DeleteGroup(organized,"三维制作");Require(FeatureRules.GroupName(organized.Apps[0])=="未分组"&&organized.Apps.Count==2&&organized.Apps[0].Pinned,"remove group preserves entries and pins");
  var workspace=new Workspace{Name=" 创作工作区 ",Paths=new List<string>{"maya.exe","MAYA.EXE","project",""}};workspace.Normalize();Require(workspace.Name=="创作工作区"&&workspace.Paths.Count==2,"workspace trims and deduplicates");organized.Workspaces.Add(workspace);
  organized.Placement=new PlacementOptions{DockLeft=17,DockTop=700,PanelLeft=40,PanelTop=55,PanelWidth=1200,PanelHeight=700,DockLocked=false,PanelLocked=true};organized.Widgets=new WidgetOptions{Clock=true,Weather=true,Music=true,City=" Beijing ",Volume=.3};organized.Widgets.Normalize();organized.Dock.AutoHide=true;organized.Dock.HideDelay=1300;organized.Dock.EdgeRange=9;
  using(var stream=new MemoryStream()){json.WriteObject(stream,organized);stream.Position=0;var restored=(Preferences)json.ReadObject(stream);restored.Normalize();Require(restored.Workspaces.Count==1&&restored.Workspaces[0].Paths.Count==2&&restored.Placement.DockLeft==17&&!restored.Placement.DockLocked&&restored.Placement.PanelLocked&&restored.Widgets.Clock&&restored.Widgets.Weather&&restored.Widgets.Music&&restored.Widgets.City=="Beijing"&&restored.Apps[0].IconPath=="custom.png"&&restored.Dock.AutoHide&&restored.Dock.HideDelay==1300&&restored.Dock.ShowRunning,"new config round trip");}
  organized.BoardIconSize=36;organized.BoardIconGap=3;var scheme=FeatureRules.Capture(organized,"测试方案");organized.Apps[0].Name="改名";organized.Dock.IconSize=64;organized.BoardIconSize=80;organized.BoardIconGap=20;organized.Apps.Add(new Entry{Name="后来添加",Path="later.exe"});var originalKeys=organized.Hotkeys.Select(k=>k.Copy()).ToList();FeatureRules.Apply(organized,scheme);Require(organized.Apps[0].Name=="Maya"&&organized.Apps[0].IconPath=="custom.png"&&organized.Apps.Count==3&&organized.Apps.Last().Path=="later.exe"&&organized.Dock.IconSize==scheme.Dock.IconSize&&HotkeyRules.Same(organized.Hotkeys,originalKeys)&&organized.Workspaces.Count==1&&organized.BoardIconSize==36&&organized.BoardIconGap==3,"scheme restores layout and keeps new entries, shortcuts and workspaces");
  using(var stream=new MemoryStream()){var codec=new DataContractJsonSerializer(typeof(AppearanceScheme));codec.WriteObject(stream,scheme);stream.Position=0;var restored=(AppearanceScheme)codec.ReadObject(stream);Require(restored.Name==scheme.Name&&restored.Version==1&&restored.Placement.PanelWidth==1200&&restored.Widgets.City=="Beijing"&&restored.Apps.Count==2,"scheme serialization");}
  bool unsupported=false;try{FeatureRules.Apply(organized,new AppearanceScheme{Version=99,Dock=new DockOptions()});}catch(ArgumentException){unsupported=true;}Require(unsupported,"reject unknown scheme version");
  var badPlacement=new PlacementOptions{PanelLeft=double.NaN,DockTop=double.PositiveInfinity,PanelWidth=-20};badPlacement.Normalize();Require(!badPlacement.PanelLeft.HasValue&&!badPlacement.DockTop.HasValue&&!badPlacement.PanelWidth.HasValue,"reject invalid placement");
  Require(FeatureRules.Position(-10,0,1000,400)==0&&FeatureRules.Position(2000,0,1000,400)==600&&FeatureRules.Position(20,0,300,400)==0,"clamp layout to work area");
  foreach(uint state in new uint[]{0,1,2,3,6,7,128,129})Require((FeatureRules.AutoHideState(state,true)&1)==1&&(FeatureRules.AutoHideState(state,false)&1)==0&&(FeatureRules.AutoHideState(state,true)&~1u)==(state&~1u),"taskbar auto-hide preserves other flags");
  var policy=new AutoHidePolicy();var now=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc);Require(!policy.Update(now,true,false,false,false,900),"auto-hide waits initial delay");Require(!policy.Update(now.AddMilliseconds(899),true,false,false,false,900)&&policy.Update(now.AddMilliseconds(900),true,false,false,false,900),"auto-hide threshold");Require(!policy.Update(now.AddMilliseconds(1000),true,false,true,false,900),"edge reveals Dock");Require(!policy.Update(now.AddMilliseconds(1500),true,false,false,true,900),"modal interaction prevents hide");Require(!policy.Update(now.AddMilliseconds(2500),false,false,false,false,900),"disable reveals Dock");
  var badFeatureDock=new DockOptions{HideDelay=0,EdgeRange=200,BlurRadius=double.NaN,EdgeStrength=30};badFeatureDock.Normalize();Require(badFeatureDock.HideDelay==200&&badFeatureDock.EdgeRange==48&&badFeatureDock.BlurRadius==18&&badFeatureDock.EdgeStrength==2,"new DIY bounds");
  Require(FeatureRules.WeatherLabel(0)=="晴"&&FeatureRules.WeatherLabel(61)=="雨"&&FeatureRules.WeatherLabel(96)=="雷雨"&&FeatureRules.WeatherLabel(-1).Contains("-1"),"weather code mapping");
  using(var stream=new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"results\":[{\"name\":\"北京\",\"country\":\"中国\",\"latitude\":39.9,\"longitude\":116.4}]}"))){var geo=(GeoReply)new DataContractJsonSerializer(typeof(GeoReply)).ReadObject(stream);Require(geo.Results[0].Name=="北京"&&geo.Results[0].Latitude==39.9,"weather geocoding JSON contract");}
  using(var stream=new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"current\":{\"temperature_2m\":21.4,\"weather_code\":3,\"wind_speed_10m\":9.1,\"time\":\"2026-10-12T01:00\"}}"))){var forecast=(WeatherReply)new DataContractJsonSerializer(typeof(WeatherReply)).ReadObject(stream);Require(forecast.Current.Temperature==21.4&&forecast.Current.Code==3&&forecast.Current.Time=="2026-10-12T01:00","weather current JSON contract");}


  var widgetLegacy=new DataContractJsonSerializer(typeof(WidgetOptions));
  using(var stream=new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"Clock\":true,\"Music\":true,\"City\":\"北京\",\"Volume\":0.4}"))){var old=(WidgetOptions)widgetLegacy.ReadObject(stream);old.Normalize();Require(WidgetRules.Layout(old).Count==1&&old.ClockPanel==0&&old.MusicPanel==0&&!old.Notes&&old.Playlist.Count==0&&old.MusicMode=="system","legacy widgets remain fused without notes or autoplay");}
  var split=new WidgetOptions{Clock=true,Weather=true,Music=true,Notes=true};split.Normalize();split.Separate();Require(WidgetRules.Layout(split).Count==4,"four independent widget panels");WidgetRules.Assign(split,"weather",0);Require(WidgetRules.Layout(split).Count==3&&WidgetRules.Layout(split)[0].SequenceEqual(new string[]{"clock","weather"}),"arbitrary selective widget fusion");Require(WidgetRules.FreePanel(split,"clock")==1,"find unused panel for detachment");split.Fuse();Require(WidgetRules.Layout(split).Count==1&&WidgetRules.Layout(split)[0].Count==4,"merge all widgets");
  split.Playlist=new List<string>{"音乐一.mp3","音乐一.MP3","two.wav",""};split.MusicMode="local";split.TrackIndex=99;split.Position(0).Left=123;split.Position(0).Top=456;split.CompactMusic=true;split.Locked=true;split.Topmost=true;split.Shuffle=true;split.RepeatMode=2;split.Normalize();Require(split.Playlist.Count==2&&split.TrackIndex==1,"playlist normalization");var copied=split.Copy();copied.Playlist.Clear();copied.Panels[0].Left=999;Require(split.Playlist.Count==2&&split.Panels[0].Left==123,"appearance capture uses independent widget state");
  using(var stream=new MemoryStream()){widgetLegacy.WriteObject(stream,split);stream.Position=0;var back=(WidgetOptions)widgetLegacy.ReadObject(stream);back.Normalize();Require(back.Notes&&back.CompactMusic&&back.Locked&&back.Topmost&&back.Playlist.Count==2&&back.TrackIndex==1&&back.Shuffle&&back.RepeatMode==2&&back.Panels[0].Top==456,"new widget state round trip");}
  Require(WidgetRules.NextTrack(0,0,0,false,0,true)==-1&&WidgetRules.NextTrack(2,3,0,false,0,true)==-1&&WidgetRules.NextTrack(2,3,1,false,0,true)==0&&WidgetRules.NextTrack(1,3,2,false,0,true)==1&&WidgetRules.NextTrack(1,3,0,true,0,false)!=1,"playlist ends/repeat/shuffle boundaries");Require(WidgetRules.Duration(65)=="1:05"&&WidgetRules.Duration(3601)=="1:00:01"&&WidgetRules.Duration(double.NaN)=="0:00","duration labels");
  Require(StartupRules.Command(@"C:\我的 软件\Lumina.exe")=="\"C:\\我的 软件\\Lumina.exe\" --startup","startup quoted path");Require(!StartupRules.Enabled(null,null)&&StartupRules.Enabled("app",null)&&!StartupRules.Enabled("app",new byte[]{3,0,0})&&StartupRules.Enabled("app",new byte[]{2,0,0}),"startup default and disabled-state detection");bool badStartup=false;try{StartupRules.Command("bad\"path");}catch(ArgumentException){badStartup=true;}Require(badStartup,"reject invalid startup target");
  var book=new NoteBook{Items=new List<NoteItem>{null,new NoteItem{Id="same",Title="  随手记  ",Body="分镜想法\n☐ 明天修改"},new NoteItem{Id="same",Title="重复"}},SelectedId="missing"};book.Normalize();Require(book.Items.Count==1&&book.Selected.Title=="随手记"&&book.Selected.Body.Contains("☐"),"notebook normalization preserves Chinese/checklists");
  string noteFolder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(noteFolder);try{string notePath=System.IO.Path.Combine(noteFolder,"notes.json");NoteFiles.Write(notePath,book);book.Selected.Body="第二版";NoteFiles.Write(notePath,book);Require(NoteFiles.Read(notePath).Selected.Body=="第二版"&&File.Exists(notePath+".bak"),"atomic notes save and backup");File.WriteAllText(notePath,"invalid");var recover=NoteFiles.Read(notePath);Require(recover.Selected.Body.Contains("☐")&&Directory.GetFiles(noteFolder,"*.damaged-*").Length==1,"recover notes from backup and retain damaged bytes");File.WriteAllText(notePath+".bak","invalid");Require(NoteFiles.Read(notePath).Items.Count==1,"fresh usable notebook if both copies damaged");}finally{Directory.Delete(noteFolder,true);}
  using(var stream=new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"Available\":true,\"Playing\":true,\"Title\":\"测试歌曲\",\"Artist\":\"歌手\",\"Position\":24.5,\"Duration\":183,\"CanSeek\":true}"))){var nowPlaying=(MediaSnapshot)new DataContractJsonSerializer(typeof(MediaSnapshot)).ReadObject(stream);Require(nowPlaying.Available&&nowPlaying.Playing&&nowPlaying.CanSeek&&nowPlaying.Title=="测试歌曲"&&nowPlaying.Position==24.5,"Windows media bridge JSON contract");}
  string metadataFolder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(metadataFolder);try{
   var frames=new List<byte>();Action<string,byte[]> frame=(id,body)=>{frames.AddRange(System.Text.Encoding.ASCII.GetBytes(id));frames.AddRange(new byte[]{(byte)(body.Length>>24),(byte)(body.Length>>16),(byte)(body.Length>>8),(byte)body.Length,0,0});frames.AddRange(body);};frame("TIT2",new byte[]{3}.Concat(System.Text.Encoding.UTF8.GetBytes("夜曲")).ToArray());frame("TPE1",new byte[]{3}.Concat(System.Text.Encoding.UTF8.GetBytes("艺术家")).ToArray());byte[] image={137,80,78,71,1,2,3};frame("APIC",new byte[]{0}.Concat(System.Text.Encoding.ASCII.GetBytes("image/png")).Concat(new byte[]{0,3,0}).Concat(image).ToArray());int len=frames.Count;string song=System.IO.Path.Combine(metadataFolder,"fallback.mp3");File.WriteAllBytes(song,new byte[]{73,68,51,3,0,0,(byte)((len>>21)&127),(byte)((len>>14)&127),(byte)((len>>7)&127),(byte)(len&127)}.Concat(frames).ToArray());var meta=TrackMetadata.Read(song);Require(meta.Title=="夜曲"&&meta.Artist=="艺术家"&&meta.Cover.SequenceEqual(image),"local ID3v2 title/artist/artwork extraction");File.WriteAllBytes(song,new byte[]{0,0,0});File.WriteAllBytes(System.IO.Path.Combine(metadataFolder,"cover.jpg"),image);meta=TrackMetadata.Read(song);Require(meta.Title=="fallback"&&meta.Cover.SequenceEqual(image),"local cover file fallback and malformed tag handling");
  }finally{Directory.Delete(metadataFolder,true);}
  // Close/save/load/reopen every subset, both fused and separated.
  foreach(bool separated in new bool[]{false,true})for(int mask=0;mask<16;mask++){
   var saved=new Preferences{Imported=true};saved.Widgets.Clock=saved.Widgets.Weather=saved.Widgets.Music=saved.Widgets.Notes=true;if(separated)saved.Widgets.Separate();
   foreach(string kind in WidgetRules.Kinds)WidgetRules.Enable(saved.Widgets,kind,(mask&(1<<Array.IndexOf(WidgetRules.Kinds,kind)))!=0);
   using(var stream=new MemoryStream()){var codec=new DataContractJsonSerializer(typeof(Preferences));codec.WriteObject(stream,saved);stream.Position=0;var restored=(Preferences)codec.ReadObject(stream);restored.Normalize();
    foreach(string kind in WidgetRules.Kinds){Require(WidgetRules.Enabled(restored.Widgets,kind)==((mask&(1<<Array.IndexOf(WidgetRules.Kinds,kind)))!=0),"saved component state");WidgetRules.Enable(restored.Widgets,kind,true);}
    var groups=WidgetRules.Layout(restored.Widgets);Require(groups.Values.Sum(x=>x.Count)==4&&groups.Count==(separated?4:1),"reopen after saving closed components");}
  }
  var registry=new WidgetRegistry<object>();var first=new object();var replacement=new object();registry.Add(0,first);Require(registry.Remove(0,first),"detach before close");registry.Add(0,replacement);Require(!registry.Remove(0,first),"late old Closed cannot remove replacement");object found;Require(registry.TryGetValue(0,out found)&&object.ReferenceEquals(found,replacement),"reopened component remains tracked");Require(registry.Remove(0,replacement)&&!registry.Remove(0,replacement),"repeated close ignored");for(int cycle=0;cycle<100;cycle++){var instance=new object();registry.Add(0,instance);Require(registry.Remove(0,instance),"repeated component reopening");}Require(registry.Values.Count==0,"no stale closed instances");
  var recovery=new WidgetOptions{City="jinan",MusicMode="local",ClockPanel=0,WeatherPanel=1,MusicPanel=2,NotesPanel=3,Playlist=new List<string>{"C:\\Music\\demo.mp3"},Panels=new List<WidgetPanelPosition>{new WidgetPanelPosition{Id=1,Left=1214,Top=661.33333333333326}}};WidgetRules.Recover(recovery);Require(WidgetRules.Kinds.All(k=>WidgetRules.Enabled(recovery,k))&&WidgetRules.Layout(recovery).Count==4&&recovery.City=="jinan"&&recovery.Playlist.Count==1&&recovery.MusicMode=="local"&&recovery.Panels[0].Left==1214,"recovery enables components while preserving user fields");
  Require(MediaTimeline.Position(10,200,true,1,7)==17&&MediaTimeline.Position(10,200,false,1,7)==10&&MediaTimeline.Position(10,200,true,2,7)==24,"timeline update age/rate/pause");Require(MediaTimeline.Position(195,200,true,1,9)==200&&MediaTimeline.Position(20,0,true,1,8)==0,"duration boundaries and missing timeline");Require(MediaTimeline.Position(double.NaN,200,true,double.NaN,double.NaN)==0,"invalid timeline values");Require(MediaTimeline.ClampSeek(10,30,150,200)==30&&MediaTimeline.ClampSeek(190,30,150,200)==150&&MediaTimeline.ClampSeek(99,0,0,200)==99,"valid seek range");
  var seekQueue=new MediaSeekQueue();seekQueue.Put(20,"song-a");seekQueue.Put(60,"song-a");double requested;Require(seekQueue.Take("song-a",out requested)&&requested==60&&!seekQueue.Take("song-a",out requested),"latest seek wins and only consumed once");seekQueue.Put(80,"song-a");Require(!seekQueue.Take("song-b",out requested),"stale track seek discarded");seekQueue.Put(10,"song-b");seekQueue.Clear();Require(!seekQueue.Take("song-b",out requested),"mode change clears queued seek");
  var customWidgets=new WidgetOptions{OpacityPercent=0,Panels=new List<WidgetPanelPosition>{new WidgetPanelPosition{Id=2,Width=600,Height=420,OpacityPercent=0}}};customWidgets.Normalize();Require(WidgetSizing.Background(customWidgets,customWidgets.Position(2))==0,"true transparent component background");var customCopy=customWidgets.Copy();Require(customCopy.Position(2).Width==600&&customCopy.Position(2).Height==420&&customCopy.Position(2).OpacityPercent==0,"copy custom widget dimensions and transparency");customCopy.Position(2).Width=500;Require(customWidgets.Position(2).Width==600,"panel dimensions deep copied");using(var stream=new MemoryStream()){var codec=new DataContractJsonSerializer(typeof(WidgetOptions));codec.WriteObject(stream,customWidgets);stream.Position=0;var restored=(WidgetOptions)codec.ReadObject(stream);restored.Normalize();Require(restored.OpacityPercent==0&&restored.Position(2).OpacityPercent==0&&restored.Position(2).Width==600&&restored.Position(2).Height==420,"transparent zero and dimensions survive JSON");}Require(!WidgetSizing.Dimension(double.NaN,260,1600).HasValue&&WidgetSizing.Dimension(10,260,1600)==260&&WidgetSizing.Dimension(2000,180,1600)==1600,"widget dimension bounds");using(var stream=new MemoryStream(Encoding.UTF8.GetBytes("{\"Clock\":true}"))){var oldWidget=(WidgetOptions)new DataContractJsonSerializer(typeof(WidgetOptions)).ReadObject(stream);oldWidget.Normalize();Require(oldWidget.OpacityPercent==72,"old widgets retain visible background default");}
  Require(AutoGroupRules.Guess(new Entry{Name="Adobe Photoshop 2025"})=="创作"&&AutoGroupRules.Guess(new Entry{Name="QQ音乐"})=="音乐"&&AutoGroupRules.Guess(new Entry{Name="QQ"})=="社交"&&AutoGroupRules.Guess(new Entry{Name="Steam"})=="游戏","software categories and overlapping music/social names");
  Require(AutoGroupRules.Guess(new Entry{Name="我的编辑器",Path=@"C:\Apps\Code.exe"})=="开发"&&AutoGroupRules.Guess(new Entry{Name="codec"})==null&&AutoGroupRules.Guess(new Entry{Name="工具",Path=@"C:\Steam\mystery.exe"})==null,"executable name fallback, word boundaries and ignore parent folders");
  var grouped=new Preferences{Apps=new List<Entry>{new Entry{Name="Chrome",Path="chrome.exe"},new Entry{Name="Word",Path="word.exe",Group="自定义"},new Entry{Name="未知程序",Path="unknown.exe"}}};Require(AutoGroupRules.ApplyUnassigned(grouped)==1&&grouped.Apps[0].Group=="浏览器"&&grouped.Apps[1].Group=="自定义"&&grouped.Apps[2].Group==null&&grouped.Groups.Contains("浏览器"),"bulk classification preserves manual groups and unknown entries");Require(AutoGroupRules.ApplyUnassigned(grouped)==0,"automatic grouping idempotent");
  foreach(bool enabled in new bool[]{false,true})using(var stream=new MemoryStream()){var codec=new DataContractJsonSerializer(typeof(Preferences));codec.WriteObject(stream,new Preferences{AutoGrouping=enabled});stream.Position=0;Require(((Preferences)codec.ReadObject(stream)).AutoGrouping==enabled,"automatic grouping on/off survives save");}
  using(var stream=new MemoryStream(Encoding.UTF8.GetBytes("{}")))Require(((Preferences)new DataContractJsonSerializer(typeof(Preferences)).ReadObject(stream)).AutoGrouping,"legacy settings enable classification for future additions");
  var day=new DateTime(2026,10,12,8,0,0);var utc=new DateTime(2026,10,12,0,0,0,DateTimeKind.Utc);Require(ClockRules.Next(7,0,day)==day.Date.AddDays(1).AddHours(7)&&ClockRules.Next(9,0,day)==day.Date.AddHours(9),"alarm scheduling today/tomorrow");
  var timed=new WidgetOptions{Alarms=new List<ClockAlarm>{new ClockAlarm{Label="单次",Hour=8,DueLocal=day,Daily=false},new ClockAlarm{Label="每日",Hour=8,DueLocal=day,Daily=true}}};Require(ClockRules.Check(timed,day.AddSeconds(-1),utc).Count==0,"alarm not early");Require(ClockRules.Check(timed,day,utc).Count==2&&!timed.Alarms[0].Enabled&&timed.Alarms[1].DueLocal==day.AddDays(1),"one shot disables and repeating reschedules");Require(ClockRules.Check(timed,day.AddSeconds(1),utc).Count==0,"alarm triggers once");Require(ClockRules.Check(timed,day.AddDays(3).AddHours(1),utc).Count==1&&timed.Alarms[1].DueLocal>day.AddDays(3).AddHours(1),"wake/relaunch skips missed repeat backlog");
  ClockRules.Start(timed,120,utc);Require(ClockRules.Remaining(timed,utc.AddSeconds(30))==90,"countdown elapsed time");ClockRules.Pause(timed,utc.AddSeconds(30));Require(!timed.TimerDueUtc.HasValue&&ClockRules.Remaining(timed,utc.AddHours(1))==90,"countdown pause");ClockRules.Resume(timed,utc.AddHours(1));Require(timed.TimerDueUtc==utc.AddHours(1).AddSeconds(90),"countdown resume");Require(ClockRules.Check(timed,day,utc.AddHours(1).AddSeconds(90)).Contains("倒计时结束")&&!timed.TimerDueUtc.HasValue&&ClockRules.Check(timed,day,utc.AddHours(2)).Count==0,"timer fires once");ClockRules.Start(timed,86400,utc);Require(ClockRules.Display(timed,utc).Contains("24:00:00"),"full-day countdown display");
  var timedCopy=timed.Copy();timedCopy.Alarms[0].Label="copy";Require(timed.Alarms[0].Label=="单次","alarm deep copy");using(var stream=new MemoryStream()){var codec=new DataContractJsonSerializer(typeof(WidgetOptions));codec.WriteObject(stream,timed);stream.Position=0;var restored=(WidgetOptions)codec.ReadObject(stream);restored.Normalize();Require(restored.TimerDueUtc==timed.TimerDueUtc&&restored.Alarms.Count==2&&restored.Alarms[1].DueLocal==timed.Alarms[1].DueLocal,"alarm and countdown persistence");}ClockRules.Cancel(timed);Require(ClockRules.Remaining(timed,utc)==0,"countdown cancel");
  string catalogPath=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data","AppGroups.tsv");if(!File.Exists(catalogPath))catalogPath=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","data","AppGroups.tsv");if(!File.Exists(catalogPath))catalogPath="data/AppGroups.tsv";Require(File.Exists(catalogPath),"offline catalog delivered");Require(AutoGroupRules.LoadCatalog(File.ReadLines(catalogPath))>=700,"large offline alias table loads");
  foreach(string title in new[]{"Black Myth: Wukong","黑神话：悟空","Counter-Strike 2","Cyberpunk 2077","Skyrim Special Edition","GTA5","原神","绝区零","Minecraft","Stardew Valley"})Require(AutoGroupRules.Guess(new Entry{Name=title,Path=title+".lnk"})=="游戏","offline game alias: "+title);
  foreach(string title in new[]{"Mod Organizer 2","Vortex","SMAPI","WeMod","skse64_loader"})Require(AutoGroupRules.Guess(new Entry{Name=title,Path=title+".exe"})=="Mod工具","mod launcher alias: "+title);
  Require(AutoGroupRules.Guess(new Entry{Name="Autodesk Maya 2023",Path="Maya.lnk"})=="创作"&&AutoGroupRules.Guess(new Entry{Name="AE",Path="AE.lnk"})=="创作"&&AutoGroupRules.Guess(new Entry{Name="FL Studio",Path="FL.lnk"})=="音乐","creative names/version/short exact aliases");
  Require(AutoGroupRules.Guess(new Entry{Name="My tool",Path="tool.lnk",LaunchTarget=@"C:\Apps\pycharm64.exe"})=="开发"&&AutoGroupRules.Guess(new Entry{Name="Unknown",Path="unknown.exe",ProductInfo="JetBrains PyCharm"})=="开发","shortcut target and executable metadata hints");
  Require(AutoGroupRules.Guess(new Entry{Name="Unknown Game",Path="g.url",LaunchTarget="steam://rungameid/999999"})=="游戏"&&AutoGroupRules.Guess(new Entry{Name="Unknown Game",Path=@"D:\SteamLibrary\steamapps\common\Unknown\play.exe"})=="游戏","offline game launch URI and Steam install location");
  Require(AutoGroupRules.Guess(new Entry{Name="Steam uninstall",Path="unins000.exe"})==null&&AutoGroupRules.Guess(new Entry{Name="Skyrim updater",Path="SkyrimUpdater.exe"})==null&&AutoGroupRules.Guess(new Entry{Name="Photoshop",Path="Photoshop.dll"})==null,"helper and non-launch files excluded from grouping");
  var learned=new Preferences{Apps=new List<Entry>{new Entry{Name="Mod Organizer 2",Path="mo2.lnk",LaunchTarget=@"C:\Tools\ModOrganizer.exe"}}};FeatureRules.Move(learned,new[]{"mo2.lnk"},"游戏");Require(AutoGroupRules.Guess(new Entry{Name="ModOrganizer",Path="new.lnk",LaunchTarget=@"C:\Tools\ModOrganizer.exe"},learned)=="游戏","manual correction learned for future matching executable");FeatureRules.RenameGroup(learned,"游戏","我的游戏");Require(AutoGroupRules.Guess(learned.Apps[0],learned)=="我的游戏","learned rules follow group rename");FeatureRules.DeleteGroup(learned,"我的游戏");Require(learned.LearnedGroups.Count==0,"deleted group clears obsolete learned rules");
  string startupFolder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(startupFolder);try{foreach(string name in new[]{"Game.exe","unins000.exe","Updater.exe","library.dll","Game.url","Site.url"})File.WriteAllText(System.IO.Path.Combine(startupFolder,name),name=="Game.url"?"[InternetShortcut]\nURL=steam://rungameid/99999":name=="Site.url"?"[InternetShortcut]\nURL=https://example.com":"");var scan=ImportScanner.Scan(new[]{startupFolder},false,CancellationToken.None,2500,10000,true);Require(scan.Paths.Count==2&&scan.Paths.Any(x=>x.EndsWith("Game.exe"))&&scan.Paths.Any(x=>x.EndsWith("Game.url")),"launcher-only scan retains game URI, filters helper exe/DLL/web bookmark");}finally{Directory.Delete(startupFolder,true);}
  var smallPanel=new WidgetPanelPosition{Id=0,Width=100,Height=100};smallPanel.Normalize();Require(smallPanel.Width==360,"minimum readable panel width migration");Require(WidgetSizing.MinimumHeight(300,900)==268&&WidgetSizing.MinimumHeight(100,900)==200&&WidgetSizing.MinimumHeight(1600,700)==700,"content-dependent minimum height bounded by screen");
  foreach(string title in new[]{"火绒安全","360安全卫士","鲁大师","腾讯电脑管家"})Require(AutoGroupRules.Guess(new Entry{Name=title,Path=title+".lnk"})=="安全维护","Chinese security category: "+title);
  foreach(string title in new[]{"百度网盘","阿里云盘","夸克网盘","迅雷"})Require(AutoGroupRules.Guess(new Entry{Name=title,Path=title+".lnk"})=="网盘下载","Chinese cloud category: "+title);
  Require(AutoGroupRules.Guess(new Entry{Name="QQ浏览器",Path="QQBrowser.exe"})=="浏览器"&&AutoGroupRules.Guess(new Entry{Name="腾讯会议",Path="wemeet.exe"})=="办公"&&AutoGroupRules.Guess(new Entry{Name="同花顺",Path="hexin.exe"})=="金融财务"&&AutoGroupRules.Guess(new Entry{Name="希沃白板",Path="seewo.exe"})=="学习阅读","Chinese common apps");
  var primary=new MediaSnapshot{Available=true,Key="player|song",Source="player",Title="song",Playing=true,Duration=0,Backend="GSMTC",CanNext=true};
  var extra=new MediaSnapshot{Available=true,Key="player|song",Source="player",Duration=180,Position=20,MaxSeek=180,Rate=1,CanSeek=true,Cover="artwork",CoverStatus="NPSMLib thumbnail",Backend="NPSMLib isolated"};
  var supplemented=MediaFallback.Merge(primary,extra);Require(supplemented.Duration==180&&supplemented.CanSeek&&supplemented.Cover=="artwork"&&supplemented.Playing&&supplemented.CanNext&&primary.Duration==0,"isolated supplement merges without replacing standard controls or mutating input");
  extra.Key="other song";Require(MediaFallback.Merge(primary,extra).Duration==0,"reject stale supplemental song");extra.Key=primary.Key;extra.Source="other player";Require(MediaFallback.Merge(primary,extra).Cover==null,"reject supplemental source mismatch");extra.Source=primary.Source;extra.Available=false;Require(MediaFallback.Merge(primary,extra).Title=="song","native failure retains standard metadata");extra.Available=true;primary.Duration=240;primary.CanSeek=true;primary.Cover="standard artwork";Require(MediaFallback.Merge(primary,extra).Duration==240&&MediaFallback.Merge(primary,extra).Cover=="standard artwork","prefer complete standard timeline and cover");
  using(var stream=new MemoryStream(Encoding.UTF8.GetBytes("{\"Music\":true}"))){var migrated=(WidgetOptions)new DataContractJsonSerializer(typeof(WidgetOptions)).ReadObject(stream);Require(migrated.AutoNativeMediaCompatibility,"automatic media supplement legacy migration");}
  var chineseRequest=new MediaRequest{Command="refresh",Source="cloudmusic.exe",Title="再见",Artist="张震岳",Album="思念是一种病",Key="cloudmusic.exe|再见|张震岳|思念是一种病",Playing=true};
  string chineseLine;using(var wire=new MemoryStream()){MediaWireProtocol.Write(wire,chineseRequest);byte[] bytes=wire.ToArray();Require(bytes[0]==123&&bytes[bytes.Length-1]==10,"UTF8 wire uses no BOM and newline framing");chineseLine=new UTF8Encoding(false,true).GetString(bytes);Require(chineseLine.Contains("再见")&&chineseLine.Contains("张震岳"),"Chinese metadata encoded directly as UTF8");var restored=MediaWireProtocol.ReadRequest(chineseLine.TrimEnd('\n'));Require(restored.Title==chineseRequest.Title&&restored.Artist==chineseRequest.Artist&&restored.Key==chineseRequest.Key&&restored.Playing,"Chinese request wire roundtrip");}
  int calls=0;using(var output=new MemoryStream()){MediaWireProtocol.Loop(new StringReader("bad json\n"+chineseLine+"{\"Command\":\"exit\"}\n"+chineseLine),output,r=>{calls++;return new MediaSnapshot{Available=true,Key=r.Key,Source=r.Source,Title=r.Title,Status="OK",Position=62,Duration=180,Backend="Netease UIA"};});string[] lines=Encoding.UTF8.GetString(output.ToArray()).TrimEnd('\n').Split('\n');Require(lines.Length==2&&calls==1,"pipe loop handles malformed frame and exit without losing next frame");using(var reply=new MemoryStream(Encoding.UTF8.GetBytes(lines[1]))){var restored=(MediaSnapshot)new DataContractJsonSerializer(typeof(MediaSnapshot)).ReadObject(reply);Require(restored.Title=="再见"&&restored.Key==chineseRequest.Key&&restored.Duration==180,"Chinese reply and real measured progress wire");}}
  double ncmElapsed,ncmTotal;Require(NeteaseTimelineRules.Progress("01:23 / 04:56",out ncmElapsed,out ncmTotal)&&ncmElapsed==83&&ncmTotal==296,"NetEase UI progress text");
  Require(NeteaseTimelineRules.Progress("０１：０２｜０５：００",out ncmElapsed,out ncmTotal)&&ncmElapsed==62&&ncmTotal==300,"fullwidth progress text");
  Require(NeteaseTimelineRules.Progress("01:00:00 / 02:30:00",out ncmElapsed,out ncmTotal)&&ncmElapsed==3600&&ncmTotal==9000,"hour progress");
  foreach(string invalid in new[]{"01:60 / 04:00","05:00 / 04:00","00:00 / 00:00","Track 01:02","1:20/3:20/4:20","01:02 / bad","-1:02 / 03:00"})Require(!NeteaseTimelineRules.Progress(invalid,out ncmElapsed,out ncmTotal),"reject nonprogress or invalid time: "+invalid);
  Require(NeteaseTimelineRules.WindowTitle("On a Night Like This - Kylie Minogue","On a Night Like This")&&!NeteaseTimelineRules.WindowTitle("Other song - Singer","On a Night Like This")&&!NeteaseTimelineRules.WindowTitle("Song Extended - Singer","Song")&&NeteaseTimelineRules.Source("cloudmusic.exe")&&!NeteaseTimelineRules.Source("QQMusic.exe"),"NetEase process and current title match");
  Require(NeteaseTimelineRules.RangeTarget(60,240,0,100)==25&&NeteaseTimelineRules.RangeTarget(600,240,10,1010)==1010,"seek maps seconds to exposed UI range");
  extra.Key=primary.Key;extra.Source=primary.Source;extra.Backend="Netease UIA";extra.Rate=0;primary.Duration=0;primary.CanSeek=false;Require(MediaFallback.Merge(primary,extra).Backend=="Netease UIA"&&MediaFallback.Merge(primary,extra).Rate==0,"UI timeline route with measured position and no invented clock");
  Console.WriteLine("PASS: legacy/batch tests, seven hotkeys/migration/conflict/rollback, groups/workspaces/schemes, placement/auto-hide policy, widgets/weather, fusion/separation, persistent playlist, notes/backup, metadata/startup rules and component lifecycle, timeline/seek queue, dimensions/transparency and automatic grouping and alarms/countdown, offline aliases/launch filtering/learned corrections and adaptive minimum sizing.");return 0;
 }
}

}
