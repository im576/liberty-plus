using System;
using System.Collections.Generic;
using System.IO;
using LibertyFramework.Arsenal.Contracts;
using LibertyFramework.Arsenal.Logic;
using LibertyFramework.Core.Config;

namespace LibertyFramework.Verify
{
    // Offline tests for the trunk interface rules (T-046): trunk capacity per vehicle class, store refusal when full, and
    // which carried weapon a take swaps out.
    internal static class TrunkUiChecks
    {
        internal static void Run(string repoRoot, Checker check)
        {
            ArsenalConfig config = JsonStore.Load<ArsenalConfig>(Path.Combine(repoRoot, "config/arsenal.json"));
            ArsenalConfigValidator.Validate(config);
            TrunkCapacityRules rules = config.TrunkCapacity;
            check.True("trunk capacity ships with a default, a stash size and vehicle classes", rules != null && rules.DefaultSlots >= 1 && rules.Classes.Count >= 2, "");
            check.True("trunk capacity: a sports car holds less than the default, a van more",
                rules.SlotsFor("banshee") < rules.DefaultSlots && rules.SlotsFor("speedo") > rules.DefaultSlots && rules.SlotsFor("admiral") == rules.DefaultSlots &&
                rules.SlotsFor("BANSHEE") == rules.SlotsFor("banshee"), "");
            check.True("a file without the trunkCapacity block uses the defaults (8 slots, unlimited stash)",
                TrunkCapacityRules.Defaults().SlotsFor("anything") == 8 && TrunkCapacityRules.Defaults().SafehouseSlots == 0, "");
            check.True("store is refused at capacity and allowed below it, 0 means unlimited",
                !TrunkCapacityRules.CanStore(4, 4) && TrunkCapacityRules.CanStore(3, 4) && TrunkCapacityRules.CanStore(500, 0), "");
            Rejected(check, repoRoot, "trunk class with 0 slots is rejected", c => c.TrunkCapacity.Classes[0].Slots = 0);
            Rejected(check, repoRoot, "a model in two trunk classes is rejected", c => c.TrunkCapacity.Classes[1].Models.Add("banshee"));
            Rejected(check, repoRoot, "default trunk size 0 is rejected", c => c.TrunkCapacity.DefaultSlots = 0);

            Dictionary<int, long> used = new Dictionary<int, long>(); used[10] = 100; used[14] = 50; used[12] = 10;
            List<WeaponRecord> carried = new List<WeaponRecord>();
            carried.Add(Make(7, WeaponCategory.Handgun)); carried.Add(Make(10, WeaponCategory.Shotgun)); carried.Add(Make(14, WeaponCategory.Rifle));
            check.True("take of a long gun with both long-gun slots full swaps out the least recently used one (the rifle)",
                carried[ArsenalPolicy.DisplacedOnTake(config, carried, Make(12, WeaponCategory.SMG), used)].WeaponId == 14, "");
            carried.RemoveAt(2);
            check.True("take with a free long-gun slot is a plain take", ArsenalPolicy.DisplacedOnTake(config, carried, Make(12, WeaponCategory.SMG), used) == -1, "");
            check.True("take of the same category swaps that weapon out first",
                carried[ArsenalPolicy.DisplacedOnTake(config, carried, Make(11, WeaponCategory.Shotgun), used)].WeaponId == 10, "");
            check.True("take of a handgun swaps the carried handgun (one sidearm)",
                carried[ArsenalPolicy.DisplacedOnTake(config, carried, Make(9, WeaponCategory.Handgun), used)].WeaponId == 7, "");
            check.True("take of a thrown weapon never displaces anything", ArsenalPolicy.DisplacedOnTake(config, carried, Make(4, WeaponCategory.Thrown), used) == -1, "");
            config.Loadout = null;
            carried.Add(Make(14, WeaponCategory.Rifle));
            check.True("without the loadout the general rules apply (SMG is a sidearm: handgun + SMG fit, a second SMG category does not exist)",
                ArsenalPolicy.DisplacedOnTake(config, carried, Make(12, WeaponCategory.SMG), used) == -1, "");
        }

        private static void Rejected(Checker check, string repoRoot, string name, Action<ArsenalConfig> mutate)
        {
            ArsenalConfig config = JsonStore.Load<ArsenalConfig>(Path.Combine(repoRoot, "config/arsenal.json"));
            mutate(config);
            bool rejected = false;
            try { ArsenalConfigValidator.Validate(config); }
            catch (InvalidDataException) { rejected = true; }
            check.True(name, rejected, "");
        }

        private static WeaponRecord Make(int id, WeaponCategory category)
        {
            WeaponRecord record = new WeaponRecord();
            record.WeaponId = id; record.Category = category; record.Ammo = 100;
            return record;
        }
    }
}
