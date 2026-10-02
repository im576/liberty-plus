using System.Runtime.Serialization;

#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Profiles
{
    [DataContract]
    internal sealed class DebugHitSettings
    {
        [DataMember(Name = "scanRadiusMeters", IsRequired = true)] internal float ScanRadiusMeters;
        [DataMember(Name = "scanIntervalMilliseconds", IsRequired = true)] internal double ScanIntervalMilliseconds;
        [DataMember(Name = "worldClassificationDelayMilliseconds", IsRequired = true)] internal double WorldClassificationDelayMilliseconds;
    }
}
