// Historical v0.9.6 test. Superseded by PersistenceTests.cs for v0.9.7 mirror/journal precedence.
using System;using System.IO;using System.Collections.Generic;using GlassBoard;
public static class RemovalStoreTests {
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static int Main(){
        try{
            var prefs=new Preferences();var app=new Entry{Name="A",Path="old.lnk",LaunchTarget="app.exe"};prefs.Apps.Add(app);prefs.Normalize();Store.Save(prefs);EntryRules.Remove(prefs,new[]{app.Path},"全部");Store.Save(prefs);
            Check(File.Exists(Path.Combine(Store.Folder,"removed.json")),"separate removal journal created");File.WriteAllText(Store.FileName,"broken JSON");var recovered=Store.Load();Check(recovered.Apps.Count==0&&RemovalRules.Blocked(recovered,new Entry{Path="new.lnk",LaunchTarget="app.exe"}),"prior layout recovery merges latest tombstones");
            var intentional=Store.Load(false);Check(intentional.Apps.Count==1,"explicit restore can override current deletion journal");RemovalRules.Forget(intentional,app);Store.Save(intentional);Check(Store.Load().Apps.Count==1,"manual restore refreshes deletion journal");
            Console.WriteLine("PASS: production Store save/recovery and independent deletion journal, explicit restore and tombstone reset.");return 0;
        }finally{if(Directory.Exists(Store.Folder))Directory.Delete(Store.Folder,true);}
    }
}
