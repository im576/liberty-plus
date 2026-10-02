using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LibertyFramework.Core.Config;
using LibertyFramework.Core.Logging;
using LibertyFramework.GameApi;
using LibertyFramework.Gunplay;
using LibertyFramework.Gunplay.Logic;
using LibertyFramework.Gunplay.Profiles;
using LibertyFramework.Weapons.Logic;

namespace LibertyFramework.Weapons
{
    // Console and autopilot commands for the Stage 1 arsenal (T-041): list the catalog, give any catalog weapon for a
    // test, evaluate the availability rules for a made-up player, and check that catalog, gunplay profiles and the loaded
    // WeaponInfo.xml agree. Reads files on demand; it holds no state and never runs per frame.
    [global::Liberty.Sdk.Module("weapon-catalog", Order = 25, Capabilities = new[] { global::Liberty.Sdk.Capabilities.EngineInternal }, Description = "Stage 1 arsenal catalog commands: list, give, offer, check")]
    public sealed class WeaponCatalogModule : LibertyFramework.Engine.Module
    {
        private const int DefaultAmmo = 300;

        public WeaponCatalogModule() { Interval = 1000; }

        protected internal override void OnStart()
        {
            Engine.Commands.Register(this, "catalog",
                "catalog [list] | give <catalog id|weapon id> [ammo] [force] [clear] | offer <money> <story progress 0-1|unknown> [contacts,comma|-] [override] | check | sim [catalog id|all] - Stage 1 arsenal",
                Run);
        }

        private string Run(string[] args)
        {
            string verb = args.Length > 0 ? args[0].ToLowerInvariant() : "list";
            try
            {
                WeaponCatalog catalog = JsonStore.Load<WeaponCatalog>(LibertyPaths.WeaponCatalog);
                catalog.Validate();
                if (verb == "list") { return List(catalog); }
                if (verb == "give") { return Give(catalog, args); }
                if (verb == "offer") { return Offer(catalog, args); }
                if (verb == "check") { return Check(catalog); }
                if (verb == "sim") { return Simulate(catalog, args.Length > 1 ? args[1] : "all"); }
                return "error: unknown catalog verb " + verb;
            }
            catch (Exception error)
            {
                RuntimeLog.Error("catalog_command_failed verb=" + verb + " error=" + error.Message);
                return "error: " + error.Message;
            }
        }

        private static string List(WeaponCatalog catalog)
        {
            StringBuilder text = new StringBuilder();
            foreach (WeaponCatalogEntry entry in catalog.Entries)
            {
                text.AppendLine(entry.Id + " weapon=" + entry.WeaponId + " " + entry.Label + " class=" + (entry.WeaponClass ?? "-") + " tier=" + (entry.Tier ?? "-") +
                    " stage1=" + entry.Stage1 + " profile=" + (entry.Profile ?? "-") +
                    (entry.Availability != null ? " price=" + entry.Availability.Price + " sources=" + string.Join(",", (entry.Availability.Sources ?? new List<string>()).ToArray()) : ""));
            }
            string listing = text.ToString().TrimEnd();
            RuntimeLog.Info("catalog_list entries=" + catalog.Entries.Count + " stage1=" + catalog.Stage1Entries().Count);
            return listing;
        }

