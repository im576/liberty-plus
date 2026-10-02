using System;
using System.Collections.Generic;
using System.IO;
using Liberty.Sdk;
using Liberty.Sdk.Events;
using LibertyFramework.CombatEffects.Logic;
using LibertyFramework.Core.Config;
using LibertyFramework.Core.Logging;

namespace LibertyFramework.CombatEffects
{
    // Contextual weapon effects (T-048), driven by the BulletFired event for every shooter near the player: a per-class muzzle
    // layer (glow/flash effects at the bullet's origin), smoke after sustained fire, extra impact effects by what the bullet
    // hit (vehicle sparks; world and objects stay configurable but empty until T-050 names the material), and a short warm
    // light at night. Everything comes from config/weapon_effects.json; every count is capped (per frame, per light, and
    // against the shared active-effect budget with the gore module) and shrinks under performance pressure.
    // Owned by CombatEffectsController; it is not a module of its own so "gore + effects" is one measured section.
    internal sealed class WeaponEffects
    {
        private readonly ILiberty liberty;
        private readonly LibertyModule owner;
        private readonly EffectGenerationGate generation;
        private readonly ShotHeat heat = new ShotHeat();
        private readonly LightPulses lights = new LightPulses();
        private readonly List<KeyValuePair<LightPulses.Pulse, float>> lit = new List<KeyValuePair<LightPulses.Pulse, float>>();
        private readonly Dictionary<string, int> counters = new Dictionary<string, int>();
        private readonly Dictionary<string, int> refusedLogged = new Dictionary<string, int>();
        private readonly Queue<long> recentEffects = new Queue<long>();
        private readonly Dictionary<int, int> shooterFrame = new Dictionary<int, int>();
        private readonly List<KeyValuePair<FxRef, long>> loops = new List<KeyValuePair<FxRef, long>>();
        private WeaponEffectsConfig config;
        private Dictionary<int, WeaponEffectSet> sets = new Dictionary<int, WeaponEffectSet>();
        private DateTime lastConfigCheckUtc;
        private string configHash;
        private int frame = -1;
        private int shotsThisFrame;
        private int impactTestsThisFrame;
        private long nextPrune;
        private int activeCap = 24;
        private int floorCap = 6;
        private float pressure;

        internal WeaponEffects(LibertyModule owner, ILiberty liberty, EffectGenerationGate generation)
        {
            this.owner = owner; this.liberty = liberty; this.generation = generation;
            LoadConfig();
        }

        internal bool Enabled { get { return config != null && config.Enabled; } }

        private void Count(string name) { int value; counters.TryGetValue(name, out value); counters[name] = value + 1; }

        private void LoadConfig()
        {
            if ((DateTime.UtcNow - lastConfigCheckUtc).TotalMilliseconds < 1000) return;
            lastConfigCheckUtc = DateTime.UtcNow;
            try
            {
                string path = Path.Combine(LibertyPaths.ConfigDirectory, "weapon_effects.json");
                if (!File.Exists(path)) { if (configHash == null) { configHash = "missing"; RuntimeLog.Error("weapon_effects_config_missing path=" + path + " (weapon effects off)"); } return; }
                byte[] bytes = JsonStore.ReadBytes(path);
                string hash = JsonStore.Hash(bytes);
                if (hash == configHash) return;
                WeaponEffectsConfig candidate = JsonStore.Parse<WeaponEffectsConfig>(bytes);
                candidate.Validate();
                config = candidate;
                sets = WeaponEffectPlan.Build(candidate);
                configHash = hash;
                Clear();
                RuntimeLog.Info("weapon_effects_config_loaded enabled=" + config.Enabled + " classes=" + config.Classes.Length + " weapons=" + sets.Count +
                    " light=" + config.Light.Enabled + " night=" + config.Night.StartHour + "-" + config.Night.EndHour);
            }
            catch (Exception error) { RuntimeLog.Error("weapon_effects_config_rejected error=" + error); }
        }

