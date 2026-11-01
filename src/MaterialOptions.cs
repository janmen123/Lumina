using System;using System.Runtime.Serialization;
namespace Lumina {
    [DataContract] public class MaterialOptions {
        [DataMember] public int Kind;
        [DataMember] public string Tint="";
        [DataMember] public double Gloss=.15,Grain=0;
        [DataMember] public bool Shadow=true;
        [OnDeserializing]void Defaults(StreamingContext context){Gloss=.15;Shadow=true;Tint="";}
        public void Normalize(){Kind=Math.Max(0,Math.Min(3,Kind));Gloss=LayoutRules.Clamp(Gloss,0,1,.15);Grain=LayoutRules.Clamp(Grain,0,1,0);if(!System.Text.RegularExpressions.Regex.IsMatch(Tint??"",@"^#[a-fA-F0-9]{6}$"))Tint="";}
        public MaterialOptions Copy(){return (MaterialOptions)MemberwiseClone();}
    }
}
