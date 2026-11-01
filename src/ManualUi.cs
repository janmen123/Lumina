using System;using System.Windows;using System.Windows.Controls;using System.Windows.Media;
namespace Lumina {
    public partial class Controller {
        void BuildManual(StackPanel panel){
            panel.Children.Add(new TextBlock{Text="玻璃板使用说明",FontSize=25,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,12,0,8)});
            panel.Children.Add(new TextBlock{Text="点击问题标题展开查看。快捷键以你在「快捷键与开关」中设置的组合为准。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,18)});
            ManualGroup(panel,"开始使用");
            ManualItem(panel,"设置父子分类与翻页动效","设置左侧按外观与动效、组件与系统分组。进入外观与动效 → UI 动效，可分别设置展开、悬停及大玻璃板翻页；翻页支持轻柔淡入、水平滑入和微缩淡入，与展开动画独立。");
            ManualItem(panel,"统一桌面层开关","外观与动效 → 面板与主题 → 所有组件置于桌面层。开启后统一覆盖 Dock、大面板、小球和组件的置顶偏好；关闭后恢复各自设置。设置窗口与系统原生菜单正常弹出。Windows 实际层级及 Win+D 请按验收清单测试。");
            ManualItem(panel,"小球独立栏目与原生开始菜单","右键小球 → 小球设置，可分别显示控制中心、系统托盘与开始菜单入口；开始菜单调用 Windows 系统自带菜单。系统托盘入口直接打开 Windows 原生隐藏图标面板，在系统面板中单击、双击或右键原图标。");
            ManualItem(panel,"第一次打开与完整解压","先将用户 ZIP 完整解压到固定位置，再运行 Lumina.exe 或 Start.cmd。不要在压缩包预览中直接运行，也不要只复制一个 EXE。便签和布局保存在当前 Windows 用户的配置目录，更新前可在通用中导出完整备份。新用户组件默认有框；老用户已有外观设置会保留。");
            ManualItem(panel,"展开与收起大玻璃板","点击 Dock 的「应用」按钮，或双击系统托盘中的 Lumina 图标。默认 Ctrl+Alt+Space 展开 / 收起；按 Esc 收起。需要点击其他软件后自动收起，可在「面板与主题」开启相应选项。");
            ManualItem(panel,"添加软件、文件和文件夹","把软件快捷方式、文件或文件夹拖入大面板，或使用添加按钮。导入开始菜单可以批量添加软件。普通入口右键支持打开位置、文件属性、改名称与图标。移除入口只影响玻璃板，原文件仍在磁盘上。");
            ManualItem(panel,"在大玻璃板中新建文件","在大面板空白处右键，或打开更多菜单 → 新建文件或文件夹。支持 TXT、Markdown、Word、Excel 和文件夹。选择保存位置后，入口会加入当前分组；在全部视图中新建则加入未分组。");
            ManualItem(panel,"分组、批量整理和删除分组","上方文件夹条可以切换分组。将图标拖到分组可整理；全部视图拖到分组会保留入口，组间拖动则移动该组归属。同一入口可以属于多个分组。删除分组保留软件入口并移到未分组，若仍属于其他组则保留其他归属。编辑模式支持批量选择，Ctrl+Z 撤销、Ctrl+Y 重做。");
            ManualItem(panel,"Dock 固定、排序与正在运行","图标右键 → 固定到 Dock，也可拖到 Dock 空隙；按住图标可拖动排序。Dock 上滚回常用入口，下滚查看正在运行的窗口。点击运行中的图标可切回，右键可选择窗口或发送关闭请求。关闭仍由目标软件处理，编辑器可能提示保存。");
            ManualItem(panel,"文件夹与回收站","Dock 的正在运行页会列出资源管理器窗口；点击切回已打开窗口。已固定文件夹会尝试按当前文件夹路径匹配现有窗口，Windows 11 非当前标签可能需要在资源管理器中切换。回收站开关在 Dock DIY 中，开启后点击进入系统回收站。");
            ManualItem(panel,"组件开启、融合、边框与位置","在桌面组件栏分别开启时钟、天气、音乐、便签。组件的更多菜单可分离或融合到 A–D 面板。默认有框，可在外观 DIY 中选择无框；无框后右键可恢复边框。拖动标题或把手移动、拖动右下角调整尺寸。锁定后不能拖动，右键菜单仍可操作。");
            ManualItem(panel,"开机启动和更新","通用中配置开机启动与启动时机。新版本从主页或通用的 GitHub 仓库入口下载，完整解压到新目录后运行；同一 Windows 账户会读取原配置。软件不再自动检查、下载或替换程序。请先退出旧版，避免同时运行两份。");
            ManualItem(panel,"选择 UI 动效","面板与主题中分别控制展开 / 收起动画和图标悬停。展开可选择轻柔淡入、轻抬淡入、微缩淡入、侧滑淡入；悬停可选择放大、轻抬、两者组合或柔和高亮。关闭展开动画仍可保留悬停。较慢电脑建议使用轻柔淡入或关闭展开。");
            ManualGroup(panel,"常见问题与排查");
            ManualItem(panel,"重启后组件没有显示","先看桌面组件中的开关是否开启，检查是否进入游戏模式；游戏模式会暂时隐藏。可从系统托盘选择「恢复并开启全部组件」。该操作保留便签内容和布局。仍有问题时使用收集诊断，提供版本、开关状态和重启前后的录屏。请勿删除配置文件来尝试恢复。");
            ManualItem(panel,"Dock、小球或组件盖住其他软件","Dock DIY 中关闭「保持在窗口最上方」；自动隐藏与置顶已分开。小球设置和桌面组件各有独立置顶开关。默认让出全屏应用，Dock 可单独关闭此行为。需要全屏时隐藏所有玻璃窗口可启用自动游戏模式，或从托盘手动进入游戏模式。");
            ManualItem(panel,"展开卡顿、图标文字重影","优先选择轻柔淡入，或单独关闭展开动画；悬停可以继续开启。大面板已取消屏幕截图底纹与逐图标入场，玻璃底板保持静止。若仍有残影，请提供显卡、Windows 版本、缩放比例，以及系统录屏和手机视频，便于区分实际绘制与拍摄拖影。");
            ManualItem(panel,"修改控制小球后其他组件不能点击","先关闭小球外观设置窗口，再尝试解锁组件位置。新版使用稳定的设置窗口所有者，并在退出设置时释放鼠标捕获。仍出现时可从系统托盘重新开启组件；请说明是否关闭了小球、切换过无框或使用过多屏。");
            ManualItem(panel,"音乐没有封面、曲名或控制无效","先在播放器中播放歌曲，启用播放器的系统媒体控制，然后选择系统音乐模式。不是所有播放器都提供完整封面与进度；本地模式需先添加音频。右键音乐 → 查看音乐读取状态，检查当前后端。可用主页或音乐菜单中的文件兼容检查，检测两个固定组件文件的完整性与下载阻止标记，只对本版校验通过的文件解除阻止。它不能解决播放器未提供媒体会话的问题。");
            ManualItem(panel,"天气一直无法读取","检查城市填写，也可填写纬度,经度；天气连接默认跟随 Windows 系统代理，可在桌面组件里手动改为直接连接。新版天气请求不复用旧连接，区分主服务与备用服务错误，并保留两条诊断。单靠 CloseWait 状态无法判断 API 或 JSON 被破坏。仍失败请发 error.log 中 WeatherClient / WeatherTransport 的完整错误，保留系统证书校验。");
            ManualItem(panel,"Wi-Fi / 蓝牙读取受限或没有适配器","读取受限与没有硬件会分别显示。确认系统中存在网卡或蓝牙适配器、驱动正常，Wi-Fi WLAN AutoConfig 服务已运行。部分 Windows 版本需要允许位置或无线权限。可从展开列表进入系统设置；企业网络、WPA3 与设备配对交给系统界面处理。不要为此关闭系统安全功能。");
            ManualItem(panel,"系统托盘图标与系统通知","单击桌面小球，再点「系统托盘」，打开 Windows 原生隐藏图标区。图标与右键菜单由 Windows 和原软件提供，已撤下小球内部的复制图标界面。手动隐藏系统任务栏时，请先恢复任务栏显示。该功能不读取其他应用内的消息，也不替换 Windows 通知中心。");
            ManualItem(panel,"删除分组后上方仍残留文件夹","新版会同步分组选择器与文件夹条，并清理拖拽预览。可先切换到全部，再重新打开面板；若仍存在，请提供分组名与拖拽 / 删除步骤。保留原配置，避免丢失定位线索。");
            ManualItem(panel,"没有更新助手、下载失败或版本不对","本版已移除自动更新与更新助手。主页中的 GitHub 按钮打开指定仓库，手动下载完整用户包即可。看到旧的更新助手报错，通常仍在运行旧程序，请检查托盘版本和程序目录。不要用新 EXE 混搭旧组件。");
            ManualItem(panel,"怎样让 AI 帮我排查或修改","音乐和天气的右键菜单、控制中心及主页提供 AI 排查信息入口。失败后先打开它，查看版本、环境和具体错误，补上复现步骤和已尝试操作，再点复制。将内容粘贴到你使用的 AI，让它先解释原因并逐步核实；需要改代码时，请使用对应版本源码，要求给出最小补丁、构建与回退步骤。修改前导出完整备份，逐项验证。界面会打开可编辑文本，由你决定分享什么信息。");
            ManualItem(panel,"备份、恢复和向作者反馈","通用中可导出或恢复完整备份，操作前保留备份文件。问题反馈请到主页二维码对应的抖音粉丝群，附版本、Windows 版本、操作步骤、截图或录屏，以及必要的诊断结果；分享前检查诊断中的个人路径和软件名称。");
        }
        static void ManualGroup(StackPanel panel,string title){panel.Children.Add(new TextBlock{Text=title,FontSize=18,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,16,0,10)});}
        static void ManualItem(StackPanel panel,string title,string text){
            panel.Children.Add(new Expander{Header=title,IsExpanded=false,Foreground=Brushes.White,Margin=new Thickness(0,0,0,10),Padding=new Thickness(8),Content=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,LineHeight=24,Margin=new Thickness(6,10,6,10)}});
        }
    }
}
