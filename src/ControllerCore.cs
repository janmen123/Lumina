using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;


namespace Lumina {
    public partial class Controller {
        public Preferences Prefs;
        public DockWindow Dock;
        public BoardWindow Board;
        public Forms.NotifyIcon Tray;
        bool importing;
        public bool ShuttingDown;
        public bool CanUndoRemoval {get{return editHistory.CanUndo;}}
        readonly bool persistConfiguration;
        DesktopAutoSync autoSync;
        public Controller():this(true) {}
        internal Controller(bool desktop) {
            persistConfiguration=desktop;
            if(!desktop){Prefs=new Preferences();Board=new BoardWindow(this);Dock=new DockWindow(this);return;}
            Prefs=Store.Load();if(Prefs.ConfigRevision<1){if(Prefs.KeepWindowSeconds<=0)Prefs.KeepWindowSeconds=3;Prefs.ConfigRevision=1;Save();}RunningWindows.KeepWindowSeconds=Prefs.KeepWindowSeconds;MigrateLegacyPaths();if(Prefs.ThemeFollowSystem)Prefs.Light=SystemTheme.IsLight();bool resetMotion=MotionPresets.ApplyInitialBaseline(Prefs);DesktopPersistence.DesktopOnly=Prefs.AllDesktopLayer;if(!string.IsNullOrEmpty(Store.RecoveryMessage))MessageBox.Show(Store.RecoveryMessage,"Lumina 配置恢复");if(Prefs.Removed.Count>0||Prefs.RemovedIdentities.Count>0)MigrateRemovalIdentities(); Board=new BoardWindow(this); Dock=new DockWindow(this);glassSuppressed=Prefs.GameMode||(Prefs.AutoGameMode&&FullscreenState.Active());
            Tray=new Forms.NotifyIcon {Text="Lumina v0.2.0 · 玻璃启动台",Icon=Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? Drawing.SystemIcons.Application,Visible=true};
            RefreshTrayMenu();
            Tray.DoubleClick+=(s,e)=>Toggle(true);
            Dock.Refresh(); if(!GlassSuppressed)Dock.Show();
            if(Prefs.HotkeyMigrationConflict){Tray.ShowBalloonTip(4500,"快捷键升级提示","原有组合与 Ctrl+Alt+H 冲突，已保留原设置并关闭新增项。可在设置中录制新组合。",Forms.ToolTipIcon.Info);Save();}
            RefreshWidgets();
            if(!Prefs.Imported) {Toggle(true);Import();}
            ApplyGameMode();if(resetMotion)Save();StartDesktopAutoSync();SystemTheme.Watch();SystemTheme.Changed+=SystemThemeChanged;
        }
        void SystemThemeChanged(){
            if(!Prefs.ThemeFollowSystem)return;
            var app=Application.Current;if(app==null||app.Dispatcher==null)return;
            app.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{
                if(ShuttingDown||!Prefs.ThemeFollowSystem)return;
                bool light=SystemTheme.IsLight();if(light==Prefs.Light)return;
                Prefs.Light=light;Save();
                try {Dock.Refresh();Board.SyncGroups();Board.Render();RefreshWidgets();} catch(Exception ex) {Store.Log(ex);}
            }));
        }
        string Shortcut(int id) {return HotkeyRules.Label(Prefs.Hotkeys.First(k=>k.ActionId==id));}
        public void RefreshTrayMenu() {
            var previous=Tray.ContextMenuStrip;var menu=new Forms.ContextMenuStrip();
            menu.Items.Add("打开应用面板   "+Shortcut(1),null,(s,e)=>Toggle(true));
            menu.Items.Add(Prefs.GameMode?"退出游戏模式":"进入游戏模式",null,(s,e)=>SetGameMode(!Prefs.GameMode));
            if(Prefs.AutoGameMode)menu.Items.Add("暂停自动游戏模式",null,(s,e)=>{Prefs.AutoGameMode=false;Prefs.GameMode=false;Save();ApplyGameMode();RefreshTrayMenu();});
            menu.Items.Add("添加软件   "+Shortcut(4),null,(s,e)=>AddFromAnywhere());
            menu.Items.Add("导入开始菜单应用",null,(s,e)=>Import());
            menu.Items.Add("外观与快捷键   "+Shortcut(5),null,(s,e)=>Settings());
            menu.Items.Add("隐藏 / 显示任务栏   "+Shortcut(2),null,(s,e)=>ShellAction(1));
            menu.Items.Add("隐藏 / 显示桌面图标   "+Shortcut(3),null,(s,e)=>ShellAction(2));
            menu.Items.Add("恢复任务栏与桌面图标   "+Shortcut(6),null,(s,e)=>ShellAction(3));
            menu.Items.Add("Windows 任务栏自动隐藏   "+Shortcut(7),null,(s,e)=>ToggleTaskbarAutoHide());
            menu.Items.Add("打开控制中心",null,(s,e)=>OpenControlCenter());menu.Items.Add("控制中心小球设置",null,(s,e)=>ControlCenterSettings(null));
            menu.Items.Add("组件布局",null,(s,e)=>ConfigureWidgets());menu.Items.Add("恢复并开启全部组件",null,(s,e)=>RecoverWidgets());foreach(string component in WidgetRules.Kinds){string kind=component;menu.Items.Add("开启组件 · "+WidgetRules.Names[Array.IndexOf(WidgetRules.Kinds,kind)],null,(s,e)=>SetWidgetEnabled(kind,true));}
            menu.Items.Add("工作区管理",null,(s,e)=>ManageWorkspaces());foreach(var work in Prefs.Workspaces){var workspace=work;menu.Items.Add("打开工作区 · "+workspace.Name,null,(s,e)=>LaunchWorkspace(workspace));}
            menu.Items.Add("外观方案",null,(s,e)=>ManageSchemes());
            menu.Items.Add("打开配置文件夹",null,(s,e)=>Process.Start(new ProcessStartInfo(Store.Folder){UseShellExecute=true}));
            menu.Items.Add(new Forms.ToolStripSeparator());menu.Items.Add("退出 Lumina",null,(s,e)=>Quit());
            Tray.ContextMenuStrip=menu;if(previous!=null)previous.Dispose();
        }
        public void Save() { if(!persistConfiguration)return;try { DockOrderRules.Normalize(Prefs);Store.Save(Prefs); } catch(Exception ex) {Store.Log(ex);MessageBox.Show("布局保存失败："+ex.Message,"Lumina");} }
        public void Refresh() {Save();Dock.Refresh();Board.SyncGroups();Board.Render();RefreshWidgets();}
        public void StartDesktopAutoSync(){if(!persistConfiguration)return;if(autoSync==null)autoSync=new DesktopAutoSync(this);autoSync.Start();}
        public void StopDesktopAutoSync(){if(autoSync!=null){autoSync.Stop();autoSync=null;}}
        public void SyncDesktopNow(){if(!persistConfiguration)return;if(autoSync==null)StartDesktopAutoSync();if(autoSync!=null)autoSync.Sync("手动");}
        public void Toggle(bool force=false) {
            if(GlassSuppressed)return;
            if(Board.IsVisible && !force) Board.CollapseSmooth(); else Board.Open();
        }
        public void Launch(Entry entry) {
            bool handled=false;
            RecentRules.Touch(Prefs,entry==null?null:entry.Path);
            try { handled=RunningWindows.Toggle(entry,Board==null?IntPtr.Zero:Board.foregroundHint); }
            catch(Exception ex) { Store.Log(ex); Store.Trace("LAUNCH toggle failed entry="+entry.Name+" error="+ex.Message); }
            if(handled) { Board.CollapseSmooth(); return; }
            try { Process.Start(new ProcessStartInfo(entry.Path){UseShellExecute=true}); Board.CollapseSmooth(); }
            catch(Exception ex) { Store.Log(ex); Store.Trace("LAUNCH start failed entry="+entry.Name+" error="+ex.Message); if(!Store.Silent)MessageBox.Show("无法打开「"+entry.Name+"」。\n入口可能已移动或卸载。\n\n"+ex.Message,"Lumina"); }
        }
        public void Add(IEnumerable<string> paths,bool manual=true,string targetGroup=null) {
            if(manual&&targetGroup==null&&Board.IsVisible)targetGroup=Board.CurrentGroup;
            if(!manual)MigrateRemovalIdentities();
            var before=BeginBoardEdit();int added=0;
            foreach(string rawPath in paths) {
                string path=EntryPathRules.Normalize(rawPath);
                if(!File.Exists(path) && !Directory.Exists(path)) continue;
                if(!LayoutRules.AllowImport(path,Prefs.Removed,manual))continue;
                var existing=Prefs.Apps.FirstOrDefault(a=>string.Equals(EntryPathRules.Normalize(a.Path),path,StringComparison.OrdinalIgnoreCase));if(existing!=null){if(manual&&targetGroup!=null&&targetGroup!="全部")added+=FeatureRules.AddShared(Prefs,new[]{existing.Path},targetGroup);continue;}
                
                var entry=new Entry {Name=EntryPathRules.Name(path),Path=path,IsFolder=Directory.Exists(path)};LaunchInfoService.Enrich(entry);if(!manual&&(!LaunchInfoService.Primary(entry)||RemovalRules.Blocked(Prefs,entry)||Prefs.Apps.Any(a=>RemovalRules.Same(a,entry))))continue;if(manual)RemovalRules.Forget(Prefs,entry);entry.Group=FeatureRules.ImportGroup(Prefs,entry,manual?targetGroup:null);Prefs.Apps.Add(entry); added++;
            }
            if(added>0){EndBoardEdit(before,"添加入口");Refresh();}
        }
        public void Choose() {
            Board.ModalBusy=true;
            try {new BatchAddWindow(this).ShowDialog();}finally{Board.ModalBusy=false;}
        }
        public void AddFromAnywhere() {Toggle(true);Choose();}
        public void Opacity(double percent) {
            Prefs.OpacityPercent=LayoutRules.GlassOpacity(percent);Board.SetGlassOpacity(Prefs.OpacityPercent.Value);
        }
        public void Remove(Entry a,string scope=null) {
            RemoveMany(new string[]{a.Path},scope);
        }
        public void RemoveMany(IEnumerable<string> paths,string scope=null) {
            string group=scope??(Board.IsVisible?Board.CurrentGroup:"全部");var targets=new HashSet<string>(paths,StringComparer.OrdinalIgnoreCase);targets.RemoveWhere(path=>!Prefs.Apps.Any(a=>string.Equals(a.Path,path,StringComparison.OrdinalIgnoreCase)&&FeatureRules.InGroup(a,group)));int count=Prefs.Apps.Count(a=>targets.Contains(a.Path));if(count==0)return;
            Board.ModalBusy=true;
            try {
                string text="从「"+group+"」移除已选的 "+count+" 个入口？\n"+(group=="全部"?"本次从所有分组与 Dock 移除，原软件和磁盘文件保留。":"同一软件在其他组中的入口保留；原软件和磁盘文件保留。")+"\n移除后可点击「撤销」。";
                var answer=Board.IsVisible?MessageBox.Show(Board,text,"Lumina",MessageBoxButton.YesNo):MessageBox.Show(text,"Lumina",MessageBoxButton.YesNo);
                if(answer==MessageBoxResult.Yes) {foreach(var entry in Prefs.Apps.Where(a=>targets.Contains(a.Path)))LaunchInfoService.Enrich(entry);var before=BeginBoardEdit();EntryRules.Remove(Prefs,targets,group);EndBoardEdit(before,"移除入口");Board.FinishSelection();Refresh();Board.SetStatus("已移除 "+count+" 个入口 · 可撤销本次移除");}
            } finally {Board.ModalBusy=false;}
        }
        public void UndoRemoval(){UndoBoard();}
        public void Pin(Entry a) {
            if(!a.Pinned && Prefs.Apps.Count(x=>x.Pinned)>=20) {MessageBox.Show("最多固定 20 个入口。请先取消一个固定。","Lumina");return;}
            var before=BeginBoardEdit();a.Pinned=!a.Pinned;EndBoardEdit(before,"调整 Dock 固定");Refresh();
        }
        static IEnumerable<string> Shortcuts(string root) {
            if(!Directory.Exists(root)) yield break;
            var pending=new Stack<string>();pending.Push(root);int count=0;
            while(pending.Count>0 && count<2500) {
                string folder=pending.Pop();string[] files=null,dirs=null;
                try { files=Directory.GetFiles(folder);dirs=Directory.GetDirectories(folder); } catch {continue;}
                foreach(string f in files) {string e=System.IO.Path.GetExtension(f).ToLowerInvariant();if((e==".lnk"||e==".appref-ms"||e==".url")&&StartupEntryRules.Candidate(f)) {count++;yield return f;}}
                foreach(string d in dirs) {try {if((File.GetAttributes(d)&FileAttributes.ReparsePoint)==0) pending.Push(d);}catch {}}
            }
        }
        public async void Import() {
            if(importing)return; importing=true;Board.SetStatus("正在读取开始菜单…");
            try {
                var paths=await Task.Run(()=>Shortcuts(Environment.GetFolderPath(Environment.SpecialFolder.Programs)).Concat(Shortcuts(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms))).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p=>System.IO.Path.GetFileNameWithoutExtension(p),StringComparer.CurrentCultureIgnoreCase).ToArray());
                if(ShuttingDown)return;int before=Prefs.Apps.Count;Add(paths,false);Prefs.Imported=true;
                if(!Prefs.Apps.Any(a=>a.Pinned)) {
                    string[] common={"edge","chrome","firefox","微信","wechat","qq","steam","blender","maya","剪映","capcut","code"};
                    foreach(var a in Prefs.Apps.Where(a=>common.Any(k=>a.Name.IndexOf(k,StringComparison.OrdinalIgnoreCase)>=0)).Take(7))a.Pinned=true;
                }
                Refresh();Board.SetStatus("已导入 "+(Prefs.Apps.Count-before)+" 个入口 · 可拖入其他软件");
            }catch(Exception ex) {Store.Log(ex);Board.SetStatus("导入失败，可使用右上角添加");}
            finally {importing=false;}
        }
        public ContextMenu Menu(Entry a,string scope="全部") {
            var menu=MenuTheme.Create(Prefs.Light);
            Action<string,Action> item=(title,action)=> {var mi=new MenuItem{Header=title};mi.Click+=(s,e)=>action();menu.Items.Add(mi);};
            item("打开",()=>Launch(a));item("打开文件所在位置",()=>{try{FileShellActions.Location(a);}catch(Exception ex){MessageBox.Show(ex.Message,"Lumina");}});item("文件属性",()=>{try{FileShellActions.Properties(a.Path);}catch(Exception ex){MessageBox.Show(ex.Message,"Lumina");}});item(a.Pinned?"取消 Dock 固定":"固定到 Dock",()=>Pin(a));
            if(scope=="Dock"){item("添加软件或文件到 Dock",()=>ChooseDockFiles());AddDockWindowMenus(menu,a);}
            else {
                item("前移一位",()=> {int i=Prefs.Apps.IndexOf(a);if(i>0){Prefs.Apps.RemoveAt(i);Prefs.Apps.Insert(i-1,a);Refresh();}});
                item("后移一位",()=> {int i=Prefs.Apps.IndexOf(a);if(i<Prefs.Apps.Count-1){Prefs.Apps.RemoveAt(i);Prefs.Apps.Insert(i+1,a);Refresh();}});
            }
            item("修改名称与图标",()=>Customize(a));
            var groupMenu=new MenuItem{Header="移动到分组"};foreach(var group in new string[]{"未分组"}.Concat(Prefs.Groups)){string name=group;var child=new MenuItem{Header=name};child.Click+=(s,e)=>{TransferEntries(new string[]{a.Path},scope=="Dock"?"全部":scope,name);};groupMenu.Items.Add(child);}menu.Items.Add(groupMenu);
            item("移除入口",()=>Remove(a,scope=="Dock"?"全部":scope));return menu;
        }
        internal static ToggleButton VisibilitySwitch() {
            var toggle=new ToggleButton{Width=50,Height=26,Cursor=Cursors.Hand,VerticalAlignment=VerticalAlignment.Center};
            var track=new FrameworkElementFactory(typeof(Border));track.Name="Track";track.SetValue(Border.CornerRadiusProperty,new CornerRadius(13));track.SetValue(Border.BackgroundProperty,new SolidColorBrush(Color.FromRgb(83,87,99)));
            var thumb=new FrameworkElementFactory(typeof(Border));thumb.Name="Thumb";thumb.SetValue(FrameworkElement.WidthProperty,20.0);thumb.SetValue(FrameworkElement.HeightProperty,20.0);thumb.SetValue(FrameworkElement.MarginProperty,new Thickness(3));thumb.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Left);thumb.SetValue(Border.CornerRadiusProperty,new CornerRadius(10));thumb.SetValue(Border.BackgroundProperty,Brushes.White);track.AppendChild(thumb);
            var template=new ControlTemplate(typeof(ToggleButton)){VisualTree=track};var on=new Trigger{Property=ToggleButton.IsCheckedProperty,Value=true};
            on.Setters.Add(new Setter(Border.BackgroundProperty,new SolidColorBrush(Color.FromRgb(100,162,238)),"Track"));on.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Right,"Thumb"));template.Triggers.Add(on);toggle.Template=template;return toggle;
        }
        public void Settings(int selectedPage=-1) {
            if(settingsOpen){if(settingsWindow!=null){if(selectedPage>=0&&settingsTabs!=null)settingsTabs.SelectedIndex=Math.Max(0,Math.Min(settingsTabs.Items.Count-1,selectedPage));if(settingsWindow.WindowState==WindowState.Minimized)settingsWindow.WindowState=WindowState.Normal;settingsWindow.Activate();}return;}settingsOpen=true;
            var preview=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};var stateTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(700)};
            bool shown=false,cleaned=false;
            Action cleanup=()=>{if(cleaned)return;cleaned=true;settingsOpen=false;settingsWindow=null;settingsTabs=null;preview.Stop();stateTimer.Stop();Save();try{if(!ShuttingDown){Dock.Refresh();RefreshWidgets();}}catch(Exception ex){Store.Log(ex);}};
            try {
                Board.Hide();
                var w=new Window{Title="Lumina",Width=Math.Min(940,SystemParameters.WorkArea.Width-40),Height=Math.Min(790,SystemParameters.WorkArea.Height-50),MinWidth=560,ResizeMode=ResizeMode.CanResize,WindowStartupLocation=WindowStartupLocation.CenterScreen,Owner=Dock,Topmost=true,Background=new SolidColorBrush(Color.FromRgb(28,30,38)),Foreground=Brushes.White};
                settingsWindow=w;w.Closed+=(s,e)=>cleanup();
                var area=SystemParameters.WorkArea;var vertical=SettingsPlacementRules.Vertical(area.Top,area.Height,Dock.RestingTop,Dock.Height,790);w.WindowStartupLocation=WindowStartupLocation.Manual;w.Left=area.Left+(area.Width-w.Width)/2;w.Top=vertical[0];w.Height=vertical[1];
                Polish.Apply(w,false);var root=new Grid{Margin=new Thickness(24)};root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});w.Content=root;
                var title=new StackPanel();title.Children.Add(new TextBlock{Text="把桌面，变成你的空间",FontSize=25,Margin=new Thickness(0,0,0,6)});title.Children.Add(new TextBlock{Text="LUMINA  /  个性化、组件与桌面控制",Foreground=Brushes.LightGray,Margin=new Thickness(0,0,0,18)});root.Children.Add(title);
                var tabs=new TabControl{Background=Brushes.Transparent,BorderThickness=new Thickness(0)};var settingsHost=new DockPanel();Grid.SetRow(settingsHost,1);root.Children.Add(settingsHost);settingsHost.Children.Add(tabs);
                settingsTabs=tabs;InstallSettingsSearch(title,tabs);
                Func<string,StackPanel> page=heading=>{var section=Polish.Section(heading,heading=="支持创作者"?"感谢每一份支持，让创作持续生长。":"调整会即时预览；关闭窗口保留外观与组件设置。",false);var panel=(StackPanel)section.Child;System.Windows.Documents.TextElement.SetForeground(panel,Brushes.White);var holder=new StackPanel{Margin=new Thickness(10,8,0,8)};holder.Children.Add(section);var tab=new TabItem{Header=heading,Content=new ScrollViewer{Content=holder,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}};tabs.Items.Add(tab);return panel;};
                var appearance=page("面板与主题");var dockPage=page("Dock DIY");var keysPage=page("快捷键与开关");var widgetsPage=page("桌面组件");var generalPage=page("通用");var motionPage=page("UI 特效");tabs.SelectedIndex=selectedPage<0?0:Math.Max(0,Math.Min(tabs.Items.Count-1,selectedPage));BuildSettingsNavigation(settingsHost,tabs);
                var draft=Prefs.Dock.Copy();bool loading=false;
                preview.Tick+=(s,e)=>{preview.Stop();Dock.Refresh(false);};
                Action live=()=>{if(loading)return;draft.ItemOrder=(Prefs.Dock.ItemOrder??new List<string>()).ToList();draft.RunningOrder=(Prefs.Dock.RunningOrder??new List<string>()).ToList();draft.Normalize();Prefs.Dock=draft.Copy();Save();if(!preview.IsEnabled)preview.Start();};
                Func<StackPanel,string,double,double,double,double,string,Action<double>,Slider> range=(panel,label,min,max,value,tick,unit,change)=>{
                    var caption=new TextBlock{Text=label+"  "+value.ToString("0.##")+unit,Margin=new Thickness(0,12,0,6),Foreground=Brushes.White};panel.Children.Add(caption);
                    var control=new Slider{Minimum=min,Maximum=max,Value=value,TickFrequency=tick,IsSnapToTickEnabled=true,Margin=new Thickness(0,0,5,4)};panel.Children.Add(control);
                    control.ValueChanged+=(s,e)=>{caption.Text=label+"  "+control.Value.ToString("0.##")+unit;change(control.Value);};return control;
                };
                Func<StackPanel,string,bool,Action<bool>,CheckBox> check=(panel,label,value,change)=>{var control=new CheckBox{Content=label,IsChecked=value,Foreground=Brushes.White,Margin=new Thickness(0,10,0,8)};panel.Children.Add(control);control.Checked+=(s,e)=>change(true);control.Unchecked+=(s,e)=>change(false);return control;};
                Func<StackPanel,string,string[],int,Action<int>,ComboBox> combo=(panel,label,items,value,change)=>{panel.Children.Add(new TextBlock{Text=label,Margin=new Thickness(0,12,0,6)});var control=new ComboBox{ItemsSource=items,SelectedIndex=value,Height=32};panel.Children.Add(control);control.SelectionChanged+=(s,e)=>{if(control.SelectedIndex>=0)change(control.SelectedIndex);};return control;};
                check(appearance,"使用浅色玻璃",Prefs.Light,value=>{Prefs.Light=value;if(value)Prefs.ThemeFollowSystem=false;live();});
                check(appearance,"跟随 Windows 深浅色主题",Prefs.ThemeFollowSystem,value=>{Prefs.ThemeFollowSystem=value;if(value)Prefs.Light=SystemTheme.IsLight();live();});
                check(appearance,"按最近使用排序图标",Prefs.BoardSortRecent,value=>{Prefs.BoardSortRecent=value;Save();Board.Render();});
                combo(appearance,"大面板尺寸",new string[]{"舒适 · 中号","宽阔 · 大号","铺满 · 全屏"},Prefs.PanelSize,value=>{Prefs.PanelSize=value;Prefs.Placement.PanelWidth=Prefs.Placement.PanelHeight=null;Save();});
                range(appearance,"大面板透明度",0,100,100-(Prefs.OpacityPercent??72),5,"%",value=>{Opacity(100-value);Save();});
                check(appearance,"所有组件置于桌面层（统一覆盖各自置顶）",Prefs.AllDesktopLayer,value=>{Prefs.AllDesktopLayer=value;DesktopPersistence.SetDesktopOnly(value);Save();});
                appearance.Children.Add(new TextBlock{Text="开启后 Dock、小球、大面板及组件保持在普通应用后方；关闭后恢复各自置顶设置。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray});
                motionPage.Children.Add(FeatureUi.Button("一键关闭过渡动效（流畅优先）",()=>{MotionPresets.DisableTransitions(Prefs);draft=Prefs.Dock.Copy();Save();Dock.Refresh(false);RefreshWidgets();RefreshControlCenter();w.Close();}));
                motionPage.Children.Add(new TextBlock{Text="停用展开、翻页、缩放悬停和弹簧过渡，保留即时高亮与点击反馈。可重新开启下方选项。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,0,0,10)});
                check(motionPage,"大玻璃板翻页动效（独立开关）",Prefs.BoardPageMotion,value=>{Prefs.BoardPageMotion=value;Save();});
                combo(motionPage,"翻页动效样式",new[]{"轻柔淡入","水平滑入","微缩淡入"},Array.IndexOf(new[]{"fade","slide","scale"},Prefs.BoardPageStyle),value=>{Prefs.BoardPageStyle=new[]{"fade","slide","scale"}[value];Save();});
                check(motionPage,"展开 / 收起动画",draft.OpenMotion,value=>{draft.OpeningAnimation=value;live();});
                combo(motionPage,"展开动效样式",InteractionRules.OpenNames,Array.IndexOf(InteractionRules.OpenStyles,draft.OpenAnimationStyle),value=>{draft.OpenAnimationStyle=InteractionRules.OpenStyles[value];live();});
                check(motionPage,"图标悬停动效（独立开关）",draft.HoverMotion,value=>{draft.HoverAnimation=value;live();});
                combo(motionPage,"图标悬停样式",InteractionRules.HoverNames,Array.IndexOf(InteractionRules.HoverStyles,draft.HoverAnimationStyle),value=>{draft.HoverAnimationStyle=InteractionRules.HoverStyles[value];live();});
                range(motionPage,"动效强度",.5,1.5,draft.MotionLevel,.1," 倍",value=>{draft.MotionLevel=value;live();});
                BuildReflectionEditor(motionPage,()=>draft,live);
                check(motionPage,"图标拖动动效（放大跟手、实时让位）",draft.DragMotion,value=>{draft.DragMotion=value;live();});
                motionPage.Children.Add(new TextBlock{Text="拖动时放大跟手、相邻图标平滑让位；关闭动效仍保留基础预览。文件可拖到支持附件或文件接收的软件。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,2,0,8)});
                range(appearance,"玻璃模糊",0,40,draft.BlurRadius,2,"",value=>{draft.BlurRadius=value;live();});range(appearance,"边缘高光",.2,2,draft.EdgeStrength,.1," 倍",value=>{draft.EdgeStrength=value;live();});
                check(appearance,"大面板置顶（关闭时保持桌面层）",Prefs.BoardAlwaysOnTop,value=>{Prefs.BoardAlwaysOnTop=value;Board.Topmost=value;Board.RefreshLayer();Save();});
                check(appearance,"锁定大面板位置和尺寸",Prefs.Placement.PanelLocked,value=>{Prefs.Placement.PanelLocked=value;Save();});
                check(appearance,"点击外部自动收起大面板（关闭时方便从桌面拖入文件）",Prefs.BoardDismissOnDeactivate,value=>{Prefs.BoardDismissOnDeactivate=value;Save();});
                check(appearance,"新增软件自动识别分组",Prefs.AutoGrouping,value=>{Prefs.AutoGrouping=value;Save();});
                check(appearance,"隐藏大面板搜索栏",Prefs.BoardHideSearch,value=>{Prefs.BoardHideSearch=value;Save();Board.ApplyDisplayOptions();});
                check(appearance,"隐藏大面板分组栏，直接显示全部图标",Prefs.BoardHideGroups,value=>{Prefs.BoardHideGroups=value;Save();Board.ApplyDisplayOptions();});
                check(appearance,"显示快捷方式箭头角标（大面板与 Dock）",draft.ShowShortcutOverlays,value=>{draft.ShowShortcutOverlays=value;Native.ClearIconCache();live();Board.RefreshIcons();Save();});
                check(appearance,"高清图标（大面板与 Dock，默认关闭）",draft.SharperIcons,value=>{draft.SharperIcons=value;Native.ClearIconCache();live();preview.Stop();Dock.Refresh(false);Board.RefreshIcons();Save();});
                appearance.Children.Add(new TextBlock{Text="优先读取软件自带的高分辨率图标；仅在加载时处理，不新增后台任务。关闭后清除图标缓存。原始图标像素低时效果有限。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,2,0,8)});
                BuildMaterialEditor(appearance,"大面板玻璃材质 DIY",Prefs.BoardMaterial,()=>{Prefs.BoardMaterial.Normalize();Board.SetMaterial(Prefs.BoardMaterial);Save();});
                var manage=new WrapPanel{Margin=new Thickness(0,12,0,0)};appearance.Children.Add(manage);manage.Children.Add(FeatureUi.Button("外观方案",()=>{Save();if(ManageSchemes(w)){draft=Prefs.Dock.Copy();w.Close();}}));manage.Children.Add(FeatureUi.Button("管理分组",()=>ManageGroups(w)));manage.Children.Add(FeatureUi.Button("创作工作区",()=>ManageWorkspaces(w)));manage.Children.Add(FeatureUi.Button("自动分组未分组软件",()=>AutoGroupApps(w)));
                manage.Children.Add(FeatureUi.Button("重置面板位置和尺寸",()=>{Prefs.Placement.PanelLeft=Prefs.Placement.PanelTop=Prefs.Placement.PanelWidth=Prefs.Placement.PanelHeight=null;Save();}));
                appearance.Children.Add(new TextBlock{Text="展开与悬停分别设置。轻柔淡入负担较低；微缩和轻抬仅作用于内容，玻璃底板保持稳定。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,12,0,0)});
                dockPage.Children.Add(new TextBlock{Text="独立定制下方玻璃板",FontSize=18,Foreground=Brushes.White});
                BuildMaterialEditor(dockPage,"Dock 玻璃材质 DIY",draft.Material,()=>{live();});
                var presets=combo(dockPage,"一键风格",new string[]{"自定义","紧凑","均衡","宽松"},0,value=>{});
                var icon=range(dockPage,"图标尺寸",32,64,draft.IconSize,2," px",value=>{draft.IconSize=value;live();});
                var spacing=range(dockPage,"图标间距",0,20,draft.Spacing,1," px",value=>{draft.Spacing=value;live();});
                var padding=range(dockPage,"玻璃板左右留白",8,28,draft.Padding,1," px",value=>{draft.Padding=value;live();});
                range(dockPage,"玻璃板上下最小留白",0,28,draft.VerticalPadding,1," px",value=>{draft.VerticalPadding=value;live();});
                dockPage.Children.Add(new TextBlock{Text="悬停放大时自动保留必要空间，避免图标和倒影被裁切。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,2,0,8)});
                var radius=range(dockPage,"圆角",0,48,draft.Radius,2," px",value=>{draft.Radius=value;live();});
                var opacity=range(dockPage,"Dock 透明度",0,100,100-draft.OpacityPercent,5,"%",value=>{draft.OpacityPercent=100-value;live();});
                var zoom=range(dockPage,"悬停放大",1,1.6,draft.Zoom,.05," 倍",value=>{draft.Zoom=value;live();});
                var labels=check(dockPage,"显示图标名称",draft.ShowLabels,value=>{draft.ShowLabels=value;live();});
                check(dockPage,"显示此电脑",draft.ShowComputer,value=>{draft.ShowComputer=value;live();});
                check(dockPage,"显示回收站",draft.ShowRecycleBin,value=>{draft.ShowRecycleBin=value;live();});
                check(dockPage,"全屏应用时让出显示层级",draft.RespectFullscreen,value=>{draft.RespectFullscreen=value;live();});
                check(dockPage,"显示设置图标",draft.ShowSettings,value=>{draft.ShowSettings=value;live();});
                check(dockPage,"显示电源快捷菜单",draft.ShowPower,value=>{draft.ShowPower=value;live();});
                check(dockPage,"Dock 保留控制中心快捷入口",draft.ShowNetwork,value=>{draft.ShowNetwork=value;live();});
                combo(dockPage,"此电脑图标样式",new string[]{"渐变徽章","简洁线框"},draft.ComputerStyle,value=>{draft.ComputerStyle=value;live();});
                combo(dockPage,"强调色",new string[]{"晴空蓝","雾紫","薄荷绿","暖金","玫瑰粉","月光灰"},draft.Accent,value=>{draft.Accent=value;live();});
                check(dockPage,"显示分隔线",draft.ShowSeparator,value=>{draft.ShowSeparator=value;live();});
                check(dockPage,"Dock 保持在窗口最上方",draft.AlwaysOnTop,value=>{draft.AlwaysOnTop=value;live();});
                check(dockPage,"显示软件运行状态",draft.ShowRunning,value=>{draft.ShowRunning=value;live();});
                check(dockPage,"Dock 中显示正在运行但未固定的软件",draft.ShowRunningSection,value=>{draft.ShowRunningSection=value;live();});
                check(dockPage,"Dock 自动隐藏，靠近所在屏幕底边唤出",draft.AutoHide,value=>{draft.AutoHide=value;live();});range(dockPage,"自动隐藏延迟",200,3000,draft.HideDelay,100," ms",value=>{draft.HideDelay=value;live();});range(dockPage,"底边触发范围",3,48,draft.EdgeRange,1," px",value=>{draft.EdgeRange=value;live();});
                check(dockPage,"弹簧弹出效果（也可右键 Dock 切换）",draft.SpringReveal,value=>{draft.SpringReveal=value;live();});
                check(dockPage,"锁定 Dock 位置",Prefs.Placement.DockLocked,value=>{Prefs.Placement.DockLocked=value;live();});
                dockPage.Children.Add(FeatureUi.Button("重置 Dock 位置",()=>{Prefs.Placement.DockCenter=Prefs.Placement.DockLeft=Prefs.Placement.DockTop=null;live();}));
                combo(dockPage,"水平位置",new string[]{"靠左","居中","靠右"},draft.Alignment,value=>{draft.Alignment=value;Prefs.Placement.DockCenter=Prefs.Placement.DockLeft=null;live();});
                range(dockPage,"左右微调",-500,500,draft.Offset,10," px",value=>{draft.Offset=value;Prefs.Placement.DockCenter=Prefs.Placement.DockLeft=null;live();});
                check(dockPage,"允许贴近屏幕底边（可进入任务栏区域）",draft.UseScreenBounds,value=>{draft.UseScreenBounds=value;Prefs.Placement.DockTop=null;live();});
                range(dockPage,"底部间距（玻璃边框到所选区域底边）",0,160,Prefs.DockGap,2," px",value=>{Prefs.DockGap=value;Prefs.Placement.DockTop=null;live();});
                range(dockPage,"固定应用显示数量",3,20,draft.MaxPinned,1," 个",value=>{draft.MaxPinned=(int)value;live();});
                dockPage.Children.Add(new TextBlock{Text="可固定 20 个入口。数量超限或屏幕放不下时，其余入口收进「更多」。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,12,0,0)});
                presets.SelectionChanged+=(s,e)=>{if(presets.SelectedIndex<1)return;int style=presets.SelectedIndex;loading=true;icon.Value=style==1?34:style==3?56:44;spacing.Value=style==1?2:style==3?10:6;padding.Value=style==1?10:style==3?22:16;radius.Value=style==1?20:style==3?36:28;opacity.Value=style==1?35:style==3?22:28;zoom.Value=style==1?1.1:style==3?1.3:1.2;labels.IsChecked=style==3;loading=false;live();};
                keysPage.Children.Add(new TextBlock{Text="快捷键与桌面开关",FontSize=18});
                keysPage.Children.Add(new TextBlock{Text="可点击录制直接按组合键，保存后生效。右侧开关即时操作。\n隐藏开关：开启隐藏，关闭显示；自动开关：Windows 任务栏自动隐藏。",Foreground=Brushes.LightGray,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,18)});
                var keyChoices=HotkeyRules.Keys();var controls=new Dictionary<int,Tuple<CheckBox,ComboBox,ComboBox>>();var switches=new Dictionary<int,ToggleButton>();var info=new Dictionary<int,TextBlock>();bool syncing=false;
                foreach(var binding in Prefs.Hotkeys.OrderBy(k=>k.ActionId)) {
                    int id=binding.ActionId;var group=new StackPanel{Margin=new Thickness(0,0,0,14)};keysPage.Children.Add(group);
                    var row=new Grid();foreach(double width in new double[]{1.7,1.7,.8,1.1})row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(width,GridUnitType.Star)});row.ColumnDefinitions.Insert(3,new ColumnDefinition{Width=new GridLength(64)});group.Children.Add(row);
                    var enabled=new CheckBox{Content=new TextBlock{Text=HotkeyRules.Actions[id-1],TextWrapping=TextWrapping.Wrap},IsChecked=binding.Enabled,Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)};row.Children.Add(enabled);
                    var modifiers=new ComboBox{ItemsSource=HotkeyRules.ModifierNames,SelectedIndex=Array.IndexOf(HotkeyRules.ModifierValues,binding.Modifiers),Height=32,Margin=new Thickness(0,0,8,0),IsEnabled=binding.Enabled};Grid.SetColumn(modifiers,1);row.Children.Add(modifiers);
                    var key=new ComboBox{ItemsSource=keyChoices.Keys.ToList(),SelectedItem=keyChoices.First(k=>k.Value==binding.Key).Key,Height=32,Margin=new Thickness(0,0,8,0),IsEnabled=binding.Enabled};Grid.SetColumn(key,2);row.Children.Add(key);
                    enabled.Checked+=(s,e)=>{modifiers.IsEnabled=true;key.IsEnabled=true;};enabled.Unchecked+=(s,e)=>{modifiers.IsEnabled=false;key.IsEnabled=false;};controls[id]=Tuple.Create(enabled,modifiers,key);
                    var record=new Button{Content="录制",Height=32,Margin=new Thickness(0,0,8,0)};Grid.SetColumn(record,3);row.Children.Add(record);record.Click+=(s,e)=>{var active=Dock.SuspendHotkeys();HotkeySetting captured;try{captured=FeatureUi.Record(w,id);}finally{Dock.ResumeHotkeys(active);}if(captured!=null){enabled.IsChecked=true;modifiers.SelectedIndex=Array.IndexOf(HotkeyRules.ModifierValues,captured.Modifiers);key.SelectedItem=keyChoices.First(x=>x.Value==captured.Key).Key;}};
                    if(id==2||id==3||id==7) {
                        var wrap=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(wrap,4);row.Children.Add(wrap);
                        var toggle=VisibilitySwitch();if(id==7){try{toggle.IsChecked=TaskbarAutoHide.Enabled;}catch{toggle.IsChecked=false;}}else toggle.IsChecked=id==3?cleanDesktopActive:DesktopVisibility.DesiredHidden(true);toggle.ToolTip=id==7?"开启 Windows 任务栏自动隐藏":"开启隐藏，关闭显示";wrap.Children.Add(toggle);wrap.Children.Add(new TextBlock{Text=id==7?"自动":"隐藏",Margin=new Thickness(6,0,0,0),VerticalAlignment=VerticalAlignment.Center,Foreground=Brushes.White});switches[id]=toggle;
                        toggle.Checked+=(s,e)=>{if(!syncing){if(id==7)ToggleTaskbarAutoHide(true);else ShellAction(id-1,true);}};toggle.Unchecked+=(s,e)=>{if(!syncing){if(id==7)ToggleTaskbarAutoHide(false);else ShellAction(id-1,false);}};
                    }
                    var status=new TextBlock{Foreground=Brushes.LightGray,FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)};group.Children.Add(status);info[id]=status;
                }
                Action updateState=()=>{syncing=true;foreach(var k in Prefs.Hotkeys){string state=!k.Enabled?"快捷键已关闭":Dock.HotkeyActive(k.ActionId)?"快捷键已注册":"快捷键未注册，请换组合或重新注册";if(k.ActionId==2||k.ActionId==3){bool taskbar=k.ActionId==2;bool? hidden=DesktopVisibility.IsHidden(taskbar);state+=" · 实际状态："+(!hidden.HasValue?"未找到系统窗口":hidden.Value?"隐藏":"显示");switches[k.ActionId].IsChecked=taskbar?DesktopVisibility.DesiredHidden(true):cleanDesktopActive;}if(k.ActionId==7){try{bool auto=TaskbarAutoHide.Enabled;switches[7].IsChecked=auto;state+=" · 自动隐藏："+(auto?"开启":"关闭");}catch{state+=" · 未找到 Windows 任务栏";}}info[k.ActionId].Text=state;}syncing=false;};
                stateTimer.Tick+=(s,e)=>updateState();updateState();stateTimer.Start();
                var actions=new WrapPanel();keysPage.Children.Add(actions);
                var reset=new Button{Content="恢复默认快捷键",Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,8,6)};actions.Children.Add(reset);reset.Click+=(s,e)=>{foreach(var k in HotkeyRules.Defaults()){var row=controls[k.ActionId];row.Item1.IsChecked=true;row.Item2.SelectedIndex=Array.IndexOf(HotkeyRules.ModifierValues,k.Modifiers);row.Item3.SelectedItem=keyChoices.First(x=>x.Value==k.Key).Key;}};
                var retry=new Button{Content="重新注册当前快捷键",Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,8,6)};actions.Children.Add(retry);retry.Click+=(s,e)=>{MessageBox.Show(w,Dock.RetryHotkeys(),"快捷键注册结果");updateState();};
                var restore=new Button{Content="显示任务栏与桌面图标",Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,8,6)};actions.Children.Add(restore);restore.Click+=(s,e)=>{ShellAction(3);updateState();};
                keysPage.Children.Add(new TextBlock{Text="隐藏状态仅在本次运行中维持；正常退出恢复启动前状态。组合键占用不会阻止直接点击开关。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,12,0,0)});
                widgetsPage.Children.Add(new TextBlock{Text="可选组件 · 默认关闭",FontSize=18});
                widgetsPage.Children.Add(FeatureUi.Button("控制中心 · 开启小球",()=>OpenControlCenter()));widgetsPage.Children.Add(FeatureUi.Button("控制中心 · 小球设置",()=>ControlCenterSettings(w)));
                widgetsPage.Children.Add(FeatureUi.Button("组件外观 DIY · 颜色 / 无框 / 时钟大小",()=>CustomizeWidgets(null,w)));
                var routes=new Dictionary<string,ComboBox>();var widgetChecks=new Dictionary<string,ToggleButton>();bool routeSync=false;
                Action syncRoutes=()=>{routeSync=true;foreach(var item in routes)item.Value.SelectedIndex=WidgetRules.Panel(Prefs.Widgets,item.Key);routeSync=false;};
                var presetsRow=new WrapPanel{Margin=new Thickness(0,6,0,14)};widgetsPage.Children.Add(presetsRow);
                
                
                widgetsPage.Children.Add(new TextBlock{Text="组件选择同一个面板会融合，选择不同面板会分离。也可点击组件右上角 ••• 调整。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,0,0,12)});
                foreach(string component in WidgetRules.Kinds){string kind=component;int index=Array.IndexOf(WidgetRules.Kinds,kind);var row=new Grid{Margin=new Thickness(0,4,0,9)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(132)});var controlsRow=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};row.Children.Add(controlsRow);var enable=VisibilitySwitch();enable.IsChecked=WidgetRules.Enabled(Prefs.Widgets,kind);enable.ToolTip="开启 / 关闭"+WidgetRules.Names[index];controlsRow.Children.Add(enable);controlsRow.Children.Add(new TextBlock{Text=WidgetRules.Names[index],Margin=new Thickness(10,0,0,0),Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center});widgetChecks[kind]=enable;enable.Click+=(s,e)=>{bool desired=enable.IsChecked==true;SetWidgetEnabled(kind,desired);};var route=new ComboBox{ItemsSource=new string[]{"面板 A","面板 B","面板 C","面板 D"},SelectedIndex=WidgetRules.Panel(Prefs.Widgets,kind),Height=34};Grid.SetColumn(route,1);row.Children.Add(route);routes[kind]=route;route.SelectionChanged+=(s,e)=>{if(routeSync||route.SelectedIndex<0)return;WidgetRules.Assign(Prefs.Widgets,kind,route.SelectedIndex);RefreshWidgets();Save();};widgetsPage.Children.Add(row);}
                var islandRow=new Grid{Margin=new Thickness(0,4,0,9)};var islandControls=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};islandRow.Children.Add(islandControls);var islandCheck=VisibilitySwitch();islandCheck.IsChecked=Prefs.Widgets.IslandEnabled;islandCheck.ToolTip="开启 / 关闭灵动岛";islandControls.Children.Add(islandCheck);islandControls.Children.Add(new TextBlock{Text="灵动岛",Margin=new Thickness(10,0,0,0),Foreground=Brushes.White,VerticalAlignment=VerticalAlignment.Center});islandCheck.Click+=(s,e)=>{Prefs.Widgets.IslandEnabled=islandCheck.IsChecked==true;RefreshWidgets();Save();};widgetsPage.Children.Add(islandRow);
                var islandSettings=new Expander{Header="灵动岛外观设置",Foreground=Brushes.White,Margin=new Thickness(0,4,0,12)};var islandOptions=new StackPanel{Margin=new Thickness(0,4,0,4)};islandSettings.Content=islandOptions;widgetsPage.Children.Add(islandSettings);
                combo(islandOptions,"样式",new[]{"纯黑胶囊","透明玻璃"},Prefs.Widgets.IslandGlass?1:0,value=>{Prefs.Widgets.IslandGlass=value==1;RefreshWidgets();Save();});
                range(islandOptions,"背景透明度（100% 全透明，玻璃样式）",0,100,Prefs.Widgets.IslandTransparency??30,5,"%",value=>{Prefs.Widgets.IslandTransparency=value;RefreshWidgets();Save();});
                check(islandOptions,"展开动效",Prefs.Widgets.IslandMotion??true,value=>{Prefs.Widgets.IslandMotion=value;RefreshWidgets();Save();});
                var componentStatus=new TextBlock{Text="组件开关会自动保存，可随时关闭或重新开启。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,6,0,10)};widgetsPage.Children.Add(componentStatus);
                stateTimer.Tick+=(s,e)=>{if(islandCheck.IsChecked!=Prefs.Widgets.IslandEnabled)islandCheck.IsChecked=Prefs.Widgets.IslandEnabled;componentStatus.Text=string.IsNullOrEmpty(WidgetStatus)?"组件开关会自动保存，可随时关闭或重新开启。":WidgetStatus;routeSync=true;try{foreach(var item in widgetChecks){bool enabled=WidgetRules.Enabled(Prefs.Widgets,item.Key);if(item.Value.IsChecked!=enabled)item.Value.IsChecked=enabled;}}finally{routeSync=false;}};
                widgetsPage.Children.Add(FeatureUi.Button("开启全部组件",()=>RecoverWidgets()));widgetsPage.Children.Add(FeatureUi.Button("重新打开已启用组件",()=>ReopenWidgets()));

                check(widgetsPage,"组件始终置顶",Prefs.Widgets.Topmost,value=>{Prefs.Widgets.Topmost=value;RefreshWidgets();});
                check(widgetsPage,"锁定组件位置",Prefs.Widgets.Locked,value=>{Prefs.Widgets.Locked=value;RefreshWidgets();});
                
                
                check(widgetsPage,"隐藏小组件右上角关闭和更多按钮",Prefs.Widgets.HideControls,value=>{Prefs.Widgets.HideControls=value;foreach(var position in Prefs.Widgets.Panels)position.HideControls=null;RefreshWidgets();Save();});
                range(widgetsPage,"组件背景透明度（100% 全透明）",0,100,100-Prefs.Widgets.OpacityPercent,5,"%",value=>{Prefs.Widgets.OpacityPercent=100-value;foreach(var panel in Prefs.Widgets.Panels)panel.OpacityPercent=null;RefreshWidgets();Save();});
                
                
                BuildMaterialEditor(widgetsPage,"小组件玻璃材质 DIY",Prefs.Widgets.Material,()=>{RefreshWidgets();Save();});
                widgetsPage.Children.Add(FeatureUi.Button("重置全部组件位置",()=>{Prefs.Widgets.Panels.Clear();Prefs.Placement.WidgetLeft=Prefs.Placement.WidgetTop=null;RefreshWidgets();Save();}));
                bool startup=false;string activeTiming=Prefs.StartupTiming;try{startup=StartupService.Enabled;if(startup)activeTiming=StartupService.Mode;}catch(Exception ex){Store.Log(ex);}
                var startupCheck=new CheckBox{Content="登录 Windows 后自动启动 Lumina",IsChecked=startup,Foreground=Brushes.White,Margin=new Thickness(0,10,0,12)};generalPage.Children.Add(startupCheck);
                generalPage.Children.Add(new TextBlock{Text="自启动优先级",Margin=new Thickness(0,4,0,8)});var startupMode=new ComboBox{ItemsSource=StartupRules.Names,SelectedIndex=Array.IndexOf(StartupRules.Modes,StartupRules.Mode(activeTiming)),MinHeight=36};generalPage.Children.Add(startupMode);
                var startupInfo=new TextBlock{Foreground=Brushes.LightGray,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,16)};generalPage.Children.Add(startupInfo);bool startupSync=false;
                Action startupState=()=>{try{bool enabled=StartupService.Enabled;startupInfo.Text=enabled?"已开启："+StartupRules.Names[Array.IndexOf(StartupRules.Modes,StartupService.Mode)]+"。移动程序后请重新应用启动设置。":"未开启；选择优先级会保存，下次开启时生效。";}catch(Exception ex){startupInfo.Text="无法读取启动设置："+ex.GetBaseException().Message;}};
                Action<bool> setStartup=value=>{if(startupSync)return;try{string mode=StartupRules.Modes[Math.Max(0,startupMode.SelectedIndex)];StartupService.Set(value,mode);Prefs.StartupTiming=mode;Save();startupState();}catch(Exception ex){startupSync=true;try{startupCheck.IsChecked=StartupService.Enabled;if(startupCheck.IsChecked==true)startupMode.SelectedIndex=Array.IndexOf(StartupRules.Modes,StartupService.Mode);}catch{startupCheck.IsChecked=false;}startupSync=false;MessageBox.Show(w,ex.GetBaseException().Message,"无法更新开机启动");startupState();}};
                startupCheck.Checked+=(s,e)=>setStartup(true);startupCheck.Unchecked+=(s,e)=>setStartup(false);
                startupMode.SelectionChanged+=(s,e)=>{if(startupSync||startupMode.SelectedIndex<0)return;if(startupCheck.IsChecked==true)setStartup(true);else{Prefs.StartupTiming=StartupRules.Modes[startupMode.SelectedIndex];Save();}};startupState();
                generalPage.Children.Add(new TextBlock{Text="高：当前用户登录后立即触发计划任务。普通：由 Windows 标准启动项调度。低：登录后延迟 60 秒。Windows 不提供跨软件的严格启动排序；此处控制启动触发方式与延迟。关闭开关会移除本软件的两种启动项。",Foreground=Brushes.LightGray,TextWrapping=TextWrapping.Wrap});
                check(generalPage,"自动收录桌面上的文件夹与文档",Prefs.DesktopAutoSync,value=>{Prefs.DesktopAutoSync=value;if(value){StartDesktopAutoSync();SyncDesktopNow();Board.SetStatus("已开启桌面自动收录");}else{StopDesktopAutoSync();Board.SetStatus("已关闭桌面自动收录，已收录的入口会保留");}Save();});
                generalPage.Children.Add(new TextBlock{Text="文件夹进入「"+Prefs.DesktopFolderGroup+"」分组；PDF、Word、Excel 与其他文档进入「"+Prefs.DesktopDocsGroup+"」分组，打开该分组后可在顶部按类型筛选。只读取桌面第一层（含公共桌面）；桌面上的项目被删除或移走后入口会自动移除；手动移动到其他分组的入口不会被挪回。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,4,0,8)});
                generalPage.Children.Add(FeatureUi.Button("立即同步桌面",()=>{SyncDesktopNow();}));
                BuildGameSettings(generalPage);
                range(generalPage,"提权或自动最小化的程序点击后保持窗口（秒，0 关闭）",0,10,Prefs.KeepWindowSeconds,1," 秒",value=>{Prefs.KeepWindowSeconds=(int)value;RunningWindows.KeepWindowSeconds=(int)value;Save();});
                generalPage.Children.Add(new TextBlock{Text="部分以管理员权限运行的程序（例如 5E）恢复后会自动最小化；保持窗口会在这几秒内重新拉起它。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,4,0,10)});
                generalPage.Children.Add(new TextBlock{Text="管理员权限",FontSize=18,Margin=new Thickness(0,22,0,10)});
                generalPage.Children.Add(new TextBlock{Text=AdminService.IsElevated()?"当前以管理员权限运行，可以直接读取提权程序的窗口路径、缩略图与图标。":"当前以普通权限运行：以管理员权限运行的程序，其缩略图、拖拽吸附与前台激活会受限。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,4,0,10)});
                generalPage.Children.Add(FeatureUi.Button("以管理员身份重启 Lumina",()=>RestartElevated()));
                check(generalPage,"记录诊断日志（排查问题时开启）",Store.Diagnostics,value=>{Store.Diagnostics=value;});
                generalPage.Children.Add(new TextBlock{Text="关闭时 session.log 只记录启动、保存与错误；开启后会把 Dock 点击、窗口切换、组件刷新等细节写进 session.log。日志超过 1 MB 自动轮转，最多保留 5 份。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new Thickness(0,4,0,10)});
                BuildUpdateSettings(generalPage,w);generalPage.Children.Add(FeatureUi.Button("找回旧版入口与分组",()=>RecoverHistoricalEntries(w)));
                generalPage.Children.Add(new TextBlock{Text="完整备份与恢复",FontSize=18,Margin=new Thickness(0,22,0,10)});generalPage.Children.Add(new TextBlock{Text="备份布局、分组、快捷键、组件、便签、外观方案与自定义图标。软件与音乐文件保留在原位置；开机启动按当前电脑设置保留。",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray});var backups=new WrapPanel{Margin=new Thickness(0,12,0,6)};backups.Children.Add(FeatureUi.Button("导出完整备份",()=>ExportFullBackup()));backups.Children.Add(FeatureUi.Button("恢复完整备份",()=>{w.Close();Dock.Dispatcher.BeginInvoke(new Action(()=>RestoreFullBackup()));}));generalPage.Children.Add(backups);
                var apply=new Button{Content="保存快捷键并完成",Height=42,Background=Polish.Color("#416AA9"),Foreground=Brushes.White,Margin=new Thickness(0,16,0,0)};Grid.SetRow(apply,2);root.Children.Add(apply);
                apply.Click+=(s,e)=>{var candidate=controls.Select(row=>new HotkeySetting{ActionId=row.Key,Enabled=row.Value.Item1.IsChecked==true,Modifiers=HotkeyRules.ModifierValues[row.Value.Item2.SelectedIndex],Key=keyChoices[(string)row.Value.Item3.SelectedItem]}).OrderBy(k=>k.ActionId).ToList();string error;
                    if(!HotkeyRules.Same(Prefs.Hotkeys,candidate)&&!Dock.ApplyHotkeys(candidate,out error)){MessageBox.Show(w,error,"快捷键未保存");return;}
                    Prefs.Hotkeys=candidate.Select(k=>k.Copy()).ToList();Save();RefreshTrayMenu();w.Close();};
                w.Show();shown=true;
            }finally{if(!shown){if(settingsWindow!=null)settingsWindow.Close();cleanup();}}
        }
        bool settingsOpen;Window settingsWindow;TabControl settingsTabs;
        bool cleanDesktopActive;
        bool iconsBeforeClean;
        public bool CleanDesktopActive {get{return cleanDesktopActive;}}
        void SetCleanDesktop(bool hide){
            if(hide==cleanDesktopActive)return;
            if(hide){
                iconsBeforeClean=DesktopVisibility.IsHidden(false)??false;
                DesktopVisibility.SetHidden(false,true);
                try{DesktopVisibility.SetDeskGoHidden(true);}catch{DesktopVisibility.SetHidden(false,iconsBeforeClean);throw;}
                cleanDesktopActive=true;
                if(widgets!=null)widgets.SetVisible(false);ControlCenterVisibility(false);
            }else{
                DesktopVisibility.SetDeskGoHidden(false);DesktopVisibility.SetHidden(false,iconsBeforeClean);cleanDesktopActive=false;
                if(widgets!=null)widgets.Sync();RefreshControlCenter();
            }
        }
        public void ShellAction(int action,bool? hidden=null) {
            try {
                string message;
                if(action==1){bool next=hidden??!DesktopVisibility.DesiredHidden(true);if(next&&TaskbarAutoHide.Enabled)TaskbarAutoHide.Set(false);message=DesktopVisibility.SetHidden(true,next);}else if(action==2){bool next=hidden??!cleanDesktopActive;SetCleanDesktop(next);message=next?"已隐藏桌面图标和分区":"已恢复桌面图标和分区";}else {SetCleanDesktop(false);try{TaskbarAutoHide.ShowAndClear();}catch(Exception ex){Store.Log(ex);}DesktopVisibility.ShowAll();message="已执行任务栏与桌面图标恢复";}
                Board.SetStatus(message);if(Tray!=null)Tray.ShowBalloonTip(1500,"Lumina",message,Forms.ToolTipIcon.Info);
            }catch(Exception ex){Store.Log(ex);MessageBox.Show("无法切换系统界面："+ex.Message,"Lumina");}
        }
        public void Quit() {
            if(ShuttingDown)return;
            try{if(settingsWindow!=null)settingsWindow.Close();Store.Save(Prefs);}
            catch(Exception ex){Store.Log(ex);MessageBox.Show("退出前保存失败，软件仍保持运行，避免丢失当前内容。\n"+ex.Message,"Lumina");return;}
            ShuttingDown=true;SystemTheme.Changed-=SystemThemeChanged;SystemTheme.Stop();
            try{DesktopVisibility.RestoreOriginal();}catch(Exception ex){Store.Log(ex);}try{TaskbarAutoHide.Restore();}catch(Exception ex){Store.Log(ex);}
            try{CloseControlCenter();CloseWidgets();}catch(Exception ex){Store.Log(ex);}if(Tray!=null){Tray.Visible=false;Tray.Dispose();}Dock.ReleaseHotkey();Application.Current.Shutdown();
        }
    }
}
