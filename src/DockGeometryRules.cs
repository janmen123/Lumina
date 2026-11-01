using System;
using System.Windows;

namespace Lumina {
    internal static class DockGeometryRules {
        internal const double ShadowMargin=14;
        internal static double ButtonHeight(DockOptions o){return o.IconSize+16+DockReflection.ExtraHeight(o)+(o.ShowLabels?18:0);}
        internal static Thickness Insets(DockOptions o){
            double height=ButtonHeight(o);
            double zoom=o.HoverMotion&&InteractionRules.ZoomHover(o.HoverAnimationStyle)?Math.Min(1.6,1+(o.Zoom-1)*o.MotionLevel):1;
            double lift=o.HoverMotion&&InteractionRules.LiftHover(o.HoverAnimationStyle)?5*o.MotionLevel:0;
            // The icon can rise into the transparent shadow margin. Keep the lower
            // hover envelope inside the glass, including when flush with the screen.
            double top=Math.Max(o.VerticalPadding,height*(zoom-1)*.65+lift-ShadowMargin);
            double bottom=Math.Max(o.VerticalPadding,height*(zoom-1)*.35-lift);
            return new Thickness(o.HoverPadding(),top,o.HoverPadding(),bottom);
        }
        internal static double Height(DockOptions o){var inset=Insets(o);return 2*ShadowMargin+ButtonHeight(o)+inset.Top+inset.Bottom;}
        internal static double Top(double requested,Rect area,double height){
            double maximum=Math.Max(area.Top,area.Bottom-height+ShadowMargin);
            return Math.Max(area.Top,Math.Min(maximum,requested));
        }
        internal static double BottomTop(Rect area,double height,double gap){return Top(area.Bottom-height+ShadowMargin-gap,area,height);}
    }
}
