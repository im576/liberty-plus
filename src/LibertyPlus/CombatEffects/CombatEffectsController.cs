using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using GTA;
using GTA.Native;
using LibertyFramework.CombatEffects.Logic;
using LibertyFramework.Core.Config;
using LibertyFramework.Core.Logging;
using LibertyFramework.DevTools;
using LibertyFramework.DevTools.Menu;
using LibertyFramework.GameApi;
using SdkPedDamaged = Liberty.Sdk.Events.PedDamaged;
using SdkPedDied = Liberty.Sdk.Events.PedDied;
using SdkPedRemoved = Liberty.Sdk.Events.PedRemoved;

namespace LibertyFramework.CombatEffects
{
    // Gore, blood and dismemberment (T-022, reworked for Stage 1 in T-047). Everything is driven by the exact PedDamaged and
    // PedDied events (ADR-0007): who shot, with which weapon, at which bone, from how far, for how much, and whether it
    // killed. No proximity scan, no health polling; NPC-on-NPC violence gets the same gore as the player's. Each firearm hit
    // produces a class- and distance-sensitive entry/exit spray, chunks on heavy hits, a bleeding wound and a region
    // reaction; head hits and close shotgun-type hits are "severe" (extra effect sets, possible suffering, panic nearby); a
    // killing hit to a limb severs it at the joint and a killing head hit decapitates. Bodies stay 3-5 minutes with hard
    // caps (BodyPersistence). The optional external blood mode leaves ordinary hit/wound visuals to a separate renderer while
    // preserving the cut and its stock stump particle effects.
    [global::Liberty.Sdk.Module("combat", Order = 20, Capabilities = new[] { global::Liberty.Sdk.Capabilities.EngineInternal }, Description = "Combat effects: hit reactions, blood, dismemberment")]
    public sealed class CombatEffectsController : LibertyFramework.Engine.Module
    {
        private sealed class PendingCut
        {
            internal Ped Ped;
            internal LimbCutPlan Plan;
            internal Vector3 Push;
            internal long Deadline;
            internal long DeathSeenAt; // 0 until the ped is first seen dead
            internal float Scale;
        }

        private readonly Dictionary<int, PedInjuryState> states = new Dictionary<int, PedInjuryState>();
        private readonly GoreStats stats = new GoreStats();
        private readonly BloodEffects blood = new BloodEffects();
        private readonly List<PendingCut> pending = new List<PendingCut>();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly NullBloodSurface bloodSurface = new NullBloodSurface();
        private CombatEffectsConfig config;
        private DateTime lastConfigCheckUtc;
        private string configHash;
        private bool disabled;
        private Dismemberment dismember;
        private bool engineChecked;
        private SkeletonCollapseEngine collapseEngine;
        private BodyPersistence bodies;
        private WoundedBehavior wounded;
        private PanicReaction panic;
        private bool bodiesPaused; // `gore bodies off`: a test switch, bodies die and are cleaned up by the game as in vanilla
        private int hitFrame = -1;
        private int hitsThisFrame;
        private long logWindowStart;
        private int logsInWindow;
        private int refusedReported;
        private volatile int goreTestRequest; // 1 gallery, 2-4 cuts, 5 leak (set from the DevTools thread)
        private volatile string goreTestStatus;
        private long goreTestShownUntil;
        private Ped galleryPed;
        private List<string> galleryEffects;
        private int galleryIndex;
        private long galleryNext;
        private readonly GTA.Font statusFont;

        public CombatEffectsController()
        {
            Interval = 0;
            LoadConfig();
            statusFont = new GTA.Font(18.0F, FontScaling.Pixel, true, false);
            statusFont.Color = Color.FromArgb(255, 235, 90, 80);
            DevToolsPages.Register("Gore Test", GoreTestItems);
            PerFrameDrawing += OnDraw;
            Tick += OnTick;
            AppDomain.CurrentDomain.DomainUnload += OnUnload;
            AppDomain.CurrentDomain.ProcessExit += OnUnload;
        }

        // Console / autopilot: the DevTools gore tests on the nearest NPC, and the acceptance counters.
        protected internal override void OnStart()
        {
            bodies = new BodyPersistence(stats);
            wounded = new WoundedBehavior(this, Liberty, stats);
            panic = new PanicReaction(this, Liberty, stats);
            Liberty.Events.Subscribe<SdkPedDamaged>(this, OnPedDamaged);
            Liberty.Events.Subscribe<SdkPedDied>(this, OnPedDied);
            Liberty.Events.Subscribe<SdkPedRemoved>(this, OnPedRemoved);
            Engine.Commands.Register(this, "gore", "gore gallery|arm|leg|head|leak - DevTools gore test on the nearest NPC; gore stats|reset - acceptance counters", args =>
            {
                string sub = args.Length > 0 ? args[0] : "";
                if (sub == "stats") { string line = stats.Report() + " bodies=" + (bodies == null ? 0 : bodies.Count) + " effects=" + EffectBudget.Total; RuntimeLog.Info(line); return line; }
                if (sub == "reset") { ResetStats(); return "gore stats reset"; }
                if (sub == "bodies") { bodiesPaused = args.Length > 1 && args[1] == "off"; return "body persistence " + (bodiesPaused ? "paused" : "active"); }
                string[] names = { "", "gallery", "arm", "leg", "head", "leak" };
                int request = Array.IndexOf(names, sub);
                if (request <= 0) { return "gore gallery|arm|leg|head|leak|stats|reset"; }
                RequestGoreTest(request);
                return "gore " + args[0] + " requested on the nearest NPC";
            });
        }

