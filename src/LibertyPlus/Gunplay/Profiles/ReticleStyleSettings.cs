using System.Runtime.Serialization;

// Fields are populated by DataContractJsonSerializer.
#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Profiles
{
    // One reticle style (T-043). Every field is optional: a class or weapon entry only names what differs from the base,
    // and the base is the old `crosshair` section, so an install that never heard of `reticles` keeps its reticle.
    [DataContract]
    internal sealed class ReticleStyleSettings
    {
        // cross (four arms), bracket (four corner brackets), ring (dots on a circle: pellet pattern), dot, none.
        [DataMember(Name = "style", IsRequired = false)] internal string Style;
        [DataMember(Name = "lineLengthPixels", IsRequired = false)] internal double? LineLengthPixels;
        [DataMember(Name = "lineThicknessPixels", IsRequired = false)] internal double? LineThicknessPixels;
        [DataMember(Name = "outlinePixels", IsRequired = false)] internal double? OutlinePixels;
        // The gap (cross), half-size (bracket) or radius (ring) follows the live spread cone, clamped to these.
        [DataMember(Name = "minimumGapPixels", IsRequired = false)] internal double? MinimumGapPixels;
        [DataMember(Name = "maximumGapPixels", IsRequired = false)] internal double? MaximumGapPixels;
        // Side of the centre dot in pixels; 0 = no dot.
        [DataMember(Name = "centerDotPixels", IsRequired = false)] internal double? CenterDotPixels;
        // Dots on the ring (ring style): about one per pellet.
        [DataMember(Name = "ringDots", IsRequired = false)] internal int? RingDots;
        [DataMember(Name = "colorArgb", IsRequired = false)] internal int[] ColorArgb;
        [DataMember(Name = "outlineArgb", IsRequired = false)] internal int[] OutlineArgb;
        [DataMember(Name = "gapSmoothingPerSecond", IsRequired = false)] internal double? GapSmoothingPerSecond;
    }
}
