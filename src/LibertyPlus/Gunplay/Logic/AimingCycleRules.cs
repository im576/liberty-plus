using System.Collections.Generic;

namespace LibertyFramework.Gunplay.Logic
{
    internal static class AimingCycleRules
    {
        internal static int NextWeapon(IList<int> candidates, int current, int direction)
        {
            if (candidates == null || candidates.Count == 0 || direction == 0) { return current; }
            int index = candidates.IndexOf(current);
            if (index < 0) { return candidates[direction > 0 ? 0 : candidates.Count - 1]; }
            return candidates[(index + direction + candidates.Count) % candidates.Count];
        }
    }
}
