using System.Runtime.Serialization;

// Fields are populated by DataContractJsonSerializer.
#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Profiles
{
    // Camera kick for one weapon. Angles are degrees of aim-camera rotation; times are milliseconds.
    [DataContract]
    internal sealed class RecoilProfile
    {
        [DataMember(Name = "verticalKickDegrees", IsRequired = true)] internal double VerticalKickDegrees;
        [DataMember(Name = "horizontalKickDegrees", IsRequired = true)] internal double HorizontalKickDegrees;
        [DataMember(Name = "horizontalRandomDegrees", IsRequired = true)] internal double HorizontalRandomDegrees;
        [DataMember(Name = "firstShotMultiplier", IsRequired = true)] internal double FirstShotMultiplier;
        [DataMember(Name = "sustainedFireGrowthPerShot", IsRequired = true)] internal double SustainedFireGrowthPerShot;
        [DataMember(Name = "sustainedFireShotCap", IsRequired = true)] internal int SustainedFireShotCap;
        [DataMember(Name = "chainResetMilliseconds", IsRequired = true)] internal double ChainResetMilliseconds;
        [DataMember(Name = "maxAccumulatedDegrees", IsRequired = true)] internal double MaxAccumulatedDegrees;
        [DataMember(Name = "minimumKickFractionAtCap", IsRequired = true)] internal double MinimumKickFractionAtCap;
        [DataMember(Name = "kickDurationMilliseconds", IsRequired = true)] internal double KickDurationMilliseconds;
        [DataMember(Name = "recoveryDelayMilliseconds", IsRequired = true)] internal double RecoveryDelayMilliseconds;
        [DataMember(Name = "recoveryDegreesPerSecond", IsRequired = true)] internal double RecoveryDegreesPerSecond;
        [DataMember(Name = "recoveryFraction", IsRequired = true)] internal double RecoveryFraction;
        [DataMember(Name = "movingMultiplier", IsRequired = true)] internal double MovingMultiplier;
        [DataMember(Name = "crouchedMultiplier", IsRequired = true)] internal double CrouchedMultiplier;
        [DataMember(Name = "coverMultiplier", IsRequired = true)] internal double CoverMultiplier;
        [DataMember(Name = "vehicleMultiplier", IsRequired = true)] internal double VehicleMultiplier;
        [DataMember(Name = "hipFireMultiplier", IsRequired = true)] internal double HipFireMultiplier;

        internal RecoilProfile Clone()
        {
            return (RecoilProfile)MemberwiseClone();
        }
    }
}
