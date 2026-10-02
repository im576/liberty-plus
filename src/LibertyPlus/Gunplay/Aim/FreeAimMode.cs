using System;
using System.IO;
using GTA;
using LibertyFramework.Core.Config;
using LibertyFramework.Core.Logging;
using LibertyFramework.GameApi;
using LibertyFramework.Gunplay.Profiles;

namespace LibertyFramework.Gunplay.Aim
{
    // Universal free aim while Liberty Framework is active:
    //  - PREF_AUTO_AIM forced off (the pause-menu "Auto-Aim" option; controls target snapping/tracking),
    //  - DISABLE_PLAYER_LOCKON(player, true) every frame (no hard lock-on; missions may reset it),
    //  - hud.dat target health/armour ring hidden.
    // The player's prior values are saved to disk first and restored on disable, unload, or next start.
    internal sealed class FreeAimMode
    {
        private readonly GamePrefs prefs;
        private readonly PlayerMemory playerMemory;
        private readonly HudReticle hud;
        private bool lockOnPriorKnown;
        private bool appliedLockOn;
        private bool appliedAutoAim;

        internal FreeAimMode(GamePrefs prefs, PlayerMemory playerMemory, HudReticle hud)
        {
            this.prefs = prefs;
            this.playerMemory = playerMemory;
            this.hud = hud;
        }

        internal bool Active { get; private set; }
        internal int AutoAimPrior { get; private set; }
        internal bool LockOnPrior { get; private set; }

        // Crash recovery: a leftover state file means the last session never restored.
        internal void RecoverFromPreviousSession()
        {
            FreeAimRestoreState state = LoadState();
            if (state == null) { return; }
            if (prefs != null)
            {
                prefs.AutoAim = state.AutoAimPreference;
                RuntimeLog.Info("freeaim_recovered auto_aim_restored=" + state.AutoAimPreference + " saved_utc=" + state.SavedUtc);
            }
            DeleteState();
        }

        internal void Enable(int playerIndex)
        {
            if (Active) { return; }
            AutoAimPrior = prefs != null ? prefs.AutoAim : 1;
            bool? lockOn = playerMemory != null ? playerMemory.LockOnDisabled(playerIndex) : null;
            lockOnPriorKnown = lockOn.HasValue;
            LockOnPrior = lockOn.HasValue && lockOn.Value;
            SaveState();
            Active = true;
            RuntimeLog.Info("freeaim_enabled prior_auto_aim=" + AutoAimPrior + " prior_lockon_disabled=" +
                (lockOnPriorKnown ? LockOnPrior.ToString() : "unknown"));
        }

        internal void Update(Player player, int playerIndex, FreeAimSettings settings)
        {
            if (!Active) { return; }
            if (!lockOnPriorKnown && playerMemory != null)
            {
                bool? lockOn = playerMemory.LockOnDisabled(playerIndex);
                if (lockOn.HasValue && !appliedLockOn)
                {
                    lockOnPriorKnown = true;
                    LockOnPrior = lockOn.Value;
                    SaveState();
                }
            }
            if (settings.ForceAutoAimOff && prefs != null)
            {
                if (prefs.AutoAim != 0) { prefs.AutoAim = 0; }
                appliedAutoAim = true;
            }
            else if (appliedAutoAim && prefs != null)
            {
                prefs.AutoAim = AutoAimPrior;
                appliedAutoAim = false;
            }
            if (player != null)
            {
                if (settings.DisableLockOn)
                {
                    Natives.DisablePlayerLockOn(player, true);
                    appliedLockOn = true;
                }
                else if (appliedLockOn)
                {
                    Natives.DisablePlayerLockOn(player, LockOnPrior);
                    appliedLockOn = false;
                }
            }
            if (hud != null)
            {
                if (settings.HideTargetHealth)
                {
                    hud.Hide(HudReticle.HealthTarget);
                    hud.Hide(HudReticle.ArmourTarget);
                }
                else
                {
                    hud.Restore(HudReticle.HealthTarget);
                    hud.Restore(HudReticle.ArmourTarget);
                }
            }
        }

        // Memory state (pref, HUD, restore file) is put back before the lock-on native, so a native failure
        // cannot leave the saved profile altered. 'player' null (process exit) skips the native: the lock-on
        // flag lives on the player ped and does not outlive the session.
        internal void Disable(Player player)
        {
            if (!Active) { return; }
            Active = false;
            if (prefs != null && appliedAutoAim) { prefs.AutoAim = AutoAimPrior; }
            appliedAutoAim = false;
            if (hud != null)
            {
                hud.Restore(HudReticle.HealthTarget);
                hud.Restore(HudReticle.ArmourTarget);
            }
            DeleteState();
            bool restoreLockOn = appliedLockOn && player != null;
            appliedLockOn = false;
            RuntimeLog.Info("freeaim_disabled restored_auto_aim=" + AutoAimPrior + " lockon_disabled_prior=" + LockOnPrior +
                (restoreLockOn ? " restoring_lockon" : " lockon_native_skipped"));
            if (restoreLockOn) { Natives.DisablePlayerLockOn(player, LockOnPrior); }
        }

        private void SaveState()
        {
            try
            {
                FreeAimRestoreState state = new FreeAimRestoreState();
                state.SchemaVersion = 1;
                state.AutoAimPreference = AutoAimPrior;
                state.LockOnDisabledBefore = LockOnPrior;
                state.SavedUtc = DateTime.UtcNow.ToString("o");
                JsonStore.Save(LibertyPaths.FreeAimRestoreState, state);
            }
            catch (Exception error)
            {
                RuntimeLog.Error("freeaim_state_save_failed error=" + error.Message);
            }
        }

        private static FreeAimRestoreState LoadState()
        {
            try
            {
                if (!File.Exists(LibertyPaths.FreeAimRestoreState)) { return null; }
                FreeAimRestoreState state = JsonStore.Load<FreeAimRestoreState>(LibertyPaths.FreeAimRestoreState);
                return state.SchemaVersion == 1 ? state : null;
            }
            catch (Exception error)
            {
                RuntimeLog.Error("freeaim_state_load_failed error=" + error.Message);
                return null;
            }
        }

        private static void DeleteState()
        {
            try
            {
                if (File.Exists(LibertyPaths.FreeAimRestoreState)) { File.Delete(LibertyPaths.FreeAimRestoreState); }
                string backup = LibertyPaths.FreeAimRestoreState + ".bak";
                if (File.Exists(backup)) { File.Delete(backup); }
            }
            catch (Exception error)
            {
                RuntimeLog.Error("freeaim_state_delete_failed error=" + error.Message);
            }
        }
    }
}