        // Counters restart from zero; kept bodies and effects stay (they are not counters).
        private void ResetStats()
        {
            int kept = stats.BodiesKept, released = stats.BodiesReleased;
            stats.Hits = 0; stats.InferredHits = 0; stats.SkippedDistance = 0; stats.SkippedBudget = 0; stats.HeadHits = 0; stats.HeadSevere = 0;
            stats.TraumaHits = 0; stats.TraumaSevere = 0; stats.Kills = 0; stats.Wounded = 0; stats.PanicEvents = 0; stats.PanicEligible = 0;
            stats.Panicked = 0; stats.PanicVerified = 0; stats.PanicChecked = 0; stats.Cuts = 0; stats.LimbsFloating = 0; stats.LimbsFlashing = 0;
            stats.LatencySumFrames = 0; stats.LatencyMaxFrames = 0;
            stats.BodiesKept = kept; stats.BodiesReleased = released;
        }

        private void LoadConfig()
        {
            if ((DateTime.UtcNow - lastConfigCheckUtc).TotalMilliseconds < 1000) return;
            lastConfigCheckUtc = DateTime.UtcNow;
            try
            {
                string path = Path.Combine(LibertyPaths.ConfigDirectory, "combat_effects.json");
                byte[] bytes = JsonStore.ReadBytes(path);
                string hash = JsonStore.Hash(bytes);
                if (hash == configHash) return;
                CombatEffectsConfig candidate = JsonStore.Parse<CombatEffectsConfig>(bytes);
                candidate.Validate();
                config = candidate;
                configHash = hash;
                states.Clear();
                blood.StopAll();
                RuntimeLog.Info("combat_effects_config_loaded enabled=" + config.Enabled + " all_firearms=" + config.AllFirearms +
                    " dismemberment=" + config.DismembermentEnabled + " decapitation=" + config.DecapitationEnabled + " scale=" + config.EffectScale +
                    " blood_visual_mode=" + (config.StockBloodVisuals ? "stock" : "external") + " classes=" + config.WeaponClasses.Length +
                    " bodies=" + config.MaximumBodies + "x" + (config.BodyLifetimeMinimumMilliseconds / 1000) + "-" + (config.BodyLifetimeMaximumMilliseconds / 1000) + "s" +
                    " max_effects=" + config.MaximumActiveEffects + " panic=" + config.PanicEnabled);
            }
            catch (Exception error) { RuntimeLog.Error("combat_effects_config_rejected error=" + error); }
        }

        // T-026: every tick's wall-clock cost goes to the shared CostMeter report.
        private void OnTick(object sender, EventArgs args)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            try { TickBody(sender, args); }
            finally { LibertyFramework.Core.Performance.Logic.CostMeter.Add("tick.combat", started); }
        }

        private void TickBody(object sender, EventArgs args)
        {
            if (disabled) return;
            try
            {
                LoadConfig();
                if (config == null || !config.Enabled) { ClearAll(); return; }
                long now = clock.ElapsedMilliseconds;
                Player player = Player;
                Ped shooter = player == null ? null : player.Character;
                if (shooter == null || !Natives.PedExists(shooter)) return;
                if (!engineChecked) { EnsureEngine(shooter); }
                if (dismember != null)
                {
                    // A failed limb throw must never take the stump (and the corpse's missing limb) down with it.
                    try
                    {
                        Func<object, bool> throwLimb = config.SeveredLimbEnabled ?
                            new Func<object, bool>(record => ThrowLimbSafely(record, now)) : null;
                        long dismemberStart = System.Diagnostics.Stopwatch.GetTimestamp();
                        dismember.Update(config, now, throwLimb, (limb, bone) => OnLimbLanded(limb, bone, now));
                        LibertyFramework.Core.Performance.Logic.CostMeter.Add("combat.dismember", dismemberStart);
                    }
                    catch (Exception error) { DisableDismemberment(error); }
                }
                long sectionStart = System.Diagnostics.Stopwatch.GetTimestamp();
                ResolvePending(now);
                LibertyFramework.Core.Performance.Logic.CostMeter.Add("combat.pending", sectionStart);
                sectionStart = System.Diagnostics.Stopwatch.GetTimestamp();
                float pressure = Liberty.Perf.Pressure;
                blood.Pressure = pressure;
                blood.Update(config, now);
                if (blood.Refused != refusedReported) { stats.SkippedBudget += blood.Refused - refusedReported; refusedReported = blood.Refused; }
                LibertyFramework.Core.Performance.Logic.CostMeter.Add("combat.blood", sectionStart);
                sectionStart = System.Diagnostics.Stopwatch.GetTimestamp();
                bodies.Sweep(config, now, pressure, Liberty.World.Player.Position, Liberty.Query, ped => dismember != null && dismember.IsTracked(ped));
                panic.Update(config, now);
                LibertyFramework.Core.Performance.Logic.CostMeter.Add("combat.aftermath", sectionStart);
                RunGoreTest(shooter, now);
            }
            catch (Exception error) { DisableAfterFailure(error); }
        }

        private void DisableAfterFailure(Exception error)
        {
            RuntimeLog.Error("feature_disabled combat_effects error=" + error);
            try { ClearAll(); } catch (Exception cleanupError) { RuntimeLog.Error("combat_effects_cleanup_failed error=" + cleanupError); }
            RemoveHooks();
            disabled = true;
        }

        // ---- exact events (engine thread, in the frame the game's damage routine was observed) ----

        private void OnPedDamaged(SdkPedDamaged e)
        {
            if (disabled || config == null || !config.Enabled) return;
            try { HandleDamage(e); }
            catch (Exception error) { DisableAfterFailure(error); }
        }

