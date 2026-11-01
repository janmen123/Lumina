using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;


namespace Lumina {
    public static class Program {
        [STAThread] public static int Main(string[] args) {
            if(args.Length==2 && args[0]=="--export-diagnostics") {
                try { DiagnosticExport.Create(args[1]); return 0; }
                catch(Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
            }
            if(args.Length>0 && args[0]=="--quit")return Native.RequestQuit()?0:1;if(args.Length>0 && args[0]=="--open-native-tray")return NativeTrayLauncher.Run();
            
            if(args.Length>1&&args[0]=="--wait-for"){int waitPid;if(int.TryParse(args[1],out waitPid)){try{Process.GetProcessById(waitPid).WaitForExit(8000);}catch{}}}
            if(args.Length>0 && args[0]=="--test-logic")return IsolatedTestRun.Run(false);
            if(args.Length>0 && args[0]=="--self-test")return IsolatedTestRun.Run(true);
            if(args.Length>1 && args[0]=="--toggle-check") return ToggleCheck(args);
            if(args.Length>0 && args[0]=="--restore-shell") {
                try {if(Native.RequestRestore())return 0;try{TaskbarAutoHide.ShowAndClear();}catch(Exception ex){Store.Log(ex);}DesktopVisibility.ShowAll();MessageBox.Show("已执行任务栏和桌面图标恢复。","Lumina");return 0;}catch(Exception ex){MessageBox.Show(ex.Message,"Lumina");return 1;}
            }
            bool created;using(var mutex=new Mutex(true,"Local\\Lumina.Desktop.v1",out created)) {
                if(!created){
                    if(args.Any(a=>a=="--startup"))return 0;
                    if(Native.RequestShowPanel())return 0;
                    MessageBox.Show("已有 Lumina 正在启动或暂未响应，请稍后重试，或从系统托盘打开应用面板。",AppInfo.Display);
                    return 1;
                }
                try {
                    Store.Trace("START exe="+System.Reflection.Assembly.GetExecutingAssembly().Location+" OS="+Environment.OSVersion+" CLR="+Environment.Version);
                    try{DesktopVisibility.Recover();}catch(Exception ex){Store.Log(ex);}
                    try{TaskbarAutoHide.Restore();}catch(Exception ex){Store.Log(ex);}
                    AppDomain.CurrentDomain.UnhandledException+=(s,e)=>{var error=e.ExceptionObject as Exception;if(error==null)error=new Exception("非托管异常");Store.Log(error);try{Store.Trace("CRASH unhandled terminating="+e.IsTerminating);}catch{}};
                    TaskScheduler.UnobservedTaskException+=(s,e)=>{Store.Log(e.Exception);try{Store.Trace("CRASH unobserved task");}catch{}e.SetObserved();};
                    var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
                    app.Exit+=(s,e)=>{try{DesktopVisibility.RestoreOriginal();}catch(Exception ex){Store.Log(ex);}try{TaskbarAutoHide.Restore();}catch(Exception ex){Store.Log(ex);}};
                    Controller controller=null;
                    app.DispatcherUnhandledException+=(s,e)=>{Store.Log(e.Exception);MessageBox.Show("操作失败，详情已记录在配置文件夹 error.log；已保留上一次成功保存的布局。\n"+e.Exception.Message,"Lumina");e.Handled=true;};
                    controller=new Controller();app.SessionEnding+=(s,e)=>controller.SaveSession();if(args.Any(a=>a=="--recover-widgets"))controller.RecoverWidgets();if(args.Any(a=>a=="--show"))controller.RevealPanel();app.Run();GC.KeepAlive(controller);return 0;
                } catch(Exception ex){Store.Log(ex);MessageBox.Show("启动失败："+ex.Message,"Lumina");return 1;}
            }
        }
        static int ToggleCheck(string[] args){
            var lines=new List<string>();
            try {
                string target=args[1];bool act=args.Any(a=>a=="--act");bool minimize=args.Any(a=>a=="--minimize");bool restore=args.Any(a=>a=="--restore");
                var entry=new Entry{Name=EntryPathRules.Name(target),Path=EntryPathRules.Normalize(target),IsFolder=Directory.Exists(target)};
                LaunchInfoService.Enrich(entry);
                RunningWindows.Scan();
                var matches=RunningWindows.For(entry);
                lines.Add("target="+target);
                lines.Add("launchTarget="+entry.LaunchTarget);
                lines.Add("executables="+string.Join(";",WindowActivationRules.Executables(entry).ToArray()));
                lines.Add("foreground="+RunningWindows.Foreground());
                lines.Add("matches="+matches.Count);
                foreach(var window in matches)lines.Add("  handle="+window.Handle+" pid="+window.ProcessId+" iconic="+RunningWindows.Minimized(window.Handle)+" path="+window.Path+" title="+window.Title);
                if(minimize&&matches.Count>0){RunningWindows.MinimizeForCheck(matches[0].Handle);lines.Add("minimize iconic="+RunningWindows.Minimized(matches[0].Handle));}
                if(restore&&matches.Count>0){RunningWindows.RestoreForCheck(matches[0].Handle);lines.Add("restore iconic="+RunningWindows.Minimized(matches[0].Handle));}
                if(act){IntPtr hint=IntPtr.Zero;int at=Array.IndexOf(args,"--hint");if(at>=0&&at+1<args.Length){long value;if(long.TryParse(args[at+1],out value))hint=new IntPtr(value);}bool ok=RunningWindows.Toggle(entry,hint);lines.Add("hint="+hint);lines.Add("toggle="+ok);lines.Add("toggle foreground="+RunningWindows.Foreground());foreach(var window in matches)lines.Add("  after iconic="+window.Handle+" "+RunningWindows.Minimized(window.Handle));}
            } catch(Exception ex){lines.Add("error="+ex);}
            try{File.WriteAllLines(Path.Combine(Store.Folder,"toggle-check.txt"),lines);}catch{}
            return 0;
        }
    }
}
