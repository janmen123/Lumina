using System;
using System.IO;
using System.Text;
using System.Runtime.Serialization.Json;
using GlassBoard;

public static class AppearanceOptionsTests {
    static void Check(bool value,string label){if(!value)throw new Exception(label);}
    static T Read<T>(string json){using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(json)))return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);}
    static T RoundTrip<T>(T value){using(var stream=new MemoryStream()){var serializer=new DataContractJsonSerializer(typeof(T));serializer.WriteObject(stream,value);stream.Position=0;return (T)serializer.ReadObject(stream);}}
    public static int Main(){
        var legacy=Read<Preferences>("{\"Apps\":[{\"Name\":\"剪映\",\"Path\":\"capcut.exe\",\"Group\":\"创作\"}],\"Dock\":{\"IconSize\":56,\"ShowRunning\":false},\"Light\":true}");legacy.Normalize();
        Check(!legacy.Dock.SharperIcons&&!legacy.Dock.ShowShortcutOverlays,"legacy configuration defaults are economical and arrow-free");
        Check(legacy.Dock.IconSize==56&&!legacy.Dock.ShowRunning&&legacy.Light&&FeatureRules.InGroup(legacy.Apps[0],"创作"),"existing appearance and group preserved");
        foreach(bool sharp in new[]{false,true})foreach(bool overlay in new[]{false,true}){
            legacy.Dock.SharperIcons=sharp;legacy.Dock.ShowShortcutOverlays=overlay;var restored=RoundTrip(legacy);restored.Normalize();
            Check(restored.Dock.SharperIcons==sharp&&restored.Dock.ShowShortcutOverlays==overlay,"independent options persist after restart");
            var scheme=RoundTrip(FeatureRules.Capture(restored,"外观"));var target=new Preferences();FeatureRules.Apply(target,scheme);
            Check(target.Dock.SharperIcons==sharp&&target.Dock.ShowShortcutOverlays==overlay,"appearance scheme retains both options");
            var copy=restored.Dock.Copy();copy.SharperIcons=!sharp;Check(restored.Dock.SharperIcons==sharp,"editing draft does not mutate original options");
        }
        var preferences=new Preferences();var item=new Entry{Path="app.lnk",Name="应用",Group="创作",ExtraGroups=new System.Collections.Generic.List<string>{"办公"}};preferences.Apps.Add(item);
        var history=new BoardEditHistory();var before=BoardEditState.Capture(preferences);FeatureRules.Transfer(preferences,new[]{item.Path},"创作","游戏");history.Remember(before,preferences,"移动");
        Check(FeatureRules.InGroup(item,"游戏")&&FeatureRules.InGroup(item,"办公")&&!FeatureRules.InGroup(item,"创作")&&preferences.Apps.Count==1,"moving to group keeps other membership and one all-view entry");
        history.Undo(preferences);Check(FeatureRules.InGroup(preferences.Apps[0],"创作")&&FeatureRules.InGroup(preferences.Apps[0],"办公"),"group move can be undone");
        Console.WriteLine("PASS: old appearance configuration, icon option restart/scheme/copy, group transfer and undo.");return 0;
    }
}