        private void OnPedDied(SdkPedDied e)
        {
            if (disabled || config == null || !config.Enabled) return;
            try { HandleDeath(e); }
            catch (Exception error) { DisableAfterFailure(error); }
        }

        private void OnPedRemoved(SdkPedRemoved e)
        {
            states.Remove(e.Ped.Handle);
            if (wounded != null) wounded.Forget(e.Ped);
            if (panic != null) panic.Forget(e.Ped);
        }

        private bool AllowHitLog(long now)
        {
            if (now - logWindowStart >= 1000) { logWindowStart = now; logsInWindow = 0; }
            return logsInWindow++ < config.MaximumHitLogsPerSecond;
        }

        private PedInjuryState StateFor(int handle, long now)
        {
            PedInjuryState state;
            if (states.TryGetValue(handle, out state)) return state;
            if (states.Count >= config.MaximumTrackedPeds)
            {
                int oldest = 0; long oldestAt = long.MaxValue;
                foreach (KeyValuePair<int, PedInjuryState> pair in states)
                    if (pair.Value.LastAttributedHitMilliseconds < oldestAt) { oldestAt = pair.Value.LastAttributedHitMilliseconds; oldest = pair.Key; }
                states.Remove(oldest);
            }
            state = new PedInjuryState();
            states.Add(handle, state);
            return state;
        }

