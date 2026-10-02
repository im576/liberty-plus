using System;
using System.Collections.Generic;
using System.IO;
using LibertyFramework.Core.Config;
using LibertyFramework.Core.Math3;
using LibertyFramework.GameApi;
using LibertyFramework.Gunplay.Profiles;
using LibertyFramework.Gunplay.Recoil;
using LibertyFramework.Gunplay.Spread;

namespace LibertyFramework.Verify
{
    internal static class LogicChecks
    {
        internal static void Run(string repoRoot, Checker check)
        {
            string configPath = Path.Combine(repoRoot, Path.Combine("config", "gunplay.json"));
            GunplayConfig config = JsonStore.Load<GunplayConfig>(configPath);
            GunplayConfigValidator.Validate(config);
            check.True("gunplay.json parses and validates", true, "weapons=" + config.Weapons.Count);
            check.Equal("three test weapons configured", 3, config.TestWeapons().Count);
            check.True("weapon ids 58/59/60", config.FindWeapon(58) != null && config.FindWeapon(59) != null && config.FindWeapon(60) != null, "");
            check.Near("pistol vertical kick read from JSON", 1.6, config.FindWeapon(58).Recoil.VerticalKickDegrees, 1e-9);
            check.Near("shotgun pellet pattern read from JSON", 3.0, config.FindWeapon(60).Spread.PelletPatternDegrees, 1e-9);
            foreach (TuningParameter parameter in config.Tuning)
            {
                check.True("tuning key resolves: " + parameter.Key, ProfileParameters.IsKnown(parameter.Key), "");
            }

            CheckPresets(repoRoot, config, check);
            CheckValidatorRejects(configPath, check);
            CheckSaveRoundTrip(config, check);
            CheckRecoil(config, check);
            CheckSpread(config, check);
            CheckGeometry(check);
            CheckCalibration(config, check);
            CheckWeaponXml(repoRoot, check);
            CheckLocations(repoRoot, check);
            CheckTeleportSnap(check);
        }

        private static void CheckPresets(string repoRoot, GunplayConfig config, Checker check)
        {
            string directory = Path.Combine(repoRoot, Path.Combine("config", "presets"));
            string[] files = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.json") : new string[0];
            check.True("at least three presets shipped", files.Length >= 3, "count=" + files.Length);
            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                try
                {
                    GunplayConfig copy = JsonStore.Load<GunplayConfig>(Path.Combine(repoRoot, Path.Combine("config", "gunplay.json")));
                    List<string> messages = new List<string>();
                    GunplayConfigStore store = new GunplayConfigStore(Path.Combine(repoRoot, Path.Combine("config", "gunplay.json")),
                        m => { }, m => messages.Add(m));
                    store.Poll(true);
                    bool applied = store.ApplyPreset(file);
                    check.True("preset applies: " + name, applied, messages.Count > 0 ? messages[messages.Count - 1] : "");
                    if (applied) { GunplayConfigValidator.Validate(store.Active); }
                }
                catch (Exception error)
                {
                    check.True("preset applies: " + name, false, error.Message);
                }
            }
        }

        private static void CheckValidatorRejects(string configPath, Checker check)
        {
            GunplayConfig broken = JsonStore.Load<GunplayConfig>(configPath);
            broken.Weapons[0].Recoil.VerticalKickDegrees = -1;
            bool rejected = false;
            try { GunplayConfigValidator.Validate(broken); } catch (InvalidDataException) { rejected = true; }
            check.True("validator rejects negative kick", rejected, "");

            byte[] malformed = System.Text.Encoding.UTF8.GetBytes("{ \"schemaVersion\": 1, ");
            bool parseRejected = false;
            try { JsonStore.Parse<GunplayConfig>(malformed); } catch (InvalidDataException) { parseRejected = true; }
            check.True("malformed JSON rejected with InvalidDataException", parseRejected, "");

            string temporary = Path.Combine(Path.GetTempPath(), "lf_verify_" + Guid.NewGuid().ToString("N") + ".json");
            File.Copy(configPath, temporary);
            List<string> errors = new List<string>();
            GunplayConfigStore store = new GunplayConfigStore(temporary, m => { }, m => errors.Add(m));
            store.Poll(true);
            File.WriteAllText(temporary, "{ broken");
            bool changed = store.Poll(false);
            check.True("store keeps last valid config after malformed edit", !changed && store.Active != null && errors.Count == 1, "errors=" + errors.Count);
            File.Delete(temporary);
        }

