using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

[assembly: System.Reflection.AssemblyTitle("GlassBoard")]
[assembly: System.Reflection.AssemblyVersion("0.2.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.2.0.0")]

namespace GlassBoard {
    public static class LayoutRules {
        public static int Pages(int count,int capacity) {
            if(count<0 || capacity<1)throw new ArgumentOutOfRangeException();
            return Math.Max(1,(count+capacity-1)/capacity);
        }
        public static bool Matches(string name,string query) {
            return (query??"").Split(new char[]{' '},StringSplitOptions.RemoveEmptyEntries).All(word=>(name??"").IndexOf(word,StringComparison.CurrentCultureIgnoreCase)>=0);
        }
        public static double GlassOpacity(double value) {return double.IsNaN(value)||double.IsInfinity(value)?72:Math.Max(0,Math.Min(100,value));}
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
        [DataMember(EmitDefaultValue=false)] public string LaunchTarget,LaunchArguments,ProductInfo,WindowTarget;
        [DataMember(EmitDefaultValue=false)] public bool DesktopAuto;
    }
    [DataContract] public class Preferences {
        [DataMember] public List<Entry> Apps = new List<Entry>();
        [DataMember(EmitDefaultValue=false)] public List<string> DockOrder=new List<string>();
        [DataMember] public int FormatVersion;
        [DataMember(EmitDefaultValue=false)] public long SaveRevision;
        [DataMember] public bool Light = false;
        [DataMember] public bool ReducedMotionBaselineApplied;
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
        [DataMember] public ControlCenterOptions ControlCenter=new ControlCenterOptions();
        [DataMember] public bool AutoGrouping=true;
        [DataMember] public bool AutoCheckUpdates=false;
        [DataMember] public string StartupTiming="priority";
        [DataMember] public bool UpdateDirectConnection=false;
        [DataMember] public bool BoardDismissOnDeactivate=false;
        [DataMember] public bool BoardAlwaysOnTop=false;
        [DataMember] public bool AllDesktopLayer=false,BoardPageMotion=true;
        [DataMember] public string BoardPageStyle="slide";
        [DataMember] public bool BoardHideSearch,BoardHideGroups,BoardHideFolders;
        [DataMember] public bool GameMode=false,AutoGameMode=false;
        [DataMember] public MaterialOptions BoardMaterial=new MaterialOptions();
        [DataMember(EmitDefaultValue=false)] public List<string> RemovedIdentities=new List<string>();
        [DataMember] public long LastUpdateCheckUtc;
        [DataMember] public Dictionary<string,string> LearnedGroups=new Dictionary<string,string>();
        [DataMember] public bool DesktopAutoSync=true,DesktopFolders=true,DesktopDocs=true;
        [DataMember] public string DesktopFolderGroup="文件夹",DesktopDocsGroup="文档";
        [DataMember] public bool DesktopAutoRevealed;
        [OnDeserializing] void Defaults(StreamingContext context){AutoGrouping=true;BoardIconSize=48;BoardIconGap=5;BoardPageMotion=true;BoardPageStyle="slide";DesktopAutoSync=true;DesktopFolders=true;DesktopDocs=true;DesktopFolderGroup="文件夹";DesktopDocsGroup="文档";}
        public bool HotkeyMigrationConflict;
        public void Normalize() {
            AutoCheckUpdates=false;UpdateDirectConnection=false;LastUpdateCheckUtc=0;if(BoardPageStyle!="fade"&&BoardPageStyle!="slide"&&BoardPageStyle!="scale")BoardPageStyle="slide";
            if(BoardMaterial==null)BoardMaterial=new MaterialOptions();BoardMaterial.Normalize();
            if(LearnedGroups==null)LearnedGroups=new Dictionary<string,string>();if(RemovedIdentities==null)RemovedIdentities=new List<string>();
            if(Apps==null)Apps=new List<Entry>();
            Apps=Apps.Where(a=>a!=null&&!string.IsNullOrWhiteSpace(a.Path)).ToList();
            foreach(var a in Apps){if(string.IsNullOrWhiteSpace(a.Name))a.Name=System.IO.Path.GetFileNameWithoutExtension(a.Path);FeatureRules.NormalizeMemberships(a);}
            Apps=EntryRules.NormalizeEntries(Apps);RemovalRules.ReconcileActive(this);DockOrderRules.Normalize(this);
            BoardIconSize=LayoutRules.Clamp(BoardIconSize,28,80,48);BoardIconGap=LayoutRules.Clamp(BoardIconGap,0,20,5);
            DockGap=LayoutRules.Clamp(DockGap,0,160,12);PanelSize=Math.Max(0,Math.Min(2,PanelSize));
            OpacityPercent=LayoutRules.GlassOpacity(OpacityPercent??72);if(Removed==null)Removed=new List<string>();
            bool oldKeys=Hotkeys!=null&&Hotkeys.All(k=>k==null||k.ActionId!=7);Hotkeys=HotkeyRules.Normalize(Hotkeys);HotkeyMigrationConflict=oldKeys&&!Hotkeys[6].Enabled;
            if(Dock==null)Dock=new DockOptions{OpacityPercent=OpacityPercent.Value};Dock.Normalize();DockLayoutRules.SyncApps(this);
            Groups=FeatureRules.Groups(Groups,Apps);
            if(string.IsNullOrWhiteSpace(DesktopFolderGroup))DesktopFolderGroup=DesktopAutoRules.DefaultFolderGroup;
            if(string.IsNullOrWhiteSpace(DesktopDocsGroup))DesktopDocsGroup=DesktopAutoRules.DefaultDocsGroup;
            if(DesktopFolders&&!Groups.Any(g=>string.Equals((g??"").Trim(),DesktopFolderGroup,StringComparison.OrdinalIgnoreCase)))Groups.Insert(0,DesktopFolderGroup);
            if(DesktopDocs&&!string.Equals(DesktopDocsGroup,DesktopFolderGroup,StringComparison.OrdinalIgnoreCase)&&!Groups.Any(g=>string.Equals((g??"").Trim(),DesktopDocsGroup,StringComparison.OrdinalIgnoreCase)))Groups.Insert(0,DesktopDocsGroup);
            if(DesktopAutoSync&&(DesktopFolders||DesktopDocs)&&!DesktopAutoRevealed){DesktopAutoRevealed=true;if(BoardHideGroups)BoardHideGroups=false;}
            if(Workspaces==null)Workspaces=new List<Workspace>();
            Workspaces=Workspaces.Where(x=>x!=null&&!string.IsNullOrWhiteSpace(x.Name)).ToList();foreach(var work in Workspaces)work.Normalize();
            StartupTiming=StartupRules.Mode(StartupTiming);
            if(ControlCenter==null){ControlCenter=new ControlCenterOptions();Dock.ShowNetwork=false;}ControlCenter.Normalize();
            if(Placement==null)Placement=new PlacementOptions();Placement.Normalize();if(Widgets==null)Widgets=new WidgetOptions();Widgets.Normalize();
        }
    }
    [DataContract] public class DockOptions {
        [DataMember] public double IconSize=44,Spacing=6,Padding=16,Radius=28,OpacityPercent=72,Zoom=1.2,Offset=0,MotionLevel=1;
        [DataMember] public double VerticalPadding=4;
        [DataMember] public bool UseScreenBounds=true;
        [DataMember] public int Alignment=1,Accent=0,MaxPinned=9,ComputerStyle=0;
        [DataMember] public bool ShowComputer=true,ShowLabels=false,ShowSeparator=true,AlwaysOnTop=false,MotionEnabled=true;
        [DataMember] public bool AutoHide=false,ShowRunning=true;
        [DataMember] public bool Reflection=false,DragMotion=true;
        [DataMember] public double ReflectionOpacity=45,ReflectionLength=38,ReflectionAngle=0,ReflectionBlur=1.5,ReflectionGap=2,ReflectionFade=85;
        [DataMember] public bool SpringReveal=true;
        [DataMember] public bool? OpeningAnimation=true,HoverAnimation=true;
        [DataMember] public string OpenAnimationStyle="fade",HoverAnimationStyle="zoom-lift";
        [DataMember] public bool ShowRecycleBin=false,RespectFullscreen=true;
        public bool OpenMotion{get{return OpeningAnimation??MotionEnabled;}}
        public bool HoverMotion{get{return HoverAnimation??MotionEnabled;}}
        [DataMember] public bool ShowPower=false,ShowNetwork=false;
        [DataMember] public bool ShowSettings=true;
        [DataMember] public List<string> ItemOrder=new List<string>(),RunningOrder=new List<string>();
        [DataMember] public bool ShowShortcutOverlays=false,SharperIcons=false;
        [DataMember] public MaterialOptions Material=new MaterialOptions();
        [DataMember] public double HideDelay=900,EdgeRange=12,BlurRadius=18,EdgeStrength=1;
        [OnDeserializing] void Defaults(StreamingContext context) {
            IconSize=44;Spacing=6;Padding=16;Radius=28;OpacityPercent=72;Zoom=1.2;Offset=0;MotionLevel=1;OpeningAnimation=HoverAnimation=null;OpenAnimationStyle="fade";HoverAnimationStyle="zoom-lift";RespectFullscreen=true;
            VerticalPadding=4;UseScreenBounds=true;
            Alignment=1;Accent=0;MaxPinned=9;ComputerStyle=0;ShowComputer=true;ShowLabels=false;ShowSeparator=true;AlwaysOnTop=false;MotionEnabled=true;
            ShowSettings=true;ItemOrder=new List<string>();RunningOrder=new List<string>();AutoHide=false;ShowRunning=true;SpringReveal=true;ShowShortcutOverlays=false;SharperIcons=false;HideDelay=900;EdgeRange=12;BlurRadius=18;EdgeStrength=1;Reflection=false;DragMotion=true;ReflectionOpacity=45;ReflectionLength=38;ReflectionAngle=0;ReflectionBlur=1.5;ReflectionGap=2;ReflectionFade=85;
        }
        public DockOptions Copy() {var copy=(DockOptions)MemberwiseClone();copy.OpenAnimationStyle=InteractionRules.OpenStyle(OpenAnimationStyle);HoverAnimationStyle=InteractionRules.HoverStyle(HoverAnimationStyle);
            ItemOrder=(ItemOrder??new List<string>()).ToList();copy.RunningOrder=(RunningOrder??new List<string>()).ToList();copy.Material=(Material??new MaterialOptions()).Copy();return copy;}
        public void Normalize() {
            OpenAnimationStyle=InteractionRules.OpenStyle(OpenAnimationStyle);HoverAnimationStyle=InteractionRules.HoverStyle(HoverAnimationStyle);
            ItemOrder=(ItemOrder??new List<string>()).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToList();RunningOrder=(RunningOrder??new List<string>()).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Take(200).ToList();if(Material==null)Material=new MaterialOptions();Material.Normalize();
            IconSize=LayoutRules.Clamp(IconSize,32,64,44);Spacing=LayoutRules.Clamp(Spacing,0,20,6);Padding=LayoutRules.Clamp(Padding,8,28,16);
            VerticalPadding=LayoutRules.Clamp(VerticalPadding,0,28,4);
            Radius=LayoutRules.Clamp(Radius,0,48,28);OpacityPercent=LayoutRules.GlassOpacity(OpacityPercent);Zoom=LayoutRules.Clamp(Zoom,1,1.6,1.2);Offset=LayoutRules.Clamp(Offset,-500,500,0);
            MotionLevel=LayoutRules.Clamp(MotionLevel,.5,1.5,1);ReflectionOpacity=LayoutRules.Clamp(ReflectionOpacity,0,100,45);ReflectionLength=LayoutRules.Clamp(ReflectionLength,15,75,38);ReflectionAngle=LayoutRules.Clamp(ReflectionAngle,-40,40,0);ReflectionBlur=LayoutRules.Clamp(ReflectionBlur,0,6,1.5);ReflectionGap=LayoutRules.Clamp(ReflectionGap,0,12,2);ReflectionFade=LayoutRules.Clamp(ReflectionFade,20,100,85);
            HideDelay=LayoutRules.Clamp(HideDelay,200,3000,900);EdgeRange=LayoutRules.Clamp(EdgeRange,3,48,12);BlurRadius=LayoutRules.Clamp(BlurRadius,0,40,18);EdgeStrength=LayoutRules.Clamp(EdgeStrength,.2,2,1);
            Alignment=Math.Max(0,Math.Min(2,Alignment));Accent=Math.Max(0,Math.Min(5,Accent));MaxPinned=Math.Max(3,Math.Min(20,MaxPinned));ComputerStyle=Math.Max(0,Math.Min(1,ComputerStyle));
        }
        public int VisiblePinned(double availableWidth,int count) {
            int fixedSlots=1+(ShowSettings?1:0)+(ShowComputer?1:0)+(ShowPower?1:0)+(ShowNetwork?1:0)+(ShowRecycleBin?1:0);double cell=IconSize+16+Spacing;
            int fit=Math.Max(0,(int)Math.Floor((availableWidth-28-HoverPadding()*2-(ShowSeparator?17:0))/cell)-fixedSlots);
            return Math.Min(count,Math.Min(MaxPinned,fit));
        }
        public double HoverPadding() {return Math.Max(Math.Max(Padding,DockReflection.SidePadding(this)),(IconSize+16)*(Math.Min(1.6,1+(Zoom-1)*MotionLevel)-1)/2+2);}
        public double WidthFor(int pinnedCount) {return 28+HoverPadding()*2+(1+(ShowSettings?1:0)+(ShowComputer?1:0)+(ShowPower?1:0)+(ShowNetwork?1:0)+(ShowRecycleBin?1:0)+pinnedCount)*(IconSize+16+Spacing)+(ShowSeparator?17:0);}
    }
    public class RemovedItem {public Entry Entry;public int Index;public bool WasRemoved,MembershipOnly;public string RemovedGroup;}
    public class RemovalBatch {public List<RemovedItem> Items=new List<RemovedItem>();}
    public static class EntryRules {
        public static RemovalBatch Remove(Preferences prefs,IEnumerable<string> paths,string group=null) {
            var wanted=new HashSet<string>(paths,StringComparer.OrdinalIgnoreCase);var batch=new RemovalBatch();
            for(int i=0;i<prefs.Apps.Count;i++) {var a=prefs.Apps[i];if(!wanted.Contains(a.Path))continue;if(group!=null&&group!="全部"&&FeatureRules.InGroup(a,group)&&FeatureRules.Memberships(a).Count()>1){batch.Items.Add(new RemovedItem{Entry=FeatureRules.Copy(a),Index=i,MembershipOnly=true,RemovedGroup=group});FeatureRules.RemoveMembership(a,group);wanted.Remove(a.Path);}else batch.Items.Add(new RemovedItem{Entry=a,Index=i,WasRemoved=prefs.Removed.Any(p=>string.Equals(p,a.Path,StringComparison.OrdinalIgnoreCase))});}
            prefs.Apps.RemoveAll(a=>wanted.Contains(a.Path));
            foreach(var item in batch.Items)if(!item.MembershipOnly){if(!item.WasRemoved)prefs.Removed.Add(item.Entry.Path);RemovalRules.Remember(prefs,item.Entry);}
            return batch;
        }
        public static int Undo(Preferences prefs,RemovalBatch batch) {
            int restored=0;if(batch==null)return 0;
            foreach(var item in batch.Items.OrderBy(x=>x.Index)) {
                if(item.MembershipOnly){var existing=prefs.Apps.FirstOrDefault(a=>string.Equals(a.Path,item.Entry.Path,StringComparison.OrdinalIgnoreCase));if(existing!=null){if(!string.IsNullOrWhiteSpace(item.RemovedGroup))FeatureRules.AddMembership(existing,item.RemovedGroup);else foreach(var name in FeatureRules.Memberships(item.Entry))FeatureRules.AddMembership(existing,name);restored++;continue;}}
                if(!prefs.Apps.Any(a=>string.Equals(a.Path,item.Entry.Path,StringComparison.OrdinalIgnoreCase))) {prefs.Apps.Insert(Math.Min(item.Index,prefs.Apps.Count),item.Entry);restored++;}
                if(!item.WasRemoved)RemovalRules.Forget(prefs,item.Entry);
            }prefs.Groups=FeatureRules.Groups(prefs.Groups,prefs.Apps);return restored;
        }
        public static List<Entry> NormalizeEntries(IEnumerable<Entry> apps){var result=new List<Entry>();var byPath=new Dictionary<string,Entry>(StringComparer.OrdinalIgnoreCase);foreach(var a in apps??new Entry[0]){if(a==null||string.IsNullOrWhiteSpace(a.Path))continue;if(Directory.Exists(a.Path)){a.Path=EntryPathRules.Normalize(a.Path);a.IsFolder=true;}FeatureRules.NormalizeMemberships(a);Entry existing;if(!byPath.TryGetValue(a.Path,out existing)){byPath.Add(a.Path,a);result.Add(a);continue;}foreach(var group in FeatureRules.Memberships(a))FeatureRules.AddMembership(existing,group);existing.Pinned|=a.Pinned;existing.IsFolder|=a.IsFolder;if(string.IsNullOrEmpty(existing.IconPath))existing.IconPath=a.IconPath;if(string.IsNullOrEmpty(existing.LaunchTarget)){existing.LaunchTarget=a.LaunchTarget;existing.LaunchArguments=a.LaunchArguments;}if(string.IsNullOrEmpty(existing.ProductInfo))existing.ProductInfo=a.ProductInfo;if(string.IsNullOrEmpty(existing.WindowTarget))existing.WindowTarget=a.WindowTarget;}return result;}
        public static bool Importable(string path) {string ext=System.IO.Path.GetExtension(path).ToLowerInvariant();return ext==".lnk"||ext==".appref-ms"||ext==".exe"||ext==".url"||ext==".cmd"||ext==".bat"||DocumentRules.Supported(path);}
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
    public static partial class Store {
        public static readonly string Folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GlassBoard");
        public static readonly string FileName = System.IO.Path.Combine(Folder, "layout.json");
        public static void Trace(string message){try{Directory.CreateDirectory(Folder);string path=System.IO.Path.Combine(Folder,"session.log");if(File.Exists(path)&&new FileInfo(path).Length>1024*1024)File.Move(path,path+"."+DateTime.Now.Ticks);File.AppendAllText(path,DateTime.Now.ToString("s")+" ["+System.Reflection.Assembly.GetExecutingAssembly().GetName().Version+"] "+message+Environment.NewLine);}catch{}}
        public static void Log(Exception ex) {
            try { Directory.CreateDirectory(Folder); File.AppendAllText(System.IO.Path.Combine(Folder,"error.log"),DateTime.Now.ToString("s")+" ["+System.Reflection.Assembly.GetExecutingAssembly().GetName().Version+"] "+ex+Environment.NewLine); } catch {}
        }
    }
    public static class Native {
        public const int RestoreMessage=0x8000+47;
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls,string title);
        [DllImport("user32.dll",SetLastError=true)] static extern bool PostMessage(IntPtr h,int message,IntPtr w,IntPtr l);
        public static bool RequestRestore() {var h=FindWindow(null,"GlassBoard Dock");return h!=IntPtr.Zero&&PostMessage(h,RestoreMessage,IntPtr.Zero,IntPtr.Zero);}
        [DllImport("user32.dll",SetLastError=true)] public static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
        [DllImport("user32.dll",SetLastError=true)] public static extern bool SetWindowDisplayAffinity(IntPtr h,uint value);
        [DllImport("user32.dll",SetLastError=true)] public static extern bool GetWindowDisplayAffinity(IntPtr h,out uint value);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string path,uint attr,out SHFILEINFO info,uint cb,uint flags);
        [DllImport("comctl32.dll")] static extern IntPtr ImageList_GetIcon(IntPtr list,int index,uint flags);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct SHFILEINFO {
            public IntPtr hIcon; public int iIcon; public uint attributes;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string displayName;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)] public string typeName;
        }
        static readonly BoundedCache<string,ImageSource> icons=new BoundedCache<string,ImageSource>(192,StringComparer.OrdinalIgnoreCase);
        public static ImageSource Icon(string path,bool showShortcutOverlays=false,bool sharper=false) {
            if(string.IsNullOrWhiteSpace(path))return null;string key=(sharper?"sharp/":"normal/")+(showShortcutOverlays?"overlay/":"plain/")+path;ImageSource cached;if(icons.TryGet(key,out cached))return cached;
            SHFILEINFO info;IntPtr icon;
            if(showShortcutOverlays){
                SHGetFileInfo(path,0,out info,(uint)Marshal.SizeOf(typeof(SHFILEINFO)),sharper?0x4160u:0x100u);icon=info.hIcon;
                if(sharper&&icon!=IntPtr.Zero){var large=ShellIconImages.Read(info.iIcon&0xFFFFFF,((uint)info.iIcon>>24)<<8);if(large!=null){if(icon!=IntPtr.Zero)DestroyIcon(icon);icons.Put(key,large);return large;}}
            }
            else {
                // SHGFI_SYSICONINDEX retrieves the link's own custom icon index
                // without composing shortcut/cloud overlays. ILD_TRANSPARENT
                // copies only that base image; the system image list is shared.
                var list=SHGetFileInfo(path,0,out info,(uint)Marshal.SizeOf(typeof(SHFILEINFO)),0x4000);
                if(sharper&&list!=IntPtr.Zero){var large=ShellIconImages.Read(info.iIcon);if(large!=null){icons.Put(key,large);return large;}}
                icon=list==IntPtr.Zero?IntPtr.Zero:ImageList_GetIcon(list,info.iIcon,1);
            }
            if(icon==IntPtr.Zero) return null;
            try {
                var source=Imaging.CreateBitmapSourceFromHIcon(icon,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(64,64));
                source.Freeze(); icons.Put(key,source); return source;
            } catch { return null; } finally { DestroyIcon(icon); }
        }
        public static void ClearIconCache(){icons.Clear();}
        public static void Computer() { Process.Start(new ProcessStartInfo("explorer.exe","shell:MyComputerFolder"){UseShellExecute=true}); }
    }
    [DataContract] public class ShellState {
        [DataMember] public long Handle;
        [DataMember] public string ClassName;
        [DataMember] public bool Visible;
    }
    public static class DesktopVisibility {
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls,string title);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h,int cmd);
        [DllImport("user32.dll",SetLastError=true)] static extern bool ShowWindowAsync(IntPtr h,int cmd);
        [DllImport("user32.dll",EntryPoint="GetWindowLongW")] static extern int GetWindowLong(IntPtr h,int index);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,StringBuilder text,int count);
        delegate bool EnumCallback(IntPtr h,IntPtr data);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumCallback callback,IntPtr data);
        [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent,EnumCallback callback,IntPtr data);
        static readonly string RecoveryFile=System.IO.Path.Combine(Store.Folder,"shell-recovery.json");
        static Dictionary<long,ShellState> original=new Dictionary<long,ShellState>();
        static bool? taskbarHidden,iconsHidden;
        static readonly DispatcherTimer maintenance=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(750)};
        static DesktopVisibility() {maintenance.Tick+=(s,e)=>Maintain();}
        static string Class(IntPtr h) {var b=new StringBuilder(256);GetClassName(h,b,256);return b.ToString();}
        static List<IntPtr> Taskbars() {
            var result=new List<IntPtr>();var main=FindWindow("Shell_TrayWnd",null);if(main!=IntPtr.Zero)result.Add(main);
            EnumWindows((h,d)=>{string cls=Class(h);if((cls=="Shell_TrayWnd"||cls=="Shell_SecondaryTrayWnd")&&!result.Contains(h))result.Add(h);return true;},IntPtr.Zero);return result;
        }
        static List<IntPtr> DesktopLists() {
            var result=new HashSet<IntPtr>();Action<IntPtr> addView=view=>{
                result.Add(view);EnumChildWindows(view,(child,data)=>{if(Class(child)=="SysListView32")result.Add(child);return true;},IntPtr.Zero);
            };
            EnumWindows((h,d)=> {
                string cls=Class(h);var view=FindWindowEx(h,IntPtr.Zero,"SHELLDLL_DefView",null);if(view!=IntPtr.Zero)addView(view);
                if(cls=="Progman"||cls=="WorkerW")EnumChildWindows(h,(child,data)=>{if(Class(child)=="SHELLDLL_DefView")addView(child);return true;},IntPtr.Zero);
                return true;
            },IntPtr.Zero);return result.OrderBy(h=>Class(h)=="SHELLDLL_DefView"?0:1).ToList();
        }
        static bool MatchesSaved(ShellState state) {
            var h=new IntPtr(state.Handle);if(!IsWindow(h)||Class(h)!=state.ClassName)return false;
            if(state.ClassName=="Shell_TrayWnd"||state.ClassName=="Shell_SecondaryTrayWnd")return Taskbars().Contains(h);
            return (state.ClassName=="SysListView32"||state.ClassName=="SHELLDLL_DefView")&&DesktopLists().Contains(h);
        }
        static void Remember(IEnumerable<IntPtr> handles) {
            bool changed=false;
            foreach(var h in handles)if(!original.ContainsKey(h.ToInt64())) {original[h.ToInt64()]=new ShellState{Handle=h.ToInt64(),ClassName=Class(h),Visible=(GetWindowLong(h,-16)&0x10000000)!=0};changed=true;}
            if(!changed)return;
            Directory.CreateDirectory(Store.Folder);
            string temp=RecoveryFile+".tmp";
            using(var s=File.Create(temp))new DataContractJsonSerializer(typeof(List<ShellState>)).WriteObject(s,original.Values.ToList());
            if(File.Exists(RecoveryFile))File.Replace(temp,RecoveryFile,null);else File.Move(temp,RecoveryFile);
        }
        public static bool? IsHidden(bool taskbar) {
            var targets=taskbar?Taskbars():DesktopLists();if(targets.Count==0)return null;
            var lists=targets.Where(h=>Class(h)=="SysListView32").ToList();if(!taskbar&&lists.Count>0)targets=lists;
            return !targets.Any(IsWindowVisible);
        }
        public static bool DesiredHidden(bool taskbar) {return (taskbar?taskbarHidden:iconsHidden)??IsHidden(taskbar)??false;}
        public static string SetHidden(bool taskbar,bool hide) {
            var handles=taskbar?Taskbars():DesktopLists();string noun=taskbar?"任务栏":"桌面图标";
            if(handles.Count==0)return "未找到"+noun+"，请确认 Windows Explorer 已运行。";
            // Journal raw WS_VISIBLE before hiding; ancestors can make a visible child appear hidden.
            int failed=0;Remember(handles);foreach(var h in handles)if(!ShowWindowAsync(h,hide?0:8)){failed++;Store.Log(new Win32Exception(Marshal.GetLastWin32Error(),"无法切换 "+Class(h)));}
            if(taskbar)taskbarHidden=hide;else iconsHidden=hide;
            if(taskbarHidden==true||iconsHidden==true)maintenance.Start();else maintenance.Stop();
            return failed>0?noun+"部分窗口切换失败，详情已记录在 error.log。":"已请求"+(hide?"隐藏":"显示")+noun;
        }
        public static string ToggleTaskbar() {return SetHidden(true,!DesiredHidden(true));}
        public static string ToggleIcons() {return SetHidden(false,!DesiredHidden(false));}
        static void Maintain() {
            try {
                foreach(bool taskbar in new bool[]{true,false})if((taskbar?taskbarHidden:iconsHidden)==true) {
                    var handles=taskbar?Taskbars():DesktopLists();if(handles.Count==0)continue;Remember(handles);
                    foreach(var h in handles)if(IsWindowVisible(h))ShowWindowAsync(h,0);
                }
            }catch(Exception ex){Store.Log(ex);maintenance.Stop();}
        }
        public static void Recover() {
            if(!File.Exists(RecoveryFile))return;
            using(var s=File.OpenRead(RecoveryFile)) {
                var states=(List<ShellState>)new DataContractJsonSerializer(typeof(List<ShellState>)).ReadObject(s);
                foreach(var state in states)if(MatchesSaved(state))ShowWindow(new IntPtr(state.Handle),state.Visible?8:0);
            }
            File.Delete(RecoveryFile);
        }
        public static void RestoreOriginal() {
            maintenance.Stop();taskbarHidden=null;iconsHidden=null;
            foreach(var state in original.Values)if(MatchesSaved(state))ShowWindow(new IntPtr(state.Handle),state.Visible?8:0);
            original.Clear();if(File.Exists(RecoveryFile))File.Delete(RecoveryFile);
        }
        public static void ShowAll() {
            maintenance.Stop();taskbarHidden=false;iconsHidden=false;
            var handles=Taskbars().Concat(DesktopLists()).ToArray();foreach(var h in handles)ShowWindow(h,8);
            original.Clear();if(File.Exists(RecoveryFile))File.Delete(RecoveryFile);
        }
    }
    public class GlassWindow : Window {
        protected Grid Root=new Grid();
        protected Border Surface=new Border();
        protected Grid ContentGrid=new Grid();
        protected bool LightTheme;
        protected double GlassPercent=72;
        protected bool MotionEnabled=true;
        protected double MotionLevel=1;
        protected double BlurRadius=18,EdgeStrength=1;
        protected MaterialOptions CurrentMaterial=new MaterialOptions();
        protected void ConfigureGlass(DockOptions options,MaterialOptions material=null){BlurRadius=options.BlurRadius;EdgeStrength=options.EdgeStrength;CurrentMaterial=(material??options.Material??new MaterialOptions()).Copy();}
        public void SetMaterial(MaterialOptions material){CurrentMaterial=material.Copy();SetGlassOpacity(GlassPercent);RefreshBackdropStyle();}
        protected double MaterialBlur{get{return CurrentMaterial.Kind==1?BlurRadius*.25:CurrentMaterial.Kind==2?Math.Min(40,BlurRadius*1.3):BlurRadius;}}
        protected static ControlTemplate GripTemplate(bool corner=false){var factory=new FrameworkElementFactory(corner?typeof(TextBlock):typeof(Border));if(corner){factory.SetValue(TextBlock.TextProperty,"◢");factory.SetValue(TextBlock.ForegroundProperty,new SolidColorBrush(Color.FromArgb(135,180,195,215)));factory.SetValue(TextBlock.FontSizeProperty,18.0);}else{factory.SetValue(Border.CornerRadiusProperty,new CornerRadius(3));factory.SetValue(Border.BackgroundProperty,new SolidColorBrush(Color.FromArgb(90,180,195,215)));}return new ControlTemplate(typeof(Thumb)){VisualTree=factory};}
        protected Brush TextBrush { get { return new SolidColorBrush(LightTheme?Color.FromRgb(25,28,34):Color.FromRgb(247,247,249)); } }
        protected Brush MutedBrush { get { return new SolidColorBrush(LightTheme?Color.FromRgb(65,69,77):Color.FromRgb(184,188,198)); } }
        public GlassWindow(double radius) {
            WindowStyle=WindowStyle.None; AllowsTransparency=true; Background=Brushes.Transparent;
            ResizeMode=ResizeMode.NoResize; ShowInTaskbar=false; Topmost=false;UseLayoutRounding=true;SnapsToDevicePixels=true;
            Content=Root; Root.Margin=new Thickness(14);
            IsVisibleChanged+=(s,e)=>{if(!IsVisible)return;var widget=this as WidgetWindow;if(this is DockWindow||widget!=null||this is ControlOrbWindow)AnimateOpen(widget!=null?Math.Min(140,60+widget.PanelId*25):this is ControlOrbWindow?40:0);};
            Surface.CornerRadius=new CornerRadius(radius); Surface.BorderThickness=new Thickness(1);
            Surface.Effect=new DropShadowEffect { BlurRadius=25,ShadowDepth=7,Opacity=.28,Color=Colors.Black };
            Root.Children.Add(Surface); Root.Children.Add(ContentGrid);
            // Keep all glass windows recordable. A capture-protected window can stop NVIDIA desktop recording.
            SourceInitialized+=(s,e)=> {if(this is DockWindow||this is BoardWindow||this is WidgetWindow||this is ControlOrbWindow||this is ControlCenterWindow||this is IslandWindow){DesktopWindowPolicy.Apply(this);DesktopPersistence.Attach(this);}try{if(!Native.SetWindowDisplayAffinity(new WindowInteropHelper(this).Handle,0))Store.Log(new Win32Exception(Marshal.GetLastWin32Error(),"SetWindowDisplayAffinity: allow desktop capture"));}catch(Exception ex){Store.Log(ex);}};
        }
        protected Button Button(string label,RoutedEventHandler click,double width=80) {
            var b=new Button {Content=label,Width=width,Height=36,Margin=new Thickness(4),Foreground=TextBrush,Cursor=Cursors.Hand};
            var factory=new FrameworkElementFactory(typeof(Border));
            factory.SetValue(Border.CornerRadiusProperty,new CornerRadius(12));
            factory.SetValue(Border.BackgroundProperty,new SolidColorBrush(Color.FromArgb(LightTheme?(byte)38:(byte)25,255,255,255)));
            factory.SetValue(Border.BorderBrushProperty,new SolidColorBrush(Color.FromArgb(42,255,255,255)));
            factory.SetValue(Border.BorderThicknessProperty,new Thickness(1));
            var presenter=new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center); factory.AppendChild(presenter);
            b.Template=new ControlTemplate(typeof(Button)) {VisualTree=factory};
            b.MouseEnter+=(s,e)=> {if(MotionEnabled)b.BeginAnimation(OpacityProperty,new DoubleAnimation(.78,TimeSpan.FromMilliseconds(120)));else{b.BeginAnimation(OpacityProperty,null);b.Opacity=.78;}};
            b.MouseLeave+=(s,e)=> {if(MotionEnabled)b.BeginAnimation(OpacityProperty,new DoubleAnimation(1,TimeSpan.FromMilliseconds(150)));else{b.BeginAnimation(OpacityProperty,null);b.Opacity=1;}};
            b.PreviewMouseLeftButtonDown+=(s,e)=>{if(MotionEnabled)b.BeginAnimation(OpacityProperty,new DoubleAnimation(.6,TimeSpan.FromMilliseconds(65)));else{b.BeginAnimation(OpacityProperty,null);b.Opacity=.6;}};
            b.PreviewMouseLeftButtonUp+=(s,e)=>{if(MotionEnabled)b.BeginAnimation(OpacityProperty,new DoubleAnimation(1,TimeSpan.FromMilliseconds(100)));else{b.BeginAnimation(OpacityProperty,null);b.Opacity=1;}};
            // Some buttons attach a handler after creation; WPF rejects a null routed-event handler.
            if(click!=null) b.Click+=click;
            return b;
        }
        string backdropCaptureKey;DateTime backdropCaptured=DateTime.MinValue;
        public void Backdrop(bool light) {
            LightTheme=light;
            SetGlassOpacity(GlassPercent);
            Surface.BorderBrush=GlassPercent==0?(Brush)Brushes.Transparent:new LinearGradientBrush(Color.FromArgb((byte)Math.Min(255,145*EdgeStrength),255,255,255),Color.FromArgb((byte)Math.Min(255,35*EdgeStrength),255,255,255),90);
            // A visible window must not photograph itself. Reuse its hidden-time backdrop and update the styling.
            if(IsVisible){RefreshBackdropStyle();return;}
            string captureKey=Left+"/"+Top+"/"+Width+"/"+Height;
            if(backdropCaptureKey==captureKey&&(DateTime.UtcNow-backdropCaptured).TotalSeconds<2&&Root.Children.OfType<Border>().Any(b=>b.Tag as string=="backdrop")){RefreshBackdropStyle();return;}
            Root.Children.OfType<Border>().Where(b=>b.Tag as string=="backdrop").ToList().ForEach(b=>Root.Children.Remove(b));
            if(this is BoardWindow||Width<40 || Height<40||GlassPercent==0) return;
            try {
                // Capture while this window is still hidden; no capture-protection flags or continuous capture.
                Matrix m=Matrix.Identity;
                new WindowInteropHelper(this).EnsureHandle();
                var src=PresentationSource.FromVisual(this); if(src!=null) m=src.CompositionTarget.TransformToDevice;
                int x=(int)Math.Round((Left+14)*m.M11),y=(int)Math.Round((Top+14)*m.M22);
                int w=Math.Max(1,(int)Math.Ceiling((Width-28)*m.M11)),h=Math.Max(1,(int)Math.Ceiling((Height-28)*m.M22));
                using(var bmp=new Drawing.Bitmap(w,h)) {
                    using(var g=Drawing.Graphics.FromImage(bmp)) g.CopyFromScreen(x,y,0,0,new Drawing.Size(w,h));
                    using(var small=new Drawing.Bitmap(bmp,Math.Max(1,w/8),Math.Max(1,h/8))) {
                        var data=small.LockBits(new Drawing.Rectangle(0,0,small.Width,small.Height),Drawing.Imaging.ImageLockMode.ReadOnly,Drawing.Imaging.PixelFormat.Format32bppArgb);
                        byte[] pixels=new byte[small.Width*small.Height*4];
                        try{for(int row=0;row<small.Height;row++)Marshal.Copy(IntPtr.Add(data.Scan0,row*data.Stride),pixels,row*small.Width*4,small.Width*4);}finally{small.UnlockBits(data);}
                        var image=BitmapSource.Create(small.Width,small.Height,96,96,PixelFormats.Bgra32,null,pixels,small.Width*4);image.Freeze();
                        var bg=new Border {Tag="backdrop",CornerRadius=Surface.CornerRadius,Background=new ImageBrush(image){Stretch=Stretch.Fill},Opacity=light?.8:.66};
                        bg.Clip=new RectangleGeometry(new Rect(0,0,Width-28,Height-28),Surface.CornerRadius.TopLeft,Surface.CornerRadius.TopLeft);
                        bg.Effect=new BlurEffect {Radius=MaterialBlur,RenderingBias=RenderingBias.Performance};
                        // Blur stays inside a rounded clipping host, leaving the corners transparent.
                        var host=new Border {Tag="backdrop",Clip=bg.Clip,Child=bg,Opacity=GlassPercent/100};
                        Root.Children.Insert(0,host);backdropCaptureKey=captureKey;backdropCaptured=DateTime.UtcNow;
                    }
                }
            } catch(Exception ex) { Store.Log(ex); }
        }
        void RefreshBackdropStyle(){foreach(var host in Root.Children.OfType<Border>().Where(b=>b.Tag as string=="backdrop")){var clip=new RectangleGeometry(new Rect(0,0,Math.Max(1,Width-28),Math.Max(1,Height-28)),Surface.CornerRadius.TopLeft,Surface.CornerRadius.TopLeft);host.Clip=clip;var bg=host.Child as Border;if(bg!=null){bg.Clip=clip;bg.CornerRadius=Surface.CornerRadius;bg.Opacity=LightTheme?.8:.66;bg.Effect=new BlurEffect{Radius=MaterialBlur,RenderingBias=RenderingBias.Performance};}}}
        protected void ReleaseBackdrop(){Root.Children.OfType<Border>().Where(b=>b.Tag as string=="backdrop").ToList().ForEach(b=>Root.Children.Remove(b));}
        public void SetGlassOpacity(double percent) {
            GlassPercent=LayoutRules.GlassOpacity(percent);
            Surface.Background=GlassMaterials.Fill(CurrentMaterial,LightTheme,GlassPercent);
            Surface.BorderBrush=GlassPercent==0?Brushes.Transparent:Polish.Line(LightTheme);Surface.Effect=GlassPercent==0||!CurrentMaterial.Shadow?null:new DropShadowEffect{BlurRadius=25,ShadowDepth=7,Opacity=.28,Color=Colors.Black};
            foreach(var b in Root.Children.OfType<Border>().Where(b=>b.Tag as string=="backdrop"))b.Opacity=GlassPercent/100;
            var material=Root.Children.OfType<Border>().FirstOrDefault(b=>b.Tag as string=="material");if(material==null){material=new Border{Tag="material",IsHitTestVisible=false};Root.Children.Insert(Math.Min(1,Root.Children.Count),material);}material.CornerRadius=Surface.CornerRadius;var effects=new Grid();material.Child=effects;material.Opacity=GlassPercent/100;
            if(CurrentMaterial.Gloss>0)effects.Children.Add(new Border{CornerRadius=Surface.CornerRadius,Background=new LinearGradientBrush(Color.FromArgb((byte)Math.Round(CurrentMaterial.Gloss*90),255,255,255),Colors.Transparent,90)});
            if(CurrentMaterial.Grain>0)effects.Children.Add(new Border{CornerRadius=Surface.CornerRadius,Background=new ImageBrush(GlassMaterials.Grain()){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,32,32)},Opacity=CurrentMaterial.Grain});
        }
        public virtual bool WantsTopmost{get{return Topmost;}}
        public virtual bool YieldToFullscreen{get{return true;}}
        public void RefreshLayer(){var handle=new WindowInteropHelper(this).Handle;if(handle==IntPtr.Zero)return;bool wanted=!DesktopPersistence.ForceDesktop(this)&&WantsTopmost&&(!YieldToFullscreen||!FullscreenState.Active(handle));if(Topmost!=wanted)Topmost=wanted;}
        protected string OpenStyle="fade";
        protected void AnimateOpen(int delayMs=0) {
            BeginAnimation(OpacityProperty,null);Opacity=1;Root.RenderTransform=Transform.Identity;
            ContentGrid.BeginAnimation(OpacityProperty,null);ContentGrid.Opacity=1;ContentGrid.RenderTransform=Transform.Identity;
            if(!MotionEnabled){ContentGrid.CacheMode=null;return;}
            string style=InteractionRules.OpenStyle(OpenStyle);bool software=(RenderCapability.Tier>>16)==0;
            var duration=TimeSpan.FromMilliseconds(software?100:style=="fade"?140:180);var ease=new CubicEase{EasingMode=EasingMode.EaseOut};
            int token=++openAnimationToken;ContentGrid.CacheMode=software||style=="fade"?null:new BitmapCache(1){SnapsToDevicePixels=true};
            var fade=new DoubleAnimation(.18,1,duration){FillBehavior=FillBehavior.Stop};
            fade.Completed+=(s,e)=>{if(token==openAnimationToken)ContentGrid.CacheMode=null;};ContentGrid.BeginAnimation(OpacityProperty,fade,HandoffBehavior.SnapshotAndReplace);
            if(software)return;
            if(style=="rise"||style=="slide"){
                var move=new TranslateTransform();ContentGrid.RenderTransform=move;
                move.BeginAnimation(style=="rise"?TranslateTransform.YProperty:TranslateTransform.XProperty,new DoubleAnimation(style=="rise"?8:12,0,duration){EasingFunction=ease,FillBehavior=FillBehavior.Stop});
            }else if(style=="zoom"){
                ContentGrid.RenderTransformOrigin=new Point(.5,.5);var scale=new ScaleTransform(1,1);ContentGrid.RenderTransform=scale;
                scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(.975,1,duration){EasingFunction=ease,FillBehavior=FillBehavior.Stop});
                scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(.975,1,duration){EasingFunction=ease,FillBehavior=FillBehavior.Stop});
            }
        }
        int openAnimationToken;
    }

    public partial class Controller {
        public Preferences Prefs;
        public DockWindow Dock;
        public BoardWindow Board;
        public Forms.NotifyIcon Tray;
        bool importing;
        public bool ShuttingDown;
        public bool CanUndoRemoval {get{return editHistory.CanUndo;}}
        readonly bool persistConfiguration;
        DesktopAutoSync autoSync;
        public Controller():this(true) {}
        internal Controller(bool desktop) {
            persistConfiguration=desktop;
            if(!desktop){Prefs=new Preferences();Board=new BoardWindow(this);Dock=new DockWindow(this);return;}
            Prefs=Store.Load();bool resetMotion=MotionPresets.ApplyInitialBaseline(Prefs);DesktopPersistence.DesktopOnly=Prefs.AllDesktopLayer;if(!string.IsNullOrEmpty(Store.RecoveryMessage))MessageBox.Show(Store.RecoveryMessage,"GlassBoard 配置恢复");if(Prefs.Removed.Count>0||Prefs.RemovedIdentities.Count>0)MigrateRemovalIdentities(); Board=new BoardWindow(this); Dock=new DockWindow(this);glassSuppressed=Prefs.GameMode||(Prefs.AutoGameMode&&FullscreenState.Active());
            Tray=new Forms.NotifyIcon {Text="GlassBoard v0.2.0 · 玻璃启动台",Icon=Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? Drawing.SystemIcons.Application,Visible=true};
            RefreshTrayMenu();
            Tray.DoubleClick+=(s,e)=>Toggle(true);
            Dock.Refresh(); if(!GlassSuppressed)Dock.Show();
            if(Prefs.HotkeyMigrationConflict){Tray.ShowBalloonTip(4500,"快捷键升级提示","原有组合与 Ctrl+Alt+H 冲突，已保留原设置并关闭新增项。可在设置中录制新组合。",Forms.ToolTipIcon.Info);Save();}
            RefreshWidgets();
            if(!Prefs.Imported) {Toggle(true);Import();}
            ApplyGameMode();if(resetMotion)Save();StartDesktopAutoSync();
        }
        string Shortcut(int id) {return HotkeyRules.Label(Prefs.Hotkeys.First(k=>k.ActionId==id));}
        public void RefreshTrayMenu() {
            var previous=Tray.ContextMenuStrip;var menu=new Forms.ContextMenuStrip();
            menu.Items.Add("打开应用面板   "+Shortcut(1),null,(s,e)=>Toggle(true));
            menu.Items.Add(Prefs.GameMode?"退出游戏模式":"进入游戏模式",null,(s,e)=>SetGameMode(!Prefs.GameMode));
            if(Prefs.AutoGameMode)menu.Items.Add("暂停自动游戏模式",null,(s,e)=>{Prefs.AutoGameMode=false;Prefs.GameMode=false;Save();ApplyGameMode();RefreshTrayMenu();});
            menu.Items.Add("添加软件   "+Shortcut(4),null,(s,e)=>AddFromAnywhere());
            menu.Items.Add("导入开始菜单应用",null,(s,e)=>Import());
            menu.Items.Add("外观与快捷键   "+Shortcut(5),null,(s,e)=>Settings());
            menu.Items.Add("隐藏 / 显示任务栏   "+Shortcut(2),null,(s,e)=>ShellAction(1));
            menu.Items.Add("隐藏 / 显示桌面图标   "+Shortcut(3),null,(s,e)=>ShellAction(2));
            menu.Items.Add("恢复任务栏与桌面图标   "+Shortcut(6),null,(s,e)=>ShellAction(3));
            menu.Items.Add("Windows 任务栏自动隐藏   "+Shortcut(7),null,(s,e)=>ToggleTaskbarAutoHide());
            menu.Items.Add("打开控制中心",null,(s,e)=>OpenControlCenter());menu.Items.Add("控制中心小球设置",null,(s,e)=>ControlCenterSettings(null));
            menu.Items.Add("备忘录记事板",null,(s,e)=>ShowNotes());menu.Items.Add("组件布局",null,(s,e)=>ConfigureWidgets());menu.Items.Add("恢复并开启全部组件",null,(s,e)=>RecoverWidgets());foreach(string component in WidgetRules.Kinds){string kind=component;menu.Items.Add("开启组件 · "+WidgetRules.Names[Array.IndexOf(WidgetRules.Kinds,kind)],null,(s,e)=>SetWidgetEnabled(kind,true));}
            menu.Items.Add("工作区管理",null,(s,e)=>ManageWorkspaces());foreach(var work in Prefs.Workspaces){var workspace=work;menu.Items.Add("打开工作区 · "+workspace.Name,null,(s,e)=>LaunchWorkspace(workspace));}
            menu.Items.Add("外观方案",null,(s,e)=>ManageSchemes());
            menu.Items.Add("打开配置文件夹",null,(s,e)=>Process.Start(new ProcessStartInfo(Store.Folder){UseShellExecute=true}));
            menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add("退出 GlassBoard",null,(s,e)=>Quit());
            Tray.ContextMenuStrip=menu;if(previous!=null)previous.Dispose();
        }
        public void Save() { if(!persistConfiguration)return;try { DockOrderRules.Normalize(Prefs);Store.Save(Prefs); } catch(Exception ex) {Store.Log(ex);MessageBox.Show("布局保存失败："+ex.Message,"GlassBoard");} }
        public void Refresh() {Save();Dock.Refresh();Board.SyncGroups();Board.Render();RefreshWidgets();}
        public void StartDesktopAutoSync(){if(!persistConfiguration)return;if(autoSync==null)autoSync=new DesktopAutoSync(this);autoSync.Start();}
        public void StopDesktopAutoSync(){if(autoSync!=null){autoSync.Stop();autoSync=null;}}
        public void SyncDesktopNow(){if(!persistConfiguration)return;if(autoSync==null)StartDesktopAutoSync();if(autoSync!=null)autoSync.Sync("手动");}
        public void Toggle(bool force=false) {
            if(GlassSuppressed)return;
            if(Board.IsVisible && !force) Board.CollapseSmooth(); else Board.Open();
        }
        public void Launch(Entry entry) {
            bool handled=false;
            try { handled=RunningWindows.Toggle(entry,Board==null?IntPtr.Zero:Board.foregroundHint); }
            catch(Exception ex) { Store.Log(ex); Store.Trace("LAUNCH toggle failed entry="+entry.Name+" error="+ex.Message); }
            if(handled) { Board.CollapseSmooth(); return; }
            try { Process.Start(new ProcessStartInfo(entry.Path){UseShellExecute=true}); Board.CollapseSmooth(); }
            catch(Exception ex) { Store.Log(ex); MessageBox.Show("无法打开「"+entry.Name+"」。\n入口可能已移动或卸载。\n\n"+ex.Message,"GlassBoard"); }
        }
        public void Add(IEnumerable<string> paths,bool manual=true,string targetGroup=null) {
            if(manual&&targetGroup==null&&Board.IsVisible)targetGroup=Board.CurrentGroup;
            if(!manual)MigrateRemovalIdentities();
            var before=BeginBoardEdit();int added=0;
            foreach(string rawPath in paths) {
                string path=EntryPathRules.Normalize(rawPath);
                if(!File.Exists(path) && !Directory.Exists(path)) continue;
                if(!LayoutRules.AllowImport(path,Prefs.Removed,manual))continue;
                var existing=Prefs.Apps.FirstOrDefault(a=>string.Equals(EntryPathRules.Normalize(a.Path),path,StringComparison.OrdinalIgnoreCase));if(existing!=null){if(manual&&targetGroup!=null&&targetGroup!="全部")added+=FeatureRules.AddShared(Prefs,new[]{existing.Path},targetGroup);continue;}
                
                var entry=new Entry {Name=EntryPathRules.Name(path),Path=path,IsFolder=Directory.Exists(path)};LaunchInfoService.Enrich(entry);if(!manual&&(!LaunchInfoService.Primary(entry)||RemovalRules.Blocked(Prefs,entry)||Prefs.Apps.Any(a=>RemovalRules.Same(a,entry))))continue;if(manual)RemovalRules.Forget(Prefs,entry);entry.Group=FeatureRules.ImportGroup(Prefs,entry,manual?targetGroup:null);Prefs.Apps.Add(entry); added++;
            }
            if(added>0){EndBoardEdit(before,"添加入口");Refresh();}
        }
        public void Choose() {
            Board.ModalBusy=true;
            try {new BatchAddWindow(this).ShowDialog();}finally{Board.ModalBusy=false;}
        }
        public void AddFromAnywhere() {Toggle(true);Choose();}
        public void Opacity(double percent) {
            Prefs.OpacityPercent=LayoutRules.GlassOpacity(percent);Board.SetGlassOpacity(Prefs.OpacityPercent.Value);
        }
        public void Remove(Entry a,string scope=null) {
            RemoveMany(new string[]{a.Path},scope);
        }
        public void RemoveMany(IEnumerable<string> paths,string scope=null) {
            string group=scope??(Board.IsVisible?Board.CurrentGroup:"全部");var targets=new HashSet<string>(paths,StringComparer.OrdinalIgnoreCase);targets.RemoveWhere(path=>!Prefs.Apps.Any(a=>string.Equals(a.Path,path,StringComparison.OrdinalIgnoreCase)&&FeatureRules.InGroup(a,group)));int count=Prefs.Apps.Count(a=>targets.Contains(a.Path));if(count==0)return;
            Board.ModalBusy=true;
            try {
                string text="从「"+group+"」移除已选的 "+count+" 个入口？\n"+(group=="全部"?"本次从所有分组与 Dock 移除，原软件和磁盘文件保留。":"同一软件在其他组中的入口保留；原软件和磁盘文件保留。")+"\n移除后可点击「撤销」。";
                var answer=Board.IsVisible?MessageBox.Show(Board,text,"GlassBoard",MessageBoxButton.YesNo):MessageBox.Show(text,"GlassBoard",MessageBoxButton.YesNo);
                if(answer==MessageBoxResult.Yes) {foreach(var entry in Prefs.Apps.Where(a=>targets.Contains(a.Path)))LaunchInfoService.Enrich(entry);var before=BeginBoardEdit();EntryRules.Remove(Prefs,targets,group);EndBoardEdit(before,"移除入口");Board.FinishSelection();Refresh();Board.SetStatus("已移除 "+count+" 个入口 · 可撤销本次移除");}
            } finally {Board.ModalBusy=false;}
        }
        public void UndoRemoval(){UndoBoard();}
        public void Pin(Entry a) {
            if(!a.Pinned && Prefs.Apps.Count(x=>x.Pinned)>=20) {MessageBox.Show("最多固定 20 个入口。请先取消一个固定。","GlassBoard");return;}
            var before=BeginBoardEdit();a.Pinned=!a.Pinned;EndBoardEdit(before,"调整 Dock 固定");Refresh();
        }
        static IEnumerable<string> Shortcuts(string root) {
            if(!Directory.Exists(root)) yield break;
            var pending=new Stack<string>();pending.Push(root);int count=0;
            while(pending.Count>0 && count<2500) {
                string folder=pending.Pop();string[] files=null,dirs=null;
                try { files=Directory.GetFiles(folder);dirs=Directory.GetDirectories(folder); } catch {continue;}
                foreach(string f in files) {string e=System.IO.Path.GetExtension(f).ToLowerInvariant();if((e==".lnk"||e==".appref-ms"||e==".url")&&StartupEntryRules.Candidate(f)) {count++;yield return f;}}
                foreach(string d in dirs) {try {if((File.GetAttributes(d)&FileAttributes.ReparsePoint)==0) pending.Push(d);}catch {}}
            }
        }
        public async void Import() {
            if(importing)return; importing=true;Board.SetStatus("正在读取开始菜单…");
            try {
                var paths=await Task.Run(()=>Shortcuts(Environment.GetFolderPath(Environment.SpecialFolder.Programs)).Concat(Shortcuts(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms))).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p=>System.IO.Path.GetFileNameWithoutExtension(p),StringComparer.CurrentCultureIgnoreCase).ToArray());
                if(ShuttingDown)return;int before=Prefs.Apps.Count;Add(paths,false);Prefs.Imported=true;
                if(!Prefs.Apps.Any(a=>a.Pinned)) {
                    string[] common={"edge","chrome","firefox","微信","wechat","qq","steam","blender","maya","剪映","capcut","code"};
                    foreach(var a in Prefs.Apps.Where(a=>common.Any(k=>a.Name.IndexOf(k,StringComparison.OrdinalIgnoreCase)>=0)).Take(7))a.Pinned=true;
                }
                Refresh();Board.SetStatus("已导入 "+(Prefs.Apps.Count-before)+" 个入口 · 可拖入其他软件");
            }catch(Exception ex) {Store.Log(ex);Board.SetStatus("导入失败，可使用右上角添加");}
            finally {importing=false;}
        }
        public ContextMenu Menu(Entry a,string scope="全部") {
            var menu=MenuTheme.Create(Prefs.Light);
            Action<string,Action> item=(title,action)=> {var mi=new MenuItem{Header=title};mi.Click+=(s,e)=>action();menu.Items.Add(mi);};
            item("打开",()=>Launch(a));item("打开文件所在位置",()=>{try{FileShellActions.Location(a);}catch(Exception ex){MessageBox.Show(ex.Message,"GlassBoard");}});item("文件属性",()=>{try{FileShellActions.Properties(a.Path);}catch(Exception ex){MessageBox.Show(ex.Message,"GlassBoard");}});item(a.Pinned?"取消 Dock 固定":"固定到 Dock",()=>Pin(a));
            if(scope=="Dock"){item("添加软件或文件到 Dock",()=>ChooseDockFiles());AddDockWindowMenus(menu,a);}
            else {
                item("前移一位",()=> {int i=Prefs.Apps.IndexOf(a);if(i>0){Prefs.Apps.RemoveAt(i);Prefs.Apps.Insert(i-1,a);Refresh();}});
                item("后移一位",()=> {int i=Prefs.Apps.IndexOf(a);if(i<Prefs.Apps.Count-1){Prefs.Apps.RemoveAt(i);Prefs.Apps.Insert(i+1,a);Refresh();}});
            }
            item("修改名称与图标",()=>Customize(a));
            var groupMenu=new MenuItem{Header="移动到分组"};foreach(var group in new string[]{"未分组"}.Concat(Prefs.Groups)){string name=group;var child=new MenuItem{Header=name};child.Click+=(s,e)=>{TransferEntries(new string[]{a.Path},scope=="Dock"?"全部":scope,name);};groupMenu.Items.Add(child);}menu.Items.Add(groupMenu);
            item("移除入口",()=>Remove(a,scope=="Dock"?"全部":scope));return menu;
        }
        internal static ToggleButton VisibilitySwitch() {
            var toggle=new ToggleButton{Width=50,Height=26,Cursor=Cursors.Hand,VerticalAlignment=VerticalAlignment.Center};
            var track=new FrameworkElementFactory(typeof(Border));track.Name="Track";track.SetValue(Border.CornerRadiusProperty,new CornerRadius(13));track.SetValue(Border.BackgroundProperty,new SolidColorBrush(Color.FromRgb(83,87,99)));
            var thumb=new FrameworkElementFactory(typeof(Border));thumb.Name="Thumb";thumb.SetValue(FrameworkElement.WidthProperty,20.0);thumb.SetValue(FrameworkElement.HeightProperty,20.0);thumb.SetValue(FrameworkElement.MarginProperty,new Thickness(3));thumb.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Left);thumb.SetValue(Border.CornerRadiusProperty,new CornerRadius(10));thumb.SetValue(Border.BackgroundProperty,Brushes.White);track.AppendChild(thumb);
            var template=new ControlTemplate(typeof(ToggleButton)){VisualTree=track};var on=new Trigger{Property=ToggleButton.IsCheckedProperty,Value=true};
            on.Setters.Add(new Setter(Border.BackgroundProperty,new SolidColorBrush(Color.FromRgb(100,162,238)),"Track"));on.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Right,"Thumb"));template.Triggers.Add(on);toggle.Template=template;return toggle;
        }
        public void Settings(int selectedPage=-1) {
            if(settingsOpen){if(settingsWindow!=null){if(selectedPage>=0&&settingsTabs!=null)settingsTabs.SelectedIndex=Math.Max(0,Math.Min(settingsTabs.Items.Count-1,selectedPage));if(settingsWindow.WindowState==WindowState.Minimized)settingsWindow.WindowState=WindowState.Normal;settingsWindow.Activate();}return;}settingsOpen=true;
            var preview=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};var stateTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(700)};
            bool shown=false,cleaned=false;
            Action cleanup=()=>{if(cleaned)return;cleaned=true;settingsOpen=false;settingsWindow=null;settingsTabs=null;preview.Stop();stateTimer.Stop();Save();try{if(!ShuttingDown){Dock.Refresh();RefreshWidgets();}}catch(Exception ex){Store.Log(ex);}};
            try {
                Board.Hide();
                var w=new Window{Title="GlassBoard · 个性化与控制 v0.2.0 · 抖音 森霖Sama 制作",Width=Math.Min(940,SystemParameters.WorkArea.Width-40),Height=Math.Min(790,SystemParameters.WorkArea.Height-50),MinWidth=560,ResizeMode=ResizeMode.CanResize,WindowStartupLocation=WindowStartupLocation.CenterScreen,Owner=Dock,Topmost=true,Background=new SolidColorBrush(Color.FromRgb(28,30,38)),Foreground=Brushes.White};
                settingsWindow=w;w.Closed+=(s,e)=>cleanup();
                var area=SystemParameters.WorkArea;var vertical=SettingsPlacementRules.Vertical(area.Top,area.Height,Dock.RestingTop,Dock.Height,790);w.WindowStartupLocation=WindowStartupLocation.Manual;w.Left=area.Left+(area.Width-w.Width)/2;w.Top=vertical[0];w.Height=vertical[1];
                Polish.Apply(w,false);var root=new Grid{Margin=new Thickness(24)};root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});w.Content=root;
                var title=new StackPanel();title.Children.Add(new TextBlock{Text="把桌面，变成你的空间",FontSize=25,Margin=new Thickness(0,0,0,6)});title.Children.Add(new TextBlock{Text="GLASSBOARD  /  个性化、组件与桌面控制",Foreground=Brushes.LightGray,Margin=new Thickness(0,0,0,18)});root.Children.Add(title);
                var tabs=new TabControl{Background=Brushes.Transparent,BorderThickness=new Thickness(0)};var settingsHost=new DockPanel();Grid.SetRow(settingsHost,1);root.Children.Add(settingsHost);settingsHost.Children.Add(tabs);
                settingsTabs=tabs;
                Func<string,StackPanel> page=heading=>{var section=Polish.Section(heading,heading=="支持创作者"?"感谢每一份支持，让创作持续生长。":"调整会即时预览；关闭窗口保留外观与组件设置。",false);var panel=(StackPanel)section.Child;System.Windows.Documents.TextElement.SetForeground(panel,Brushes.White);var holder=new StackPanel{Margin=new Thickness(10,8,0,8)};holder.Children.Add(section);var tab=new TabItem{Header=heading,Content=new ScrollViewer{Content=holder,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}};tabs.Items.Add(tab);return panel;};
                var appearance=page("面板与主题");var dockPage=page("Dock DIY");var keysPage=page("快捷键与开关");var widgetsPage=page("桌面组件");var generalPage=page("通用");var creatorPage=page("支持创作者");bool creatorLoaded=false;Action loadCreator=()=>{if(creatorLoaded)return;BuildCreatorSupport(creatorPage,w);creatorLoaded=true;};tabs.SelectionChanged+=(ss,ee)=>{if(tabs.SelectedIndex==5)loadCreator();};var homePage=page("主页");BuildHomePage(homePage,w);var manualPage=page("说明书");BuildManual(manualPage);var motionPage=page("UI 动效");tabs.SelectedIndex=selectedPage<0?6:Math.Max(0,Math.Min(tabs.Items.Count-1,selectedPage));if(tabs.SelectedIndex==5)loadCreator();BuildSettingsNavigation(settingsHost,tabs);
                var draft=Prefs.Dock.Copy();bool loading=false;
                preview.Tick+=(s,e)=>{preview.Stop();Dock.Refresh(false);};
                Action live=()=>{if(loading)return;draft.ItemOrder=(Prefs.Dock.ItemOrder??new List<string>()).ToList();draft.RunningOrder=(Prefs.Dock.RunningOrder??new List<string>()).ToList();draft.Normalize();Prefs.Dock=draft.Copy();Save();if(!preview.IsEnabled)preview.Start();};
                Func<StackPanel,string,double,double,double,double,string,Action<double>,Slider> range=(panel,label,min,max,value,tick,unit,change)=>{
                    var caption=new TextBlock{Text=label+"  "+value.ToString("0.##")+unit,Margin=new Thickness(0,12,0,6),Foreground=Brushes.White};panel.Children.Add(caption);
                    var control=new Slider{Minimum=min,Maximum=max,Value=value,TickFrequency=tick,IsSnapToTickEnabled=true,Margin=new Thickness(0,0,5,4)};panel.Children.Add(control);
                    control.ValueChanged+=(s,e)=>{caption.Text=label+"  "+control.Value.ToString("0.##")+unit;change(control.Value);};return control;
                };
                Func<StackPanel,string,bool,Action<bool>,CheckBox> check=(panel,label,value,change)=>{var control=new CheckBox{Content=label,IsChecked=value,Foreground=Brushes.White,Margin=new Thickness(0,10,0,8)};panel.Children.Add(control);control.Checked+=(s,e)=>change(true);control.Unchecked+=(s,e)=>change(false);return control;};
                Func<StackPanel,string,string[],int,Action<int>,ComboBox> combo=(panel,label,items,value,change)=>{panel.Children.Add(new TextBlock{Text=label,Margin=new Thickness(0,12,0,6)});var control=new ComboBox{ItemsSource=items,SelectedIndex=value,Height=32};panel.Children.Add(control);control.SelectionChanged+=(s,e)=>{if(control.SelectedIndex>=0)change(control.SelectedIndex);};return control;};
                check(appearance,"使用浅色玻璃",Prefs.Light,value=>{Prefs.Light=value;live();});
                combo(appearance,"大面板尺寸",new string[]{"舒适 · 中号","宽阔 · 大号","铺满 · 全屏"},Prefs.PanelSize,value=>{Prefs.PanelSize=value;Prefs.Placement.PanelWidth=Prefs.Placement.PanelHeight=null;Save();});
                range(appearance,"大面板透明度",0,100,100-(Prefs.OpacityPercent??72),5,"%",value=>{Opacity(100-value);Save();});
                check(appearance,"所有组件置于桌面层（统一覆盖各自置顶）",Prefs.AllDesktopLayer,value=>{Prefs.AllDesktopLayer=value;DesktopPersistence.SetDesktopOnly(value);Save();});
                appearance.Children.Add(new TextBlock{Text="开启后 Dock、小球、大面板及组件保持在普通应用后方；关闭后恢复各自置顶设置。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray});
                motionPage.Children.Add(FeatureUi.Button("一键关闭过渡动效（流畅优先）",()=>{MotionPresets.DisableTransitions(Prefs);draft=Prefs.Dock.Copy();Save();Dock.Refresh(false);RefreshWidgets();RefreshControlCenter();w.Close();}));
                motionPage.Children.Add(new TextBlock{Text="停用展开、翻页、缩放悬停和弹簧过渡，保留即时高亮与点击反馈。可重新开启下方选项。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,0,0,10)});
                check(motionPage,"大玻璃板翻页动效（独立开关）",Prefs.BoardPageMotion,value=>{Prefs.BoardPageMotion=value;Save();});
                combo(motionPage,"翻页动效样式",new[]{"轻柔淡入","水平滑入","微缩淡入"},Array.IndexOf(new[]{"fade","slide","scale"},Prefs.BoardPageStyle),value=>{Prefs.BoardPageStyle=new[]{"fade","slide","scale"}[value];Save();});
                check(motionPage,"展开 / 收起动画",draft.OpenMotion,value=>{draft.OpeningAnimation=value;live();});
                combo(motionPage,"展开动效样式",InteractionRules.OpenNames,Array.IndexOf(InteractionRules.OpenStyles,draft.OpenAnimationStyle),value=>{draft.OpenAnimationStyle=InteractionRules.OpenStyles[value];live();});
                check(motionPage,"图标悬停动效（独立开关）",draft.HoverMotion,value=>{draft.HoverAnimation=value;live();});
                combo(motionPage,"图标悬停样式",InteractionRules.HoverNames,Array.IndexOf(InteractionRules.HoverStyles,draft.HoverAnimationStyle),value=>{draft.HoverAnimationStyle=InteractionRules.HoverStyles[value];live();});
                range(motionPage,"动效强度",.5,1.5,draft.MotionLevel,.1," 倍",value=>{draft.MotionLevel=value;live();});
                BuildReflectionEditor(motionPage,()=>draft,live);
                check(motionPage,"图标拖动动效（放大跟手、实时让位）",draft.DragMotion,value=>{draft.DragMotion=value;live();});
                motionPage.Children.Add(new TextBlock{Text="拖动时放大跟手、相邻图标平滑让位；关闭动效仍保留基础预览。文件可拖到支持附件或文件接收的软件。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,2,0,8)});
                range(appearance,"玻璃模糊",0,40,draft.BlurRadius,2,"",value=>{draft.BlurRadius=value;live();});range(appearance,"边缘高光",.2,2,draft.EdgeStrength,.1," 倍",value=>{draft.EdgeStrength=value;live();});
                check(appearance,"大面板置顶（关闭时保持桌面层）",Prefs.BoardAlwaysOnTop,value=>{Prefs.BoardAlwaysOnTop=value;Board.Topmost=value;Board.RefreshLayer();Save();});
                check(appearance,"锁定大面板位置和尺寸",Prefs.Placement.PanelLocked,value=>{Prefs.Placement.PanelLocked=value;Save();});
                check(appearance,"点击外部自动收起大面板（关闭时方便从桌面拖入文件）",Prefs.BoardDismissOnDeactivate,value=>{Prefs.BoardDismissOnDeactivate=value;Save();});
                check(appearance,"新增软件自动识别分组",Prefs.AutoGrouping,value=>{Prefs.AutoGrouping=value;Save();});
                check(appearance,"隐藏大面板搜索栏",Prefs.BoardHideSearch,value=>{Prefs.BoardHideSearch=value;Save();Board.ApplyDisplayOptions();});
                check(appearance,"隐藏大面板分组栏，直接显示全部图标",Prefs.BoardHideGroups,value=>{Prefs.BoardHideGroups=value;Save();Board.ApplyDisplayOptions();});
                check(appearance,"显示快捷方式箭头角标（大面板与 Dock）",draft.ShowShortcutOverlays,value=>{draft.ShowShortcutOverlays=value;Native.ClearIconCache();live();Board.RefreshIcons();Save();});
                check(appearance,"高清图标（大面板与 Dock，默认关闭）",draft.SharperIcons,value=>{draft.SharperIcons=value;Native.ClearIconCache();live();preview.Stop();Dock.Refresh(false);Board.RefreshIcons();Save();});
                appearance.Children.Add(new TextBlock{Text="优先读取软件自带的高分辨率图标；仅在加载时处理，不新增后台任务。关闭后清除图标缓存。原始图标像素低时效果有限。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,2,0,8)});
                BuildMaterialEditor(appearance,"大面板玻璃材质 DIY",Prefs.BoardMaterial,()=>{Prefs.BoardMaterial.Normalize();Board.SetMaterial(Prefs.BoardMaterial);Save();});
                var manage=new WrapPanel{Margin=new Thickness(0,12,0,0)};appearance.Children.Add(manage);manage.Children.Add(FeatureUi.Button("外观方案",()=>{Save();if(ManageSchemes(w)){draft=Prefs.Dock.Copy();w.Close();}}));manage.Children.Add(FeatureUi.Button("管理分组",()=>ManageGroups(w)));manage.Children.Add(FeatureUi.Button("创作工作区",()=>ManageWorkspaces(w)));manage.Children.Add(FeatureUi.Button("自动分组未分组软件",()=>AutoGroupApps(w)));
                manage.Children.Add(FeatureUi.Button("重置面板位置和尺寸",()=>{Prefs.Placement.PanelLeft=Prefs.Placement.PanelTop=Prefs.Placement.PanelWidth=Prefs.Placement.PanelHeight=null;Save();}));
                appearance.Children.Add(new TextBlock{Text="展开与悬停分别设置。轻柔淡入负担较低；微缩和轻抬仅作用于内容，玻璃底板保持稳定。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,12,0,0)});
                dockPage.Children.Add(new TextBlock{Text="独立定制下方玻璃板",FontSize=18,Foreground=Brushes.White});
                BuildMaterialEditor(dockPage,"Dock 玻璃材质 DIY",draft.Material,()=>{live();});
                var presets=combo(dockPage,"一键风格",new string[]{"自定义","紧凑","均衡","宽松"},0,value=>{});
                var icon=range(dockPage,"图标尺寸",32,64,draft.IconSize,2," px",value=>{draft.IconSize=value;live();});
                var spacing=range(dockPage,"图标间距",0,20,draft.Spacing,1," px",value=>{draft.Spacing=value;live();});
                var padding=range(dockPage,"玻璃板左右留白",8,28,draft.Padding,1," px",value=>{draft.Padding=value;live();});
                range(dockPage,"玻璃板上下最小留白",0,28,draft.VerticalPadding,1," px",value=>{draft.VerticalPadding=value;live();});
                dockPage.Children.Add(new TextBlock{Text="悬停放大时自动保留必要空间，避免图标和倒影被裁切。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,2,0,8)});
                var radius=range(dockPage,"圆角",0,48,draft.Radius,2," px",value=>{draft.Radius=value;live();});
                var opacity=range(dockPage,"Dock 透明度",0,100,100-draft.OpacityPercent,5,"%",value=>{draft.OpacityPercent=100-value;live();});
                var zoom=range(dockPage,"悬停放大",1,1.6,draft.Zoom,.05," 倍",value=>{draft.Zoom=value;live();});
                var labels=check(dockPage,"显示图标名称",draft.ShowLabels,value=>{draft.ShowLabels=value;live();});
                check(dockPage,"显示此电脑",draft.ShowComputer,value=>{draft.ShowComputer=value;live();});
                check(dockPage,"显示回收站",draft.ShowRecycleBin,value=>{draft.ShowRecycleBin=value;live();});
                check(dockPage,"全屏应用时让出显示层级",draft.RespectFullscreen,value=>{draft.RespectFullscreen=value;live();});
                check(dockPage,"显示设置图标",draft.ShowSettings,value=>{draft.ShowSettings=value;live();});
                check(dockPage,"显示电源快捷菜单",draft.ShowPower,value=>{draft.ShowPower=value;live();});
                check(dockPage,"Dock 保留控制中心快捷入口",draft.ShowNetwork,value=>{draft.ShowNetwork=value;live();});
                combo(dockPage,"此电脑图标样式",new string[]{"渐变徽章","简洁线框"},draft.ComputerStyle,value=>{draft.ComputerStyle=value;live();});
                combo(dockPage,"强调色",new string[]{"晴空蓝","雾紫","薄荷绿","暖金","玫瑰粉","月光灰"},draft.Accent,value=>{draft.Accent=value;live();});
                check(dockPage,"显示分隔线",draft.ShowSeparator,value=>{draft.ShowSeparator=value;live();});
                check(dockPage,"Dock 保持在窗口最上方",draft.AlwaysOnTop,value=>{draft.AlwaysOnTop=value;live();});
                check(dockPage,"显示软件运行状态",draft.ShowRunning,value=>{draft.ShowRunning=value;live();});
                check(dockPage,"Dock 自动隐藏，靠近所在屏幕底边唤出",draft.AutoHide,value=>{draft.AutoHide=value;live();});range(dockPage,"自动隐藏延迟",200,3000,draft.HideDelay,100," ms",value=>{draft.HideDelay=value;live();});range(dockPage,"底边触发范围",3,48,draft.EdgeRange,1," px",value=>{draft.EdgeRange=value;live();});
                check(dockPage,"弹簧弹出效果（也可右键 Dock 切换）",draft.SpringReveal,value=>{draft.SpringReveal=value;live();});
                check(dockPage,"锁定 Dock 位置",Prefs.Placement.DockLocked,value=>{Prefs.Placement.DockLocked=value;live();});
                dockPage.Children.Add(FeatureUi.Button("重置 Dock 位置",()=>{Prefs.Placement.DockCenter=Prefs.Placement.DockLeft=Prefs.Placement.DockTop=null;live();}));
                combo(dockPage,"水平位置",new string[]{"靠左","居中","靠右"},draft.Alignment,value=>{draft.Alignment=value;Prefs.Placement.DockCenter=Prefs.Placement.DockLeft=null;live();});
                range(dockPage,"左右微调",-500,500,draft.Offset,10," px",value=>{draft.Offset=value;Prefs.Placement.DockCenter=Prefs.Placement.DockLeft=null;live();});
                check(dockPage,"允许贴近屏幕底边（可进入任务栏区域）",draft.UseScreenBounds,value=>{draft.UseScreenBounds=value;Prefs.Placement.DockTop=null;live();});
                range(dockPage,"底部间距（玻璃边框到所选区域底边）",0,160,Prefs.DockGap,2," px",value=>{Prefs.DockGap=value;Prefs.Placement.DockTop=null;live();});
                range(dockPage,"固定应用显示数量",3,20,draft.MaxPinned,1," 个",value=>{draft.MaxPinned=(int)value;live();});
                dockPage.Children.Add(new TextBlock{Text="可固定 20 个入口。数量超限或屏幕放不下时，其余入口收进「更多」。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,12,0,0)});
                presets.SelectionChanged+=(s,e)=>{if(presets.SelectedIndex<1)return;int style=presets.SelectedIndex;loading=true;icon.Value=style==1?34:style==3?56:44;spacing.Value=style==1?2:style==3?10:6;padding.Value=style==1?10:style==3?22:16;radius.Value=style==1?20:style==3?36:28;opacity.Value=style==1?35:style==3?22:28;zoom.Value=style==1?1.1:style==3?1.3:1.2;labels.IsChecked=style==3;loading=false;live();};
                keysPage.Children.Add(new TextBlock{Text="快捷键与桌面开关",FontSize=18});
                keysPage.Children.Add(new TextBlock{Text="可点击录制直接按组合键，保存后生效。右侧开关即时操作。\n隐藏开关：开启隐藏，关闭显示；自动开关：Windows 任务栏自动隐藏。",Foreground=Brushes.LightGray,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,18)});
                var keyChoices=HotkeyRules.Keys();var controls=new Dictionary<int,Tuple<CheckBox,ComboBox,ComboBox>>();var switches=new Dictionary<int,ToggleButton>();var info=new Dictionary<int,TextBlock>();bool syncing=false;
                foreach(var binding in Prefs.Hotkeys.OrderBy(k=>k.ActionId)) {
                    int id=binding.ActionId;var group=new StackPanel{Margin=new Thickness(0,0,0,14)};keysPage.Children.Add(group);
                    var row=new Grid();foreach(double width in new double[]{1.7,1.7,.8,1.1})row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(width,GridUnitType.Star)});row.ColumnDefinitions.Insert(3,new ColumnDefinition{Width=new GridLength(64)});group.Children.Add(row);
                    var enabled=new CheckBox{Content=new TextBlock{Text=HotkeyRules.Actions[id-1],TextWrapping=TextWrapping.Wrap},IsChecked=binding.Enabled,Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)};row.Children.Add(enabled);
                    var modifiers=new ComboBox{ItemsSource=HotkeyRules.ModifierNames,SelectedIndex=Array.IndexOf(HotkeyRules.ModifierValues,binding.Modifiers),Height=32,Margin=new Thickness(0,0,8,0),IsEnabled=binding.Enabled};Grid.SetColumn(modifiers,1);row.Children.Add(modifiers);
                    var key=new ComboBox{ItemsSource=keyChoices.Keys.ToList(),SelectedItem=keyChoices.First(k=>k.Value==binding.Key).Key,Height=32,Margin=new Thickness(0,0,8,0),IsEnabled=binding.Enabled};Grid.SetColumn(key,2);row.Children.Add(key);
                    enabled.Checked+=(s,e)=>{modifiers.IsEnabled=true;key.IsEnabled=true;};enabled.Unchecked+=(s,e)=>{modifiers.IsEnabled=false;key.IsEnabled=false;};controls[id]=Tuple.Create(enabled,modifiers,key);
                    var record=new Button{Content="录制",Height=32,Margin=new Thickness(0,0,8,0)};Grid.SetColumn(record,3);row.Children.Add(record);record.Click+=(s,e)=>{var active=Dock.SuspendHotkeys();HotkeySetting captured;try{captured=FeatureUi.Record(w,id);}finally{Dock.ResumeHotkeys(active);}if(captured!=null){enabled.IsChecked=true;modifiers.SelectedIndex=Array.IndexOf(HotkeyRules.ModifierValues,captured.Modifiers);key.SelectedItem=keyChoices.First(x=>x.Value==captured.Key).Key;}};
                    if(id==2||id==3||id==7) {
                        var wrap=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(wrap,4);row.Children.Add(wrap);
                        var toggle=VisibilitySwitch();if(id==7){try{toggle.IsChecked=TaskbarAutoHide.Enabled;}catch{toggle.IsChecked=false;}}else toggle.IsChecked=DesktopVisibility.DesiredHidden(id==2);toggle.ToolTip=id==7?"开启 Windows 任务栏自动隐藏":"开启隐藏，关闭显示";wrap.Children.Add(toggle);wrap.Children.Add(new TextBlock{Text=id==7?"自动":"隐藏",Margin=new Thickness(6,0,0,0),VerticalAlignment=VerticalAlignment.Center,Foreground=Brushes.White});switches[id]=toggle;
                        toggle.Checked+=(s,e)=>{if(!syncing){if(id==7)ToggleTaskbarAutoHide(true);else ShellAction(id-1,true);}};toggle.Unchecked+=(s,e)=>{if(!syncing){if(id==7)ToggleTaskbarAutoHide(false);else ShellAction(id-1,false);}};
                    }
                    var status=new TextBlock{Foreground=Brushes.LightGray,FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)};group.Children.Add(status);info[id]=status;
                }
                Action updateState=()=>{syncing=true;foreach(var k in Prefs.Hotkeys){string state=!k.Enabled?"快捷键已关闭":Dock.HotkeyActive(k.ActionId)?"快捷键已注册":"快捷键未注册，请换组合或重新注册";if(k.ActionId==2||k.ActionId==3){bool taskbar=k.ActionId==2;bool? hidden=DesktopVisibility.IsHidden(taskbar);state+=" · 实际状态："+(!hidden.HasValue?"未找到系统窗口":hidden.Value?"隐藏":"显示");switches[k.ActionId].IsChecked=DesktopVisibility.DesiredHidden(taskbar);}if(k.ActionId==7){try{bool auto=TaskbarAutoHide.Enabled;switches[7].IsChecked=auto;state+=" · 自动隐藏："+(auto?"开启":"关闭");}catch{state+=" · 未找到 Windows 任务栏";}}info[k.ActionId].Text=state;}syncing=false;};
                stateTimer.Tick+=(s,e)=>updateState();updateState();stateTimer.Start();
                var actions=new WrapPanel();keysPage.Children.Add(actions);
                var reset=new Button{Content="恢复默认快捷键",Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,8,6)};actions.Children.Add(reset);reset.Click+=(s,e)=>{foreach(var k in HotkeyRules.Defaults()){var row=controls[k.ActionId];row.Item1.IsChecked=true;row.Item2.SelectedIndex=Array.IndexOf(HotkeyRules.ModifierValues,k.Modifiers);row.Item3.SelectedItem=keyChoices.First(x=>x.Value==k.Key).Key;}};
                var retry=new Button{Content="重新注册当前快捷键",Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,8,6)};actions.Children.Add(retry);retry.Click+=(s,e)=>{MessageBox.Show(w,Dock.RetryHotkeys(),"快捷键注册结果");updateState();};
                var restore=new Button{Content="显示任务栏与桌面图标",Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,8,6)};actions.Children.Add(restore);restore.Click+=(s,e)=>{ShellAction(3);updateState();};
                keysPage.Children.Add(new TextBlock{Text="隐藏状态仅在本次运行中维持；正常退出恢复启动前状态。组合键占用不会阻止直接点击开关。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,12,0,0)});
                widgetsPage.Children.Add(new TextBlock{Text="可选组件 · 默认关闭",FontSize=18});
                widgetsPage.Children.Add(FeatureUi.Button("控制中心 · 开启小球",()=>OpenControlCenter()));widgetsPage.Children.Add(FeatureUi.Button("控制中心 · 小球设置",()=>ControlCenterSettings(w)));
                widgetsPage.Children.Add(FeatureUi.Button("组件外观 DIY · 颜色 / 无框 / 时钟 / 音乐按钮",()=>CustomizeWidgets(null,w)));
                var routes=new Dictionary<string,ComboBox>();var widgetChecks=new Dictionary<string,ToggleButton>();bool routeSync=false;
                Action syncRoutes=()=>{routeSync=true;foreach(var item in routes)item.Value.SelectedIndex=WidgetRules.Panel(Prefs.Widgets,item.Key);routeSync=false;};
                var presetsRow=new WrapPanel{Margin=new Thickness(0,6,0,14)};widgetsPage.Children.Add(presetsRow);
                presetsRow.Children.Add(FeatureUi.Button("全部融合",()=>{Prefs.Widgets.Fuse();syncRoutes();RefreshWidgets();Save();}));
                presetsRow.Children.Add(FeatureUi.Button("全部分离",()=>{Prefs.Widgets.Separate();syncRoutes();RefreshWidgets();Save();}));
                widgetsPage.Children.Add(new TextBlock{Text="组件选择同一个面板会融合，选择不同面板会分离。也可点击组件右上角 ••• 调整。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,0,0,12)});
                foreach(string component in WidgetRules.Kinds){string kind=component;int index=Array.IndexOf(WidgetRules.Kinds,kind);var row=new Grid{Margin=new Thickness(0,4,0,9)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(132)});var controlsRow=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};row.Children.Add(controlsRow);var enable=VisibilitySwitch();enable.IsChecked=WidgetRules.Enabled(Prefs.Widgets,kind);enable.ToolTip="开启 / 关闭"+WidgetRules.Names[index];controlsRow.Children.Add(enable);controlsRow.Children.Add(new TextBlock{Text=WidgetRules.Names[index],Margin=new Thickness(10,0,0,0),Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center});widgetChecks[kind]=enable;enable.Click+=(s,e)=>{bool desired=enable.IsChecked==true;SetWidgetEnabled(kind,desired);};var route=new ComboBox{ItemsSource=new string[]{"面板 A","面板 B","面板 C","面板 D"},SelectedIndex=WidgetRules.Panel(Prefs.Widgets,kind),Height=34};Grid.SetColumn(route,1);row.Children.Add(route);routes[kind]=route;route.SelectionChanged+=(s,e)=>{if(routeSync||route.SelectedIndex<0)return;WidgetRules.Assign(Prefs.Widgets,kind,route.SelectedIndex);RefreshWidgets();Save();};widgetsPage.Children.Add(row);}
                var islandRow=new Grid{Margin=new Thickness(0,4,0,9)};var islandControls=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};islandRow.Children.Add(islandControls);var islandCheck=VisibilitySwitch();islandCheck.IsChecked=Prefs.Widgets.IslandEnabled;islandCheck.ToolTip="开启 / 关闭灵动岛";islandControls.Children.Add(islandCheck);islandControls.Children.Add(new TextBlock{Text="灵动岛",Margin=new Thickness(10,0,0,0),Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center});islandCheck.Click+=(s,e)=>{Prefs.Widgets.IslandEnabled=islandCheck.IsChecked==true;RefreshWidgets();Save();};widgetsPage.Children.Add(islandRow);
                var islandSettings=new Expander{Header="灵动岛外观设置",Foreground=Brushes.White,Margin=new Thickness(0,4,0,12)};var islandOptions=new StackPanel{Margin=new Thickness(0,4,0,4)};islandSettings.Content=islandOptions;widgetsPage.Children.Add(islandSettings);
                combo(islandOptions,"样式",new[]{"纯黑胶囊","透明玻璃"},Prefs.Widgets.IslandGlass?1:0,value=>{Prefs.Widgets.IslandGlass=value==1;RefreshWidgets();Save();});
                range(islandOptions,"背景透明度（100% 全透明，玻璃样式）",0,100,Prefs.Widgets.IslandTransparency??30,5,"%",value=>{Prefs.Widgets.IslandTransparency=value;RefreshWidgets();Save();});
                check(islandOptions,"展开动效",Prefs.Widgets.IslandMotion??true,value=>{Prefs.Widgets.IslandMotion=value;RefreshWidgets();Save();});
                var componentStatus=new TextBlock{Text="组件开关会自动保存，可随时关闭或重新开启。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,6,0,10)};widgetsPage.Children.Add(componentStatus);
                stateTimer.Tick+=(s,e)=>{if(islandCheck.IsChecked!=Prefs.Widgets.IslandEnabled)islandCheck.IsChecked=Prefs.Widgets.IslandEnabled;componentStatus.Text=string.IsNullOrEmpty(WidgetStatus)?"组件开关会自动保存，可随时关闭或重新开启。":WidgetStatus;routeSync=true;try{foreach(var item in widgetChecks){bool enabled=WidgetRules.Enabled(Prefs.Widgets,item.Key);if(item.Value.IsChecked!=enabled)item.Value.IsChecked=enabled;}}finally{routeSync=false;}};
                widgetsPage.Children.Add(FeatureUi.Button("开启全部组件",()=>RecoverWidgets()));widgetsPage.Children.Add(FeatureUi.Button("重新打开已启用组件",()=>ReopenWidgets()));

                check(widgetsPage,"组件始终置顶",Prefs.Widgets.Topmost,value=>{Prefs.Widgets.Topmost=value;RefreshWidgets();});
                check(widgetsPage,"锁定组件位置",Prefs.Widgets.Locked,value=>{Prefs.Widgets.Locked=value;RefreshWidgets();});
                widgetsPage.Children.Add(new TextBlock{Text="天气城市（启用天气后联网查询）",Margin=new Thickness(0,14,0,8)});var city=new TextBox{Text=Prefs.Widgets.City,MinHeight=42,TextWrapping=TextWrapping.Wrap,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalContentAlignment=VerticalAlignment.Center};widgetsPage.Children.Add(city);widgetsPage.Children.Add(FeatureUi.Button("应用天气城市",()=>{Prefs.Widgets.City=city.Text.Trim();Prefs.Widgets.Normalize();RefreshWidgets();Save();}));
                combo(widgetsPage,"天气连接方式",new[]{"使用 Windows 系统代理（默认）","直接连接"},Prefs.Widgets.WeatherDirectConnection?1:0,value=>{Prefs.Widgets.WeatherDirectConnection=value==1;Save();RefreshWidgets();});
                check(widgetsPage,"隐藏小组件右上角关闭和更多按钮",Prefs.Widgets.HideControls,value=>{Prefs.Widgets.HideControls=value;foreach(var position in Prefs.Widgets.Panels)position.HideControls=null;RefreshWidgets();Save();});
                range(widgetsPage,"组件背景透明度（100% 全透明）",0,100,100-Prefs.Widgets.OpacityPercent,5,"%",value=>{Prefs.Widgets.OpacityPercent=100-value;foreach(var panel in Prefs.Widgets.Panels)panel.OpacityPercent=null;RefreshWidgets();Save();});
                range(widgetsPage,"本地音乐音量",0,1,Prefs.Widgets.Volume,.05,"",value=>{Prefs.Widgets.Volume=value;RefreshWidgets();});
                widgetsPage.Children.Add(new TextBlock{Text="音乐支持封面、系统媒体会话、持久播放列表、随机与循环。备忘录自动保存并提供文本导出；切换布局或外观方案保留文字。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,12,0,12)});
                BuildMaterialEditor(widgetsPage,"小组件玻璃材质 DIY",Prefs.Widgets.Material,()=>{RefreshWidgets();Save();});
                widgetsPage.Children.Add(FeatureUi.Button("重置全部组件位置",()=>{Prefs.Widgets.Panels.Clear();Prefs.Placement.WidgetLeft=Prefs.Placement.WidgetTop=null;RefreshWidgets();Save();}));
                bool startup=false;string activeTiming=Prefs.StartupTiming;try{startup=StartupService.Enabled;if(startup)activeTiming=StartupService.Mode;}catch(Exception ex){Store.Log(ex);}
                var startupCheck=new CheckBox{Content="登录 Windows 后自动启动 GlassBoard",IsChecked=startup,Foreground=Brushes.White,Margin=new Thickness(0,10,0,12)};generalPage.Children.Add(startupCheck);
                generalPage.Children.Add(new TextBlock{Text="自启动优先级",Margin=new Thickness(0,4,0,8)});var startupMode=new ComboBox{ItemsSource=StartupRules.Names,SelectedIndex=Array.IndexOf(StartupRules.Modes,StartupRules.Mode(activeTiming)),MinHeight=36};generalPage.Children.Add(startupMode);
                var startupInfo=new TextBlock{Foreground=Brushes.LightGray,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,16)};generalPage.Children.Add(startupInfo);bool startupSync=false;
                Action startupState=()=>{try{bool enabled=StartupService.Enabled;startupInfo.Text=enabled?"已开启："+StartupRules.Names[Array.IndexOf(StartupRules.Modes,StartupService.Mode)]+"。移动程序后请重新应用启动设置。":"未开启；选择优先级会保存，下次开启时生效。";}catch(Exception ex){startupInfo.Text="无法读取启动设置："+ex.GetBaseException().Message;}};
                Action<bool> setStartup=value=>{if(startupSync)return;try{string mode=StartupRules.Modes[Math.Max(0,startupMode.SelectedIndex)];StartupService.Set(value,mode);Prefs.StartupTiming=mode;Save();startupState();}catch(Exception ex){startupSync=true;try{startupCheck.IsChecked=StartupService.Enabled;if(startupCheck.IsChecked==true)startupMode.SelectedIndex=Array.IndexOf(StartupRules.Modes,StartupService.Mode);}catch{startupCheck.IsChecked=false;}startupSync=false;MessageBox.Show(w,ex.GetBaseException().Message,"无法更新开机启动");startupState();}};
                startupCheck.Checked+=(s,e)=>setStartup(true);startupCheck.Unchecked+=(s,e)=>setStartup(false);
                startupMode.SelectionChanged+=(s,e)=>{if(startupSync||startupMode.SelectedIndex<0)return;if(startupCheck.IsChecked==true)setStartup(true);else{Prefs.StartupTiming=StartupRules.Modes[startupMode.SelectedIndex];Save();}};startupState();
                generalPage.Children.Add(new TextBlock{Text="高：当前用户登录后立即触发计划任务。普通：由 Windows 标准启动项调度。低：登录后延迟 60 秒。Windows 不提供跨软件的严格启动排序；此处控制启动触发方式与延迟。关闭开关会移除本软件的两种启动项。",Foreground=Brushes.LightGray,TextWrapping=TextWrapping.Wrap});
                check(generalPage,"自动收录桌面上的文件夹与文档",Prefs.DesktopAutoSync,value=>{Prefs.DesktopAutoSync=value;if(value){StartDesktopAutoSync();SyncDesktopNow();Board.SetStatus("已开启桌面自动收录");}else{StopDesktopAutoSync();Board.SetStatus("已关闭桌面自动收录，已收录的入口会保留");}Save();});
                generalPage.Children.Add(new TextBlock{Text="文件夹进入「"+Prefs.DesktopFolderGroup+"」分组；PDF、Word、Excel 与其他文档进入「"+Prefs.DesktopDocsGroup+"」分组，打开该分组后可在顶部按类型筛选。只读取桌面第一层（含公共桌面）；桌面上的项目被删除或移走后入口会自动移除；手动移动到其他分组的入口不会被挪回。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,4,0,8)});
                generalPage.Children.Add(FeatureUi.Button("立即同步桌面",()=>{SyncDesktopNow();}));
                BuildGameSettings(generalPage);
                BuildUpdateSettings(generalPage,w);generalPage.Children.Add(FeatureUi.Button("找回旧版入口与分组",()=>RecoverHistoricalEntries(w)));
                generalPage.Children.Add(new TextBlock{Text="完整备份与恢复",FontSize=18,Margin=new Thickness(0,22,0,10)});generalPage.Children.Add(new TextBlock{Text="备份布局、分组、快捷键、组件、便签、外观方案与自定义图标。软件与音乐文件保留在原位置；开机启动按当前电脑设置保留。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray});var backups=new WrapPanel{Margin=new Thickness(0,12,0,6)};backups.Children.Add(FeatureUi.Button("导出完整备份",()=>ExportFullBackup()));backups.Children.Add(FeatureUi.Button("恢复完整备份",()=>{w.Close();Dock.Dispatcher.BeginInvoke(new Action(()=>RestoreFullBackup()));}));generalPage.Children.Add(backups);
                var apply=new Button{Content="保存快捷键并完成",Height=42,Background=Polish.Color("#416AA9"),Foreground=Brushes.White,Margin=new Thickness(0,16,0,0)};Grid.SetRow(apply,2);root.Children.Add(apply);
                apply.Click+=(s,e)=>{var candidate=controls.Select(row=>new HotkeySetting{ActionId=row.Key,Enabled=row.Value.Item1.IsChecked==true,Modifiers=HotkeyRules.ModifierValues[row.Value.Item2.SelectedIndex],Key=keyChoices[(string)row.Value.Item3.SelectedItem]}).OrderBy(k=>k.ActionId).ToList();string error;
                    if(!HotkeyRules.Same(Prefs.Hotkeys,candidate)&&!Dock.ApplyHotkeys(candidate,out error)){MessageBox.Show(w,error,"快捷键未保存");return;}
                    Prefs.Hotkeys=candidate.Select(k=>k.Copy()).ToList();Save();RefreshTrayMenu();w.Close();};
                w.Show();shown=true;
            }finally{if(!shown){if(settingsWindow!=null)settingsWindow.Close();cleanup();}}
        }
        bool settingsOpen;Window settingsWindow;TabControl settingsTabs;
        public void ShellAction(int action,bool? hidden=null) {
            try {
                string message;
                if(action==1){bool next=hidden??!DesktopVisibility.DesiredHidden(true);if(next&&TaskbarAutoHide.Enabled)TaskbarAutoHide.Set(false);message=DesktopVisibility.SetHidden(true,next);}else if(action==2)message=hidden.HasValue?DesktopVisibility.SetHidden(false,hidden.Value):DesktopVisibility.ToggleIcons();else {try{TaskbarAutoHide.ShowAndClear();}catch(Exception ex){Store.Log(ex);}DesktopVisibility.ShowAll();message="已执行任务栏与桌面图标恢复";}
                Board.SetStatus(message);if(Tray!=null)Tray.ShowBalloonTip(1500,"GlassBoard",message,Forms.ToolTipIcon.Info);
            }catch(Exception ex){Store.Log(ex);MessageBox.Show("无法切换系统界面："+ex.Message,"GlassBoard");}
        }
        public void Quit() {
            if(ShuttingDown)return;
            try{if(settingsWindow!=null)settingsWindow.Close();Store.Save(Prefs);if(widgets!=null&&!widgets.Notes.Flush())throw new IOException("备忘录未能保存，请先导出备忘录或检查配置目录。");}
            catch(Exception ex){Store.Log(ex);MessageBox.Show("退出前保存失败，软件仍保持运行，避免丢失当前内容。\n"+ex.Message,"GlassBoard");return;}
            ShuttingDown=true;
            try{DesktopVisibility.RestoreOriginal();}catch(Exception ex){Store.Log(ex);}try{TaskbarAutoHide.Restore();}catch(Exception ex){Store.Log(ex);}
            try{CloseControlCenter();CloseWidgets();}catch(Exception ex){Store.Log(ex);}if(Tray!=null){Tray.Visible=false;Tray.Dispose();}Dock.ReleaseHotkey();Application.Current.Shutdown();
        }
    }
    public class AddCandidate : INotifyPropertyChanged {
        public string Path {get;set;}
        public string Name {get{return EntryPathRules.Name(Path);}}
        bool selected=true;
        public bool Selected {get{return selected;}set{if(selected==value)return;selected=value;if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs("Selected"));}}
        public event PropertyChangedEventHandler PropertyChanged;
    }
    public class BatchAddWindow : Window {
        readonly Controller C;readonly string targetGroup;readonly CancellationTokenSource cancellation=new CancellationTokenSource();
        readonly List<AddCandidate> candidates=new List<AddCandidate>();ListBox list;TextBlock status;Button add;WrapPanel sources;CheckBox recursive,launchOnly;bool busy,closed,changing;
        public BatchAddWindow(Controller controller) {
            C=controller;targetGroup=C.Board.IsVisible?C.Board.CurrentGroup:"全部";Title="GlassBoard · 添加到 "+targetGroup;Width=Math.Min(800,SystemParameters.WorkArea.Width-40);Height=Math.Min(670,SystemParameters.WorkArea.Height-50);
            WindowStartupLocation=WindowStartupLocation.CenterOwner;Owner=C.Board.IsVisible?(Window)C.Board:C.Dock;Topmost=true;Background=new SolidColorBrush(Color.FromRgb(32,34,42));Foreground=Brushes.White;
            var root=new Grid{Margin=new Thickness(24)};Content=root;root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            var header=new StackPanel();root.Children.Add(header);header.Children.Add(new TextBlock{Text="一次加入应用、文件与文件夹",FontSize=22,Margin=new Thickness(0,0,0,8)});
            header.Children.Add(new TextBlock{Text="添加位置："+targetGroup+" · 选择来源并勾选，已有入口会自动去重。",Foreground=Brushes.LightGray,Margin=new Thickness(0,0,0,12)});
            sources=new WrapPanel();header.Children.Add(sources);
            sources.Children.Add(MakeButton("选择多个文件",PickFiles));sources.Children.Add(MakeButton("添加文件夹入口",()=>{string folder=FeatureUi.PickFolder(this,"添加文件夹本身作为入口（不扫描里面的文件）");if(folder!=null)Merge(new[]{folder});}));sources.Children.Add(MakeButton("扫描文件夹",PickFolder));
            sources.Children.Add(MakeButton("读取桌面",()=>Scan(new string[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)},false)));
            sources.Children.Add(MakeButton("读取开始菜单",()=>Scan(new string[]{Environment.GetFolderPath(Environment.SpecialFolder.Programs),Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)},true)));
            recursive=new CheckBox{Content="扫描文件夹时包含子文件夹",IsChecked=true,Foreground=Brushes.White,Margin=new Thickness(0,8,0,12)};header.Children.Add(recursive);launchOnly=new CheckBox{Content="只扫描启动入口（过滤卸载、更新、辅助进程和网页书签）",IsChecked=true,Foreground=Brushes.White,Margin=new Thickness(0,0,0,10)};header.Children.Add(launchOnly);
            list=new ListBox{Background=new SolidColorBrush(Color.FromRgb(25,27,34)),BorderThickness=new Thickness(0),Foreground=Brushes.White,Margin=new Thickness(0,0,0,12)};
            VirtualizingStackPanel.SetIsVirtualizing(list,true);VirtualizingStackPanel.SetVirtualizationMode(list,VirtualizationMode.Recycling);
            ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);
            var itemStyle=new Style(typeof(ListBoxItem));itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));list.ItemContainerStyle=itemStyle;
            var check=new FrameworkElementFactory(typeof(CheckBox));check.SetBinding(ToggleButton.IsCheckedProperty,new Binding("Selected"){Mode=BindingMode.TwoWay});check.SetValue(FrameworkElement.MarginProperty,new Thickness(6));check.SetValue(Control.ForegroundProperty,Brushes.White);
            var labels=new FrameworkElementFactory(typeof(StackPanel));var name=new FrameworkElementFactory(typeof(TextBlock));name.SetBinding(TextBlock.TextProperty,new Binding("Name"));name.SetValue(TextBlock.FontSizeProperty,14.0);labels.AppendChild(name);
            var path=new FrameworkElementFactory(typeof(TextBlock));path.SetBinding(TextBlock.TextProperty,new Binding("Path"));path.SetValue(TextBlock.FontSizeProperty,11.0);path.SetValue(TextBlock.ForegroundProperty,Brushes.LightGray);path.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);labels.AppendChild(path);check.AppendChild(labels);
            list.ItemTemplate=new DataTemplate{VisualTree=check};Grid.SetRow(list,1);root.Children.Add(list);
            var footer=new StackPanel();Grid.SetRow(footer,2);root.Children.Add(footer);
            status=new TextBlock{Text="请选择文件，或读取桌面 / 开始菜单。",Margin=new Thickness(0,0,0,10),Foreground=Brushes.LightGray};footer.Children.Add(status);
            var actions=new WrapPanel();footer.Children.Add(actions);actions.Children.Add(MakeButton("全选",()=>SelectAll(true)));actions.Children.Add(MakeButton("取消全选",()=>SelectAll(false)));
            add=MakeButton("添加已选（0）",()=>{var paths=candidates.Where(x=>x.Selected).Select(x=>x.Path).ToArray();int before=C.Prefs.Apps.Count(a=>FeatureRules.InGroup(a,targetGroup));C.Add(paths,true,targetGroup);C.Board.ClearSearch();C.Board.SetStatus("已添加 "+(C.Prefs.Apps.Count(a=>FeatureRules.InGroup(a,targetGroup))-before)+" 个入口");Close();});add.IsEnabled=false;actions.Children.Add(add);
            actions.Children.Add(MakeButton("关闭",()=>Close()));Closed+=(s,e)=>{closed=true;cancellation.Cancel();};
        }
        static Button MakeButton(string title,Action action) {var b=new Button{Content=title,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,8,4)};b.Click+=(s,e)=>action();return b;}
        void SelectAll(bool value) {if(busy)return;changing=true;foreach(var c in candidates)c.Selected=value;changing=false;UpdateCount();}
        void UpdateCount() {int count=candidates.Count(x=>x.Selected);add.Content="添加已选（"+count+"）";add.IsEnabled=!busy&&count>0;if(!busy)status.Text="共 "+candidates.Count+" 个新入口 · 已选 "+count+" 个";}
        void Merge(IEnumerable<string> paths) {
            var known=new HashSet<string>(candidates.Select(c=>c.Path),StringComparer.OrdinalIgnoreCase);
            foreach(var path in EntryRules.Candidates(paths,C.Prefs.Apps.Where(a=>targetGroup=="全部"||FeatureRules.InGroup(a,targetGroup))))if(known.Add(path)) {
                var item=new AddCandidate{Path=path};item.PropertyChanged+=(s,e)=>{if(!changing)UpdateCount();};candidates.Add(item);
            }
            candidates.Sort((a,b)=>StringComparer.CurrentCultureIgnoreCase.Compare(a.Name,b.Name));list.ItemsSource=null;list.ItemsSource=candidates;UpdateCount();
        }
        void PickFiles() {
            var dialog=new Microsoft.Win32.OpenFileDialog{Title="按 Ctrl / Shift 多选软件、快捷方式或文件",Multiselect=true,Filter="应用、文档与表格|*.exe;*.lnk;*.appref-ms;*.url;*.cmd;*.bat;*.doc;*.docx;*.docm;*.xls;*.xlsx;*.xlsm;*.csv;*.ppt;*.pptx;*.pptm;*.wps;*.et;*.dps;*.xlsb;*.pdf;*.txt;*.rtf;*.md|所有文件|*.*"};
            if(dialog.ShowDialog(this)==true){if(dialog.FileNames.Any(DocumentRules.Supported))launchOnly.IsChecked=false;Merge(dialog.FileNames);}
        }
        void PickFolder() {
            using(var dialog=new Forms.FolderBrowserDialog{Description="选择要扫描的软件或快捷方式文件夹",ShowNewFolderButton=false}) {
                var owner=new Forms.NativeWindow();owner.AssignHandle(new WindowInteropHelper(this).Handle);
                try{if(dialog.ShowDialog(owner)==Forms.DialogResult.OK)Scan(new string[]{dialog.SelectedPath},recursive.IsChecked==true);}finally{owner.ReleaseHandle();}
            }
        }
        async void Scan(string[] roots,bool recurse) {
            if(busy)return;busy=true;sources.IsEnabled=false;recursive.IsEnabled=false;add.IsEnabled=false;list.IsEnabled=false;status.Text="正在扫描，可关闭窗口取消…";
            try {
                bool only=launchOnly.IsChecked==true;var found=await Task.Run(()=>{var result=ImportScanner.Scan(roots,recurse,cancellation.Token,2500,10000,only);if(only)result.Paths=result.Paths.Where(path=>{cancellation.Token.ThrowIfCancellationRequested();return LaunchInfoService.Primary(LaunchInfoService.Read(path));}).ToList();return result;},cancellation.Token);
                if(closed)return;busy=false;Merge(found.Paths);if(found.Skipped>0)status.Text+=" · 跳过 "+found.Skipped+" 个无法读取的目录";if(found.Limited)status.Text+=" · 达到扫描上限，请分文件夹添加";
            }catch(OperationCanceledException){}catch(Exception ex){if(!closed){Store.Log(ex);status.Text="扫描失败："+ex.Message;}}
            finally {busy=false;if(!closed){sources.IsEnabled=true;recursive.IsEnabled=true;list.IsEnabled=true;add.IsEnabled=candidates.Any(x=>x.Selected);}}
        }
    }
    public partial class DockWindow : GlassWindow {
        Controller C;StackPanel Items=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};
        HwndSource source;HotkeyManager hotkeys;
        DispatcherTimer autoTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)},runningTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(5)},foregroundTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(300)};
        DateTime lastRunningScan=DateTime.MinValue;AutoHidePolicy hidePolicy=new AutoHidePolicy();bool renderedHidden;double shownTop;Rect dockWorkingArea,dockScreenArea;Thumb moveGrip;bool dragging;
        public double RestingTop {get{return shownTop;}}
        public void GamePolling(bool enabled){var interval=TimeSpan.FromMilliseconds(enabled?350:5000);if(runningTimer.Interval!=interval)runningTimer.Interval=interval;}
        public DockWindow(Controller c):base(29) {
            C=c;Title="GlassBoard Dock";Height=112;ShowActivated=false;Topmost=false;ContentGrid.Children.Add(Items);InitDockPointer();InitializeBackgroundMove();InitTaskDock();
            moveGrip=new Thumb{Width=42,Height=6,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,7,0,0),Cursor=Cursors.SizeAll,ToolTip="拖动 Dock；设置中可锁定位置"};Root.Children.Add(moveGrip);
            moveGrip.Template=InvisibleDockGrip();Closing+=(s,e)=>{if(!C.ShuttingDown){e.Cancel=true;Dispatcher.BeginInvoke(new Action(()=>C.Quit()));}};
            moveGrip.DragStarted+=(s,e)=>BeginDockMove();moveGrip.DragDelta+=(s,e)=>{if(!dragging)return;var area=DockPlacementArea(true);Left=FeatureRules.Position(Left+e.HorizontalChange,area.Left,area.Width,Width);Top=DockGeometryRules.Top(Top+e.VerticalChange,area,Height);};moveGrip.DragCompleted+=(s,e)=>FinishDockMove();
            autoTimer.Tick+=(s,e)=>{RunningWindows.RememberForeground();UpdateAutoHide();};foregroundTimer.Tick+=(s,e)=>RunningWindows.RememberForeground();foregroundTimer.Start();runningTimer.Tick+=(s,e)=>{ReleaseCancelledExternalDrag();if(boardPinDragging)return;C.CheckGameMode();if(!C.Prefs.AutoGameMode){var slow=IdleState.Seconds()>=90?TimeSpan.FromSeconds(15):TimeSpan.FromSeconds(5);if(runningTimer.Interval!=slow)runningTimer.Interval=slow;}if(C.GlassSuppressed||(!IsVisible&&!C.Board.IsVisible))return;if((!C.Prefs.Dock.ShowRunning&&!taskWheel.Running)||(DateTime.UtcNow-lastRunningScan).TotalSeconds<(renderedHidden?20:5))return;lastRunningScan=DateTime.UtcNow;RunningWindows.Scan();RefreshTaskSnapshot();UpdateRunning();C.Board.UpdateRunning();};runningTimer.Start();
            SourceInitialized+=(s,e)=> {
                source=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);source.AddHook(Hook);
                hotkeys=new HotkeyManager(RegisterKey,id=>Native.UnregisterHotKey(source.Handle,id));
                var failed=hotkeys.Start(C.Prefs.Hotkeys);
                if(failed.Count>0)TrayWarning(string.Join("、",failed));
            };
            var menu=MenuTheme.Create(C.Prefs.Light);var addDock=new MenuItem{Header="添加软件或文件到 Dock"};addDock.Click+=(ss,ee)=>C.ChooseDockFiles();menu.Items.Add(addDock);var settings=new MenuItem{Header="外观设置"};settings.Click+=(s,e)=>C.Settings();menu.Items.Add(settings);AddDockControls(menu);
            var quit=new MenuItem{Header="退出"};quit.Click+=(s,e)=>C.Quit();menu.Items.Add(quit);ContextMenu=menu;
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplayChanged;
        }
        void TrayWarning(string shortcuts) {Dispatcher.BeginInvoke(new Action(()=> {if(C.Tray!=null)C.Tray.ShowBalloonTip(3000,"GlassBoard",shortcuts+" 已被占用，对应功能仍可从托盘操作。",Forms.ToolTipIcon.Info);}));}
        bool RegisterKey(HotkeySetting key) {bool ok=Native.RegisterHotKey(source.Handle,key.ActionId,key.Modifiers|0x4000,key.Key);if(!ok)Store.Log(new Win32Exception(Marshal.GetLastWin32Error(),"RegisterHotKey: "+HotkeyRules.Actions[key.ActionId-1]+" "+HotkeyRules.Label(key)));return ok;}
        void DisplayChanged(object s,EventArgs e) {Dispatcher.BeginInvoke(new Action(()=>{C.Board.Hide();Refresh();}));}
        public bool ApplyHotkeys(List<HotkeySetting> candidate,out string error) {if(hotkeys==null){error="快捷键窗口尚未就绪。";return false;}return hotkeys.TryApply(candidate,out error);}
        public bool HotkeyActive(int id) {return hotkeys!=null&&hotkeys.Active.Any(k=>k.ActionId==id);}
        public string RetryHotkeys() {if(hotkeys==null)return "快捷键窗口尚未就绪。";hotkeys.Release();var failed=hotkeys.Start(C.Prefs.Hotkeys);return failed.Count==0?"已重新注册启用的快捷键。":"以下组合被占用，请改键或使用开关：\n"+string.Join("\n",failed);}
        public List<HotkeySetting> SuspendHotkeys(){var previous=hotkeys==null?new List<HotkeySetting>():hotkeys.Active.Select(k=>k.Copy()).ToList();if(hotkeys!=null)hotkeys.Release();return previous;}
        public void ResumeHotkeys(List<HotkeySetting> keys){if(hotkeys==null)return;hotkeys.Release();var failed=hotkeys.Start(keys);if(failed.Count>0)TrayWarning(string.Join("、",failed));}
        public void ReleaseHotkey() {CancelDockPointer(false);autoTimer.Stop();runningTimer.Stop();foregroundTimer.Stop();taskHintTimer.Stop();if(hotkeys!=null)hotkeys.Release();Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=DisplayChanged;}
        IntPtr Hook(IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled) {
            // Preserve the application's foreground state until the icon action
            // decides whether to restore or minimize it; still deliver the click.
            if(msg==0x21){handled=true;return new IntPtr(3);} // MA_NOACTIVATE
            if(msg==Native.RestoreMessage){handled=true;Dispatcher.BeginInvoke(new Action(()=>C.ShellAction(3)));return IntPtr.Zero;}
            if(msg==0x312){int id=w.ToInt32();handled=true;Dispatcher.BeginInvoke(new Action(()=>{if(id==1)C.Toggle();else if(id==2)C.ShellAction(1);else if(id==3)C.ShellAction(2);else if(id==4)C.AddFromAnywhere();else if(id==5)C.Settings();else if(id==6)C.ShellAction(3);else if(id==7)C.ToggleTaskbarAutoHide();}));}return IntPtr.Zero;
        }
        public void Refresh(bool capture=true) {
            if(DockInputBusy)return;
            double oldWidth=Width,oldLeft=Left;StopDockLayoutMotion();
            var o=C.Prefs.Dock;o.Normalize();LightTheme=C.Prefs.Light;MotionEnabled=o.OpenMotion;MotionLevel=o.MotionLevel;OpenStyle=o.OpenAnimationStyle;ConfigureGlass(o);Topmost=InteractionRules.DockTopmost(o,boardPinDragging);RefreshLayer();
            revealSerial++;BeginAnimation(TopProperty,null);renderedHidden=false;hidePolicy=new AutoHidePolicy();moveGrip.Visibility=Visibility.Visible;moveGrip.Cursor=C.Prefs.Placement.DockLocked?Cursors.Arrow:Cursors.SizeAll;moveGrip.ToolTip=C.Prefs.Placement.DockLocked?"右键 Dock → 允许自由拖动 Dock":"拖动顶部空白区域移动 Dock，松手保存";
            Items.Children.Clear();Items.Margin=DockGeometryRules.Insets(o);Surface.CornerRadius=new CornerRadius(o.Radius);
            AddSystemButton(DockLayoutRules.Apps,DockButton("▦",null,()=>C.Toggle(),"展开大号应用面板"),()=>C.Toggle());
            if(o.ShowComputer)AddSystemButton(DockLayoutRules.Computer,DockButton("computer",null,()=> {C.Board.Hide();Native.Computer();},"此电脑"),()=>{C.Board.Hide();Native.Computer();});
            
            var area=DockPlacementArea();dockWorkingArea=area;dockScreenArea=DesktopWindowPolicy.WorkArea(this,false,true);var pinned=DockOrderRules.Ordered(C.Prefs);int displayCount=taskWheel.Running?taskGroups.Count:pinned.Count;int slots=o.VisiblePinned(area.Width-20,displayCount);
            int shown=displayCount>slots?Math.Max(0,slots-1):slots;
            if(taskWheel.Running)BuildTaskButtons(shown,slots);else {
            foreach(var a in pinned.Take(shown)) {var b=DockButton(null,FeatureUi.Icon(a,C.Prefs.Dock.ShowShortcutOverlays,C.Prefs.Dock.SharperIcons),()=>C.LaunchDock(a),a.Name,a);b.ContextMenu=C.Menu(a,"Dock");AddDockControls(b.ContextMenu);Items.Children.Add(b);}
            if(pinned.Count>shown&&slots>0) {
                var more=DockButton("···",null,()=>{},"更多固定应用");var menu=MenuTheme.Create(C.Prefs.Light);
                foreach(var a in pinned.Skip(shown)) {var item=new MenuItem{Header=a.Name};var entry=a;item.Click+=(s,e)=>{if(!DockDragSuppresses(item))C.LaunchDock(entry);};BindDockSource(item,entry,()=>menu.IsOpen=false);menu.Items.Add(item);}
                AddDockControls(menu);more.ContextMenu=menu;more.Click+=(s,e)=>{menu.PlacementTarget=more;menu.Placement=PlacementMode.Top;menu.IsOpen=true;};Items.Children.Add(more);
            }
            if(pinned.Count==0) Items.Children.Add(DockButton("+",null,()=>C.Toggle(true),"打开面板，可拖入软件固定"));
            }
            AddQuickButtons();
            if(o.ShowSettings)AddSystemButton(DockLayoutRules.Settings,DockButton("⚙",null,()=>C.Settings(),"DIY 设置"),()=>C.Settings());
            ArrangeDockItems();
            Width=Math.Min(area.Width-20,o.WidthFor(displayCount==0?1:slots));
            Height=DockGeometryRules.Height(o);
            double x=o.Alignment==0?area.Left+10:o.Alignment==2?area.Right-Width-10:area.Left+(area.Width-Width)/2;
            var placement=C.Prefs.Placement;
            if(placement.DockLeft.HasValue&&!placement.DockCenter.HasValue)placement.DockCenter=placement.DockLeft.Value+Width/2;
            Left=DockAnchorRules.Left(placement.DockLeft.HasValue?placement.DockCenter:null,Width,x+o.Offset,area.Left,area.Width);Top=DockGeometryRules.Top(placement.DockTop??DockGeometryRules.BottomTop(area,Height,C.Prefs.DockGap),area,Height);shownTop=Top;
            if(o.AutoHide)autoTimer.Start();else autoTimer.Stop();
            GlassPercent=o.OpacityPercent;
            if(capture)Backdrop(C.Prefs.Light);else {
                SetGlassOpacity(GlassPercent);
                foreach(var host in Root.Children.OfType<Border>().Where(b=>b.Tag as string=="backdrop")) {
                    var clip=new RectangleGeometry(new Rect(0,0,Math.Max(1,Width-28),Math.Max(1,Height-28)),o.Radius,o.Radius);host.Clip=clip;
                    var bg=host.Child as Border;if(bg!=null){bg.Clip=clip;bg.CornerRadius=Surface.CornerRadius;bg.Effect=new BlurEffect{Radius=MaterialBlur,RenderingBias=RenderingBias.Performance};}
                }
            }
            var accent=Accent(o.Accent);Surface.BorderBrush=GlassPercent==0?(Brush)Brushes.Transparent:new LinearGradientBrush(Color.FromArgb((byte)Math.Min(255,160*EdgeStrength),accent.R,accent.G,accent.B),Color.FromArgb((byte)Math.Min(255,30*EdgeStrength),255,255,255),90);UpdateRunning();if(!capture)AnimateDockLayout(oldLeft,oldWidth);
        }
        void UpdateAutoHide(){ReleaseCancelledExternalDrag();if(C.GlassSuppressed||source==null||!IsVisible)return;RefreshLayer();if(C.Prefs.Dock.RespectFullscreen&&FullscreenState.Active(source.Handle)&&!boardPinDragging){ForceHideForFullscreen();return;}var position=Forms.Cursor.Position;var pointer=source.CompositionTarget.TransformFromDevice.Transform(new Point(position.X,position.Y));double bottom=dockScreenArea.Bottom;
            bool over=!renderedHidden&&pointer.X>=Left&&pointer.X<=Left+Width&&pointer.Y>=shownTop&&pointer.Y<=shownTop+Height;
            bool edge=pointer.X>=Left-12&&pointer.X<=Left+Width+12&&pointer.Y>=bottom-C.Prefs.Dock.EdgeRange;
            bool menu=(ContextMenu!=null&&ContextMenu.IsOpen)||Items.Children.OfType<Button>().Any(b=>b.ContextMenu!=null&&b.ContextMenu.IsOpen);
            bool hide=hidePolicy.Update(DateTime.UtcNow,C.Prefs.Dock.AutoHide,over,edge,dragging||DockInputBusy||externalDockDrag||(taskMenu!=null&&taskMenu.IsOpen)||menu||C.UiBusy||C.Board.IsVisible,C.Prefs.Dock.HideDelay);
            if(hide==renderedHidden)return;renderedHidden=hide;double target=hide?bottom-20:shownTop;AnimateDockVisibility(hide,target);
        }
        void UpdateRunning(){foreach(var button in Items.Children.OfType<Button>()){var entry=button.Tag as Entry;var grid=button.Content as Grid;if(entry==null||grid==null)continue;foreach(var dot in grid.Children.OfType<Border>().Where(b=>b.Tag as string=="running"))dot.Visibility=C.Prefs.Dock.ShowRunning&&RunningWindows.IsRunning(entry)?Visibility.Visible:Visibility.Collapsed;}}
        static Color Accent(int index) {return new Color[]{Color.FromRgb(121,185,250),Color.FromRgb(183,149,245),Color.FromRgb(105,218,183),Color.FromRgb(242,193,104),Color.FromRgb(244,150,178),Color.FromRgb(194,203,217)}[index];}
        Button DockButton(string text,ImageSource icon,Action action,string tip,Entry entry=null) {
            var o=C.Prefs.Dock;double size=o.IconSize;
            var b=Button("",(s,e)=>{if(!dockIconDragging&&!DockDragSuppresses(s as FrameworkElement))action();},size+16);b.Height=size+16+DockReflection.ExtraHeight(o)+(o.ShowLabels?18:0);b.Margin=new Thickness(o.Spacing/2,0,o.Spacing/2,0);b.ToolTip=tip;
            ToolTipService.SetIsEnabled(b,false);
            var glass=new FrameworkElementFactory(typeof(Border));glass.SetValue(Border.BackgroundProperty,new SolidColorBrush(Color.FromArgb(1,255,255,255)));
            var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);glass.AppendChild(presenter);b.Template=new ControlTemplate(typeof(Button)){VisualTree=glass};
            FrameworkElement visual;
            if(text=="computer") {
                var color=Accent(o.Accent);var monitor=new Grid{Width=44,Height=44};bool badge=o.ComputerStyle==0;
                if(badge)monitor.Children.Add(new Border{CornerRadius=new CornerRadius(12),Background=new LinearGradientBrush(Color.FromRgb((byte)(color.R*.65),(byte)(color.G*.65),(byte)(color.B*.65)),color,45),BorderThickness=new Thickness(1),BorderBrush=new SolidColorBrush(Color.FromArgb(90,255,255,255))});
                var stroke=badge?Brushes.White:TextBrush;
                monitor.Children.Add(new Border{Height=badge?19:25,Width=badge?29:38,CornerRadius=new CornerRadius(3),BorderThickness=new Thickness(1.6),BorderBrush=stroke,Background=badge?new SolidColorBrush(Color.FromArgb(36,255,255,255)):Brushes.Transparent,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,badge?9:5,0,0)});
                monitor.Children.Add(new Border{Width=2,Height=5,Background=stroke,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,badge?28:30,0,0)});
                monitor.Children.Add(new Border{Width=15,Height=1.6,CornerRadius=new CornerRadius(1),Background=stroke,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,badge?33:35,0,0)});
                if(badge)monitor.Children.Add(new Border{Width=19,Height=1,Background=new SolidColorBrush(Color.FromArgb(100,255,255,255)),VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,12,0,0)});
                visual=new Viewbox{Width=size,Height=size,Child=monitor,Stretch=Stretch.Uniform};
            }
            else if(icon!=null)visual=new Image{Source=icon,Width=size,Height=size,Stretch=Stretch.Uniform};
            else visual=new TextBlock{Text=text??"□",FontSize=size*.72,Width=size,Height=size,TextAlignment=TextAlignment.Center,Foreground=TextBrush,VerticalAlignment=VerticalAlignment.Center};
            FrameworkElement mirror=DockReflection.Create(visual,o);
            var box=new StackPanel{VerticalAlignment=VerticalAlignment.Center};box.Children.Add(visual);if(mirror!=null)box.Children.Add(mirror);
            if(o.ShowLabels)box.Children.Add(new TextBlock{Text=text=="computer"?"此电脑":text=="▦"?"应用":text=="⚙"?"设置":text=="···"?"更多":tip,FontSize=10,Foreground=MutedBrush,TextAlignment=TextAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=size+12,Margin=new Thickness(0,4,0,0)});
            var content=new Grid{IsHitTestVisible=false};content.Children.Add(box);if(entry!=null){b.Tag=entry;content.Children.Add(new Border{Tag="running",Width=5,Height=4,CornerRadius=new CornerRadius(2),Background=new SolidColorBrush(Color.FromRgb(135,205,255)),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Visibility=Visibility.Collapsed});}b.Content=content;
            var scale=new ScaleTransform(1,1);var lift=new TranslateTransform();var transforms=new TransformGroup();transforms.Children.Add(scale);transforms.Children.Add(lift);b.RenderTransform=transforms;b.RenderTransformOrigin=new Point(.5,.65);
            Action<bool> animate=hover=> {if(!C.Prefs.Dock.HoverMotion)return;var ease=new CubicEase{EasingMode=EasingMode.EaseOut};double target=hover&&InteractionRules.ZoomHover(o.HoverAnimationStyle)?Math.Min(1.6,1+(o.Zoom-1)*MotionLevel):1;
                scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(target,TimeSpan.FromMilliseconds(180)){EasingFunction=ease});scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(target,TimeSpan.FromMilliseconds(180)){EasingFunction=ease});lift.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(hover&&InteractionRules.LiftHover(o.HoverAnimationStyle)?-5*MotionLevel:0,TimeSpan.FromMilliseconds(180)){EasingFunction=ease});};
            b.MouseEnter+=(s,e)=>{if(!DockInputBusy)animate(true);};b.MouseLeave+=(s,e)=>animate(false);if(entry!=null)BindDockIcon(b,entry);return b;
        }
    }
    public partial class BoardWindow : GlassWindow {
        public bool ModalBusy;
        internal IntPtr foregroundHint;
        Controller C;TextBox Search;UniformGrid Tiles;TextBlock Pager,Status,Empty;Grid layout;int page;int capacity=35;int columns=7;Point dragStart;Entry dragEntry;bool editing;
        readonly HashSet<string> selected=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        WrapPanel bulkBar;RowDefinition bulkRow;TextBlock selectionCount;Button selectAll,clearSelection,deleteSelected,undoRemoval,editSelection,moveSelected;
        ContextMenu iconMenu;ComboBox groups,moveGroup;string currentGroup="全部";bool syncingGroups,iconSizeSync;Slider boardIconSlider;TextBlock iconSizeText;Button addExisting;Thumb moveGrip,resizeGrip;int visibilitySerial;WrapPanel folderBar,docBar;string docFilter="全部",docSignature="";bool foldersVisible {get{return !C.Prefs.BoardHideGroups&&!C.Prefs.BoardHideFolders;}}
        readonly DispatcherTimer idleRelease=new DispatcherTimer{Interval=TimeSpan.FromSeconds(20)};bool preparingView;string pendingStatus;
        public string CurrentGroup {get{return currentGroup;}}
        public void ClearSearch(){if(Search!=null)Search.Clear();}
        public BoardWindow(Controller c):base(42) {
            C=c;Title="GlassBoard 应用启动台";AllowDrop=true;InitDragExperience();
            IsVisibleChanged+=(s,e)=>{if(IsVisible)idleRelease.Stop();else if(layout!=null)idleRelease.Start();};idleRelease.Tick+=(s,e)=>{if(IsVisible){idleRelease.Stop();return;}if(ReleaseHiddenView())idleRelease.Stop();};Closed+=(s,e)=>{idleRelease.Stop();sizeRefresh.Stop();};
            PreviewKeyDown+=(s,e)=> {
                if(Search==null||groupDragRunning)return;
                if(e.Key==Key.Escape){EndDragPreview();CollapseSmooth();e.Handled=true;}
                else if(!Search.IsKeyboardFocusWithin&&(Keyboard.Modifiers&ModifierKeys.Control)!=0&&(e.Key==Key.Z||e.Key==Key.Y)){if(e.Key==Key.Y||(Keyboard.Modifiers&ModifierKeys.Shift)!=0)C.RedoBoard();else C.UndoBoard();e.Handled=true;}
                else if(editing&&!Search.IsKeyboardFocusWithin&&e.Key==Key.A&&(Keyboard.Modifiers&ModifierKeys.Control)!=0){SelectFiltered();e.Handled=true;}
                else if(editing&&!Search.IsKeyboardFocusWithin&&e.Key==Key.Delete){C.RemoveMany(selected.ToArray());e.Handled=true;}
                else if(e.Key==Key.PageDown){ChangePage(1);e.Handled=true;}else if(e.Key==Key.PageUp){ChangePage(-1);e.Handled=true;}
                else if(e.Key==Key.Enter && Search.IsKeyboardFocusWithin&&!editing){var a=Filtered().FirstOrDefault();if(a!=null)C.Launch(a);e.Handled=true;}
            };
            Drop+=ExternalDrop;
            PreviewMouseRightButtonUp+=BoardBlankMenu;
            Deactivated+=(s,e)=> { Dispatcher.BeginInvoke(new Action(()=> {if(C.Prefs.BoardDismissOnDeactivate&&IsVisible&&!IsActive&&!ModalBusy&&!activeDrag&&!IsContextMenuOpen()&&Mouse.LeftButton!=MouseButtonState.Pressed)CollapseSmooth();}));};
            moveGrip=new Thumb{Width=60,Height=8,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,8,0,0),Cursor=Cursors.SizeAll,ToolTip="拖动面板位置"};Root.Children.Add(moveGrip);
            resizeGrip=new Thumb{Width=20,Height=20,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,5,5),Cursor=Cursors.SizeNWSE,ToolTip="拖动调整面板大小"};Root.Children.Add(resizeGrip);
            moveGrip.Template=GripTemplate();resizeGrip.Template=GripTemplate(true);Closing+=(s,e)=>{if(!C.ShuttingDown){e.Cancel=true;CollapseSmooth();}};
            moveGrip.DragStarted+=(s,e)=>ModalBusy=true;moveGrip.DragDelta+=(s,e)=>{if(C.Prefs.Placement.PanelLocked)return;var area=SystemParameters.WorkArea;Left=FeatureRules.Position(Left+e.HorizontalChange,area.Left,area.Width,Width);Top=FeatureRules.Position(Top+e.VerticalChange,area.Top,area.Height,Height);};
            InitResizeRefresh();
            resizeGrip.DragStarted+=(s,e)=>{ModalBusy=true;resizingPanel=true;sizeRefresh.Stop();};resizeGrip.DragDelta+=(s,e)=>{if(C.Prefs.Placement.PanelLocked)return;var area=SystemParameters.WorkArea;Width=Math.Max(Math.Min(860,area.Width-32),Math.Min(area.Width-32,Width+e.HorizontalChange));Height=Math.Max(Math.Min(560,area.Height-32),Math.Min(area.Height-32,Height+e.VerticalChange));Left=FeatureRules.Position(Left,area.Left,area.Width,Width);Top=FeatureRules.Position(Top,area.Top,area.Height,Height);};
            moveGrip.DragCompleted+=(s,e)=>{try{SavePlacement();Backdrop(C.Prefs.Light);}finally{ModalBusy=false;}};resizeGrip.DragCompleted+=(s,e)=>{try{resizingPanel=false;SavePlacement();QueueSizeRefresh();Backdrop(C.Prefs.Light);}finally{ModalBusy=false;}};
        }
        void SavePlacement(){var p=C.Prefs.Placement;p.PanelLeft=Left;p.PanelTop=Top;p.PanelWidth=Width;p.PanelHeight=Height;C.Save();}
        void BuildForSize(){
            string query=C.Prefs.BoardHideSearch||Search==null?"":Search.Text;
            if(C.Prefs.BoardHideGroups)currentGroup="全部";
            string key=ViewStructureKey();bool changed=layout==null||viewStructureKey!=key;
            if(changed){columns=BoardIconRules.Columns(Width-104,C.Prefs.BoardIconSize,C.Prefs.BoardIconGap);int rows=BoardIconRules.Rows(Height-(C.Prefs.BoardHideGroups?180:258),C.Prefs.BoardIconSize,C.Prefs.BoardIconGap);capacity=columns*rows;Build(rows,query);viewStructureKey=key;}
            else{SyncGroups();if(viewRenderDirty||viewDataKey!=ViewDataKey())Render(false);}
        }
        public void RefreshIcons(){if(boardIconSlider!=null){iconSizeSync=true;try{boardIconSlider.Value=C.Prefs.BoardIconSize;iconSizeText.Text="图标 "+Math.Round(C.Prefs.BoardIconSize)+" px";}finally{iconSizeSync=false;}}iconsDirty=true;QueueSizeRefresh();}
        void IconSettings(){var menu=MenuTheme.Create(C.Prefs.Light);iconMenu=menu;menu.Closed+=(s,e)=>{if(iconMenu==menu)iconMenu=null;};Action<string,double,double,double,Action<double>> add=(name,min,max,value,change)=>{var box=new StackPanel{Width=230};var label=new TextBlock{Text=name+"："+Math.Round(value)+" px",Foreground=TextBrush,Margin=new Thickness(0,0,0,8)};box.Children.Add(label);var slider=new Slider{Minimum=min,Maximum=max,Value=value,TickFrequency=1,IsSnapToTickEnabled=true,Style=Resources[typeof(Slider)] as Style};box.Children.Add(slider);slider.ValueChanged+=(s,e)=>{label.Text=name+"："+Math.Round(slider.Value)+" px";change(slider.Value);RefreshIcons();C.Save();};menu.Items.Add(new MenuItem{Header=box,StaysOpenOnClick=true});};add("图标大小",28,80,C.Prefs.BoardIconSize,v=>C.Prefs.BoardIconSize=v);add("图标间距",0,20,C.Prefs.BoardIconGap,v=>C.Prefs.BoardIconGap=v);var reset=new MenuItem{Header="恢复默认"};reset.Click+=(s,e)=>{C.Prefs.BoardIconSize=48;C.Prefs.BoardIconGap=5;RefreshIcons();C.Save();};menu.Items.Add(reset);menu.PlacementTarget=this;menu.IsOpen=true;}

        public void CollapseSmooth(){StopOpeningFrame();EndDragPreview();if(!IsVisible)return;int serial=++visibilitySerial;if(!MotionEnabled){Hide();return;}var animation=new DoubleAnimation(0,TimeSpan.FromMilliseconds(110));animation.Completed+=(s,e)=>{if(serial==visibilitySerial)Hide();};ContentGrid.BeginAnimation(OpacityProperty,animation);}
        internal bool ReleaseHiddenView(){if(IsVisible||preparingView||ModalBusy||activeDrag||IsMouseCaptureWithin)return false;sizeRefresh.Stop();StopOpeningFrame();viewStructureKey=viewDataKey=null;viewRenderDirty=true;if(iconMenu!=null)iconMenu.IsOpen=false;iconMenu=null;EndDragPreview();ContentGrid.Children.Clear();ReleaseBackdrop();Search=null;Tiles=null;Pager=Status=Empty=null;layout=null;bulkBar=folderBar=null;bulkRow=null;selectionCount=iconSizeText=null;selectAll=clearSelection=deleteSelected=undoRemoval=editSelection=moveSelected=addExisting=null;groups=moveGroup=null;boardIconSlider=null;dragEntry=null;return true;}
        internal bool HasView{get{return layout!=null;}}
        bool IsContextMenuOpen() {return (iconMenu!=null&&iconMenu.IsOpen)||(Tiles!=null && Tiles.Children.OfType<FrameworkElement>().Any(x=>x.ContextMenu!=null&&x.ContextMenu.IsOpen));}
        public void Open() {
            foregroundHint=RunningWindows.CaptureForegroundHint();idleRelease.Stop();EndDragPreview();preparingView=true;try{
            visibilitySerial++;
            var area=SystemParameters.WorkArea;
            double wantedW=C.Prefs.PanelSize==0?980:1280,wantedH=C.Prefs.PanelSize==0?620:730;
            if(C.Prefs.PanelSize==2){wantedW=area.Width-32;wantedH=area.Height-C.Dock.Height-40;}
            var placement=C.Prefs.Placement;Width=Math.Min(placement.PanelWidth??wantedW,area.Width-32);Height=Math.Min(placement.PanelHeight??wantedH,area.Height-32);
            Height=Math.Max(Math.Min(400,area.Height-130),Height);
            Width=Math.Max(Math.Min(860,area.Width-32),Width);Height=Math.Max(Math.Min(560,area.Height-32),Height);
            Left=FeatureRules.Position(placement.PanelLeft??(area.Left+(area.Width-Width)/2),area.Left,area.Width,Width);Top=FeatureRules.Position(placement.PanelTop??(area.Top+(area.Height-C.Dock.Height-Height)/2),area.Top,area.Height,Height);
            moveGrip.Visibility=resizeGrip.Visibility=placement.PanelLocked?Visibility.Collapsed:Visibility.Visible;
            viewRenderDirty|=editing||selected.Count>0;editing=false;selected.Clear();Topmost=C.Prefs.BoardAlwaysOnTop;RefreshLayer();LightTheme=C.Prefs.Light;MotionEnabled=C.Prefs.Dock.OpenMotion;MotionLevel=C.Prefs.Dock.MotionLevel;OpenStyle=C.Prefs.Dock.OpenAnimationStyle;ConfigureGlass(C.Prefs.Dock,C.Prefs.BoardMaterial);GlassPercent=C.Prefs.OpacityPercent??72;Polish.Apply(this,LightTheme);BuildForSize();Backdrop(C.Prefs.Light);PrepareOpeningFrame();Show();Activate();Native.SetForegroundWindow(new WindowInteropHelper(this).Handle);ScheduleOpeningFrame();if(!C.Prefs.BoardHideSearch)Search.Focus();else Focus();
            }finally{preparingView=false;}
        }
        void Build(int rows,string query="") {
            ContentGrid.Children.Clear();
            layout=new Grid{Margin=new Thickness(38,22,38,20)};
            layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(55)});
            layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            bulkRow=new RowDefinition{Height=new GridLength(0)};layout.RowDefinitions.Add(bulkRow);
            layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
            layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(45)});
            layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(32)});ContentGrid.Children.Add(layout);
            var header=new Grid();header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(116)});header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(80)});header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(40)});header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(40)});header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(44)});layout.Children.Add(header);
            var searchHost=new Border {CornerRadius=new CornerRadius(18),BorderThickness=new Thickness(1),BorderBrush=new SolidColorBrush(Color.FromArgb(90,255,255,255)),Background=new SolidColorBrush(Color.FromArgb(25,255,255,255)),Margin=new Thickness(10,0,18,9)};
            var searchGrid=new Grid();searchHost.Child=searchGrid;
            var hint=new TextBlock{Text="搜索应用、文件夹…",Foreground=MutedBrush,Margin=new Thickness(18,0,0,0),VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false};searchGrid.Children.Add(hint);
            Search=new TextBox{Text=query,FontSize=15,Foreground=TextBrush,Background=Brushes.Transparent,BorderThickness=new Thickness(0),CaretBrush=TextBrush,VerticalContentAlignment=VerticalAlignment.Center,Padding=new Thickness(18,0,18,0)};
            hint.Visibility=string.IsNullOrEmpty(query)?Visibility.Visible:Visibility.Hidden;
            Search.TextChanged+=(s,e)=>{hint.Visibility=Search.Text.Length==0?Visibility.Visible:Visibility.Hidden;page=0;Render(true);};searchGrid.Children.Add(Search);header.Children.Add(searchHost);searchHost.Visibility=C.Prefs.BoardHideSearch?Visibility.Collapsed:Visibility.Visible;
            if(C.Prefs.BoardHideSearch)header.Children.Add(new TextBlock{Text=C.Prefs.BoardHideGroups?"全部应用":currentGroup,Foreground=TextBrush,FontSize=18,Margin=new Thickness(12,0,0,8),VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false});
            var add=Button("+ 批量添加",(s,e)=>C.Choose(),106);Grid.SetColumn(add,1);header.Children.Add(add);
            editSelection=Button(editing?"完成":"多选",null,68);editSelection.ToolTip="跨页勾选，一次移除多个入口";editSelection.Click+=(s,e)=>{SetSelectionMode(!editing);Search.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));Render();};Grid.SetColumn(editSelection,2);header.Children.Add(editSelection);
            var settings=Button("⚙",(s,e)=>C.Settings(),30);settings.Content=Polish.Icon("settings",TextBrush,18);settings.ToolTip="外观、组件与快捷键";Grid.SetColumn(settings,3);header.Children.Add(settings);
            var display=Button("···",(s,e)=>MoreMenu(),32);display.ToolTip="更多与显示选项";Grid.SetColumn(display,4);header.Children.Add(display);
            var close=Button("×",(s,e)=>CollapseSmooth(),32);Grid.SetColumn(close,5);header.Children.Add(close);
            var groupBar=new WrapPanel{VerticalAlignment=VerticalAlignment.Center,Visibility=C.Prefs.BoardHideGroups?Visibility.Collapsed:Visibility.Visible};Grid.SetRow(groupBar,1);layout.Children.Add(groupBar);
            groups=new ComboBox{Width=145,Height=30,Margin=new Thickness(8,0,10,0)};groupBar.Children.Add(groups);groups.SelectionChanged+=(s,e)=>{if(syncingGroups)return;string nextGroup=groups.SelectedItem as string??"全部";if(nextGroup!=currentGroup){selected.Clear();docFilter="全部";}currentGroup=nextGroup;if(addExisting!=null)addExisting.IsEnabled=currentGroup!="全部";page=0;Render(true);};
            var density=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(6,0,8,0)};iconSizeText=new TextBlock{Text="图标 "+Math.Round(C.Prefs.BoardIconSize)+" px",Foreground=TextBrush,VerticalAlignment=VerticalAlignment.Center,Width=75};density.Children.Add(iconSizeText);boardIconSlider=new Slider{Minimum=28,Maximum=80,Value=C.Prefs.BoardIconSize,Width=110,TickFrequency=2,IsSnapToTickEnabled=true,ToolTip="缩小图标，让每页容纳更多入口"};boardIconSlider.ValueChanged+=(s,e)=>{if(iconSizeSync)return;C.Prefs.BoardIconSize=boardIconSlider.Value;iconSizeText.Text="图标 "+Math.Round(boardIconSlider.Value)+" px";RefreshIcons();C.Save();};density.Children.Add(boardIconSlider);groupBar.Children.Add(density);addExisting=Button("添加已有图标",(s,e)=>C.AddExistingToGroup(currentGroup),112);groupBar.Children.Add(addExisting);groupBar.Children.Add(Button("添加文件夹",(s,e)=>C.AddFolder(),100));groupBar.Children.Add(Button("新建",(s,e)=>NewFileMenu(),60));groupBar.Children.Add(Button("更多",(s,e)=>MoreMenu(),68));
            bulkBar=new WrapPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};Grid.SetRow(bulkBar,2);layout.Children.Add(bulkBar);
            selectAll=Button("全选结果",(s,e)=>SelectFiltered(),90);selectAll.ToolTip="选中当前搜索结果的所有页；已有勾选会保留";bulkBar.Children.Add(selectAll);
            clearSelection=Button("取消全选",(s,e)=>{selected.Clear();Render();},90);bulkBar.Children.Add(clearSelection);
            deleteSelected=Button("移除已选",(s,e)=>C.RemoveMany(selected.ToArray()),112);deleteSelected.Foreground=new SolidColorBrush(Color.FromRgb(255,155,155));bulkBar.Children.Add(deleteSelected);
            undoRemoval=Button("撤销",(s,e)=>C.UndoBoard(),72);groupBar.Children.Add(undoRemoval);
            selectionCount=new TextBlock{Foreground=MutedBrush,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(10,0,0,0)};bulkBar.Children.Add(selectionCount);
            moveGroup=new ComboBox{Width=112,Height=30,Margin=new Thickness(12,0,4,0),VerticalAlignment=VerticalAlignment.Center};bulkBar.Children.Add(moveGroup);moveSelected=Button("移动",(s,e)=>{string group=moveGroup.SelectedItem as string;if(group==null||selected.Count==0)return;C.TransferEntries(selected.ToArray(),currentGroup,group);},100);bulkBar.Children.Add(moveSelected);SyncGroups();
            var content=new DockPanel{Margin=new Thickness(0,10,0,4)};Grid.SetRow(content,3);layout.Children.Add(content);
            folderSignature="";folderBar=new WrapPanel{Margin=new Thickness(0,0,0,8),Visibility=foldersVisible?Visibility.Visible:Visibility.Collapsed};DockPanel.SetDock(folderBar,Dock.Top);content.Children.Add(folderBar);
            docSignature="";docBar=new WrapPanel{Margin=new Thickness(0,0,0,8),Visibility=Visibility.Collapsed};DockPanel.SetDock(docBar,Dock.Top);content.Children.Add(docBar);
            var tileHost=new Grid();content.Children.Add(tileHost);
            Tiles=new UniformGrid{Columns=columns,Rows=rows,ClipToBounds=true,Background=Brushes.Transparent};tileHost.Children.Add(Tiles);Tiles.SizeChanged+=(s,e)=>QueueSizeRefresh();
            Empty=new TextBlock{Text="将软件、文档或文件夹拖到这里\n面板默认保持打开；按 Esc 收起，也可点击「批量添加」",TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,Foreground=MutedBrush,FontSize=19,LineHeight=32};tileHost.Children.Add(Empty);
            var footer=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};Grid.SetRow(footer,4);layout.Children.Add(footer);
            footer.Children.Add(Button("‹",(s,e)=>ChangePage(-1),40));Pager=new TextBlock{Width=100,TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Foreground=TextBrush,FontSize=14};footer.Children.Add(Pager);footer.Children.Add(Button("›",(s,e)=>ChangePage(1),40));
            var bottom=new Grid();bottom.ColumnDefinitions.Add(new ColumnDefinition());bottom.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(230)});Grid.SetRow(bottom,5);layout.Children.Add(bottom);
            Status=new TextBlock{Text="图标可拖到 Dock 固定 · Esc 收起 · ··· 显示选项",Foreground=MutedBrush,FontSize=11,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center};var statusColumn=new StackPanel{VerticalAlignment=VerticalAlignment.Center};statusColumn.Children.Add(Status);bottom.Children.Add(statusColumn);
            var transparencyBox=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Right};Grid.SetColumn(transparencyBox,1);bottom.Children.Add(transparencyBox);
            var amount=new TextBlock{Text="透明度 "+Math.Round(100-GlassPercent)+"%",Foreground=MutedBrush,Width=83,VerticalAlignment=VerticalAlignment.Center};transparencyBox.Children.Add(amount);
            var transparency=new Slider{Minimum=0,Maximum=100,Value=100-GlassPercent,Width=140,TickFrequency=5,IsSnapToTickEnabled=true,ToolTip="只改变玻璃背景，图标和文字保持清晰"};transparencyBox.Children.Add(transparency);
            transparency.ValueChanged+=(s,e)=>{C.Opacity(100-transparency.Value);amount.Text="透明度 "+Math.Round(transparency.Value)+"%";C.Save();};
            Render(true);
        }
        public void SyncGroups(){if(addExisting!=null)addExisting.IsEnabled=currentGroup!="全部";if(groups==null)return;syncingGroups=true;var names=new string[]{"全部","未分组"}.Concat(C.Prefs.Groups).ToList();groups.ItemsSource=names;if(!names.Contains(currentGroup)){currentGroup="全部";selected.Clear();page=0;}groups.SelectedItem=currentGroup;if(moveGroup!=null){string name=moveGroup.SelectedItem as string;moveGroup.ItemsSource=names.Skip(1).ToList();moveGroup.SelectedItem=names.Contains(name)?name:"未分组";}syncingGroups=false;if(addExisting!=null)addExisting.IsEnabled=currentGroup!="全部";}
        List<Entry> Filtered() {string q=C.Prefs.BoardHideSearch||Search==null?"":Search.Text.Trim();var list=C.Prefs.Apps.Where(a=>(C.Prefs.BoardHideGroups||currentGroup=="全部"||FeatureRules.InGroup(a,currentGroup))&&LayoutRules.Matches(a.Name,q)).ToList();if(!C.Prefs.BoardHideGroups&&docFilter!="全部"&&string.Equals(currentGroup,C.Prefs.DesktopDocsGroup,StringComparison.OrdinalIgnoreCase))list=list.Where(a=>DesktopAutoRules.DocKind(a.Path)==docFilter).ToList();return list;}
        public void SetStatus(string text) {pendingStatus=text;if(Status!=null)Status.Text=text;}
        void SetSelectionMode(bool enabled) {editing=enabled;selected.Clear();dragEntry=null;if(editSelection!=null)editSelection.Content=enabled?"完成":"多选";}
        public void FinishSelection() {SetSelectionMode(false);}
        void SelectFiltered() {foreach(var a in Filtered())selected.Add(a.Path);Render();}
        void UpdateSelectionBar() {
            selected.RemoveWhere(p=>!C.Prefs.Apps.Any(a=>string.Equals(a.Path,p,StringComparison.OrdinalIgnoreCase)));
            bool visible=editing;bulkBar.Visibility=visible?Visibility.Visible:Visibility.Collapsed;bulkRow.Height=visible?GridLength.Auto:new GridLength(0);moveGroup.IsEnabled=editing;moveSelected.Content=currentGroup=="全部"?"添加到分组":"移动";moveSelected.IsEnabled=editing&&selected.Count>0;
            selectAll.IsEnabled=editing;clearSelection.IsEnabled=editing&&selected.Count>0;deleteSelected.IsEnabled=editing&&selected.Count>0;
            deleteSelected.Content="移除已选（"+selected.Count+"）";undoRemoval.IsEnabled=C.CanUndoRemoval;undoRemoval.ToolTip="撤销："+C.UndoLabel+" · Ctrl+Z";undoRemoval.Visibility=C.CanUndoRemoval?Visibility.Visible:Visibility.Collapsed;selectionCount.Text=editing?"已选 "+selected.Count+" · 跨页保留":"可撤销最近操作";
        }
        public void Render(bool animate=false) {
            if(Tiles==null||(!IsVisible&&!preparingView)){viewRenderDirty=true;return;}viewRenderDirty=false;viewDataKey=ViewDataKey();UpdateSelectionBar();RefreshFolders();RefreshDocBar();Tiles.Children.Clear();var entries=Filtered();int total=LayoutRules.Pages(entries.Count,capacity);page=Math.Min(page,total-1);Pager.Text=(page+1)+" / "+total;Empty.Visibility=entries.Count==0?Visibility.Visible:Visibility.Collapsed;if(pendingStatus!=null)Status.Text=pendingStatus;
            int index=0;foreach(var a in entries.Skip(page*capacity).Take(capacity)) Tiles.Children.Add(Tile(a,index++,animate));
        }
        void ChangePage(int delta) {var entries=Filtered();int total=LayoutRules.Pages(entries.Count,capacity);int next=Math.Max(0,Math.Min(total-1,page+delta));if(next==page)return;page=next;Render();AnimatePage(delta);}
        FrameworkElement Tile(Entry a,int index,bool animate) {
            bool isSelected=editing&&selected.Contains(a.Path);
            var selectedBrush=new SolidColorBrush(Color.FromArgb(48,111,167,255));
            var tile=new Border{Tag=a,Background=isSelected?selectedBrush:Brushes.Transparent,BorderThickness=new Thickness(1),BorderBrush=isSelected?new SolidColorBrush(Color.FromArgb(150,146,187,255)):Brushes.Transparent,CornerRadius=new CornerRadius(18),Margin=new Thickness(C.Prefs.BoardIconGap),Padding=new Thickness(4),Cursor=Cursors.Hand,ToolTip=a.Name,ContextMenu=editing?null:new ContextMenu(),AllowDrop=!editing};
            tile.ContextMenuOpening+=(sender,eventArgs)=>{if(editing){eventArgs.Handled=true;return;}eventArgs.Handled=true;var menu=C.Menu(a,currentGroup);tile.ContextMenu=menu;iconMenu=menu;menu.Closed+=(ss,ee)=>{if(iconMenu==menu)iconMenu=null;};menu.PlacementTarget=tile;menu.IsOpen=true;};
            var container=new Grid();tile.Child=container;var box=new StackPanel{VerticalAlignment=VerticalAlignment.Center};container.Children.Add(box);
            var icon=FeatureUi.Icon(a,C.Prefs.Dock.ShowShortcutOverlays,C.Prefs.Dock.SharperIcons);
            if(icon!=null)box.Children.Add(new Image{Source=icon,Width=C.Prefs.BoardIconSize,Height=C.Prefs.BoardIconSize,Margin=new Thickness(0,0,0,8)});
            else box.Children.Add(new TextBlock{Text="▣",Foreground=TextBrush,FontSize=C.Prefs.BoardIconSize*.84,Height=C.Prefs.BoardIconSize,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,8)});
            box.Children.Add(new TextBlock{Text=a.Name,Foreground=TextBrush,FontSize=12,TextAlignment=TextAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=Math.Max(20,(Tiles.ActualWidth>0?Tiles.ActualWidth:Width-104)/columns-2*C.Prefs.BoardIconGap-10)});
            container.Children.Add(new Border{Tag="running",Width=5,Height=5,CornerRadius=new CornerRadius(3),Background=new SolidColorBrush(Color.FromRgb(135,205,255)),VerticalAlignment=VerticalAlignment.Bottom,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,1),ToolTip="正在运行，点击切回窗口",Visibility=Visibility.Collapsed});
            if(editing) {
                var check=new CheckBox{IsChecked=isSelected,IsHitTestVisible=false,Focusable=false,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(2)};container.Children.Add(check);
            }
            var scale=new ScaleTransform(1,1);var lift=new TranslateTransform();var transforms=new TransformGroup();transforms.Children.Add(scale);transforms.Children.Add(lift);var iconVisual=box.Children[0] as FrameworkElement;iconVisual.RenderTransform=transforms;iconVisual.RenderTransformOrigin=new Point(.5,.5);
            Action<bool> hover=over=>{tile.Background=isSelected?selectedBrush:over?new SolidColorBrush(Color.FromArgb(24,255,255,255)):Brushes.Transparent;if(!C.Prefs.Dock.HoverMotion)return;var ease=new CubicEase{EasingMode=EasingMode.EaseOut};
                scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(over&&InteractionRules.ZoomHover(C.Prefs.Dock.HoverAnimationStyle)?1+.05*MotionLevel:1,TimeSpan.FromMilliseconds(170)){EasingFunction=ease});scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(over&&InteractionRules.ZoomHover(C.Prefs.Dock.HoverAnimationStyle)?1+.05*MotionLevel:1,TimeSpan.FromMilliseconds(170)){EasingFunction=ease});lift.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(over&&InteractionRules.LiftHover(C.Prefs.Dock.HoverAnimationStyle)?-3*MotionLevel:0,TimeSpan.FromMilliseconds(170)){EasingFunction=ease});};
            tile.MouseEnter+=(s,e)=>hover(true);tile.MouseLeave+=(s,e)=>hover(false);
            tile.MouseLeftButtonDown+=(s,e)=>{dragStart=e.GetPosition(this);dragEntry=a;tile.CaptureMouse();};
            tile.MouseMove+=(s,e)=> {if(e.LeftButton!=MouseButtonState.Pressed||dragEntry!=a)return;var p=e.GetPosition(this);if(Math.Abs(p.X-dragStart.X)+Math.Abs(p.Y-dragStart.Y)>10) {dragEntry=null;tile.ReleaseMouseCapture();StartEntryDrag(tile,a);}};
            tile.MouseLeftButtonUp+=(s,e)=> {tile.ReleaseMouseCapture();if(dragEntry==a){dragEntry=null;if(editing){if(!selected.Remove(a.Path))selected.Add(a.Path);Render();e.Handled=true;}else C.Launch(a);}};
            tile.Drop+=(s,e)=> {
                if(!e.Data.GetDataPresent("GlassBoard.Entry"))return;
                string path=(string)e.Data.GetData("GlassBoard.Entry");C.ReorderEntry(path,a);e.Handled=true;
            };return tile;
        }
        string[] DraggedEntries(IDataObject data){if(data.GetDataPresent("GlassBoard.Entries"))return (string[])data.GetData("GlassBoard.Entries");if(data.GetDataPresent("GlassBoard.Entry"))return new[]{(string)data.GetData("GlassBoard.Entry")};return new string[0];}
        void OpenGroup(string name){FinishSelection();currentGroup=name;docFilter="全部";page=0;ClearSearch();SyncGroups();Render(true);}
        void RefreshFolders(){if(folderBar==null||groupDragRunning)return;if(activeDrag&&folderBar.Children.Count>0){if(folderBar.Children.OfType<Border>().Select(b=>b.Tag as string).SequenceEqual(new[]{"全部","未分组"}.Concat(C.Prefs.Groups)))return;EndDragPreview();}string signature=currentGroup+"/"+LightTheme+"/"+string.Join("|",new[]{"全部","未分组"}.Concat(C.Prefs.Groups).Select(g=>g+":"+C.Prefs.Apps.Count(a=>FeatureRules.InGroup(a,g))));if(folderSignature==signature&&folderBar.Children.Count>0)return;folderSignature=signature;folderBar.Children.Clear();foreach(string item in new[]{"全部","未分组"}.Concat(C.Prefs.Groups)){string name=item;int count=name=="全部"?C.Prefs.Apps.Count:C.Prefs.Apps.Count(a=>FeatureRules.InGroup(a,name));var normal=name==currentGroup?new SolidColorBrush(Color.FromArgb(55,130,180,255)):new SolidColorBrush(Color.FromArgb(22,255,255,255));var box=new Border{Tag=name,CornerRadius=new CornerRadius(12),Background=normal,Padding=new Thickness(10,6,10,6),Margin=new Thickness(0,0,6,6),Cursor=Cursors.Hand,AllowDrop=true,ToolTip=name=="全部"?"查看全部入口":"点击打开；将一个或多个入口拖到这里分组"};var label=new StackPanel{Orientation=Orientation.Horizontal};var folderIcon=Polish.Icon(name=="全部"?"grid":"folder",TextBrush,18);folderIcon.Margin=new Thickness(0,0,6,0);label.Children.Add(folderIcon);label.Children.Add(new TextBlock{Text=name+"  "+count,Foreground=TextBrush,FontSize=12,VerticalAlignment=VerticalAlignment.Center});box.Child=label;AttachGroupReorder(box,name);box.DragEnter+=(sender,e)=>{if(DraggedEntries(e.Data).Length>0||e.Data.GetDataPresent(DataFormats.FileDrop)){BeginGroupHover(name);box.Background=new SolidColorBrush(Color.FromArgb(90,100,160,240));e.Effects=GroupDragEffect(e.Data,name);e.Handled=true;}};box.DragOver+=(sender,e)=>{if(DraggedEntries(e.Data).Length>0||e.Data.GetDataPresent(DataFormats.FileDrop))BeginGroupHover(name);e.Effects=GroupDragEffect(e.Data,name);e.Handled=true;};box.DragLeave+=(sender,e)=>{StopGroupHover(name);box.Background=normal;};box.Drop+=(sender,e)=>{e.Handled=true;if(name=="全部")return;var paths=DraggedEntries(e.Data);if(paths.Length>0){string from=e.Data.GetDataPresent("GlassBoard.SourceGroup")?(string)e.Data.GetData("GlassBoard.SourceGroup"):currentGroup;C.TransferEntries(paths,from,name);}else if(e.Data.GetDataPresent(DataFormats.FileDrop)){C.Add((string[])e.Data.GetData(DataFormats.FileDrop),true,name);OpenGroup(name);SetStatus("已添加到「"+name+"」");}};folderBar.Children.Add(box);}}
        void RefreshDocBar(){
            if(docBar==null)return;
            bool visible=!C.Prefs.BoardHideGroups&&string.Equals(currentGroup,C.Prefs.DesktopDocsGroup,StringComparison.OrdinalIgnoreCase);
            docBar.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
            if(!visible)return;
            if(docFilter!="全部"&&Array.IndexOf(DesktopAutoRules.DocKinds,docFilter)<0)docFilter="全部";
            string signature=currentGroup+"/"+docFilter;
            foreach(string probe in new[]{"全部"}.Concat(DesktopAutoRules.DocKinds))signature+="|"+probe+":"+DocCount(probe);
            if(docSignature==signature&&docBar.Children.Count>0)return;
            docSignature=signature;docBar.Children.Clear();
            foreach(string kind in new[]{"全部"}.Concat(DesktopAutoRules.DocKinds)){
                string name=kind;int count=DocCount(name);bool active=docFilter==name;
                var box=new Border{Tag=name,CornerRadius=new CornerRadius(12),Background=active?new SolidColorBrush(Color.FromArgb(55,130,180,255)):new SolidColorBrush(Color.FromArgb(22,255,255,255)),Padding=new Thickness(10,6,10,6),Margin=new Thickness(0,0,6,6),Cursor=Cursors.Hand,ToolTip=name=="全部"?"显示该分组的全部文档":"只看"+name+"类型的文档"};
                box.Child=new TextBlock{Text=name+"  "+count,Foreground=TextBrush,FontSize=12,VerticalAlignment=VerticalAlignment.Center};
                box.MouseLeftButtonUp+=(s,e)=>{docFilter=name;page=0;Render(true);e.Handled=true;};
                docBar.Children.Add(box);
            }
        }
        int DocCount(string kind){string group=C.Prefs.DesktopDocsGroup;return C.Prefs.Apps.Count(a=>FeatureRules.InGroup(a,group)&&(kind=="全部"||DesktopAutoRules.DocKind(a.Path)==kind));}
        void ExternalDrop(object s,DragEventArgs e) {if(DraggedEntries(e.Data).Length>0){e.Handled=true;e.Effects=DragDropEffects.None;return;}if(e.Data.GetDataPresent(DataFormats.FileDrop)) {C.Add((string[])e.Data.GetData(DataFormats.FileDrop),true,currentGroup);ClearSearch();e.Handled=true;}}
        public void UpdateRunning(){if(Tiles==null||!IsVisible)return;foreach(var tile in Tiles.Children.OfType<Border>()){var entry=tile.Tag as Entry;var grid=tile.Child as Grid;if(entry==null||grid==null)continue;foreach(var marker in grid.Children.OfType<Border>().Where(b=>b.Tag as string=="running"))marker.Visibility=C.Prefs.Dock.ShowRunning&&RunningWindows.IsRunning(entry)?Visibility.Visible:Visibility.Collapsed;}}
    }
    public static class Program {
        [STAThread] public static int Main(string[] args) {
            if(args.Length>0 && args[0]=="--open-native-tray")return NativeTrayLauncher.Run();
            if(args.Length>0 && args[0]=="--netease-ui-bridge")return NeteaseUiReader.Run();
            if(args.Length>0 && args[0]=="--self-test") return SelfTest.Run();
            if(args.Length>1 && args[0]=="--toggle-check") return ToggleCheck(args);
            if(args.Length>0 && args[0]=="--restore-shell") {
                try {if(Native.RequestRestore())return 0;try{TaskbarAutoHide.ShowAndClear();}catch(Exception ex){Store.Log(ex);}DesktopVisibility.ShowAll();MessageBox.Show("已执行任务栏和桌面图标恢复。","GlassBoard");return 0;}catch(Exception ex){MessageBox.Show(ex.Message,"GlassBoard");return 1;}
            }
            bool created;using(var mutex=new Mutex(true,"Local\\GlassBoard.Desktop.v1",out created)) {
                if(!created){MessageBox.Show("已有 GlassBoard 在运行，本次启动的 v0.2.0 尚未运行。\n请先从托盘退出已有程序，再启动此版本。","GlassBoard v0.2.0");return 0;}
                try {
                    Store.Trace("START exe="+System.Reflection.Assembly.GetExecutingAssembly().Location+" OS="+Environment.OSVersion+" CLR="+Environment.Version);
                    try{DesktopVisibility.Recover();}catch(Exception ex){Store.Log(ex);}
                    try{TaskbarAutoHide.Restore();}catch(Exception ex){Store.Log(ex);}
                    var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
                    app.Exit+=(s,e)=>{try{DesktopVisibility.RestoreOriginal();}catch(Exception ex){Store.Log(ex);}try{TaskbarAutoHide.Restore();}catch(Exception ex){Store.Log(ex);}};
                    Controller controller=null;
                    app.DispatcherUnhandledException+=(s,e)=>{Store.Log(e.Exception);if(controller!=null)try{Store.Save(controller.Prefs);}catch(Exception saveError){Store.Log(saveError);}MessageBox.Show("操作失败，详情已记录在配置文件夹 error.log。\n"+e.Exception.Message,"GlassBoard");e.Handled=true;};
                    controller=new Controller();app.SessionEnding+=(s,e)=>controller.SaveSession();if(args.Any(a=>a=="--recover-widgets"))controller.RecoverWidgets();app.Run();GC.KeepAlive(controller);return 0;
                } catch(Exception ex){Store.Log(ex);MessageBox.Show("启动失败："+ex.Message,"GlassBoard");return 1;}
            }
        }
        static int ToggleCheck(string[] args){
            var lines=new List<string>();
            try {
                string target=args[1];bool act=args.Any(a=>a=="--act");bool minimize=args.Any(a=>a=="--minimize");bool restore=args.Any(a=>a=="--restore");
                var entry=new Entry{Name=EntryPathRules.Name(target),Path=EntryPathRules.Normalize(target),IsFolder=Directory.Exists(target)};
                LaunchInfoService.Enrich(entry);
                RunningWindows.Scan();
                var matches=RunningWindows.For(entry);
                lines.Add("target="+target);
                lines.Add("launchTarget="+entry.LaunchTarget);
                lines.Add("executables="+string.Join(";",WindowActivationRules.Executables(entry).ToArray()));
                lines.Add("foreground="+RunningWindows.Foreground());
                lines.Add("matches="+matches.Count);
                foreach(var window in matches)lines.Add("  handle="+window.Handle+" pid="+window.ProcessId+" iconic="+RunningWindows.Minimized(window.Handle)+" path="+window.Path+" title="+window.Title);
                if(minimize&&matches.Count>0){RunningWindows.MinimizeForCheck(matches[0].Handle);lines.Add("minimize iconic="+RunningWindows.Minimized(matches[0].Handle));}
                if(restore&&matches.Count>0){RunningWindows.RestoreForCheck(matches[0].Handle);lines.Add("restore iconic="+RunningWindows.Minimized(matches[0].Handle));}
                if(act){IntPtr hint=IntPtr.Zero;int at=Array.IndexOf(args,"--hint");if(at>=0&&at+1<args.Length){long value;if(long.TryParse(args[at+1],out value))hint=new IntPtr(value);}bool ok=RunningWindows.Toggle(entry,hint);lines.Add("hint="+hint);lines.Add("toggle="+ok);lines.Add("toggle foreground="+RunningWindows.Foreground());foreach(var window in matches)lines.Add("  after iconic="+window.Handle+" "+RunningWindows.Minimized(window.Handle));}
            } catch(Exception ex){lines.Add("error="+ex);}
            try{File.WriteAllLines(Path.Combine(Store.Folder,"toggle-check.txt"),lines);}catch{}
            return 0;
        }
    }
    public static class SelfTest {
        sealed class ButtonTestWindow : GlassWindow {
            public ButtonTestWindow() : base(12) {}
            void RequireCaptureAllowed(){uint affinity;var handle=new WindowInteropHelper(this).EnsureHandle();if(!Native.GetWindowDisplayAffinity(handle,out affinity))throw new Win32Exception(Marshal.GetLastWin32Error(),"GetWindowDisplayAffinity self-test");if(affinity!=0)throw new Exception("玻璃窗口仍被标记为限制屏幕捕获："+affinity);}
            public void CheckCaptureCompatibility(){Width=240;Height=180;Left=SystemParameters.WorkArea.Left+20;Top=SystemParameters.WorkArea.Top+20;RequireCaptureAllowed();Backdrop(false);RequireCaptureAllowed();Show();Backdrop(true);RequireCaptureAllowed();Hide();RequireCaptureAllowed();}
            public void CheckHandlers() {
                var deferred=Button("编辑",null,60);bool deferredCalled=false;
                deferred.ApplyTemplate();
                deferred.Click+=(s,e)=>deferredCalled=true;
                deferred.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                bool inlineCalled=false;var inline=Button("添加",(s,e)=>inlineCalled=true);
                inline.ApplyTemplate();
                inline.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                if(!deferredCalled||!inlineCalled)throw new Exception("按钮事件绑定失败");
                foreach(string icon in new string[]{"play","pause","next","previous","music","shuffle","repeat","sun"}){var control=Button("",null,52);control.Content=Polish.Icon(icon,Polish.Ink(true),22);control.ApplyTemplate();control.Measure(new Size(52,52));control.Arrange(new Rect(0,0,52,52));if(control.DesiredSize.Width<=0)throw new Exception("组件图标布局失败："+icon);}
                var toggle=Controller.VisibilitySwitch();toggle.ApplyTemplate();var thumb=(Border)toggle.Template.FindName("Thumb",toggle);
                toggle.IsChecked=true;if(thumb.HorizontalAlignment!=HorizontalAlignment.Right)throw new Exception("开关开启状态失败");
                toggle.IsChecked=false;if(thumb.HorizontalAlignment!=HorizontalAlignment.Left)throw new Exception("开关关闭状态失败");int clicks=0;toggle.Click+=(s,e)=>clicks++;toggle.IsChecked=true;toggle.IsChecked=false;if(clicks!=0)throw new Exception("状态同步触发了用户点击");toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));if(clicks!=1)throw new Exception("组件点击命令没有执行");
            }
        }
        public static int Run() {
            try {
                CheckMemoryResources();
                MenuAppearanceChecks.Run();
                DockBehaviorChecks.Run();
                FeedbackChecks.Run();
                Feedback18Checks.Run();
                PersonalizationChecks.Run();
                var buttons=new ButtonTestWindow();try{buttons.CheckHandlers();buttons.CheckCaptureCompatibility();}finally{buttons.Close();}
                foreach(bool light in new bool[]{false,true}){var styled=new Window{Width=440,Height=350};try{Polish.Apply(styled,light);var panel=new StackPanel();styled.Content=panel;var button=new Button{Content="样式"};var combo=new ComboBox{ItemsSource=new string[]{"A","B"},SelectedIndex=0};var text=new TextBox{Text=new string('长',120),FontSize=17,TextWrapping=TextWrapping.Wrap,MinHeight=48,Padding=new Thickness(12,9,12,9)};var check=new CheckBox{Content="显示",IsChecked=true};var slider=new Slider{Minimum=0,Maximum=100,Value=25};panel.Children.Add(button);panel.Children.Add(combo);panel.Children.Add(text);panel.Children.Add(check);panel.Children.Add(slider);new WindowInteropHelper(styled).EnsureHandle();panel.Measure(new Size(400,300));panel.Arrange(new Rect(0,0,400,300));panel.UpdateLayout();foreach(var control in new Control[]{button,combo,text,check,slider})control.ApplyTemplate();foreach(double font in new[]{13.0,17.0,24.0}){var probe=new TextBox{Text="闹钟 08:30 中文测试",FontSize=font};panel.Children.Add(probe);probe.ApplyTemplate();probe.Measure(new Size(360,double.PositiveInfinity));probe.Arrange(new Rect(0,0,360,probe.DesiredSize.Height));probe.UpdateLayout();var host=(ScrollViewer)probe.Template.FindName("PART_ContentHost",probe);var line=new TextBlock{Text=probe.Text,FontFamily=probe.FontFamily,FontSize=font};line.Measure(new Size(360,double.PositiveInfinity));if(host==null||host.ViewportHeight+.5<line.DesiredSize.Height)throw new Exception("输入框文字被裁切："+font);panel.Children.Remove(probe);}var track=(Track)slider.Template.FindName("PART_Track",slider);if(track==null||track.Thumb==null)throw new Exception("新滑块模板无 Thumb");slider.Value=75;if(Math.Abs(track.Value-75)>.01)throw new Exception("新滑块值绑定失败");text.Measure(new Size(320,double.PositiveInfinity));if(text.DesiredSize.Height<=48)throw new Exception("长便签标题没有按行增高");var checkBoxPart=(Border)check.Template.FindName("Box",check);if(checkBoxPart==null)throw new Exception("勾选框缺少点击区域");foreach(bool state in new bool[]{false,true,false,true}){check.IsChecked=state;panel.UpdateLayout();var point=checkBoxPart.TranslatePoint(new Point(checkBoxPart.ActualWidth/2,checkBoxPart.ActualHeight/2),check);if(check.InputHitTest(point)==null)throw new Exception("勾选框中心不能点击，状态="+state);var templateRoot=(FrameworkElement)VisualTreeHelper.GetChild(check,0);var gap=templateRoot.TranslatePoint(new Point(23,templateRoot.ActualHeight/2),check);if(check.InputHitTest(gap)==null)throw new Exception("勾选框与文字间隙不能点击");}if(combo.Template.FindName("PART_Popup",combo)==null)throw new Exception("新下拉模板无 Popup");}finally{styled.Close();}}

                var p=new Preferences{Light=true,DockGap=25,Imported=true,OpacityPercent=40};p.Removed.Add("C:\\工具.lnk");
                p.Apps.Add(new Entry{Name="中文 软件",Path="C:\\测试目录\\工具.lnk",Pinned=true});
                using(var s=new MemoryStream()) {
                    var json=new DataContractJsonSerializer(typeof(Preferences));json.WriteObject(s,p);s.Position=0;
                    var restored=(Preferences)json.ReadObject(s);
                    if(restored.Apps.Count!=1||restored.Apps[0].Name!="中文 软件"||!restored.Apps[0].Pinned||restored.DockGap!=25||restored.OpacityPercent!=40||restored.Removed.Count!=1)throw new Exception("配置往返失败");
                }
                if(LayoutRules.Pages(0,35)!=1||LayoutRules.Pages(35,35)!=1||LayoutRules.Pages(36,35)!=2||LayoutRules.Pages(70,35)!=2)throw new Exception("分页边界失败");
                if(!LayoutRules.Matches("Microsoft Visual Studio Code","code MICROSOFT")||!LayoutRules.Matches("剪映专业版","剪映")||LayoutRules.Matches("Steam","maya"))throw new Exception("搜索失败");
                if(LayoutRules.GlassOpacity(-1)!=0||LayoutRules.GlassOpacity(100)!=100||LayoutRules.GlassOpacity(double.NaN)!=72)throw new Exception("透明度边界失败");
                foreach(string key in new[]{"Wechat","Alipay"})using(var stream=new MemoryStream(CreatorAssets.Read(key),false)){var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();if(bitmap.PixelWidth<200||bitmap.PixelHeight<200)throw new Exception("收款图片尺寸异常");bitmap.Freeze();}
                File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test-result.txt"),"PASS: transparent material bounds and widget recovery menu; Dock rendering/order/mouse-capture release and native process path/reuse; light/dark nested menus, submenu open/close, checkmarks, disabled items, header sliders, shortcut overlay cache and legacy configuration; shared theme styles, bounded thumbnails and stream-independent pixels, hidden board release/reopen; recordable glass window affinity before/after backdrop/show/hide; embedded creator image decoding, WPF light/dark button/combo/text/check/slider templates, value bindings, button handlers, visibility switch, configuration, removed entries, opacity bounds, pages and search\r\n",Encoding.UTF8);return 0;
            }catch(Exception ex){File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test-result.txt"),ex.ToString());return 1;}
        }
        static void CheckMemoryResources(){
            var one=new Window();var two=new Window();try{foreach(bool light in new[]{false,true}){Polish.Apply(one,light);Polish.Apply(two,light);foreach(var type in new[]{typeof(Button),typeof(TextBox),typeof(ComboBox),typeof(Slider),typeof(CheckBox)}){var style=(Style)one.Resources[type];if(!style.IsSealed||!object.ReferenceEquals(style,two.Resources[type]))throw new Exception("主题样式未共享："+type.Name);}Polish.Apply(one,light);}}finally{one.Close();two.Close();}
            foreach(var dims in new[]{new[]{512,4096},new[]{4096,512},new[]{64,64}}){byte[] pixels=new byte[dims[0]*dims[1]];var source=BitmapSource.Create(dims[0],dims[1],96,96,PixelFormats.Gray8,null,pixels,dims[0]);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));using(var stream=new MemoryStream()){encoder.Save(stream);var image=ThumbnailImages.Read(stream.ToArray(),600);if(!image.IsFrozen||image.PixelWidth>600||image.PixelHeight>600||image.PixelWidth>dims[0]||image.PixelHeight>dims[1])throw new Exception("缩略图尺寸或冻结状态无效");int stride=(image.PixelWidth*image.Format.BitsPerPixel+7)/8;image.CopyPixels(new byte[stride*image.PixelHeight],stride,0);}}
            var controller=new Controller(false);try{controller.Prefs.Groups.Add("内存检查分组");controller.Board.Open();if(!controller.Board.HasView||!controller.Board.IsVisible)throw new Exception("面板未创建");controller.Board.Hide();controller.Board.ModalBusy=true;if(controller.Board.ReleaseHiddenView())throw new Exception("忙碌时清理了面板");controller.Board.ModalBusy=false;if(!controller.Board.ReleaseHiddenView()||controller.Board.HasView)throw new Exception("隐藏面板资源未释放");controller.Board.Render();controller.Board.Open();if(!controller.Board.HasView||!controller.Board.IsVisible||!controller.Prefs.Groups.Contains("内存检查分组"))throw new Exception("面板重开或配置保留失败");}finally{controller.ShuttingDown=true;controller.Dock.ReleaseHotkey();controller.Board.Close();controller.Dock.Close();}
        }
    }
}



