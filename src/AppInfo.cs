using System;

namespace Lumina {
    // 版本号唯一来源：界面、托盘、启动提示与清单都从这里取，避免多处不一致。
    public static class AppInfo {
        public const string Version="0.2.2";
        public const string AssemblyVersion=Version+".0";
        public const string Display="Lumina v"+Version;
    }
}
