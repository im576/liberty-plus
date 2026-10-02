using System;
using System.Collections.Generic;
using LibertyFramework.Arsenal.Contracts;

namespace LibertyFramework.Arsenal.Logic
{
    internal static class ArsenalPolicy
    {
        internal static bool MayMoveWeapons(bool mission, bool cutsceneOrFade)
        {
            return !mission && !cutsceneOrFade;
        }
        internal static bool LoadoutActive(ArsenalConfig config)
        {
            return config.Loadout != null && config.Loadout.Enabled;
        }

        // The loadout's override of a category wins while the loadout is active; otherwise the general mapping applies.
        private static CategoryRule RuleFor(ArsenalConfig config, WeaponCategory category)
        {
            if (LoadoutActive(config) && config.Loadout.CategoryOverrides != null)
            {
                foreach (CategoryRule rule in config.Loadout.CategoryOverrides) { if (rule.Category == category) { return rule; } }
            }
            foreach (CategoryRule rule in config.Categories) { if (rule.Category == category) { return rule; } }
            return null;
        }

        internal static string Group(ArsenalConfig config, WeaponCategory category)
        {
            CategoryRule rule = RuleFor(config, category);
            return rule == null ? "uncounted" : rule.Group;
        }

        internal static BodySlot Slot(ArsenalConfig config, WeaponCategory category)
        {
            CategoryRule rule = RuleFor(config, category);
            return rule == null ? BodySlot.None : rule.BodySlot;
        }

        internal static int Limit(ArsenalConfig config, string group)
        {
            bool loadout = LoadoutActive(config);
            if (group == "sidearm") { return loadout ? config.Loadout.SidearmLimit : config.SidearmLimit; }
            if (group == "longGun") { return loadout ? config.Loadout.LongGunLimit : config.LongGunLimit; }
            return config.MeleeLimit;
        }

        // Most rounds the loadout lets Niko carry for a category; -1 when unlimited (no loadout, or no entry for it).
        internal static int AmmoCap(ArsenalConfig config, WeaponCategory category)
        {
            if (!LoadoutActive(config) || config.Loadout.AmmoCaps == null) { return -1; }
            foreach (AmmoCap cap in config.Loadout.AmmoCaps) { if (cap.Category == category) { return cap.MaximumRounds; } }
            return -1;
        }

        internal static int OverflowIndex(ArsenalConfig config, IList<WeaponRecord> carried, IDictionary<int, long> lastUsed)
        {
            foreach (string group in new string[] { "sidearm", "longGun", "melee" })
            {
                int count = 0;
                int oldest = -1;
                long age = long.MaxValue;
                for (int index = 0; index < carried.Count; index++)
                {
                    if (Group(config, carried[index].Category) != group) { continue; }
                    count++;
                    long used;
                    if (!lastUsed.TryGetValue(carried[index].WeaponId, out used)) { used = 0; }
                    if (oldest < 0 || used < age) { oldest = index; age = used; }
                }
                if (count > Limit(config, group)) { return oldest; }
            }
            return -1;
        }

        internal static bool IsOwnedGain(bool fromStorage, bool mission, long gainMilliseconds, long lastMoneyDecreaseMilliseconds, int windowMilliseconds)
        {
            return fromStorage || (!mission && lastMoneyDecreaseMilliseconds >= 0 && gainMilliseconds >= lastMoneyDecreaseMilliseconds &&
                gainMilliseconds - lastMoneyDecreaseMilliseconds <= windowMilliseconds);
        }

        internal static void ResolveLoss(IList<WeaponRecord> snapshot, bool busted, StorageBin lastSafehouse)
        {
            if (!busted && lastSafehouse != null)
            {
                foreach (WeaponRecord record in snapshot) { if (record.Owned) { lastSafehouse.Weapons.Add(record.Clone()); } }
            }
            snapshot.Clear();
        }

        internal static StorageBin FindOrAdd(List<StorageBin> bins, string id)
        {
            foreach (StorageBin bin in bins) { if (bin.Id == id) { return bin; } }
            StorageBin added = new StorageBin(); added.Id = id; bins.Add(added); return added;
        }
    }
}
