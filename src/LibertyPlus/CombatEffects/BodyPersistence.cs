using System;
using System.Collections.Generic;
using GTA;
using GTA.Native;
using Liberty.Sdk;
using LibertyFramework.CombatEffects.Logic;
using LibertyFramework.Core.Logging;

namespace LibertyFramework.CombatEffects
{
    // Bodies stay 3-5 minutes (T-047). A ped that dies within bodyMaximumDistanceAtDeathMeters of the player is pinned with
    // SET_CHAR_AS_MISSION_CHAR (the same call the dismemberment already uses to keep a corpse) and released with
    // MARK_CHAR_AS_NO_LONGER_NEEDED when BodyLedger says so: lifetime over, too far, over the cap, or performance pressure.
    // Mission peds are never touched. Releasing hands the body back to the game, which removes it on its own schedule; a
    // body evicted because of the cap or pressure is deleted when the player cannot see it.
    internal sealed class BodyPersistence
    {
        private readonly BodyLedger ledger = new BodyLedger();
        private readonly Dictionary<int, Ped> peds = new Dictionary<int, Ped>();
        private readonly GoreStats stats;
        private long nextSweep;

        internal BodyPersistence(GoreStats stats) { this.stats = stats; }

        internal int Count { get { return ledger.Count; } }

        internal bool Keep(Ped ped, long now, float distanceMeters, CombatEffectsConfig config, Func<Ped, bool> isProtected)
        {
            if (!config.BodyPersistenceEnabled || ped == null || distanceMeters > config.BodyMaximumDistanceAtDeathMeters) return false;
            int handle = ped.GetHashCode();
            if (ledger.Contains(handle)) return false;
            if (CombatEffectsNatives.IsMissionPed(ped) || (isProtected != null && isProtected(ped))) return false;
            try { Function.Call("SET_CHAR_AS_MISSION_CHAR", ped); }
            catch (Exception error) { RuntimeLog.Error("body_pin_failed error=" + error.Message); return false; }
            ledger.Add(handle, now, config.BodyLifetimeMinimumMilliseconds, config.BodyLifetimeMaximumMilliseconds);
            peds[handle] = ped;
            stats.BodiesKept++;
            RuntimeLog.Info("body_kept handle=" + handle + " distance=" + distanceMeters.ToString("0.0") + " bodies=" + ledger.Count);
            return true;
        }

        internal void Sweep(CombatEffectsConfig config, long now, float pressure, Vec3 player, IWorldQuery query, Func<Ped, bool> isProtected)
        {
            if (ledger.Count == 0 || now < nextSweep) return;
            nextSweep = now + config.BodySweepIntervalMilliseconds;
            List<BodyLedger.Release> released = ledger.Sweep(now, pressure, config.MaximumBodies, config.MinimumBodiesUnderPressure,
                config.BodyCleanupDistanceMeters, handle => { Ped ped; return peds.TryGetValue(handle, out ped) && ped.Exists(); },
                handle => { Ped ped; return peds.TryGetValue(handle, out ped) && ped.Exists() ? ped.Position.DistanceTo(new Vector3(player.X, player.Y, player.Z)) : 0f; });
            foreach (BodyLedger.Release release in released) ReleaseOne(release, query, isProtected);
        }

        private void ReleaseOne(BodyLedger.Release release, IWorldQuery query, Func<Ped, bool> isProtected)
        {
            Ped ped;
            peds.TryGetValue(release.Handle, out ped);
            peds.Remove(release.Handle);
            stats.BodiesReleased++;
            bool deleted = false;
            try
            {
                if (ped != null && ped.Exists() && (isProtected == null || !isProtected(ped)))
                {
                    // Cap and pressure evictions remove the body now if nobody is looking; everything else is the game's to clean.
                    if (release.Reason == BodyLedger.Reason.OverCap && !query.IsOnScreen(new PedRef(release.Handle))) { ped.Delete(); deleted = true; }
                    else { ped.NoLongerNeeded(); }
                }
            }
            catch (Exception error) { RuntimeLog.Error("body_release_failed handle=" + release.Handle + " error=" + error.Message); }
            RuntimeLog.Info("body_released handle=" + release.Handle + " reason=" + release.Reason + " age_s=" + (release.AgeMilliseconds / 1000) + " deleted=" + deleted + " bodies=" + ledger.Count);
        }

        // Module stop: every kept body goes back to the game (natives; call from a tick, not from unload).
        internal void ReleaseAll()
        {
            foreach (int handle in ledger.Drain())
            {
                Ped ped;
                if (peds.TryGetValue(handle, out ped))
                {
                    try { if (ped.Exists()) ped.NoLongerNeeded(); }
                    catch (Exception error) { RuntimeLog.Error("body_release_failed handle=" + handle + " error=" + error.Message); }
                }
                stats.BodiesReleased++;
            }
            peds.Clear();
        }
    }
}
