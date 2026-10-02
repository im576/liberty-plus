namespace LibertyFramework.Hud.Logic
{
    // Inventory ammo is not held-weapon proof. Even agreement of both current readings is only a numeric baseline, never a visible one.
    internal static class HudBaselineReadback
    {
        internal static bool HasHeldAmmo(int expected, int held, int snapshot, bool owned, int clip, int total)
        {
            return expected > 0 && held == expected && snapshot == expected && owned && clip > 0 && total > 0;
        }
    }
}
