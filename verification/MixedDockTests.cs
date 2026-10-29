using System;using System.IO;using System.Linq;using System.Text;using System.Collections.Generic;using System.Runtime.Serialization.Json;using GlassBoard;
class MixedDockTests {
    static int count;static void Check(bool test,string name){count++;if(!test)throw new Exception(name);}
    static T RoundTrip<T>(T item){using(var s=new MemoryStream()){var j=new DataContractJsonSerializer(typeof(T));j.WriteObject(s,item);s.Position=0;return (T)j.ReadObject(s);}}
    static string Keys(Preferences p){return string.Join("|",DockLayoutRules.Common(p));}
    static Preferences Sample(){var p=new Preferences();for(int i=0;i<4;i++)p.Apps.Add(new Entry{Name="App"+i,Path="app"+i,Pinned=true});p.Dock.ShowPower=p.Dock.ShowNetwork=true;p.Normalize();return p;}
    static int Main(){
        var p=Sample();Check(DockLayoutRules.Common(p).Count==10&&p.Dock.ShowSettings,"legacy default positions include system and apps");
        foreach(string system in DockLayoutRules.SystemKeys){
            p=Sample();DockLayoutRules.Move(p,system,"app3",false);var order=DockLayoutRules.Common(p);Check(order.IndexOf(system)>order.IndexOf("app3"),"system moved right");
            Check(DockLayoutRules.Move(p,system,"app0",true),"system before first app "+system);order=DockLayoutRules.Common(p);Check(order.IndexOf(system)<order.IndexOf("app0"),"system moved left");
            var saved=RoundTrip(p);saved.Normalize();Check(Keys(p)==Keys(saved),"mixed order survives restart");
        }
        var boardOrder=string.Join("|",p.Apps.Select(a=>a.Path));DockLayoutRules.Move(p,"app3",DockLayoutRules.Computer,true);p.Normalize();Check(DockOrderRules.Ordered(p).First().Path=="app3","mixed app reorder syncs app order");Check(string.Join("|",p.Apps.Select(a=>a.Path))==boardOrder,"mixed ordering leaves board app list intact");
        string before=Keys(p);p.Dock.ShowSettings=false;p.Dock.ShowComputer=false;p.Normalize();Check(!DockLayoutRules.Common(p,true).Contains(DockLayoutRules.Settings)&&!DockLayoutRules.Common(p,true).Contains(DockLayoutRules.Computer),"settings and computer can hide");Check(Keys(p)==before,"hide retains saved positions");p.Dock.ShowSettings=p.Dock.ShowComputer=true;Check(Keys(p)==before,"reenable restores positions");
        var copy=p.Dock.Copy();copy.ItemOrder.Clear();copy.RunningOrder.Add("running:test");Check(p.Dock.ItemOrder.Count==10&&p.Dock.RunningOrder.Count==0,"Dock copies own independent lists");
        var scheme=RoundTrip(FeatureRules.Capture(p,"mixed"));var target=new Preferences();FeatureRules.Apply(target,scheme);Check(Keys(target)==Keys(p),"appearance scheme includes mixed positions");
        var history=new BoardEditHistory();var state=BoardEditState.Capture(p);DockLayoutRules.Move(p,DockLayoutRules.Settings,"app3",true);history.Remember(state,p,"mixed");string after=Keys(p);history.Undo(p);Check(Keys(p)==before,"undo restores system position");history.Redo(p);Check(Keys(p)==after,"redo restores system position");
        var taskKeys=new[]{"running:A","running:B","running:C"};var common=Keys(p);Check(DockLayoutRules.Move(p,DockLayoutRules.Power,"running:A",true,taskKeys),"running page system ordering");Check(Keys(p)==common,"running page keeps common layout independent");
        var running=DockLayoutRules.Running(p,taskKeys);Check(running.IndexOf(DockLayoutRules.Power)<running.IndexOf("running:A"),"running move saved");var changed=DockLayoutRules.Running(p,new[]{"running:A","running:C","running:D"});Check(!changed.Contains("running:B")&&changed.Contains("running:D")&&changed.Distinct().Count()==changed.Count,"running close/new window reconciles without duplicates");
        var restored=RoundTrip(p);restored.Normalize();Check(DockLayoutRules.Running(restored,taskKeys).SequenceEqual(running),"running order persists");
        Check(!DockLayoutRules.Move(p,"missing",null,false)&&!DockLayoutRules.Move(p,"app0","app0",true),"stale and self drop are no-ops");
        double width=p.Dock.WidthFor(4);p.Dock.ShowSettings=false;Check(Math.Abs(width-p.Dock.WidthFor(4)-(p.Dock.IconSize+16+p.Dock.Spacing))<.001,"hidden settings releases exact width");
        DockOrderRules.Move(p,"app0","app3",true);p.Normalize();Check(DockLayoutRules.Common(p).Where(k=>!DockLayoutRules.SystemItem(k)).SequenceEqual(DockOrderRules.Ordered(p).Select(a=>a.Path)),"legacy app moves stay consistent with mixed layout");
        p.Apps[1].Pinned=false;p.Normalize();Check(!DockLayoutRules.Common(p).Contains("app1")&&DockLayoutRules.Common(p).Count==9,"unpin removes app but keeps all system positions");
        var next=new Entry{Name="new",Path="new",Pinned=true};p.Apps.Add(next);p.Normalize();DockLayoutRules.Move(p,"new",DockLayoutRules.Settings,true);Check(DockLayoutRules.Common(p).IndexOf("new")<DockLayoutRules.Common(p).IndexOf(DockLayoutRules.Settings),"new app inserts next to system target");
        Check(ClockCatalog.Names.Length==14&&ClockCatalog.Names.Distinct().Count()==14,"fourteen distinct clock presets");for(int i=0;i<ClockCatalog.Names.Length;i++){var style=new WidgetStyle{ClockStyle=i};style.Normalize();Check(RoundTrip(style).ClockStyle==i,"all clock styles persist");}
        var noon=new DateTime(2026,10,17,12,0,0);Check(ClockCatalog.DayFraction(noon)==.5&&ClockCatalog.HourAngle(noon)==0&&ClockCatalog.MinuteAngle(noon)==0,"clock geometry at noon");Check(ClockCatalog.HourAngle(noon.AddMinutes(30))==15&&ClockCatalog.MinuteAngle(noon.AddMinutes(30))==180,"analog hands include minute contribution");Check(ClockCatalog.DayFraction(noon.Date)==0&&ClockCatalog.DayFraction(noon.Date.AddHours(23).AddMinutes(59))<1,"day orbit boundaries");Check(ClockCatalog.Weekday(noon,5)=="S A T U R D A Y","spaced weekday label");
        using(var s=new MemoryStream(Encoding.UTF8.GetBytes("{}"))){var old=(DockOptions)new DataContractJsonSerializer(typeof(DockOptions)).ReadObject(s);old.Normalize();Check(old.ShowSettings&&old.ItemOrder!=null,"legacy settings icon remains enabled");}
        Console.WriteLine("PASS: "+count+" checks: mixed system/app drag ordering, restart/hide/reenable, schemes/copy/undo/redo, running list reconciliation, settings width, new pin targets, 13 clock presets and minute-based geometry.");return 0;
    }
}
