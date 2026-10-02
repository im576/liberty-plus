using GTA;

namespace LibertyFramework.CombatEffects
{
    // A repeating blood effect on one ped bone (bleeding wound, arterial stump spurt). Re-triggered on an
    // interval because the stock effects are one-shot; each pulse follows the bone as the body moves.
    internal sealed class BloodEmitter
    {
        internal Ped Ped;
        internal int Bone;
        internal string Effect;
        internal float Scale;
        internal float EndScaleFraction;
        internal long StartedMilliseconds;
        internal long NextMilliseconds;
        internal long IntervalMilliseconds;
        internal long EndIntervalMilliseconds;
        internal long UntilMilliseconds;
        internal bool Stump;
    }
}
