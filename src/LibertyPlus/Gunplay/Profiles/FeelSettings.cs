using System.Runtime.Serialization;

#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Profiles
{
    [DataContract]
    internal sealed class FeelSettings
    {
        [DataMember(Name = "enabled", IsRequired = true)] internal bool Enabled;
        [DataMember(Name = "shakePitchDegrees", IsRequired = true)] internal double ShakePitchDegrees;
        [DataMember(Name = "shakeHeadingDegrees", IsRequired = true)] internal double ShakeHeadingDegrees;
        [DataMember(Name = "aimFovReductionDegrees", IsRequired = true)] internal double AimFovReductionDegrees;
        [DataMember(Name = "fovSmoothingPerSecond", IsRequired = true)] internal double FovSmoothingPerSecond;
    }
}
