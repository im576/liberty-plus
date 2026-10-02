using System;
using System.Collections.Generic;
using LibertyFramework.Gunplay.Profiles;

namespace LibertyFramework.Gunplay.Spread
{
    // Converts between the spread cone (degrees) and the game's CWeaponInfo accuracy value, and
    // closes the loop with measured bullet traces: if real deviations exceed the intended cone,
    // the gain rises so less accuracy is written, and vice versa. The game places each bullet at a
    // uniformly random radius inside the cone, so the unbiased estimate of the true cone from N
    // samples is max * (N + 1) / N.
    internal sealed class SpreadCalibrator
    {
        private readonly Queue<double> ratios = new Queue<double>();
        private double gain = 1.0;

        internal double Gain { get { return gain; } }
        internal int SampleCount { get { return ratios.Count; } }
        internal double LastEstimatedRatio { get; private set; }
        internal int Updates { get; private set; }

        internal void Reset()
        {
            ratios.Clear();
            gain = 1.0;
            LastEstimatedRatio = 0;
            Updates = 0;
        }

        internal double ToAccuracy(SpreadCalibrationSettings settings, double spreadDegrees)
        {
            double tangent = ShotGeometry.DegreesToTangent(Math.Max(0, spreadDegrees));
            double accuracy = tangent / (settings.TangentPerAccuracyUnit * gain);
            return Math.Max(settings.MinimumAccuracyValue, accuracy);
        }

        internal double FromAccuracy(SpreadCalibrationSettings settings, double accuracy)
        {
            return ShotGeometry.TangentToDegrees(Math.Max(0, accuracy) * settings.TangentPerAccuracyUnit * gain);
        }

        // Returns true when the gain changed.
        internal bool AddSample(SpreadCalibrationSettings settings, double measuredDegrees, double intendedDegrees)
        {
            if (double.IsNaN(measuredDegrees) || intendedDegrees < settings.MinimumSampleSpreadDegrees) { return false; }
            if (measuredDegrees > settings.MaximumMeasurableDeviationDegrees) { return false; }
            ratios.Enqueue(measuredDegrees / intendedDegrees);
            while (ratios.Count > settings.SampleWindow) { ratios.Dequeue(); }
            if (!settings.AutoCalibrate || ratios.Count < settings.SampleWindow) { return false; }

            double maximum = 0;
            foreach (double ratio in ratios) { maximum = Math.Max(maximum, ratio); }
            double estimate = maximum * (ratios.Count + 1) / ratios.Count;
            LastEstimatedRatio = estimate;
            if (estimate <= 0) { return false; }
            double updated = gain * Math.Pow(estimate, settings.GainAdjustRate);
            updated = Math.Max(settings.MinimumGain, Math.Min(settings.MaximumGain, updated));
            ratios.Clear();
            Updates++;
            bool changed = Math.Abs(updated - gain) > 1e-6;
            gain = updated;
            return changed;
        }
    }
}
