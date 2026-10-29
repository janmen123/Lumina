# v0.8 核心逻辑检查

CoreTests.cs 从本版生产源码提取 LayoutRules、配置数据类、DockOptions、EntryRules、ImportScanner、HotkeyRules、HotkeyManager、ShellState，以及 FeatureCore 与天气数据契约，加上检查程序，在 .NET 8 执行。全部通过：

- 中文配置往返、旧版和部分字段迁移、旧透明度/布局/快捷键保留。
- 批量移除大小写匹配、重复选择去重、自动导入忽略、撤销顺序/固定状态，不覆盖后来添加或手动恢复的入口。
- 撤销不清除更早的忽略记录。
- 候选去重和已有入口过滤；文件类型筛选、递归开关、重叠根目录、取消和上限。
- DIY 全字段序列化（含关闭状态）、非法值归一化、不同屏宽下 Dock 容量。
- 搜索/分页、透明度、恢复记录 64 位句柄与可见状态。
- 七项自定义快捷键默认/迁移、重复拒绝、关闭项、重新绑定、占用回退、恢复默认与释放，使用内存注册适配器。

新增通过：

- 旧六项配置补齐第七项，旧自定义 Ctrl+Alt+H 冲突时仅关闭新增项。
- 分组批量移动、改名、删除保留入口；工作区路径去重与保存。
- 新布局/组件字段 JSON 往返、无效位置清理、工作区边界约束。
- 外观方案往返、布局恢复、后加入口/快捷键/工作区保留、不支持版本拒绝。
- Windows 自动隐藏位修改保留其他标志；Dock 延迟、边缘唤出、对话框阻止隐藏、关闭后恢复可见。
- 新 DIY 范围约束、天气代码与城市/当前天气 JSON 数据契约。

不涉及 Windows 原生显示控制、真实 RegisterHotKey、WPF 交互或视觉。EXE --self-test 增加 WPF 按钮/开关模板检查，但在 Linux 未执行。不能据此声称 Windows 实机验收通过。

Windows 可运行 RunCoreTests.ps1，编译并执行同一 CoreTests.cs。

新增 v0.6 已通过：旧组件默认融合与关闭备忘录；四个独立面板、任意合并、空闲面板分离；面板位置/播放列表/循环/锁定 JSON 往返及深复制；顺序/随机/循环边界及时间格式；自启动路径引用及禁用状态规则；中文/清单笔记规范化、真实临时文件原子写入、上次保存备份与损坏恢复；ID3v2 曲名/歌手/内嵌封面解析、封面文件回退；媒体桥 JSON 契约。

自启动只验证纯规则，未写真实 Windows 注册表。媒体桥只验证 JSON 契约，未在本环境解析或运行 PowerShell/WinRT。滑块与勾选框模板仅通过 XML 结构解析；WPF --self-test 及真实控件操作仍需 Windows 执行。


v0.6.1 重新从当前生产类提取并运行原回归套件。控件布局改动不属于这些纯逻辑测试的证明范围；Windows --self-test 新增长便签标题增高和音乐图标测量，尚待实机执行。


v0.6.2 通过：融合/分离两种布局 × 16 种组件关闭组合，保存、加载后重新开启全部组件；生产 WidgetRegistry 的旧实例延迟关闭、重复关闭及 100 次关闭/重新登记。未执行真实 WPF 生命周期和设置异常恢复。


v0.6.3：恢复全部组件的生产规则测试通过，保留城市/模式/播放列表/面板位置。另在私有临时测试中读取用户提供的实际配置和便签，验证配置恢复和序列化往返，不修改上传文件。Windows UI、恢复入口、PowerShell 诊断导出尚未执行。


v0.7：新增时间轴年龄/速率/范围、最新拖动队列与切歌作废、组件尺寸/0%背景及旧字段迁移、软件分类/词边界/手动分组保留、开关保存测试。已在 .NET 8 执行生产类提取套件，并针对 net48 编译。Windows API 与 WPF 验证仍待实机。


v0.7.1：闹钟今天/明天调度、单次/每天只提醒一次、跨日唤醒不补发重复提醒、倒计时暂停/继续/取消/24小时显示、闹钟深复制与 JSON 保存通过。verify_media_bridge.py 对生产 PowerShell 请求循环进行模拟：同一曲目匹配、NPSM 时间轴/跳转、延迟封面重试、拒绝过期请求及无关会话。模拟不代表 Windows COM/WinRT 实机验证。


v0.8：实际离线分类表加载（至少700别名），中英文游戏/Mod/创作别名、标点空格、快捷方式目标/产品描述、Steam URI/安装位置、辅助文件排除、真实临时目录启动项扫描、手动分类学习及改名/删除、最小宽度迁移和内容/屏幕约束高度通过。Viewbox 与隐藏滚动条属于 WPF 行为，尚需实机。

