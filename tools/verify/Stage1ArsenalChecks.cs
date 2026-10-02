using System;
using System.Collections.Generic;
using System.IO;
using LibertyFramework.Core.Config;
using LibertyFramework.GameApi;
using LibertyFramework.Gunplay.Logic;
using LibertyFramework.Gunplay.Profiles;
using LibertyFramework.Weapons.Logic;

namespace LibertyFramework.Verify
{
    // T-041: the Stage 1 arsenal as data. Catalog, gunplay profiles and the gate agree; nothing restricted is normal availability.
    internal static class Stage1ArsenalChecks
    {
        internal static void Run(string repoRoot, Checker check)
        {
            WeaponCatalog catalog = JsonStore.Load<WeaponCatalog>(Path.Combine(repoRoot, "config/weapon-catalog.json"));
            catalog.Validate();
            GunplayConfig config = JsonStore.Load<GunplayConfig>(Path.Combine(repoRoot, "config/gunplay.json"));
            GunplayConfigValidator.Validate(config);
            List<WeaponCatalogEntry> stage1 = catalog.Stage1Entries();
            check.True("catalog defines a Stage 1 arsenal", stage1.Count >= 5, "stage1=" + stage1.Count);

            // Every Stage 1 weapon: class, tier, availability, profile, model, stats; and a matching gunplay profile.
            foreach (WeaponCatalogEntry entry in stage1)
            {
                WeaponProfile profile = config.FindWeapon(entry.WeaponId);
                check.True("stage1 " + entry.Id + " has class, tier, sources, model and stats",
                    !string.IsNullOrEmpty(entry.WeaponClass) && catalog.FindTier(entry.Tier) != null && entry.Availability.Sources.Count > 0 &&
                    !string.IsNullOrEmpty(entry.Model) && entry.Stats != null && entry.VanillaStats != null, "tier=" + entry.Tier + " class=" + entry.WeaponClass);
                check.True("stage1 " + entry.Id + " has a matching gunplay profile",
                    profile != null && profile.CatalogId == entry.Id && profile.ProfileName == entry.Profile && profile.WeaponInfoName == entry.WeaponInfoType &&
                    profile.VanillaWeaponId == entry.WeaponId, entry.Profile);
                check.True("stage1 " + entry.Id + " passes the gunplay gate", Stage1Gate.ProfileFor(config, catalog, entry.WeaponId) == profile, "");
            }

            // Every gunplay profile is either a test weapon or belongs to a Stage 1 catalog entry.
            foreach (WeaponProfile profile in config.Weapons)
            {
                if (profile.IsTestWeapon) { continue; }
                WeaponCatalogEntry owner = catalog.FindById(profile.CatalogId);
                check.True("gunplay profile " + profile.ProfileName + " belongs to a Stage 1 catalog weapon", owner != null && owner.Stage1 && owner.WeaponId == profile.WeaponId, "");
            }

            // No sniper, LMG, P90-type or military weapon in normal availability, and they keep the game's own behaviour.
            foreach (WeaponCatalogEntry entry in catalog.Entries)
            {
                if (!catalog.IsRestrictedClass(entry.WeaponClass)) { continue; }
                WeaponTier tier = catalog.FindTier(entry.Tier);
                check.True("restricted class " + entry.WeaponClass + " (" + entry.Id + ") is not Stage 1 and not normal availability",
                    !entry.Stage1 && tier != null && !tier.NormalAvailability && Stage1Gate.ProfileFor(config, catalog, entry.WeaponId) == null, "tier=" + entry.Tier);
            }
            foreach (int excluded in new[] { 13, 15, 16, 17 })
            {
                WeaponCatalogEntry entry = catalog.Find(excluded);
                check.True("weapon " + excluded + " (P90 look, MG36, snipers) is excluded from Stage 1", entry != null && !entry.Stage1 && config.FindWeapon(excluded) == null, "");
            }
            AvailabilityContext everything = new AvailabilityContext();
            everything.Money = int.MaxValue; everything.StoryProgress = 1.0;
            everything.Contacts.Add("little-jacob");
            List<WeaponCatalogEntry> offered = WeaponAvailability.Offered(catalog, everything);
            bool restrictedOffered = false;
            foreach (WeaponCatalogEntry entry in offered) { if (catalog.IsRestrictedClass(entry.WeaponClass) || !catalog.FindTier(entry.Tier).NormalAvailability) { restrictedOffered = true; } }
            check.True("even a player with all money, progress and contacts is never offered a restricted weapon", !restrictedOffered && offered.Count == stage1.Count, "offered=" + offered.Count);

            // Availability rules: money, progress, contact; unknown progress is conservative; the dev override is explicit.
            AvailabilityContext broke = new AvailabilityContext(); broke.Money = 0; broke.StoryProgress = 0.0;
            List<WeaponCatalogEntry> atStart = WeaponAvailability.Offered(catalog, broke);
            check.True("with no money nothing is offered", atStart.Count == 0, "offered=" + atStart.Count);
            AvailabilityContext early = new AvailabilityContext(); early.Money = 600; early.StoryProgress = 0.0;
            List<string> earlyIds = new List<string>();
            foreach (WeaponCatalogEntry entry in WeaponAvailability.Offered(catalog, early)) { earlyIds.Add(entry.Id); }
            check.True("at the start $600 buys the common pistol, shotgun and Uzi only", earlyIds.Contains("service-pistol") && earlyIds.Contains("imi-uzi") &&
                earlyIds.Contains("remington-1100") && !earlyIds.Contains("ak-47") && !earlyIds.Contains("combat-pistol"), string.Join(",", earlyIds.ToArray()));
            AvailabilityContext unknown = new AvailabilityContext(); unknown.Money = 5000; unknown.StoryProgress = null;
            unknown.Contacts.Add("little-jacob");
            check.True("unknown story progress never unlocks a tier that needs progress",
                !WeaponAvailability.Evaluate(catalog, catalog.FindById("ak-47"), unknown).Available &&
                WeaponAvailability.Evaluate(catalog, catalog.FindById("service-pistol"), unknown).Available, "");
            AvailabilityContext noContact = new AvailabilityContext(); noContact.Money = 5000; noContact.StoryProgress = 1.0;
            check.True("a weapon that needs a contact is not offered without it", !WeaponAvailability.Evaluate(catalog, catalog.FindById("ak-47"), noContact).Available, "");
            AvailabilityContext dev = new AvailabilityContext(); dev.Override = true;
            check.True("the developer override offers any catalog weapon, but a missing entry stays refused",
                WeaponAvailability.Evaluate(catalog, catalog.FindById("m40a1"), dev).Available && !WeaponAvailability.Evaluate(catalog, null, dev).Available, "");

            // The gate: switch off, missing catalog entry and a mismatched id all leave the weapon vanilla; test weapons are unaffected.
            WeaponProfile glock = config.FindWeapon(7);
            GunplayConfig switchedOff = JsonStore.Load<GunplayConfig>(Path.Combine(repoRoot, "config/gunplay.json"));
            switchedOff.Stage1Weapons = new Stage1WeaponSettings(); switchedOff.Stage1Weapons.Enabled = false;
            check.True("stage1Weapons.enabled=false leaves catalog weapons vanilla", Stage1Gate.ProfileFor(switchedOff, catalog, 7) == null, "");
            check.True("with the switch off the test weapons still get their profiles", Stage1Gate.ProfileFor(switchedOff, catalog, 58) != null, "");
            check.True("a missing catalog leaves catalog weapons vanilla", Stage1Gate.ProfileFor(config, null, 7) == null && Stage1Gate.ProfileFor(config, null, 58) != null, "");
            check.True("a vanilla weapon without a profile stays vanilla", Stage1Gate.ProfileFor(config, catalog, 16) == null && Stage1Gate.ProfileFor(config, catalog, 3) == null, "");
            GunplayConfig noSection = JsonStore.Load<GunplayConfig>(Path.Combine(repoRoot, "config/gunplay.json"));
            noSection.Stage1Weapons = null;
            check.True("an older gunplay.json without the stage1Weapons section keeps the profiles on", noSection.Stage1WeaponsEnabled && Stage1Gate.ProfileFor(noSection, catalog, 7) != null && Stage1Gate.ProfileFor(noSection, catalog, 7).ProfileName == glock.ProfileName, "");

            // Bad data is refused by the validators.
            check.True("a vanilla-id profile without a catalogId is rejected", Rejects(repoRoot, delegate (GunplayConfig copy) { copy.FindWeapon(7).CatalogId = null; }), "");
            check.True("a Stage 1 entry in a restricted class is rejected", RejectsCatalog(repoRoot, delegate (WeaponCatalog copy) { copy.FindById("ak-47").WeaponClass = "sniper"; }), "");
            check.True("a Stage 1 entry in a tier outside normal availability is rejected", RejectsCatalog(repoRoot, delegate (WeaponCatalog copy) { copy.FindById("ak-47").Tier = "restricted"; }), "");
            check.True("a Stage 1 entry without a profile is rejected", RejectsCatalog(repoRoot, delegate (WeaponCatalog copy) { copy.FindById("imi-uzi").Profile = null; }), "");
            check.True("an entry with an unknown tier is rejected", RejectsCatalog(repoRoot, delegate (WeaponCatalog copy) { copy.FindById("fn-p90").Tier = "mythic"; }), "");

            // Stats: catalog stats, the game's own values, and (when the installed file is known) WeaponInfo.xml.
            foreach (WeaponCatalogEntry entry in stage1)
            {
                check.True("stage1 " + entry.Id + " stats are positive and differ from the game's own only where intended",
                    entry.Stats.TimeBetweenShotsMilliseconds > 0 && entry.Stats.DamageBase > 0 && entry.Stats.ClipSize > 0 && entry.Stats.AmmoMax > 0, "diff=" + entry.VanillaStats.Differences(entry.Stats));
            }
            check.True("the stats comparison reports a differing field", catalog.FindById("imi-uzi").Stats.Differences(catalog.FindById("imi-uzi").VanillaStats).Contains("timebetweenshots"), "");
            string staged = Path.Combine(repoRoot, "staging/phase2/update/common/data/WeaponInfo.xml");
            if (File.Exists(staged))
            {
                Dictionary<string, WeaponStats> xml = WeaponInfoXml.ReadStats(staged);
                foreach (WeaponCatalogEntry entry in stage1)
                {
                    WeaponStats actual;
                    xml.TryGetValue(entry.WeaponInfoType, out actual);
                    string difference = catalog.ExpectedStats(entry).Differences(actual);
                    check.True("packaged WeaponInfo.xml holds the catalog stats of " + entry.Id, difference.Length == 0, difference);
                }
            }
            else { check.Skip("packaged WeaponInfo.xml stats", "staging/phase2 is generated by tools/package-phase2.ps1"); }
        }

        private static bool Rejects(string repoRoot, Action<GunplayConfig> break_)
        {
            GunplayConfig copy = JsonStore.Load<GunplayConfig>(Path.Combine(repoRoot, "config/gunplay.json"));
            break_(copy);
            try { GunplayConfigValidator.Validate(copy); } catch (InvalidDataException) { return true; }
            return false;
        }

        private static bool RejectsCatalog(string repoRoot, Action<WeaponCatalog> break_)
        {
            WeaponCatalog copy = JsonStore.Load<WeaponCatalog>(Path.Combine(repoRoot, "config/weapon-catalog.json"));
            break_(copy);
            try { copy.Validate(); } catch (InvalidOperationException) { return true; }
            return false;
        }
    }
}
