using System;using Lumina;
class SettingsPlacementTests {
    static void Check(bool value,string label){if(!value)throw new Exception(label);}
    static void Case(double top,double height,double dockTop,double dockHeight){var p=SettingsPlacementRules.Vertical(top,height,dockTop,dockHeight,790);Check(p[0]>=top&&p[0]+p[1]<=top+height&&p[1]>0,"settings stays in work area");Check(p[0]+p[1]<=dockTop||p[0]>=dockTop+dockHeight,"settings leaves the Dock unobstructed");}
    static int Main(){Case(0,1040,900,140);Case(0,680,510,140);Case(0,480,290,170);Case(-1080,1040,-170,120);Case(0,1040,20,150);Case(0,1040,470,150);Case(0,1040,1050,120);Case(0,1040,-150,120);Console.WriteLine("PASS: settings placement avoids Dock on small screens, moved Dock and offset work areas");return 0;}
}
