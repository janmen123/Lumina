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


namespace Lumina {
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
        [DataMember] public bool ThemeFollowSystem = false;
        [DataMember] public bool BoardSortRecent = false;
        [DataMember(EmitDefaultValue=false)] public List<string> RecentUse = new List<string>();
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
        [DataMember] public int KeepWindowSeconds=3;
        [DataMember] public int ConfigRevision;
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
        [DataMember] public bool AutoHide=false,ShowRunning=true,ShowRunningSection=true;
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
            ShowSettings=true;ItemOrder=new List<string>();RunningOrder=new List<string>();AutoHide=false;ShowRunning=true;ShowRunningSection=true;SpringReveal=true;ShowShortcutOverlays=false;SharperIcons=false;HideDelay=900;EdgeRange=12;BlurRadius=18;EdgeStrength=1;Reflection=false;DragMotion=true;ReflectionOpacity=45;ReflectionLength=38;ReflectionAngle=0;ReflectionBlur=1.5;ReflectionGap=2;ReflectionFade=85;
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
}