        internal void OnBullet(BulletFired e, long now)
        {
            if (config == null || !generation.Allows(config.Enabled)) return;
            int currentFrame = liberty.World.Frame;
            if (currentFrame != frame) { frame = currentFrame; shotsThisFrame = 0; impactTestsThisFrame = 0; }
            WeaponEffectSet set;
            if (!sets.TryGetValue(e.Weapon, out set)) { Count("unmapped"); return; }
            Vec3 player = liberty.World.Player.Position;
            if (player.DistanceTo(e.From) > config.MaximumDistanceMeters) { Count("skipped_distance"); return; }
            if (++shotsThisFrame > config.MaximumShotsPerFrame) { Count("skipped_frame_cap"); return; }
            string name = set.Class.Name;
            Count("shots_" + name);
            Count("shots_weapon_" + e.Weapon);
            Vec3 direction = (e.To - e.From).Normalized;
            Vec3 rotation = Rotation(direction);
            // A shotgun fires many pellets in one frame: one muzzle flash, smoke puff and light per shooter per frame (impacts stay per bullet).
            int lastFrame;
            bool firstOfFrame = !(shooterFrame.TryGetValue(e.Shooter.Handle, out lastFrame) && lastFrame == currentFrame);
            shooterFrame[e.Shooter.Handle] = currentFrame;
            if (shooterFrame.Count > 256) shooterFrame.Clear();
            bool budget = EffectBudget.Available("weapon", activeCap, floorCap, pressure) > 0;

            if (budget)
            {
                if (firstOfFrame)
                    foreach (string effect in set.Class.MuzzleEffects)
                        if (Play(effect, e.From, rotation, set.MuzzleScale, now, config.Light.DurationMilliseconds)) Count("muzzle_" + name);
                if (firstOfFrame && heat.RecordShot(e.Shooter.Handle, now, set.Class.SmokeAfterShots, set.Class.SmokeWindowMilliseconds, set.Class.SmokeCooldownMilliseconds))
                    foreach (string effect in set.Class.SmokeEffects)
                        if (Play(effect, e.From + direction * config.SmokeOffsetMeters, rotation, set.Class.SmokeScale, now, config.SmokeDurationMilliseconds)) Count("smoke_" + name);
                Impact(e, set, direction, now);
            }
            else Count("skipped_budget");

            if (config.Light.Enabled && set.LightScale > 0 && WeaponEffectPlan.IsNight(liberty.World.Info.Hours, config.Night.StartHour, config.Night.EndHour) &&
                player.DistanceTo(e.From) <= config.Light.MaximumDistanceMeters && firstOfFrame && HasRoom())
            {
                if (lights.Add(e.From.X, e.From.Y, e.From.Z, set.LightScale, now, config.Light.DurationMilliseconds, config.Light.Maximum, name))
                    Count("light_requests_" + name);
                ReportBudget();
            }
        }

        // Extra impact effects by entity kind (one raycast; the game's own material impact is drawn by the game).
        private void Impact(BulletFired e, WeaponEffectSet set, Vec3 direction, long now)
        {
            if (config.MaximumImpactTestsPerFrame <= 0) return;
            if (set.Class.ImpactVehicle.Length == 0 && set.Class.ImpactWorld.Length == 0 && set.Class.ImpactObject.Length == 0) return;
            if (liberty.World.Player.Position.DistanceTo(e.To) > config.ImpactMaximumDistanceMeters) return;
            if (impactTestsThisFrame >= config.MaximumImpactTestsPerFrame) { Count("skipped_impact_cap"); return; }
            impactTestsThisFrame++;
            RayHit hit = liberty.Query.Raycast(e.From, e.To + direction * config.ImpactRayExtensionMeters, RayMask.All, RayIgnore.Of(e.Shooter));
            if (!hit.IsHit) { Count("impact_miss_" + set.Class.Name); return; }
            string[] effects = WeaponEffectPlan.ImpactEffects(set.Class, (int)hit.Kind);
            if (effects == null || effects.Length == 0) { Count("impact_" + hit.Kind.ToString().ToLowerInvariant() + "_none_" + set.Class.Name); return; }
            Vec3 rotation = Rotation(hit.Normal);
            foreach (string effect in effects)
                if (Play(effect, hit.Position, rotation, set.Class.ImpactScale, now, config.EffectLifetimeMilliseconds)) Count("impact_" + hit.Kind.ToString().ToLowerInvariant() + "_" + set.Class.Name);
        }

        // Effects are authored along the local Y axis: pitch about X, heading about Z (degrees). The screenshot review of the
        // stage1-effects scenario is what confirms the orientation; both signs are in one place.
        private static Vec3 Rotation(Vec3 direction)
        {
            float pitch = (float)(Math.Asin(Math.Max(-1f, Math.Min(1f, direction.Z))) * 180.0 / Math.PI);
            return new Vec3(pitch, 0f, direction.ToHeading());
        }

        // Room under the shared cap, and under the weapon effects' own share of it (the rest stays free for gore).
        private bool HasRoom()
        {
            int own = recentEffects.Count + loops.Count + lights.Count;
            return own < EffectBudget.Available("weapon", activeCap, floorCap, pressure) && own < Math.Max(1, (int)(EffectBudget.Cap(activeCap, floorCap, pressure) * config.MaximumEffectShare));
        }
        private void ReportBudget() { EffectBudget.Report("weapon", recentEffects.Count + loops.Count + lights.Count); }

