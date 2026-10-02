using LibertyFramework.GameApi;
using System;
using System.Collections.Generic;
using System.IO;
using LibertyFramework.Core.Config;
using LibertyFramework.Gunplay.Logic;
using LibertyFramework.Gunplay.Profiles;
using LibertyFramework.Weapons.Logic;

namespace LibertyFramework.Verify
{
    // T-042: every Stage 1 weapon profile delivers what its class asks (gunplay.json classTargets): a first-shot cone, burst
    // recovery, and for automatic weapons a spread that grows with every shot and a bounded camera climb. Same model code as the game loop.
    internal static class Stage1GunplayChecks
    {
        internal static void Run(string repoRoot, Checker check)
        {
            WeaponCatalog catalog = JsonStore.Load<WeaponCatalog>(Path.Combine(repoRoot, "config/weapon-catalog.json"));
            catalog.Validate();
            GunplayConfig config = JsonStore.Load<GunplayConfig>(Path.Combine(repoRoot, "config/gunplay.json"));
            GunplayConfigValidator.Validate(config);
            check.True("classTargets exist for every Stage 1 class", config.ClassTargets != null, "");
            foreach (WeaponCatalogEntry entry in catalog.Stage1Entries())
            {
                ClassTargetSettings target = config.FindClassTarget(entry.WeaponClass);
                check.True("class target for " + entry.WeaponClass + " (" + entry.Id + ")", target != null, "");
                if (target == null) { continue; }
                WeaponProfile profile = config.FindWeapon(entry.WeaponId);
                WeaponStats stats = catalog.ExpectedStats(entry);
                GunplaySimulationResult result = GunplaySimulation.Run(profile, config.Movement, target, stats.TimeBetweenShotsMilliseconds.Value);
                List<string> problems = GunplaySimulation.Violations(result, target);
                check.True(entry.Id + " meets its " + entry.WeaponClass + " targets", problems.Count == 0,
                    "first=" + result.FirstShotConeDegrees.ToString("0.###") + " burst_peak=" + result.BurstPeakConeDegrees.ToString("0.###") +
                    " recovery_ms=" + result.BurstRecoveryMilliseconds.ToString("0") +
                    (target.SustainedShots > 0 ? " climb=" + result.ClimbDegrees.ToString("0.##") + " cone30=" + result.SustainedCones[result.SustainedCones.Length - 1].ToString("0.##") : "") +
                    (problems.Count > 0 ? " PROBLEMS: " + string.Join("; ", problems.ToArray()) : ""));
            }

            // A laser and a runaway climb are caught: the same profile with the growth removed or the kick tripled must fail.
            WeaponCatalogEntry rifle = catalog.FindById("ak-47");
            ClassTargetSettings rifleTarget = config.FindClassTarget("rifle");
            WeaponProfile flat = JsonStore.Load<GunplayConfig>(Path.Combine(repoRoot, "config/gunplay.json")).FindWeapon(rifle.WeaponId);
            flat.Spread.PerShotDegrees = 0;
            check.True("a rifle whose spread never grows is reported as a laser",
                GunplaySimulation.Violations(GunplaySimulation.Run(flat, config.Movement, rifleTarget, 110), rifleTarget).Exists(p => p.Contains("laser")), "");
            WeaponProfile violent = JsonStore.Load<GunplayConfig>(Path.Combine(repoRoot, "config/gunplay.json")).FindWeapon(rifle.WeaponId);
            violent.Recoil.VerticalKickDegrees *= 3;
            check.True("a rifle with three times the kick is reported as climbing too far",
                GunplaySimulation.Violations(GunplaySimulation.Run(violent, config.Movement, rifleTarget, 110), rifleTarget).Exists(p => p.Contains("climb")), "");
            WeaponProfile wide = JsonStore.Load<GunplayConfig>(Path.Combine(repoRoot, "config/gunplay.json")).FindWeapon(7);
            wide.Spread.BaseDegrees = 2;
            ClassTargetSettings pistolTarget = config.FindClassTarget("pistol");
            check.True("a pistol whose first shot is wide is reported",
                GunplaySimulation.Violations(GunplaySimulation.Run(wide, config.Movement, pistolTarget, 333), pistolTarget).Exists(p => p.Contains("first-shot")), "");

            // Range bookkeeping: a pause longer than the chain reset starts a new chain, a weapon change too.
            RangeRecorder recorder = new RangeRecorder();
            recorder.Add(7, 1000, 0.1, 0.3, 450); recorder.Add(7, 1300, 0.2, 0.4, 450); recorder.Add(7, 3000, 0.1, 0.3, 450); recorder.Add(14, 3100, 0.1, 0.3, 450);
            check.True("range recorder numbers shots within a chain and restarts after a pause or a weapon change",
                recorder.Shots[0].Index == 1 && recorder.Shots[1].Index == 2 && recorder.Shots[2].Index == 1 && recorder.Shots[3].Index == 1 && recorder.Count == 4, "");
            check.True("range recorder summarises per weapon and group", recorder.Summary().Count == 6, "");
            recorder.Reset();
            check.True("range recorder reset forgets the run", recorder.Count == 0, "");
            GunplayConfig broken = JsonStore.Load<GunplayConfig>(Path.Combine(repoRoot, "config/gunplay.json"));
            broken.FindClassTarget("rifle").ClimbMaxDegrees = 1;
            broken.FindClassTarget("rifle").ClimbMinDegrees = 5;
            bool rejected = false;
            try { GunplayConfigValidator.Validate(broken); } catch (InvalidDataException) { rejected = true; }
            check.True("classTargets with a climb range that ends before it starts are rejected", rejected, "");
        }
    }
}
