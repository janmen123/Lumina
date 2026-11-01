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

[assembly: System.Reflection.AssemblyTitle("Lumina")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Lumina.Regression")]
[assembly: System.Reflection.AssemblyVersion(Lumina.AppInfo.AssemblyVersion)]
[assembly: System.Reflection.AssemblyFileVersion(Lumina.AppInfo.AssemblyVersion)]

namespace Lumina {
    public static class LayoutRules {
        public static int Pages(int count,int capacity) {
            if(count<0 || capacity<1)throw new ArgumentOutOfRangeException();
            return Math.Max(1,(count+capacity-1)/capacity);
        }
        public static bool Matches(string name,string query) {
            string text=name??"";
            if(string.IsNullOrEmpty((query??"").Trim()))return true;
            if((query??"").Split(new char[]{' '},StringSplitOptions.RemoveEmptyEntries).All(word=>text.IndexOf(word,StringComparison.CurrentCultureIgnoreCase)>=0))return true;
            return PinyinRules.Matches(text,query);
        }
        public static double GlassOpacity(double value) {return double.IsNaN(value)||double.IsInfinity(value)?72:Math.Max(0,Math.Min(100,value));}
        public static bool AllowImport(string path,IEnumerable<string> removed,bool manual) {return manual||!removed.Any(p=>string.Equals(p,path,StringComparison.OrdinalIgnoreCase));}
        public static double Clamp(double value,double min,double max,double fallback) {return double.IsNaN(value)||double.IsInfinity(value)?fallback:Math.Max(min,Math.Min(max,value));}
    }

}
