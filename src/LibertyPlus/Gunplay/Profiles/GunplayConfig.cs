using System.Collections.Generic;
using System.Runtime.Serialization;

// Fields are populated by DataContractJsonSerializer.
#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Profiles
{
    // Root of config/gunplay.json. See docs/architecture/CONFIG_SCHEMA.md for units and ranges.
    [DataContract]
    internal sealed class GunplayConfig
    {
        [DataMember(Name = "schemaVersion", IsRequired = true, Order = 0)] internal int SchemaVersion;
        [DataMember(Name = "freeAim", IsRequired = true, Order = 1)] internal FreeAimSettings FreeAim;
        [DataMember(Name = "crosshair", IsRequired = true, Order = 2)] internal CrosshairSettings Crosshair;
        [DataMember(Name = "spreadCalibration", IsRequired = true, Order = 3)] internal SpreadCalibrationSettings SpreadCalibration;
        [DataMember(Name = "recoilGlobal", IsRequired = true, Order = 4)] internal RecoilGlobalSettings RecoilGlobal;
        [DataMember(Name = "movement", IsRequired = true, Order = 5)] internal MovementSettings Movement;
        [DataMember(Name = "weapons", IsRequired = true, Order = 6)] internal List<WeaponProfile> Weapons;
        [DataMember(Name = "tuning", IsRequired = true, Order = 7)] internal List<TuningParameter> Tuning;
        [DataMember(Name = "testRange", IsRequired = true, Order = 8)] internal TestRangeSettings TestRange;
        [DataMember(Name = "feel", IsRequired = true, Order = 9)] internal FeelSettings Feel;
        [DataMember(Name = "debugHit", IsRequired = true, Order = 10)] internal DebugHitSettings DebugHit;
        [DataMember(Name = "switchWhileAiming", IsRequired = true, Order = 11)] internal AimingSwitchSettings SwitchWhileAiming;
        // Optional so older gunplay.json files (and saved presets) still load; null = shoulder swap off.
        [DataMember(Name = "shoulderSwap", IsRequired = false, Order = 12)] internal ShoulderSwapSettings ShoulderSwap;
        // T-026, optional: null = refresh camera values every tick.
        [DataMember(Name = "performance", IsRequired = false, Order = 13)] internal PerformanceSettings Performance;

        // T-041, optional: null = Stage 1 catalog weapons enabled.
        [DataMember(Name = "stage1Weapons", IsRequired = false, Order = 14)] internal Stage1WeaponSettings Stage1Weapons;

        // T-043, optional: per-class and per-weapon reticles; null = the single `crosshair` for every weapon.
        [DataMember(Name = "reticles", IsRequired = false, Order = 16)] internal ReticleSettings Reticles;

        // T-042, optional: what each Stage 1 weapon class must deliver (measured by GunplaySimulation).
        [DataMember(Name = "classTargets", IsRequired = false, Order = 15)] internal System.Collections.Generic.List<ClassTargetSettings> ClassTargets;

        internal ClassTargetSettings FindClassTarget(string weaponClass)
        {
            if (ClassTargets == null || weaponClass == null) { return null; }
            foreach (ClassTargetSettings target in ClassTargets) { if (target.WeaponClass == weaponClass) { return target; } }
            return null;
        }

        internal bool Stage1WeaponsEnabled { get { return Stage1Weapons == null || Stage1Weapons.Enabled; } }

        // The registered test weapons (58+), in file order.
        internal System.Collections.Generic.List<WeaponProfile> TestWeapons()
        {
            System.Collections.Generic.List<WeaponProfile> result = new System.Collections.Generic.List<WeaponProfile>();
            if (Weapons == null) { return result; }
            foreach (WeaponProfile weapon in Weapons) { if (weapon.IsTestWeapon) { result.Add(weapon); } }
            return result;
        }

        internal WeaponProfile FindWeapon(int weaponId)
        {
            if (Weapons == null) { return null; }
            foreach (WeaponProfile weapon in Weapons)
            {
                if (weapon.WeaponId == weaponId) { return weapon; }
            }
            return null;
        }
    }
}

