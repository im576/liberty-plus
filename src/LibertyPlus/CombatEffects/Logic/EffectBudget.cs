using System;
using System.Collections.Generic;

namespace LibertyFramework.CombatEffects.Logic
{
    // The shared cap on active particle emitters (STAGE1 Pillar 2: 24 proposed). Gore (T-047) and combat effects
    // (T-048) each report how many emitters they hold; either side asks how many more it may start. Under performance
    // pressure the cap shrinks toward a floor. Engine thread only.
    internal static class EffectBudget
    {
        private static readonly Dictionary<string, int> active = new Dictionary<string, int>();

        internal static void Report(string source, int count) { active[source] = Math.Max(0, count); }

        internal static int Total
        {
            get { int total = 0; foreach (int count in active.Values) total += count; return total; }
        }

        internal static int Cap(int maximum, int minimumUnderPressure, float pressure)
        {
            float clamped = Math.Max(0f, Math.Min(1f, pressure));
            int cap = (int)Math.Round(maximum * (1.0f - clamped));
            return Math.Max(Math.Min(minimumUnderPressure, maximum), Math.Min(maximum, cap));
        }

        // Emitters that may still start, given what other sources hold (this source's own count is excluded).
        internal static int Available(string source, int maximum, int minimumUnderPressure, float pressure)
        {
            int others = 0;
            foreach (KeyValuePair<string, int> pair in active) if (pair.Key != source) others += pair.Value;
            return Cap(maximum, minimumUnderPressure, pressure) - others;
        }

        internal static void Reset() { active.Clear(); }
    }
}
