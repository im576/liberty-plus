using System;
using System.Collections.Generic;

namespace LibertyFramework.Gunplay.Spread
{
    // Rolling evidence that the displayed cone matches real bullet traces for one weapon.
    internal sealed class ShotAuditStats
    {
        private const int Capacity = 64;
        private readonly Queue<double> measured = new Queue<double>();
        private readonly Queue<double> intended = new Queue<double>();

        internal int TotalBullets { get; private set; }
        internal double LastMeasuredDegrees { get; private set; }
        internal double LastIntendedDegrees { get; private set; }

        internal void Reset()
        {
            measured.Clear();
            intended.Clear();
            TotalBullets = 0;
            LastMeasuredDegrees = 0;
            LastIntendedDegrees = 0;
        }

        internal void Add(double measuredDegrees, double intendedDegrees)
        {
            if (double.IsNaN(measuredDegrees)) { return; }
            TotalBullets++;
            LastMeasuredDegrees = measuredDegrees;
            LastIntendedDegrees = intendedDegrees;
            measured.Enqueue(measuredDegrees);
            intended.Enqueue(intendedDegrees);
            while (measured.Count > Capacity) { measured.Dequeue(); intended.Dequeue(); }
        }

        internal int WindowCount { get { return measured.Count; } }

        // Share of recent bullets that landed inside the cone shown on screen (5% tolerance).
        internal double InsideConeFraction
        {
            get
            {
                if (measured.Count == 0) { return 0; }
                double[] m = measured.ToArray();
                double[] i = intended.ToArray();
                int inside = 0;
                for (int index = 0; index < m.Length; index++)
                {
                    if (m[index] <= i[index] * 1.05 + 0.02) { inside++; }
                }
                return (double)inside / m.Length;
            }
        }

        internal double MaximumMeasuredDegrees
        {
            get
            {
                double maximum = 0;
                foreach (double value in measured) { maximum = Math.Max(maximum, value); }
                return maximum;
            }
        }

        internal double MeanMeasuredDegrees
        {
            get
            {
                if (measured.Count == 0) { return 0; }
                double sum = 0;
                foreach (double value in measured) { sum += value; }
                return sum / measured.Count;
            }
        }

        internal string Summary()
        {
            return "bullets=" + TotalBullets + " window=" + WindowCount + " inside_cone=" + (InsideConeFraction * 100).ToString("0") +
                "% mean_dev=" + MeanMeasuredDegrees.ToString("0.00") + " max_dev=" + MaximumMeasuredDegrees.ToString("0.00") +
                " last_dev=" + LastMeasuredDegrees.ToString("0.00") + " last_cone=" + LastIntendedDegrees.ToString("0.00");
        }
    }
}
