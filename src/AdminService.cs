using System;using System.ComponentModel;using System.Diagnostics;using System.Security.Principal;using System.Windows;

namespace GlassBoard {
    public static class AdminService {
        public static bool IsElevated(){
            try {
                using(var identity=WindowsIdentity.GetCurrent())return identity!=null&&new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            } catch(Exception ex){Store.Log(ex);return false;}
        }
    }
    public partial class Controller {
        public void RestartElevated(){
            try {
                string exe=Process.GetCurrentProcess().MainModule.FileName;
                if(AdminService.IsElevated()){Store.Trace("ADMIN restart normal");Process.Start(new ProcessStartInfo(exe){UseShellExecute=true,WorkingDirectory=System.IO.Path.GetDirectoryName(exe)});Quit();return;}
                var info=new ProcessStartInfo(exe){UseShellExecute=true,Verb="runas",Arguments="--wait-for "+Process.GetCurrentProcess().Id,WorkingDirectory=System.IO.Path.GetDirectoryName(exe)};
                Process.Start(info);
                Store.Trace("ADMIN restart elevated");
                Quit();
            } catch(Win32Exception ex) {
                if(ex.NativeErrorCode==1223)return;
                Store.Log(ex);MessageBox.Show("无法以管理员身份重启："+ex.Message,"GlassBoard");
            } catch(Exception ex) {
                Store.Log(ex);MessageBox.Show("无法以管理员身份重启："+ex.Message,"GlassBoard");
            }
        }
    }
}
