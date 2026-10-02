using System;
using System.Collections.Generic;

namespace LibertyFramework.CombatEffects.Logic
{
    // Which kept bodies must go now (T-047 persistence). Pure bookkeeping: the caller supplies time, performance
    // pressure and distances, and releases the returned bodies. Rules, in order: a body older than its lifetime goes
    // (the lifetime is between the configured minimum and maximum, fixed per body, and shrinks to 25% under full
    // pressure); a body farther than the cleanup distance goes; then the oldest go until the count fits the cap
    // (which shrinks toward a floor under pressure). Gameplay performance wins over keeping every body.
    internal sealed class BodyLedger
    {
        internal enum Reason { Expired, Far, OverCap, Missing }

        internal struct Release
        {
            internal int Handle;
            internal Reason Reason;
            internal long AgeMilliseconds;
        }

        private struct Entry
        {
            internal int Handle;
            internal long DiedAt;
            internal long Lifetime;
        }

        private readonly List<Entry> bodies = new List<Entry>();

        internal int Count { get { return bodies.Count; } }

        internal bool Contains(int handle)
        {
            foreach (Entry entry in bodies) if (entry.Handle == handle) return true;
            return false;
        }

        // The body's lifetime is spread over [minimum, maximum] by its handle so a group of kills does not vanish together.
        internal static long LifetimeFor(int handle, long minimumMilliseconds, long maximumMilliseconds)
        {
            long span = Math.Max(0, maximumMilliseconds - minimumMilliseconds);
            uint spread = unchecked((uint)handle * 2654435761u) >> 8;
            return minimumMilliseconds + (span == 0 ? 0 : (long)(spread % (uint)(span + 1)));
        }

        internal void Add(int handle, long now, long minimumMilliseconds, long maximumMilliseconds)
        {
            if (Contains(handle)) return;
            Entry entry = new Entry();
            entry.Handle = handle; entry.DiedAt = now; entry.Lifetime = LifetimeFor(handle, minimumMilliseconds, maximumMilliseconds);
            bodies.Add(entry);
        }

        internal static int EffectiveCap(int maximum, int minimumUnderPressure, float pressure)
        {
            float clamped = Math.Max(0f, Math.Min(1f, pressure));
            int cap = (int)Math.Round(maximum * (1.0f - clamped));
            return Math.Max(Math.Min(minimumUnderPressure, maximum), Math.Min(maximum, cap));
        }

        // exists: false = the ped is gone (removed by the game). distance: metres to the player (null = not checked).
        internal List<Release> Sweep(long now, float pressure, int maximum, int minimumUnderPressure, float cleanupDistanceMeters,
            Func<int, bool> exists, Func<int, float> distance)
        {
            List<Release> released = new List<Release>();
            float clamped = Math.Max(0f, Math.Min(1f, pressure));
            float lifetimeScale = 1.0f - 0.75f * clamped;
            for (int i = 0; i < bodies.Count; )
            {
                Entry entry = bodies[i];
                long age = now - entry.DiedAt;
                Reason reason;
                if (exists != null && !exists(entry.Handle)) reason = Reason.Missing;
                else if (age >= (long)(entry.Lifetime * lifetimeScale)) reason = Reason.Expired;
                else if (distance != null && distance(entry.Handle) > cleanupDistanceMeters) reason = Reason.Far;
                else { i++; continue; }
                released.Add(new Release { Handle = entry.Handle, Reason = reason, AgeMilliseconds = age });
                bodies.RemoveAt(i);
            }
            int cap = EffectiveCap(maximum, minimumUnderPressure, pressure);
            while (bodies.Count > cap)
            {
                int oldest = 0;
                for (int i = 1; i < bodies.Count; i++) if (bodies[i].DiedAt < bodies[oldest].DiedAt) oldest = i;
                released.Add(new Release { Handle = bodies[oldest].Handle, Reason = Reason.OverCap, AgeMilliseconds = now - bodies[oldest].DiedAt });
                bodies.RemoveAt(oldest);
            }
            return released;
        }

        // Every body, oldest first (module stop: release everything).
        internal List<int> Drain()
        {
            List<int> handles = new List<int>();
            foreach (Entry entry in bodies) handles.Add(entry.Handle);
            bodies.Clear();
            return handles;
        }
    }
}
