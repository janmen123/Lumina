using System;
using System.IO;
using System.Linq;
namespace Lumina {
    public static partial class Store {
        public static readonly string Folder = RuntimePaths.DataFolder;
        public static readonly string FileName = System.IO.Path.Combine(Folder, "layout.json");
        static bool? diagnostics;
        static readonly string[] EssentialTags= {
            "SAVE","START","LOAD","ERROR","SESSION_END","RECOVER","SHUTDOWN","LAUNCH"
        }
        ;
        static readonly string DiagnosticsMarker=System.IO.Path.Combine(Folder,"debug-log.on");
        public static bool Silent;
        public static bool Diagnostics {
            get {
                if(!diagnostics.HasValue) {
                    bool on=false;
                    try {
                        on=File.Exists(DiagnosticsMarker);
                    }
                    catch {
                    }
                    diagnostics=on;
                }
                return diagnostics.Value;
            }
            set {
                diagnostics=value;
                try {
                    Directory.CreateDirectory(Folder);
                    if(value)File.WriteAllText(DiagnosticsMarker,"1");
                    else if(File.Exists(DiagnosticsMarker))File.Delete(DiagnosticsMarker);
                }
                catch {
                }
            }
        }
        static bool Essential(string message) {
            var tag=(message??"").Trim();
            int space=tag.IndexOf(' ');
            if(space>0)tag=tag.Substring(0,space);
            return Array.IndexOf(EssentialTags,tag)>=0;
        }
        static readonly object logLock = new object();
        public static void Trace(string message) {
            if (!Diagnostics && !Essential(message)) return;
            Append("session.log", message);
        }
        public static void Log(Exception error) {
            Append("error.log", error == null ? "Unknown error" : error.ToString());
        }
        public static void Log(string context, Exception error) {
            var native = error as System.ComponentModel.Win32Exception;
            Append("error.log", context + " win32=" + (native == null ? 0 : native.NativeErrorCode) + " " + error);
        }
        static void Append(string name, string message) {
            try {
                lock (logLock) {
                    Directory.CreateDirectory(Folder);
                    string path = Path.Combine(Folder, name);
                    if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024) Rotate(path, 5);
                    File.AppendAllText(path, DateTime.Now.ToString("s") + " [" + AppInfo.Version + "] "
                    + DiagnosticText.Redact(message) + Environment.NewLine);
                }
            }
            catch {
                /* Diagnostic failure must not interrupt the desktop. */
            }
        }
        static void Rotate(string path, int keep) {
            File.Move(path, path + "." + DateTime.UtcNow.Ticks);
            string pattern = Path.GetFileName(path) + ".*";
            foreach (string old in Directory.GetFiles(Folder, pattern).OrderByDescending(File.GetLastWriteTimeUtc).Skip(keep)) {
                try {
                    File.Delete(old);
                }
                catch {
                }
            }
        }
    }
}
