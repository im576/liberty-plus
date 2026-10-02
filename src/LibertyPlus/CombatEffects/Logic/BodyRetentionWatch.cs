using System;
using System.Collections.Generic;

namespace LibertyFramework.CombatEffects.Logic
{
    // Opt-in fixture evidence. Ambient deaths must not affect a test of three specifically spawned bodies.
    internal sealed class BodyRetentionWatch
    {
        private readonly HashSet<int> targets;
        private readonly HashSet<int> kept = new HashSet<int>();
        private readonly Dictionary<int, BodyLedger.Release> released = new Dictionary<int, BodyLedger.Release>();
        private readonly long minimumAge, maximumAge;

        internal BodyRetentionWatch(IEnumerable<int> handles, long minimum, long maximum)
        {
            targets = new HashSet<int>(handles); minimumAge = minimum; maximumAge = maximum;
        }

        internal void Kept(int handle) { if (targets.Contains(handle)) { kept.Add(handle); } }
        internal void Released(BodyLedger.Release release) { if (targets.Contains(release.Handle)) { released[release.Handle] = release; } }

        internal string Report(Func<int, bool> isHeld)
        {
            int held = 0, expiredInRange = 0, overCap = 0;
            foreach (int handle in targets) { if (isHeld(handle)) { held++; } }
            foreach (BodyLedger.Release release in released.Values)
            {
                if (release.Reason == BodyLedger.Reason.OverCap) { overCap++; }
                if (release.Reason == BodyLedger.Reason.Expired && release.AgeMilliseconds >= minimumAge && release.AgeMilliseconds <= maximumAge)
                { expiredInRange++; }
            }
            return "body_watch targets=" + targets.Count + " kept=" + kept.Count + " held=" + held +
                " released=" + released.Count + " expired_in_range=" + expiredInRange + " over_cap=" + overCap;
        }
    }
}
