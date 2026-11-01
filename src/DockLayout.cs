using System;using System.Linq;using System.Collections.Generic;using System.Windows;using System.Windows.Controls;using System.Windows.Media;
namespace GlassBoard {
    public partial class DockWindow {
        static readonly DependencyProperty LayoutKey=DependencyProperty.RegisterAttached("DockLayoutKey",typeof(string),typeof(DockWindow),new PropertyMetadata(null));
        static string ItemKey(Button b){return b.GetValue(LayoutKey) as string??(b.Tag is Entry?((Entry)b.Tag).Path:null);}
        IEnumerable<string> TaskKeys{get{return taskGroups.Select(g=>"running:"+g.Key);}}
        List<string> LayoutKeys{get{return taskWheel.Running?DockLayoutRules.Running(C.Prefs,TaskKeys):DockLayoutRules.Common(C.Prefs);}}
        internal bool MoveLayout(string source,string target,bool before){return DockLayoutRules.Move(C.Prefs,source,target,before,taskWheel.Running?TaskKeys:null);}
        internal bool StepLayout(string source,int step){return DockLayoutRules.Step(C.Prefs,source,step,taskWheel.Running?TaskKeys:null);}
        void AddSystemButton(string key,Button button,Action action){button.SetValue(LayoutKey,key);BindLayoutButton(button,key,button.ToolTip as string??"系统入口",action);var menu=button.ContextMenu??MenuTheme.Create(C.Prefs.Light);var earlier=new MenuItem{Header="向前移动"};earlier.Click+=(s,e)=>C.StepDock(key,-1);var later=new MenuItem{Header="向后移动"};later.Click+=(s,e)=>C.StepDock(key,1);if(menu.Items.Count>0)menu.Items.Add(new Separator());menu.Items.Add(earlier);menu.Items.Add(later);if(button.ContextMenu==null)AddDockControls(menu);button.ContextMenu=menu;Items.Children.Add(button);}
        void BindLayoutButton(Button button,string key,string name,Action action){button.SetValue(LayoutKey,key);BindPointerItem(button,key,name,action);AddInsertMarker(button);}
        void AddInsertMarker(Button button){var grid=button.Content as Grid;if(grid==null||grid.Children.OfType<Border>().Any(b=>b.Tag as string=="dockInsert"))return;grid.Children.Add(new Border{Tag="dockInsert",Width=3,Height=C.Prefs.Dock.IconSize+8,CornerRadius=new CornerRadius(2),Background=Polish.Accent,VerticalAlignment=VerticalAlignment.Center,Visibility=Visibility.Collapsed,IsHitTestVisible=false});}
        // A hairline separates the always-pinned icons from the apps that are merely running,
        // matching the one after the app-panel button.
        Border DockSeparator(){return new Border{Width=1,Height=C.Prefs.Dock.IconSize*.7,Margin=new Thickness(8,0,8,0),Background=new SolidColorBrush(Color.FromArgb(65,220,220,230))};}
        void ArrangeDockItems(){var buttons=Items.Children.OfType<Button>().ToList();var order=LayoutKeys;var arranged=new List<Button>();foreach(string key in order){var b=buttons.FirstOrDefault(x=>string.Equals(ItemKey(x),key,StringComparison.OrdinalIgnoreCase));if(b!=null)arranged.Add(b);}var extras=buttons.Where(b=>ItemKey(b)==null).ToList();int last=arranged.FindLastIndex(b=>!DockLayoutRules.SystemItem(ItemKey(b)));arranged.InsertRange(last<0?Math.Min(2,arranged.Count):last+1,extras);Items.Children.Clear();bool runningSeparated=false;foreach(var b in arranged){if(C.Prefs.Dock.ShowSeparator&&extras.Count>0&&!runningSeparated&&ItemKey(b)==null){Items.Children.Add(DockSeparator());runningSeparated=true;}Items.Children.Add(b);if(C.Prefs.Dock.ShowSeparator&&ItemKey(b)==DockLayoutRules.Apps)Items.Children.Add(DockSeparator());}}
        internal void CheckMixedPointerReorder(){
            C.Prefs.Dock.MotionEnabled=false;C.Prefs.Dock.ShowPower=C.Prefs.Dock.ShowNetwork=C.Prefs.Dock.ShowComputer=C.Prefs.Dock.ShowSettings=C.Prefs.Dock.ShowRecycleBin=true;taskWheel.Set(false);Refresh(false);UpdateLayout();
            foreach(string key in DockLayoutRules.SystemKeys)for(int side=0;side<2;side++){
                var source=Items.Children.OfType<Button>().FirstOrDefault(b=>ItemKey(b)==key);if(source==null)throw new Exception("混排缺少系统入口 "+key);var targets=Items.Children.OfType<Button>().Where(b=>b.Tag is Entry).ToList();var target=side==0?targets.First():targets.Last();string destination=ItemKey(target);var drop=DockLayoutRules.OnIcon(C.Prefs,key,destination);if(drop==null)throw new Exception("混排目标无效");
                var expected=LayoutKeys.ToList();expected.Remove(key);expected.Insert(expected.IndexOf(destination)+(drop.Before?0:1),key);bool clicked=false;
                var start=source.TranslatePoint(new Point(source.ActualWidth/2,source.ActualHeight/2),this);var end=target.TranslatePoint(new Point(target.ActualWidth/2,target.ActualHeight/2),this);
                if(!StartPointerItem(source,key,key,()=>clicked=true,start,null))throw new Exception("系统入口无法捕获拖动");MoveDockPointer(end);FinishDockPointer(end);UpdateLayout();if(clicked||!LayoutKeys.SequenceEqual(expected))throw new Exception("系统入口拖放未保存，或拖动触发了点击");
            }
            string saved=string.Join("|",LayoutKeys);C.Prefs.Dock.ShowSettings=false;Refresh(false);if(Items.Children.OfType<Button>().Any(b=>ItemKey(b)==DockLayoutRules.Settings)||ContextMenu==null)throw new Exception("隐藏设置图标后仍显示，或缺少恢复菜单");C.Prefs.Dock.ShowSettings=true;Refresh(false);if(string.Join("|",LayoutKeys)!=saved)throw new Exception("重新显示设置图标后丢失位置");
        }
    }
}
