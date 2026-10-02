using System;
using System.Collections.Generic;
using Liberty.Sdk;
using LibertyFramework.CombatEffects.Logic;

namespace LibertyFramework.CombatEffects
{
    // Strong panic after severe violence (T-047): unarmed pedestrians on foot near the event run from the attacker with the
    // game's own flee task, and some cry out. Whether they really ran is measured: panicVerifyMilliseconds later each
    // panicked ped is checked (moved at least panicMovedMeters, or dead) and counted in GoreStats. Armed peds are left
    // to fight or flee as the game decides; a ped panics again only after panicCooldownMilliseconds.
    internal sealed class PanicReaction
    {
        private struct Check
        {
            internal PedRef Ped;
            internal Vec3 Start;
            internal long Due;
        }

        private readonly ILiberty liberty;
        private readonly LibertyModule owner;
        private readonly GoreStats stats;
        private readonly Random random = new Random(9191);
        private readonly Dictionary<int, long> cooldownUntil = new Dictionary<int, long>();
        private readonly List<Check> checks = new List<Check>();
        private readonly List<PedState> nearby = new List<PedState>();

        internal PanicReaction(LibertyModule owner, ILiberty liberty, GoreStats stats)
        {
            this.owner = owner; this.liberty = liberty; this.stats = stats;
        }

        internal void Forget(PedRef ped) { cooldownUntil.Remove(ped.Handle); }

        // victim and attacker are excluded; bystanders are snapshot peds within panicRadiusMeters of 'at'.
        internal void OnSevereViolence(PedRef victim, PedRef attacker, Vec3 at, CombatEffectsConfig config, long now, string why)
        {
            if (!config.PanicEnabled || config.PanicMaximumPeds <= 0) return;
            stats.PanicEvents++;
            PedRef from = attacker.IsNone ? victim : attacker;
            int eligible = 0, panicked = 0;
            liberty.Query.PedsInRadius(at, config.PanicRadiusMeters, ped => ped.Ped != victim && ped.Ped != attacker && !ped.IsPlayer && !ped.IsDead && !ped.InVehicle && ped.Health > 0, nearby);
            foreach (PedState ped in nearby)
            {
                long until;
                if (cooldownUntil.TryGetValue(ped.Ped.Handle, out until) && until > now) continue;
                if (liberty.Weapons.Current(ped.Ped) > 3) continue; // armed (ids 1-3 are melee): fights or flees on its own
                eligible++;
                if (panicked >= config.PanicMaximumPeds) continue;
                cooldownUntil[ped.Ped.Handle] = now + config.PanicCooldownMilliseconds;
                liberty.Tasks.Flee(ped.Ped, from, config.PanicFleeDistanceMeters);
                if (config.PanicSpeechContexts.Length > 0 && random.NextDouble() < config.PanicScreamChance)
                    liberty.Audio.Say(ped.Ped, config.PanicSpeechContexts[random.Next(config.PanicSpeechContexts.Length)]);
                Check check = new Check();
                check.Ped = ped.Ped; check.Start = ped.Position; check.Due = now + config.PanicVerifyMilliseconds;
                checks.Add(check);
                panicked++;
            }
            stats.PanicEligible += eligible;
            stats.Panicked += panicked;
            if (eligible > 0)
                liberty.Log.Info(owner, "gore_panic why=" + why + " eligible=" + eligible + " panicked=" + panicked + " radius=" + config.PanicRadiusMeters.ToString("0"));
        }

        internal void Update(CombatEffectsConfig config, long now)
        {
            for (int i = checks.Count - 1; i >= 0; i--)
            {
                Check check = checks[i];
                if (now < check.Due) continue;
                checks.RemoveAt(i);
                stats.PanicChecked++;
                bool gone = !liberty.Peds.Exists(check.Ped);
                bool moved = !gone && (liberty.Peds.IsDead(check.Ped) || liberty.Peds.GetPosition(check.Ped).DistanceTo(check.Start) >= config.PanicMovedMeters);
                if (gone || moved) stats.PanicVerified++;
                else liberty.Log.Info(owner, "gore_panic_not_moving handle=" + check.Ped.Handle);
            }
            if (cooldownUntil.Count > 256)
            {
                List<int> old = new List<int>();
                foreach (KeyValuePair<int, long> pair in cooldownUntil) if (pair.Value <= now) old.Add(pair.Key);
                foreach (int handle in old) cooldownUntil.Remove(handle);
            }
        }

        internal void Clear() { checks.Clear(); cooldownUntil.Clear(); }
    }
}
