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
        private BodyRetentionWatch watch;

        internal BodyPersistence(GoreStats stats) { this.stats = stats; }

        internal int Count { get { return ledger.Count; } }

        internal void Watch(IEnumerable<int> handles, CombatEffectsConfig config)
        { watch = new BodyRetentionWatch(handles, config.BodyLifetimeMinimumMilliseconds, config.BodyLifetimeMaximumMilliseconds); }
        internal string WatchReport() { return watch == null ? "error body watch not started" : watch.Report(ledger.Contains); }

        internal bool Keep(Ped ped, long now, float distanceMeters, CombatEffectsConfig config, Func<Ped, bool> isProtected)
        {
            if (!config.BodyPersistenceEnabled || ped == null) return false;
            int handle = ped.GetHashCode();
            if (distanceMeters > config.BodyMaximumDistanceAtDeathMeters) { RuntimeLog.Info("body_not_kept handle=" + handle + " reason=distance meters=" + distanceMeters.ToString("0.0")); return false; }
            if (ledger.Contains(handle)) return false;
            // A script-created ped (a test fixture, or a mission ped) belongs to whoever made it: its owner decides when it goes.
            if (CombatEffectsNatives.IsMissionPed(ped)) { RuntimeLog.Info("body_not_kept handle=" + handle + " reason=mission_owned"); return false; }
            if (isProtected != null && isProtected(ped)) { RuntimeLog.Info("body_not_kept handle=" + handle + " reason=protected"); return false; }
            try { Function.Call("SET_CHAR_AS_MISSION_CHAR", ped); }
            catch (Exception error) { RuntimeLog.Error("body_pin_failed error=" + error.Message); return false; }
            ledger.Add(handle, now, config.BodyLifetimeMinimumMilliseconds, config.BodyLifetimeMaximumMilliseconds);
            peds[handle] = ped;
            stats.BodiesKept++;
            if (watch != null) { watch.Kept(handle); }
            RuntimeLog.Info("body_kept handle=" + handle + " distance=" + distanceMeters.ToString("0.0") + " bodies=" + ledger.Count);
            return true;
        }

        internal void Sweep(CombatEffectsConfig config, long now, float pressure, Vec3 player, IWorldQuery query, Action<Ped> beforeRelease)
        {
            if (!config.BodyPersistenceEnabled) { ReleaseAll(beforeRelease, now); return; }
            if (ledger.Count == 0 || now < nextSweep) return;
            nextSweep = now + config.BodySweepIntervalMilliseconds;
            List<BodyLedger.Release> released = ledger.Sweep(now, pressure, config.MaximumBodies, config.MinimumBodiesUnderPressure,
                config.BodyCleanupDistanceMeters, handle => { Ped ped; return peds.TryGetValue(handle, out ped) && ped.Exists(); },
                handle => { Ped ped; return peds.TryGetValue(handle, out ped) && ped.Exists() ? ped.Position.DistanceTo(new Vector3(player.X, player.Y, player.Z)) : 0f; });
            foreach (BodyLedger.Release release in released) ReleaseOne(release, query, beforeRelease, now);
        }

        private void ReleaseOne(BodyLedger.Release release, IWorldQuery query, Action<Ped> beforeRelease, long now)
        {
            Ped ped;
            peds.TryGetValue(release.Handle, out ped);
            bool deleted = false;
            try
            {
                if (ped != null && ped.Exists())
                {
                    if (beforeRelease != null) beforeRelease(ped);
                    // Cap and pressure evictions remove the body now if nobody is looking; everything else is the game's to clean.
                    if (release.Reason == BodyLedger.Reason.OverCap && !query.IsOnScreen(new PedRef(release.Handle))) { ped.Delete(); deleted = true; }
                    else { ped.NoLongerNeeded(); }
                }
            }
            catch (Exception error)
            {
                ledger.Restore(release, now);
                RuntimeLog.Error("body_release_failed handle=" + release.Handle + " retained=True error=" + error.Message);
                return;
            }
            peds.Remove(release.Handle);
            stats.BodiesReleased++;
            if (watch != null) { watch.Released(release); }
            RuntimeLog.Info("body_released handle=" + release.Handle + " reason=" + release.Reason + " age_s=" + (release.AgeMilliseconds / 1000) + " deleted=" + deleted + " bodies=" + ledger.Count);
        }

        // Module stop: every kept body goes back to the game (natives; call from a tick, not from unload).
        internal void ReleaseAll(Action<Ped> beforeRelease = null, long now = 0)
        {
            int releasing = ledger.Count;
            foreach (BodyLedger.Release release in ledger.DrainReleases(now))
            {
                int handle = release.Handle;
                Ped ped;
                if (peds.TryGetValue(handle, out ped))
                {
                    try { if (ped.Exists()) { if (beforeRelease != null) beforeRelease(ped); ped.NoLongerNeeded(); } }
                    catch (Exception error) { ledger.Restore(release, now); RuntimeLog.Error("body_release_failed handle=" + handle + " retained=True error=" + error.Message); continue; }
                }
                peds.Remove(handle);
                stats.BodiesReleased++;
                if (watch != null) { watch.Released(release); }
            }
            if (releasing > 0) RuntimeLog.Info("body_release_all remaining=" + ledger.Count);
        }
    }
}
