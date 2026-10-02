using System;
using System.Collections.Generic;
using LibertyFramework.Gunplay.Profiles;
using LibertyFramework.Gunplay.Recoil;
using LibertyFramework.Gunplay.Spread;

namespace LibertyFramework.Gunplay.Logic
{
    // What the spread and recoil model delivers for one weapon profile in the three T-042 situations: a first aimed shot,
    // a burst followed by a pause, and a sustained full-auto burst. Pure model run (no game calls): the same SpreadModel and
    // RecoilSolver the game loop uses, stepped at a fixed frame rate with the weapon's own fire interval.
    internal sealed class GunplaySimulationResult
    {
        internal double FirstShotConeDegrees;
        internal double BurstPeakConeDegrees;
        // Milliseconds from the last burst shot until the cone is back within tolerance of the first-shot cone (-1 = never within the window).
        internal double BurstRecoveryMilliseconds;
        // Cone right after each sustained shot (empty when the class has no sustained target).
        internal double[] SustainedCones = new double[0];
        internal bool SustainedGrowsEveryShot;
        // Peak camera pitch displacement (degrees) during the sustained burst.
        internal double ClimbDegrees;
        internal double MaximumConeDegrees;
    }

    internal static class GunplaySimulation
    {
        private const double FrameSeconds = 1.0 / 60.0;
        private const double RecoveryWindowMilliseconds = 5000;

        internal static GunplaySimulationResult Run(WeaponProfile profile, MovementSettings movement, ClassTargetSettings target, double fireIntervalMilliseconds)
        {
            ShooterState standing = new ShooterState();
            standing.Aiming = true;
            GunplaySimulationResult result = new GunplaySimulationResult();
            result.MaximumConeDegrees = profile.Spread.MaxDegrees;

            SpreadModel spread = new SpreadModel();
            result.FirstShotConeDegrees = spread.Current(profile.Spread, movement, standing);

            // Burst, then a pause: frames of no fire until the cone is back.
            double now = 0;
            for (int shot = 0; shot < target.BurstShotCount; shot++)
            {
                if (shot > 0) { now = Advance(spread, profile, movement, standing, now, fireIntervalMilliseconds); }
                spread.AddShot(profile.Spread, now);
            }
            result.BurstPeakConeDegrees = spread.Current(profile.Spread, movement, standing);
            double lastShot = now;
            double limit = result.FirstShotConeDegrees * (1.0 + target.RecoveryToleranceFraction) + 1e-9;
            result.BurstRecoveryMilliseconds = -1;
            if (result.BurstPeakConeDegrees <= limit) { result.BurstRecoveryMilliseconds = 0; }
            else
            {
                for (double waited = 0; waited < RecoveryWindowMilliseconds; waited += FrameSeconds * 1000.0)
                {
                    now += FrameSeconds * 1000.0;
                    double cone = spread.Step(profile.Spread, movement, standing, now, FrameSeconds);
                    if (cone <= limit) { result.BurstRecoveryMilliseconds = now - lastShot; break; }
                }
            }

            if (target.SustainedShots <= 0) { return result; }

            // Sustained full-auto fire from a rested weapon: cone after each shot, and the camera climb.
            spread = new SpreadModel();
            RecoilSolver recoil = new RecoilSolver(1);
            double recoilMultiplier = RecoilMultiplier.For(profile.Recoil, movement, standing);
            List<double> cones = new List<double>();
            double pitch = 0, peak = 0;
            now = 100000;
            double nextShot = now;
            int fired = 0;
            while (fired < target.SustainedShots)
            {
                if (now + 1e-6 >= nextShot)
                {
                    spread.AddShot(profile.Spread, now);
                    recoil.AddShot(profile.Recoil, now, recoilMultiplier);
                    cones.Add(spread.Current(profile.Spread, movement, standing));
                    fired++;
                    nextShot += fireIntervalMilliseconds;
                }
                spread.Step(profile.Spread, movement, standing, now, FrameSeconds);
                pitch += recoil.Step(profile.Recoil, now, FrameSeconds, false).PitchDegrees;
                peak = Math.Max(peak, pitch);
                now += FrameSeconds * 1000.0;
            }
            // The last kick is still easing in when the last shot is fired.
            for (int frame = 0; frame < 30; frame++)
            {
                pitch += recoil.Step(profile.Recoil, now, FrameSeconds, false).PitchDegrees;
                peak = Math.Max(peak, pitch);
                now += FrameSeconds * 1000.0;
                if (now - (nextShot - fireIntervalMilliseconds) >= profile.Recoil.RecoveryDelayMilliseconds) { break; }
            }
            result.SustainedCones = cones.ToArray();
            result.ClimbDegrees = peak;
            result.SustainedGrowsEveryShot = true;
            for (int index = 1; index < result.SustainedCones.Length; index++)
            {
                bool atCap = result.SustainedCones[index - 1] >= result.MaximumConeDegrees - 1e-9;
                if (result.SustainedCones[index] <= result.SustainedCones[index - 1] + 1e-12 && !atCap) { result.SustainedGrowsEveryShot = false; }
            }
            return result;
        }

        // Time passes with the trigger released between two shots: the model recovers frame by frame.
        private static double Advance(SpreadModel spread, WeaponProfile profile, MovementSettings movement, ShooterState state, double now, double intervalMilliseconds)
        {
            double end = now + intervalMilliseconds;
            while (now + FrameSeconds * 1000.0 < end)
            {
                now += FrameSeconds * 1000.0;
                spread.Step(profile.Spread, movement, state, now, FrameSeconds);
            }
            return end;
        }

        // The ways a result misses its class target, as text; empty when it passes.
        internal static List<string> Violations(GunplaySimulationResult result, ClassTargetSettings target)
        {
            List<string> problems = new List<string>();
            if (result.FirstShotConeDegrees > target.FirstShotConeMaxDegrees + 1e-9)
            { problems.Add("first-shot cone " + result.FirstShotConeDegrees.ToString("0.###") + " > " + target.FirstShotConeMaxDegrees.ToString("0.###") + " deg"); }
            if (result.BurstRecoveryMilliseconds < 0 || result.BurstRecoveryMilliseconds > target.BurstRecoveryMaxMilliseconds)
            { problems.Add("burst recovery " + (result.BurstRecoveryMilliseconds < 0 ? "never" : result.BurstRecoveryMilliseconds.ToString("0") + " ms") + " > " + target.BurstRecoveryMaxMilliseconds.ToString("0") + " ms"); }
            if (target.SustainedShots > 0)
            {
                if (!result.SustainedGrowsEveryShot) { problems.Add("spread does not grow with every sustained shot (laser)"); }
                if (result.ClimbDegrees < target.ClimbMinDegrees || result.ClimbDegrees > target.ClimbMaxDegrees)
                { problems.Add("climb " + result.ClimbDegrees.ToString("0.##") + " deg outside " + target.ClimbMinDegrees.ToString("0.##") + "-" + target.ClimbMaxDegrees.ToString("0.##")); }
            }
            return problems;
        }
    }
}
