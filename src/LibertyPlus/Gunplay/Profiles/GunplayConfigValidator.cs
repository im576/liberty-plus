using System;
using System.Collections.Generic;
using System.IO;
using LibertyFramework.Gunplay.Logic;

namespace LibertyFramework.Gunplay.Profiles
{
    // Rejects configs that would produce nonsense in game; the previous valid config stays active.
    internal static class GunplayConfigValidator
    {
        internal static void Validate(GunplayConfig config)
        {
            List<string> errors = new List<string>();
            if (config.SchemaVersion != 1) { errors.Add("schemaVersion must be 1"); }
            if (config.FreeAim == null || config.Crosshair == null || config.SpreadCalibration == null ||
                config.RecoilGlobal == null || config.Movement == null || config.Weapons == null ||
                config.Tuning == null || config.TestRange == null || config.Feel == null || config.DebugHit == null || config.SwitchWhileAiming == null)
            {
                throw new InvalidDataException("missing top-level section");
            }

            CrosshairSettings crosshair = config.Crosshair;
            if (config.FreeAim.Profile != "vanilla" && config.FreeAim.Profile != "free")
            {
                errors.Add("freeAim.profile must be vanilla or free; slowdown and light need a verified CE assist control (T-014)");
            }
            Positive(errors, "crosshair.lineLengthPixels", crosshair.LineLengthPixels);
            Positive(errors, "crosshair.lineThicknessPixels", crosshair.LineThicknessPixels);
            NonNegative(errors, "crosshair.outlinePixels", crosshair.OutlinePixels);
            NonNegative(errors, "crosshair.minimumGapPixels", crosshair.MinimumGapPixels);
            if (crosshair.MaximumGapPixels < crosshair.MinimumGapPixels) { errors.Add("crosshair.maximumGapPixels < minimumGapPixels"); }
            Positive(errors, "crosshair.gapSmoothingPerSecond", crosshair.GapSmoothingPerSecond);
            Argb(errors, "crosshair.colorArgb", crosshair.ColorArgb);
            Argb(errors, "crosshair.outlineArgb", crosshair.OutlineArgb);
            if (crosshair.FovAxis != "vertical" && crosshair.FovAxis != "horizontal") { errors.Add("crosshair.fovAxis must be vertical or horizontal"); }

            SpreadCalibrationSettings calibration = config.SpreadCalibration;
            Positive(errors, "spreadCalibration.tangentPerAccuracyUnit", calibration.TangentPerAccuracyUnit);
            if (calibration.SampleWindow < 3) { errors.Add("spreadCalibration.sampleWindow must be >= 3"); }
            if (calibration.GainAdjustRate <= 0 || calibration.GainAdjustRate > 1) { errors.Add("spreadCalibration.gainAdjustRate must be in (0,1]"); }
            if (calibration.MinimumGain <= 0 || calibration.MaximumGain < calibration.MinimumGain) { errors.Add("spreadCalibration gain bounds invalid"); }
            Positive(errors, "spreadCalibration.maximumMeasurableDeviationDegrees", calibration.MaximumMeasurableDeviationDegrees);
            NonNegative(errors, "spreadCalibration.minimumAccuracyValue", calibration.MinimumAccuracyValue);

            RecoilGlobalSettings recoil = config.RecoilGlobal;
            Positive(errors, "debugHit.scanRadiusMeters", config.DebugHit.ScanRadiusMeters);
            Positive(errors, "debugHit.scanIntervalMilliseconds", config.DebugHit.ScanIntervalMilliseconds);
            Positive(errors, "debugHit.worldClassificationDelayMilliseconds", config.DebugHit.WorldClassificationDelayMilliseconds);
            AimingSwitchSettings cycling = config.SwitchWhileAiming;
            if ((cycling.PreviousButton != "DPadLeft" && cycling.PreviousButton != "DPadRight") ||
                (cycling.NextButton != "DPadLeft" && cycling.NextButton != "DPadRight") ||
                cycling.PreviousButton == cycling.NextButton)
            { errors.Add("switchWhileAiming needs distinct DPadLeft/DPadRight buttons"); }
            if (config.ShoulderSwap != null)
            {
                ShoulderSwapSettings swap = config.ShoulderSwap;
                if (!ShoulderSwapSettings_IsButton(swap.ControllerButton)) { errors.Add("shoulderSwap.controllerButton must be LeftShoulder, RightShoulder, LeftThumb, RightThumb, XButton or YButton"); }
                if (string.IsNullOrEmpty(swap.KeyboardKey)) { errors.Add("shoulderSwap.keyboardKey is required"); }
                if (!(swap.TransitionMilliseconds >= 0 && swap.TransitionMilliseconds <= 2000)) { errors.Add("shoulderSwap.transitionMilliseconds must be 0-2000"); }
            }
            if (config.Performance != null &&
                (!(config.Performance.GameCameraRefreshMilliseconds >= 0 && config.Performance.GameCameraRefreshMilliseconds <= 5000) ||
                 !(config.Performance.FovRefreshMilliseconds >= 0 && config.Performance.FovRefreshMilliseconds <= 5000)))
            {
                errors.Add("performance refresh intervals must be 0-5000 ms");
            }
            NonNegative(errors, "feel.shakePitchDegrees", config.Feel.ShakePitchDegrees);
            NonNegative(errors, "feel.shakeHeadingDegrees", config.Feel.ShakeHeadingDegrees);
            NonNegative(errors, "feel.aimFovReductionDegrees", config.Feel.AimFovReductionDegrees);
            Positive(errors, "feel.fovSmoothingPerSecond", config.Feel.FovSmoothingPerSecond);
            if (recoil.RecoveryCancelStickThreshold <= 0 || recoil.RecoveryCancelStickThreshold > 1) { errors.Add("recoilGlobal.recoveryCancelStickThreshold must be in (0,1]"); }
            Positive(errors, "recoilGlobal.cameraValidationToleranceDegrees", recoil.CameraValidationToleranceDegrees);
            if (recoil.CameraValidationSamples < 1) { errors.Add("recoilGlobal.cameraValidationSamples must be >= 1"); }
            Positive(errors, "recoilGlobal.maximumDeltaSeconds", recoil.MaximumDeltaSeconds);

            NonNegative(errors, "movement.movingSpeedThresholdMetersPerSecond", config.Movement.MovingSpeedThreshold);
            if (config.Movement.FullPenaltySpeed <= config.Movement.MovingSpeedThreshold) { errors.Add("movement.fullMovementPenaltySpeedMetersPerSecond must exceed threshold"); }

            HashSet<int> ids = new HashSet<int>();
            foreach (WeaponProfile weapon in config.Weapons)
            {
                if (weapon == null) { errors.Add("null weapon entry"); continue; }
                if (!ids.Add(weapon.WeaponId)) { errors.Add("duplicate weaponId " + weapon.WeaponId); }
                // Test weapons live in the custom (58+) slots; a Stage 1 catalog weapon keeps its vanilla id (1-57) and names its catalog entry.
                if (weapon.WeaponId < 1 || weapon.WeaponId > 127) { errors.Add("weaponId " + weapon.WeaponId + " is out of range"); }
                else if (weapon.WeaponId < 58 && (weapon.VanillaWeaponId != weapon.WeaponId || string.IsNullOrEmpty(weapon.CatalogId)))
                { errors.Add("weaponId " + weapon.WeaponId + " is a vanilla id: needs vanillaWeaponId equal to it and a catalogId (a test weapon uses a 58+ id)"); }
                else if (weapon.WeaponId >= 58 && weapon.VanillaWeaponId == weapon.WeaponId) { errors.Add("test weapon " + weapon.WeaponId + " needs its own vanillaWeaponId"); }
                if (string.IsNullOrEmpty(weapon.WeaponInfoName) || string.IsNullOrEmpty(weapon.Label)) { errors.Add("weapon " + weapon.WeaponId + " needs weaponInfoName and label"); }
                ValidateProfiles(errors, "weapon " + weapon.WeaponId, weapon.Recoil, weapon.Spread);
            }

            if (config.ClassTargets != null)
            {
                HashSet<string> classes = new HashSet<string>();
                foreach (ClassTargetSettings target in config.ClassTargets)
                {
                    if (target == null || string.IsNullOrEmpty(target.WeaponClass) || !classes.Add(target.WeaponClass)) { errors.Add("classTargets: missing or duplicate class"); continue; }
                    string owner = "classTargets " + target.WeaponClass;
                    Positive(errors, owner + " firstShotConeMaxDegrees", target.FirstShotConeMaxDegrees);
                    if (target.BurstShotCount < 1 || target.BurstShotCount > 30) { errors.Add(owner + " burstShotCount must be 1-30"); }
                    Positive(errors, owner + " burstRecoveryMaxMilliseconds", target.BurstRecoveryMaxMilliseconds);
                    if (!(target.RecoveryToleranceFraction >= 0 && target.RecoveryToleranceFraction <= 1)) { errors.Add(owner + " recoveryToleranceFraction must be 0-1"); }
                    if (target.SustainedShots < 0 || target.SustainedShots > 200) { errors.Add(owner + " sustainedShots must be 0-200"); }
                    if (target.SustainedShots > 0 && !(target.ClimbMinDegrees >= 0 && target.ClimbMaxDegrees >= target.ClimbMinDegrees)) { errors.Add(owner + " needs 0 <= climbMinDegrees <= climbMaxDegrees"); }
                }
            }

            if (config.Reticles != null) { ValidateReticles(errors, config); }

            foreach (TuningParameter parameter in config.Tuning)
            {
                if (parameter == null || !ProfileParameters.IsKnown(parameter.Key)) { errors.Add("unknown tuning key " + (parameter == null ? "null" : parameter.Key)); continue; }
                if (parameter.Step <= 0 || parameter.Maximum < parameter.Minimum) { errors.Add("tuning " + parameter.Key + " has invalid step/range"); }
            }

            TestRangeSettings range = config.TestRange;
            if (range.TargetDistancesMeters == null || range.TargetDistancesMeters.Length == 0) { errors.Add("testRange.targetDistancesMeters is empty"); }
            if (range.AmmoRefillRounds <= 0) { errors.Add("testRange.ammoRefillRounds must be positive"); }
            if (range.HealthRefill <= 100 || range.ArmorRefill < 0) { errors.Add("testRange.healthRefill must exceed 100 (GTA IV death threshold) and armorRefill must be >= 0"); }

            if (errors.Count > 0) { throw new InvalidDataException(string.Join("; ", errors.ToArray())); }
        }

