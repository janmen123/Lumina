using System;
using System.Collections.Generic;
using System.Linq;

namespace Lumina {
    // 纯逻辑自检：不创建窗口、不碰桌面，可在无界面环境里运行。
    internal static class LogicTests {
        static readonly List<string> failures=new List<string>();
        static int checks;
        static void Check(string name,Func<bool> action) {
            checks++;
            try { if(!action())failures.Add(name+"：断言不成立"); }
            catch(Exception ex) { failures.Add(name+"："+ex.GetBaseException().Message); }
        }
        static Preferences Dock() {
            var prefs=new Preferences();
            prefs.Dock=new DockOptions();
            prefs.Apps.Add(new Entry{Name="A",Path=@"C:\apps\a.exe",Pinned=true});
            prefs.Apps.Add(new Entry{Name="B",Path=@"C:\apps\b.exe",Pinned=true});
            prefs.Apps.Add(new Entry{Name="C",Path=@"C:\apps\c.exe",Pinned=false});
            return prefs;
        }
        public static int Run() {
            failures.Clear();
            checks=0;
            // 分页与搜索
            Check("分页 0 项按 1 页",()=>LayoutRules.Pages(0,10)==1);
            Check("分页 11 项按 2 页",()=>LayoutRules.Pages(11,10)==2);
            Check("搜索命中",()=>LayoutRules.Matches("Photoshop","photo"));
            Check("搜索不命中",()=>!LayoutRules.Matches("Photoshop","zzz"));
            Check("搜索空串全命中",()=>LayoutRules.Matches(null,""));
            // 数值钳制
            Check("玻璃透明度 NaN 回退 72",()=>LayoutRules.GlassOpacity(double.NaN)==72);
            Check("玻璃透明度超界钳制",()=>LayoutRules.GlassOpacity(150)==100);
            Check("Clamp 回退值",()=>LayoutRules.Clamp(double.NaN,0,100,5)==5);
            Check("Clamp 上限",()=>LayoutRules.Clamp(150,0,100,5)==100);
            // 游戏模式
            Check("手动游戏模式抑制",()=>GameModeRules.Suppressed(true,false,false,false));
            Check("自动+全屏抑制",()=>GameModeRules.Suppressed(false,true,true,false));
            Check("自动但未全屏不抑制",()=>!GameModeRules.Suppressed(false,true,false,false));
            Check("桌面可见时不全屏",()=>!GameModeRules.Suppressed(false,true,true,true));
            Check("自动模式遇到游戏也抑制",()=>GameModeRules.AutoActive(true,false,true));
            Check("自动模式没游戏没全屏不抑制",()=>!GameModeRules.AutoActive(true,false,false));
            Check("自动模式关闭时不检测游戏",()=>!GameModeRules.AutoActive(false,false,true));
            Check("Steam 库里的游戏算游戏",()=>GameDetection.IsGamePath(@"D:\Steam\steamapps\common\Counter-Strike Global Offensive\game\bin\win64\cs2.exe"));
            Check("Epic 启动器不算游戏",()=>!GameDetection.IsGamePath(@"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe"));
            Check("WeGame 库里的游戏算游戏",()=>GameDetection.IsGamePath(@"D:\WeGameApps\rail_apps\League of Legends\LeagueClient.exe"));
            Check("普通程序不算游戏",()=>!GameDetection.IsGamePath(@"C:\Program Files\DeepSeek\Lumina\Lumina.exe"));
            Check("空路径不算游戏",()=>!GameDetection.IsGamePath(null));
            // Dock 拖动阈值与点击判定
            var gesture=new DockPointerGesture("k",100,100,10,10);
            var click=gesture.Release(104,103,true,null);
            Check("未超过阈值视为点击",()=>click.Click && !click.Dragged && click.Target==null);
            var drag=new DockPointerGesture("k",100,100,10,10);
            drag.Move(130,100);
            Check("超过阈值进入拖动",()=>drag.Dragging);
            var drop=drag.Release(130,100,true,new DockDropTarget{Path="t",Before=true});
            Check("拖动松手提交目标",()=>drop.Dragged && !drop.Click && drop.Target!=null && drop.Target.Path=="t");
            var outside=new DockPointerGesture("k",100,100,10,10);
            outside.Move(130,100);
            var miss=outside.Release(130,300,false,new DockDropTarget{Path="t",Before=true});
            Check("拖出 Dock 不提交",()=>miss.Dragged && miss.Target==null && !miss.Click);
            var cancelled=new DockPointerGesture("k",100,100,10,10);
            cancelled.Cancel();
            var after=cancelled.Release(100,100,true,new DockDropTarget{Path="t",Before=true});
            Check("取消后不点击不提交",()=>!after.Click && after.Target==null);
            var invalid=new DockPointerGesture("k",100,100,10,10);
            invalid.Move(double.NaN,100);
            Check("NaN 移动不进入拖动",()=>!invalid.Dragging);
            // 间隙落点
            var gapFirst=DockDropRules.OnGap("s",new[]{"a","b","c"},new double[]{0,50,100},-10);
            var gapMiddle=DockDropRules.OnGap("s",new[]{"a","b","c"},new double[]{0,50,100},30);
            var gapLast=DockDropRules.OnGap("s",new[]{"a","b","c"},new double[]{0,50,100},140);
            Check("最左间隙插到首位",()=>gapFirst!=null && gapFirst.Path=="a" && gapFirst.Before);
            Check("中间间隙插到右侧图标前",()=>gapMiddle!=null && gapMiddle.Path=="b" && gapMiddle.Before);
            Check("最右间隙不指向图标",()=>gapLast==null || gapLast.Path==null);
            // Dock 顺序规则
            var prefs=Dock();
            Check("Dock 移动生效",()=>DockOrderRules.Move(prefs,@"C:\apps\b.exe",@"C:\apps\a.exe",true));
            Check("Dock 移动后顺序正确",()=>DockOrderRules.Ordered(prefs)[0].Path==@"C:\apps\b.exe");
            Check("Dock 合并保留应用",()=>DockLayoutRules.Common(prefs).Contains(@"C:\apps\b.exe"));
            Check("Dock 以主面板开头",()=>DockLayoutRules.Common(prefs)[0]==DockLayoutRules.Apps);
            Check("Dock 未固定应用不出现",()=>!DockLayoutRules.Common(prefs).Contains(@"C:\apps\c.exe"));
            // 铺满整屏的普通窗口：不挡内容，但指针回到底边还能弹出
            Check("独占全屏不算贴边收起",()=>!InteractionRules.DockDucks(true,true,true,false));
            Check("铺满整屏要贴边收起",()=>InteractionRules.DockDucks(true,false,true,false));
            Check("未开启让位时不收起",()=>!InteractionRules.DockDucks(false,false,true,false));
            Check("只有最大化窗口不收起",()=>!InteractionRules.DockDucks(true,false,false,false));
            Check("拖动 Dock 时不收起",()=>!InteractionRules.DockDucks(true,false,true,true));
            Check("贴边收起后底边可唤醒",()=>InteractionRules.DockSpringOpen(false,true));
            Check("不贴边时按自动隐藏设置",()=>InteractionRules.DockSpringOpen(false,false)==false&&InteractionRules.DockSpringOpen(true,false));
            Check("最大化带标题栏不算独占全屏",()=>!WindowReliabilityRules.Fullscreen(true,false,true,true,true));
            Check("底边压进 Dock 带的宽窗口要贴边收起",()=>WindowReliabilityRules.Immersive(true,false,true,true,true));
            Check("最小化的窗口不算",()=>!WindowReliabilityRules.Immersive(true,true,true,true,true));
            Check("窄窗口不算铺满",()=>!WindowReliabilityRules.Immersive(true,false,true,false,true));
            Check("没压进 Dock 带的窗口不算",()=>!WindowReliabilityRules.Immersive(true,false,false,true,true));
            Check("没最大化也没铺满的窗口不算",()=>!WindowReliabilityRules.Immersive(true,false,true,true,false));
            Check("贴边收起要连续两轮才生效",()=>InteractionRules.DuckStreak(0,true)==1&&InteractionRules.DuckStreak(1,true)==2&&!InteractionRules.DuckConfirmed(1)&&InteractionRules.DuckConfirmed(2));
            Check("判定中断就清零",()=>InteractionRules.DuckStreak(2,false)==0);
            Check("壁纸引擎不算游戏",()=>!GameDetection.IsGamePath("E:\\Steam1\\steamapps\\common\\wallpaper_engine\\bin\\wallpaper64.exe"));
            // 任务栏：隐藏时借用系统自动隐藏状态，让工作区退回整屏
            Check("自动隐藏只切最低位",()=>FeatureRules.AutoHideState(0x0u,true)==0x1u&&FeatureRules.AutoHideState(0x1u,false)==0x0u&&FeatureRules.AutoHideState(0x2u,true)==0x3u);
            // 窗口激活规则
            Check("前台未最小化则最小化",()=>WindowActivationRules.MinimizeDock(true,false));
            Check("前台已最小化则恢复",()=>!WindowActivationRules.MinimizeDock(true,true));
            Check("非前台一律激活",()=>!WindowActivationRules.MinimizeDock(false,false));
            var steamEntry=new Entry{Name="Wallpaper Engine：壁纸引擎",Path=@"C:\x\Wallpaper Engine.url",LaunchTarget="steam://rungameid/431960"};
            Check("steam 快捷方式匹配游戏窗口",()=>WindowActivationRules.HostedMatch(steamEntry,@"E:\Steam1\steamapps\common\wallpaper_engine\bin\wallpaperui.exe",null));
            Check("steam 快捷方式不匹配无关窗口",()=>!WindowActivationRules.HostedMatch(steamEntry,@"C:\Windows\explorer.exe",null));
            var exeEntry=new Entry{Name="A",Path=@"C:\apps\a.exe"};
            Check("可执行名归一化",()=>WindowActivationRules.Executables(exeEntry).Length>=1);
            // 快捷键规则
            var defaults=HotkeyRules.Defaults();
            Check("默认快捷键数量与动作一致",()=>defaults.Count==HotkeyRules.Actions.Length);
            Check("默认快捷键有效",()=>HotkeyRules.Validate(defaults)==null);
            Check("默认快捷键标签",()=>HotkeyRules.Label(defaults[0]).StartsWith("Ctrl + Alt"));
            var duplicate=HotkeyRules.Defaults();
            duplicate[1].Modifiers=duplicate[0].Modifiers;
            duplicate[1].Key=duplicate[0].Key;
            Check("重复快捷键被拒绝",()=>HotkeyRules.Validate(duplicate)!=null);
            var broken=HotkeyRules.Defaults();
            broken[0].Modifiers=99;
            Check("非法组合键被拒绝",()=>HotkeyRules.Validate(broken)!=null);
            // 时钟规则
            var morning=new DateTime(2026,1,1,11,0,0);
            var evening=new DateTime(2026,1,1,13,0,0);
            Check("未到点今晚触发",()=>ClockRules.Next(12,30,morning)==new DateTime(2026,1,1,12,30,0));
            Check("已过点顺延到明天",()=>ClockRules.Next(12,30,evening)==new DateTime(2026,1,2,12,30,0));
            var widget=new WidgetOptions{TimerDueUtc=new DateTime(2026,1,1,12,0,0)};
            Check("倒计时剩余秒数",()=>Math.Abs(ClockRules.Remaining(widget,new DateTime(2026,1,1,11,59,0))-60)<0.001);
            widget.TimerDueUtc=new DateTime(2026,1,1,10,0,0);
            Check("倒计时不会为负",()=>ClockRules.Remaining(widget,new DateTime(2026,1,1,11,0,0))==0);
            // 导入与去重
            Check("lnk 可导入",()=>EntryRules.Importable(@"C:\a\b.lnk"));
            Check("txt 不可导入",()=>!EntryRules.Importable(@"C:\a\b.zzz"));
            var existing=new List<Entry>{new Entry{Path=@"C:\a\b.lnk"}};
            Check("重复路径不重复导入",()=>EntryRules.Candidates(new[]{@"C:\a\b.lnk",@"C:\a\c.lnk"},existing).Count==1);
            // 输出
            var lines=new List<string>();
            lines.Add((failures.Count==0?"LOGIC PASS ":"LOGIC FAIL ")+checks+" 项检查，失败 "+failures.Count+" 项");
            lines.AddRange(failures.Select(f=>"FAIL "+f));
            string path=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"logic-test-result.txt");
            try { System.IO.File.WriteAllText(path,string.Join("\r\n",lines)+"\r\n"); } catch(Exception ex) { Console.WriteLine(ex.Message); }
            foreach(string line in lines)Console.WriteLine(line);
            return failures.Count==0?0:1;
        }
    }
}
