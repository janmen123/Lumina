using System;using System.Text;
namespace Lumina {
    public static class AiSupport {
        public static string Build(string component,string details){
            var text=new StringBuilder();text.AppendLine("请帮我排查 Lumina 的读取 / 检索失败，并给出可以执行的修复步骤。");
            text.AppendLine("版本：v"+typeof(AiSupport).Assembly.GetName().Version.ToString(3));text.AppendLine("组件："+(component??"未指定"));
            text.AppendLine("系统："+Environment.OSVersion+"；系统 "+(Environment.Is64BitOperatingSystem?"64":"32")+" 位；进程 "+(Environment.Is64BitProcess?"64":"32")+" 位");
            text.AppendLine("记录时间："+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));text.AppendLine("源码仓库：https://github.com/SENLIN-SAMA/win-.-");
            text.AppendLine("实现：Windows .NET Framework 4.8 桌面工具；请以当前版本实际启用的组件为准。\n");
            text.AppendLine("【已观察到的状态 / 错误】");text.AppendLine(DiagnosticText.Redact(string.IsNullOrWhiteSpace(details)?"暂未记录具体错误，请先尝试刷新对应组件。":details.Length>24000?details.Substring(0,24000):details));
            text.AppendLine("\n【请我补充】");text.AppendLine("我期望的结果：\n实际结果：\n复现步骤：\n已尝试的操作及结果：\n使用的播放器或相关设备：");
            text.AppendLine("\n【排查要求】");text.AppendLine("1. 区分已证实的事实与推测；没有看到日志或源码时，不要直接断言是某个版本 bug。");
            text.AppendLine("2. 先检查对应组件开关、Windows 权限、代理、播放器媒体会话和已知依赖；每次只改一项，说明怎么验证和还原。");
            text.AppendLine("3. 如需命令，请注明执行位置、用途和影响范围，先给只读检查，再根据结果修复。文件下载阻止只处理本版已校验的组件文件。");
            text.AppendLine("4. 保留现有布局、分组、便签和配置。不要要求关闭杀毒、跳过 HTTPS 证书校验或批量解除无关文件的阻止。");
            text.AppendLine("5. 如需改代码，请先确认拿到与此版本对应的源码，再给最小补丁、编译步骤、验证方法和回退方法。");return text.ToString();
        }
    }
}
