using System;
using System.Collections.Generic;
using System.IO;
using LibertyFramework.Arsenal.Contracts;
using LibertyFramework.Arsenal.Logic;
using LibertyFramework.Core.Config;
using LibertyFramework.Weapons.Logic;

namespace LibertyFramework.Verify
{
    internal static class Phase2SystemsChecks
    {
        internal static void Run(string root, Checker check)
        {
            WeaponCatalog catalog = JsonStore.Load<WeaponCatalog>(Path.Combine(root, "config/weapon-catalog.json"));
            catalog.Validate();
            check.True("catalog has service pistol replacement pair", catalog.Find(7).Family == catalog.Find(9).Family &&
                catalog.Find(7).Role == "replacement" && catalog.Find(9).Role == "replacement", "");
            check.True("catalog registers existing CE add-on", catalog.Find(59).Role == "add-on" && catalog.Find(59).Model == "lf_gold_carbine", "");
            AttachmentOption grip = catalog.FindAttachment("match-grip");
            check.True("gold pistol has a priced match grip with bounded bloom effect", grip != null &&
                grip.Price > 0 && grip.PerShotBloomMultiplier > 0 && grip.PerShotBloomMultiplier < 1 &&
                catalog.Find(58).Attachments.Contains(grip.Id), "");

            ArsenalState state = new ArsenalState();
            WeaponRecord weapon = new WeaponRecord(); weapon.WeaponId = 7; weapon.Owned = true; weapon.Finish = "factory"; weapon.Ammo = 47;
            weapon.Attachments = new List<string>(); weapon.Attachments.Add("match-grip"); weapon.Progression = 3;
            WeaponIdentity.Ensure(weapon);
            string originalId = weapon.InstanceId;
            state.CarriedRecords.Add(weapon);
            WeaponRecord sameType = weapon.Clone(); sameType.InstanceId = Guid.NewGuid().ToString("N");
            check.True("same weapon type cannot replace a carried physical instance on take",
                !WeaponIdentity.CanTake(state.CarriedRecords, sameType), "");
            string folder = Path.Combine(Path.GetTempPath(), "lf_phase2_verify_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string path = Path.Combine(folder, "arsenal.json");
                JsonStore.Save(path, state);
                ArsenalState loaded = ArsenalStateStore.LoadOrEmpty(path, error => { throw error; });
                WeaponRecord carried = loaded.CarriedRecords[0];
                check.True("physical weapon metadata survives state round trip", carried.InstanceId == originalId &&
                    carried.Finish == "factory" && carried.Progression == 3 && carried.Attachments[0] == "match-grip" && carried.Owned && carried.Ammo == 47, "");
                WeaponRecord transferred = carried.Clone(); loaded.CarriedRecords.Clear();
                ArsenalPolicy.FindOrAdd(loaded.VehicleTrunks, "lvs:owned_42").Weapons.Add(transferred);
                JsonStore.Save(path, loaded);
                ArsenalState restored = ArsenalStateStore.LoadOrEmpty(path, error => { throw error; });
                check.True("trunk transfer retains physical identity", restored.CarriedRecords.Count == 0 &&
                    restored.VehicleTrunks[0].Weapons[0].InstanceId == originalId && restored.VehicleTrunks[0].Weapons[0].Ammo == 47 && restored.VehicleTrunks[0].Weapons[0].Owned, "");
                WeaponRecord fromTrunk = restored.VehicleTrunks[0].Weapons[0];
                restored.VehicleTrunks[0].Weapons.Clear();
                ArsenalPolicy.FindOrAdd(restored.SafehouseStashes, "home").Weapons.Add(fromTrunk);
                JsonStore.Save(path, restored);
                ArsenalState atHome = ArsenalStateStore.LoadOrEmpty(path, error => { throw error; });
                check.True("safehouse transfer retains physical identity and metadata", atHome.SafehouseStashes[0].Weapons[0].InstanceId == originalId &&
                    atHome.SafehouseStashes[0].Weapons[0].Attachments[0] == "match-grip" && atHome.SafehouseStashes[0].Weapons[0].Ammo == 47 && atHome.SafehouseStashes[0].Weapons[0].Owned, "");
                WeaponRecord upgraded = atHome.SafehouseStashes[0].Weapons[0];
                upgraded.WeaponId = 58; upgraded.Finish = "gold-test";
                upgraded.CatalogId = "gold-test-pistol"; upgraded.Progression = 1;
                JsonStore.Save(path, atHome);
                ArsenalState afterUpgrade = ArsenalStateStore.LoadOrEmpty(path, error => { throw error; });
                check.True("gunsmith finish upgrade retains identity and unlock in storage", afterUpgrade.SafehouseStashes[0].Weapons[0].InstanceId == originalId &&
                    afterUpgrade.SafehouseStashes[0].Weapons[0].WeaponId == 58 && afterUpgrade.SafehouseStashes[0].Weapons[0].Progression == 1, "");
                restored.VehicleTrunks[0].Weapons.Add(transferred.Clone());
                WeaponIdentity.Normalize(restored);
                check.True("duplicate instance IDs repaired", restored.SafehouseStashes[0].Weapons[0].InstanceId !=
                    restored.VehicleTrunks[0].Weapons[0].InstanceId, "");

                List<WeaponRecord> loss = new List<WeaponRecord>();
                loss.Add(carried.Clone());
                StorageBin lossBin = new StorageBin(); lossBin.Id = "loss-home";
                ArsenalPolicy.ResolveLoss(loss, false, lossBin);
                ArsenalState wasted = new ArsenalState();
                wasted.CarriedRecords = loss;
                wasted.SafehouseStashes.Add(lossBin);
                JsonStore.Save(path, wasted);
                ArsenalState afterWasted = ArsenalStateStore.LoadOrEmpty(path, error => { throw error; });
                check.True("wasted clears carried snapshot and retains owned instance in safehouse",
                    afterWasted.CarriedRecords.Count == 0 && afterWasted.SafehouseStashes[0].Weapons[0].InstanceId == originalId, "");
                List<WeaponRecord> bust = new List<WeaponRecord>(); bust.Add(carried.Clone());
                ArsenalPolicy.ResolveLoss(bust, true, lossBin);
                check.True("busted clears carried snapshot without adding stash copy", bust.Count == 0 && lossBin.Weapons.Count == 1, "");
            }
            finally { Directory.Delete(folder, true); }
        }
    }
}
