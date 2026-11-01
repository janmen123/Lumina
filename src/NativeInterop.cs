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
    public static class Native {
        public const int RestoreMessage=0x8000+47;
        public const int ShowPanelMessage=0x8000+48;
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
        [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(uint process);
        public static bool RequestShowPanel(){
            var handle=FindWindow(null,"Lumina Dock");
            if(handle==IntPtr.Zero)return false;
            uint process;GetWindowThreadProcessId(handle,out process);
            // Pass the foreground grant from the explicitly launched process to the owner.
            AllowSetForegroundWindow(process);
            return PostMessage(handle,ShowPanelMessage,IntPtr.Zero,IntPtr.Zero);
        }
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls,string title);
        [DllImport("user32.dll",SetLastError=true)] static extern bool PostMessage(IntPtr h,int message,IntPtr w,IntPtr l);
        public static bool RequestQuit(){var h=FindWindow(null,"Lumina Dock");return h!=IntPtr.Zero&&PostMessage(h,0x0010,IntPtr.Zero,IntPtr.Zero);} public static bool RequestRestore() {var h=FindWindow(null,"Lumina Dock");return h!=IntPtr.Zero&&PostMessage(h,RestoreMessage,IntPtr.Zero,IntPtr.Zero);}
        [DllImport("user32.dll",SetLastError=true)] public static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
        [DllImport("user32.dll",SetLastError=true)] public static extern bool SetWindowDisplayAffinity(IntPtr h,uint value);
        [DllImport("user32.dll",SetLastError=true)] public static extern bool GetWindowDisplayAffinity(IntPtr h,out uint value);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string path,uint attr,out SHFILEINFO info,uint cb,uint flags);
        [DllImport("comctl32.dll")] static extern IntPtr ImageList_GetIcon(IntPtr list,int index,uint flags);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct SHFILEINFO {
            public IntPtr hIcon; public int iIcon; public uint attributes;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string displayName;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)] public string typeName;
        }
        static readonly BoundedCache<string,ImageSource> icons=new BoundedCache<string,ImageSource>(192,StringComparer.OrdinalIgnoreCase);
        public static ImageSource Icon(string path,bool showShortcutOverlays=false,bool sharper=false) {
            if(string.IsNullOrWhiteSpace(path))return null;string key=(sharper?"sharp/":"normal/")+(showShortcutOverlays?"overlay/":"plain/")+path;ImageSource cached;if(icons.TryGet(key,out cached))return cached;
            SHFILEINFO info;IntPtr icon;
            if(showShortcutOverlays){
                SHGetFileInfo(path,0,out info,(uint)Marshal.SizeOf(typeof(SHFILEINFO)),sharper?0x4160u:0x100u);icon=info.hIcon;
                if(sharper&&icon!=IntPtr.Zero){var large=ShellIconImages.Read(info.iIcon&0xFFFFFF,((uint)info.iIcon>>24)<<8);if(large!=null){if(icon!=IntPtr.Zero)DestroyIcon(icon);icons.Put(key,large);return large;}}
            }
            else {
                // SHGFI_SYSICONINDEX retrieves the link's own custom icon index
                // without composing shortcut/cloud overlays. ILD_TRANSPARENT
                // copies only that base image; the system image list is shared.
                var list=SHGetFileInfo(path,0,out info,(uint)Marshal.SizeOf(typeof(SHFILEINFO)),0x4000);
                if(sharper&&list!=IntPtr.Zero){var large=ShellIconImages.Read(info.iIcon);if(large!=null){icons.Put(key,large);return large;}}
                icon=list==IntPtr.Zero?IntPtr.Zero:ImageList_GetIcon(list,info.iIcon,1);
            }
            if(icon==IntPtr.Zero) return null;
            try {
                var source=Imaging.CreateBitmapSourceFromHIcon(icon,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(64,64));
                source.Freeze(); icons.Put(key,source); return source;
            } catch { return null; } finally { DestroyIcon(icon); }
        }
        public static void ClearIconCache(){icons.Clear();}
        public static void Computer() { Process.Start(new ProcessStartInfo("explorer.exe","shell:MyComputerFolder"){UseShellExecute=true}); }
    }
    [DataContract] public class ShellState {
        [DataMember] public long Handle;
        [DataMember] public string ClassName;
        [DataMember] public bool Visible;
    }
}