        private void HandleDamage(SdkPedDamaged e)
        {
            if (e.Type != global::Liberty.Sdk.DamageType.Bullet) return;
            if (e.Ped == Liberty.Player.Ped) return;
            // Without the damage hook the engine infers events from health changes and knows only the player's shots.
            if (!e.Exact && !e.ByPlayer) return;
            if (!config.AllFirearms && Array.IndexOf(config.AllowedWeaponIds, e.Weapon) < 0) return;
            Ped target = LibertyFramework.Engine.Handles.Ped(e.Ped);
            if (target == null || !Natives.PedExists(target)) return;
            int handle = e.Ped.Handle;
            if (dismember != null && dismember.IsTracked(target) && !states.ContainsKey(handle)) return; // our own limb clones
            if (!config.IncludeMissionPeds && CombatEffectsNatives.IsMissionPed(target)) return;
            global::Liberty.Sdk.PedState victimState;
            bool inSnapshot = Liberty.World.TryGetPed(e.Ped, out victimState);
            if (inSnapshot && victimState.InVehicle) return;

            int damage = (int)Math.Round(e.HealthLost);
            if (damage <= 0) damage = Math.Max(0, e.HealthBefore - e.HealthAfter);
            if (damage <= 0 && !e.Killed) return; // armour took it

            long now = clock.ElapsedMilliseconds;
            int frame = Liberty.World.Frame;
            global::Liberty.Sdk.Vec3 victimPosition = inSnapshot ? victimState.Position : Liberty.Peds.GetPosition(e.Ped);
            global::Liberty.Sdk.Vec3 attackerPosition = e.Attacker.IsNone ? victimPosition : Liberty.Peds.GetPosition(e.Attacker);
            float distance = attackerPosition.DistanceTo(victimPosition);
            float playerDistance = Liberty.World.Player.Position.DistanceTo(victimPosition);

            stats.Hits++;
            if (!e.Exact) stats.InferredHits++;
            if (frame != hitFrame) { hitFrame = frame; hitsThisFrame = 0; }
            bool visible = playerDistance <= config.EffectMaximumDistanceMeters;
            bool effects = visible && ++hitsThisFrame <= config.MaximumEffectHitsPerFrame;
            if (!visible) stats.SkippedDistance++;
            else if (!effects) stats.SkippedBudget++;

            PedInjuryState state = StateFor(handle, now);
            int bone = (int)e.Bone;
            HitRegion region = HitClassifier.Classify(bone);
            if (region == HitRegion.Unknown) { bone = 0x36A0; region = HitRegion.Torso; } // unknown bone: bleed from the chest
            state.LastBone = bone;
            state.LastAttributedHitMilliseconds = now;
            bool dead = e.Killed;
            GoreWeaponClass weaponClass = GoreClassifier.Find(config.WeaponClasses, e.Weapon);
            GoreClassifier.Severity severity = GoreClassifier.Classify(weaponClass, region, distance);
            float classScale = GoreClassifier.DistanceScale(weaponClass, distance);
            // Shots into a body that is already dead still spray, but the acceptance counters only judge hits on the living.
            bool countable = !state.DeathBurst && !(inSnapshot && victimState.IsDead && !e.Killed);
            if (countable && severity == GoreClassifier.Severity.Head) stats.HeadHits++;
            else if (countable && severity == GoreClassifier.Severity.Trauma) stats.TraumaHits++;

            // Downed peds bleed out 1-3 health at a time: drip only (no spray, reaction or log line per tick).
            if (damage < config.MinimumEffectDamage && severity == GoreClassifier.Severity.None)
            {
                if (effects && config.StockBloodVisuals && state.Bleeds == 0)
                    Play(config.BleedEffectName, target, bone, config.EffectScale, now, config.BleedIntervalMilliseconds * 4, 0);
                if (dead && effects) DeathBurst(target, state, bone, config.EffectScale, now);
                return;
            }
            float lowest = config.EffectScale * 0.8f * Math.Min(1.0f, classScale);
            float highest = config.MaximumHitScale > 0 ? config.MaximumHitScale : config.EffectScale * 2.2f;
            float scale = Clamp(config.EffectScale * damage / 40.0f * classScale, lowest, highest);

            bool severeSpawned = false;
            if (effects)
            {
                string entry = weaponClass != null && !string.IsNullOrEmpty(weaponClass.EntryEffect) ? weaponClass.EntryEffect : config.ImpactEffectName;
                if (config.StockBloodVisuals)
                {
                    Play(entry, target, bone, scale, now, 0, 0);
                    Play(config.MistEffectName, target, bone, scale, now, 0, 0);
                    if (damage >= config.ExitDamage) Play(config.ExitEffectName, target, bone, scale, now, 0, 0);
                    bool specialEntry = weaponClass != null && !string.IsNullOrEmpty(weaponClass.EntryEffect);
                    if (damage >= config.ChunkDamage || specialEntry)
                    {
                        string chunks = weaponClass != null && !string.IsNullOrEmpty(weaponClass.ChunksEffect) ? weaponClass.ChunksEffect : config.HeavyChunksEffectName;
                        Play(chunks, target, bone, scale, now, 0, 0);
                    }
                    if (config.WoundsEnabled && !state.EngineBleeding)
                    {
                        state.EngineBleeding = true;
                        try { CombatEffectsNatives.SetBleeding(target, true); }
                        catch (Exception error) { RuntimeLog.Error("set_char_bleeding_failed error=" + error.Message); }
                    }
                    if (config.WoundsEnabled && state.Bleeds < config.MaximumWoundsPerPed)
                    {
                        state.Bleeds++;
                        Play(config.BleedEffectName, target, bone, config.EffectScale, now, config.BleedDurationMilliseconds, config.BleedIntervalMilliseconds);
                        if (damage >= config.ExitDamage)
                            Play(config.WoundSpurtEffectName, target, bone, scale, now, config.WoundSpurtDurationMilliseconds, config.ArterialIntervalMilliseconds);
                    }
                    // Severe: firearm head hit inside the class's range, or any hit inside its trauma range.
                    if (severity == GoreClassifier.Severity.Head)
                        severeSpawned = PlaySet(config.HeadTraumaEffects, target, 0x4B5, config.EffectScale * config.HeadTraumaScale * Math.Max(0.6f, classScale), now);
                    else if (severity == GoreClassifier.Severity.Trauma)
                        severeSpawned = PlaySet(config.TraumaEffects, target, bone, config.EffectScale * config.TraumaScale * Math.Max(0.6f, classScale), now);
                }
                else if (config.WoundsEnabled)
                {
                    if (!state.EngineBleeding)
                    {
                        state.EngineBleeding = true;
                        try { CombatEffectsNatives.SetBleeding(target, true); }
                        catch (Exception error) { RuntimeLog.Error("set_char_bleeding_failed error=" + error.Message); }
                    }
                    if (damage >= config.ExternalBleedMinimumDamage && state.Bleeds < config.MaximumWoundsPerPed)
                    {
                        float bleedScale = scale * config.ExternalBleedScaleMultiplier;
                        int duration = dead ? config.ExternalFatalBleedDurationMilliseconds : config.ExternalBleedDurationMilliseconds;
                        if (blood.Leak(config, config.ExternalBleedEffectName, target, bone, bleedScale, now, duration,
                            config.ExternalBleedStartIntervalMilliseconds, config.ExternalBleedEndIntervalMilliseconds, false)) state.Bleeds++;
                    }
                }
                if (dead) DeathBurst(target, state, bone, scale, now);
            }

            if (config.InjuriesEnabled && damage >= config.MinimumInjuryDamage)
            {
                int hits;
                state.RegionHits.TryGetValue(region, out hits);
                state.RegionHits[region] = hits + 1;
            }
            // The shot's direction: from the attacker through the victim (the exact hit direction when the bullet was found).
            float dx = e.HasHit ? e.HitDirection.X : victimPosition.X - attackerPosition.X;
            float dy = e.HasHit ? e.HitDirection.Y : victimPosition.Y - attackerPosition.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            Vector3 push = length > 0.01f ? new Vector3(dx / length, dy / length, 0) : new Vector3(0, 0, 0);
            if (effects && config.ReactionsEnabled && !dead && now - state.LastReactionMilliseconds >= config.ReactionCooldownMilliseconds)
            {
                state.LastReactionMilliseconds = now;
                float force = ReactionForce(region);
                if (force > 0) CombatEffectsNatives.React(target, push.X * force, push.Y * force,
                    region == HitRegion.LeftLeg || region == HitRegion.RightLeg ? 0.0f : force * config.ReactionVerticalFraction);
            }

            // Severing waits briefly for death: peds are flagged dead a few frames after the killing hit.
            bool cutQueued = false;
            if (visible)
            {
                if (LimbCutPlan.IsHeadBone(bone))
                {
                    if (config.DecapitationEnabled && damage >= config.DecapitationMinimumDamage && !state.HeadRemoved)
                        cutQueued = Queue(target, LimbCutPlan.Head(), push, now, scale);
                    else if (effects && config.StockBloodVisuals) Play(config.MouthBloodEffectName, target, 0x4B5, scale, now, 0, 0);
                }
                else if (config.DismembermentEnabled && damage >= config.MinimumLimbLossDamage)
                {
                    LimbCutPlan plan = LimbCutPlan.ForHitBone(bone);
                    if (plan != null) cutQueued = Queue(target, plan, push, now, scale);
                }
            }
            if (severity == GoreClassifier.Severity.Trauma && cutQueued) severeSpawned = true;
            if (severity == GoreClassifier.Severity.Head && cutQueued) severeSpawned = true;
            if (countable && severity == GoreClassifier.Severity.Head && severeSpawned) stats.HeadSevere++;
            if (countable && severity == GoreClassifier.Severity.Trauma && severeSpawned) stats.TraumaSevere++;

            // Same frame by construction: the event is published in the frame that drained the hook's ring, and every effect
            // above was issued from this handler. The counters prove it stays true.
            int latency = Liberty.World.Frame - frame;
            stats.LatencySumFrames += latency;
            if (latency > stats.LatencyMaxFrames) stats.LatencyMaxFrames = latency;

            bool severe = severity != GoreClassifier.Severity.None || cutQueued;
            if (severe || dead || AllowHitLog(now))
                RuntimeLog.Info((severe ? "gore_severe" : "combat_hit") + " region=" + region + " bone=0x" + bone.ToString("X") + " damage=" + damage +
                    " weapon=" + e.Weapon + " class=" + (weaponClass == null ? "none" : weaponClass.Name) + " dist=" + distance.ToString("0.0") +
                    " attacker=" + e.Attacker.Handle + " exact=" + e.Exact + " frame=" + frame + " killed=" + dead +
                    (severity != GoreClassifier.Severity.None ? " kind=" + severity.ToString().ToLowerInvariant() + " spawned=" + severeSpawned : "") +
                    (cutQueued ? " cut=queued" : "") + (effects ? "" : " effects=skipped"));

            if (severe && !dead)
            {
                wounded.TryStart(e.Ped, config, now);
                panic.OnSevereViolence(e.Ped, e.Attacker, victimPosition, config, now, "severe");
            }
        }

