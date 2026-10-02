using System.Collections.Generic;

namespace LibertyFramework.CombatEffects
{
    internal sealed class PedInjuryState
    {
        internal int LastHealth;
        internal long LastReactionMilliseconds;
        internal int Bleeds;
        internal int LastBone;
        internal long LastAttributedHitMilliseconds;
        internal readonly Dictionary<HitRegion, int> RegionHits = new Dictionary<HitRegion, int>();
        internal bool HeadRemoved;
        internal bool DeathBurst;
        internal bool EngineBleeding;
    }
}
