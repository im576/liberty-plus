using System;
using System.Collections.Generic;
using System.IO;
using LibertyFramework.Arsenal.Contracts;
using LibertyFramework.Arsenal.Logic;
using LibertyFramework.Core.Config;

namespace LibertyFramework.Verify
{
    // Offline tests for Arsenal Core (T-020). Owned by Agent A; add checks here.
    internal static class ArsenalCoreChecks
    {
        internal static void Run(string repoRoot, Checker check)
        {
            WeaponRecord record = new WeaponRecord();
            record.WeaponId = 58;
            record.Owned = true;
            check.True("arsenal contract: weapon record clones", record.Clone().WeaponId == 58 && record.Clone() != record, "");
            ArsenalConfig config = JsonStore.Load<ArsenalConfig>(Path.Combine(repoRoot, "config/arsenal.json"));
            ArsenalConfigValidator.Validate(config);
            check.True("arsenal config validates", config.LongGunLimit == 2 && config.Categories.Count == 8, "");
            check.True("arsenal trunk timings come from config", config.TrunkTimings != null && config.TrunkTimings.TurnMilliseconds == 450 &&
                config.TrunkTimings.CloseMaxMilliseconds == 2400, "");
            int openMax = config.TrunkTimings.OpenMaxMilliseconds;
            config.TrunkTimings.OpenMaxMilliseconds = config.TrunkTimings.OpenMinMilliseconds - 1;
            bool rejected = false;
            try { ArsenalConfigValidator.Validate(config); } catch (InvalidDataException) { rejected = true; }
            check.True("arsenal trunk timings with min > max are rejected", rejected, "");
            config.TrunkTimings.OpenMaxMilliseconds = openMax;
            List<WeaponRecord> weapons = new List<WeaponRecord>();
            weapons.Add(Make(10, WeaponCategory.Shotgun, false));
            weapons.Add(Make(14, WeaponCategory.Rifle, false));
            weapons.Add(Make(16, WeaponCategory.Sniper, false));
            Dictionary<int, long> used = new Dictionary<int, long>(); used[10] = 50; used[14] = 10; used[16] = 100;
            check.True("long gun cap spans shotgun rifle sniper", ArsenalPolicy.OverflowIndex(config, weapons, used) == 1, "");
            weapons.RemoveAt(1); weapons.Add(Make(18, WeaponCategory.Heavy, false));
            check.True("heavy counts toward long gun cap", ArsenalPolicy.OverflowIndex(config, weapons, used) == 2, "");
            weapons.Clear(); weapons.Add(Make(7, WeaponCategory.Handgun, false)); weapons.Add(Make(12, WeaponCategory.SMG, false));
            check.True("handgun plus SMG fit sidearm cap", ArsenalPolicy.OverflowIndex(config, weapons, used) == -1, "");
            check.True("mission and cutscene defer moves", !ArsenalPolicy.MayMoveWeapons(true, false) &&
                !ArsenalPolicy.MayMoveWeapons(false, true) && ArsenalPolicy.MayMoveWeapons(false, false), "");
            check.True("purchase within money window becomes owned", ArsenalPolicy.IsOwnedGain(false, false, 1200, 1000, config.PurchaseWindowMilliseconds), "");
            check.True("late pickup remains unowned", !ArsenalPolicy.IsOwnedGain(false, false, 4000, 1000, config.PurchaseWindowMilliseconds), "");
            check.True("stash take becomes owned", ArsenalPolicy.IsOwnedGain(true, false, 4000, -1, config.PurchaseWindowMilliseconds), "");
            check.True("mission gain remains unowned", !ArsenalPolicy.IsOwnedGain(false, true, 1200, 1000, config.PurchaseWindowMilliseconds), "");
            List<WeaponRecord> loss = new List<WeaponRecord>(); loss.Add(Make(7, WeaponCategory.Handgun, true)); loss.Add(Make(12, WeaponCategory.SMG, false));
            StorageBin stash = new StorageBin(); stash.Id = "home";
            ArsenalPolicy.ResolveLoss(loss, true, stash);
            check.True("bust clears all with no stash transfer", loss.Count == 0 && stash.Weapons.Count == 0, "");
            loss.Add(Make(7, WeaponCategory.Handgun, true)); loss.Add(Make(12, WeaponCategory.SMG, false));
            ArsenalPolicy.ResolveLoss(loss, false, stash);
            check.True("wasted sends owned only to safehouse", loss.Count == 0 && stash.Weapons.Count == 1 && stash.Weapons[0].WeaponId == 7, "");
            string sample = "[owned.owned_42]\nmodel=sentinel\nepisode=iv\nmodelhash=12345\nx=12.5\ny=20\nz=3\ndestroyed=false\n";
            List<LvsOwnedVehicleEntry> entries = LvsOwnedVehicleReader.Parse(sample);
            check.True("LVS owned INI parses ID episode model and position", entries.Count == 1 &&
                LvsOwnedVehicleReader.Match(entries, "iv", 12345, 12.6f, 20, 3, 2) == "owned_42", "");
            string folder = Path.Combine(Path.GetTempPath(), "lf_arsenal_verify_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string path = Path.Combine(folder, "arsenal_iv.json");
                ArsenalState state = new ArsenalState();
                ArsenalPolicy.FindOrAdd(state.SafehouseStashes, "home").Weapons.Add(Make(7, WeaponCategory.Handgun, true));
                JsonStore.Save(path, state);
                ArsenalState restored = ArsenalStateStore.LoadOrEmpty(path, error => { throw error; });
                check.True("Arsenal state JSON round-trip", restored.SafehouseStashes.Count == 1 && restored.SafehouseStashes[0].Weapons[0].WeaponId == 7, "");
                JsonStore.Save(path, restored);
                File.WriteAllText(path, "{bad json");
                int errors = 0;
                ArsenalState empty = ArsenalStateStore.LoadOrEmpty(path, error => errors++);
                check.True("corrupt state starts empty and preserves .bak", errors == 1 && empty.SafehouseStashes.Count == 0 &&
                    File.Exists(path + ".bak") && Directory.GetFiles(folder, "*.corrupt_*").Length == 1, "");
            }
            finally { Directory.Delete(folder, true); }
        }

        private static WeaponRecord Make(int id, WeaponCategory category, bool owned)
        {
            WeaponRecord record = new WeaponRecord(); record.WeaponId = id; record.Category = category; record.Owned = owned; record.Ammo = 20;
            return record;
        }
    }
}
