using System;using System.Windows;using System.Windows.Controls;using System.Windows.Media;
namespace GlassBoard {
    public partial class Controller {
        public void ShowAiHelp(Window owner,string component,string details){
            bool previous=Board.ModalBusy;Board.ModalBusy=true;try{
                var w=FeatureUi.Dialog("AI 排查信息 · "+component,owner??Dock,720,720);var root=new DockPanel{Margin=new Thickness(22)};w.Content=root;
                var note=new TextBlock{Text="先查看并补充下方信息，再复制给你使用的 AI。AI 可协助排查设置，必要时针对对应版本源码给出修改步骤。",TextWrapping=TextWrapping.Wrap,LineHeight=23,Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(note,System.Windows.Controls.Dock.Top);root.Children.Add(note);
                var actions=new WrapPanel{Margin=new Thickness(0,12,0,0)};DockPanel.SetDock(actions,System.Windows.Controls.Dock.Bottom);root.Children.Add(actions);
                var text=new TextBox{Text=AiSupport.Build(component,details),AcceptsReturn=true,AcceptsTab=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(12),FontSize=13};root.Children.Add(text);
                actions.Children.Add(FeatureUi.Button("复制给 AI 的排查信息",()=>{try{Clipboard.SetText(text.Text);note.Text="已复制。粘贴给 AI 后，可按它的提问补充结果。";}catch(Exception ex){MessageBox.Show(w,ex.Message,"复制失败");}}));
                actions.Children.Add(FeatureUi.Button("打开源码仓库",OpenRepository));actions.Children.Add(FeatureUi.Button("关闭",()=>w.Close()));w.ShowDialog();
            }catch(Exception ex){Store.Log(ex);MessageBox.Show(ex.Message,"无法打开排查信息");}finally{Board.ModalBusy=previous;}
        }
    }
}
