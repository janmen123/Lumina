using System;using System.Linq;using System.Text.RegularExpressions;
namespace GlassBoard {
    public static class WeatherPresentation {
        public static string Place(string requested,GeoPlace resolved){GeoPlace point;if(!string.IsNullOrWhiteSpace(requested)&&!WeatherClient.Coordinates(requested,out point))return WeatherClient.NormalizeCity(requested);return resolved==null?"未知地点":resolved.Name;}
        public static string Description(int code,string description){
            string s=(description??"").Trim();if(Regex.IsMatch(s,@"[\u3400-\u9fff]"))return s;
            string v=s.ToLowerInvariant();if(v.Contains("thunder"))return "雷雨";if(v.Contains("blizzard"))return "暴风雪";if(v.Contains("sleet"))return "雨夹雪";if(v.Contains("snow"))return v.Contains("heavy")?"大雪":v.Contains("light")?"小雪":"雪";
            if(v.Contains("freezing")&&(v.Contains("rain")||v.Contains("drizzle")))return "冻雨";
            if(v.Contains("rain")||v.Contains("drizzle"))return v.Contains("heavy")||v.Contains("torrential")?"大雨":v.Contains("moderate")?"中雨":v.Contains("light")||v.Contains("drizzle")?"小雨":v.Contains("shower")?"阵雨":"雨";
            if(v.Contains("fog")||v.Contains("mist"))return "雾";if(v.Contains("overcast"))return "阴";if(v.Contains("cloud"))return "多云";if(v.Contains("sunny"))return "晴";if(v.Contains("clear"))return "晴";if(v.Contains("ice")||v.Contains("hail"))return "冰粒";
            return WmoDescription(code);
        }
        static string WmoDescription(int code){if(code==0)return "晴";if(code==1)return "晴间多云";if(code==2)return "多云";if(code==3)return "阴";if(code==45||code==48)return "雾";if(code==51||code==53||code==55)return "毛毛雨";if(code==56||code==57||code==66||code==67)return "冻雨";if(code==61)return "小雨";if(code==63)return "中雨";if(code==65)return "大雨";if(code==71)return "小雪";if(code==73)return "中雪";if(code==75)return "大雪";if(code==77)return "雪粒";if(code>=80&&code<=82)return "阵雨";if(code==85||code==86)return "阵雪";if(code==95||code==96||code==99)return "雷雨";return "天气暂未知";}
        public static string Icon(int code,string description){string text=Description(code,description);if(text.Contains("雷"))return "storm";if(text.Contains("雪")||text.Contains("冰"))return "snow";if(text.Contains("雨"))return "rain";if(text.Contains("雾"))return "fog";if(text.Contains("云")||text.Contains("阴"))return "cloud";if(text=="晴")return "sun";return "cloud";}
    }
}
