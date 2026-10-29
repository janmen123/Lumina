using System;using System.Linq;using System.IO;using System.Text;using System.Collections.Generic;using System.Runtime.Serialization.Json;using GlassBoard;
public static class DockTaskTests {
    static int assertions;
    static void Check(bool condition,string label){assertions++;if(!condition)throw new Exception(label);}
    static T Json<T>(T value){using(var s=new MemoryStream()){var json=new DataContractJsonSerializer(typeof(T));json.WriteObject(s,value);s.Position=0;return (T)json.ReadObject(s);}}
    static string Order(Preferences p){return string.Join(",",DockOrderRules.Ordered(p).Select(a=>a.Name));}
    public static int Main(){
        var wheel=new DockWheelState();Check(!wheel.Running,"starts on fixed apps");
        Check(wheel.Scroll(-120,0)&&wheel.Running,"down shows running without activating windows");
        Check(!wheel.Scroll(120,100)&&wheel.Running,"inertia is debounced");Check(wheel.Scroll(120,400)&&!wheel.Running,"up restores fixed");
        Check(!wheel.Scroll(120,800)&&!wheel.Running,"up on fixed does not toggle");
        Check(!wheel.Scroll(-40,900)&&!wheel.Scroll(-40,920)&&wheel.Scroll(-40,950)&&wheel.Running,"high resolution deltas accumulate");
        Check(!wheel.Scroll(-120,1300)&&wheel.Running,"down on running does not toggle back");
        wheel.Set(false);Check(!wheel.Scroll(0,1600)&&!wheel.Running,"empty wheel ignored");
        var prefs=new Preferences();var a=new Entry{Name="A",Path="a.lnk",LaunchTarget=@"C:\Apps\A.exe",Pinned=true,Group="创作"};var b=new Entry{Name="B",Path="b.exe",Pinned=true};var c=new Entry{Name="C",Path="c.exe",Group="办公"};prefs.Apps.AddRange(new[]{a,b,c});prefs.Normalize();
        var original=prefs.Apps.Select(x=>x.Path).ToArray();var state=BoardEditState.Capture(prefs);var result=DockPinRules.Apply(prefs,new[]{c},new DockDropTarget{Path=a.Path,Before=true});
        Check(result.Added==1&&Order(prefs)=="C,A,B","board entry inserts before first pinned");Check(original.SequenceEqual(prefs.Apps.Select(x=>x.Path))&&c.Group=="办公","pin leaves board ordering and memberships intact");
        var history=new BoardEditHistory();history.Remember(state,prefs,"拖入固定");history.Undo(prefs);Check(!prefs.Apps[2].Pinned&&Order(prefs)=="A,B","undo restores prior pins and order");history.Redo(prefs);Check(Order(prefs)=="C,A,B","redo restores pin position");
        result=DockPinRules.Apply(prefs,new[]{new Entry{Name="alias",Path="a-copy.lnk",LaunchTarget=@"C:\Apps\A.exe"}},null);
        Check(result.Added==0&&prefs.Apps.Count==3&&Order(prefs)=="C,B,A","desktop alias reuses pinned target and moves to drop position");
        var alias=new Entry{Name="board-alias",Path="board-alias.lnk",LaunchTarget=@"C:\Apps\A.exe"};prefs.Apps.Add(alias);
        result=DockPinRules.Apply(prefs,new[]{alias},null);Check(result.Added==0&&!alias.Pinned&&prefs.Apps.Count(x=>x.Pinned&&x.LaunchTarget==alias.LaunchTarget)==1,"existing unpinned board alias reuses pinned identity");prefs.Apps.Remove(alias);
        var d=new Entry{Name="D",Path="d.lnk",LaunchTarget=@"C:\Apps\D.exe"};var e=new Entry{Name="E",Path="e.exe"};
        result=DockPinRules.Apply(prefs,new[]{d,e,d},new DockDropTarget{Path="b.exe",Before=false});Check(result.Added==2&&Order(prefs)=="C,B,D,E,A","multiple desktop entries retain relative order, duplicates ignored");
        string order=Order(prefs);DockPinRules.Apply(prefs,new[]{prefs.Apps.First(x=>x.Name=="D")},new DockDropTarget{Path=d.Path,Before=false});Check(Order(prefs)==order,"self drop is stable");
        var restored=Json(prefs);restored.Normalize();Check(Order(restored)==order&&restored.Apps.Count==5,"pin/order survive configuration restart");
        var cap=new Preferences();for(int i=0;i<20;i++)cap.Apps.Add(new Entry{Name="N"+i,Path="n"+i,Pinned=true});cap.Normalize();
        result=DockPinRules.Apply(cap,new[]{new Entry{Name="extra",Path="extra"},cap.Apps[19]},new DockDropTarget{Path="n0",Before=true});Check(result.Skipped==1&&cap.Apps.Count==20&&Order(cap).StartsWith("N19,N0,"),"cap rejects new entries but allows existing reordering");
        result=DockPinRules.Apply(new Preferences(),new[]{new Entry{Name="first",Path="first"}},null);Check(result.Added==1,"empty Dock accepts first entry");
        for(int mask=0;mask<8;mask++){
            prefs.BoardHideSearch=(mask&1)!=0;prefs.BoardHideGroups=(mask&2)!=0;prefs.BoardHideFolders=(mask&4)!=0;
            var saved=Json(prefs);Check(saved.BoardHideSearch==prefs.BoardHideSearch&&saved.BoardHideGroups==prefs.BoardHideGroups&&saved.BoardHideFolders==prefs.BoardHideFolders,"independent display options persist");
            var target=new Preferences();FeatureRules.Apply(target,Json(FeatureRules.Capture(prefs,"测试")));Check(target.BoardHideSearch==prefs.BoardHideSearch&&target.BoardHideGroups==prefs.BoardHideGroups&&target.BoardHideFolders==prefs.BoardHideFolders,"display options preserved by scheme");
        }
        using(var stream=new MemoryStream(Encoding.UTF8.GetBytes("{}"))){var legacy=(Preferences)new DataContractJsonSerializer(typeof(Preferences)).ReadObject(stream);legacy.Normalize();Check(!legacy.BoardHideSearch&&!legacy.BoardHideGroups&&!legacy.BoardHideFolders,"old configuration keeps familiar visible controls");}
        Console.WriteLine("PASS: "+assertions+" production checks: wheel direction/debounce/high resolution, board/desktop pin insertion, alias deduplication, multi-entry order, cap, undo/redo/restart, display option migration and schemes.");return 0;
    }
}
