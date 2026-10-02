using System;
using System.IO;
using System.Runtime.Serialization;

#pragma warning disable 0649
namespace LibertyFramework.CombatEffects
{
    // One weapon class of the gore presentation (T-047). The catalog's "family" (T-041) is the natural source of the
    // ids once it lands; until then the ids are listed here. scaleMultiplier is the caliber sensitivity: it multiplies
    // the entry/exit effect scale. Effects fade with distance from the attacker over effectFalloffMeters (scale 1 at
    // point blank down to distanceScaleFloor at that distance and beyond).
    [DataContract]
    internal sealed class GoreWeaponClass
    {
        [DataMember(Name="name", IsRequired=true)] internal string Name;
        [DataMember(Name="weaponIds", IsRequired=true)] internal int[] WeaponIds;
        [DataMember(Name="scaleMultiplier", IsRequired=false)] internal float ScaleMultiplier;
        [DataMember(Name="severeHeadMaximumMeters", IsRequired=false)] internal float SevereHeadMaximumMeters;
        // 0 = this class has no trauma effect.
        [DataMember(Name="traumaMaximumMeters", IsRequired=false)] internal float TraumaMaximumMeters;
        [DataMember(Name="effectFalloffMeters", IsRequired=false)] internal float EffectFalloffMeters;
        [DataMember(Name="distanceScaleFloor", IsRequired=false)] internal float DistanceScaleFloor;
        // Names of the shipped effects (entry/chunks); empty = the global default for that slot.
        [DataMember(Name="entryEffect", IsRequired=false)] internal string EntryEffect;
        [DataMember(Name="chunksEffect", IsRequired=false)] internal string ChunksEffect;

        [OnDeserializing]
        private void ApplyDefaults(StreamingContext context)
        {
            ScaleMultiplier = 1.0f; SevereHeadMaximumMeters = 10f; EffectFalloffMeters = 30f; DistanceScaleFloor = 0.6f;
        }

        internal static GoreWeaponClass[] Defaults()
        {
            return new[]
            {
                Make("pistol", new[] { 7, 8, 9, 58 }, 1.0f, 10f, 0f, 30f, 0.6f, null, null),
                Make("smg", new[] { 12, 13 }, 0.9f, 12f, 0f, 30f, 0.6f, null, null),
                Make("rifle", new[] { 14, 15, 59 }, 1.3f, 20f, 0f, 45f, 0.7f, null, "blood_gun_chunks"),
                Make("shotgun", new[] { 10, 11, 60 }, 1.5f, 15f, 5f, 15f, 0.5f, "blood_shotgun_entry", "blood_shotgun_chunks"),
                Make("sniper", new[] { 16, 17 }, 1.8f, 60f, 0f, 200f, 1.0f, "blood_sniper_entry", "blood_sniper_chunks"),
            };
        }

        private static GoreWeaponClass Make(string name, int[] ids, float scale, float head, float trauma, float falloff, float floor, string entry, string chunks)
        {
            GoreWeaponClass value = new GoreWeaponClass();
            value.Name = name; value.WeaponIds = ids; value.ScaleMultiplier = scale; value.SevereHeadMaximumMeters = head;
            value.TraumaMaximumMeters = trauma; value.EffectFalloffMeters = falloff; value.DistanceScaleFloor = floor;
            value.EntryEffect = entry; value.ChunksEffect = chunks;
            return value;
        }
    }

