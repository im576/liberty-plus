using System.Runtime.Serialization;

// Fields are populated by DataContractJsonSerializer.
#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Profiles
{
    [DataContract]
    internal sealed class SpreadCalibrationSettings
    {
        // Muzzle deviation tangent per unit of CWeaponInfo accuracy for the player (see docs/game-api/MEMORY.md).
        [DataMember(Name = "tangentPerAccuracyUnit", IsRequired = true)] internal double TangentPerAccuracyUnit;
        [DataMember(Name = "autoCalibrate", IsRequired = true)] internal bool AutoCalibrate;
        [DataMember(Name = "minimumSampleSpreadDegrees", IsRequired = true)] internal double MinimumSampleSpreadDegrees;
        [DataMember(Name = "sampleWindow", IsRequired = true)] internal int SampleWindow;
        [DataMember(Name = "gainAdjustRate", IsRequired = true)] internal double GainAdjustRate;
        [DataMember(Name = "minimumGain", IsRequired = true)] internal double MinimumGain;
        [DataMember(Name = "maximumGain", IsRequired = true)] internal double MaximumGain;
        [DataMember(Name = "maximumMeasurableDeviationDegrees", IsRequired = true)] internal double MaximumMeasurableDeviationDegrees;
        [DataMember(Name = "minimumAccuracyValue", IsRequired = true)] internal double MinimumAccuracyValue;
    }
}