        private void HandleDeath(SdkPedDied e)
        {
            if (e.Ped == Liberty.Player.Ped) return;
            long now = clock.ElapsedMilliseconds;
            stats.Kills++;
            global::Liberty.Sdk.Vec3 at = Liberty.Peds.GetPosition(e.Ped);
            float playerDistance = Liberty.World.Player.Position.DistanceTo(at);
            wounded.Forget(e.Ped);
            Ped ped = LibertyFramework.Engine.Handles.Ped(e.Ped);
            if (!bodiesPaused && ped != null && Natives.PedExists(ped)) bodies.Keep(ped, now, playerDistance, config, null);
            panic.OnSevereViolence(e.Ped, e.Killer, at, config, now, "kill");
        }

        // True when the cut was queued (or already pending on this ped).
        private bool Queue(Ped target, LimbCutPlan plan, Vector3 push, long now, float scale)
        {
            if (dismember != null && dismember.IsTracked(target, plan.Name)) return false;
            // Playtest: shotgun pellets and follow-up hits on the falling body queued a cut per limb, so one kill
            // blew off four limbs at once. A ped gets at most maximumCutsPerPed cuts (pending + done); later hits
            // on the same body only bleed.
            int cuts = dismember != null ? dismember.CutsOn(target) : 0;
            foreach (PendingCut cut in pending)
            {
                if (cut.Ped != target) continue;
                if (cut.Plan.Name == plan.Name) return true;
                cuts++;
            }
            if (config.MaximumCutsPerPed > 0 && cuts >= config.MaximumCutsPerPed) return false;
            PendingCut item = new PendingCut();
            item.Ped = target; item.Plan = plan; item.Push = push; item.Scale = scale;
            item.Deadline = now + config.PendingDeathWindowMilliseconds;
            pending.Add(item);
            return true;
        }

