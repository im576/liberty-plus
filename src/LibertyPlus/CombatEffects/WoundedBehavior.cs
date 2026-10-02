using System;
using System.Collections;
using System.Collections.Generic;
using Liberty.Sdk;
using LibertyFramework.CombatEffects.Logic;

namespace LibertyFramework.CombatEffects
{
    // A survivable, severe hit can leave the victim suffering (T-047): the game's own ragdoll drops them, they cry out
    // (ambient speech from config), then the game's cower task keeps them down for woundedCowerMilliseconds and the task is
    // cleared so the ped's normal behaviour resumes. It is a short sequence of game tasks, not a scripted animation loop.
    // A configurable share of severe hits (woundedChance) starts it; at most woundedMaximumPeds at a time.
    internal sealed class WoundedBehavior
    {
        private readonly ILiberty liberty;
        private readonly LibertyModule owner;
        private readonly GoreStats stats;
        private readonly Random random = new Random(4747);
        private readonly Dictionary<int, long> until = new Dictionary<int, long>();

        internal WoundedBehavior(LibertyModule owner, ILiberty liberty, GoreStats stats)
        {
            this.owner = owner; this.liberty = liberty; this.stats = stats;
        }

        internal bool IsWounded(PedRef ped) { return until.ContainsKey(ped.Handle); }

        internal void Forget(PedRef ped) { until.Remove(ped.Handle); }

        internal bool TryStart(PedRef ped, CombatEffectsConfig config, long now)
        {
            if (config.WoundedMaximumPeds <= 0 || config.WoundedChance <= 0 || until.ContainsKey(ped.Handle)) return false;
            List<int> finished = null;
            foreach (KeyValuePair<int, long> pair in until) if (pair.Value <= now) (finished = finished ?? new List<int>()).Add(pair.Key);
            if (finished != null) foreach (int handle in finished) until.Remove(handle);
            if (until.Count >= config.WoundedMaximumPeds || random.NextDouble() >= config.WoundedChance) return false;
            long total = config.WoundedRagdollMilliseconds + config.WoundedCowerMilliseconds;
            until[ped.Handle] = now + total;
            stats.Wounded++;
            liberty.Scheduler.Start(owner, "wounded-" + ped.Handle, Suffer(ped, config.WoundedRagdollMilliseconds, config.WoundedCowerMilliseconds, Pick(config.WoundedSpeechContexts)));
            liberty.Log.Info(owner, "gore_wounded handle=" + ped.Handle + " ragdoll_ms=" + config.WoundedRagdollMilliseconds + " cower_ms=" + config.WoundedCowerMilliseconds);
            return true;
        }

        private string Pick(string[] contexts) { return contexts == null || contexts.Length == 0 ? null : contexts[random.Next(contexts.Length)]; }

        private IEnumerator Suffer(PedRef ped, int ragdollMilliseconds, int cowerMilliseconds, string speech)
        {
            if (ragdollMilliseconds > 0) { liberty.Peds.Ragdoll(ped, ragdollMilliseconds); }
            if (!string.IsNullOrEmpty(speech)) { liberty.Audio.Say(ped, speech); }
            yield return Wait.Milliseconds(ragdollMilliseconds + 300);
            if (!liberty.Peds.Exists(ped) || liberty.Peds.IsDead(ped)) { until.Remove(ped.Handle); yield break; }
            liberty.Tasks.Cower(ped);
            yield return Wait.Milliseconds(cowerMilliseconds);
            if (liberty.Peds.Exists(ped) && !liberty.Peds.IsDead(ped)) { liberty.Tasks.Clear(ped); }
            until.Remove(ped.Handle);
        }
    }
}
