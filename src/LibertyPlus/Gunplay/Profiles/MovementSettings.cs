using System.Runtime.Serialization;

// Fields are populated by DataContractJsonSerializer.
#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Profiles
{
    [DataContract]
    internal sealed class MovementSettings
    {
        [DataMember(Name = "movingSpeedThresholdMetersPerSecond", IsRequired = true)] internal double MovingSpeedThreshold;
        [DataMember(Name = "fullMovementPenaltySpeedMetersPerSecond", IsRequired = true)] internal double FullPenaltySpeed;
    }
}
