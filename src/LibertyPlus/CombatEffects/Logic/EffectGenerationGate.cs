namespace LibertyFramework.CombatEffects.Logic
{
    // Diagnostic teardown blocks new owned effects without changing either saved enabled setting or combat behavior.
    internal sealed class EffectGenerationGate
    {
        internal bool Paused { get; private set; }

        internal bool Allows(bool enabled) { return enabled && !Paused; }

        // A diagnostic scope belongs to one running instance, never to its stopped/failed successor.
        internal void Reset() { Paused = false; }

        internal bool Pause()
        {
            if (Paused) return false;
            Paused = true;
            return true;
        }

        internal bool Resume()
        {
            if (!Paused) return false;
            Paused = false;
            return true;
        }
    }
}
