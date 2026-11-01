using System.IO;
namespace Lumina {
    public sealed class UnsupportedLayoutException : IOException {
        public UnsupportedLayoutException() : base("配置来自较新版本，当前程序不会降级或覆盖；请使用匹配版本打开。") {}
    }
}
