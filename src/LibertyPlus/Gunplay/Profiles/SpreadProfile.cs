using System.Runtime.Serialization;

// Fields are populated by DataContractJsonSerializer.
#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Profiles
{
    // Firing spread for one weapon: half-angle of the bullet deviation cone, in degrees,
    // measured from the muzzle. This value is written into the game's own accuracy input.
    [DataContract]
    internal sealed class SpreadProfile
    {
        [DataMember(Name = "baseDegrees", IsRequired = true)] internal double BaseDegrees;
        [DataMember(Name = "perShotDegrees", IsRequired = true)] internal double PerShotDegrees;
        [DataMember(Name = "burstShotCount", IsRequired = true)] internal int BurstShotCount;
        [DataMember(Name = "burstPerShotMultiplier", IsRequired = true)] internal double BurstPerShotMultiplier;
        [DataMember(Name = "chainResetMilliseconds", IsRequired = true)] internal double ChainResetMilliseconds;
        [DataMember(Name = "maxDegrees", IsRequired = true)] internal double MaxDegrees;
        [DataMember(Name = "recoveryDelayMilliseconds", IsRequired = true)] internal double RecoveryDelayMilliseconds;
        [DataMember(Name = "recoveryDegreesPerSecond", IsRequired = true)] internal double RecoveryDegreesPerSecond;
        [DataMember(Name = "shortBurstRecoveryDegreesPerSecond", IsRequired = true)] internal double ShortBurstRecoveryDegreesPerSecond;
        [DataMember(Name = "movingAddDegrees", IsRequired = true)] internal double MovingAddDegrees;
        [DataMember(Name = "crouchedMultiplier", IsRequired = true)] internal double CrouchedMultiplier;
        [DataMember(Name = "coverMultiplier", IsRequired = true)] internal double CoverMultiplier;
        [DataMember(Name = "vehicleMultiplier", IsRequired = true)] internal double VehicleMultiplier;
        [DataMember(Name = "hipFireMultiplier", IsRequired = true)] internal double HipFireMultiplier;
        [DataMember(Name = "blindFireMultiplier", IsRequired = true)] internal double BlindFireMultiplier;
        [DataMember(Name = "airborneMultiplier", IsRequired = true)] internal double AirborneMultiplier;
        // Extra display-only cone for multi-pellet weapons until the shot audit measures the real pattern.
        [DataMember(Name = "pelletPatternDegrees", IsRequired = true)] internal double PelletPatternDegrees;

        internal SpreadProfile Clone()
        {
            return (SpreadProfile)MemberwiseClone();
        }
    }
}