        internal static void ValidateProfiles(List<string> errors, string owner, RecoilProfile recoil, SpreadProfile spread)
        {
            if (recoil == null || spread == null) { errors.Add(owner + " needs recoil and spread"); return; }
            NonNegative(errors, owner + " recoil.verticalKickDegrees", recoil.VerticalKickDegrees);
            NonNegative(errors, owner + " recoil.horizontalRandomDegrees", recoil.HorizontalRandomDegrees);
            if (Math.Abs(recoil.VerticalKickDegrees) > 20 || Math.Abs(recoil.HorizontalKickDegrees) > 20) { errors.Add(owner + " recoil kick beyond 20 degrees"); }
            Positive(errors, owner + " recoil.firstShotMultiplier", recoil.FirstShotMultiplier);
            NonNegative(errors, owner + " recoil.sustainedFireGrowthPerShot", recoil.SustainedFireGrowthPerShot);
            if (recoil.SustainedFireShotCap < 0) { errors.Add(owner + " recoil.sustainedFireShotCap negative"); }
            NonNegative(errors, owner + " recoil.maxAccumulatedDegrees", recoil.MaxAccumulatedDegrees);
            Fraction(errors, owner + " recoil.minimumKickFractionAtCap", recoil.MinimumKickFractionAtCap);
            NonNegative(errors, owner + " recoil.kickDurationMilliseconds", recoil.KickDurationMilliseconds);
            NonNegative(errors, owner + " recoil.recoveryDelayMilliseconds", recoil.RecoveryDelayMilliseconds);
            NonNegative(errors, owner + " recoil.recoveryDegreesPerSecond", recoil.RecoveryDegreesPerSecond);
            Fraction(errors, owner + " recoil.recoveryFraction", recoil.RecoveryFraction);
            NonNegative(errors, owner + " recoil.movingMultiplier", recoil.MovingMultiplier);
            NonNegative(errors, owner + " recoil.crouchedMultiplier", recoil.CrouchedMultiplier);
            NonNegative(errors, owner + " recoil.coverMultiplier", recoil.CoverMultiplier);
            NonNegative(errors, owner + " recoil.vehicleMultiplier", recoil.VehicleMultiplier);
            NonNegative(errors, owner + " recoil.hipFireMultiplier", recoil.HipFireMultiplier);

            NonNegative(errors, owner + " spread.baseDegrees", spread.BaseDegrees);
            NonNegative(errors, owner + " spread.perShotDegrees", spread.PerShotDegrees);
            if (spread.BurstShotCount < 1) { errors.Add(owner + " spread.burstShotCount must be >= 1"); }
            Fraction(errors, owner + " spread.burstPerShotMultiplier", spread.BurstPerShotMultiplier);
            NonNegative(errors, owner + " spread.chainResetMilliseconds", spread.ChainResetMilliseconds);
            if (spread.MaxDegrees < spread.BaseDegrees || spread.MaxDegrees > 30) { errors.Add(owner + " spread.maxDegrees must be >= base and <= 30"); }
            NonNegative(errors, owner + " spread.recoveryDelayMilliseconds", spread.RecoveryDelayMilliseconds);
            NonNegative(errors, owner + " spread.recoveryDegreesPerSecond", spread.RecoveryDegreesPerSecond);
            Positive(errors, owner + " spread.shortBurstRecoveryDegreesPerSecond", spread.ShortBurstRecoveryDegreesPerSecond);
            NonNegative(errors, owner + " spread.movingAddDegrees", spread.MovingAddDegrees);
            Positive(errors, owner + " spread.crouchedMultiplier", spread.CrouchedMultiplier);
            Positive(errors, owner + " spread.coverMultiplier", spread.CoverMultiplier);
            Positive(errors, owner + " spread.vehicleMultiplier", spread.VehicleMultiplier);
            Positive(errors, owner + " spread.hipFireMultiplier", spread.HipFireMultiplier);
            Positive(errors, owner + " spread.blindFireMultiplier", spread.BlindFireMultiplier);
            Positive(errors, owner + " spread.airborneMultiplier", spread.AirborneMultiplier);
            NonNegative(errors, owner + " spread.pelletPatternDegrees", spread.PelletPatternDegrees);
        }

