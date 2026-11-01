namespace Lumina {
 public static class MotionPresets {
  public static bool ApplyInitialBaseline(Preferences preferences){if(preferences.ReducedMotionBaselineApplied)return false;DisableTransitions(preferences);preferences.ReducedMotionBaselineApplied=true;return true;}
  public static void DisableTransitions(Preferences preferences){if(preferences.Dock==null)preferences.Dock=new DockOptions();preferences.Dock.MotionEnabled=false;preferences.Dock.OpeningAnimation=false;preferences.Dock.HoverAnimation=false;preferences.Dock.SpringReveal=false;preferences.Dock.DragMotion=false;preferences.BoardPageMotion=false;if(preferences.ControlCenter!=null)preferences.ControlCenter.Motion=false;}
 }
}

