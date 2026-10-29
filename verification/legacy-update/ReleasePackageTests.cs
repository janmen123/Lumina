using System;using System.IO;using System.Linq;using System.Reflection;using System.Runtime.Serialization.Json;using GlassBoard;
public static class ReleasePackageTests {
    public static int Main(string[] args){
        byte[] bytes=File.ReadAllBytes(args[0]);UpdateAsset index;using(var input=File.OpenRead(args[1]))index=(UpdateAsset)new DataContractJsonSerializer(typeof(UpdateAsset)).ReadObject(input);
        if(index.Size!=bytes.Length||index.Name!=Path.GetFileName(args[0])||index.Digest!="sha256:"+UpdateRules.Hash(bytes))throw new Exception("package/index mismatch");
        if(bytes.Length>=2*1024*1024)throw new Exception("lightweight user package exceeded 2 MiB");
        string stage=Path.Combine(Path.GetTempPath(),"GlassBoard-package-check-"+Guid.NewGuid().ToString("N"));try{
            var hashes=UpdateApply.Extract(bytes,stage,new Version(0,9,17,0));
            if(hashes.Count!=23||hashes.Keys.Any(k=>k.StartsWith("engine/")||k.Contains("TaskbarLyrics")||k.StartsWith("licenses/nuget/")))throw new Exception("unexpected heavyweight runtime dependency");
            foreach(string path in new[]{"GlassBoard.Updater.exe","lib/NPSMLib.dll","Interop.UIAutomationClient.dll","src/MediaBridge.ps1","src/NativeMediaBridge.ps1"})if(!hashes.ContainsKey(path))throw new Exception("missing original runtime dependency: "+path);
            var app=Assembly.LoadFile(Path.Combine(stage,"GlassBoard.exe"));
            foreach(string name in new[]{"MediaBridge.ps1","NativeMediaBridge.ps1","SystemControl.ps1"})using(var stream=app.GetManifestResourceStream("GlassBoard."+name))if(stream==null)throw new Exception("missing embedded helper "+name);
            if(app.GetReferencedAssemblies().Any(a=>a.Name.Contains("TaskbarLyrics")||a.Name=="System.Private.CoreLib"||a.Name=="WinRT.Runtime"))throw new Exception("external framework reference survived rollback");
            Console.WriteLine("PASS: v0.9.17 lightweight 23-file package, under 2 MiB, no engine/.NET 8/TaskbarLyrics, original media dependencies, embedded controls, executable version, hashes and update index.");
        }finally{if(Directory.Exists(stage))Directory.Delete(stage,true);}return 0;
    }
}
