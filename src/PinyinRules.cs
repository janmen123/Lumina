using System;
using System.Collections.Generic;
using System.Text;

namespace Lumina {
    // Turns a mixed Chinese/Latin name into pinyin-initial text ("抖音" → "dy") so
    // search boxes can match by the first letter of each character. The mapping is
    // derived from GB2312 code ranges, which covers common simplified Chinese without
    // bundling a dictionary.
    public static class PinyinRules {
        static readonly int[] Boundaries={
            45217,45253,45761,46318,46826,47010,47297,47614,48119,49062,49324,49896,
            50371,50614,50622,50906,51387,51446,52218,52698,52980,53689,54481,55290
        };
        const string Letters="ABCDEFGHJKLMNOPQRSTWXYZ";
        public static string Initials(string text) {
            if(string.IsNullOrEmpty(text))return "";
            var buffer=new StringBuilder(text.Length);
            foreach(char c in text) {
                if(c>='0'&&c<='9'){buffer.Append(c);continue;}
                if(c<128){if(char.IsLetter(c))buffer.Append(char.ToLowerInvariant(c));continue;}
                char initial=Initial(c);
                if(initial!='\0')buffer.Append(initial);
            }
            return buffer.ToString();
        }
        public static bool Matches(string name,string query) {
            if(string.IsNullOrEmpty(query))return true;
            string plain=name??"";
            string initials=Initials(plain);
            if(initials.Length==0)return false;
            foreach(string word in query.Split(new char[]{' '},StringSplitOptions.RemoveEmptyEntries)) {
                string key=word.ToLowerInvariant();
                if(plain.IndexOf(word,StringComparison.CurrentCultureIgnoreCase)>=0)continue;
                if(initials.IndexOf(key,StringComparison.Ordinal)>=0)continue;
                return false;
            }
            return true;
        }
        static char Initial(char c) {
            byte[] bytes;
            try { bytes=Encoding.GetEncoding(936).GetBytes(c.ToString()); }
            catch { return '\0'; }
            if(bytes.Length!=2)return '\0';
            int code=(bytes[0]<<8)+bytes[1];
            if(code<Boundaries[0]||code>Boundaries[Boundaries.Length-1])return '\0';
            for(int i=0;i<Letters.Length;i++) {
                if(code>=Boundaries[i]&&code<Boundaries[i+1])return char.ToLowerInvariant(Letters[i]);
            }
            return '\0';
        }
    }
}
