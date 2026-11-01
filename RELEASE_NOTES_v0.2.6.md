# Lumina 0.2.6 修复说明

## 问题

把任务栏隐藏（Ctrl+Alt+T）之后，控制面板里的「系统托盘」页点「打开系统托盘」只得到提示
「未找到隐藏图标入口。请先显示 Windows 任务栏，再点"打开系统托盘"。」，但面板并没有告诉用户
怎么把任务栏显示回来，点按钮本身也不解决任何事。

## 原因

隐藏图标入口（任务栏右侧的尖角按钮）是任务栏窗口的一部分。任务栏被隐藏时该窗口
`WS_VISIBLE` 位被清掉、UI Automation 里根本不存在这个元素，所以 `NativeTrayLauncher` 只能
返回 2（找不到入口）。这不是 Windows 11 XAML 任务栏的兼容问题：把任务栏显示出来后，同一个
helper 立刻返回 0 并成功展开托盘浮层（已实测）。

## 改动

- `ControlCenterTray.OpenNativeTray()`：调用 helper 前先判断 `DesktopVisibility.IsHidden(true)`，
  若任务栏处于隐藏状态就调用 `DesktopVisibility.SetHidden(true,false)` 把它显示出来（走的正是
  Ctrl+Alt+T 的同一段逻辑，会同步恢复系统自动隐藏状态、并停掉 750 ms 的维护定时器），
  等 450 ms 让 XAML 任务栏建好 UI 树，再交给 helper 打开托盘。
- 失败提示改为「未找到隐藏图标入口。请按 Ctrl+Alt+T 显示任务栏后重试。」
- 托盘页说明补一句「任务栏被隐藏时会先自动显示。」
- 版本号 0.2.6。

## 验证

- `--test-logic`：66 项检查，失败 0 项。
- `verification/RunRegression.ps1 -Binary Lumina.exe`：4 组全 PASS（含 74 条可靠性断言）。
- 组件级实测：任务栏隐藏时 `Lumina.exe --open-native-tray` 退出码 2；按 Ctrl+Alt+T 显示任务栏后
  同一命令退出码 0，`TopLevelWindowForOverflowXamlIsland` 变为可见。
- 面板按钮的完整链路（先显示任务栏再打开托盘）由用户在实机点击确认。

## 已知边界

任务栏被显示出来后不会自动再隐藏：Lumina 认为任务栏现在是显示状态，用户需要时再按一次
Ctrl+Alt+T。这样做是为了避免维护定时器在托盘浮层打开时把任务栏收回去、连带把浮层关掉。
