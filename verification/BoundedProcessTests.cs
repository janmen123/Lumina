using System;using System.IO;using System.Diagnostics;using System.Threading;using Lumina;
class BoundedProcessTests {
 static string host;static int checks;static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static ProcessStartInfo Self(string args){return new ProcessStartInfo(host,"\""+typeof(BoundedProcessTests).Assembly.Location+"\" "+args){UseShellExecute=false,CreateNoWindow=true};}
 static int Main(string[] args){
  if(args.Length>0&&args[0]=="--worker"){if(args[1]=="hang"){File.WriteAllText(args[2],Process.GetCurrentProcess().Id.ToString());Thread.Sleep(20000);}return 7;}
  host=args[0];
  Check(BoundedProcess.RunAsync(Self("--worker exit"),4000).GetAwaiter().GetResult()==7,"normal helper exit code is preserved");
  string file=Path.Combine(Path.GetTempPath(),"gb-tray-worker-"+Guid.NewGuid().ToString("N"));
  try{int result=BoundedProcess.RunAsync(Self("--worker hang \""+file+"\""),1200).GetAwaiter().GetResult();Check(result==BoundedProcess.TimedOut,"blocked helper has a bounded result");Check(File.Exists(file),"real child started before deadline");int pid=int.Parse(File.ReadAllText(file));bool gone;try{using(var p=Process.GetProcessById(pid))gone=p.HasExited;}catch(ArgumentException){gone=true;}Check(gone,"timed out owned helper is stopped");Check(BoundedProcess.RunAsync(Self("--worker exit"),4000).GetAwaiter().GetResult()==7,"another launch succeeds after timeout");}
  finally{if(File.Exists(file))File.Delete(file);}
  Console.WriteLine(checks+" real child-process exit/timeout/cleanup/retry checks PASS");return 0;
 }
}
