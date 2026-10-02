using System;
using System.Collections.Generic;
using System.Globalization;

namespace LibertyFramework.CombatEffects.Logic
{
    // Diagnostic window includes event handlers and the combat tick, grouped by engine frame.
    // Acceptance numbers below are T-047 criteria, not gameplay tuning.
    internal sealed class CombatCostWindow
    {
        private readonly Dictionary<int, double> frames = new Dictionary<int, double>();
        private bool recording;
        private bool overflow;
        // Frames in which a ped was created for a thrown limb: the game call itself is tens of milliseconds and cannot be split.
        private readonly Dictionary<int, double> spawns = new Dictionary<int, double>();

        internal void Start() { frames.Clear(); spawns.Clear(); overflow = false; recording = true; }
        internal void AddSpawn(int frame, double milliseconds)
        {
            if (!recording) return;
            double cost; spawns.TryGetValue(frame, out cost); spawns[frame] = cost + Math.Max(0, milliseconds);
        }
        internal void Add(int frame, double milliseconds)
        {
            if (!recording) return;
            if (frames.Count >= 100000 && !frames.ContainsKey(frame)) { overflow = true; return; }
            double cost; frames.TryGetValue(frame, out cost); frames[frame] = cost + Math.Max(0, milliseconds);
        }
        internal string Finish()
        {
            recording = false;
            double sum = 0, maximum = 0;
            foreach (double cost in frames.Values) { sum += cost; maximum = Math.Max(maximum, cost); }
            double average = frames.Count == 0 ? 0 : sum / frames.Count;
            bool pass = frames.Count > 0 && !overflow && average <= 0.8 && maximum <= 4;
            // The same peak with the limb-clone creation taken out (reported, never used to pass the budget).
            double withoutSpawn = 0;
            foreach (KeyValuePair<int, double> pair in frames)
            {
                double spawn; spawns.TryGetValue(pair.Key, out spawn);
                withoutSpawn = Math.Max(withoutSpawn, pair.Value - spawn);
            }
            return "combat_budget frames=" + frames.Count + " avg_ms=" + average.ToString("0.000", CultureInfo.InvariantCulture) +
                " max_ms=" + maximum.ToString("0.000", CultureInfo.InvariantCulture) + " overflow=" + overflow + " budget=" + (pass ? "PASS" : "FAIL") +
                " spawn_frames=" + spawns.Count + " max_without_clone_spawn_ms=" + withoutSpawn.ToString("0.000", CultureInfo.InvariantCulture);
        }
    }
}
