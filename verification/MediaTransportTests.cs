// Executed on .NET 8 with PowerShell mock paths in /tmp; native Windows UI is not covered.
using System;using System.IO;using System.Text;using System.Diagnostics;using System.Runtime.Serialization.Json;using System.Threading.Tasks;using System.Reflection;
namespace GlassBoard {
    public sealed class MediaSessionBridge:IDisposable {
        readonly bool nativeOnly,neteaseOnly;public MediaSessionBridge(bool native=false,bool netease=false){nativeOnly=native;neteaseOnly=netease;}public bool UseNative;Process process;readonly object gate=new object();bool stopped;DateTime retry=DateTime.MinValue;string lastFailure="";readonly object errorGate=new object();string stderr="";
        public Task<MediaSnapshot> Send(string command,double seconds=0,string key=null,MediaSnapshot hint=null){return Task.Run(()=>{lock(gate){if(stopped)return new MediaSnapshot{Status="播放器已关闭"};try{
            if(process==null||process.HasExited){if(DateTime.UtcNow<retry)return new MediaSnapshot{Status="等待后台读取重试："+lastFailure};Start();}
            var request=new MediaRequest{Command=command,Seconds=seconds,Key=key,UseNative=UseNative,Source=hint==null?null:hint.Source,Title=hint==null?null:hint.Title,Artist=hint==null?null:hint.Artist,Album=hint==null?null:hint.Album,Playing=hint!=null&&hint.Playing};MediaWireProtocol.Write(process.StandardInput.BaseStream,request);
            var read=process.StandardOutput.ReadLineAsync();if(!read.Wait(nativeOnly||neteaseOnly?3500:10000))throw new TimeoutException("系统媒体读取超时");string line=read.Result;if(line==null){string detail;lock(errorGate)detail=stderr;throw new IOException("后台读取进程已退出"+(process.HasExited?"（"+process.ExitCode+"）":"")+(string.IsNullOrWhiteSpace(detail)?"":"："+detail.Trim()));}if(line.Length>12000000)throw new InvalidDataException("封面数据过大");using(var s=new MemoryStream(Encoding.UTF8.GetBytes(line)))return (MediaSnapshot)new DataContractJsonSerializer(typeof(MediaSnapshot)).ReadObject(s);
        }catch(Exception ex){lastFailure=ex.GetType().Name+"："+ex.Message;Kill();retry=DateTime.UtcNow.AddSeconds(5);return new MediaSnapshot{Status=lastFailure};}}});}
        void Start(){Kill();string script=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"src",nativeOnly?"NativeMediaBridge.ps1":"MediaBridge.ps1");if(!neteaseOnly&&!File.Exists(script))throw new FileNotFoundException("请完整解压程序，媒体桥脚本缺失");var p=new ProcessStartInfo{FileName=neteaseOnly?System.Reflection.Assembly.GetExecutingAssembly().Location:System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe"),Arguments=neteaseOnly?"--netease-ui-bridge":"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+script+"\"",UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};lock(errorGate)stderr="";process=Process.Start(p);process.ErrorDataReceived+=(s,e)=>{if(!string.IsNullOrEmpty(e.Data))lock(errorGate){stderr+=e.Data+"\n";if(stderr.Length>2000)stderr=stderr.Substring(stderr.Length-2000);}};process.BeginErrorReadLine();}
        void Kill(){var p=process;process=null;if(p==null)return;try{if(!p.HasExited)p.Kill();}catch{}p.Dispose();}
        public void Dispose(){stopped=true;var p=process;try{if(p!=null&&!p.HasExited)p.Kill();}catch{}Task.Run(()=>{lock(gate)Kill();});}
    }

class TransportTest {static void Check(bool ok,string message){if(!ok)throw new Exception(message);}static int Main(){
using(var worker=Process.Start(new ProcessStartInfo{FileName="/tmp/glassboard-pwsh/pwsh",Arguments="-NoLogo -NoProfile -File /tmp/GlassBoardNativeMock085.ps1",UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8}))
using(var bridge=new MediaSessionBridge(true)){
typeof(MediaSessionBridge).GetField("process",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(bridge,worker);
var hint=new MediaSnapshot{Available=true,Source="FakeMusic",Title="再见",Artist="张震岳",Album="思念是一种病",Key="FakeMusic|再见|张震岳|思念是一种病",Playing=true};
var first=bridge.Send("cover",0,hint.Key,hint).Result;Check(first.Available&&first.Key==hint.Key&&first.Title==hint.Title&&first.Duration==180,"Unicode native matching via actual Send");
var second=bridge.Send("cover",0,hint.Key,hint).Result;Check(second.Available&&!string.IsNullOrEmpty(second.Cover)&&second.CoverStatus=="NPSMLib thumbnail","Chinese song delayed artwork returned");
var seek=bridge.Send("seek",50,hint.Key,hint).Result;Check(seek.Status=="OK"&&seek.Position==50,"Unicode seek route");
}Console.WriteLine("PASS: actual MediaSessionBridge.Send -> UTF8 subprocess pipes -> production native matching -> cover/timeline/seek replies for Chinese song");return 0;}}
}
