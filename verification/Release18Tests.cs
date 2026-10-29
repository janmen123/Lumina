using System;using System.IO;using System.Linq;using System.Reflection;using System.Security.Cryptography;using System.Text;
class Release18Tests {
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static int Main(string[] args){var root=Path.GetFullPath(args[0]);var assembly=Assembly.LoadFile(Path.Combine(root,"GlassBoard.exe"));Check(assembly.GetName().Version.ToString()=="0.9.20.0","EXE version");
 foreach(string type in new[]{"GlassBoard.UpdateService","GlassBoard.UpdateApply","GlassBoard.UpdateWorker","GlassBoard.UpdateRules"})Check(assembly.GetType(type,false)==null,"retired updater type remains: "+type);
 Check(!File.Exists(Path.Combine(root,"GlassBoard.Updater.exe")),"updater binary remains");
 foreach(string name in new[]{"MediaBridge.ps1","NativeMediaBridge.ps1","SystemControl.ps1"})using(var stream=assembly.GetManifestResourceStream("GlassBoard."+name))using(var source=File.OpenRead(Path.Combine(root,"src",name))){Check(stream!=null,"embedded script missing");using(var reader=new StreamReader(stream))using(var expected=new StreamReader(source))Check(reader.ReadToEnd()==expected.ReadToEnd(),"embedded script differs from final source "+name);}
 using(var stream=assembly.GetManifestResourceStream("GlassBoard.Assets.douyin.jpg"))using(var sha=SHA256.Create()){Check(stream!=null,"Douyin QR missing");string actual=BitConverter.ToString(sha.ComputeHash(stream));using(var source=File.OpenRead(Path.Combine(root,"assets/support/douyin.jpg")))Check(actual==BitConverter.ToString(sha.ComputeHash(source)),"QR resource changed");}
 Check(assembly.GetReferencedAssemblies().All(a=>!a.Name.StartsWith("Microsoft.Win32.SystemEvents")&&!a.Name.StartsWith("TaskbarLyrics")),"unexpected redistributable dependency");
 Console.WriteLine("PASS: final 0.9.20 EXE, updater absence, original embedded media/control scripts, exact supplied QR resource, framework-only dependencies");return 0;}
}
