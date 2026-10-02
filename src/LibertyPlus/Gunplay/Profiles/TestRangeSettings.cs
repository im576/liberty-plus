using System.Runtime.Serialization;

// Fields are populated by DataContractJsonSerializer.
#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Profiles
{
    [DataContract]
    internal sealed class TestRangeSettings
    {
        [DataMember(Name = "targetDistancesMeters", IsRequired = true)] internal double[] TargetDistancesMeters;
        [DataMember(Name = "vehicleDistanceMeters", IsRequired = true)] internal double VehicleDistanceMeters;
        [DataMember(Name = "vehicleLateralMeters", IsRequired = true)] internal double VehicleLateralMeters;
        [DataMember(Name = "ammoRefillRounds", IsRequired = true)] internal int AmmoRefillRounds;
        [DataMember(Name = "armorRefill", IsRequired = true)] internal int ArmorRefill;
        [DataMember(Name = "healthRefill", IsRequired = true)] internal int HealthRefill;
    }
}
