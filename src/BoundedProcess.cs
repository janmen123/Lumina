using System;using System.Diagnostics;using System.Threading.Tasks;
namespace Lumina {
 public static class BoundedProcess {
  public const int TimedOut=1460;
  public static async Task<int> RunAsync(ProcessStartInfo info,int timeoutMilliseconds){
   if(timeoutMilliseconds<1)throw new ArgumentOutOfRangeException("timeoutMilliseconds");
   using(var process=new Process{StartInfo=info,EnableRaisingEvents=true}){
    var exit=new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
    process.Exited+=(s,e)=>{try{exit.TrySetResult(process.ExitCode);}catch(InvalidOperationException){exit.TrySetResult(-1);}};
    if(!process.Start())throw new InvalidOperationException("启动托盘辅助程序失败。");
    if(process.HasExited)exit.TrySetResult(process.ExitCode);
    if(await Task.WhenAny(exit.Task,Task.Delay(timeoutMilliseconds)).ConfigureAwait(false)==exit.Task)return await exit.Task.ConfigureAwait(false);
    try{if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}
    await Task.WhenAny(exit.Task,Task.Delay(500)).ConfigureAwait(false);return TimedOut;
   }
  }
 }
}
