using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Lumina {
    internal static class RuntimePaths {
        // Only the in-process regression runner sets this AppDomain value.
        internal static bool IsTest { get { return AppDomain.CurrentDomain.GetData("Lumina.TestDataFolder") != null; } }
        internal static string DataFolder {
            get {
                var test = AppDomain.CurrentDomain.GetData("Lumina.TestDataFolder") as string;
                if (test == null) return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumina");
                string full = Path.GetFullPath(test);
                string prefix = Path.Combine(Path.GetTempPath(), "Lumina-regression-");
                if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(Path.GetDirectoryName(full.TrimEnd(Path.DirectorySeparatorChar)),
                        Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Regression data must be in a dedicated temporary directory.");
                return full;
            }
        }
    }

    [Flags]
    internal enum ScanPauseReason { Game = 1, Fullscreen = 2 }

    internal sealed class ScanPauseState {
        ScanPauseReason reasons;
        internal bool Paused { get { return reasons != 0; } }
        internal void Set(ScanPauseReason reason, bool value) {
            if (value) reasons |= reason;
            else reasons &= ~reason;
        }
    }

    internal static class WindowReliabilityRules {
        internal static bool ContinueHold(bool valid, bool switched, double elapsed, int seconds) {
            return valid && !switched && seconds > 0 && elapsed >= 0
                && elapsed < Math.Min(10, seconds);
        }

        internal static bool Fullscreen(bool visible, bool minimized, bool maximized,
            bool caption, bool coversMonitor) {
            return visible && !minimized && coversMonitor && !(maximized && caption);
        }

        // 底边压进 Dock 那条带、并且至少有半屏宽的窗口：最大化的应用、贴边的长窗口。
        // 这类窗口的底边正好落在 Dock 悬浮的那一条带上，Dock 会压在它的内容上。
        // 它不该被当成游戏全屏去停扫描，但需要让开。
        internal static bool Immersive(bool visible, bool minimized, bool reachesBand,
            bool wide, bool maximizedOrCovers) {
            return visible && !minimized && reachesBand && wide && maximizedOrCovers;
        }

        // 该不该把前景窗口当成"铺满屏幕的应用窗口"：桌面挂件和悬浮层要么是工具窗口
        // （腾讯桌面整理、壁纸引擎），要么没有标题栏又不覆盖整块屏幕。
        // 但浏览器 F11、Electron 客户端全屏、播放器全屏这类无边框全屏同样没有 WS_CAPTION、
        // 也不是 IsZoomed，它们一定整块盖住显示器，所以"覆盖整块屏幕"要能单独成立，
        // 否则这些窗口没法定成铺满，Dock 会一直压在它们的画面上。
        internal static bool ForegroundWindow(bool toolWindow, bool caption, bool maximized,
            bool coversMonitor) {
            return !toolWindow && (caption || maximized || coversMonitor);
        }
    }

    public static partial class WindowActivationRules {
        internal static bool FallbackMatch(IEnumerable<string> candidates, string path, string processName) {
            var target = NormalizePath(path);
            foreach (var candidate in candidates ?? Enumerable.Empty<string>()) {
                var source = NormalizePath(candidate);
                if (string.IsNullOrWhiteSpace(source)) continue;
                string name = Path.GetFileName(source);
                if (new[] { "update.exe", "launcher.exe", "rundll32.exe", "explorer.exe" }
                    .Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
                if (target == null) {
                    if (string.Equals(name, processName, StringComparison.OrdinalIgnoreCase)) return true;
                    continue;
                }
                if (!string.Equals(name, Path.GetFileName(target), StringComparison.OrdinalIgnoreCase)) continue;
                // Allow a product's bin subdirectory, but never an unrelated installation
                // just because its executable has the same name.
                string folder = Path.GetDirectoryName(source);
                if (!string.IsNullOrEmpty(folder) && target.StartsWith(folder.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }

    internal static class RecoveryActions {
        internal static void RunAll(IEnumerable<Action> actions) {
            var failures = new List<Exception>();
            foreach (var action in actions) {
                try { action(); }
                catch (Exception ex) { failures.Add(ex); }
            }
            if (failures.Count > 0) throw new AggregateException("部分恢复未完成，恢复记录已保留，可稍后重试。", failures);
        }
    }
}