        private static void CheckSaveRoundTrip(GunplayConfig config, Checker check)
        {
            string directory = Path.Combine(Path.GetTempPath(), "lf_verify_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "gunplay.json");
            JsonStore.Save(path, config);
            JsonStore.Save(path, config);
            GunplayConfig reloaded = JsonStore.Load<GunplayConfig>(path);
            GunplayConfigValidator.Validate(reloaded);
            check.Near("saved config round-trips", config.FindWeapon(59).Spread.PerShotDegrees, reloaded.FindWeapon(59).Spread.PerShotDegrees, 1e-12);
            check.True("save keeps a .bak of the previous file", File.Exists(path + ".bak"), "");
            check.True("saved JSON is indented for humans", File.ReadAllText(path).Contains("\n  "), "");
            Directory.Delete(directory, true);
        }

        private static void CheckRecoil(GunplayConfig config, Checker check)
        {
            RecoilProfile pistol = config.FindWeapon(58).Recoil;
            RecoilSolver solver = new RecoilSolver(1);
            solver.AddShot(pistol, 0, 1.0);
            double kick = 0;
            double time = 0;
            for (int frame = 0; frame < 5; frame++) { time += 16; kick += solver.Step(pistol, time, 0.016, false).PitchDegrees; }
            check.Near("pistol single kick fully applied within kick duration", pistol.VerticalKickDegrees, kick, 1e-6);
            double total = kick;
            for (int frame = 0; frame < 120; frame++) { time += 16; total += solver.Step(pistol, time, 0.016, false).PitchDegrees; }
            check.Near("pistol recovers configured fraction", pistol.VerticalKickDegrees * (1 - pistol.RecoveryFraction), total, 1e-6);

            RecoilSolver counter = new RecoilSolver(1);
            counter.AddShot(pistol, 0, 1.0);
            double countered = 0;
            time = 0;
            for (int frame = 0; frame < 60; frame++) { time += 16; countered += counter.Step(pistol, time, 0.016, frame > 3).PitchDegrees; }
            check.True("counter-steering cancels auto recovery", countered > pistol.VerticalKickDegrees * 0.9, "net=" + countered.ToString("0.00"));

            RecoilProfile carbine = config.FindWeapon(59).Recoil;
            RecoilSolver burst = new RecoilSolver(2);
            double climb = 0;
            time = 0;
            for (int shot = 0; shot < 30; shot++)
            {
                burst.AddShot(carbine, time, 1.0);
                for (int frame = 0; frame < 7; frame++) { time += 17; climb += burst.Step(carbine, time, 0.017, false).PitchDegrees; }
            }
            double single = carbine.VerticalKickDegrees * carbine.FirstShotMultiplier;
            check.True("carbine sustained fire accumulates beyond one kick", climb > single * 3, "climb=" + climb.ToString("0.00"));
            check.True("carbine climb bounded near maxAccumulated", climb < carbine.MaxAccumulatedDegrees * 1.8, "climb=" + climb.ToString("0.00"));

            RecoilSolver shotgun = new RecoilSolver(3);
            shotgun.AddShot(config.FindWeapon(60).Recoil, 0, 1.0);
            check.True("shotgun single kick is the strongest", shotgun.LastKickPitchDegrees > single * 3, "kick=" + shotgun.LastKickPitchDegrees.ToString("0.00"));
        }

        private static void CheckSpread(GunplayConfig config, Checker check)
        {
            SpreadProfile pistol = config.FindWeapon(58).Spread;
            SpreadModel model = new SpreadModel();
            ShooterState still = new ShooterState();
            still.Aiming = true;
            double time = 0;
            double first = model.Step(pistol, config.Movement, still, time, 0.016);
            check.Near("first shot uses base spread", pistol.BaseDegrees, first, 1e-9);
            for (int shot = 0; shot < 6; shot++)
            {
                model.AddShot(pistol, time);
                for (int frame = 0; frame < 20; frame++) { time += 16.65; model.Step(pistol, config.Movement, still, time, 0.01665); }
            }
            double rapid = model.Current(pistol, config.Movement, still);
            check.True("paced pistol taps remain tight", rapid < first * 1.5, "spread=" + rapid.ToString("0.00"));
            for (int frame = 0; frame < 400; frame++) { time += 16.65; model.Step(pistol, config.Movement, still, time, 0.01665); }
            check.Near("spread recovers to base at rest", pistol.BaseDegrees, model.Current(pistol, config.Movement, still), 1e-9);

            SpreadModel barePistol = new SpreadModel();
            SpreadModel grippedPistol = new SpreadModel();
            for (int shot = 0; shot < 4; shot++)
            {
                barePistol.AddShot(pistol, shot * 80, 1.0);
                grippedPistol.AddShot(pistol, shot * 80, 0.75);
            }
            check.True("match grip reduces repeat-shot bloom without altering first-shot base",
                grippedPistol.Current(pistol, config.Movement, still) < barePistol.Current(pistol, config.Movement, still) &&
                grippedPistol.Current(pistol, config.Movement, still) > pistol.BaseDegrees, "");

            SpreadProfile carbine = config.FindWeapon(59).Spread;
            SpreadModel carbineModel = new SpreadModel();
            time = 0;
            for (int shot = 0; shot < 4; shot++) { carbineModel.AddShot(carbine, time); time += 100; carbineModel.Step(carbine, config.Movement, still, time, 0.1); }
            double shortBurst = carbineModel.Current(carbine, config.Movement, still);
            for (int shot = 0; shot < 8; shot++) { carbineModel.AddShot(carbine, time); time += 100; carbineModel.Step(carbine, config.Movement, still, time, 0.1); }
            double longSpray = carbineModel.Current(carbine, config.Movement, still);
            check.True("short carbine burst remains below long spray", shortBurst < longSpray && shortBurst < carbine.MaxDegrees * 0.6,
                "short=" + shortBurst.ToString("0.00") + " long=" + longSpray.ToString("0.00"));
            for (int frame = 0; frame < 200; frame++) { time += 16.65; carbineModel.Step(carbine, config.Movement, still, time, 0.01665); }
            check.Near("long spray eventually recovers", carbine.BaseDegrees, carbineModel.Current(carbine, config.Movement, still), 1e-9);

            ShooterState running = still;
            running.SpeedMetersPerSecond = 7;
            check.Near("full movement adds movingAddDegrees", pistol.BaseDegrees + pistol.MovingAddDegrees, model.Current(pistol, config.Movement, running), 1e-9);
            ShooterState blind = new ShooterState();
            blind.InCover = true;
            check.Near("blind fire multiplier applied", pistol.BaseDegrees * pistol.BlindFireMultiplier, model.Current(pistol, config.Movement, blind), 1e-9);
            ShooterState vehicle = still;
            vehicle.InVehicle = true;
            check.Near("vehicle multiplier applied", pistol.BaseDegrees * pistol.VehicleMultiplier, model.Current(pistol, config.Movement, vehicle), 1e-9);
        }

        private static void CheckGeometry(Checker check)
        {
            Vec3 camera = new Vec3(0, 0, 1.6);
            Vec3 forward = new Vec3(0, 1, 0);
            Vec3 muzzle = new Vec3(0.35, 0.6, 1.35);
            Vec3 aim = new Vec3(0, 25, 1.6);
            Vec3 direction = (aim - muzzle).Normalized();
            double angle = 1.0 * Math.PI / 180;
            // Rotate the ideal muzzle->aim ray by 1 degree about the world Z axis.
            Vec3 rotated = new Vec3(direction.X * Math.Cos(angle) - direction.Y * Math.Sin(angle),
                direction.X * Math.Sin(angle) + direction.Y * Math.Cos(angle), direction.Z);
            Vec3 end = muzzle + rotated * (aim - muzzle).Length;
            check.Near("shot geometry recovers a 1 degree deviation", 1.0, ShotGeometry.DeviationDegrees(camera, forward, muzzle, end), 0.08);
            check.Near("shot geometry reports 0 for a perfect shot", 0.0, ShotGeometry.DeviationDegrees(camera, forward, muzzle, aim), 1e-6);
            Vec3 west = Vec3.FromPitchHeadingDegrees(0, 90);
            check.True("heading 90 points west (-X)", west.X < -0.99, west.ToString());
        }

        private static void CheckCalibration(GunplayConfig config, Checker check)
        {
            SpreadCalibrationSettings settings = config.SpreadCalibration;
            SpreadCalibrator calibrator = new SpreadCalibrator();
            Random random = new Random(7);
            const double hiddenEngineFactor = 1.6;
            double intended = 1.0;
            for (int shot = 0; shot < 400; shot++)
            {
                double accuracy = calibrator.ToAccuracy(settings, intended);
                double actualCone = ShotGeometry.TangentToDegrees(accuracy * settings.TangentPerAccuracyUnit * hiddenEngineFactor);
                double measured = actualCone * random.NextDouble();
                calibrator.AddSample(settings, measured, intended);
            }
            double finalCone = ShotGeometry.TangentToDegrees(calibrator.ToAccuracy(settings, intended) * settings.TangentPerAccuracyUnit * hiddenEngineFactor);
            check.Near("auto-calibration converges on an unknown engine factor", intended, finalCone, 0.15);
            check.True("calibration gain within bounds", calibrator.Gain >= settings.MinimumGain && calibrator.Gain <= settings.MaximumGain,
                "gain=" + calibrator.Gain.ToString("0.000"));
            check.Near("accuracy for 0 degrees is the configured minimum", settings.MinimumAccuracyValue, new SpreadCalibrator().ToAccuracy(settings, 0), 1e-9);
            check.Near("pistol vanilla accuracy 2.6 maps to about 0.39 degrees", 0.387, new SpreadCalibrator().FromAccuracy(settings, 2.6), 0.01);
        }

        private static void CheckWeaponXml(string repoRoot, Checker check)
        {
            string staged = Path.Combine(repoRoot, Path.Combine("staging", Path.Combine("phase1", Path.Combine("update", Path.Combine("common", Path.Combine("data", "WeaponInfo.xml"))))));
            string phase2 = Path.Combine(repoRoot, "staging/phase2/update/common/data/WeaponInfo.xml");
            if (File.Exists(phase2)) { staged = phase2; }
            if (!File.Exists(staged)) { staged = Path.Combine(repoRoot, Path.Combine("staging", Path.Combine("t007", "WeaponInfo.xml"))); }
            if (!File.Exists(staged) && check.NoGame) { check.Skip("WeaponInfo.xml accuracies", "staging WeaponInfo.xml is generated from the game's files"); return; }
            Dictionary<string, float> accuracies = WeaponInfoXml.ReadAccuracies(staged);
            check.True("WeaponInfo.xml accuracy for LF_GOLD_PISTOL is 2.6", accuracies.ContainsKey("LF_GOLD_PISTOL") && Math.Abs(accuracies["LF_GOLD_PISTOL"] - 2.6f) < 1e-4, staged);
            check.True("LF_GOLD_CARBINE and LF_GOLD_SHOTGUN present", accuracies.ContainsKey("LF_GOLD_CARBINE") && accuracies.ContainsKey("LF_GOLD_SHOTGUN"), "");
            check.True("test accuracies are distinct (layout proof is meaningful)",
                accuracies["LF_GOLD_PISTOL"] != accuracies["LF_GOLD_CARBINE"] && accuracies["LF_GOLD_CARBINE"] != accuracies["LF_GOLD_SHOTGUN"], "");
        }

        private static void CheckLocations(string repoRoot, Checker check)
        {
            string path = Path.Combine(repoRoot, Path.Combine("config", Path.Combine("devtools", "locations.json")));
            check.True("locations.json exists", File.Exists(path), path);
            if (!File.Exists(path)) { return; }
            LibertyFramework.DevTools.Teleport.LocationFile file = JsonStore.Load<LibertyFramework.DevTools.Teleport.LocationFile>(path);
            check.True("locations include a gun test range", file.Locations != null && file.Locations.Exists(l => l.Id == "gun_test_range"), "");
        }

        private static void CheckTeleportSnap(Checker check)
        {
            check.True("nearby pavement snap accepted", LibertyFramework.DevTools.Teleport.Logic.TeleportSnap.IsUsablePavement(-64.8f, 663.4f, 15f, -64.4f, 674.8f, 14.5f), "");
            check.True("corrupt pavement snap rejected", !LibertyFramework.DevTools.Teleport.Logic.TeleportSnap.IsUsablePavement(-64.8f, 663.4f, 15f, 0f, 1417044000f, 2.136292e27f), "");
            check.True("nonfinite pavement snap rejected", !LibertyFramework.DevTools.Teleport.Logic.TeleportSnap.IsUsablePavement(-64.8f, 663.4f, 15f, float.NaN, 663.4f, 15f), "");
            check.True("corrupt ground snap rejected", !LibertyFramework.DevTools.Teleport.Logic.TeleportSnap.IsUsableGround(15f, float.PositiveInfinity), "");
        }
    }
}
