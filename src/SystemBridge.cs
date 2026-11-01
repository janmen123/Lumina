using System;using System.Collections.Generic;using System.IO;using System.Text;using System.Diagnostics;using System.Runtime.Serialization;using System.Runtime.Serialization.Json;using System.Threading;using System.Threading.Tasks;
namespace Lumina {
    [DataContract]public sealed class BridgeRequest {
        [DataMember]public int Version=1,Id;[DataMember]public string Action="",Text="",Key="",Adapter="",Profile="",Password="",Title="",Artist="",Album="",Source="",Path="";
        [DataMember]public double Value,Position,Duration;[DataMember]public bool Enabled,Playing,Translation;[DataMember]public int Offset,Auth,Cipher;
    }
    [DataContract]public sealed class BridgeReply {[DataMember]public int Version,Id;[DataMember]public bool Ok;[DataMember]public string Error;[DataMember]public BridgeResult Result;}
    [DataContract]public sealed class BridgeResult {[DataMember]public string Warning;[DataMember]public SystemSnapshot System;[DataMember]public List<WifiNetwork> Networks;[DataMember]public List<BluetoothDevice> Devices;}
    [DataContract]public sealed class SystemSnapshot {[DataMember]public AudioSnapshot Audio;[DataMember]public double? Brightness;[DataMember]public List<RadioSnapshot> Radios;[DataMember]public List<string> Errors;}
    [DataContract]public sealed class AudioSnapshot {[DataMember]public double Volume;[DataMember]public bool Muted;[DataMember]public List<AudioOutput> Outputs;}
    [DataContract]public sealed class AudioOutput {[DataMember]public string Id,Name;[DataMember]public bool Selected;}
    [DataContract]public sealed class RadioSnapshot {[DataMember]public string Kind,Name;[DataMember]public bool Enabled,Available;}
    [DataContract]public sealed class WifiNetwork {[DataMember]public string Name,Profile,Adapter;[DataMember]public int Signal,Auth,Cipher;[DataMember]public bool Secure,Connected;}
    [DataContract]public sealed class BluetoothDevice {[DataMember]public string Id,Name;[DataMember]public bool Paired,Connected;}
    public sealed class SystemBridge:IDisposable {
        readonly SemaphoreSlim gate=new SemaphoreSlim(1);readonly System.Threading.Timer idle;DateTime lastUse=DateTime.UtcNow;
        public SystemBridge(){idle=new System.Threading.Timer(s=>TrimIdle(),null,60000,60000);}
        void TrimIdle(){if(stopped||!gate.Wait(0))return;try{if((DateTime.UtcNow-lastUse).TotalMinutes>=2)Kill();}finally{gate.Release();}}
        Process process;StreamWriter input;bool stopped;int serial;DateTime retry;string error="";
        public bool Installed{get{return Array.IndexOf(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceNames(),"GlassBoard.SystemControl.ps1")>=0;}}
        public async Task<BridgeResult> Send(BridgeRequest request){
            if(stopped)throw new ObjectDisposedException("SystemBridge");await gate.WaitAsync().ConfigureAwait(false);
            try{if(stopped)throw new ObjectDisposedException("SystemBridge");if(process==null||process.HasExited){if(DateTime.UtcNow<retry)throw new IOException("后台模块正在恢复，请稍后重试。");Start();}
                lastUse=DateTime.UtcNow;request.Id=++serial;request.Version=1;
                string body;using(var bytes=new MemoryStream()){new DataContractJsonSerializer(typeof(BridgeRequest)).WriteObject(bytes,request);body=Encoding.UTF8.GetString(bytes.ToArray());}
                await input.WriteLineAsync(body).ConfigureAwait(false);await input.FlushAsync().ConfigureAwait(false);
                var read=process.StandardOutput.ReadLineAsync();if(await Task.WhenAny(read,Task.Delay(22000)).ConfigureAwait(false)!=read)throw new TimeoutException("系统操作超时，请重试。");
                string line=await read.ConfigureAwait(false);if(line==null||line.Length>8000000)throw new IOException("后台模块未返回有效数据。");BridgeReply reply;using(var bytes=new MemoryStream(Encoding.UTF8.GetBytes(line)))reply=(BridgeReply)new DataContractJsonSerializer(typeof(BridgeReply)).ReadObject(bytes);
                if(reply==null||reply.Version!=1||reply.Id!=request.Id)throw new InvalidDataException("后台响应与当前操作不匹配。");
                if(!reply.Ok)throw new BridgeCommandException(reply.Error??"操作未完成");return reply.Result??new BridgeResult();
            }catch(BridgeCommandException){throw;}catch(Exception ex){Kill();retry=DateTime.UtcNow.AddSeconds(5);throw new IOException(ex.Message+(error.Length>0?" · "+error:""),ex);}finally{lastUse=DateTime.UtcNow;request.Password="";gate.Release();}
        }
        void Start(){
            if(!Installed)throw new FileNotFoundException("内置控制中心模块缺失，请重新下载完整程序。");
            string executable=System.Reflection.Assembly.GetExecutingAssembly().Location;
            string command="$a=[Reflection.Assembly]::LoadFile('"+executable.Replace("'","''")+"');$r=New-Object IO.StreamReader($a.GetManifestResourceStream('GlassBoard.SystemControl.ps1'));try{$s=$r.ReadToEnd()}finally{$r.Dispose()};& ([ScriptBlock]::Create($s))";
            var info=new ProcessStartInfo{FileName=WindowsHost.PowerShell,Arguments="-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(command)),WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory,UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
            process=Process.Start(info);input=new StreamWriter(process.StandardInput.BaseStream,new UTF8Encoding(false)){AutoFlush=true};error="";process.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)error=e.Data.Length>500?e.Data.Substring(0,500):e.Data;};process.BeginErrorReadLine();
        }
        void Kill(){var p=process;process=null;if(p==null)return;try{if(!p.HasExited)p.Kill();}catch{}p.Dispose();}
        public void Dispose(){stopped=true;idle.Dispose();Kill();}
        sealed class BridgeCommandException:Exception {public BridgeCommandException(string message):base(message){}}
    }
}
