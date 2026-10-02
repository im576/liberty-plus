using System;
using System.IO;
using System.Runtime.Serialization;

#pragma warning disable 0649
namespace LibertyFramework.CombatEffects
{
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

        internal bool GoreConfigured { get { return EffectScale > 0 && MaximumEmitters > 0 && !string.IsNullOrEmpty(BleedEffectName); } }
        internal bool StockBloodVisuals { get { return string.IsNullOrEmpty(BloodVisualMode) || BloodVisualMode == "stock"; } }

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
            if (AllFirearms) { return; }
            foreach (int id in AllowedWeaponIds)
                if (id < 58 || id > 255) throw new InvalidDataException("combat effects requires registered test weapon IDs");
        }
    }
}
