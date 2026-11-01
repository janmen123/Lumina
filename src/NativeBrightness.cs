using System;using System.Management;
namespace Lumina {
    public static class NativeBrightness {
        public static double? Read(){using(var search=new ManagementObjectSearcher("root\\WMI","SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active=True"))using(var items=search.Get()){foreach(ManagementObject item in items)using(item)return Convert.ToDouble(item["CurrentBrightness"]);}return null;}
        public static void Set(double value){bool found=false;using(var search=new ManagementObjectSearcher("root\\WMI","SELECT * FROM WmiMonitorBrightnessMethods WHERE Active=True"))using(var items=search.Get()){foreach(ManagementObject item in items)using(item){item.InvokeMethod("WmiSetBrightness",new object[]{(uint)0,(byte)Math.Max(0,Math.Min(100,value))});found=true;}}if(!found)throw new InvalidOperationException("当前屏幕不支持系统亮度调节，请使用显示器按键。");}
    }
}
