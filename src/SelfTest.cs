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
    public static class SelfTest {
        sealed class ButtonTestWindow : GlassWindow {
            public ButtonTestWindow() : base(12) {}
            void RequireCaptureAllowed(){uint affinity;var handle=new WindowInteropHelper(this).EnsureHandle();if(!Native.GetWindowDisplayAffinity(handle,out affinity))throw new Win32Exception(Marshal.GetLastWin32Error(),"GetWindowDisplayAffinity self-test");if(affinity!=0)throw new Exception("玻璃窗口仍被标记为限制屏幕捕获："+affinity);}
            public void CheckCaptureCompatibility(){Width=240;Height=180;Left=SystemParameters.WorkArea.Left+20;Top=SystemParameters.WorkArea.Top+20;RequireCaptureAllowed();Backdrop(false);RequireCaptureAllowed();Show();Backdrop(true);RequireCaptureAllowed();Hide();RequireCaptureAllowed();}
            public void CheckHandlers() {
                var deferred=Button("编辑",null,60);bool deferredCalled=false;
                deferred.ApplyTemplate();
                deferred.Click+=(s,e)=>deferredCalled=true;
                deferred.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                bool inlineCalled=false;var inline=Button("添加",(s,e)=>inlineCalled=true);
                inline.ApplyTemplate();
                inline.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                if(!deferredCalled||!inlineCalled)throw new Exception("按钮事件绑定失败");
                foreach(string icon in new string[]{"play","pause","next","previous","music","shuffle","repeat","sun"}){var control=Button("",null,52);control.Content=Polish.Icon(icon,Polish.Ink(true),22);control.ApplyTemplate();control.Measure(new Size(52,52));control.Arrange(new Rect(0,0,52,52));if(control.DesiredSize.Width<=0)throw new Exception("组件图标布局失败："+icon);}
                var toggle=Controller.VisibilitySwitch();toggle.ApplyTemplate();var thumb=(Border)toggle.Template.FindName("Thumb",toggle);
                toggle.IsChecked=true;if(thumb.HorizontalAlignment!=HorizontalAlignment.Right)throw new Exception("开关开启状态失败");
                toggle.IsChecked=false;if(thumb.HorizontalAlignment!=HorizontalAlignment.Left)throw new Exception("开关关闭状态失败");int clicks=0;toggle.Click+=(s,e)=>clicks++;toggle.IsChecked=true;toggle.IsChecked=false;if(clicks!=0)throw new Exception("状态同步触发了用户点击");toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));if(clicks!=1)throw new Exception("组件点击命令没有执行");
            }
        }
        static string running;
        static System.Threading.Timer watchdog;
        static readonly List<string> failures=new List<string>();
        static void Arm(string name,int seconds){running=name;Disarm();watchdog=new System.Threading.Timer(state=>{try{File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test-result.txt"),"TIMEOUT: "+(running??"self-test")+"（超过 "+seconds+" 秒无响应，已跳过后续检查）\r\n",Encoding.UTF8);}catch{}Environment.Exit(2);},null,seconds*1000,System.Threading.Timeout.Infinite);}
        static void Disarm(){var timer=watchdog;watchdog=null;if(timer!=null)try{timer.Dispose();}catch{}}
        static void Check(string name,int seconds,Action action){Arm(name,seconds);try{action();}catch(Exception ex){failures.Add(name+"："+ex.GetBaseException().Message);}finally{Disarm();}}
        static void Step(string name,int seconds,Action action){Check(name,seconds,action);}
        public static int Run() {
            try {
                Step("内存资源",60,()=>CheckMemoryResources());
                Step("菜单外观",90,()=>MenuAppearanceChecks.Run());
                Step("Dock 行为",90,()=>DockBehaviorChecks.Run());
                Step("反馈面板",90,()=>FeedbackChecks.Run());
                Step("反馈面板 1.8",90,()=>Feedback18Checks.Run());
                Step("组件外观",90,()=>PersonalizationChecks.Run());
                Step("按钮与捕获",90,()=>{var buttons=new ButtonTestWindow();try{buttons.CheckHandlers();buttons.CheckCaptureCompatibility();}finally{buttons.Close();}});
                Step("主题模板",180,()=>{foreach(bool light in new bool[]{false,true}){var styled=new Window{Width=440,Height=350};try{Polish.Apply(styled,light);var panel=new StackPanel();styled.Content=panel;var button=new Button{Content="样式"};var combo=new ComboBox{ItemsSource=new string[]{"A","B"},SelectedIndex=0};var text=new TextBox{Text=new string('长',120),FontSize=17,TextWrapping=TextWrapping.Wrap,MinHeight=48,Padding=new Thickness(12,9,12,9)};var check=new CheckBox{Content="显示",IsChecked=true};var slider=new Slider{Minimum=0,Maximum=100,Value=25};panel.Children.Add(button);panel.Children.Add(combo);panel.Children.Add(text);panel.Children.Add(check);panel.Children.Add(slider);new WindowInteropHelper(styled).EnsureHandle();styled.ShowInTaskbar=false;styled.ShowActivated=false;styled.WindowStartupLocation=WindowStartupLocation.Manual;styled.Left=SystemParameters.WorkArea.Right-470;styled.Top=SystemParameters.WorkArea.Bottom-380;styled.Show();styled.ShowInTaskbar=false;styled.WindowStartupLocation=WindowStartupLocation.Manual;styled.Left=SystemParameters.WorkArea.Right-470;styled.Top=SystemParameters.WorkArea.Bottom-380;styled.Show();panel.Measure(new Size(400,300));panel.Arrange(new Rect(0,0,400,300));panel.UpdateLayout();foreach(var control in new Control[]{button,combo,text,check,slider})control.ApplyTemplate();foreach(double font in new[]{13.0,17.0,24.0}){var probe=new TextBox{Text="闹钟 08:30 中文测试",FontSize=font};panel.Children.Add(probe);probe.ApplyTemplate();probe.Measure(new Size(360,double.PositiveInfinity));probe.Arrange(new Rect(0,0,360,probe.DesiredSize.Height));probe.UpdateLayout();var host=(ScrollViewer)probe.Template.FindName("PART_ContentHost",probe);var line=new TextBlock{Text=probe.Text,FontFamily=probe.FontFamily,FontSize=font};line.Measure(new Size(360,double.PositiveInfinity));if(host==null||host.ViewportHeight+.5<line.DesiredSize.Height)throw new Exception("输入框文字被裁切："+font);panel.Children.Remove(probe);}var track=(Track)slider.Template.FindName("PART_Track",slider);if(track==null||track.Thumb==null)throw new Exception("新滑块模板无 Thumb");slider.Value=75;if(Math.Abs(track.Value-75)>.01)throw new Exception("新滑块值绑定失败");text.Measure(new Size(320,double.PositiveInfinity));if(text.DesiredSize.Height<=48)throw new Exception("长便签标题没有按行增高");var checkBoxPart=(Border)check.Template.FindName("Box",check);if(checkBoxPart==null)throw new Exception("勾选框缺少点击区域");foreach(bool state in new bool[]{false,true,false,true}){check.IsChecked=state;panel.UpdateLayout();var point=checkBoxPart.TranslatePoint(new Point(checkBoxPart.ActualWidth/2,checkBoxPart.ActualHeight/2),check);if(check.InputHitTest(point)==null)throw new Exception("勾选框中心不能点击，状态="+state);var templateRoot=(FrameworkElement)VisualTreeHelper.GetChild(check,0);var gap=templateRoot.TranslatePoint(new Point(23,templateRoot.ActualHeight/2),check);if(check.InputHitTest(gap)==null)throw new Exception("勾选框与文字间隙不能点击");}if(combo.Template.FindName("PART_Popup",combo)==null)throw new Exception("新下拉模板无 Popup");}finally{styled.Close();}}});

                Step("配置与资源",120,()=>{var p=new Preferences{Light=true,DockGap=25,Imported=true,OpacityPercent=40};p.Removed.Add("C:\\工具.lnk");
                p.Apps.Add(new Entry{Name="中文 软件",Path="C:\\测试目录\\工具.lnk",Pinned=true});
                using(var s=new MemoryStream()) {
                    var json=new DataContractJsonSerializer(typeof(Preferences));json.WriteObject(s,p);s.Position=0;
                    var restored=(Preferences)json.ReadObject(s);
                    if(restored.Apps.Count!=1||restored.Apps[0].Name!="中文 软件"||!restored.Apps[0].Pinned||restored.DockGap!=25||restored.OpacityPercent!=40||restored.Removed.Count!=1)throw new Exception("配置往返失败");
                }
                if(LayoutRules.Pages(0,35)!=1||LayoutRules.Pages(35,35)!=1||LayoutRules.Pages(36,35)!=2||LayoutRules.Pages(70,35)!=2)throw new Exception("分页边界失败");
                if(!LayoutRules.Matches("Microsoft Visual Studio Code","code MICROSOFT")||!LayoutRules.Matches("剪映专业版","剪映")||LayoutRules.Matches("Steam","maya"))throw new Exception("搜索失败");
                if(LayoutRules.GlassOpacity(-1)!=0||LayoutRules.GlassOpacity(100)!=100||LayoutRules.GlassOpacity(double.NaN)!=72)throw new Exception("透明度边界失败");
                foreach(string key in new[]{"Wechat","Alipay"})using(var stream=new MemoryStream(CreatorAssets.Read(key),false)){var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();if(bitmap.PixelWidth<200||bitmap.PixelHeight<200)throw new Exception("收款图片尺寸异常");bitmap.Freeze();}});
                Disarm();string resultPath=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test-result.txt");
                if(failures.Count>0){File.WriteAllText(resultPath,"FAILED（其余检查已继续执行）：\r\n"+string.Join("\r\n",failures.ToArray())+"\r\n",Encoding.UTF8);return 1;}
                File.WriteAllText(resultPath,"PASS: transparent material bounds and widget recovery menu; Dock rendering/order/mouse-capture release and native process path/reuse; light/dark nested menus, submenu open/close, checkmarks, disabled items, header sliders, shortcut overlay cache and legacy configuration; shared theme styles, bounded thumbnails and stream-independent pixels, hidden board release/reopen; recordable glass window affinity before/after backdrop/show/hide; embedded creator image decoding, WPF light/dark button/combo/text/check/slider templates, value bindings, button handlers, visibility switch, configuration, removed entries, opacity bounds, pages and search\r\n",Encoding.UTF8);return 0;
            }catch(Exception ex){Disarm();File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test-result.txt"),ex.ToString());return 1;}
        }
        static void CheckMemoryResources(){
            var one=new Window();var two=new Window();try{foreach(bool light in new[]{false,true}){Polish.Apply(one,light);Polish.Apply(two,light);foreach(var type in new[]{typeof(Button),typeof(TextBox),typeof(ComboBox),typeof(Slider),typeof(CheckBox)}){var style=(Style)one.Resources[type];if(!style.IsSealed||!object.ReferenceEquals(style,two.Resources[type]))throw new Exception("主题样式未共享："+type.Name);}Polish.Apply(one,light);}}finally{one.Close();two.Close();}
            foreach(var dims in new[]{new[]{512,4096},new[]{4096,512},new[]{64,64}}){byte[] pixels=new byte[dims[0]*dims[1]];var source=BitmapSource.Create(dims[0],dims[1],96,96,PixelFormats.Gray8,null,pixels,dims[0]);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));using(var stream=new MemoryStream()){encoder.Save(stream);var image=ThumbnailImages.Read(stream.ToArray(),600);if(!image.IsFrozen||image.PixelWidth>600||image.PixelHeight>600||image.PixelWidth>dims[0]||image.PixelHeight>dims[1])throw new Exception("缩略图尺寸或冻结状态无效");int stride=(image.PixelWidth*image.Format.BitsPerPixel+7)/8;image.CopyPixels(new byte[stride*image.PixelHeight],stride,0);}}
            var controller=new Controller(false);try{controller.Prefs.Groups.Add("内存检查分组");controller.Board.Open();if(!controller.Board.HasView||!controller.Board.IsVisible)throw new Exception("面板未创建");controller.Board.Hide();controller.Board.ModalBusy=true;if(controller.Board.ReleaseHiddenView())throw new Exception("忙碌时清理了面板");controller.Board.ModalBusy=false;if(!controller.Board.ReleaseHiddenView()||controller.Board.HasView)throw new Exception("隐藏面板资源未释放");controller.Board.Render();controller.Board.Open();if(!controller.Board.HasView||!controller.Board.IsVisible||!controller.Prefs.Groups.Contains("内存检查分组"))throw new Exception("面板重开或配置保留失败");}finally{controller.ShuttingDown=true;controller.Dock.ReleaseHotkey();controller.Board.Close();controller.Dock.Close();}
        }
    }
}
