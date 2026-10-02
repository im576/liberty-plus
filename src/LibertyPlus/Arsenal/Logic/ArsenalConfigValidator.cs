using System;
using System.Collections.Generic;
using System.IO;
using LibertyFramework.Arsenal.Contracts;

namespace LibertyFramework.Arsenal.Logic
{
    internal static class ArsenalConfigValidator
    {
        // A submachine gun may count as a sidearm (general mapping) or as a long gun (Stage 1 loadout, slung on the back).
        private static bool ValidRule(CategoryRule rule)
        {
            bool sidearmSlot = rule.BodySlot == BodySlot.SidearmPrimary || rule.BodySlot == BodySlot.SidearmSecondary;
            bool longGunSlot = rule.BodySlot == BodySlot.LongGun1 || rule.BodySlot == BodySlot.LongGun2;
            return (rule.Category == WeaponCategory.Melee && rule.Group == "melee" && rule.BodySlot == BodySlot.Melee) ||
                (rule.Category == WeaponCategory.Handgun && rule.Group == "sidearm" && sidearmSlot) ||
                (rule.Category == WeaponCategory.SMG && ((rule.Group == "sidearm" && sidearmSlot) || (rule.Group == "longGun" && longGunSlot))) ||
                ((rule.Category == WeaponCategory.Shotgun || rule.Category == WeaponCategory.Rifle || rule.Category == WeaponCategory.Sniper ||
                    rule.Category == WeaponCategory.Heavy) && rule.Group == "longGun" && longGunSlot) ||
                (rule.Category == WeaponCategory.Thrown && rule.Group == "uncounted" && rule.BodySlot == BodySlot.None);
        }

        internal static void Validate(ArsenalConfig config)
        {
            if (config == null || config.SchemaVersion != 1 || config.SidearmLimit < 1 || config.SidearmLimit > 2 ||
                config.LongGunLimit < 1 || config.LongGunLimit > 4 || config.MeleeLimit != 1 ||
                config.PurchaseWindowMilliseconds < 0 || config.PurchaseWindowMilliseconds > 10000 ||
                config.TrunkDistanceMeters <= 0 || config.TrunkRearOffsetMeters <= 0 || config.OwnedVehicleMatchMeters <= 0 || config.FallbackVehicleMatchMeters <= 0 ||
                config.Categories == null || config.Safehouses == null || config.GunsmithGoldFinishPrice < 0) { throw new InvalidDataException("Invalid Arsenal limits, distances, or gunsmith price."); }
            if (config.TrunkTimings == null || !config.TrunkTimings.Valid) { throw new InvalidDataException("trunkTimings missing or out of range (0-5000 ms steps, min <= max <= 10000)."); }
            if (config.SafehouseBlipSprite < 0 || (config.SafehouseBlipSprite > 0 && !(config.DiscoveredSafehouseRadiusMeters > 0)))
                { throw new InvalidDataException("safehouseBlipSprite needs a positive discoveredSafehouseRadiusMeters."); }
            HashSet<WeaponCategory> seen = new HashSet<WeaponCategory>();
            foreach (CategoryRule rule in config.Categories)
            {
                if (rule == null || !seen.Add(rule.Category) || (rule.Group != "sidearm" && rule.Group != "longGun" && rule.Group != "melee" && rule.Group != "uncounted"))
                    { throw new InvalidDataException("Invalid Arsenal category mapping."); }
                if (!ValidRule(rule)) { throw new InvalidDataException("Category mapped to wrong Arsenal group or body slot: " + rule.Category); }
            }
            LoadoutRules loadout = config.Loadout;
            if (loadout != null)
            {
                if (loadout.SidearmLimit < 1 || loadout.SidearmLimit > 2 || loadout.LongGunLimit < 1 || loadout.LongGunLimit > 4)
                    { throw new InvalidDataException("loadout sidearmLimit must be 1-2 and longGunLimit 1-4."); }
                HashSet<WeaponCategory> overridden = new HashSet<WeaponCategory>();
                if (loadout.CategoryOverrides != null)
                {
                    foreach (CategoryRule rule in loadout.CategoryOverrides)
                    {
                        if (rule == null || !overridden.Add(rule.Category) || !ValidRule(rule))
                            { throw new InvalidDataException("loadout categoryOverrides has an invalid or duplicate category."); }
                    }
                }
                HashSet<WeaponCategory> capped = new HashSet<WeaponCategory>();
                if (loadout.AmmoCaps != null)
                {
                    foreach (AmmoCap cap in loadout.AmmoCaps)
                    {
                        // Melee and thrown weapons have no magazine rules here; a cap would only clip grenades.
                        if (cap == null || !capped.Add(cap.Category) || cap.MaximumRounds < 1 || cap.MaximumRounds > 9999 ||
                            cap.Category < WeaponCategory.Handgun || cap.Category > WeaponCategory.Heavy)
                            { throw new InvalidDataException("loadout ammoCaps needs a unique firearm category and 1-9999 rounds."); }
                    }
                }
            }
            foreach (WeaponCategory category in new WeaponCategory[] { WeaponCategory.Melee, WeaponCategory.Handgun, WeaponCategory.SMG,
                WeaponCategory.Shotgun, WeaponCategory.Rifle, WeaponCategory.Sniper, WeaponCategory.Heavy, WeaponCategory.Thrown })
                { if (!seen.Contains(category)) { throw new InvalidDataException("Missing Arsenal category " + category); } }
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SafehouseRule house in config.Safehouses)
            {
                if (house == null || string.IsNullOrWhiteSpace(house.Id) || !ids.Add(house.Id) || string.IsNullOrWhiteSpace(house.Name) ||
                    string.IsNullOrWhiteSpace(house.Episode) || house.Radius <= 0 || float.IsNaN(house.X) || float.IsNaN(house.Y) || float.IsNaN(house.Z))
                    { throw new InvalidDataException("Invalid Arsenal safehouse."); }
            }
        }
    }
}