## v0.8.1 additional checks
Icon density and paging capacity, preferences migration and persistence,
appearance scheme density restoration, bindable note title property and
notification, legacy note JSON title, domestic software groups. PASS.
Production PowerShell mocks also exercise default native-off mode,
thumbnail disposal errors and timeline failure without metadata loss. PASS.
Native Windows and WPF self-test were not executed in this environment.

## v0.8.2 media supplement checks
Merge only matching source/track, standard data preference, keep transport
controls, failed native snapshot does not erase standard metadata,
input not mutated, default-enabled migration. PASS.
Both production bridge loops tested against mock sessions. PASS.
Real Windows execution and native-process timeout behavior remain unverified.

## v0.8.3 progress fallback checks
Production progress parser, fullwidth and hour formats, invalid ranges,
exact player/track matching, seek mapping and measured UI timeline routing: PASS.
.NET 8 execution and .NET Framework 4.8 harness compilation: PASS.
Actual NetEase 3.1.41 UI Automation remains unverified.

## v0.8.4 selection lifecycle regression
Extracted production RemoveMany, SetSelectionMode, FinishSelection and UpdateSelectionBar methods executed with UI stubs: PASS. Covers confirm/cancel, finishing with undo available, no-target behavior and modal state restoration. BulkSelectionTests.cs contains the executed harness. This does not verify native WPF layout.

## v0.8.5 media wire checks
UTF8/no-BOM/newline framing, Chinese title/artist/album/key request roundtrip, reply roundtrip, malformed-frame recovery and exit handling: PASS. Core harness execution on .NET8 and compilation against .NET Framework 4.8: PASS. Production PowerShell loop and pipe initializers tested in ASCII and raw Unicode modes. Real Windows acceptance remains unverified.

## v0.8.6 current-group and vendor adapter checks
PASS: actual production Controller.Add with UI/enrichment stubs checks current group, explicit ungrouped target, duplicates and manual re-add. FeatureRules import priority and batched folder moves tested; prior JSON/group regression retained.
PASS: bundled upstream progress parser, measured cache retained between worker ticks, playing stale cache expiry, paused cache retention, generation mismatch rejection. Core harness executes on .NET8 and compiles under C#5/net48 with the bundled interop assembly.
PASS: PowerShell production matching and Unicode pipes, including parent application/process lookup failure without aborting valid song/artist matching.
NOT EXECUTED: real Windows drag/drop UI and COM progress against NetEase 3.1.41.

## v0.8.7 shared membership and cover-only tests
PASS: production membership addition without duplicate entries, idempotence,
shared copy isolation, secondary-group rename/delete, source-group transfer,
local group removal/undo preserving other groups and Dock, all-view removal,
JSON persistence/migration. Existing icon capacity regressions retained.
PASS: production Controller.Add with group/UI enrichment stubs including
existing-path re-import adds membership without duplicating the entry.
PASS: CoverOnly standard and native media requests skip intentionally failing
timeline APIs and still return delayed artwork. Real Windows UI remains unverified.

## v0.8.8 review regressions
Before fixes, reproduced failures: undo overwrites later-added groups; all-view
transfer removes original groups; whitespace names break membership matching;
duplicate legacy paths remain visible. GroupReviewTests.cs now passes these
and duplicate appearance-scheme migration. All core tests re-extracted from
current production code, .NET8 PASS and C#5/net48 compile PASS.
Music mock reproduces post-transport timeline access before the fix, then
PASS for previous/toggle/next in CoverOnly mode with a throwing timeline API.
Source update clears selections on user group switch and missing-group fallback;
native WPF event/layout behavior still requires Windows validation.

## v0.8.9 experience regression
Regenerated current production core: .NET8 PASS, C#5/net48 compilation PASS.
ExperienceTests.cs PASS: bounded undo/redo, board-only snapshots, shared
memberships, redo invalidation/no-ops, artwork identity/expiry/eviction,
metadata source isolation, settings/notes/schemes/custom-icon backup roundtrip,
portable icon rebasing, malformed archive rejection, blocked destinations,
and rollback after an injected failure following the first commit.
GroupReviewTests and production Add with history hooks: PASS.
Production PowerShell media/control mocks and syntax: PASS.
ResourceTests.cs: executable manifest resources Wechat/Alipay match original
uploaded byte hashes, not just packaged filenames. PASS.
Native WPF drag/hover, creator page rendering/scan acceptance, multi-screen
DPI, real player and Windows hotkey/restore registration remain unverified.
CheckWindows.cmd includes WPF resource decoding/self-test and monitor info
for execution on Windows; it was NOT executed in this environment.