        private bool Play(string effect, Vec3 position, Vec3 rotation, float scale, long now, int duration)
        {
            if (!HasRoom()) { Count("skipped_budget"); return false; }
            bool ok;
            try
            {
                ok = liberty.Fx.Burst(effect, position, rotation, scale);
                if (ok) recentEffects.Enqueue(now);
                else
                {
                    FxRef fx = liberty.Fx.Start(owner, effect, position, rotation, scale);
                    ok = !fx.IsNone;
                    if (ok) loops.Add(new KeyValuePair<FxRef, long>(fx, now + duration));
                }
                ReportBudget();
            }
            catch (Exception error) { RuntimeLog.Error("weapon_effect_failed effect=" + effect + " error=" + error.Message); return false; }
            if (!ok)
            {
                int logged; refusedLogged.TryGetValue(effect, out logged);
                if (logged < 1) { refusedLogged[effect] = logged + 1; RuntimeLog.Info("weapon_effect_refused effect=" + effect + " (looping or unknown; fix weapon_effects.json)"); }
                Count("refused");
            }
            return ok;
        }

        // Every tick: light the muzzle pulses (the light native draws for one frame per call), report the one-shot effects that
        // are still alive to the shared budget, prune idle shooters, pick up config changes.
        internal void Update(long now, int activeCap, int floorCap, float pressure)
        {
            this.activeCap = activeCap; this.floorCap = floorCap; this.pressure = pressure;
            LoadConfig();
            if (config == null || !config.Enabled) { Clear(); return; }
            while (recentEffects.Count > 0 && now - recentEffects.Peek() > config.EffectLifetimeMilliseconds) recentEffects.Dequeue();
            for (int i = loops.Count - 1; i >= 0; i--)
                if (now >= loops[i].Value) StopLoop(i);
            lights.Active(now, lit);
            // Running loops can be stopped as pressure rises. One-shot leases expire naturally; refuse new admissions meanwhile.
            int allowed = Math.Max(0, EffectBudget.Available("weapon", activeCap, floorCap, pressure));
            while (loops.Count > 0 && recentEffects.Count + loops.Count + lights.Count > allowed)
            {
                if (!StopLoop(0)) break;
                Count("pressure_stopped");
            }
            if (recentEffects.Count + loops.Count + lights.Count > allowed) { lights.Clear(); lit.Clear(); }
            ReportBudget();
            if (now >= nextPrune) { nextPrune = now + 10000; heat.Prune(now, 20000); }
            if (lights.Count == 0) return;
            float shrink = 1.0f - Math.Max(0f, Math.Min(1f, pressure)) * 0.5f;
            foreach (KeyValuePair<LightPulses.Pulse, float> pair in lit)
            {
                CombatEffectsNatives.DrawLight(pair.Key.X, pair.Key.Y, pair.Key.Z, config.Light.Red, config.Light.Green, config.Light.Blue,
                    config.Light.Range * pair.Key.Scale * shrink, config.Light.Intensity * pair.Key.Scale * pair.Value);
                if (!pair.Key.DrawReported) { pair.Key.DrawReported = true; Count("lights_" + pair.Key.SourceClass); }
            }
            Count("light_frames");
        }

        internal string Report()
        {
            List<string> keys = new List<string>(counters.Keys);
            keys.Sort(StringComparer.Ordinal);
            List<string> parts = new List<string>();
            foreach (string key in keys) parts.Add(key + "=" + counters[key]);
            return "weapon_effects_stats " + (parts.Count == 0 ? "none" : string.Join(" ", parts.ToArray())) + " lights=" + lights.Count + " replaced=" + lights.Replaced +
                " heat_shooters=" + heat.Tracked + " owned_loops=" + loops.Count;
        }

        internal void ResetStats() { counters.Clear(); }

        private bool StopLoop(int index)
        {
            try { liberty.Fx.Stop(loops[index].Key); loops.RemoveAt(index); return true; }
            catch (Exception error) { RuntimeLog.Error("weapon_effect_stop_failed handle=" + loops[index].Key.Handle + " error=" + error.Message); return false; }
        }

        internal void Clear()
        {
            for (int i = loops.Count - 1; i >= 0; i--) StopLoop(i);
            heat.Clear(); lights.Clear(); lit.Clear(); recentEffects.Clear(); ReportBudget();
        }
    }
}