    [DataContract]
    internal sealed class CombatEffectsConfig
    {
        [DataMember(Name="schemaVersion", IsRequired=true)] internal int SchemaVersion;
        [DataMember(Name="enabled", IsRequired=true)] internal bool Enabled;
        [DataMember(Name="reactionsEnabled", IsRequired=true)] internal bool ReactionsEnabled;
        [DataMember(Name="injuriesEnabled", IsRequired=true)] internal bool InjuriesEnabled;
        [DataMember(Name="woundsEnabled", IsRequired=true)] internal bool WoundsEnabled;
        [DataMember(Name="limbLossPrototypeEnabled", IsRequired=true)] internal bool LimbLossPrototypeEnabled;
        [DataMember(Name="headLossPrototypeEnabled", IsRequired=true)] internal bool HeadLossPrototypeEnabled;
        [DataMember(Name="impactEffectName", IsRequired=true)] internal string ImpactEffectName;
        [DataMember(Name="woundEffectName", IsRequired=true)] internal string WoundEffectName;
        [DataMember(Name="reactionForceHead", IsRequired=true)] internal float ReactionForceHead;
        [DataMember(Name="reactionForceTorso", IsRequired=true)] internal float ReactionForceTorso;
        [DataMember(Name="reactionForceArm", IsRequired=true)] internal float ReactionForceArm;
        [DataMember(Name="reactionForceLeg", IsRequired=true)] internal float ReactionForceLeg;
        [DataMember(Name="reactionVerticalFraction", IsRequired=true)] internal float ReactionVerticalFraction;
        [DataMember(Name="scanRadiusMeters", IsRequired=true)] internal float ScanRadiusMeters;
        [DataMember(Name="sampleIntervalMilliseconds", IsRequired=true)] internal int SampleIntervalMilliseconds;
        [DataMember(Name="maximumTrackedPeds", IsRequired=true)] internal int MaximumTrackedPeds;
        [DataMember(Name="maximumWoundsPerPed", IsRequired=true)] internal int MaximumWoundsPerPed;
        [DataMember(Name="woundLifetimeMilliseconds", IsRequired=true)] internal int WoundLifetimeMilliseconds;
        [DataMember(Name="reactionCooldownMilliseconds", IsRequired=true)] internal int ReactionCooldownMilliseconds;
        [DataMember(Name="minimumInjuryDamage", IsRequired=true)] internal int MinimumInjuryDamage;
        [DataMember(Name="minimumLimbLossDamage", IsRequired=true)] internal int MinimumLimbLossDamage;
        [DataMember(Name="minimumLimbLossHits", IsRequired=true)] internal int MinimumLimbLossHits;
        [DataMember(Name="allowedWeaponIds", IsRequired=true)] internal int[] AllowedWeaponIds;
        // T-022 dismemberment (optional fields; absent = off). A lethal limb hit collapses that limb's bones.
        [DataMember(Name="dismembermentEnabled", IsRequired=false)] internal bool DismembermentEnabled;
        [DataMember(Name="severedLimbEnabled", IsRequired=false)] internal bool SeveredLimbEnabled;
        [DataMember(Name="severedLimbForce", IsRequired=false)] internal float SeveredLimbForce;
        [DataMember(Name="severedLimbLifetimeMilliseconds", IsRequired=false)] internal int SeveredLimbLifetimeMilliseconds;
        [DataMember(Name="maximumSeveredPeds", IsRequired=false)] internal int MaximumSeveredPeds;
        [DataMember(Name="severedLimbLifetimeCorpseMilliseconds", IsRequired=false)] internal int SeveredCorpseLifetimeMilliseconds;
        [DataMember(Name="stumpEffectName", IsRequired=false)] internal string StumpEffectName;
        // Gore overhaul (all optional). Effect names are stock gta_core.wpfl particle effects.
        [DataMember(Name="allFirearms", IsRequired=false)] internal bool AllFirearms;
        [DataMember(Name="includeMissionPeds", IsRequired=false)] internal bool IncludeMissionPeds;
        [DataMember(Name="effectScale", IsRequired=false)] internal float EffectScale;
        [DataMember(Name="exitEffectName", IsRequired=false)] internal string ExitEffectName;
        [DataMember(Name="shotgunEntryEffectName", IsRequired=false)] internal string ShotgunEntryEffectName;
        [DataMember(Name="shotgunChunksEffectName", IsRequired=false)] internal string ShotgunChunksEffectName;
        [DataMember(Name="sniperEntryEffectName", IsRequired=false)] internal string SniperEntryEffectName;
        [DataMember(Name="sniperChunksEffectName", IsRequired=false)] internal string SniperChunksEffectName;
        [DataMember(Name="heavyChunksEffectName", IsRequired=false)] internal string HeavyChunksEffectName;
        [DataMember(Name="exitDamage", IsRequired=false)] internal int ExitDamage;
        [DataMember(Name="chunkDamage", IsRequired=false)] internal int ChunkDamage;
        [DataMember(Name="bleedEffectName", IsRequired=false)] internal string BleedEffectName;
        [DataMember(Name="bleedIntervalMilliseconds", IsRequired=false)] internal int BleedIntervalMilliseconds;
        [DataMember(Name="bleedDurationMilliseconds", IsRequired=false)] internal int BleedDurationMilliseconds;
        [DataMember(Name="arterialEffectName", IsRequired=false)] internal string ArterialEffectName;
        [DataMember(Name="arterialIntervalMilliseconds", IsRequired=false)] internal int ArterialIntervalMilliseconds;
        [DataMember(Name="arterialDurationMilliseconds", IsRequired=false)] internal int ArterialDurationMilliseconds;
        [DataMember(Name="severBurstEffectName", IsRequired=false)] internal string SeverBurstEffectName;
        [DataMember(Name="severMistEffectName", IsRequired=false)] internal string SeverMistEffectName;
        [DataMember(Name="decapitationEnabled", IsRequired=false)] internal bool DecapitationEnabled;
        [DataMember(Name="decapitationMinimumDamage", IsRequired=false)] internal int DecapitationMinimumDamage;
        [DataMember(Name="mouthBloodEffectName", IsRequired=false)] internal string MouthBloodEffectName;
        [DataMember(Name="pendingDeathWindowMilliseconds", IsRequired=false)] internal int PendingDeathWindowMilliseconds;
        [DataMember(Name="maximumEmitters", IsRequired=false)] internal int MaximumEmitters;
        // Visibility pass: hits below minimumEffectDamage (bleed-out ticks) only drip; every other hit adds mist;
        // strong hits spurt; the killing hit bursts; the DevTools gore test plays effects at goreTestScale.
        [DataMember(Name="minimumEffectDamage", IsRequired=false)] internal int MinimumEffectDamage;
        [DataMember(Name="mistEffectName", IsRequired=false)] internal string MistEffectName;
        [DataMember(Name="deathEffectName", IsRequired=false)] internal string DeathEffectName;
        [DataMember(Name="woundSpurtEffectName", IsRequired=false)] internal string WoundSpurtEffectName;
        [DataMember(Name="woundSpurtDurationMilliseconds", IsRequired=false)] internal int WoundSpurtDurationMilliseconds;
        [DataMember(Name="maximumHitScale", IsRequired=false)] internal float MaximumHitScale;
        [DataMember(Name="goreTestScale", IsRequired=false)] internal float GoreTestScale;
        [DataMember(Name="goreTestIntervalMilliseconds", IsRequired=false)] internal int GoreTestIntervalMilliseconds;
        // Looping stock effects (streams, drips, mist, chunks) run as started effects: bounded count, burst length for
        // one-off uses. The killing hit leaves the body leaking. Severing waits for the death ragdoll to start, and
        // collapsed bones keep a tiny uniform scale (never zero).
        [DataMember(Name="maximumLoopedEffects", IsRequired=false)] internal int MaximumLoopedEffects;
        [DataMember(Name="burstLoopMilliseconds", IsRequired=false)] internal int BurstLoopMilliseconds;
        [DataMember(Name="deathLeakEffectName", IsRequired=false)] internal string DeathLeakEffectName;
        [DataMember(Name="deathLeakDurationMilliseconds", IsRequired=false)] internal int DeathLeakDurationMilliseconds;
        [DataMember(Name="severDelayMilliseconds", IsRequired=false)] internal int SeverDelayMilliseconds;
        [DataMember(Name="collapseScale", IsRequired=false)] internal float CollapseScale;
        // T-026: damage sampling runs at sampleIntervalMilliseconds only within activeSampleWindowMilliseconds of the
        // player's last shot, otherwise at idleSampleIntervalMilliseconds (0 or not above sampleInterval = always full rate).
        [DataMember(Name="idleSampleIntervalMilliseconds", IsRequired=false)] internal int IdleSampleIntervalMilliseconds;
        [DataMember(Name="activeSampleWindowMilliseconds", IsRequired=false)] internal int ActiveSampleWindowMilliseconds;
        // T-026: dismemberment upkeep cadence once the engine collapse is installed (0 = every tick).
        [DataMember(Name="dismemberRefreshMilliseconds", IsRequired=false)] internal int DismemberRefreshMilliseconds;
        // Cuts allowed per body (pending + done; 0 = unlimited). 1 = one limb or the head per kill.
        [DataMember(Name="maximumCutsPerPed", IsRequired=false)] internal int MaximumCutsPerPed;
        // Limb clone presentation: spawn offset beside the corpse (m), engine-confirmed ticks before it is shown,
        // settle time before the floating check (ms), and the height above ground (m) that counts as floating.
        [DataMember(Name="severedLimbSpawnOffsetMeters", IsRequired=false)] internal float SeveredLimbSpawnOffsetMeters;
        [DataMember(Name="limbConfirmTicks", IsRequired=false)] internal int LimbConfirmTicks;
        [DataMember(Name="limbSettleMilliseconds", IsRequired=false)] internal int LimbSettleMilliseconds;
        [DataMember(Name="limbFloatingHeightMeters", IsRequired=false)] internal float LimbFloatingHeightMeters;
        // An external visual mod can draw impact wounds and surface blood while this script owns cuts and reactions.
        [DataMember(Name="bloodVisualMode", IsRequired=false)] internal string BloodVisualMode;
        // Pulse only confirmed one-shot particles when the installed CE build refuses looping blood PTFX.
        [DataMember(Name="externalBleedEffectName", IsRequired=false)] internal string ExternalBleedEffectName;
        [DataMember(Name="externalBleedMinimumDamage", IsRequired=false)] internal int ExternalBleedMinimumDamage;
        [DataMember(Name="externalBleedDurationMilliseconds", IsRequired=false)] internal int ExternalBleedDurationMilliseconds;
        [DataMember(Name="externalFatalBleedDurationMilliseconds", IsRequired=false)] internal int ExternalFatalBleedDurationMilliseconds;
        [DataMember(Name="externalBleedStartIntervalMilliseconds", IsRequired=false)] internal int ExternalBleedStartIntervalMilliseconds;
        [DataMember(Name="externalBleedEndIntervalMilliseconds", IsRequired=false)] internal int ExternalBleedEndIntervalMilliseconds;
        [DataMember(Name="externalBleedScaleMultiplier", IsRequired=false)] internal float ExternalBleedScaleMultiplier;
        [DataMember(Name="externalBleedEndScaleFraction", IsRequired=false)] internal float ExternalBleedEndScaleFraction;
        [DataMember(Name="externalMaximumBleedEmitters", IsRequired=false)] internal int ExternalMaximumBleedEmitters;
        [DataMember(Name="externalStumpBurstEffectName", IsRequired=false)] internal string ExternalStumpBurstEffectName;
        [DataMember(Name="externalStumpBleedScale", IsRequired=false)] internal float ExternalStumpBleedScale;
        [DataMember(Name="externalStumpBleedDurationMilliseconds", IsRequired=false)] internal int ExternalStumpBleedDurationMilliseconds;
        [DataMember(Name="externalStumpBleedStartIntervalMilliseconds", IsRequired=false)] internal int ExternalStumpBleedStartIntervalMilliseconds;
        [DataMember(Name="externalStumpBleedEndIntervalMilliseconds", IsRequired=false)] internal int ExternalStumpBleedEndIntervalMilliseconds;
        [DataMember(Name="externalLimbLandingEffectName", IsRequired=false)] internal string ExternalLimbLandingEffectName;
        [DataMember(Name="externalLimbLandingScale", IsRequired=false)] internal float ExternalLimbLandingScale;
        [DataMember(Name="limbLandingMinimumMilliseconds", IsRequired=false)] internal int LimbLandingMinimumMilliseconds;
        [DataMember(Name="limbLandingMaximumHeightMeters", IsRequired=false)] internal float LimbLandingMaximumHeightMeters;
        [DataMember(Name="limbThrowMaximumAttempts", IsRequired=false)] internal int LimbThrowMaximumAttempts;
        [DataMember(Name="limbThrowRetryMilliseconds", IsRequired=false)] internal int LimbThrowRetryMilliseconds;
        [DataMember(Name="severedLimbSpawnHeightMeters", IsRequired=false)] internal float SeveredLimbSpawnHeightMeters;
        [DataMember(Name="severedLimbVerticalForceFraction", IsRequired=false)] internal float SeveredLimbVerticalForceFraction;
        // The thrown-limb clone is told to ragdoll for this long after it is made (0 = not). Added in T-047 for floating limbs.
        [DataMember(Name="severedLimbRagdollMilliseconds", IsRequired=false)] internal int SeveredLimbRagdollMilliseconds;
        // The T-047 measurement found most "floating" limbs were clones that were still alive and standing (Die() on a fresh ped did
        // not take): a clone is kept invisible and re-killed every tick until it is dead, for at most limbKillTimeoutMilliseconds, then
        // removed and counted as a failure. The floating check waits for the limb to stop moving (below limbSettleSpeedMetersPerSecond), for at most
        // limbSettleMaximumMilliseconds after it was shown, so a limb still tumbling is not judged in mid-air.
        [DataMember(Name="limbKillTimeoutMilliseconds", IsRequired=false)] internal int LimbKillTimeoutMilliseconds;
        // Upkeep of the severed records (corpses and limb clones) is spread over ticks: at most this many records are serviced per tick,
        // round-robin (0 = all every tick). The native hook applies the collapse every frame anyway; this only bounds the script-side work
        // (existence and skeleton checks, refreshes after a ragdoll change) so many corpses cannot make one tick expensive.
        [DataMember(Name="dismemberRecordsPerTick", IsRequired=false)] internal int DismemberRecordsPerTick;
        [DataMember(Name="limbSettleMaximumMilliseconds", IsRequired=false)] internal int LimbSettleMaximumMilliseconds;
        [DataMember(Name="limbSettleSpeedMetersPerSecond", IsRequired=false)] internal float LimbSettleSpeedMetersPerSecond;

