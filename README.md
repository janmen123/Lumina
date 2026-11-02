# Lumina

当前版本：**0.2.7**。Windows 桌面启动台，使用 .NET Framework 4.8 / WPF。

本项目是基于 [GlassBoard](https://github.com/SENLIN-SAMA/win-.-)（作者 森霖Sama，MIT）修改的衍生版，保留原许可证、原作者身份与第三方素材声明。与原版相比的本地改动见 [LOCAL_CHANGES.txt](LOCAL_CHANGES.txt) 和 [CHANGELOG.md](CHANGELOG.md)。

## 使用

运行 `Lumina.exe` 或 `Start.cmd`。软件已经运行时，再次启动会直接唤回面板；命令行也可使用 `Lumina.exe --show`。主要功能包括 Dock、应用分组与搜索、运行窗口切换、桌面时钟、控制中心、快捷键、外观方案和工作区。具体可选组件以设置界面为准。

当前版本没有系统窗口玻璃注入，也不自动下载或安装上游更新。上游链接用于查看原项目（[GlassBoard](https://github.com/SENLIN-SAMA/win-.-)），不能保证上游包含本项目的本地修改。

完整操作说明见 [使用说明.md](使用说明.md)，本次变化见 [RELEASE_NOTES_v0.2.7.md](RELEASE_NOTES_v0.2.7.md)。旧版发布说明和 CHANGELOG 是历史记录，不代表当前功能。

## 数据与恢复

用户数据在 `%LOCALAPPDATA%\Lumina`。首次迁移会沿用旧 GlassBoard 数据。配置保存包含原子替换、最近可用副本、上一版副本和最多 30 份常规布局快照；完整备份仍应在设置里主动导出。

配置损坏时会保留原件并尝试读取有效副本；全部损坏或配置来自较新版本时，拒绝静默重置。不要直接删除数据目录。

## 构建与检查

在项目目录运行：

```powershell
.\Build.ps1 -OutputPath work\candidate\Lumina.exe
.\verification\RunRegression.ps1 -Binary "$PWD\work\candidate\Lumina.exe"
.\verification\RunSelfTest.ps1 -Binary "$PWD\work\candidate\Lumina.exe"
```

构建使用 Windows 自带的 .NET Framework 编译器，不要求 .NET SDK。版本号统一来自 `src/AppInfo.cs`；构建时生成原生清单。

回归测试在独立临时目录验证当前编译产物，不读取或覆盖真实布局。`--test-logic` 运行无界面逻辑检查；`--self-test` 同样先隔离数据再创建测试窗口，日志仅写入临时目录；正常完成后清理，超时强制退出时可能保留临时目录。`RunSelfTest.ps1` 还会核对真实数据目录的文件哈希，验证期间请勿在正在运行的程序中修改布局。测试通过不等于所有第三方程序、显示缩放和全屏游戏都完成实机验收。

## 日志与授权

详细诊断默认关闭，可在设置中按需开启。新写入日志会遮盖常见路径、网址、邮箱、凭据及诊断标题字段；日志不上传。`CollectDiagnostics.cmd` 导出经过再次脱敏的日志，不打包布局或便签；自由文本仍建议分享前检查。自动脱敏不会追溯改写已有历史日志。

源码与素材分别按 `LICENSE`、`THIRD_PARTY_NOTICES.md` 和 `assets/support/ASSET_NOTICE.md` 授权；保留原有素材签名及版权说明。
