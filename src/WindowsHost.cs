using System;using System.IO;
namespace GlassBoard {
    public static class WindowsHost {
        // WinRT radio enumeration must run at the OS's native architecture.
        public static string PowerShell{get{string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);string system=Environment.Is64BitOperatingSystem&&!Environment.Is64BitProcess?Path.Combine(windows,"Sysnative"):Path.Combine(windows,"System32");return Path.Combine(system,"WindowsPowerShell","v1.0","powershell.exe");}}
    }
}