        // T-047 (Stage 1 harsh gore). All optional: OnDeserializing gives every field its default, so a config file from
        // before T-047 loads and behaves as documented in CONFIG_SCHEMA.md. The scan* and sample* fields above are unused
        // (damage arrives as exact PedDamaged events) and kept only so older files still validate.
        // Weapon classes: which vanilla/catalog weapon ids belong to which class, and how the class changes the presentation.
        [DataMember(Name="weaponClasses", IsRequired=false)] internal GoreWeaponClass[] WeaponClasses;
        // Severe head trauma (any firearm hit to the head within the class's severeHeadMaximumMeters) and trauma (shotgun-type
        // hit within traumaMaximumMeters): extra effect sets on top of the entry effects, scaled by headTraumaScale/traumaScale.
        [DataMember(Name="headTraumaEffects", IsRequired=false)] internal string[] HeadTraumaEffects;
        [DataMember(Name="headTraumaScale", IsRequired=false)] internal float HeadTraumaScale;
        [DataMember(Name="traumaEffects", IsRequired=false)] internal string[] TraumaEffects;
        [DataMember(Name="traumaScale", IsRequired=false)] internal float TraumaScale;
        // Exact events beyond this distance from the player produce no particles (they are still counted); at most
        // maximumEffectHitsPerFrame hits per engine frame get the full effect set (the rest only bleed).
        [DataMember(Name="effectMaximumDistanceMeters", IsRequired=false)] internal float EffectMaximumDistanceMeters;
        [DataMember(Name="maximumEffectHitsPerFrame", IsRequired=false)] internal int MaximumEffectHitsPerFrame;
        // combat_hit log lines per second (severe hits and kills are always logged); the counters are never limited.
        [DataMember(Name="maximumHitLogsPerSecond", IsRequired=false)] internal int MaximumHitLogsPerSecond;
        // A hit that is not the killing hit but is severe (head, trauma, limb loss) can leave the victim suffering:
        // ragdoll, pain speech, then the game's own cower task. woundedChance is the share of such hits (0-1).
        [DataMember(Name="woundedChance", IsRequired=false)] internal float WoundedChance;
        [DataMember(Name="woundedRagdollMilliseconds", IsRequired=false)] internal int WoundedRagdollMilliseconds;
        [DataMember(Name="woundedCowerMilliseconds", IsRequired=false)] internal int WoundedCowerMilliseconds;
        [DataMember(Name="woundedSpeechContexts", IsRequired=false)] internal string[] WoundedSpeechContexts;
        [DataMember(Name="woundedMaximumPeds", IsRequired=false)] internal int WoundedMaximumPeds;
        // Nearby unarmed pedestrians flee after severe violence (a kill, or a severe hit).
        [DataMember(Name="panicEnabled", IsRequired=false)] internal bool PanicEnabled;
        [DataMember(Name="panicRadiusMeters", IsRequired=false)] internal float PanicRadiusMeters;
        [DataMember(Name="panicMaximumPeds", IsRequired=false)] internal int PanicMaximumPeds;
        [DataMember(Name="panicFleeDistanceMeters", IsRequired=false)] internal float PanicFleeDistanceMeters;
        [DataMember(Name="panicCooldownMilliseconds", IsRequired=false)] internal int PanicCooldownMilliseconds;
        [DataMember(Name="panicScreamChance", IsRequired=false)] internal float PanicScreamChance;
        [DataMember(Name="panicSpeechContexts", IsRequired=false)] internal string[] PanicSpeechContexts;
        [DataMember(Name="panicVerifyMilliseconds", IsRequired=false)] internal int PanicVerifyMilliseconds;
        [DataMember(Name="panicMovedMeters", IsRequired=false)] internal float PanicMovedMeters;
        // Bodies of peds killed near the player stay (the game is told to keep them) for a lifetime between the two
        // values, then are released. Hard cap, distance cleanup, and both shrink under performance pressure.
        [DataMember(Name="bodyPersistenceEnabled", IsRequired=false)] internal bool BodyPersistenceEnabled;
        [DataMember(Name="bodyLifetimeMinimumMilliseconds", IsRequired=false)] internal int BodyLifetimeMinimumMilliseconds;
        [DataMember(Name="bodyLifetimeMaximumMilliseconds", IsRequired=false)] internal int BodyLifetimeMaximumMilliseconds;
        [DataMember(Name="maximumBodies", IsRequired=false)] internal int MaximumBodies;
        [DataMember(Name="bodyMaximumDistanceAtDeathMeters", IsRequired=false)] internal float BodyMaximumDistanceAtDeathMeters;
        [DataMember(Name="bodyCleanupDistanceMeters", IsRequired=false)] internal float BodyCleanupDistanceMeters;
        [DataMember(Name="bodySweepIntervalMilliseconds", IsRequired=false)] internal int BodySweepIntervalMilliseconds;
        [DataMember(Name="minimumBodiesUnderPressure", IsRequired=false)] internal int MinimumBodiesUnderPressure;
        // Total active gore/effect emitters (looped effects plus pulses; T-048's effects report into the same budget);
        // under pressure the cap shrinks to no fewer than minimumActiveEffects. Oldest are removed first.
        [DataMember(Name="maximumActiveEffects", IsRequired=false)] internal int MaximumActiveEffects;
        [DataMember(Name="minimumActiveEffects", IsRequired=false)] internal int MinimumActiveEffects;
        [DataMember(Name="oneShotLifetimeMilliseconds", IsRequired=false)] internal int OneShotLifetimeMilliseconds;
        // Blood pools, trails and surface blood: interface designed (IBloodSurface), implementation waits for T-051.
        [DataMember(Name="bloodSurfaceEnabled", IsRequired=false)] internal bool BloodSurfaceEnabled;

