namespace LibertyFramework.CombatEffects.Logic
{
    // Blood pools, trails and surface blood (STAGE1 Pillar 2 "persistent aftermath"). The interface is fixed now so
    // the gore module can request surface blood today; the real implementation waits for T-051 (R3), which answers what
    // decal and pool capabilities IV exposes. Until then NullBloodSurface accepts every request and draws nothing, and
    // config bloodSurfaceEnabled stays false. Positions are world metres; amount is 0-1 (severity of the wound).
    internal interface IBloodSurface
    {
        // A pool under a body that is bleeding out. Returns false when it was not (or could not be) created.
        bool AddPool(float x, float y, float z, float amount);
        // A smear from one point to another (a dragged or crawling body).
        bool AddTrail(float fromX, float fromY, float fromZ, float toX, float toY, float toZ, float amount);
        // A splatter on a wall or floor at a hit point, facing along the normal.
        bool AddSplatter(float x, float y, float z, float normalX, float normalY, float normalZ, float amount);
        // Oldest first; called under performance pressure and when the cap is exceeded.
        void RemoveOldest(int count);
        int Count { get; }
    }

    internal sealed class NullBloodSurface : IBloodSurface
    {
        internal int Requests;
        public bool AddPool(float x, float y, float z, float amount) { Requests++; return false; }
        public bool AddTrail(float fromX, float fromY, float fromZ, float toX, float toY, float toZ, float amount) { Requests++; return false; }
        public bool AddSplatter(float x, float y, float z, float normalX, float normalY, float normalZ, float amount) { Requests++; return false; }
        public void RemoveOldest(int count) { }
        public int Count { get { return 0; } }
    }
}
