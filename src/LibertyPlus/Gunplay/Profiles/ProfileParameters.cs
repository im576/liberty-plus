using System;
using System.Collections.Generic;

namespace LibertyFramework.Gunplay.Profiles
{
    // Named access to tunable profile fields for live tuning and preset handling.
    // Keys match config/gunplay.json paths ("recoil.verticalKickDegrees").
    internal static class ProfileParameters
    {
        private static readonly Dictionary<string, Func<WeaponProfile, double>> Getters = new Dictionary<string, Func<WeaponProfile, double>>();
        private static readonly Dictionary<string, Action<WeaponProfile, double>> Setters = new Dictionary<string, Action<WeaponProfile, double>>();

        static ProfileParameters()
        {
            Add("recoil.verticalKickDegrees", p => p.Recoil.VerticalKickDegrees, (p, v) => p.Recoil.VerticalKickDegrees = v);
            Add("recoil.horizontalKickDegrees", p => p.Recoil.HorizontalKickDegrees, (p, v) => p.Recoil.HorizontalKickDegrees = v);
            Add("recoil.horizontalRandomDegrees", p => p.Recoil.HorizontalRandomDegrees, (p, v) => p.Recoil.HorizontalRandomDegrees = v);
            Add("recoil.firstShotMultiplier", p => p.Recoil.FirstShotMultiplier, (p, v) => p.Recoil.FirstShotMultiplier = v);
            Add("recoil.sustainedFireGrowthPerShot", p => p.Recoil.SustainedFireGrowthPerShot, (p, v) => p.Recoil.SustainedFireGrowthPerShot = v);
            Add("recoil.sustainedFireShotCap", p => p.Recoil.SustainedFireShotCap, (p, v) => p.Recoil.SustainedFireShotCap = (int)Math.Round(v));
            Add("recoil.chainResetMilliseconds", p => p.Recoil.ChainResetMilliseconds, (p, v) => p.Recoil.ChainResetMilliseconds = v);
            Add("recoil.maxAccumulatedDegrees", p => p.Recoil.MaxAccumulatedDegrees, (p, v) => p.Recoil.MaxAccumulatedDegrees = v);
            Add("recoil.minimumKickFractionAtCap", p => p.Recoil.MinimumKickFractionAtCap, (p, v) => p.Recoil.MinimumKickFractionAtCap = v);
            Add("recoil.kickDurationMilliseconds", p => p.Recoil.KickDurationMilliseconds, (p, v) => p.Recoil.KickDurationMilliseconds = v);
            Add("recoil.recoveryDelayMilliseconds", p => p.Recoil.RecoveryDelayMilliseconds, (p, v) => p.Recoil.RecoveryDelayMilliseconds = v);
            Add("recoil.recoveryDegreesPerSecond", p => p.Recoil.RecoveryDegreesPerSecond, (p, v) => p.Recoil.RecoveryDegreesPerSecond = v);
            Add("recoil.recoveryFraction", p => p.Recoil.RecoveryFraction, (p, v) => p.Recoil.RecoveryFraction = v);
            Add("recoil.movingMultiplier", p => p.Recoil.MovingMultiplier, (p, v) => p.Recoil.MovingMultiplier = v);
            Add("recoil.crouchedMultiplier", p => p.Recoil.CrouchedMultiplier, (p, v) => p.Recoil.CrouchedMultiplier = v);
            Add("recoil.coverMultiplier", p => p.Recoil.CoverMultiplier, (p, v) => p.Recoil.CoverMultiplier = v);
            Add("recoil.vehicleMultiplier", p => p.Recoil.VehicleMultiplier, (p, v) => p.Recoil.VehicleMultiplier = v);
            Add("recoil.hipFireMultiplier", p => p.Recoil.HipFireMultiplier, (p, v) => p.Recoil.HipFireMultiplier = v);
            Add("spread.baseDegrees", p => p.Spread.BaseDegrees, (p, v) => p.Spread.BaseDegrees = v);
            Add("spread.perShotDegrees", p => p.Spread.PerShotDegrees, (p, v) => p.Spread.PerShotDegrees = v);
            Add("spread.burstShotCount", p => p.Spread.BurstShotCount, (p, v) => p.Spread.BurstShotCount = (int)Math.Round(v));
            Add("spread.burstPerShotMultiplier", p => p.Spread.BurstPerShotMultiplier, (p, v) => p.Spread.BurstPerShotMultiplier = v);
            Add("spread.chainResetMilliseconds", p => p.Spread.ChainResetMilliseconds, (p, v) => p.Spread.ChainResetMilliseconds = v);
            Add("spread.maxDegrees", p => p.Spread.MaxDegrees, (p, v) => p.Spread.MaxDegrees = v);
            Add("spread.recoveryDelayMilliseconds", p => p.Spread.RecoveryDelayMilliseconds, (p, v) => p.Spread.RecoveryDelayMilliseconds = v);
            Add("spread.recoveryDegreesPerSecond", p => p.Spread.RecoveryDegreesPerSecond, (p, v) => p.Spread.RecoveryDegreesPerSecond = v);
            Add("spread.shortBurstRecoveryDegreesPerSecond", p => p.Spread.ShortBurstRecoveryDegreesPerSecond, (p, v) => p.Spread.ShortBurstRecoveryDegreesPerSecond = v);
            Add("spread.movingAddDegrees", p => p.Spread.MovingAddDegrees, (p, v) => p.Spread.MovingAddDegrees = v);
            Add("spread.crouchedMultiplier", p => p.Spread.CrouchedMultiplier, (p, v) => p.Spread.CrouchedMultiplier = v);
            Add("spread.coverMultiplier", p => p.Spread.CoverMultiplier, (p, v) => p.Spread.CoverMultiplier = v);
            Add("spread.vehicleMultiplier", p => p.Spread.VehicleMultiplier, (p, v) => p.Spread.VehicleMultiplier = v);
            Add("spread.hipFireMultiplier", p => p.Spread.HipFireMultiplier, (p, v) => p.Spread.HipFireMultiplier = v);
            Add("spread.blindFireMultiplier", p => p.Spread.BlindFireMultiplier, (p, v) => p.Spread.BlindFireMultiplier = v);
            Add("spread.airborneMultiplier", p => p.Spread.AirborneMultiplier, (p, v) => p.Spread.AirborneMultiplier = v);
            Add("spread.pelletPatternDegrees", p => p.Spread.PelletPatternDegrees, (p, v) => p.Spread.PelletPatternDegrees = v);
        }

        private static void Add(string key, Func<WeaponProfile, double> getter, Action<WeaponProfile, double> setter)
        {
            Getters.Add(key, getter);
            Setters.Add(key, setter);
        }

        internal static bool IsKnown(string key)
        {
            return key != null && Getters.ContainsKey(key);
        }

        internal static double Get(WeaponProfile profile, string key)
        {
            return Getters[key](profile);
        }

        internal static void Set(WeaponProfile profile, string key, double value)
        {
            Setters[key](profile, value);
        }

        internal static string ShortName(string key)
        {
            int dot = key.IndexOf('.');
            return (dot > 0 ? key.Substring(0, 1).ToUpperInvariant() + " " : "") + key.Substring(dot + 1);
        }
    }
}
