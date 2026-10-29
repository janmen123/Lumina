using System;using System.IO;using System.Text;using System.Reflection;using System.Runtime.InteropServices;using System.Security.Principal;using System.Xml;using Microsoft.Win32;
namespace GlassBoard {
    public static partial class StartupRules {
        public static readonly string[] Modes={"priority","standard","delayed"};
        public static readonly string[] Names={"高 · 登录后优先启动","普通 · Windows 标准启动项","低 · 登录后延迟 60 秒"};
        public static string Mode(string value){return value=="standard"||value=="delayed"?value:"priority";}
        // COM HRESULTs may be projected as FileNotFoundException by .NET,
        // including underneath reflection's TargetInvocationException.
        public static bool MissingTask(Exception error){while(error is TargetInvocationException&&error.InnerException!=null)error=error.InnerException;return error!=null&&(error.HResult==unchecked((int)0x80070002)||error.HResult==unchecked((int)0x8004130F));}
        static string Escape(string value){return System.Security.SecurityElement.Escape(value??"");}
        public static string TaskXml(string path,string sid,string mode){Command(path);if(string.IsNullOrWhiteSpace(sid))throw new ArgumentException("用户标识无效");return "<?xml version=\"1.0\" encoding=\"UTF-16\"?><Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\"><RegistrationInfo><Description>GlassBoard current-user logon startup</Description></RegistrationInfo><Triggers><LogonTrigger><Enabled>true</Enabled><UserId>"+Escape(sid)+"</UserId><Delay>"+(Mode(mode)=="delayed"?"PT60S":"PT0S")+"</Delay></LogonTrigger></Triggers><Principals><Principal id=\"User\"><UserId>"+Escape(sid)+"</UserId><LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals><Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><AllowHardTerminate>true</AllowHardTerminate><StartWhenAvailable>true</StartWhenAvailable><RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable><AllowStartOnDemand>true</AllowStartOnDemand><Enabled>true</Enabled><Hidden>false</Hidden><RunOnlyIfIdle>false</RunOnlyIfIdle><WakeToRun>false</WakeToRun><ExecutionTimeLimit>PT0S</ExecutionTimeLimit><Priority>6</Priority></Settings><Actions Context=\"User\"><Exec><Command>"+Escape(path)+"</Command><Arguments>--startup</Arguments><WorkingDirectory>"+Escape(Path.GetDirectoryName(path))+"</WorkingDirectory></Exec></Actions></Task>";}
        public static string TaskMode(string xml){var doc=new XmlDocument{XmlResolver=null};doc.LoadXml(xml);var delay=doc.GetElementsByTagName("Delay");return delay.Count>0&&delay[0].InnerText=="PT60S"?"delayed":"priority";}
        public static string TaskCommand(string xml){var doc=new XmlDocument{XmlResolver=null};doc.LoadXml(xml);var commands=doc.GetElementsByTagName("Command");return commands.Count==0?"":Command(commands[0].InnerText);}
    }
    public static class StartupService {
        const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run",Approved=@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",Name="GlassBoard";
        sealed class Scheduler:IDisposable {
            readonly System.Collections.Generic.List<object> objects=new System.Collections.Generic.List<object>();readonly object folder;readonly string user;readonly string name;
            object Keep(object value){if(value!=null)objects.Add(value);return value;}
            static object Call(object o,string method,params object[] args){return o.GetType().InvokeMember(method,BindingFlags.InvokeMethod|BindingFlags.OptionalParamBinding,null,o,args);}
            static object Get(object o,string property){return o.GetType().InvokeMember(property,BindingFlags.GetProperty,null,o,null);}
            public Scheduler(){using(var identity=WindowsIdentity.GetCurrent())user=identity.User.Value;name="GlassBoard-"+user;var type=Type.GetTypeFromProgID("Schedule.Service",true);var service=Keep(Activator.CreateInstance(type));Call(service,"Connect");folder=Keep(Call(service,"GetFolder",@"\"));}
            public string Read(out bool enabled){enabled=false;object task;try{task=Keep(Call(folder,"GetTask",name));}catch(Exception ex){if(StartupRules.MissingTask(ex))return null;throw;}enabled=(bool)Get(task,"Enabled");return (string)Get(task,"Xml");}
            public void Register(string xml){Keep(Call(folder,"RegisterTask",name,xml,6,user,null,3,null));}
            public void Delete(){bool enabled;if(Read(out enabled)!=null)Call(folder,"DeleteTask",name,0);}
            public void Dispose(){for(int i=objects.Count-1;i>=0;i--)if(Marshal.IsComObject(objects[i]))Marshal.ReleaseComObject(objects[i]);}
        }
        static bool RunEnabled{get{using(var k=Registry.CurrentUser.OpenSubKey(Key))using(var a=Registry.CurrentUser.OpenSubKey(Approved))return StartupRules.Enabled(k==null?null:k.GetValue(Name) as string,a==null?null:a.GetValue(Name) as byte[]);}}
        public static bool Enabled{get{using(var scheduler=new Scheduler()){bool enabled;var task=scheduler.Read(out enabled);return task!=null&&enabled||RunEnabled;}}}
        public static string Mode{get{using(var scheduler=new Scheduler()){bool enabled;var xml=scheduler.Read(out enabled);return xml!=null&&enabled?StartupRules.TaskMode(xml):"standard";}}}
        public static string Target{get{using(var scheduler=new Scheduler()){bool enabled;var xml=scheduler.Read(out enabled);if(xml!=null&&enabled)return StartupRules.TaskCommand(xml);}using(var k=Registry.CurrentUser.OpenSubKey(Key))return k==null?null:k.GetValue(Name) as string;}}
        static void WriteRun(string value,byte[] approved){using(var k=Registry.CurrentUser.CreateSubKey(Key)){if(value==null)k.DeleteValue(Name,false);else k.SetValue(Name,value,RegistryValueKind.String);}using(var a=Registry.CurrentUser.CreateSubKey(Approved)){if(approved==null)a.DeleteValue(Name,false);else a.SetValue(Name,approved,RegistryValueKind.Binary);}}
        public static void Set(bool enabled){Set(enabled,"standard");}
        public static void Set(bool enabled,string mode){mode=StartupRules.Mode(mode);string previous;byte[] approved;using(var k=Registry.CurrentUser.OpenSubKey(Key))previous=k==null?null:k.GetValue(Name) as string;using(var a=Registry.CurrentUser.OpenSubKey(Approved))approved=a==null?null:a.GetValue(Name) as byte[];
            using(var scheduler=new Scheduler()){bool taskEnabled;string task=scheduler.Read(out taskEnabled);try{
                if(enabled&&mode!="standard"){string sid;using(var identity=WindowsIdentity.GetCurrent())sid=identity.User.Value;scheduler.Register(StartupRules.TaskXml(Assembly.GetExecutingAssembly().Location,sid,mode));WriteRun(null,null);}
                else{WriteRun(enabled?StartupRules.Command(Assembly.GetExecutingAssembly().Location):null,null);scheduler.Delete();}
            }catch(Exception error){try{WriteRun(previous,approved);if(task==null)scheduler.Delete();else scheduler.Register(task);}catch(Exception rollback){throw new IOException("启动设置失败，恢复原设置也未完成："+rollback.GetBaseException().Message,error);}throw new IOException("启动设置未完成，已恢复原启动项："+error.GetBaseException().Message,error);}}
        }
    }
}
