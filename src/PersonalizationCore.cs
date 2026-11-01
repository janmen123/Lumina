using System;using System.Collections.Generic;using System.Linq;using System.Runtime.Serialization;using System.Text.RegularExpressions;
namespace Lumina {
    [DataContract] public class WidgetStyle {
        [DataMember] public string Kind="clock",TextColor="",Font="";
        [DataMember] public double TextOpacity=100,ClockSize=48;
        [DataMember] public bool Frameless=false,TextShadow=false,ShowDate=true,Hour24=true;
        [DataMember] public int ClockStyle=0;
        [DataMember] public bool ShowAmPm=true;
        [OnDeserializing] void Defaults(StreamingContext c){Frameless=false;TextOpacity=100;ClockSize=48;ShowDate=Hour24=true;ShowAmPm=false;TextColor=Font="";}
        public void Normalize(){TextColor=Color(TextColor);Font=(Font??"").Trim();if(Font.Length>120)Font="";TextOpacity=LayoutRules.Clamp(TextOpacity,0,100,100);ClockSize=LayoutRules.Clamp(ClockSize,28,100,48);ClockStyle=Math.Max(0,Math.Min(ClockCatalog.Names.Length-1,ClockStyle));}
        public static string Color(string value){value=(value??"").Trim();if(!value.StartsWith("#")&&value.Length==6)value="#"+value;return Regex.IsMatch(value,@"^#[0-9a-fA-F]{6}$")?value.ToUpperInvariant():"";}
        public WidgetStyle Copy(){return (WidgetStyle)MemberwiseClone();}
        public string Signature(){return Kind+"/"+TextColor+"/"+Font+"/"+TextOpacity+"/"+ClockSize+"/"+Frameless+"/"+TextShadow+"/"+ShowDate+"/"+Hour24+"/"+ClockStyle+"/"+ShowAmPm;}
    }
    public partial class WidgetOptions {
        [DataMember] public List<WidgetStyle> Styles=new List<WidgetStyle>();
        public void NormalizeStyles(){Styles=(Styles??new List<WidgetStyle>()).Where(x=>x!=null&&WidgetRules.Kinds.Contains(x.Kind)).GroupBy(x=>x.Kind).Select(g=>g.First()).ToList();foreach(var s in Styles)s.Normalize();}
        public WidgetStyle Style(string kind){if(Styles==null)Styles=new List<WidgetStyle>();var s=Styles.FirstOrDefault(x=>x.Kind==kind);if(s==null){s=new WidgetStyle{Kind=kind};Styles.Add(s);}return s;}
    }
    public static class DockAnchorRules {
        public static double Left(double? center,double width,double fallback,double areaLeft,double areaWidth){return FeatureRules.Position(center.HasValue?center.Value-width/2:fallback,areaLeft,areaWidth,width);}
    }
    public static class BackgroundWorkRules {
        public static double MediaPollSeconds(bool visible,bool available,bool playing){return !visible?10:!available?5:playing?1.5:3;}
    }
    public static class ClockCatalog {
        public static readonly string[] Names={"经典时间","手写星期叠字","极简大数字","星期与日期","分栏数字","宽距星期","日期海报","纵向数字","细线指针","日程轨道","衬线杂志","等宽电子","横向日历","纵向大字海报"};
        public static string Weekday(DateTime now,int style){string day=now.ToString("dddd",System.Globalization.CultureInfo.GetCultureInfo("en-US"));return style==5?string.Join(" ",day.ToUpperInvariant().Select(c=>c.ToString())):style==11?now.ToString("ddd",System.Globalization.CultureInfo.GetCultureInfo("en-US")).ToUpperInvariant():day;}
        public static string DateLabel(DateTime now,int style){var en=System.Globalization.CultureInfo.GetCultureInfo("en-US");return style==5?now.ToString("dd MMMM yyyy",en).ToUpperInvariant():style==10?now.ToString("dddd · dd MMMM",en):style==11?now.ToString("yyyy / MM / dd",en):now.ToString("M月d日  dddd");}
        public static double HourAngle(DateTime now){return now.Hour%12*30+now.Minute*.5;}
        public static double MinuteAngle(DateTime now){return now.Minute*6;}
        public static double DayFraction(DateTime now){return (now.Hour*60+now.Minute)/1440.0;}
    }
}
