using System;
using System.Collections.Generic;
using System.IO;
using LibertyFramework.Arsenal.Contracts;
using LibertyFramework.Arsenal.Holsters.Logic;
using LibertyFramework.Arsenal.Logic;
using LibertyFramework.Core.Config;

namespace LibertyFramework.Verify
{
    // Offline tests for the Stage 1 physical loadout (T-044): 1 sidearm + 2 long guns, ammunition caps, the holster outfit
    // classes and placements, and the inventory-integrity invariants (death cycles, save/load).
    internal static class LoadoutChecks
    {
        internal static void Run(string repoRoot, Checker check)
        {
            ArsenalConfig config = JsonStore.Load<ArsenalConfig>(Path.Combine(repoRoot, "config/arsenal.json"));
            ArsenalConfigValidator.Validate(config);
            check.True("loadout block ships enabled with 1 sidearm and 2 long guns", ArsenalPolicy.LoadoutActive(config) &&
                ArsenalPolicy.Limit(config, "sidearm") == 1 && ArsenalPolicy.Limit(config, "longGun") == 2, "");
            check.True("loadout counts SMGs as long guns on a long-gun slot", ArsenalPolicy.Group(config, WeaponCategory.SMG) == "longGun" &&
                ArsenalPolicy.Slot(config, WeaponCategory.SMG) == BodySlot.LongGun1, "");
            check.True("handgun stays the one sidearm", ArsenalPolicy.Group(config, WeaponCategory.Handgun) == "sidearm" &&
                ArsenalPolicy.Slot(config, WeaponCategory.Handgun) == BodySlot.SidearmPrimary, "");

            Dictionary<int, long> used = new Dictionary<int, long>(); used[10] = 50; used[12] = 10; used[14] = 30;
            List<WeaponRecord> carried = new List<WeaponRecord>();
            carried.Add(Make(7, WeaponCategory.Handgun)); carried.Add(Make(10, WeaponCategory.Shotgun)); carried.Add(Make(12, WeaponCategory.SMG));
            check.True("pistol + shotgun + SMG fit the loadout", ArsenalPolicy.OverflowIndex(config, carried, used) == -1, "");
            carried.Add(Make(14, WeaponCategory.Rifle));
            check.True("a third long gun overflows the least recently used one (the SMG)", ArsenalPolicy.OverflowIndex(config, carried, used) == 2, "");
            carried.RemoveAt(3);
            config.Loadout.Enabled = false;
            check.True("switching the loadout off restores the general SMG-as-sidearm rule", ArsenalPolicy.Group(config, WeaponCategory.SMG) == "sidearm" &&
                ArsenalPolicy.Limit(config, "sidearm") == 2 && ArsenalPolicy.AmmoCap(config, WeaponCategory.Rifle) == -1, "");
            config.Loadout.Enabled = true;
            config.Loadout = null;
            check.True("a file without a loadout block behaves as before (2 + 2 + 1)", !ArsenalPolicy.LoadoutActive(config) &&
                ArsenalPolicy.Limit(config, "longGun") == 2 && ArsenalPolicy.Group(config, WeaponCategory.SMG) == "sidearm", "");
            config = JsonStore.Load<ArsenalConfig>(Path.Combine(repoRoot, "config/arsenal.json"));

            check.True("ammo cap: the configured category is limited, melee and thrown never", ArsenalPolicy.AmmoCap(config, WeaponCategory.Rifle) > 0 &&
                ArsenalPolicy.AmmoCap(config, WeaponCategory.Melee) == -1 && ArsenalPolicy.AmmoCap(config, WeaponCategory.Thrown) == -1, "");
            check.True("ammo caps leave a full magazine for every firearm", ArsenalPolicy.AmmoCap(config, WeaponCategory.Handgun) >= 30 &&
                ArsenalPolicy.AmmoCap(config, WeaponCategory.Shotgun) >= 12 && ArsenalPolicy.AmmoCap(config, WeaponCategory.Rifle) >= 60, "");

            Rejected(check, repoRoot, "a loadout sidearm limit above 2 is rejected", c => c.Loadout.SidearmLimit = 3);
            Rejected(check, repoRoot, "an SMG override on a sidearm slot with the long-gun group is rejected", c => c.Loadout.CategoryOverrides[0].BodySlot = BodySlot.SidearmPrimary);
            Rejected(check, repoRoot, "a handgun override to the long-gun group is rejected", c =>
            {
                CategoryRule rule = new CategoryRule(); rule.Category = WeaponCategory.Handgun; rule.Group = "longGun"; rule.BodySlot = BodySlot.LongGun1;
                c.Loadout.CategoryOverrides.Add(rule);
            });
            Rejected(check, repoRoot, "a duplicate ammo cap category is rejected", c => c.Loadout.AmmoCaps.Add(c.Loadout.AmmoCaps[0]));
            Rejected(check, repoRoot, "an ammo cap on thrown weapons is rejected", c =>
            {
                AmmoCap cap = new AmmoCap(); cap.Category = WeaponCategory.Thrown; cap.MaximumRounds = 5;
                c.Loadout.AmmoCaps.Add(cap);
            });

            DeathCycles(repoRoot, check);
            Holsters(repoRoot, check);
        }

