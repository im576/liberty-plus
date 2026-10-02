using System;

namespace LibertyFramework.DevTools.Teleport.Logic
{
    // Keep invalid results from the game's pavement/ground queries out of SET_CHAR_COORDINATES. The requested
    // location is already loaded and safe to fall back to if a nearby snap is unavailable.
    internal static class TeleportSnap
    {
        private const double MaximumSnapDistanceMeters = 100.0;

        internal static bool IsUsablePavement(float requestedX, float requestedY, float requestedZ,
            float pavementX, float pavementY, float pavementZ)
        {
            if (!Finite(pavementX) || !Finite(pavementY) || !Finite(pavementZ)) { return false; }
            double lengthSquared = (double)pavementX * pavementX + (double)pavementY * pavementY + (double)pavementZ * pavementZ;
            if (lengthSquared <= 1.0) { return false; } // zero vector is the native's no-result value
            double dx = (double)pavementX - requestedX, dy = (double)pavementY - requestedY, dz = (double)pavementZ - requestedZ;
            return dx * dx + dy * dy + dz * dz <= MaximumSnapDistanceMeters * MaximumSnapDistanceMeters;
        }

        internal static bool IsUsableGround(float requestedZ, float groundZ)
        {
            return Finite(groundZ) && groundZ != 0 && Math.Abs((double)groundZ - requestedZ) <= MaximumSnapDistanceMeters;
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
