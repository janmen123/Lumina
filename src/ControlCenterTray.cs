using System;using System.Threading.Tasks;using System.Windows.Controls;using System.Windows.Media;
namespace Lumina {
 public sealed partial class ControlCenterWindow {
  TextBlock nativeTrayStatus;
  void ShowTray(){MarkSection("tray");page="tray";volumeDelay.Stop();brightnessDelay.Stop();SetSectionSize(true);body.Children.Clear();
   Label("Windows 系统托盘");nativeTrayStatus=new TextBlock{Text="使用 Windows 原生托盘面板，直接操作原软件图标和菜单。任务栏被隐藏时会先自动显示。",TextWrapping=System.Windows.TextWrapping.Wrap,Foreground=Brushes.LightGray,Margin=new System.Windows.Thickness(6,8,6,14)};body.Children.Add(nativeTrayStatus);
   body.Children.Add(ActionButton("打开系统托盘",OpenNativeTray,290));
  }
  internal void CheckReopenAfterTray(){bool priorReading=reading;reading=true;try{for(int i=0;i<4;i++){SelectSection("tray");Collapse(true);nativeTrayBusy=true;OpenNear();if(!IsVisible||page!="home"||activeSection!="control")throw new Exception("托盘操作后小球无法正常重新展开");if(command)throw new Exception("托盘操作占用了控制中心命令状态");Collapse(true);}}finally{nativeTrayBusy=false;reading=priorReading;}}
  bool nativeTrayBusy;int trayViewRevision;
  async void OpenNativeTray(){
   if(nativeTrayBusy||stopped)return;nativeTrayBusy=true;Collapse(true);int revision=trayViewRevision;
   try{
    // 任务栏被隐藏时（Ctrl+Alt+T），隐藏图标入口并不存在，先把它显示出来再交给 helper。
    bool revealed=false;
    try{if(DesktopVisibility.IsHidden(true)==true){DesktopVisibility.SetHidden(true,false);revealed=true;if(revealed)await Task.Delay(450);}}catch(Exception ex){Store.Log(ex);}
    string exe=System.Reflection.Assembly.GetExecutingAssembly().Location;
    int result=await BoundedProcess.RunAsync(new System.Diagnostics.ProcessStartInfo(exe,"--open-native-tray"){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory},3000);
    if(stopped||revision!=trayViewRevision||result==0)return;
    if(result==BoundedProcess.TimedOut&&NativeTrayLauncher.OverflowVisible())return;
    string message=result==BoundedProcess.TimedOut?"托盘调用未及时返回。可重新点击打开，小球仍可正常使用。":"未找到隐藏图标入口。请按 Ctrl+Alt+T 显示任务栏后重试。";
    ShowTray();nativeTrayStatus.Text=message;Show();Activate();
   }catch(Exception ex){if(!stopped&&revision==trayViewRevision){ShowTray();nativeTrayStatus.Text="未能打开系统托盘："+ex.GetBaseException().Message;Show();Activate();Store.Trace("Native tray: "+ex.GetBaseException().Message);}}
   finally{nativeTrayBusy=false;}
  }
 }
}