        private void ResolvePending(long now)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingCut cut = pending[i];
                if (cut.Ped == null || !Natives.PedExists(cut.Ped) || (cut.DeathSeenAt == 0 && now > cut.Deadline)) { pending.RemoveAt(i); continue; }
                if (cut.DeathSeenAt == 0)
                {
                    if (!Natives.PedDead(cut.Ped) && Natives.PedHealth(cut.Ped) > 0) continue;
                    cut.DeathSeenAt = now;
                }
                // Let the death ragdoll start from the intact pose before any bone is collapsed.
                if (now - cut.DeathSeenAt < config.SeverDelayMilliseconds) continue;
                pending.RemoveAt(i);
                Sever(cut, now);
            }
        }

        private void Sever(PendingCut cut, long now)
        {
            if (dismember != null && dismember.IsTracked(cut.Ped, cut.Plan.Name)) return;
            bool head = cut.Plan.Name == "head";
            PedInjuryState state;
            states.TryGetValue(cut.Ped.GetHashCode(), out state);
            bool collapsed = false;
            long collapseStart = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                collapsed = dismember != null && (dismember.IsTracked(cut.Ped) || dismember.SeveredCount < config.MaximumSeveredPeds) &&
                    dismember.Sever(cut.Ped, cut.Plan, cut.Push, now, config.SeveredCorpseLifetimeMilliseconds);
            }
            catch (Exception error) { RuntimeLog.Error("dismember_sever_failed part=" + cut.Plan.Name + " error=" + error.Message); }
            if (!collapsed && !head && dismember != null)
            {
                LimbCutPlan upper = LimbCutPlan.Upper(cut.Plan);
                if (upper != null)
                {
                    try
                    {
                        if (dismember.Sever(cut.Ped, upper, cut.Push, now, config.SeveredCorpseLifetimeMilliseconds))
                        {
                            RuntimeLog.Info("combat_sever_fallback from=" + cut.Plan.Name + " to=" + upper.Name);
                            cut.Plan = upper;
                            collapsed = true;
                        }
                    }
                    catch (Exception error) { RuntimeLog.Error("dismember_sever_failed part=" + upper.Name + " error=" + error.Message); }
                }
            }
            if (head && !collapsed) { CombatEffectsNatives.RemoveHead(cut.Ped); } // stock fallback
            if (head && state != null) state.HeadRemoved = true;
            if (!head && !collapsed) { RuntimeLog.Error("combat_sever_skipped part=" + cut.Plan.Name + " collapse_failed"); return; }
            LibertyFramework.Core.Performance.Logic.CostMeter.Add("combat.sever.collapse", collapseStart);
            long fxStart = System.Diagnostics.Stopwatch.GetTimestamp();
            if (collapsed) stats.Cuts++;
            if (config.StockBloodVisuals)
            {
                try { CombatEffectsNatives.SetBleeding(cut.Ped, true); } catch (Exception error) { RuntimeLog.Error("set_char_bleeding_failed error=" + error.Message); }
            }
            if (config.StockBloodVisuals)
            {
                float burst = Math.Max(cut.Scale, config.EffectScale) * 1.4f;
                Play(config.SeverBurstEffectName, cut.Ped, cut.Plan.StumpTag, burst, now, 0, 0);
                Play(config.SeverMistEffectName, cut.Ped, cut.Plan.StumpTag, burst, now, 0, 0);
                Play(config.HeavyChunksEffectName, cut.Ped, cut.Plan.StumpTag, burst, now, 0, 0);
                Play(config.ArterialEffectName, cut.Ped, cut.Plan.StumpTag, config.EffectScale * 1.2f, now, config.ArterialDurationMilliseconds, config.ArterialIntervalMilliseconds);
                Play(config.BleedEffectName, cut.Ped, cut.Plan.StumpTag, config.EffectScale * 1.3f, now, config.BleedDurationMilliseconds, config.BleedIntervalMilliseconds);
            }
            else
            {
                blood.RemoveForPed(cut.Ped); // a bone hidden by the cut must not keep emitting its earlier wound
                try { CombatEffectsNatives.SetBleeding(cut.Ped, true); } catch (Exception error) { RuntimeLog.Error("set_char_bleeding_failed error=" + error.Message); }
                Play(config.ExternalStumpBurstEffectName, cut.Ped, cut.Plan.StumpTag, config.ExternalStumpBleedScale, now, 0, 0);
                blood.Leak(config, config.ExternalBleedEffectName, cut.Ped, cut.Plan.StumpTag,
                    config.ExternalStumpBleedScale, now, config.ExternalStumpBleedDurationMilliseconds,
                    config.ExternalStumpBleedStartIntervalMilliseconds, config.ExternalStumpBleedEndIntervalMilliseconds, true);
            }
            LibertyFramework.Core.Performance.Logic.CostMeter.Add("combat.sever.fx", fxStart);
            RuntimeLog.Info("combat_sever part=" + cut.Plan.Name + " collapsed=" + collapsed);
        }

        private bool Play(string effect, Ped ped, int bone, float scale, long now, int durationMilliseconds, int intervalMilliseconds)
        {
            return blood.Play(config, effect, ped, bone, scale, now, durationMilliseconds, intervalMilliseconds);
        }

        // True when at least one effect of the set spawned.
        private bool PlaySet(string[] effects, Ped ped, int bone, float scale, long now)
        {
            bool any = false;
            foreach (string effect in effects) any |= Play(effect, ped, bone, scale, now, 0, 0);
            return any;
        }

        // The killing hit: a death burst, blood from the mouth, and the body keeps leaking where it lies.
        private void DeathBurst(Ped target, PedInjuryState state, int bone, float scale, long now)
        {
            if (state.DeathBurst) return;
            state.DeathBurst = true;
            if (!config.StockBloodVisuals)
            {
                if (config.WoundsEnabled)
                    blood.Leak(config, config.ExternalBleedEffectName, target, bone, scale * config.ExternalBleedScaleMultiplier,
                        now, config.ExternalFatalBleedDurationMilliseconds, config.ExternalBleedStartIntervalMilliseconds,
                        config.ExternalBleedEndIntervalMilliseconds, false);
                return;
            }
            Play(config.DeathEffectName, target, 0x36A0, scale * 1.2f, now, 0, 0);
            Play(config.MouthBloodEffectName, target, 0x4B5, scale, now, 0, 0);
            Play(config.DeathLeakEffectName, target, 0x36A0, config.EffectScale, now, config.DeathLeakDurationMilliseconds, config.BleedIntervalMilliseconds);
        }

        private bool ThrowLimbSafely(object record, long now)
        {
            try { return dismember.ThrowLimb(config, record, now); }
            catch (Exception error) { RuntimeLog.Error("dismember_limb_failed error=" + error.Message); return false; }
        }

        private void OnLimbLanded(Ped limb, int bone, long now)
        {
            if (config.StockBloodVisuals || string.IsNullOrEmpty(config.ExternalLimbLandingEffectName)) return;
            Play(config.ExternalLimbLandingEffectName, limb, bone, config.ExternalLimbLandingScale, now, 0, 0);
        }

        // DevTools > Gore Test: deterministic checks that need no aiming. Requests are set on the menu's thread and
        // run here on the combat tick.
        private void RunGoreTest(Ped player, long now)
        {
            int request = goreTestRequest;
            if (request != 0)
            {
                goreTestRequest = 0;
                Ped target = NearestPed(player, 12.0f);
                if (target == null) { goreTestStatus = "Gore test: no NPC within 12 m"; goreTestShownUntil = now + 4000; return; }
                if (request == 1)
                {
                    galleryPed = target;
                    galleryEffects = new List<string>();
                    foreach (string name in new[] { config.ImpactEffectName, config.MistEffectName, config.ExitEffectName, config.HeavyChunksEffectName,
                        config.ShotgunEntryEffectName, config.ShotgunChunksEffectName, config.SniperEntryEffectName, config.SniperChunksEffectName,
                        config.WoundSpurtEffectName, config.ArterialEffectName, config.SeverBurstEffectName, config.SeverMistEffectName,
                        config.DeathEffectName, config.MouthBloodEffectName, config.BleedEffectName, config.DeathLeakEffectName })
                        if (!string.IsNullOrEmpty(name) && !galleryEffects.Contains(name)) galleryEffects.Add(name);
                    foreach (string name in config.HeadTraumaEffects) if (!galleryEffects.Contains(name)) galleryEffects.Add(name);
                    foreach (string name in config.TraumaEffects) if (!galleryEffects.Contains(name)) galleryEffects.Add(name);
                    galleryIndex = 0;
                    galleryNext = now;
                    RuntimeLog.Info("gore_test gallery effects=" + galleryEffects.Count);
                }
                else if (request == 5)
                {
                    bool leakStarted = blood.Leak(config, config.ExternalBleedEffectName, target, 0x36A0,
                        config.EffectScale * config.ExternalBleedScaleMultiplier, now, config.ExternalBleedDurationMilliseconds,
                        config.ExternalBleedStartIntervalMilliseconds, config.ExternalBleedEndIntervalMilliseconds, false);
                    goreTestStatus = leakStarted ? "Gore test: wound leak started" : "Gore test: wound leak refused";
                    goreTestShownUntil = now + 4000;
                    RuntimeLog.Info("gore_test leak spawned=" + leakStarted);
                }
                else
                {
                    LimbCutPlan plan = request == 2 ? LimbCutPlan.ForHitBone(0x4C2) : request == 3 ? LimbCutPlan.ForHitBone(0x1A7) : LimbCutPlan.Head();
                    Vector3 away = target.Position - player.Position;
                    float length = (float)Math.Sqrt(away.X * away.X + away.Y * away.Y);
                    Vector3 push = length > 0.01f ? new Vector3(away.X / length, away.Y / length, 0) : new Vector3(0, 0, 0);
                    if (!target.isDead) target.Die();
                    Queue(target, plan, push, now, config.GoreTestScale > 0 ? config.GoreTestScale : config.EffectScale);
                    goreTestStatus = "Gore test: severing " + plan.Name;
                    goreTestShownUntil = now + 4000;
                    RuntimeLog.Info("gore_test sever part=" + plan.Name);
                }
            }
            if (galleryEffects == null || now < galleryNext) return;
            if (galleryIndex >= galleryEffects.Count || galleryPed == null || !galleryPed.Exists())
            {
                galleryEffects = null;
                goreTestStatus = "Gore test: gallery finished";
                goreTestShownUntil = now + 3000;
                return;
            }
            string effect = galleryEffects[galleryIndex++];
            bool ok = Play(effect, galleryPed, 0x36A0, config.GoreTestScale, now, config.GoreTestIntervalMilliseconds, 0);
            goreTestStatus = "Gore test " + galleryIndex + "/" + galleryEffects.Count + ": " + effect + (ok ? "" : "  (engine refused)");
            goreTestShownUntil = now + config.GoreTestIntervalMilliseconds + 500;
            galleryNext = now + config.GoreTestIntervalMilliseconds;
            RuntimeLog.Info("gore_test effect=" + effect + " spawned=" + ok);
        }

        private Ped NearestPed(Ped player, float radius)
        {
            Ped best = null;
            float bestDistance = radius;
            foreach (Ped ped in World.GetPeds(player.Position, radius))
            {
                if (ped == null || ped == player || !ped.Exists() || ped.isInVehicle()) continue;
                if (dismember != null && dismember.IsTracked(ped)) continue;
                float distance = ped.Position.DistanceTo(player.Position);
                if (distance < bestDistance) { bestDistance = distance; best = ped; }
            }
            return best;
        }

        // Same requests as the Gore Test page (1 effect gallery, 2 cut left arm, 3 cut right leg, 4 cut head, 5 wound
        // leak on the nearest NPC); used by the autopilot. Runs on the next tick.
        internal void RequestGoreTest(int request) { goreTestRequest = request; }

        private List<MenuItem> GoreTestItems()
        {
            return new List<MenuItem> {
                MenuItem.Action("Play every blood effect on nearest NPC", () => { goreTestRequest = 1; return "Close the menu and watch the nearest NPC"; }),
                MenuItem.Action("Start wound leak on nearest NPC", () => { goreTestRequest = 5; return "Close the menu and watch the nearest NPC"; }),
                MenuItem.Confirmed("Kill nearest NPC and cut left arm", () => { goreTestRequest = 2; return "Close the menu and watch the nearest NPC"; }),
                MenuItem.Confirmed("Kill nearest NPC and cut right leg", () => { goreTestRequest = 3; return "Close the menu and watch the nearest NPC"; }),
                MenuItem.Confirmed("Kill nearest NPC and cut head", () => { goreTestRequest = 4; return "Close the menu and watch the nearest NPC"; }),
                MenuItem.Info(() => "Dismemberment: " + (dismember == null ? "OFF (see log)" : "ready, engine=" + dismember.EngineActive + ", severed=" + dismember.SeveredCount) + ", blood loops=" + blood.ActiveLoops + ", pulses=" + blood.ActivePulses + ", bodies=" + (bodies == null ? 0 : bodies.Count)),
            };
        }

        private void OnDraw(object sender, GraphicsEventArgs args)
        {
            if (disabled || goreTestStatus == null || clock.ElapsedMilliseconds > goreTestShownUntil) return;
            try
            {
                GTA.Graphics graphics = args.Graphics;
                graphics.Scaling = FontScaling.Pixel;
                graphics.DrawRectangle(new RectangleF(36, 24, 620, 34), Color.FromArgb(190, 8, 12, 18));
                graphics.DrawText(goreTestStatus, new RectangleF(48, 28, 600, 28), TextAlignment.Left, statusFont);
            }
            catch (Exception error) { RuntimeLog.Error("gore_test_draw_failed error=" + error.Message); goreTestStatus = null; }
        }

        private float ReactionForce(HitRegion region)
        {
            switch (region)
            {
                case HitRegion.Head: return config.ReactionForceHead;
                case HitRegion.Torso: return config.ReactionForceTorso;
                case HitRegion.LeftArm: case HitRegion.RightArm: return config.ReactionForceArm;
                case HitRegion.LeftLeg: case HitRegion.RightLeg: return config.ReactionForceLeg;
                default: return 0;
            }
        }

        private static float Clamp(float value, float minimum, float maximum) { return value < minimum ? minimum : value > maximum ? maximum : value; }

        // Engine access (Gunplay's resolved addresses) and the ADR-0005 hooks; validated on the player's own ped first.
        private void EnsureEngine(Ped self)
        {
            LibertyFramework.Gunplay.GunplayController gunplay = LibertyFramework.Gunplay.GunplayController.Instance;
            LibertyFramework.Core.Memory.GameAddresses addresses = gunplay != null ? gunplay.Addresses : null;
            if (addresses == null) return; // Gunplay has not resolved yet; try next tick
            engineChecked = true;
            if (!config.DismembermentEnabled && !config.DecapitationEnabled) return;
            if (!addresses.PedSkeletonResolved) { RuntimeLog.Error("dismemberment_unavailable ped skeleton not resolved (see engine_resolve ped_skeleton)"); return; }
            PedSkeleton skeleton = new PedSkeleton(LibertyFramework.Engine.LibertyEngine.Current.Memory.Live, addresses);
            uint pointer = skeleton.PedFromHandle(self.GetHashCode());
            if (pointer == 0 || skeleton.MatrixBase(pointer) == 0 || skeleton.IndexOf(pointer, self.Model.Hash, 0x4B5) <= 0)
            {
                skeleton.Dispose();
                RuntimeLog.Error("dismemberment_validation_failed player_ped=0x" + pointer.ToString("X8"));
                return;
            }
            collapseEngine = null;
            if (addresses.SkeletonUpdateResolved)
            {
                try
                {
                    collapseEngine = new SkeletonCollapseEngine(config.CollapseScale);
                    int site = 0;
                    foreach (uint call in addresses.SkeletonUpdateCallSites)
                        collapseEngine.HookCallSite("skeleton_update_" + (site++), call, addresses.SkeletonUpdateFunction);
                    // The ragdoll sync writes physics-driven bones directly (not through crSkeleton::Update). It calls
                    // fragInst vfunc +E0h itself on entry (0x5F7DBD), so the hook may call it too.
                    if (addresses.SkeletonHooksResolved)
                    {
                        collapseEngine.HookFragFunction("frag_skeleton_sync", addresses.FragSkeletonSyncFunction,
                            new byte?[] { 0x81, 0xEC, 0x64, 0x01, 0x00, 0x00, 0xA1, null, null, null, null });
                    }
                    collapseEngine.Install();
                }
                catch (Exception error)
                {
                    RuntimeLog.Error("skeleton_collapse_engine_failed tick fallback only error=" + error.Message);
                    RemoveHooks();
                    collapseEngine = null;
                }
            }
            else
            {
                RuntimeLog.Error("skeleton_collapse_engine_unavailable (see engine_resolve skeleton_update) tick fallback only");
            }
            dismember = new Dismemberment(skeleton, collapseEngine, config.CollapseScale, stats);
            RuntimeLog.Info("dismemberment_ready player_ped=0x" + pointer.ToString("X8") + " engine=" + dismember.EngineActive);
        }

        private void DisableDismemberment(Exception error)
        {
            RuntimeLog.Error("feature_disabled dismemberment error=" + error);
            RemoveHooks();
            try { dismember.Clear(); } catch (Exception cleanup) { RuntimeLog.Error("dismemberment_cleanup_failed error=" + cleanup.Message); }
            dismember = null;
        }

        // Memory only (safe at unload): restores every patched byte; the engine table is emptied first.
        private void RemoveHooks()
        {
            try { if (collapseEngine != null) collapseEngine.Remove(); }
            catch (Exception error) { RuntimeLog.Error("skeleton_collapse_remove_failed error=" + error.Message); }
        }

        private void ClearAll()
        {
            states.Clear();
            pending.Clear();
            try { blood.StopAll(); } catch (Exception error) { RuntimeLog.Error("blood_stop_failed error=" + error.Message); }
            if (dismember != null) { try { dismember.Clear(); } catch (Exception error) { RuntimeLog.Error("dismemberment_cleanup_failed error=" + error.Message); } }
            if (panic != null) panic.Clear();
        }

        // T-047 (audit F14): stopping the module (`stop combat`, hot reload, restart) takes the ADR-0005 skeleton hooks out, so
        // a restarted instance installs its own instead of finding ours still in place. Natives are allowed here.
        protected internal override void OnStop()
        {
            RemoveHooks();
            try { ClearAll(); } catch (Exception error) { RuntimeLog.Error("combat_effects_cleanup_failed error=" + error.Message); }
            try { if (bodies != null) bodies.ReleaseAll(); } catch (Exception error) { RuntimeLog.Error("body_release_all_failed error=" + error.Message); }
            collapseEngine = null;
            dismember = null;
            EffectBudget.Reset();
        }

        // Unload and process exit: memory only (no natives). Hooks come out before the domain goes away.
        private void OnUnload(object sender, EventArgs args)
        {
            RemoveHooks();
        }
    }
}
