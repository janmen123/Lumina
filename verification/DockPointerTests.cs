using System;using System.IO;using System.Linq;using System.Collections.Generic;using Lumina;
public static class DockPointerTests {
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);}
    static Preferences Apps(int count){var p=new Preferences();for(int i=0;i<count;i++)p.Apps.Add(new Entry{Name="App"+i,Path="app"+i+".exe",Pinned=true,Group="创作"});p.Normalize();return p;}
    static string Order(Preferences p){return string.Join("|",DockOrderRules.Ordered(p).Select(a=>a.Path));}
    public static int Main(){try{
        // Exercise every source/target pair through press -> movement -> release ->
        // actual production reorder/save/load, rather than testing Move in isolation.
        for(int from=0;from<6;from++)for(int to=0;to<6;to++)if(from!=to){
            var p=Apps(6);var expected=p.Apps.Select(a=>a.Path).ToList();string source=expected[from],target=expected[to];expected.Remove(source);expected.Insert(to,source);
            var gesture=new DockPointerGesture(source,from*70+30,40,4,4);gesture.Move(to*70+30,40);var hit=DockDropRules.OnIcon(p,source,target);var end=gesture.Release(to*70+30,40,true,hit);
            Check(end.Dragged&&!end.Click&&end.Target!=null&&!gesture.Active,"release produces exactly one drag commit");Check(DockOrderRules.Move(p,source,end.Target.Path,end.Target.Before),"pointer commit moves source");Store.Save(p);var restored=Store.Load();Check(DockOrderRules.Ordered(restored).Select(a=>a.Path).SequenceEqual(expected),"all 30 forward/backward placements survive production persistence");Check(p.Apps.Select(a=>a.Path).SequenceEqual(Apps(6).Apps.Select(a=>a.Path))&&p.Apps.All(a=>a.Group=="创作"),"pointer reorder keeps board/group order");
            var duplicate=gesture.Release(to*70+30,40,true,hit);Check(!duplicate.Click&&duplicate.Target==null,"mouseup/timer duplicate cannot commit again");
        }
        var prefs=Apps(4);string original=Order(prefs);var click=new DockPointerGesture("app2.exe",150,40,4,4);var result=click.Release(152,41,true,DockDropRules.OnIcon(prefs,"app2.exe","app0.exe"));Check(result.Click&&!result.Dragged&&result.Target==null,"short movement remains single click, not a reorder");
        var missedMove=new DockPointerGesture("app3.exe",230,40,4,4);result=missedMove.Release(20,40,true,DockDropRules.OnIcon(prefs,"app3.exe","app0.exe"));Check(result.Dragged&&result.Target.Before&&!result.Click,"final release position works even without last move event");
        var cancelled=new DockPointerGesture("app3.exe",230,40,4,4);cancelled.Move(20,40);cancelled.Cancel();result=cancelled.Release(20,40,true,DockDropRules.OnIcon(prefs,"app3.exe","app0.exe"));Check(result.Target==null&&!result.Click&&Order(prefs)==original,"Escape/lost capture cannot commit preview order");
        var outside=new DockPointerGesture("app3.exe",230,40,4,4);outside.Move(20,40);result=outside.Release(-20,-50,false,DockDropRules.OnIcon(prefs,"app3.exe","app0.exe"));Check(result.Target==null&&!result.Click,"release outside Dock cancels instead of launching or moving");
        var self=new DockPointerGesture("app3.exe",230,40,4,4);self.Move(210,40);result=self.Release(230,40,true,DockDropRules.OnIcon(prefs,"app3.exe","app3.exe"));Check(result.Target==null&&!result.Click,"dragging back to source does not accidentally launch");
        var overflow=new DockPointerGesture("app3.exe",300,-90,4,4);overflow.Move(15,40);result=overflow.Release(15,40,true,DockDropRules.OnGap("app3.exe",new[]{"app0.exe","app1.exe"},new[]{40.0,110.0},15));Check(result.Target.Path=="app0.exe"&&result.Target.Before,"overflow press outside Dock can release into first slot");
        var popupClick=new DockPointerGesture("app3.exe",300,-90,4,4);result=popupClick.Release(300,-90,false,null,true);Check(result.Click,"closed overflow popup short click still launches once");
        var invalid=new DockPointerGesture("app3.exe",230,40,4,4);invalid.Move(20,40);result=invalid.Release(double.NaN,40,true,DockDropRules.OnIcon(prefs,"app3.exe","app0.exe"));Check(result.Target==null&&!result.Click,"invalid release coordinates are rejected");
        Console.WriteLine("PASS: 30 bidirectional press/move/release/save/restart placements, board isolation, duplicate release, short click, missing move, cancel/lost capture, outside/self drop, overflow and invalid coordinates.");return 0;
    }finally{if(Directory.Exists(Store.Folder))Directory.Delete(Store.Folder,true);}}
}
