using System;
using System.Linq;
namespace Lumina {
    public static class InteractionRules {
        public static readonly string[] OpenStyles={"fade","rise","zoom","slide"};
        public static readonly string[] OpenNames={"轻柔淡入 · 推荐","轻抬淡入","微缩淡入","侧滑淡入"};
        public static readonly string[] HoverStyles={"zoom","lift","zoom-lift","highlight"};
        public static readonly string[] HoverNames={"轻微放大","轻抬","放大并轻抬","柔和高亮"};
        public static string OpenStyle(string value){return OpenStyles.Contains(value)?value:"fade";}
        public static string HoverStyle(string value){return HoverStyles.Contains(value)?value:"zoom-lift";}
        public static bool ZoomHover(string value){return value=="zoom"||value=="zoom-lift";}
        public static bool LiftHover(string value){return value=="lift"||value=="zoom-lift";}
        public static bool DockTopmost(DockOptions options,bool dragging){return options.AlwaysOnTop||dragging;}
        // 独占全屏（游戏、全屏视频）继续整条隐藏并停扫描；只是"铺满整屏"的普通窗口
        // 则改成贴边收起、指针回到底边再弹出，既不挡内容又随时可点。
        public static bool DockDucks(bool respectFullscreen,bool exclusiveFullscreen,bool immersive,bool dragging){return respectFullscreen&&!dragging&&!exclusiveFullscreen&&immersive;}
        public static bool DockSpringOpen(bool autoHide,bool ducking){return autoHide||ducking;}
        // 收起要连续两轮（前景轮询 300ms，约 600ms）判定成立才生效，避免窗口一闪就把 Dock 打下去；
        // 恢复不设门槛，指针一碰或被遮挡的窗口走了就立刻回来。
        public static int DuckStreak(int current,bool immersive){return immersive?Math.Min(current+1,2):0;}
        public static bool DuckConfirmed(int streak){return streak>=2;}
        public static string WirelessState(SystemSnapshot snapshot,string kind){
            if(snapshot==null)return "尚未读取";
            var radios=(snapshot.Radios??new System.Collections.Generic.List<RadioSnapshot>()).Where(r=>r.Kind==kind).ToList();
            if(radios.Any(r=>r.Available))return radios.Any(r=>r.Enabled)?"已开启":"已关闭";
            if(radios.Count>0)return "已检测到设备 · 系统管理";
            if((snapshot.Errors??new System.Collections.Generic.List<string>()).Any(e=>e!=null&&(e.StartsWith("无线：")||e.StartsWith(kind=="WiFi"?"Wi-Fi：":"蓝牙："))))return "读取受限 · 可打开系统设置";
            return "未检测到适配器";
        }
    }
}