        // Inventory integrity: 50 wasted cycles with owned weapons lose nothing, and the state survives a save/load unchanged.
        private static void DeathCycles(string repoRoot, Checker check)
        {
            StorageBin stash = new StorageBin(); stash.Id = "safehouse";
            int ownedAtDeath = 0;
            for (int cycle = 0; cycle < 50; cycle++)
            {
                List<WeaponRecord> carried = new List<WeaponRecord>();
                WeaponRecord pistol = Make(7, WeaponCategory.Handgun); pistol.Owned = true; carried.Add(pistol);
                WeaponRecord rifle = Make(14, WeaponCategory.Rifle); rifle.Owned = cycle % 2 == 0; carried.Add(rifle);
                WeaponRecord found = Make(10, WeaponCategory.Shotgun); found.Owned = false; carried.Add(found);
                foreach (WeaponRecord record in carried) { if (record.Owned) { ownedAtDeath++; } }
                ArsenalPolicy.ResolveLoss(carried, false, stash);
            }
            check.True("50 wasted cycles: every owned weapon is in the safehouse stash, none lost", stash.Weapons.Count == ownedAtDeath && ownedAtDeath == 75, "stash " + stash.Weapons.Count + " owned " + ownedAtDeath);
            StorageBin busted = new StorageBin(); busted.Id = "safehouse";
            for (int cycle = 0; cycle < 50; cycle++)
            {
                List<WeaponRecord> carried = new List<WeaponRecord>();
                WeaponRecord pistol = Make(7, WeaponCategory.Handgun); pistol.Owned = true; carried.Add(pistol);
                ArsenalPolicy.ResolveLoss(carried, true, busted);
            }
            check.True("50 busted cycles: the bust rule is unchanged (owned weapons are confiscated, nothing duplicated)", busted.Weapons.Count == 0, "");

            ArsenalState state = new ArsenalState();
            state.SafehouseStashes.Add(stash);
            StorageBin trunk = new StorageBin(); trunk.Id = "lvs:owned_1";
            WeaponRecord stored = Make(14, WeaponCategory.Rifle); stored.Owned = true; stored.Ammo = 240; trunk.Weapons.Add(stored);
            state.VehicleTrunks.Add(trunk);
            WeaponRecord inHand = Make(12, WeaponCategory.SMG); inHand.Owned = true; state.CarriedRecords.Add(inHand);
            state.OwnedCarried.Add(12);
            WeaponIdentity.Normalize(state);
            string folder = Path.Combine(Path.GetTempPath(), "lf_loadout_verify_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string first = Path.Combine(folder, "a.json"), second = Path.Combine(folder, "b.json");
                JsonStore.Save(first, state);
                ArsenalState loaded = ArsenalStateStore.LoadOrEmpty(first, error => { throw error; });
                JsonStore.Save(second, loaded);
                check.True("save/load gives an identical arsenal state (byte for byte after a second save)",
                    File.ReadAllText(first) == File.ReadAllText(second) && loaded.SafehouseStashes[0].Weapons.Count == ownedAtDeath &&
                    loaded.VehicleTrunks[0].Weapons[0].Ammo == 240 && loaded.CarriedRecords[0].WeaponId == 12, "");
            }
            finally { Directory.Delete(folder, true); }
        }

