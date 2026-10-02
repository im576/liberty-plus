using System;
using System.Collections.Generic;

namespace LibertyFramework.Gunplay.Logic
{
    // One recorded bullet of a range measurement (T-042).
    internal sealed class RangeShot
    {
        internal int Weapon;
        // 1 = first shot of a chain (after a pause longer than the chain reset), 2 = second, and so on.
        internal int Index;
        internal double GapMilliseconds;
        // Angle between the bullet's real path and the line to the aim point, degrees.
        internal double DeviationDegrees;
        // The cone the spread model had written for this shot, degrees.
        internal double ConeDegrees;
    }

    // Collects the bullets of a range run and summarises delivered spread per weapon: first shots, the rest of a burst and
    // sustained fire. Pure bookkeeping, no game calls (the game loop feeds it; the verifier tests it).
    internal sealed class RangeRecorder
    {
        private readonly List<RangeShot> shots = new List<RangeShot>();
        private double lastShotMilliseconds = double.NegativeInfinity;
        private int lastWeapon = -1;
        private int index;

        internal int Count { get { return shots.Count; } }

        // A new run: forget the bullets and the chain.
        internal void Reset()
        {
            shots.Clear();
            lastShotMilliseconds = double.NegativeInfinity;
            lastWeapon = -1;
            index = 0;
        }
        internal IList<RangeShot> Shots { get { return shots; } }

        internal RangeShot Add(int weapon, double nowMilliseconds, double deviationDegrees, double coneDegrees, double chainResetMilliseconds)
        {
            double gap = nowMilliseconds - lastShotMilliseconds;
            if (gap > chainResetMilliseconds || weapon != lastWeapon) { index = 0; }
            index++;
            RangeShot shot = new RangeShot();
            shot.Weapon = weapon; shot.Index = index; shot.GapMilliseconds = double.IsInfinity(gap) ? -1 : gap;
            shot.DeviationDegrees = deviationDegrees; shot.ConeDegrees = coneDegrees;
            shots.Add(shot);
            lastShotMilliseconds = nowMilliseconds;
            lastWeapon = weapon;
            return shot;
        }

        // One line per weapon and group: n, mean/p95/max deviation, mean cone, and the share of bullets inside the cone (5% + 0.02 deg tolerance).
        internal List<string> Summary()
        {
            List<string> lines = new List<string>();
            List<int> weapons = new List<int>();
            foreach (RangeShot shot in shots) { if (!weapons.Contains(shot.Weapon)) { weapons.Add(shot.Weapon); } }
            foreach (int weapon in weapons)
            {
                lines.Add(Group(weapon, "first", delegate (RangeShot s) { return s.Index == 1; }));
                lines.Add(Group(weapon, "burst", delegate (RangeShot s) { return s.Index >= 2 && s.Index <= 3; }));
                lines.Add(Group(weapon, "sustained", delegate (RangeShot s) { return s.Index >= 4; }));
            }
            return lines;
        }

        private string Group(int weapon, string name, Predicate<RangeShot> include)
        {
            List<double> deviations = new List<double>();
            double coneSum = 0; int inside = 0;
            foreach (RangeShot shot in shots)
            {
                if (shot.Weapon != weapon || !include(shot)) { continue; }
                deviations.Add(shot.DeviationDegrees);
                coneSum += shot.ConeDegrees;
                if (shot.DeviationDegrees <= shot.ConeDegrees * 1.05 + 0.02) { inside++; }
            }
            if (deviations.Count == 0) { return "weapon=" + weapon + " group=" + name + " n=0"; }
            deviations.Sort();
            double sum = 0; foreach (double value in deviations) { sum += value; }
            double p95 = deviations[Math.Min(deviations.Count - 1, (int)Math.Ceiling(deviations.Count * 0.95) - 1)];
            return "weapon=" + weapon + " group=" + name + " n=" + deviations.Count + " mean=" + (sum / deviations.Count).ToString("0.000") +
                " p95=" + p95.ToString("0.000") + " max=" + deviations[deviations.Count - 1].ToString("0.000") +
                " cone_mean=" + (coneSum / deviations.Count).ToString("0.000") + " inside_cone=" + ((double)inside / deviations.Count).ToString("0.00");
        }
    }
}
