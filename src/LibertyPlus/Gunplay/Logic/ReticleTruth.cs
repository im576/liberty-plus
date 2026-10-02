using System;

namespace LibertyFramework.Gunplay.Logic
{
    // Truthfulness of the drawn reticle (T-043): while the cone has been steady, the drawn opening must equal the opening the
    // cone asks for (within 5%); while the cone moves the drawn opening may lag (it eases closed). Pure bookkeeping.
    internal sealed class ReticleTruth
    {
        internal const double TolerancePercent = 5.0;
        // The target must have stayed within 0.1% (or 0.02 px) for this long before a frame counts: a slowly closing cone is still moving.
        internal const double SteadyMilliseconds = 400;
        private double lastTarget = double.NaN;
        private double steadySince = double.NaN;

        internal int SteadySamples { get; private set; }
        internal int Violations { get; private set; }
        // Frames (steady or not) where the drawn opening was more than 5% smaller than the cone asks for: the reticle must never under-report spread.
        internal int UnderReports { get; private set; }
        internal double WorstErrorPercent { get; private set; }
        internal int Samples { get; private set; }

        internal void Reset()
        {
            lastTarget = double.NaN;
            steadySince = double.NaN;
            SteadySamples = 0; Violations = 0; UnderReports = 0; WorstErrorPercent = 0; Samples = 0;
        }

        // Returns true when this frame was steady (and was therefore checked).
        internal bool Add(double nowMilliseconds, double targetPixels, double drawnPixels)
        {
            Samples++;
            if (targetPixels > 0 && drawnPixels < targetPixels * (1.0 - TolerancePercent / 100.0)) { UnderReports++; }
            bool moved = double.IsNaN(lastTarget) || Math.Abs(targetPixels - lastTarget) > Math.Max(0.02, lastTarget * 0.001);
            if (moved) { steadySince = nowMilliseconds; }
            lastTarget = targetPixels;
            if (nowMilliseconds - steadySince < SteadyMilliseconds) { return false; }
            SteadySamples++;
            double error = targetPixels <= 0 ? 0 : Math.Abs(drawnPixels - targetPixels) / targetPixels * 100.0;
            if (error > WorstErrorPercent) { WorstErrorPercent = error; }
            if (error > TolerancePercent) { Violations++; }
            return true;
        }
    }
}
