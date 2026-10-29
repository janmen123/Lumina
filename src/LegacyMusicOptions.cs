using System.Runtime.Serialization;
namespace GlassBoard { public partial class WidgetOptions {
[DataMember]public bool? EnhancedMusic;[DataMember]public bool? ShowLyrics;[DataMember]public bool LyricTranslation=true;[DataMember]public int LyricOffset;[DataMember]public bool StandaloneLyrics;[DataMember]public double? LyricLeft,LyricTop;
} }
