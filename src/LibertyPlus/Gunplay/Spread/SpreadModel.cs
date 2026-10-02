using System;
using LibertyFramework.Gunplay.Profiles;

namespace LibertyFramework.Gunplay.Spread
{
    // Current bullet-cone half-angle for the held weapon. Bloom grows per shot and decays after
    // a delay; movement adds spread; stance/context multiply it. Pure math, no game calls.
    internal sealed class SpreadModel
    {
        private double bloomDegrees;
        private double lastShotMilliseconds = double.NegativeInfinity;
        private int chainShots;

        internal double BloomDegrees { get { return bloomDegrees; } }

        internal void Reset()
        {
            bloomDegrees = 0;
            lastShotMilliseconds = double.NegativeInfinity;
            chainShots = 0;
        }

        internal void AddShot(SpreadProfile profile, double nowMilliseconds, double attachmentMultiplier = 1.0)
        {
            if (nowMilliseconds - lastShotMilliseconds > profile.ChainResetMilliseconds) { chainShots = 0; }
            chainShots++;
            lastShotMilliseconds = nowMilliseconds;
            double headroom = Math.Max(0, profile.MaxDegrees - profile.BaseDegrees);
            double multiplier = chainShots <= profile.BurstShotCount ? profile.BurstPerShotMultiplier : 1.0;
            bloomDegrees = Math.Min(headroom, bloomDegrees + profile.PerShotDegrees * multiplier * attachmentMultiplier);
        }

        internal double Step(SpreadProfile profile, MovementSettings movement, ShooterState state, double nowMilliseconds, double deltaSeconds)
        {
            if (nowMilliseconds - lastShotMilliseconds >= profile.RecoveryDelayMilliseconds)
            {
                double rate = chainShots <= profile.BurstShotCount ?
                    profile.ShortBurstRecoveryDegreesPerSecond : profile.RecoveryDegreesPerSecond;
                bloomDegrees = Math.Max(0, bloomDegrees - rate * deltaSeconds);
                if (bloomDegrees == 0 && nowMilliseconds - lastShotMilliseconds > profile.ChainResetMilliseconds) { chainShots = 0; }
            }
            return Current(profile, movement, state);
        }

        internal double Current(SpreadProfile profile, MovementSettings movement, ShooterState state)
        {
            double movementFactor = MovementFactor(movement, state.SpeedMetersPerSecond);
            double core = Math.Min(profile.MaxDegrees, profile.BaseDegrees + bloomDegrees + profile.MovingAddDegrees * movementFactor);
            return Math.Max(0, core * StateMultiplier(profile, state));
        }

        internal static double MovementFactor(MovementSettings movement, double speed)
        {
            if (speed <= movement.MovingSpeedThreshold) { return 0; }
            double span = Math.Max(0.01, movement.FullPenaltySpeed - movement.MovingSpeedThreshold);
            return Math.Min(1.0, (speed - movement.MovingSpeedThreshold) / span);
        }

        internal static double StateMultiplier(SpreadProfile profile, ShooterState state)
        {
            double multiplier = 1.0;
            if (state.InVehicle) { multiplier *= profile.VehicleMultiplier; }
            else if (state.BlindFire) { multiplier *= profile.BlindFireMultiplier; }
            else
            {
                if (state.InCover) { multiplier *= profile.CoverMultiplier; }
                else if (state.Crouched) { multiplier *= profile.CrouchedMultiplier; }
                if (!state.Aiming) { multiplier *= profile.HipFireMultiplier; }
            }
            if (state.Airborne) { multiplier *= profile.AirborneMultiplier; }
            return multiplier;
        }
    }
}