        [OnDeserializing]
        private void ApplyDefaults(StreamingContext context)
        {
            WeaponClasses = GoreWeaponClass.Defaults();
            HeadTraumaEffects = new[] { "blood_gun_entry_arterial", "blood_ped_mouth" };
            HeadTraumaScale = 1.4f;
            TraumaEffects = new[] { "blood_shotgun_entry", "blood_shotgun_exit" };
            TraumaScale = 1.5f;
            EffectMaximumDistanceMeters = 60;
            MaximumEffectHitsPerFrame = 6;
            WoundedChance = 0.35f;
            WoundedRagdollMilliseconds = 2500;
            WoundedCowerMilliseconds = 6000;
            MaximumHitLogsPerSecond = 10;
            WoundedSpeechContexts = new[] { "PAIN" };
            WoundedMaximumPeds = 4;
            PanicEnabled = true;
            PanicRadiusMeters = 25;
            PanicMaximumPeds = 8;
            PanicFleeDistanceMeters = 60;
            PanicCooldownMilliseconds = 8000;
            PanicScreamChance = 0.5f;
            PanicSpeechContexts = new[] { "PAIN" };
            PanicVerifyMilliseconds = 3000;
            PanicMovedMeters = 1.5f;
            BodyPersistenceEnabled = true;
            BodyLifetimeMinimumMilliseconds = 180000;
            BodyLifetimeMaximumMilliseconds = 300000;
            MaximumBodies = 10;
            BodyMaximumDistanceAtDeathMeters = 60;
            BodyCleanupDistanceMeters = 120;
            BodySweepIntervalMilliseconds = 1000;
            MinimumBodiesUnderPressure = 2;
            MaximumActiveEffects = 24;
            MinimumActiveEffects = 6;
            OneShotLifetimeMilliseconds = 400;
            SeveredLimbRagdollMilliseconds = 4000;
            LimbKillTimeoutMilliseconds = 3000;
            DismemberRecordsPerTick = 8;
            LimbSettleMaximumMilliseconds = 4000;
            LimbSettleSpeedMetersPerSecond = 0.5f;
        }

