using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Runtime.Serialization.Json;
using Lumina;

public static class DockBehaviorTests {
    static void Check(bool value,string label){if(!value)throw new Exception(label);}
    static T Json<T>(T value){using(var stream=new MemoryStream()){var serializer=new DataContractJsonSerializer(typeof(T));serializer.WriteObject(stream,value);stream.Position=0;return (T)serializer.ReadObject(stream);}}
    static string Order(Preferences p){return string.Join(",",DockOrderRules.Ordered(p).Select(a=>a.Name));}
    public static int Main(){
        var prefs=new Preferences();for(int i=0;i<5;i++)prefs.Apps.Add(new Entry{Name=((char)('A'+i)).ToString(),Path="C:\\apps\\"+i+".exe",Pinned=i!=1,Group="创作"});prefs.Normalize();
        Check(Order(prefs)=="A,C,D,E","legacy pinned order");var board=prefs.Apps.Select(a=>a.Path).ToArray();var history=new BoardEditHistory();var before=BoardEditState.Capture(prefs);
        Check(DockOrderRules.Move(prefs,prefs.Apps[0].Path,prefs.Apps[3].Path,false)&&Order(prefs)=="C,D,A,E","move right after target");history.Remember(before,prefs,"Dock 顺序");
        Check(board.SequenceEqual(prefs.Apps.Select(a=>a.Path))&&prefs.Apps.All(a=>a.Group=="创作"),"Dock movement leaves board/group order intact");
        Check(Order(Json(prefs))=="C,D,A,E","order persists on restart");history.Undo(prefs);Check(Order(prefs)=="A,C,D,E","undo Dock reorder");history.Redo(prefs);Check(Order(prefs)=="C,D,A,E","redo Dock reorder");
        Check(DockOrderRules.Move(prefs,prefs.Apps[4].Path,prefs.Apps[2].Path,true)&&Order(prefs)=="E,C,D,A","move left before target");Check(!DockOrderRules.Move(prefs,prefs.Apps[4].Path,prefs.Apps[2].Path,true),"adjacent no-op");
        Check(!DockOrderRules.Move(prefs,prefs.Apps[1].Path,null,false)&&!DockOrderRules.Move(prefs,prefs.Apps[0].Path,"missing.exe",false),"reject unpinned source or stale target");Check(DockOrderRules.Move(prefs,prefs.Apps[4].Path,null,false)&&Order(prefs)=="C,D,A,E","drop at end/overflow");
        Check(DockOrderRules.Step(prefs,prefs.Apps[0].Path,-1)&&Order(prefs)=="C,A,D,E","keyboard/menu step follows Dock order");
        prefs.DockOrder.Insert(0,"missing.exe");prefs.DockOrder.Add(prefs.DockOrder[1].ToUpperInvariant());prefs.Normalize();Check(Order(prefs)=="C,A,D,E"&&prefs.DockOrder.Count==4,"normalize stale/duplicate/case paths");
        prefs.Apps[2].Pinned=false;prefs.Normalize();Check(Order(prefs)=="A,D,E","unpin removes order slot");prefs.Apps[1].Pinned=true;prefs.Normalize();Check(Order(prefs)=="A,D,E,B","new pin appended");
        var scheme=Json(FeatureRules.Capture(prefs,"Dock"));var restored=new Preferences();FeatureRules.Apply(restored,scheme);Check(Order(restored)==Order(prefs),"appearance scheme retains Dock order");
        var legacy=new DataContractJsonSerializer(typeof(Preferences));using(var stream=new MemoryStream(Encoding.UTF8.GetBytes("{\"Apps\":[{\"Name\":\"旧软件\",\"Path\":\"old.exe\",\"Pinned\":true}]}"))){var old=(Preferences)legacy.ReadObject(stream);old.Normalize();Check(Order(old)=="旧软件","old JSON without DockOrder upgrades");}
        var direct=new Entry{Path=@"C:\Apps\Editor.exe"};Check(WindowActivationRules.Executables(direct).Single()==direct.Path,"exe identity");
        var link=new Entry{Path=@"C:\Desktop\编辑器.lnk",LaunchTarget=@"C:\Apps\Editor.exe"};Check(WindowActivationRules.Executables(link).Single()==link.LaunchTarget,"resolved shortcut identity");
        link.WindowTarget=@"C:\Games\Game.exe";Check(WindowActivationRules.Executables(link).Single()==link.WindowTarget,"explicit association overrides launcher identity");Check(Json(link).WindowTarget==link.WindowTarget&&FeatureRules.Copy(link).WindowTarget==link.WindowTarget,"association persists and copies");
        Check(WindowActivationRules.Executables(new Entry{Path="steam.url",LaunchTarget="steam://rungameid/123"}).Length==0,"protocol launchers do not match unrelated windows");Check(WindowActivationRules.Executables(new Entry{Path="folder.exe",IsFolder=true}).Length==0,"folder not treated as process");
        Check(WindowActivationRules.NormalizePath(@"\\?\C:\Apps\Editor.exe")==direct.Path,"extended path identity");
        Check(WindowActivationRules.Select(new long[]{20,10,20},10,0,false)==20,"cycle next existing window");Check(WindowActivationRules.Select(new long[]{20,10},20,0,false)==10,"cycle wraps");Check(WindowActivationRules.Select(new long[]{20,10},99,20,false)==20,"return to last window from another application");Check(WindowActivationRules.Select(new long[]{20,10},99,20,true)==10,"Dock foreground still allows repeated cycling");Check(WindowActivationRules.Select(new long[]{20},0,0,false)==20,"single existing window is reused");Check(WindowActivationRules.Select(new long[0],0,0,false)==0,"launch only when no window matches");
        var drag=new Preferences();for(int i=0;i<5;i++)drag.Apps.Add(new Entry{Name=((char)('A'+i)).ToString(),Path="icon"+i,Pinned=true});drag.Normalize();
        var hit=DockDropRules.OnIcon(drag,"icon4","icon0");Check(hit!=null&&hit.Before,"back-to-front whole icon uses preceding boundary");DockOrderRules.Move(drag,"icon4",hit.Path,hit.Before);Check(Order(drag)=="E,A,B,C,D","last icon can move directly to first");
        hit=DockDropRules.OnIcon(drag,"icon4","icon3");Check(hit!=null&&!hit.Before,"front-to-back whole icon uses following boundary");DockOrderRules.Move(drag,"icon4",hit.Path,hit.Before);Check(Order(drag)=="A,B,C,D,E","first icon can move directly to last");
        hit=DockDropRules.OnIcon(drag,"icon3","icon2");DockOrderRules.Move(drag,"icon3",hit.Path,hit.Before);Check(Order(drag)=="A,B,D,C,E","one slot backwards works across entire target icon");Check(DockDropRules.OnIcon(drag,"icon3","icon3")==null,"self drop does not reorder");
        hit=DockDropRules.OnGap("icon3",new[]{"icon0","icon1","icon3","icon2"},new[]{50.0,120.0,190.0,260.0},5);Check(hit.Path=="icon0"&&hit.Before,"leading padding inserts before first");hit=DockDropRules.OnGap("icon3",new[]{"icon0","icon1","icon3","icon2"},new[]{50.0,120.0,190.0,260.0},180);Check(hit.Path=="icon2"&&hit.Before,"gap skips dragged source instead of creating self-target");hit=DockDropRules.OnGap("hidden",new[]{"icon0","icon1"},new[]{50.0,120.0},5);Check(hit.Path=="icon0"&&hit.Before,"overflow item can move before first visible icon");
        Console.WriteLine("PASS: bidirectional whole-icon/gap/leading-padding/overflow drop resolution; Dock before/after/end/overflow reorder, no-op and stale input, board isolation, restart/undo/redo/schemes/migration; executable identity, association and window cycling.");return 0;
    }
}
