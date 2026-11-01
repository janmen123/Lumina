using System;
namespace Lumina {
    public static class SettingsPlacementRules {
        // Prefer the larger free area above or below the Dock, including when its glass is temporarily hidden.
        public static double[] Vertical(double workTop,double workHeight,double dockTop,double dockHeight,double preferredHeight){
            double bottom=workTop+Math.Max(1,workHeight),margin=Math.Min(12,workHeight/4);
            double aboveEnd=Math.Max(workTop,Math.Min(bottom,dockTop));
            double belowStart=Math.Max(workTop,Math.Min(bottom,dockTop+Math.Max(0,dockHeight)));
            double above=Math.Max(0,aboveEnd-workTop-2*margin),below=Math.Max(0,bottom-belowStart-2*margin);
            bool useBelow=below>above;double room=useBelow?below:above;
            if(room<1)return new[]{workTop+margin,Math.Max(1,workHeight-2*margin)};
            double height=Math.Max(1,Math.Min(preferredHeight,room));
            return new[]{(useBelow?belowStart:workTop)+margin+(room-height)/2,height};
        }
    }
}