        internal bool GoreConfigured { get { return EffectScale > 0 && MaximumEmitters > 0 && !string.IsNullOrEmpty(BleedEffectName); } }
        internal bool StockBloodVisuals { get { return string.IsNullOrEmpty(BloodVisualMode) || BloodVisualMode == "stock"; } }

        private void ValidateGore()
        {
            if (WeaponClasses == null || WeaponClasses.Length == 0 || WeaponClasses.Length > 16)
                throw new InvalidDataException("combat_effects.json weaponClasses must have 1-16 entries");
            System.Collections.Generic.HashSet<int> seen = new System.Collections.Generic.HashSet<int>();
            foreach (GoreWeaponClass weaponClass in WeaponClasses)
            {
                if (weaponClass == null || string.IsNullOrEmpty(weaponClass.Name) || weaponClass.WeaponIds == null || weaponClass.WeaponIds.Length == 0 ||
                    weaponClass.ScaleMultiplier <= 0 || weaponClass.ScaleMultiplier > 4 || weaponClass.SevereHeadMaximumMeters < 0 || weaponClass.SevereHeadMaximumMeters > 300 ||
                    weaponClass.TraumaMaximumMeters < 0 || weaponClass.TraumaMaximumMeters > 300 || weaponClass.EffectFalloffMeters <= 0 || weaponClass.EffectFalloffMeters > 500 ||
                    weaponClass.DistanceScaleFloor <= 0 || weaponClass.DistanceScaleFloor > 1)
                    throw new InvalidDataException("combat_effects.json weaponClasses entry invalid");
                foreach (int id in weaponClass.WeaponIds)
                    if (id < 0 || id > 255 || !seen.Add(id)) throw new InvalidDataException("combat_effects.json weapon id " + id + " listed twice or out of range");
            }
            if (HeadTraumaEffects == null || TraumaEffects == null || WoundedSpeechContexts == null || PanicSpeechContexts == null ||
                HeadTraumaScale <= 0 || HeadTraumaScale > 8 || TraumaScale <= 0 || TraumaScale > 8 ||
                EffectMaximumDistanceMeters < 5 || EffectMaximumDistanceMeters > 500 || MaximumEffectHitsPerFrame < 1 || MaximumEffectHitsPerFrame > 64 ||
                WoundedChance < 0 || WoundedChance > 1 || WoundedRagdollMilliseconds < 0 || WoundedRagdollMilliseconds > 10000 || WoundedCowerMilliseconds < 0 || WoundedCowerMilliseconds > 60000 ||
                MaximumHitLogsPerSecond < 0 || MaximumHitLogsPerSecond > 1000 || WoundedMaximumPeds < 0 || WoundedMaximumPeds > 32 ||
                PanicRadiusMeters < 1 || PanicRadiusMeters > 100 || PanicMaximumPeds < 0 || PanicMaximumPeds > 32 || PanicFleeDistanceMeters < 5 || PanicFleeDistanceMeters > 200 ||
                PanicCooldownMilliseconds < 0 || PanicCooldownMilliseconds > 120000 || PanicScreamChance < 0 || PanicScreamChance > 1 ||
                PanicVerifyMilliseconds < 500 || PanicVerifyMilliseconds > 30000 || PanicMovedMeters <= 0 || PanicMovedMeters > 20)
                throw new InvalidDataException("combat_effects.json gore/wounded/panic bounds invalid");
            if (BodyLifetimeMinimumMilliseconds < 1000 || BodyLifetimeMaximumMilliseconds < BodyLifetimeMinimumMilliseconds || BodyLifetimeMaximumMilliseconds > 3600000 ||
                MaximumBodies < 1 || MaximumBodies > 64 || MinimumBodiesUnderPressure < 0 || MinimumBodiesUnderPressure > MaximumBodies ||
                BodyMaximumDistanceAtDeathMeters < 1 || BodyMaximumDistanceAtDeathMeters > 500 || BodyCleanupDistanceMeters < BodyMaximumDistanceAtDeathMeters || BodyCleanupDistanceMeters > 1000 ||
                BodySweepIntervalMilliseconds < 100 || BodySweepIntervalMilliseconds > 10000 ||
                MaximumActiveEffects < 1 || MaximumActiveEffects > 128 || MinimumActiveEffects < 0 || MinimumActiveEffects > MaximumActiveEffects ||
                OneShotLifetimeMilliseconds < 16 || OneShotLifetimeMilliseconds > 5000 || SeveredLimbRagdollMilliseconds < 0 || SeveredLimbRagdollMilliseconds > 30000 ||
                DismemberRecordsPerTick < 0 || DismemberRecordsPerTick > 64 || LimbKillTimeoutMilliseconds < 100 || LimbKillTimeoutMilliseconds > 30000 || LimbSettleMaximumMilliseconds < 0 || LimbSettleMaximumMilliseconds > 30000 ||
                LimbSettleSpeedMetersPerSecond <= 0 || LimbSettleSpeedMetersPerSecond > 10)
                throw new InvalidDataException("combat_effects.json persistence/budget bounds invalid");
        }

