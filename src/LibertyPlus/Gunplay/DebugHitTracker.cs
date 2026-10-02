using System;
using System.Collections.Generic;
using GTA;
using LibertyFramework.GameApi;

namespace LibertyFramework.Gunplay
{
    // A debug-only health-delta tracker. Damage-source flags distinguish the player's hits
    // from ambient NPC combat; world impacts have no entity health delta.
    internal sealed class DebugHitTracker
    {
        private readonly Dictionary<Ped, int> pedHealth = new Dictionary<Ped, int>();
        private readonly Dictionary<Vehicle, int> vehicleHealth = new Dictionary<Vehicle, int>();
        internal string LastHit = "no hit recorded";
        private double pendingShotMilliseconds = double.NegativeInfinity;
        internal double LastTraceDistanceMeters;

        internal void NoteShot(double now)
        {
            pendingShotMilliseconds = now;
            LastTraceDistanceMeters = 0;
        }

        internal void Reset()
        {
            pedHealth.Clear();
            vehicleHealth.Clear();
            pendingShotMilliseconds = double.NegativeInfinity;
        }

        internal void Sample(Ped shooter, float radiusMeters, double now, double worldDelayMilliseconds)
        {
            HashSet<Ped> seenPeds = new HashSet<Ped>();
            foreach (Ped target in World.GetPeds(shooter.Position, radiusMeters))
            {
                if (target == null || target == shooter || !target.Exists()) { continue; }
                seenPeds.Add(target);
                int previous;
                if (pedHealth.TryGetValue(target, out previous) && target.Health < previous && target.HasBeenDamagedBy(shooter))
                {
                    int bone = DebugHitNatives.LastDamageBone(target);
                    LastHit = "ped bone=" + bone + " distance=" + target.Position.DistanceTo(shooter.Position).ToString("0.0") +
                        "m damage=" + (previous - target.Health);
                    pendingShotMilliseconds = double.NegativeInfinity;
                }
                pedHealth[target] = target.Health;
            }
            foreach (Ped target in new List<Ped>(pedHealth.Keys)) { if (!seenPeds.Contains(target)) { pedHealth.Remove(target); } }

            HashSet<Vehicle> seenVehicles = new HashSet<Vehicle>();
            foreach (Vehicle target in World.GetVehicles(shooter.Position, radiusMeters))
            {
                if (target == null || !target.Exists()) { continue; }
                seenVehicles.Add(target);
                int previous;
                if (vehicleHealth.TryGetValue(target, out previous) && target.Health < previous && DebugHitNatives.VehicleDamagedBy(target, shooter))
                {
                    LastHit = "vehicle distance=" + target.Position.DistanceTo(shooter.Position).ToString("0.0") +
                        "m damage=" + (previous - target.Health);
                    pendingShotMilliseconds = double.NegativeInfinity;
                }
                vehicleHealth[target] = target.Health;
            }
            foreach (Vehicle target in new List<Vehicle>(vehicleHealth.Keys)) { if (!seenVehicles.Contains(target)) { vehicleHealth.Remove(target); } }
            if (pendingShotMilliseconds > 0 && now - pendingShotMilliseconds >= worldDelayMilliseconds)
            {
                LastHit = "world/unknown trace=" + (LastTraceDistanceMeters > 0 ? LastTraceDistanceMeters.ToString("0.0") + "m" : "-") + " damage=0";
                pendingShotMilliseconds = double.NegativeInfinity;
            }
        }
    }
}
