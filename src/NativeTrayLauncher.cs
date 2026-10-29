using System;using System.Linq;using System.Runtime.InteropServices;using System.Threading;using System.Windows.Automation;
namespace GlassBoard {
 public static class NativeTrayLauncher {
  static readonly string[] OverflowClasses={"NotifyIconOverflowWindow","TopLevelWindowForOverflowXamlIsland"};
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindow(string cls,string title);
  [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr window);
  [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
  [DllImport("user32.dll")]static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
  public static bool OverflowVisible(){return OverflowClasses.Any(cls=>{var handle=FindWindow(cls,null);return handle!=IntPtr.Zero&&IsWindowVisible(handle);});}
  static AutomationElement Chevron(){var handle=FindWindow("Shell_TrayWnd",null);if(handle==IntPtr.Zero)return null;return AutomationElement.FromHandle(handle).FindFirst(TreeScope.Descendants,new OrCondition(new PropertyCondition(AutomationElement.ClassNameProperty,"SystemTray.ChevronIconView"),new PropertyCondition(AutomationElement.AutomationIdProperty,"SystemTrayChevron"),new PropertyCondition(AutomationElement.NameProperty,"显示隐藏的图标"),new PropertyCondition(AutomationElement.NameProperty,"Show hidden icons")));}
  static bool Invoke(AutomationElement arrow){if(arrow==null)return false;object pattern;if(arrow.TryGetCurrentPattern(InvokePattern.Pattern,out pattern)){((InvokePattern)pattern).Invoke();return true;}if(arrow.TryGetCurrentPattern(ExpandCollapsePattern.Pattern,out pattern)){((ExpandCollapsePattern)pattern).Expand();return true;}return false;}
  public static int Run(){int code=1;var thread=new Thread(()=>{try{code=Open()?0:2;}catch(Exception ex){Store.Trace("Native tray helper: "+ex.GetBaseException().Message);code=1;}});thread.IsBackground=true;thread.SetApartmentState(ApartmentState.MTA);thread.Start();thread.Join();return code;}
  public static bool Open(){
   if(OverflowVisible())return true;
   foreach(int key in new[]{0x10,0x11,0x12,0x5B,0x5C})if((GetAsyncKeyState(key)&0x8000)!=0)throw new InvalidOperationException("请松开 Ctrl、Alt、Shift 或 Windows 键后重试。");
   try{if(Invoke(Chevron()))return true;}catch(ElementNotAvailableException){}
   // Win+B reveals/focuses the native notification area. Invoke only the actual
   // chevron discovered afterward; never click guessed coordinates or fake icons.
   try{keybd_event(0x5B,0,0,UIntPtr.Zero);keybd_event(0x42,0,0,UIntPtr.Zero);}finally{keybd_event(0x42,0,2,UIntPtr.Zero);keybd_event(0x5B,0,2,UIntPtr.Zero);}
   Thread.Sleep(180);return OverflowVisible()||Invoke(Chevron());
  }
 }
}