        internal void Validate()
        {
            if (!string.IsNullOrEmpty(BloodVisualMode) && BloodVisualMode != "stock" && BloodVisualMode != "external")
                throw new InvalidDataException("combat_effects.json bloodVisualMode must be stock or external");
            if (BloodVisualMode == "external" && (string.IsNullOrEmpty(ExternalBleedEffectName) || string.IsNullOrEmpty(ExternalStumpBurstEffectName) ||
                string.IsNullOrEmpty(ExternalLimbLandingEffectName) || ExternalBleedMinimumDamage < 1 || ExternalBleedMinimumDamage > 200 ||
                ExternalBleedDurationMilliseconds < 500 || ExternalBleedDurationMilliseconds > 30000 ||
                ExternalFatalBleedDurationMilliseconds < ExternalBleedDurationMilliseconds || ExternalFatalBleedDurationMilliseconds > 60000 ||
                ExternalBleedStartIntervalMilliseconds < 100 || ExternalBleedStartIntervalMilliseconds > 3000 ||
                ExternalBleedEndIntervalMilliseconds < ExternalBleedStartIntervalMilliseconds || ExternalBleedEndIntervalMilliseconds > 5000 ||
                ExternalBleedScaleMultiplier <= 0 || ExternalBleedScaleMultiplier > 2 ||
                ExternalBleedEndScaleFraction <= 0 || ExternalBleedEndScaleFraction > 1 ||
                ExternalMaximumBleedEmitters < 1 || ExternalMaximumBleedEmitters > 32 ||
                ExternalStumpBleedScale <= 0 || ExternalStumpBleedScale > 5 ||
                ExternalStumpBleedDurationMilliseconds < 500 || ExternalStumpBleedDurationMilliseconds > 60000 ||
                ExternalStumpBleedStartIntervalMilliseconds < 100 || ExternalStumpBleedStartIntervalMilliseconds > 3000 ||
                ExternalStumpBleedEndIntervalMilliseconds < ExternalStumpBleedStartIntervalMilliseconds || ExternalStumpBleedEndIntervalMilliseconds > 5000 ||
                ExternalLimbLandingScale <= 0 || ExternalLimbLandingScale > 5 ||
                LimbLandingMinimumMilliseconds < 0 || LimbLandingMinimumMilliseconds > 5000 ||
                LimbLandingMaximumHeightMeters <= 0 || LimbLandingMaximumHeightMeters > 3 ||
                LimbThrowMaximumAttempts < 1 || LimbThrowMaximumAttempts > 10 ||
                LimbThrowRetryMilliseconds < 0 || LimbThrowRetryMilliseconds > 3000 ||
                SeveredLimbSpawnHeightMeters < 0 || SeveredLimbSpawnHeightMeters > 2 ||
                SeveredLimbVerticalForceFraction < 0 || SeveredLimbVerticalForceFraction > 2))
                throw new InvalidDataException("combat effects external blood/limb bounds invalid");
            if (SchemaVersion != 1 || ScanRadiusMeters <= 0 || ScanRadiusMeters > 100 ||
                SampleIntervalMilliseconds < 25 || SampleIntervalMilliseconds > 1000 ||
                MaximumTrackedPeds < 1 || MaximumTrackedPeds > 64 || MaximumWoundsPerPed < 1 || MaximumWoundsPerPed > 16 ||
                WoundLifetimeMilliseconds < 100 || WoundLifetimeMilliseconds > 600000 ||
                ReactionCooldownMilliseconds < 0 || ReactionCooldownMilliseconds > 30000 ||
                MinimumInjuryDamage < 1 || MinimumLimbLossDamage < 1 || MinimumLimbLossHits < 1 ||
                AllowedWeaponIds == null || AllowedWeaponIds.Length == 0)
                throw new InvalidDataException("combat_effects.json invalid bounds");
            if (string.IsNullOrEmpty(ImpactEffectName) || string.IsNullOrEmpty(WoundEffectName) ||
                ReactionForceHead < 0 || ReactionForceHead > 10 || ReactionForceTorso < 0 || ReactionForceTorso > 10 ||
                ReactionForceArm < 0 || ReactionForceArm > 10 || ReactionForceLeg < 0 || ReactionForceLeg > 10 ||
                ReactionVerticalFraction < 0 || ReactionVerticalFraction > 1)
                throw new InvalidDataException("combat effects visual/reaction bounds invalid");
            if (DismembermentEnabled && (MaximumSeveredPeds < 1 || MaximumSeveredPeds > 16 || string.IsNullOrEmpty(StumpEffectName) ||
                SeveredCorpseLifetimeMilliseconds < 1000 || SeveredCorpseLifetimeMilliseconds > 600000 ||
                LimbThrowMaximumAttempts < 1 || LimbThrowMaximumAttempts > 10 || LimbThrowRetryMilliseconds < 0 || LimbThrowRetryMilliseconds > 3000 ||
                SeveredLimbSpawnHeightMeters < 0 || SeveredLimbSpawnHeightMeters > 2 ||
                SeveredLimbVerticalForceFraction < 0 || SeveredLimbVerticalForceFraction > 2 ||
                (SeveredLimbEnabled && (SeveredLimbForce < 0 || SeveredLimbForce > 30 || SeveredLimbLifetimeMilliseconds < 1000 || SeveredLimbLifetimeMilliseconds > 600000))))
                throw new InvalidDataException("combat effects dismemberment bounds invalid");
            if ((DismembermentEnabled || DecapitationEnabled) && (CollapseScale < 0.0001f || CollapseScale > 0.1f))
                throw new InvalidDataException("combat effects collapseScale must be 0.0001-0.1");
            if (GoreConfigured && (EffectScale > 5 || MaximumEmitters > 128 || BleedIntervalMilliseconds < 100 || BleedDurationMilliseconds < 0 ||
                ArterialIntervalMilliseconds < 100 || ArterialDurationMilliseconds < 0 || PendingDeathWindowMilliseconds < 0 ||
                PendingDeathWindowMilliseconds > 5000 || ExitDamage < 0 || ChunkDamage < 0 || DecapitationMinimumDamage < 0 || MinimumEffectDamage < 0 || WoundSpurtDurationMilliseconds < 0 ||
                MaximumHitScale < 0 || MaximumHitScale > 8 || GoreTestScale < 0 || GoreTestScale > 8 || GoreTestIntervalMilliseconds < 0 ||
                MaximumLoopedEffects < 1 || MaximumLoopedEffects > 64 || BurstLoopMilliseconds < 50 || BurstLoopMilliseconds > 5000 ||
                DeathLeakDurationMilliseconds < 0 || DeathLeakDurationMilliseconds > 120000 || SeverDelayMilliseconds < 0 || SeverDelayMilliseconds > 3000 ||
                IdleSampleIntervalMilliseconds < 0 || IdleSampleIntervalMilliseconds > 2000 || ActiveSampleWindowMilliseconds < 0 || ActiveSampleWindowMilliseconds > 30000 ||
                DismemberRefreshMilliseconds < 0 || DismemberRefreshMilliseconds > 1000 ||
                MaximumCutsPerPed < 0 || MaximumCutsPerPed > 8 ||
                SeveredLimbSpawnOffsetMeters < 0 || SeveredLimbSpawnOffsetMeters > 3 || LimbConfirmTicks < 0 || LimbConfirmTicks > 30 ||
                LimbSettleMilliseconds < 0 || LimbSettleMilliseconds > 10000 || LimbFloatingHeightMeters < 0.05f || LimbFloatingHeightMeters > 5))
                throw new InvalidDataException("combat effects gore bounds invalid");
            ValidateGore();
            if (AllFirearms) { return; }
            foreach (int id in AllowedWeaponIds)
                if (id < 58 || id > 255) throw new InvalidDataException("combat effects requires registered test weapon IDs");
        }
    }
}
