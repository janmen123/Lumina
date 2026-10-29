using System;
using System.Linq;
namespace GlassBoard {
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
