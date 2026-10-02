using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LibertyFramework.CombatEffects;
using LibertyFramework.CombatEffects.Logic;
using LibertyFramework.Core.Config;
using LibertyFramework.Finishes;

namespace LibertyFramework.Verify
{
    // T-022: limb cut plans, the gore config, and that every configured particle exists in the game's gta_core.wpfl.
    internal static class CombatChecks
    {
        internal static void Run(string exePath, string repoRoot, Checker check)
        {
            LimbCutPlan knee = LimbCutPlan.ForHitBone(0x1A4);
            check.True("left foot hit cuts at the knee", knee != null && knee.CutTag == 0x1A3 && knee.StumpTag == 0x1A2 &&
                Array.IndexOf(knee.RemovedTags, 0x1A2) < 0 && Array.IndexOf(knee.RemovedTags, 0x1A5) >= 0, knee == null ? "null" : knee.Name);
            LimbCutPlan hip = LimbCutPlan.ForHitBone(0x1A7);
            check.True("right thigh hit cuts at the hip and removes the whole leg", hip != null && hip.CutTag == 0x1A7 && hip.StumpTag == 0x1A1 &&
                Array.IndexOf(hip.RemovedTags, 0x4B0) >= 0, hip == null ? "null" : hip.Name);
            LimbCutPlan elbow = LimbCutPlan.ForHitBone(0x4D0);
            check.True("right hand hit cuts at the elbow, fingers included", elbow != null && elbow.CutTag == 0x4C9 &&
                Array.IndexOf(elbow.RemovedTags, 0x35C4) >= 0 && Array.IndexOf(elbow.RemovedTags, 0x4C8) < 0, elbow == null ? "null" : elbow.Name);
            LimbCutPlan shoulder = LimbCutPlan.ForHitBone(0x4C1);
            check.True("left upper-arm hit cuts at the shoulder, clavicle stays", shoulder != null && shoulder.CutTag == 0x4C1 &&
                shoulder.StumpTag == 0x4C0 && Array.IndexOf(shoulder.RemovedTags, 0x4C0) < 0, shoulder == null ? "null" : shoulder.Name);
            check.True("head and torso hits never cut a limb", LimbCutPlan.ForHitBone(0x4B5) == null && LimbCutPlan.ForHitBone(0x1A1) == null, "");
            check.True("unresolved knee/elbow cuts fall back to hip/shoulder", LimbCutPlan.Upper(LimbCutPlan.ForHitBone(0x1A8)).Name == "right_leg_hip" &&
                LimbCutPlan.Upper(LimbCutPlan.ForHitBone(0x4C2)).Name == "left_arm_shoulder" && LimbCutPlan.Upper(LimbCutPlan.ForHitBone(0x1A7)) == null, "");
            LimbCutPlan head = LimbCutPlan.Head();
            check.True("decapitation cuts at the neck, spine stays", head.CutTag == 0x4B4 && head.StumpTag == 0x36A1 && LimbCutPlan.IsHeadBone(0x4B5), "");

            CombatEffectsConfig config = JsonStore.Load<CombatEffectsConfig>(Path.Combine(repoRoot, Path.Combine("config", "combat_effects.json")));
            config.Validate();
            check.True("gore config validates (all firearms, dismemberment, decapitation, emitters)", config.GoreConfigured && config.AllFirearms &&
                config.DismembermentEnabled && config.DecapitationEnabled && config.EffectScale > 0, "");
            check.True("stock blood visuals are the portable default", config.StockBloodVisuals, config.BloodVisualMode);
            config.BloodVisualMode = "external";
            config.Validate();
            check.True("external blood mode has bounded leak and throw settings", !config.StockBloodVisuals && config.DismembermentEnabled &&
                config.ExternalMaximumBleedEmitters <= config.MaximumEmitters && config.LimbThrowMaximumAttempts > 1, "");
            config.BloodVisualMode = "unknown";
            bool badModeRejected = false;
            try { config.Validate(); } catch (InvalidDataException) { badModeRejected = true; }
            check.True("unknown blood visual mode is rejected", badModeRejected, "");
            config.BloodVisualMode = "stock";

            string game = Path.GetDirectoryName(Path.GetFullPath(exePath));
            string core = Path.Combine(game, @"update\pc\data\effects\gta_core.wpfl");
            if (!File.Exists(core)) { core = Path.Combine(game, @"pc\data\effects\gta_core.wpfl"); }
            string text = Encoding.ASCII.GetString(RscResource.Parse(File.ReadAllBytes(core)).Body);
            foreach (string name in new[] { config.ImpactEffectName, config.ExitEffectName, config.ShotgunEntryEffectName, config.ShotgunChunksEffectName,
                config.SniperEntryEffectName, config.SniperChunksEffectName, config.HeavyChunksEffectName, config.BleedEffectName, config.ArterialEffectName,
                config.SeverBurstEffectName, config.SeverMistEffectName, config.MouthBloodEffectName, config.MistEffectName, config.DeathEffectName, config.WoundSpurtEffectName, config.DeathLeakEffectName,
                config.ExternalBleedEffectName, config.ExternalStumpBurstEffectName, config.ExternalLimbLandingEffectName })
            {
                check.True("particle effect exists in gta_core.wpfl: " + name, !string.IsNullOrEmpty(name) && text.Contains(name + "\0"), core);
            }
        }
    }
}
