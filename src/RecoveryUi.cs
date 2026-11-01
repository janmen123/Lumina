using System;using System.Linq;using System.Collections.Generic;using System.Windows;using System.Windows.Controls;
namespace Lumina {
    public partial class Controller {
        void RecoverHistoricalEntries(Window owner){bool previous=Board.ModalBusy;Board.ModalBusy=true;try{
            var candidates=EntryRecoveryRules.Candidates(Prefs,Store.HistoricalEntries());if(candidates.Count==0){MessageBox.Show(owner,"历史配置中没有找到缺失入口。若旧版已多次覆盖配置，仍可能需要从你保留的完整备份中恢复。","找回旧版入口");return;}
            var w=FeatureUi.Dialog("找回旧版入口与分组",owner,760,580);var root=new DockPanel{Margin=new Thickness(22)};w.Content=root;
            var heading=new TextBlock{Text="从升级前配置、安全副本和完整备份中找回入口或分组归属。勾选后合并到当前布局；当前 DIY 保留。标注为已移除的入口默认不勾选。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,14)};DockPanel.SetDock(heading,System.Windows.Controls.Dock.Top);root.Children.Add(heading);
            var actions=new WrapPanel();DockPanel.SetDock(actions,System.Windows.Controls.Dock.Bottom);root.Children.Add(actions);var rows=new StackPanel();root.Children.Add(new ScrollViewer{Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            var choices=new Dictionary<Entry,CheckBox>();foreach(var entry in candidates){bool removed=RemovalRules.Blocked(Prefs,entry);var check=new CheckBox{Content=new TextBlock{Text=entry.Name+" · "+string.Join("、",FeatureRules.Memberships(entry))+(removed?" 【曾移除】":"")+"\n"+entry.Path,TextWrapping=TextWrapping.Wrap},IsChecked=!removed,Margin=new Thickness(0,6,0,10)};choices.Add(entry,check);rows.Children.Add(check);}
            actions.Children.Add(FeatureUi.Button("全选",()=>{foreach(var check in choices.Values)check.IsChecked=true;}));actions.Children.Add(FeatureUi.Button("取消全选",()=>{foreach(var check in choices.Values)check.IsChecked=false;}));
            actions.Children.Add(FeatureUi.Button("合并所选入口",()=>{var chosen=choices.Where(x=>x.Value.IsChecked==true).Select(x=>x.Key).ToList();if(chosen.Count==0)return;FlushBackupState();string backup=System.IO.Path.Combine(Store.Folder,"backups","找回入口前_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".gbbackup");System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(backup));BackupFiles.Create(Store.Folder,backup);var before=BeginBoardEdit();int count=EntryRecoveryRules.Restore(Prefs,chosen);EndBoardEdit(before,"找回旧版入口");Refresh();MessageBox.Show(w,"已合并 "+count+" 个入口及分组归属。","找回完成");w.Close();}));w.ShowDialog();
        }catch(Exception ex){Store.Log(ex);MessageBox.Show(owner,ex.Message,"找回未完成");}finally{Board.ModalBusy=previous;}}
    }
}