        // T-043: the section itself, then every class and weapon entry as it will be drawn (resolved over the base crosshair).
        private static void ValidateReticles(List<string> errors, GunplayConfig config)
        {
            ReticleSettings settings = config.Reticles;
            if (settings.NotAiming != "hidden" && settings.NotAiming != "reduced") { errors.Add("reticles.notAiming must be hidden or reduced"); }
            Fraction(errors, "reticles.notAimingOpacity", settings.NotAimingOpacity);
            if (settings.Classes == null) { errors.Add("reticles.classes is missing"); return; }
            HashSet<string> classes = new HashSet<string>();
            foreach (ReticleClassSettings entry in settings.Classes)
            {
                if (entry == null || string.IsNullOrEmpty(entry.WeaponClass) || entry.Style == null || !classes.Add(entry.WeaponClass)) { errors.Add("reticles.classes: missing or duplicate class"); continue; }
                ValidateResolvedReticle(errors, "reticles class " + entry.WeaponClass, ReticleResolver.Resolve(config, entry.WeaponClass, 0), entry.Style);
            }
            HashSet<int> weapons = new HashSet<int>();
            if (settings.Weapons != null)
            {
                foreach (ReticleWeaponSettings entry in settings.Weapons)
                {
                    if (entry == null || entry.WeaponId < 1 || entry.WeaponId > 127 || !weapons.Add(entry.WeaponId)) { errors.Add("reticles.weapons: bad or duplicate weaponId"); continue; }
                    if (!string.IsNullOrEmpty(entry.WeaponClass) && !classes.Contains(entry.WeaponClass)) { errors.Add("reticles weapon " + entry.WeaponId + " names unknown class " + entry.WeaponClass); continue; }
                    ValidateResolvedReticle(errors, "reticles weapon " + entry.WeaponId, ReticleResolver.Resolve(config, entry.WeaponClass, entry.WeaponId), entry.Style);
                }
            }
        }

