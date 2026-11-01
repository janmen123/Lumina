using System;using System.IO;using System.Linq;using System.Windows;using System.Windows.Controls;using System.Windows.Interop;using System.Windows.Media;using System.Windows.Media.Imaging;
namespace GlassBoard {
    public partial class Controller {
        void BuildHomePage(StackPanel panel,Window owner){
            panel.Children.Add(new TextBlock{Text="GlassBoard · 玻璃板",FontSize=26,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,12,0,8)});
            panel.Children.Add(new TextBlock{Text="v"+CurrentVersion.ToString(3)+"  /  "+CreatorIdentityProof.AuthorName,FontSize=16,Margin=new Thickness(0,0,0,18)});
            panel.Children.Add(FeatureUi.Button("GitHub 仓库 · 下载新版本",OpenRepository));
            panel.Children.Add(new TextBlock{Text="抖音主页与问题反馈",FontSize=20,Margin=new Thickness(0,22,0,10)});
            panel.Children.Add(new TextBlock{Text="抖音号：Senlin_Sama\n使用抖音扫一扫下方主页码。遇到问题，请到抖音主页的粉丝群反馈。",TextWrapping=TextWrapping.Wrap,LineHeight=23});
            panel.Children.Add(FeatureUi.Button("复制抖音号",()=>{try{Clipboard.SetText(CreatorIdentityProof.DouyinId);}catch(Exception ex){MessageBox.Show(owner,ex.Message,"复制失败");}}));
            try{
                var source=DouyinImage();var image=new Image{Source=source,Height=560,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,12,0,8),Cursor=System.Windows.Input.Cursors.Hand,ToolTip="点击放大主页码"};
                image.MouseLeftButtonUp+=(s,e)=>{var w=FeatureUi.Dialog("抖音主页 · 森霖Sama",owner,560,820);w.Content=new ScrollViewer{Content=new Image{Source=source,Stretch=Stretch.Uniform,Margin=new Thickness(14)}};w.ShowDialog();};panel.Children.Add(image);
            }catch(Exception ex){Store.Log(ex);panel.Children.Add(new TextBlock{Text="主页码缺失或校验未通过，请重新解压官方原版，或在抖音搜索 Senlin_Sama。",TextWrapping=TextWrapping.Wrap});}
            panel.Children.Add(new TextBlock{Text="反馈时请附上版本、复现步骤和截图或录屏。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,14)});
            panel.Children.Add(FeatureUi.Button("音乐组件 · 文件兼容检查",()=>MusicFileCheck(owner)));
            panel.Children.Add(FeatureUi.Button("读取失败？生成 AI 排查信息",()=>ShowAiHelp(owner,"综合排查",WidgetStatus)));
        }
        static ImageSource DouyinImage(){using(var stream=new MemoryStream(CreatorAssets.Read("Douyin"),false)){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();return image;}}
        public void MusicFileCheck(Window owner){
            bool busy=Board.ModalBusy;Board.ModalBusy=true;
            try{
                var files=ComponentFiles.Inspect(AppDomain.CurrentDomain.BaseDirectory);var w=FeatureUi.Dialog("音乐组件 · 文件兼容检查",owner,580,450);var panel=new StackPanel{Margin=new Thickness(22)};w.Content=new ScrollViewer{Content=panel};
                panel.Children.Add(new TextBlock{Text=string.Join("\n",files.Select(f=>f.Name+"："+f.Status)),TextWrapping=TextWrapping.Wrap,LineHeight=24});
                panel.Children.Add(new TextBlock{Text="文件阻止只是可能原因之一。也请确认播放器正在播放，并已允许系统媒体控制。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,14,0,12)});
                var repair=FeatureUi.Button("解除已校验组件文件的阻止",()=>{try{int count=ComponentFiles.Repair(files);MessageBox.Show(w,"已处理 "+count+" 个已校验文件。请重新打开音乐组件。","兼容检查");w.Close();ReopenWidgets();}catch(Exception ex){MessageBox.Show(w,ex.Message,"未完成");}});repair.IsEnabled=files.Any(f=>f.Valid&&f.Blocked);panel.Children.Add(repair);
                panel.Children.Add(FeatureUi.Button("打开配置与日志文件夹",()=>{try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Store.Folder){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(w,ex.Message);}}));w.ShowDialog();
            }catch(Exception ex){Store.Log(ex);MessageBox.Show(owner,ex.Message,"兼容检查");}finally{Board.ModalBusy=busy;}
        }
    }
    public partial class BoardWindow {public override bool WantsTopmost{get{return C!=null&&C.Prefs.BoardAlwaysOnTop;}}}
    public partial class DockWindow {
        public override bool WantsTopmost{get{return C!=null&&InteractionRules.DockTopmost(C.Prefs.Dock,boardPinDragging);}}
        public override bool YieldToFullscreen{get{return C==null||C.Prefs.Dock.RespectFullscreen;}}
        void ForceHideForFullscreen(){if(C.GlassSuppressed)return;bool first=!renderedHidden;renderedHidden=true;hidePolicy=new AutoHidePolicy();if(first){double target=dockScreenArea.Bottom-20;AnimateDockVisibility(true,target);}}
    }
    public sealed partial class WidgetWindow {public override bool WantsTopmost{get{return M!=null&&M.C.Prefs.Widgets.Topmost;}}}
}
