# Lumina 1.0.1 — 全屏判定与备份修复

Lumina 是 Windows 桌面启动台：底部 Dock、应用分组与搜索、运行窗口切换，配合桌面时钟、控制中心、快捷键、外观方案和工作区。

1.0.1 是 1.0.0 之后的维护版，不改界面结构，只修问题：无边框全屏不再让 Dock 让开、Dock 在桌面与运行窗口之间的恢复/关闭误判，以及完整备份导出失败与分组撤销遗漏。

## 本版改动

### Dock 与运行窗口

- **无边框全屏**：浏览器 F11、Electron 客户端全屏、播放器全屏同样没有 `WS_CAPTION`、也不 `IsZoomed`，1.0.0 为挡掉桌面挂件加的过滤把它们一起挡掉了，Dock 会一直压在画面上。判定收敛到 `WindowReliabilityRules.ForegroundWindow`：非工具窗口，且「有标题栏 / 最大化 / 整块盖住显示器」任一成立即算铺满；桌面挂件仍由 `WS_EX_TOOLWINDOW` 排除（`src/RuntimeReliability.cs`、`src/FeedbackUi.cs`）。
- **点击桌面**：清除记住的前台窗口，不再把桌面当成可恢复的应用；恢复附属弹窗前先恢复主窗口，避免点了没反应（`src/RunningWindows.cs`）。
- **关闭窗口**：改走系统菜单的关闭命令（`WM_SYSCOMMAND` + `SC_CLOSE`），保留应用自己的「确认关闭」；发送失败时在 Dock 与预览卡片上提示原因并记录日志，不再静默失败（`src/RunningWindows.cs`、`src/DockTasks.cs`、`src/DockInteractions.cs`、`src/RequestedFeatures.cs`）。
- **异步恢复**：激活逻辑抽出 `src/WindowActivationAttempt.cs`，恢复后 250ms 再核验。最小化窗口在异步恢复期间会短暂成为前台，旧逻辑据此过早返回；现在要求非最小化且确实在前台，未达成才闪任务栏并记录 `WINDOW_SWITCH not completed`，用户有新输入时放弃核验。

### 控制中心

- 修复无线读取初始化 `MakeGenericType` 抛异常、蓝牙只能走兼容列表且开关不可用的问题（`src/SystemControl.ps1`）。

### 备份与分组撤销

- 修复完整备份导出没有收集 `notes.json`、校验却强制读取导致导出失败：旧便签存在则完整保留，不存在则允许备份，恢复不删除目标里的旧便签；校验改用 `Store.ReadLayout`，并拒绝不支持的配置版本（`src/ProductCore.cs`）。
- 修复分组改名未同步大小写不同的成员关系与学习规则；撤销/重做分组改名、删除时补上桌面文件夹/文档自动归类目标与开关（`src/FeatureCore.cs`、`src/ProductCore.cs`）。

## 发布包

`Lumina_v1.0.1_win64.zip` 内容与 1.0.0 一致：`Lumina.exe`、`Lumina.exe.config`、`Start.cmd`、`使用说明.md`、`LICENSE`、`THIRD_PARTY_NOTICES.md`、`lib/NPSMLib.dll`、`licenses/NPSMLib_LICENSE.txt`、`assets/support/ASSET_NOTICE.md`。源码与发布包来自同一提交（tag `v1.0.1`），可用 `Build.ps1` 复现。

## 升级与回退

退出旧版，用新的 `Lumina.exe` 覆盖即可；配置、布局与外观方案沿用，无需迁移。`lib/NPSMLib.dll` 保持压缩包内的原文件，程序启动时按 SHA-256 校验，缺失只影响音乐封面增强。回退同样只需换回 1.0.0 的 `Lumina.exe`。

## 验证

- 构建：`Build.ps1` 使用 Windows 自带的 .NET Framework 4.8 编译器，产物 `FileVersion` / `ProductVersion` 均为 `1.0.1.0`。
- 逻辑自检：`Lumina.exe --test-logic` → `LOGIC PASS 75 项检查，失败 0 项`，其中新增 5 项前台窗口判定。
- 回归套件：`verification\RunRegression.ps1` 在独立临时目录对同一产物全部通过——存储耐久性、迁移、损坏恢复、30 份快照、拖动排序与 Dock 重排、可靠性 74 条断言；新增 `DeepAuditRegression` 9 项备份/分组回归 `passed=9 failed=0`。
- 窗口生命周期：`verification/DockWindowRegression.cs` 15 项检查通过，覆盖关闭请求、应用拒绝关闭、异步激活核验与附属弹窗恢复。
- 控制中心：`verification/RadioTypeTests.ps1` 校验嵌入脚本与 WinRT 泛型类型，不改硬件状态，PASS。
- 界面自检：`verification\RunSelfTest.ps1` PASS，核对 46 个真实配置文件后 0 变化。

## 已知限制

- 仅面向 Windows 10 / 11 x64，依赖系统 .NET Framework 4.8，未提供自包含单文件版本。
- 不带自动更新，升级需要手动下载并替换 `Lumina.exe`。
- 回归测试不等于所有第三方程序、显示缩放组合与全屏游戏的实机验收；5E、完美平台和 Edge 的间歇切换问题仍需日常复测。遇到异常时可用 `CollectDiagnostics.cmd` 导出脱敏日志反馈。

## 授权

代码按 MIT 许可发布，衍生自 [GlassBoard](https://github.com/SENLIN-SAMA/win-.-)（作者 森霖Sama），保留其原始版权声明。第三方组件与服务素材的授权分别见 `THIRD_PARTY_NOTICES.md` 和 `assets/support/ASSET_NOTICE.md`。
