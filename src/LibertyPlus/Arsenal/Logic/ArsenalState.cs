using System.Collections.Generic;
using System.Runtime.Serialization;
using LibertyFramework.Arsenal.Contracts;

#pragma warning disable 0649
namespace LibertyFramework.Arsenal.Logic
{
    [DataContract]
    internal sealed class ArsenalState
    {
        [DataMember(Name = "schemaVersion", IsRequired = true)] internal int SchemaVersion = 1;
        [DataMember(Name = "lastSafehouseId", IsRequired = true)] internal string LastSafehouseId = "";
        [DataMember(Name = "lastVehicleKey", IsRequired = true)] internal string LastVehicleKey = "";
        [DataMember(Name = "fallbackModelHash", IsRequired = true)] internal int FallbackModelHash;
        [DataMember(Name = "fallbackX", IsRequired = true)] internal float FallbackX;
        [DataMember(Name = "fallbackY", IsRequired = true)] internal float FallbackY;
        [DataMember(Name = "fallbackZ", IsRequired = true)] internal float FallbackZ;
        [DataMember(Name = "ownedCarried", IsRequired = true)] internal List<int> OwnedCarried = new List<int>();
        [DataMember(Name = "safehouseStashes", IsRequired = true)] internal List<StorageBin> SafehouseStashes = new List<StorageBin>();
        [DataMember(Name = "vehicleTrunks", IsRequired = true)] internal List<StorageBin> VehicleTrunks = new List<StorageBin>();
        [DataMember(Name = "carriedRecords", IsRequired = false)] internal List<WeaponRecord> CarriedRecords = new List<WeaponRecord>();
    }

}
