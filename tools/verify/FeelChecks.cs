using System;
using System.IO;
using LibertyFramework.Arsenal.Contracts;
using LibertyFramework.Arsenal.Holsters.Logic;
using LibertyFramework.Core.Config;
using LibertyFramework.Gunplay.Profiles;
using LibertyFramework.Gunplay.Logic;
using System.Collections.Generic;

namespace LibertyFramework.Verify
{
    // Offline tests for Feel & Presentation (T-011, T-013..T-017, T-021). Owned by Agent B; add checks here.
    internal static class FeelChecks
    {
        internal static void Run(string repoRoot, Checker check)
        {
            check.True("holster contract: five body slots", (int)BodySlot.Melee == 5, "");
            check.True("holster handgun right thigh", HolsterRules.SlotFor(WeaponCategory.Handgun, true) == BodySlot.SidearmPrimary, "");
            check.True("holster SMG left hip", HolsterRules.SlotFor(WeaponCategory.SMG, true) == BodySlot.SidearmSecondary, "");
            check.True("holster two long guns", HolsterRules.SlotFor(WeaponCategory.Rifle, true) == BodySlot.LongGun1 &&
                HolsterRules.SlotFor(WeaponCategory.Shotgun, false) == BodySlot.LongGun2, "");
            check.True("holster thrown omitted", HolsterRules.SlotFor(WeaponCategory.Thrown, true) == BodySlot.None, "");
            check.True("holster in hand hidden", !HolsterRules.Visible(true, true, true, false, false, false, true), "");
            check.True("holster vehicle hidden", !HolsterRules.Visible(false, true, true, false, true, false, true), "");
            check.True("holster bike visible when enabled", HolsterRules.Visible(false, true, true, false, true, true, true), "");
            check.True("holster fade hidden", !HolsterRules.Visible(false, true, true, true, false, false, true), "");
            check.True("holster death hidden", !HolsterRules.Visible(false, false, true, false, false, false, true), "");
            string holstersPath = Path.Combine(repoRoot, Path.Combine("config", "holsters.json"));
            HolsterConfig config = JsonStore.Load<HolsterConfig>(holstersPath);
            config.Validate();
            check.True("holsters config valid", config.Placements.Count >= 5 && config.FindWeapon(58) != null &&
                config.FindWeapon(59) != null && config.FindWeapon(60) != null, "");
            config.Weapons.Add(config.Weapons[0]);
            bool duplicateRejected = false;
            try { config.Validate(); }
            catch (InvalidDataException) { duplicateRejected = true; }
            check.True("holsters duplicate ID rejected", duplicateRejected, "");
            GunplayConfig gunplay = JsonStore.Load<GunplayConfig>(Path.Combine(repoRoot, Path.Combine("config", "gunplay.json")));
            GunplayConfigValidator.Validate(gunplay);
            check.True("feel config loaded", gunplay.Feel.Enabled && gunplay.Feel.AimFovReductionDegrees > 0, "");
            gunplay.FreeAim.Profile = "slowdown";
            bool unsupportedRejected = false;
            try { GunplayConfigValidator.Validate(gunplay); }
            catch (InvalidDataException error) { unsupportedRejected = error.Message.Contains("verified CE assist control"); }
            check.True("unsupported aim profile rejected clearly", unsupportedRejected, "");
            List<int> carried = new List<int>(new[] { 58, 59, 60 });
            check.True("aiming cycle wraps carried list", AimingCycleRules.NextWeapon(carried, 60, 1) == 58 &&
                AimingCycleRules.NextWeapon(carried, 58, -1) == 60, "");
            check.True("aiming cycle excludes absent weapon", AimingCycleRules.NextWeapon(carried, 7, 1) == 58, "");
            string finishes = File.ReadAllText(Path.Combine(repoRoot, Path.Combine("assets", Path.Combine("finishes", "finishes.json"))));
            check.True("T-011 gold variants declared", finishes.Contains("lf_gold_carbine") && finishes.Contains("lf_gold_shotgun"), "");
        }
    }
}