        private string Give(WeaponCatalog catalog, string[] args)
        {
            if (args.Length < 2) { return "error: catalog give <catalog id|weapon id> [ammo] [force] [clear]"; }
            WeaponCatalogEntry entry = catalog.FindById(args[1]);
            int numeric;
            if (entry == null && int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric)) { entry = catalog.Find(numeric); }
            if (entry == null) { return "error: no catalog entry " + args[1]; }
            bool force = Array.IndexOf(args, "force") >= 0;
            bool clear = Array.IndexOf(args, "clear") >= 0;
            if (!entry.Stage1 && !force) { return "error: " + entry.Id + " is not a Stage 1 weapon (add force to give it for a test)"; }
            int ammo = DefaultAmmo;
            if (args.Length > 2 && args[2] != "force" && args[2] != "clear" && !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out ammo)) { return "error: bad ammo " + args[2]; }
            global::Liberty.Sdk.PedRef player = Liberty.Player.Ped;
            // For tests: start from an empty inventory, so the Arsenal's loadout limits do not move the weapon being tested.
            if (clear) { Liberty.Weapons.RemoveAll(player); }
            Liberty.Weapons.Give(player, entry.WeaponId, ammo);
            Liberty.Weapons.Select(player, entry.WeaponId);
            RuntimeLog.Info("catalog_give id=" + entry.Id + " weapon=" + entry.WeaponId + " ammo=" + ammo + " stage1=" + entry.Stage1);
            return "gave " + entry.Id + " (weapon " + entry.WeaponId + ", ammo " + ammo + ")";
        }

        // What the availability rules would offer a player with this money, story progress and contacts.
        private static string Offer(WeaponCatalog catalog, string[] args)
        {
            AvailabilityContext context = new AvailabilityContext();
            if (args.Length < 3) { return "error: catalog offer <money> <story progress 0-1|unknown> [contacts,comma]"; }
            if (!int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out context.Money)) { return "error: bad money " + args[1]; }
            double progress;
            if (args[2] != "unknown")
            {
                if (!double.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out progress)) { return "error: bad story progress " + args[2]; }
                context.StoryProgress = progress;
            }
            if (args.Length > 3 && args[3] != "-") { foreach (string contact in args[3].Split(',')) { context.Contacts.Add(contact); } }
            context.Override = Array.IndexOf(args, "override") >= 0;
            StringBuilder text = new StringBuilder();
            int offered = 0;
            foreach (WeaponCatalogEntry entry in catalog.Entries)
            {
                AvailabilityResult result = WeaponAvailability.Evaluate(catalog, entry, context);
                if (result.Available) { offered++; }
                text.AppendLine(entry.Id + ": " + (result.Available ? "offered" : "not offered (" + result.Reason + ")"));
            }
            RuntimeLog.Info("catalog_offer money=" + context.Money + " progress=" + args[2] + " offered=" + offered);
            return text.ToString().TrimEnd();
        }

        // Catalog, gunplay profiles and the loaded WeaponInfo.xml must agree; any disagreement is an "error:" reply (fails the scenario step).
        private string Check(WeaponCatalog catalog)
        {
            GunplayController gunplay = GunplayController.Instance;
            GunplayConfig config = gunplay != null ? gunplay.Config : null;
            if (config == null) { return "error: gunplay config is not loaded"; }
            List<string> problems = new List<string>();
            Dictionary<string, WeaponStats> xml = null;
            string xmlPath = WeaponInfoXml.ActivePath(LibertyPaths.GameDirectory);
            try { xml = WeaponInfoXml.ReadStats(xmlPath); }
            catch (Exception error) { problems.Add("cannot read " + xmlPath + ": " + error.Message); }
            int stage1 = 0;
            foreach (WeaponCatalogEntry entry in catalog.Entries)
            {
                WeaponProfile profile = config.FindWeapon(entry.WeaponId);
                WeaponProfile gated = Stage1Gate.ProfileFor(config, catalog, entry.WeaponId);
                if (entry.Stage1)
                {
                    stage1++;
                    if (profile == null || profile.CatalogId != entry.Id || profile.ProfileName != entry.Profile) { problems.Add(entry.Id + ": no matching gunplay profile " + entry.Profile); }
                    else if (gated == null && config.Stage1WeaponsEnabled) { problems.Add(entry.Id + ": the gunplay gate refused its profile"); }
                    if (profile != null && profile.WeaponInfoName != entry.WeaponInfoType) { problems.Add(entry.Id + ": profile weaponInfoName " + profile.WeaponInfoName + " != " + entry.WeaponInfoType); }
                    if (!gunplay.CanControlSpread(entry.WeaponId)) { problems.Add(entry.Id + ": its WeaponInfo entry was not validated, so the spread model cannot drive it"); }
                    string difference = StatsDifference(catalog, xml, entry);
                    RuntimeLog.Info("stage1_weaponinfo id=" + entry.Id + " type=" + entry.WeaponInfoType + " stats=" + (difference.Length == 0 ? "match" : "mismatch " + difference));
                    if (difference.Length > 0) { problems.Add(entry.Id + ": installed WeaponInfo.xml differs from the catalog stats (" + difference + ")"); }
                }
                else if (entry.WeaponId < 58 && gated != null) { problems.Add(entry.Id + ": is not a Stage 1 weapon but the gunplay gate applies a profile"); }
                RuntimeLog.Info("stage1_gate_check id=" + entry.Id + " weapon=" + entry.WeaponId + " stage1=" + entry.Stage1 + " gunplay=" + (gated != null ? "liberty" : "vanilla"));
            }
            if (problems.Count > 0) { return "error: " + string.Join("; ", problems.ToArray()); }
            return "catalog ok: " + stage1 + " Stage 1 weapons of " + catalog.Entries.Count + " entries; profiles, gate and WeaponInfo.xml agree";
        }

        // T-042: the live gunplay config and catalog through the spread/recoil model, against each class's targets (same code as the verifier).
        private static string Simulate(WeaponCatalog catalog, string which)
        {
            GunplayController gunplay = GunplayController.Instance;
            GunplayConfig config = gunplay != null ? gunplay.Config : null;
            if (config == null) { return "error: gunplay config is not loaded"; }
            List<string> failures = new List<string>();
            int count = 0;
            foreach (WeaponCatalogEntry entry in catalog.Stage1Entries())
            {
                if (which != "all" && !string.Equals(which, entry.Id, StringComparison.OrdinalIgnoreCase)) { continue; }
                ClassTargetSettings target = config.FindClassTarget(entry.WeaponClass);
                WeaponProfile profile = Stage1Gate.ProfileFor(config, catalog, entry.WeaponId);
                WeaponStats stats = catalog.ExpectedStats(entry);
                if (target == null || profile == null || stats == null || !stats.TimeBetweenShotsMilliseconds.HasValue) { failures.Add(entry.Id + ": no class target, profile or fire rate"); continue; }
                GunplaySimulationResult result = GunplaySimulation.Run(profile, config.Movement, target, stats.TimeBetweenShotsMilliseconds.Value);
                List<string> problems = GunplaySimulation.Violations(result, target);
                count++;
                RuntimeLog.Info("gunplay_sim id=" + entry.Id + " class=" + entry.WeaponClass + " first=" + result.FirstShotConeDegrees.ToString("0.###") +
                    " burst_peak=" + result.BurstPeakConeDegrees.ToString("0.###") + " recovery_ms=" + result.BurstRecoveryMilliseconds.ToString("0") +
                    (target.SustainedShots > 0 ? " climb=" + result.ClimbDegrees.ToString("0.##") + " cone_last=" + result.SustainedCones[result.SustainedCones.Length - 1].ToString("0.###") + " grows=" + result.SustainedGrowsEveryShot : "") +
                    " ok=" + (problems.Count == 0) + (problems.Count > 0 ? " problems=" + string.Join("; ", problems.ToArray()) : ""));
                foreach (string problem in problems) { failures.Add(entry.Id + ": " + problem); }
            }
            if (count == 0 && failures.Count == 0) { return "error: no Stage 1 weapon matches " + which; }
            if (failures.Count > 0) { return "error: " + string.Join("; ", failures.ToArray()); }
            return "gunplay sim ok: " + count + " Stage 1 weapons meet their class targets";
        }

        private static string StatsDifference(WeaponCatalog catalog, Dictionary<string, WeaponStats> xml, WeaponCatalogEntry entry)
        {
            if (xml == null || catalog.ExpectedStats(entry) == null) { return ""; }
            WeaponStats actual;
            if (!xml.TryGetValue(entry.WeaponInfoType, out actual)) { return "type " + entry.WeaponInfoType + " missing from WeaponInfo.xml"; }
            return catalog.ExpectedStats(entry).Differences(actual);
        }
    }
}
