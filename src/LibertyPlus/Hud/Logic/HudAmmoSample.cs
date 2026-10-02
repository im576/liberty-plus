namespace LibertyFramework.Hud.Logic
{
    // Keep the displayed clip and total from the same poll. Mixing a new world-snapshot clip with an older total invents reserve rounds.
    internal sealed class HudAmmoSample
    {
        internal int Weapon { get; private set; } = -1;
        internal int Clip { get; private set; } = -1;
        internal int Total { get; private set; } = -1;
        private int observedClip = -1;
        private long sampledAt;

        internal bool NeedsRefresh(int weapon, int currentObservedClip, long now, long interval)
        {
            return Weapon != weapon || observedClip != currentObservedClip || now - sampledAt >= interval;
        }

        internal void Capture(int weapon, int currentObservedClip, int clip, int total, long now)
        {
            Weapon = weapon;
            observedClip = currentObservedClip;
            Clip = clip;
            Total = total;
            sampledAt = now;
        }

        internal void Reset()
        {
            Weapon = Clip = Total = observedClip = -1;
            sampledAt = 0;
        }
    }
}
