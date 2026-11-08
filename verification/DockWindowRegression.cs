using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Lumina;
class DockWindowRegression {
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    class Probe:Form {
        public int CloseCommands; public bool Refuse;
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x112 && (m.WParam.ToInt64()&0xfff0)==0xf060)CloseCommands++;
            base.WndProc(ref m);
        }
        protected override void OnFormClosing(FormClosingEventArgs e) { if(Refuse)e.Cancel=true; base.OnFormClosing(e); }
    }
    static void Check(bool value,string text) { if(!value)throw new Exception(text); Console.WriteLine("PASS: "+text); }
    static void Pump() { for(int i=0;i<15;i++){Application.DoEvents();Thread.Sleep(20);} }
    static RunningWindow Target(Form f) { return new RunningWindow {Handle=f.Handle,ProcessId=(uint)Process.GetCurrentProcess().Id}; }
    [STAThread] static int Main() {
        AppDomain.CurrentDomain.SetData("Lumina.TestDataFolder",System.IO.Path.Combine(System.IO.Path.GetTempPath(),"Lumina-regression-dock-"+Guid.NewGuid().ToString("N")));
        try {
            var flow=typeof(RunningWindows).Assembly.GetType("Lumina.WindowActivationAttempt").GetMethod("Run",BindingFlags.Static|BindingFlags.NonPublic);
            bool iconic=true,front=true;int restored=0,focused=0,fallbacks=0;
            Func<bool> minimized=()=>iconic,foreground=()=>front;
            Action restore=()=>restored++,focus=()=>focused++;
            Action fallback=()=>{fallbacks++;iconic=false;};
            Func<bool> run=()=>(bool)flow.Invoke(null,new object[]{minimized,foreground,restore,focus,fallback});
            Check(run()&&restored==1&&focused==1&&fallbacks==1,"foreground-but-minimized async restore reaches fallback");
            Check(run()&&restored==1&&fallbacks==1,"already visible foreground window needs no fallback");
            iconic=true;fallback=()=>fallbacks++;
            Check(!run(),"foreground alone never reports success while still minimized");
            iconic=false;front=false;fallback=()=>{fallbacks++;front=true;};
            Check(run(),"visible background window still receives foreground fallback");
            using(var f=new Probe {Text="Lumina temporary close test",Width=250,Height=100}) {
                f.Show();Pump();var w=Target(f);
                Check(RunningWindows.RequestClose(w),"normal close request accepted");Pump();
                Check(f.CloseCommands==1 && !IsWindow(w.Handle),"exactly one system close command destroys test window");
                Check(!RunningWindows.RequestClose(w) && RunningWindows.CloseFailure!=null,"stale handle fails with feedback");
            }
            using(var f=new Probe {Text="Lumina temporary cancel test",Refuse=true,Width=250,Height=100}) {
                f.Show();Pump();var w=Target(f);
                var wrong=new RunningWindow {Handle=w.Handle,ProcessId=w.ProcessId+1};
                var verify=typeof(RunningWindows).GetMethod("VerifyActivation",BindingFlags.Static|BindingFlags.NonPublic);
                var pending=typeof(RunningWindows).GetField("activationCheck",BindingFlags.Static|BindingFlags.NonPublic);
                verify.Invoke(null,new object[]{w.Handle});
                Check(pending.GetValue(null)!=null,"async activation verification is deferred");
                Check(!RunningWindows.RequestClose(wrong),"reused handle/wrong process rejected");
                Check(pending.GetValue(null)==null,"new close action cancels stale activation verification");
                Check(RunningWindows.RequestClose(w),"application may handle close request");Pump();
                Check(IsWindow(w.Handle)&&f.CloseCommands==1,"application cancellation respected; no forced termination");
                f.Refuse=false;f.Close();
            }
            using(var owner=new Probe {Text="Lumina temporary owner test",Width=250,Height=100})
            using(var popup=new Probe {Text="Lumina temporary dialog test",Width=200,Height=80}) {
                owner.Show();popup.Show(owner);popup.Activate();Pump();
                RunningWindows.MinimizeForCheck(owner.Handle);Pump();
                Check(IsIconic(owner.Handle),"owner minimized with owned popup");
                Check(RunningWindows.ActivateHandle(owner.Handle),"owned-window activation recognized");Pump();
                Check(!IsIconic(owner.Handle),"activation restores owner before focusing popup");
                popup.Close();owner.Close();
            }
            return 0;
        } catch(Exception e) {Console.Error.WriteLine(e);return 1;}
    }
}
