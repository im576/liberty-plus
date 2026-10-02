using System;
using LibertyFramework.Gunplay.Profiles;

namespace LibertyFramework.Gunplay.Recoil
{
    // Camera-kick state for the currently held weapon. Pure math: the caller feeds shots and
    // frame time and applies the returned pitch/heading deltas to the aim camera.
    // Kick is eased in over kickDuration; a configured fraction is recovered after a delay,
    // and recovery is abandoned when the player counter-steers so the two never fight.
    internal sealed class RecoilSolver
    {
        private readonly Random random;
        private int chainShots;
        private double lastShotMilliseconds = double.NegativeInfinity;
        private double pendingPitch;
        private double pendingHeading;
        private double pendingRemainingMilliseconds;
        private double recoverablePitch;
        private double recoverableHeading;
        private double accumulatedPitch;

        internal RecoilSolver(int seed)
        {
            random = new Random(seed);
        }

        internal int ChainShots { get { return chainShots; } }
        internal double AccumulatedPitchDegrees { get { return accumulatedPitch; } }
        internal double RecoverablePitchDegrees { get { return recoverablePitch; } }
        internal double LastKickPitchDegrees { get; private set; }
        internal double LastKickHeadingDegrees { get; private set; }

        internal void Reset()
        {
            chainShots = 0;
            lastShotMilliseconds = double.NegativeInfinity;
            pendingPitch = 0;
            pendingHeading = 0;
            pendingRemainingMilliseconds = 0;
            recoverablePitch = 0;
            recoverableHeading = 0;
            accumulatedPitch = 0;
        }

        // stateMultiplier combines moving/crouched/cover/vehicle/hip-fire multipliers for this shot.
        internal void AddShot(RecoilProfile profile, double nowMilliseconds, double stateMultiplier)
        {
            if (nowMilliseconds - lastShotMilliseconds > profile.ChainResetMilliseconds)
            {
                chainShots = 0;
                accumulatedPitch = 0;
            }
            chainShots++;
            lastShotMilliseconds = nowMilliseconds;

            double shotMultiplier = chainShots == 1 ? profile.FirstShotMultiplier :
                1.0 + profile.SustainedFireGrowthPerShot * Math.Min(chainShots - 1, profile.SustainedFireShotCap);
            double capFactor = 1.0;
            if (profile.MaxAccumulatedDegrees > 0)
            {
                capFactor = Clamp(1.0 - accumulatedPitch / profile.MaxAccumulatedDegrees, profile.MinimumKickFractionAtCap, 1.0);
            }
            double scale = shotMultiplier * stateMultiplier * capFactor;
            double pitch = profile.VerticalKickDegrees * scale;
            double heading = (profile.HorizontalKickDegrees + (random.NextDouble() * 2.0 - 1.0) * profile.HorizontalRandomDegrees) * scale;

            pendingPitch += pitch;
            pendingHeading += heading;
            pendingRemainingMilliseconds = Math.Max(profile.KickDurationMilliseconds, 1.0);
            accumulatedPitch += pitch;
            recoverablePitch += pitch * profile.RecoveryFraction;
            recoverableHeading += heading * profile.RecoveryFraction;
            LastKickPitchDegrees = pitch;
            LastKickHeadingDegrees = heading;
        }

        // Returns the camera delta for this frame. Positive pitch = aim up.
        internal RecoilStep Step(RecoilProfile profile, double nowMilliseconds, double deltaSeconds, bool playerCounterSteering)
        {
            double deltaMilliseconds = deltaSeconds * 1000.0;
            double pitch = 0;
            double heading = 0;

            if (pendingRemainingMilliseconds > 0)
            {
                double fraction = Math.Min(1.0, deltaMilliseconds / pendingRemainingMilliseconds);
                double pitchPart = pendingPitch * fraction;
                double headingPart = pendingHeading * fraction;
                pitch += pitchPart;
                heading += headingPart;
                pendingPitch -= pitchPart;
                pendingHeading -= headingPart;
                pendingRemainingMilliseconds -= deltaMilliseconds;
                if (pendingRemainingMilliseconds <= 0)
                {
                    pitch += pendingPitch;
                    heading += pendingHeading;
                    pendingPitch = 0;
                    pendingHeading = 0;
                }
            }

            if (playerCounterSteering)
            {
                // The player is correcting by hand; do not also pull the camera.
                recoverablePitch = 0;
                recoverableHeading = 0;
            }
            else if (nowMilliseconds - lastShotMilliseconds >= profile.RecoveryDelayMilliseconds && recoverablePitch > 0)
            {
                double amount = Math.Min(recoverablePitch, profile.RecoveryDegreesPerSecond * deltaSeconds);
                double ratio = recoverablePitch > 1e-9 ? amount / recoverablePitch : 1.0;
                double headingAmount = recoverableHeading * ratio;
                pitch -= amount;
                heading -= headingAmount;
                recoverablePitch -= amount;
                recoverableHeading -= headingAmount;
                accumulatedPitch = Math.Max(0, accumulatedPitch - amount);
            }

            return new RecoilStep(pitch, heading);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }
    }
}
