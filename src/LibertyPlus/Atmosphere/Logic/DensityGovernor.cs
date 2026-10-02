using System;

namespace LibertyFramework.Atmosphere.Logic
{
    // E-5 adaptive population density (no game calls; offline-tested). GTA IV's CPU cost follows how many peds and cars
    // are simulated. A smoothed frame time maps linearly from full density (at or below fullDensityFrameMilliseconds)
    // to the configured minimum (at or above minimumDensityFrameMilliseconds); the output moves at a capped rate so
    // the street never visibly empties at once.
    internal sealed class DensityGovernor
    {
        private double smoothedFrameMilliseconds = -1;
        internal double PedDensity { get; private set; }
        internal double CarDensity { get; private set; }
        internal double SmoothedFrameMilliseconds { get { return smoothedFrameMilliseconds; } }

        internal DensityGovernor() { PedDensity = 1; CarDensity = 1; }

        internal void Observe(DensitySettings settings, double frameMilliseconds, double deltaSeconds)
        {
            if (frameMilliseconds <= 0 || frameMilliseconds > 1000 || deltaSeconds <= 0) { return; }
            double alpha = Math.Min(1.0, deltaSeconds / settings.SmoothingSeconds);
            smoothedFrameMilliseconds = smoothedFrameMilliseconds < 0 ? frameMilliseconds
                : smoothedFrameMilliseconds + (frameMilliseconds - smoothedFrameMilliseconds) * alpha;
            double t = (smoothedFrameMilliseconds - settings.FullDensityFrameMilliseconds) /
                (settings.MinimumDensityFrameMilliseconds - settings.FullDensityFrameMilliseconds);
            t = Math.Max(0, Math.Min(1, t));
            double step = settings.MaximumChangePerSecond * deltaSeconds;
            PedDensity = Approach(PedDensity, 1 - t * (1 - settings.MinimumPedDensity), step);
            CarDensity = Approach(CarDensity, 1 - t * (1 - settings.MinimumCarDensity), step);
        }

        private static double Approach(double value, double target, double step)
        {
            return value < target ? Math.Min(target, value + step) : Math.Max(target, value - step);
        }
    }
}
