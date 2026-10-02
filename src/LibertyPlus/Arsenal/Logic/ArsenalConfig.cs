using System.Collections.Generic;
using System.Runtime.Serialization;
using LibertyFramework.Arsenal.Contracts;

#pragma warning disable 0649
namespace LibertyFramework.Arsenal.Logic
{
    [DataContract]
    internal sealed class ArsenalConfig
    {
        [DataMember(Name = "schemaVersion", IsRequired = true)] internal int SchemaVersion;
        [DataMember(Name = "sidearmLimit", IsRequired = true)] internal int SidearmLimit;
        [DataMember(Name = "longGunLimit", IsRequired = true)] internal int LongGunLimit;
        [DataMember(Name = "meleeLimit", IsRequired = true)] internal int MeleeLimit;
        [DataMember(Name = "purchaseWindowMilliseconds", IsRequired = true)] internal int PurchaseWindowMilliseconds;
        [DataMember(Name = "trunkDistanceMeters", IsRequired = true)] internal float TrunkDistanceMeters;
        [DataMember(Name = "trunkRearOffsetMeters", IsRequired = true)] internal float TrunkRearOffsetMeters;
        [DataMember(Name = "ownedVehicleMatchMeters", IsRequired = true)] internal float OwnedVehicleMatchMeters;
        [DataMember(Name = "fallbackVehicleMatchMeters", IsRequired = true)] internal float FallbackVehicleMatchMeters;
        [DataMember(Name = "categories", IsRequired = true)] internal List<CategoryRule> Categories;
        [DataMember(Name = "safehouses", IsRequired = true)] internal List<SafehouseRule> Safehouses;
        // Radar sprite of the game's own safehouse blips (ScriptHookDotNet BlipIcon.Building_Safehouse = 29);
        // 0 disables discovery. Discovered safehouses join the configured list for the current episode.
        [DataMember(Name = "safehouseBlipSprite", IsRequired = false)] internal int SafehouseBlipSprite;
        [DataMember(Name = "discoveredSafehouseRadiusMeters", IsRequired = false)] internal float DiscoveredSafehouseRadiusMeters;
        [DataMember(Name = "gunsmithGoldFinishPrice", IsRequired = false)] internal int GunsmithGoldFinishPrice;
        // ADR-0006 cost control: the weapon inventory is re-read on engine weapon/shot/reload events and at least this
        // often (pickups and purchases change it without an event); arrest/death/mission/cutscene/fade state at most
        // this often. 0 or absent = the defaults (500 / 500 ms).
        [DataMember(Name = "inventoryRefreshMilliseconds", IsRequired = false)] internal int InventoryRefreshMilliseconds;
        [DataMember(Name = "stateRefreshMilliseconds", IsRequired = false)] internal int StateRefreshMilliseconds;
        [DataMember(Name = "trunkTimings", IsRequired = true)] internal TrunkTimings TrunkTimings;
        // T-044: the Stage 1 loadout; absent in files that predate it (general limits apply then).
        [DataMember(Name = "loadout", IsRequired = false)] internal LoadoutRules Loadout;
        // T-045: weapon wheel bindings; absent in files that predate it (WeaponWheelConfig.Defaults then).
        [DataMember(Name = "weaponWheel", IsRequired = false)] internal WeaponWheelConfig WeaponWheel;
        // T-046: trunk capacity per vehicle class; absent in files that predate it (TrunkCapacityRules.Defaults then).
        [DataMember(Name = "trunkCapacity", IsRequired = false)] internal TrunkCapacityRules TrunkCapacity;
    }

}