        private static void Holsters(string repoRoot, Checker check)
        {
            HolsterConfig holsters = JsonStore.Load<HolsterConfig>(Path.Combine(repoRoot, "config/holsters.json"));
            holsters.Validate();
            check.True("holsters.json validates with its outfit classes and loadout placements", holsters.Enabled, "");

            // An old file (no blocks) still loads and resolves to the default outfit.
            HolsterConfig old = JsonStore.Load<HolsterConfig>(Path.Combine(repoRoot, "config/holsters.json"));
            old.OutfitClasses = null; old.LoadoutPlacements = null;
            old.Validate();
            check.True("a holsters.json without outfit classes or loadout placements still validates",
                old.OutfitFor(new Dictionary<int, int>()) == HolsterConfig.DefaultOutfit &&
                old.FindPlacement(BodySlot.LongGun1, WeaponCategory.SMG, "x", "anything") != null, "");

            HolsterPlacement rifle = holsters.FindPlacement(BodySlot.LongGun2, WeaponCategory.Rifle, "w_ak47", HolsterConfig.DefaultOutfit);
            HolsterPlacement smg = holsters.FindPlacement(BodySlot.LongGun1, WeaponCategory.SMG, "w_mp5", HolsterConfig.DefaultOutfit);
            check.True("every long-gun class resolves a placement for both slots",
                rifle != null && smg != null && holsters.FindPlacement(BodySlot.LongGun1, WeaponCategory.Shotgun, "w_shotgun", HolsterConfig.DefaultOutfit) != null &&
                holsters.FindPlacement(BodySlot.LongGun2, WeaponCategory.SMG, "w_mp5", HolsterConfig.DefaultOutfit) != null, "");

            // Two slung guns never share a body position: the two long-gun slots differ in offset (and mirror in rotation).
            bool apart = true;
            foreach (WeaponCategory category in new WeaponCategory[] { WeaponCategory.Shotgun, WeaponCategory.SMG, WeaponCategory.Rifle, WeaponCategory.Sniper, WeaponCategory.Heavy })
            {
                foreach (HolsterOutfitClass outfitClass in OutfitsIncludingDefault(holsters))
                {
                    HolsterPlacement a = holsters.FindPlacement(BodySlot.LongGun1, category, "m", outfitClass.Id);
                    HolsterPlacement b = holsters.FindPlacement(BodySlot.LongGun2, category, "m", outfitClass.Id);
                    float dx = a.Position[0] - b.Position[0], dy = a.Position[1] - b.Position[1], dz = a.Position[2] - b.Position[2];
                    if (a.Bone != b.Bone || Math.Sqrt(dx * dx + dy * dy + dz * dz) < 0.2) { apart = false; }
                }
            }
            check.True("the two slung long guns sit at least 0.2 m apart for every weapon class and outfit class", apart, "");

            // Specificity: outfit outranks model outranks category outranks the slot default.
            HolsterConfig synthetic = JsonStore.Load<HolsterConfig>(Path.Combine(repoRoot, "config/holsters.json"));
            synthetic.OutfitClasses = new List<HolsterOutfitClass>();
            HolsterOutfitClass coat = new HolsterOutfitClass(); coat.Id = "coat"; coat.Component = 1; coat.Drawables = new int[] { 4, 5 };
            synthetic.OutfitClasses.Add(coat);
            synthetic.LoadoutPlacements = new List<HolsterPlacement>();
            synthetic.LoadoutPlacements.Add(Placement("LongGun1", "Rifle", null, null, 0.11f));
            synthetic.LoadoutPlacements.Add(Placement("LongGun1", null, "w_ak47", null, 0.22f));
            synthetic.LoadoutPlacements.Add(Placement("LongGun1", null, null, "coat", 0.33f));
            synthetic.Validate();
            check.True("placement specificity: outfit > model > category > slot default",
                synthetic.FindPlacement(BodySlot.LongGun1, WeaponCategory.Rifle, "w_ak47", "coat").Position[0] == 0.33f &&
                synthetic.FindPlacement(BodySlot.LongGun1, WeaponCategory.Rifle, "w_ak47", "default").Position[0] == 0.22f &&
                synthetic.FindPlacement(BodySlot.LongGun1, WeaponCategory.Rifle, "w_m4", "default").Position[0] == 0.11f &&
                synthetic.FindPlacement(BodySlot.LongGun1, WeaponCategory.Shotgun, "w_shotgun", "default").Position[0] == -0.16f, "");
            Dictionary<int, int> worn = new Dictionary<int, int>(); worn[1] = 5;
            check.True("outfit class comes from the worn drawable of its component", synthetic.OutfitFor(worn) == "coat", "");
            worn[1] = 9;
            check.True("an unlisted drawable is the default outfit", synthetic.OutfitFor(worn) == "default" && synthetic.OutfitFor(new Dictionary<int, int>()) == "default", "");
            synthetic.LoadoutPlacements.Add(Placement("LongGun1", null, null, "no_such_class", 0f));
            bool rejected = false;
            try { synthetic.Validate(); } catch (InvalidDataException) { rejected = true; }
            check.True("a placement for an unknown outfit class is rejected", rejected, "");
        }

        private static IEnumerable<HolsterOutfitClass> OutfitsIncludingDefault(HolsterConfig holsters)
        {
            HolsterOutfitClass fallback = new HolsterOutfitClass(); fallback.Id = HolsterConfig.DefaultOutfit;
            yield return fallback;
            if (holsters.OutfitClasses != null) { foreach (HolsterOutfitClass outfitClass in holsters.OutfitClasses) { yield return outfitClass; } }
        }

        private static HolsterPlacement Placement(string slot, string category, string model, string outfit, float x)
        {
            HolsterPlacement placement = new HolsterPlacement();
            placement.Slot = slot; placement.Bone = "Spine2"; placement.Category = category; placement.Model = model; placement.Outfit = outfit;
            placement.Position = new float[] { x, 0, 0 }; placement.Rotation = new float[] { 0, 0, 0 };
            return placement;
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
