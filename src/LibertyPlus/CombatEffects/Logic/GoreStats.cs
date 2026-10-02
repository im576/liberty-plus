using System.Globalization;

#pragma warning disable 0649

namespace LibertyFramework.CombatEffects.Logic
{
    // Counters the gore module keeps for the autopilot's acceptance trials (`lf gore stats` logs Report()).
    // Every counter is incremented by the code path it names, from the exact event; nothing here is estimated.
    internal sealed class GoreStats
    {
        internal int Hits;              // exact bullet hits on peds that reached the gore pipeline
        internal int InferredHits;      // the same for events without the exact hook (Exact == false)
        internal int SkippedDistance;   // hits beyond effectMaximumDistanceMeters (no particles)
        internal int SkippedBudget;     // hits over the per-frame or active-effect budget (drip only)
        internal int HeadHits;          // firearm head hits inside their class's severe range
        internal int HeadSevere;        // ... of which the severe head effect set spawned
        internal int TraumaHits;        // hits inside a class's trauma range
        internal int TraumaSevere;      // ... of which a trauma effect or a limb cut was started
        internal int Kills;
        internal int Wounded;           // survivable severe hits that started the suffering behaviour
        internal int PanicEvents;       // severe-violence events that looked for bystanders
        internal int PanicEligible;     // bystanders that qualified (on foot, alive, unarmed, in range)
        internal int Panicked;          // ... given a flee task
        internal int PanicVerified;     // ... that had really moved (or died) when checked
        internal int PanicChecked;
        internal int Cuts;              // dismemberment cuts that collapsed
        internal int LimbsFloating;     // thrown limbs removed for floating
        internal int LimbsFlashing;     // thrown limbs re-hidden or re-shown after being made visible
        internal int BodiesKept;
        internal int BodiesReleased;
        internal long LatencySumFrames; // engine frames between the event's frame and the effect call (0 by construction)
        internal int LatencyMaxFrames;

        internal static string Rate(int part, int whole)
        {
            return whole == 0 ? "n/a" : ((double)part / whole).ToString("0.00", CultureInfo.InvariantCulture);
        }

        internal string Report()
        {
            return "gore_stats hits=" + Hits + " inferred=" + InferredHits + " skipped_distance=" + SkippedDistance + " skipped_budget=" + SkippedBudget +
                " head_hits=" + HeadHits + " head_severe=" + HeadSevere + " head_rate=" + Rate(HeadSevere, HeadHits) +
                " trauma_hits=" + TraumaHits + " trauma_severe=" + TraumaSevere + " trauma_rate=" + Rate(TraumaSevere, TraumaHits) +
                " kills=" + Kills + " wounded=" + Wounded + " cuts=" + Cuts + " limbs_floating=" + LimbsFloating + " limbs_flashing=" + LimbsFlashing +
                " panic_events=" + PanicEvents + " panic_eligible=" + PanicEligible + " panicked=" + Panicked + " panic_rate=" + Rate(Panicked, PanicEligible) +
                " panic_checked=" + PanicChecked + " panic_moved=" + PanicVerified + " panic_moved_rate=" + Rate(PanicVerified, PanicChecked) +
                " bodies_kept=" + BodiesKept + " bodies_released=" + BodiesReleased + " latency_max_frames=" + LatencyMaxFrames;
        }
    }
}
