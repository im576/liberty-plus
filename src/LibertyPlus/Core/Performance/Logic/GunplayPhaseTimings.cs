using System;
using System.Diagnostics;

namespace LibertyFramework.Core.Performance.Logic
{
    // Accumulates coarse per-tick timings without allocation or logging on the game loop.
    internal sealed class GunplayPhaseTimings
    {
        private static readonly string[] Names = { "setup", "camera", "bullets", "weapon", "hud" };
        private readonly long[] totals = new long[5];
        private readonly long[] maxima = new long[5];
        private int samples;

        internal void Observe(long start, long setupEnd, long cameraEnd, long bulletsEnd, long weaponEnd, long end)
        {
            if (start > setupEnd || setupEnd > cameraEnd || cameraEnd > bulletsEnd ||
                bulletsEnd > weaponEnd || weaponEnd > end) { return; }

            Add(0, setupEnd - start);
            Add(1, cameraEnd - setupEnd);
            Add(2, bulletsEnd - cameraEnd);
            Add(3, weaponEnd - bulletsEnd);
            Add(4, end - weaponEnd);
            samples++;
        }

        private void Add(int index, long elapsed)
        {
            totals[index] += elapsed;
            if (elapsed > maxima[index]) { maxima[index] = elapsed; }
        }

        internal string ReportAndReset()
        {
            string report = "phase_samples=" + samples;
            for (int index = 0; index < Names.Length; index++)
            {
                double averageMilliseconds = samples > 0 ? totals[index] * 1000.0 / Stopwatch.Frequency / samples : 0;
                double maximumMilliseconds = maxima[index] * 1000.0 / Stopwatch.Frequency;
                report += " phase_" + Names[index] + "_avg_ms=" + averageMilliseconds.ToString("0.000") +
                    " phase_" + Names[index] + "_max_ms=" + maximumMilliseconds.ToString("0.000");
            }
            Array.Clear(totals, 0, totals.Length);
            Array.Clear(maxima, 0, maxima.Length);
            samples = 0;
            return report;
        }
    }
}
