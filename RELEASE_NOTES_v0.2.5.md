# Lumina 0.2.5（2026-11-02，本地维护）

玩窗口化的游戏时，Dock 不再从屏幕底边浮出来挡视野：前台进程的安装路径落在 Steam / Epic / GOG / WeGame / Riot / Xbox / Ubisoft 的游戏库里，就当作"在玩游戏"，直接进入游戏模式，Dock、面板、组件一起隐藏，扫描也停掉。

## 问题

0.2.3 给"铺满整屏的普通窗口"做了贴边收起：Dock 只留屏底 20 像素，指针触底时弹出。这在浏览器/聊天软件上合适，但很多游戏默认是**窗口化（最大化但保留标题栏）**，走的正是这条贴边收起的路径——一甩鼠标压到底边，Dock 就弹出来盖在准星上。彻底隐藏原来只有"独占全屏"这一档，窗口化游戏够不着。

## 改动

- 新增 `src/GameDetection.cs`：`IsGamePath` 按目录特征判断（`\steamapps\common\`、`\epic games\`、`\gog games\`、`\gog galaxy\games\`、`\wegameapps\`、`\riot games\`、`\xboxgames\`、`\ubisoft game launcher\games\`），并排除平台客户端本身（Epic 启动器、Riot Client、Steam 的 `\steam\bin\`、WeGame）；`ForegroundIsGame()` 取前台窗口的进程路径，按 (hwnd, pid) 缓存 1.5 秒，避免每次轮询都查进程。
- `GameModeRules.AutoActive(automatic, fullscreen, game)` 把"全屏"和"检测到游戏"并成同一个自动抑制条件；开启"检测全屏或游戏时自动进入"后，两者任一成立即进入游戏模式。
- `Controller.ApplyGameMode` 与 `ControllerCore` 的初始抑制状态都改成同一条判据，避免启动瞬间与后续状态不一致。
- 设置项文案改为「检测全屏或游戏时自动进入（默认关闭）」；默认值不变，仍需要用户显式打开（本次已在你的配置里打开）。

## 验证

- 逻辑自检 58 项增至 66 项：新增自动模式的三种组合、Steam 库里 `cs2.exe` 算游戏、Epic 启动器不算、WeGame 里 `LeagueClient` 算、普通程序不算。
- 回归套件全 PASS（生产 Store 持久化与恢复、30 次拖动排序、Dock 排序与撤销重做、可靠性回归 74 项断言）。
- 实机端到端：在 `work\fake-game\steamapps\common\TestGame\` 编译了一个 900×600 的空窗口程序冒充游戏，前台切到它之后日志记为 `GAME suppress=True auto=True game=True fullscreen=False`，Dock 的窗口 `IsWindowVisible` 变 False；切回普通窗口记为 `game=False` 并恢复显示。
- 顺带确认原有行为没变：独占全屏（抖音全屏播放）仍然直接隐藏，日志 `fullscreen=True`。

## 已知边界

- 只按安装路径识别游戏。装在自定义目录（不经过任何平台库）的游戏、以及平台库之外绿色版游戏，识别不到；这类仍可用托盘右键「进入游戏模式」手动切换。
- 平台客户端本身不算游戏（避免开着 Steam 就一直抑制），但客户端里内嵌的游戏窗口如果由同一个进程渲染，同样识别不到。
- 前台窗口的进程路径读不到时（权限受限的反作弊进程）按"不是游戏"处理。
- 未逐一实测 CS2 / 其他游戏，仅用同路径特征的替身程序验证了链路。
