# Lumina 1.0.0 — 首个正式版

Lumina 是 Windows 桌面启动台：底部 Dock、应用分组与搜索、运行窗口切换，配合桌面时钟、控制中心、快捷键、外观方案和工作区。

1.0.0 把此前 0.2.x 的本地维护线收口为正式发布版：功能与 0.2.7 一致，版本号统一提升到 1.0.0，并首次提供可直接下载的运行包。

## 本版改动

- `src/AppInfo.cs`：版本 0.2.7 → 1.0.0。程序集版本、原生清单（`src/app.manifest`）、界面与托盘显示、诊断日志前缀都从这一个常量生成。
- 新增发布包 `Lumina_v1.0.0_win64.zip`，内容为：`Lumina.exe`、`Lumina.exe.config`、`Start.cmd`、`使用说明.md`、`LICENSE`、`THIRD_PARTY_NOTICES.md`、`lib/NPSMLib.dll`、`licenses/NPSMLib_LICENSE.txt`、`assets/support/ASSET_NOTICE.md`。
- 源码与发布包来自同一提交（tag `v1.0.0`），可用 `Build.ps1` 复现。
- `README.md`：版本号更正为 1.0.0，增加下载入口与上游署名。
- `使用说明.md`：标题去掉硬编码版本号，避免随版本过期。

## 1.0 包含的能力

- **Dock**：位置、图标尺寸、边距可调；贴边自动收起为窄条，指针触底弹出；鼠标停靠时不需要点按。
- **分组**：自建分组可左右拖动排序，落在左半区插入其前方、右半区插入其后方，松手即保存；Ctrl+Z / Ctrl+Y 撤销重做；「全部」「未分组」固定在前。
- **运行窗口**：列出当前窗口并一键切换；桌面层窗口（桌面整理、壁纸程序）与工具窗不参与「铺满屏幕」判定。
- **游戏模式**：按 Steam / Epic / GOG / WeGame / Riot / Xbox / Ubisoft 安装路径识别，窗口化游戏同样进入游戏模式，Dock 与组件一起让位。
- **任务栏**：Ctrl+Alt+T 隐藏任务栏并把工作区退回整屏；「打开系统托盘」会先把任务栏显示出来。
- **外观与工作区**：外观方案可保存、导入导出，包含布局、组件与城市等信息；最多 30 份历史布局快照。
- **音乐**：以 GSMTC 为主；`lib/NPSMLib.dll` 可用时补充封面与进度，并支持拖动进度。

## 数据与恢复

用户数据在 `%LOCALAPPDATA%\Lumina`。保存采用原子替换，保留最近可用副本、上一版副本和最多 30 份快照。配置损坏时保留原件并尝试读取有效副本；全部损坏或来自较新版本时拒绝静默重置。完整备份建议在设置里主动导出。

## 升级与回退

从 0.2.x 覆盖 `Lumina.exe` 即可，配置与布局自动沿用，无需迁移步骤。`lib/NPSMLib.dll` 保持压缩包内的原文件，程序启动时会按 SHA-256 校验；缺失或与版本不一致时只影响音乐封面增强，其余功能正常。回退同样只需换回旧版 `Lumina.exe`。

## 验证

- 构建：`Build.ps1` 使用 Windows 自带的 .NET Framework 4.8 编译器，不需要 .NET SDK；产物 `FileVersion` / `ProductVersion` 均为 `1.0.0.0`。
- 逻辑自检：`Lumina.exe --test-logic` → `LOGIC PASS 70 项检查，失败 0 项`。
- 回归套件：`verification\RunRegression.ps1` 在独立临时目录对同一产物全部通过——存储耐久性、迁移、损坏恢复、删除日志、30 份快照；30 组双向拖动排序与 Dock 重排撤销；可靠性回归 74 条断言。

## 已知限制

- 仅面向 Windows 10 / 11 x64，依赖系统 .NET Framework 4.8，未提供自包含单文件版本。
- 不带自动更新，升级需要手动下载并替换 `Lumina.exe`。
- 回归测试不等于所有第三方程序、显示缩放组合与全屏游戏的实机验收；遇到具体软件表现异常时，可用 `CollectDiagnostics.cmd` 导出脱敏日志反馈。

## 授权

代码按 MIT 许可发布，衍生自 [GlassBoard](https://github.com/SENLIN-SAMA/win-.-)（作者 森霖Sama），保留其原始版权声明。第三方组件与服务素材的授权分别见 `THIRD_PARTY_NOTICES.md` 和 `assets/support/ASSET_NOTICE.md`。
