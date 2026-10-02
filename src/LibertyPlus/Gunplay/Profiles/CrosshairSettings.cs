using System.Runtime.Serialization;

// Fields are populated by DataContractJsonSerializer.
#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Profiles
{
    [DataContract]
    internal sealed class CrosshairSettings
    {
        [DataMember(Name = "replaceVanillaReticle", IsRequired = true)] internal bool ReplaceVanillaReticle;
        [DataMember(Name = "showForVanillaWeapons", IsRequired = true)] internal bool ShowForVanillaWeapons;
        [DataMember(Name = "lineLengthPixels", IsRequired = true)] internal double LineLengthPixels;
        [DataMember(Name = "lineThicknessPixels", IsRequired = true)] internal double LineThicknessPixels;
        [DataMember(Name = "outlinePixels", IsRequired = true)] internal double OutlinePixels;
        [DataMember(Name = "minimumGapPixels", IsRequired = true)] internal double MinimumGapPixels;
        [DataMember(Name = "maximumGapPixels", IsRequired = true)] internal double MaximumGapPixels;
        [DataMember(Name = "showCenterDot", IsRequired = true)] internal bool ShowCenterDot;
        [DataMember(Name = "centerDotPixels", IsRequired = true)] internal double CenterDotPixels;
        [DataMember(Name = "colorArgb", IsRequired = true)] internal int[] ColorArgb;
        [DataMember(Name = "outlineArgb", IsRequired = true)] internal int[] OutlineArgb;
        [DataMember(Name = "gapSmoothingPerSecond", IsRequired = true)] internal double GapSmoothingPerSecond;
        // "vertical" when the game camera FOV is the vertical field of view (RAGE convention).
        [DataMember(Name = "fovAxis", IsRequired = true)] internal string FovAxis;
    }
}