        private static void ValidateResolvedReticle(List<string> errors, string owner, ResolvedReticle style, ReticleStyleSettings named)
        {
            if (Array.IndexOf(ReticleResolver.Styles, style.Style) < 0) { errors.Add(owner + ": style must be one of " + string.Join(", ", ReticleResolver.Styles)); }
            Positive(errors, owner + " lineLengthPixels", style.LineLengthPixels);
            Positive(errors, owner + " lineThicknessPixels", style.LineThicknessPixels);
            NonNegative(errors, owner + " outlinePixels", style.OutlinePixels);
            NonNegative(errors, owner + " minimumGapPixels", style.MinimumGapPixels);
            if (style.MaximumGapPixels < style.MinimumGapPixels) { errors.Add(owner + ": maximumGapPixels < minimumGapPixels"); }
            NonNegative(errors, owner + " centerDotPixels", style.CenterDotPixels);
            if (style.RingDots < 3 || style.RingDots > 64) { errors.Add(owner + ": ringDots must be 3-64"); }
            Positive(errors, owner + " gapSmoothingPerSecond", style.GapSmoothingPerSecond);
            if (named != null && named.ColorArgb != null) { Argb(errors, owner + " colorArgb", named.ColorArgb); }
            if (named != null && named.OutlineArgb != null) { Argb(errors, owner + " outlineArgb", named.OutlineArgb); }
        }

        private static bool ShoulderSwapSettings_IsButton(string name)
        {
            return name == "LeftShoulder" || name == "RightShoulder" || name == "LeftThumb" || name == "RightThumb" || name == "XButton" || name == "YButton";
        }

        private static void Positive(List<string> errors, string name, double value)
        {
            if (!(value > 0) || double.IsInfinity(value)) { errors.Add(name + " must be > 0"); }
        }

        private static void NonNegative(List<string> errors, string name, double value)
        {
            if (!(value >= 0) || double.IsInfinity(value)) { errors.Add(name + " must be >= 0"); }
        }

        private static void Fraction(List<string> errors, string name, double value)
        {
            if (!(value >= 0 && value <= 1)) { errors.Add(name + " must be in [0,1]"); }
        }

        private static void Argb(List<string> errors, string name, int[] value)
        {
            if (value == null || value.Length != 4) { errors.Add(name + " needs 4 values"); return; }
            foreach (int component in value)
            {
                if (component < 0 || component > 255) { errors.Add(name + " values must be 0-255"); return; }
            }
        }
    }
}
