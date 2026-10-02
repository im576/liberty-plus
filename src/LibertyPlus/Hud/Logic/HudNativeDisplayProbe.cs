namespace LibertyFramework.Hud.Logic
{
    // Diagnostic lease only: disabling the config or losing safe gameplay cancels it, rather than rearming on return.
    internal sealed class HudNativeDisplayProbe
    {
        internal bool Requested { get; private set; }
        internal bool Applied { get; private set; }
        private long untilMilliseconds;

        internal void Arm(long nowMilliseconds, int durationMilliseconds)
        {
            Requested = true;
            untilMilliseconds = nowMilliseconds + durationMilliseconds;
        }

        internal void Cancel() { Requested = false; }

        // 1 = enforce this frame; -1 = release our visibility owner; 0 = leave the game alone.
        internal int Update(long nowMilliseconds, bool enabled, bool safeGameplay)
        {
            if (!enabled || !safeGameplay || nowMilliseconds >= untilMilliseconds) { Cancel(); }
            if (Requested) { Applied = true; return 1; }
            if (!Applied) { return 0; }
            Applied = false;
            return -1;
        }
    }
}
