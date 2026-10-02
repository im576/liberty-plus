using System;
using System.IO;
using LibertyFramework.Core.Config;
using LibertyFramework.Gunplay.Logic;
using LibertyFramework.Gunplay.Profiles;
using LibertyFramework.Weapons.Logic;

namespace LibertyFramework.Verify
{
    // T-043: weapon-specific reticles as data. Class styles, per-weapon overrides, the migration from the single `crosshair`
    // section, the opening equals the cone, and the truthfulness check.
    internal static class Stage1ReticleChecks
    {
        internal static void Run(string repoRoot, Checker check)
        {
            string path = Path.Combine(repoRoot, "config/gunplay.json");
            WeaponCatalog catalog = JsonStore.Load<WeaponCatalog>(Path.Combine(repoRoot, "config/weapon-catalog.json"));
            catalog.Validate();
            GunplayConfig config = JsonStore.Load<GunplayConfig>(path);
            GunplayConfigValidator.Validate(config);
            check.True("gunplay.json has a reticles section with class styles", config.Reticles != null && config.Reticles.Enabled && config.Reticles.Classes.Count >= 5, "");

            // Each Stage 1 class shows its own configured reticle.
            ResolvedReticle pistol = ResolveFor(config, catalog, 7);
            ResolvedReticle smg = ResolveFor(config, catalog, 12);
            ResolvedReticle rifle = ResolveFor(config, catalog, 14);
            ResolvedReticle shotgun = ResolveFor(config, catalog, 11);
            check.True("pistol: a small precise cross", pistol.Style == "cross" && pistol.WeaponClass == "pistol" && pistol.LineLengthPixels <= 6, pistol.Style + " length=" + pistol.LineLengthPixels);
            check.True("SMG: a wider cross than the pistol's", smg.Style == "cross" && smg.LineLengthPixels > pistol.LineLengthPixels && smg.MinimumGapPixels > pistol.MinimumGapPixels, "length=" + smg.LineLengthPixels);
            check.True("assault rifle: structured corner brackets", rifle.Style == "bracket" && rifle.WeaponClass == "rifle", rifle.Style);
            check.True("shotgun: a pellet ring", shotgun.Style == "ring" && shotgun.RingDots >= 6 && shotgun.MinimumGapPixels >= 10, shotgun.Style + " dots=" + shotgun.RingDots);
            check.True("the four Stage 1 classes have four different reticles", pistol.Style != rifle.Style && rifle.Style != shotgun.Style && pistol.LineLengthPixels != smg.LineLengthPixels, "");
            check.True("sniper class draws no reticle of its own (the game's scope)", ReticleResolver.Resolve(config, "sniper", 16).IsNone, "");
            check.True("a catalog weapon takes its catalog class even when the slot says otherwise", ReticleResolver.ClassOf(catalog, 14, "pistol") == "rifle" && ReticleResolver.ClassOf(catalog, 3, "pistol") == "pistol", "");

            // Per-weapon overrides.
            ResolvedReticle automag = ResolveFor(config, catalog, 9);
            ResolvedReticle sweeper = ResolveFor(config, catalog, 10);
            check.True("per-weapon override: the AutoMag keeps the pistol cross with its own arm length and thickness", automag.Style == "cross" && automag.LineLengthPixels == 4 && automag.LineThicknessPixels == 3 && pistol.LineThicknessPixels == 2, "");
            check.True("per-weapon override: the Street Sweeper keeps the shotgun ring with thicker dots", sweeper.Style == "ring" && sweeper.LineThicknessPixels == 4 && shotgun.LineThicknessPixels == 3 && sweeper.RingDots == shotgun.RingDots, "");

            // Migration: no reticles section (or it switched off) = the old crosshair for every weapon.
            GunplayConfig legacy = JsonStore.Load<GunplayConfig>(path);
            legacy.Reticles = null;
            GunplayConfigValidator.Validate(legacy);
            bool same = true;
            foreach (int id in new[] { 7, 12, 14, 11, 3 })
            {
                ResolvedReticle old = ReticleResolver.Resolve(legacy, "rifle", id);
                same = same && old.Style == "cross" && old.LineLengthPixels == legacy.Crosshair.LineLengthPixels && old.LineThicknessPixels == legacy.Crosshair.LineThicknessPixels &&
                    old.MinimumGapPixels == legacy.Crosshair.MinimumGapPixels && old.MaximumGapPixels == legacy.Crosshair.MaximumGapPixels &&
                    old.OutlinePixels == legacy.Crosshair.OutlinePixels && old.CenterDotPixels == 0 && old.GapSmoothingPerSecond == legacy.Crosshair.GapSmoothingPerSecond;
            }
            check.True("without a reticles section every weapon keeps the old crosshair", same, "");
            legacy = JsonStore.Load<GunplayConfig>(path);
            legacy.Reticles.Enabled = false;
            check.True("reticles.enabled=false restores the old crosshair", ReticleResolver.Resolve(legacy, "rifle", 14).Style == "cross" && ReticleResolver.Resolve(legacy, "rifle", 14).LineLengthPixels == legacy.Crosshair.LineLengthPixels, "");

            // The opening is the cone: no cosmetic factor, only the style's clamp.
            double perTangent = 869.0;
            bool truthful = true;
            foreach (double cone in new[] { 0.5, 1.0, 2.0, 3.0 })
            {
                double expected = Math.Tan(cone * Math.PI / 180.0) * perTangent;
                double target = ReticleResolver.TargetPixels(cone, perTangent, 500.0, rifle);
                truthful = truthful && Math.Abs(target - expected) / expected < 1e-9;
            }
            check.True("the rifle reticle opening equals tan(cone) x the game's pixels per tangent (no cosmetic factor)", truthful, "");
            check.True("the opening is clamped to the style's minimum and maximum", ReticleResolver.TargetPixels(0.001, perTangent, 500.0, rifle) == rifle.MinimumGapPixels &&
                ReticleResolver.TargetPixels(30, perTangent, 500.0, rifle) == rifle.MaximumGapPixels, "");
            check.True("without the game's projection the FOV formula (fallback) is used", Math.Abs(ReticleResolver.TargetPixels(2.0, 0, 500.0, rifle) - Math.Tan(2.0 * Math.PI / 180.0) * 500.0) < 1e-9, "");

            // Truthfulness check: steady frames must match, the reticle may lag while closing but never under-report.
            ReticleTruth truth = new ReticleTruth();
            for (int frame = 0; frame < 120; frame++) { truth.Add(frame * 16.7, 20.0, 20.0); }
            check.True("a steady reticle at the cone passes the truthfulness check", truth.SteadySamples > 50 && truth.Violations == 0 && truth.UnderReports == 0, "steady=" + truth.SteadySamples);
            truth = new ReticleTruth();
            double drawn = 40;
            for (int frame = 0; frame < 200; frame++)
            {
                double target = 20.0;
                drawn = drawn > target ? drawn + (target - drawn) * 0.4 : target;
                truth.Add(frame * 16.7, target, drawn);
            }
            check.True("easing closed after a shot is not a violation once the cone is steady", truth.Violations == 0 && truth.UnderReports == 0, "worst=" + truth.WorstErrorPercent.ToString("0.0"));
            truth = new ReticleTruth();
            for (int frame = 0; frame < 120; frame++) { truth.Add(frame * 16.7, 20.0, 12.0); }
            check.True("a reticle drawn 40% too small is reported as under-reporting and as a violation", truth.UnderReports > 100 && truth.Violations > 0, "");
            truth = new ReticleTruth();
            for (int frame = 0; frame < 120; frame++) { truth.Add(frame * 16.7, 20.0, 25.0); }
            check.True("a reticle stuck 25% too large is a violation once steady", truth.Violations > 50 && truth.UnderReports == 0, "");

            // The validator refuses nonsense.
            check.True("an unknown reticle style is rejected", Rejects(path, delegate (GunplayConfig copy) { copy.Reticles.Classes[0].Style.Style = "laser"; }), "");
            check.True("a ring with two dots is rejected", Rejects(path, delegate (GunplayConfig copy) { copy.Reticles.Classes[3].Style.RingDots = 2; }), "");
            check.True("a duplicate class is rejected", Rejects(path, delegate (GunplayConfig copy) { copy.Reticles.Classes[1].WeaponClass = "pistol"; }), "");
            check.True("a weapon naming an unknown class is rejected", Rejects(path, delegate (GunplayConfig copy) { copy.Reticles.Weapons[0].WeaponClass = "cannon"; }), "");
            check.True("a maximum gap below the minimum is rejected", Rejects(path, delegate (GunplayConfig copy) { copy.Reticles.Classes[0].Style.MaximumGapPixels = 1; }), "");
            check.True("notAiming must be hidden or reduced", Rejects(path, delegate (GunplayConfig copy) { copy.Reticles.NotAiming = "always"; }), "");
        }

        private static ResolvedReticle ResolveFor(GunplayConfig config, WeaponCatalog catalog, int weaponId)
        {
            return ReticleResolver.Resolve(config, ReticleResolver.ClassOf(catalog, weaponId, "pistol"), weaponId);
        }

        private static bool Rejects(string path, Action<GunplayConfig> break_)
        {
            GunplayConfig copy = JsonStore.Load<GunplayConfig>(path);
            break_(copy);
            try { GunplayConfigValidator.Validate(copy); } catch (InvalidDataException) { return true; }
            return false;
        }
    }
}
