using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Liberty.Sdk;
using LibertyFramework.Core.Config;
using LibertyFramework.Core.Logging;
using LibertyFramework.Core.Memory;
using LibertyFramework.Core.Performance.Logic;
using LibertyFramework.GameApi;
using LibertyFramework.Hud.Logic;

namespace LibertyFramework.Hud
{
    // T-049 Basic Liberty HUD (config/hud.json, design STAGE1 section 7 and ART-007 r2). Health, armour, current weapon with
    // ammunition, wanted stars and interaction prompts drawn by Liberty.Ui, each shown when something makes it relevant and faded
    // out when not. The weapon, ammo, health and armour form one compact group at the TOP RIGHT; the vanilla radar stays
    // bottom left; the prompt is the IV-style help box (top left) with the button names of the device in use.
    //
    // hud.dat globals resolve these elements, but the September 30 live captures show they do not hide weapon/ammo/wanted.
    // HudPlan therefore retains vanilla until visible hiding is verified; table membership and saved-value ownership are not proof.
    // Diagnostic layout-test and an explicit drawWithoutHidingVanilla override still allow layout inspection. Turning the
    // module off or `"enabled": false` restores every hidden component.
    //
    // Threading: the engine tick gathers state and builds an immutable Frame; OnDraw only draws that Frame (no game calls, no
    // Game.Resolution), as the draw-pass rules require.
    [global::Liberty.Sdk.Module("hud", Order = 60, Capabilities = new[] { global::Liberty.Sdk.Capabilities.EngineInternal },
        Description = "Basic Liberty HUD: contextual weapon, ammo, health, armour, wanted and prompts (T-049)")]
    public sealed class HudModule : LibertyFramework.Engine.Module
    {
        private const int ConfigPollMilliseconds = 1000;
        private const int RightMouseButton = 0x02;
        private static readonly int[] DeviceKeys =
        {
            0x01, 0x02, 0x04, // mouse fire, aim, middle button also switch glyphs back to keyboard/mouse
            0x57, 0x41, 0x53, 0x44, // W A S D
            0x45, 0x46, 0x52, 0x20, // E F R Space
            0x10, 0x11, 0x09, 0x0D, // Shift Control Tab Enter
            0x25, 0x26, 0x27, 0x28  // arrows
        };

        // What the draw pass reads; rebuilt every tick and replaced whole.
        private sealed class Frame
        {
            internal HudConfig Config;
            internal double WeaponAlpha, HealthAlpha, ArmourAlpha, WantedAlpha, PromptAlpha;
            internal bool DrawWeapon, DrawHealth, DrawArmour, DrawWanted, DrawPrompt;
            internal TextureRef Icon, Star;
            internal string Ammo = "";
            internal bool ClipLow;
            internal double HealthFill, ArmourFill, HealthPulse;
            internal bool HealthLow;
            internal int Stars;
            internal string Prompt = "";
        }

        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly HudInputDevice device = new HudInputDevice();
        private readonly HudPresence weaponPresence = new HudPresence();
        private readonly HudPresence healthPresence = new HudPresence();
        private readonly HudPresence armourPresence = new HudPresence();
        private readonly HudPresence wantedPresence = new HudPresence();
        private readonly HudPresence promptPresence = new HudPresence();
        private readonly HudPromptText promptExpansion = new HudPromptText();
        private readonly HudAmmoSample ammoSample = new HudAmmoSample();
        private readonly HudNativeDisplayProbe nativeDisplay = new HudNativeDisplayProbe();
        private readonly HashSet<string> probeHidden = new HashSet<string>();
        private readonly HashSet<string> planHidden = new HashSet<string>();
        private readonly Dictionary<string, string> forced = new Dictionary<string, string>();
        private readonly Dictionary<string, bool> shownLogged = new Dictionary<string, bool>();
        private readonly Dictionary<string, string> lastReason = new Dictionary<string, string>();

        private volatile Frame frame;
        private HudConfig config;
        private string configHash;
        private long lastConfigCheckMilliseconds = -ConfigPollMilliseconds;
        private HudReticle hider;
        private GameAddresses addresses;
        private bool disabled;
        private bool planVerified;
        private HudDecision weaponPlan, healthPlan, armourPlan, wantedPlan;
        private TextureRef star = TextureRef.None;

        private double lastSeconds;
        private int lastWeapon = -1;
        private int lastHealth = -1, lastArmour = -1;
        private int clipMaximumSeen;
        private int wantedLevel;
        // "Not read yet": far enough in the past that the first poll is due, small enough that the subtraction cannot overflow.
        private const long Never = -1000000;
        private long lastWantedPollMilliseconds = Never, lastDevicePollMilliseconds = Never;
        private bool lastReloading, lastHealthLow;
        // The ammo line is rebuilt only when its numbers change.
        private int ammoShownClip = int.MinValue, ammoShownTotal = int.MinValue;
        private bool ammoShownIncludesClip;
        private string ammoShownText = "";
        // Test hook (`hudctl layout-test on`): draw every element even where its vanilla counterpart stays, to see the Liberty layout.
        private bool layoutTest;
        // Diagnostic probe suspends replacement so each screenshot starts from vanilla.
        private bool probeMode;
        private byte[] configTestOriginal;
        private int? cashTestOriginal;
        private PedRef baselineTaskPed = PedRef.None;
        private string baselineTask = "none";
        private long baselineTaskExpiresMilliseconds;
        private bool aiming, padActive, keyboardActive;
        private string deviceOverride;
        private string promptText = "";
        private string promptLogged = "";

        protected internal override void OnStart()
        {
            Interval = 0;
            try
            {
                LoadConfig(true);
                star = Engine.Ui.LoadTexture(HudArt.StarPng(), "hud:star");
                ResolveTable();
                SubscribeEvents();
                RegisterCommands();
                ApplyPlan();
                lastSeconds = clock.Elapsed.TotalSeconds;
                RuntimeLog.Info("hud_started enabled=" + config.Enabled + " hide_vanilla=" + config.HideVanilla + " table=" + (hider != null) +
                    " weapon=" + weaponPlan.Mode + " health=" + healthPlan.Mode + " armour=" + armourPlan.Mode + " wanted=" + wantedPlan.Mode);
            }
            catch (Exception error) { Fail("start", error); }
        }

        protected internal override void OnStop() { Release("stopped"); }

        protected internal override void OnUnload() { Release("unload"); }

        // Everything the module changed in the game goes back: the vanilla components, the help box.
        private void Release(string reason)
        {
            frame = null;
            nativeDisplay.Cancel();
            try { UpdateNativeDisplay(); }
            catch (Exception error) { RuntimeLog.Error("hud_native_display_restore_failed error=" + error.Message); }
            try { RestoreConfigTest(); }
            catch (Exception error) { RuntimeLog.Error("hud_config_test_restore_failed error=" + error.Message); }
            try { RestoreCashTest(); }
            catch (Exception error) { RuntimeLog.Error("hud_cash_test_restore_failed error=" + error.Message); }
            try { ReleaseBaselineTask(); }
            catch (Exception error) { RuntimeLog.Error("hud_baseline_task_restore_failed error=" + error.Message); }
            try { if (hider != null) { hider.RestoreAll(); } }
            catch (Exception error) { RuntimeLog.Error("hud_restore_failed error=" + error.Message); }
            planHidden.Clear();
            probeHidden.Clear();
            try { Engine.Ui.HelpDrawnByHud = false; } catch (Exception error) { RuntimeLog.Error("hud_release_ui_failed error=" + error.Message); }
            RuntimeLog.Info("hud_released reason=" + reason);
        }

        private void Fail(string where, Exception error)
        {
            RuntimeLog.Error("feature_disabled hud at=" + where + " error=" + error);
            disabled = true;
            Release("error");
        }

        // --- configuration ----------------------------------------------------------------------------------------------------

        private void LoadConfig(bool force)
        {
            long now = clock.ElapsedMilliseconds;
            if (!force && now - lastConfigCheckMilliseconds < ConfigPollMilliseconds) { return; }
            lastConfigCheckMilliseconds = now;
            try
            {
                string path = LibertyPaths.HudConfig;
                if (!System.IO.File.Exists(path))
                {
                    if (config == null) { config = HudConfig.Defaults(); configHash = null; RuntimeLog.Info("hud_config_missing using defaults"); }
                    return;
                }
                byte[] bytes = JsonStore.ReadBytes(path);
                string hash = JsonStore.Hash(bytes);
                if (hash == configHash) { return; }
                HudConfig candidate = JsonStore.Parse<HudConfig>(bytes);
                candidate.Validate();
                bool first = config == null;
                config = candidate;
                configHash = hash;
                RuntimeLog.Info("hud_config_loaded enabled=" + config.Enabled + " hide_vanilla=" + config.HideVanilla);
                if (!first) { ApplyPlan(); }
            }
            catch (Exception error)
            {
                // A broken file keeps the last good configuration (or the defaults at start).
                RuntimeLog.Error("hud_config_rejected error=" + error.Message);
                if (config == null) { config = HudConfig.Defaults(); }
            }
        }

        // --- the hud.dat table ------------------------------------------------------------------------------------------------

        private void ResolveTable()
        {
            LibertyFramework.Engine.EngineMemory shared = Engine.Memory;
            if (!shared.Resolve()) { RuntimeLog.Error("hud_table_unavailable engine memory unresolved; the vanilla HUD stays"); return; }
            addresses = shared.Addresses;
            if (!addresses.HudTableResolved)
            {
                RuntimeLog.Error("hud_table_unavailable components=" + addresses.HudComponents.Count + " register=0x" + addresses.HudRegisterFunction.ToString("X8") +
                    "; the vanilla HUD stays");
                return;
            }
            hider = new HudReticle(shared.Live, addresses.HudComponents);
            StringBuilder names = new StringBuilder();
            foreach (GameAddresses.HudComponentGlobals component in addresses.HudComponents) { names.Append(names.Length > 0 ? "," : "").Append(component.Name); }
            RuntimeLog.Info("hud_table components=" + addresses.HudComponents.Count + " unparsed=" + addresses.HudUnparsed.Count + " names=" + names);
        }

        // Which elements Liberty draws, given the config and what the table can hide. Runs at start and on every config change.
        private void ApplyPlan()
        {
            Func<string, bool> isResolved = name => hider != null && hider.Knows(name) && !IsReticleComponent(name);
            // No T-049 replacement component has verified visible hiding. Keep probes available, without promoting their ledger to capability.
            Func<string, bool> isHidingVerified = name => false;
            string hiding = probeMode ? HudConfig.HideNone : config.HideVanilla;
            weaponPlan = HudPlan.Decide(config.Weapon.Enabled, hiding, config.Weapon.VanillaComponents, !probeMode && (config.Weapon.DrawWithoutHidingVanilla || layoutTest), isResolved, isHidingVerified);
            healthPlan = HudPlan.Decide(config.Health.Enabled, hiding, config.Health.VanillaComponents, !probeMode && (config.Health.DrawWithoutHidingVanilla || layoutTest), isResolved, isHidingVerified);
            armourPlan = HudPlan.Decide(config.Armour.Enabled, hiding, config.Armour.VanillaComponents, !probeMode && (config.Armour.DrawWithoutHidingVanilla || layoutTest), isResolved, isHidingVerified);
            wantedPlan = HudPlan.Decide(config.Wanted.Enabled, hiding, config.Wanted.VanillaComponents, !probeMode && (config.Wanted.DrawWithoutHidingVanilla || layoutTest), isResolved, isHidingVerified);
            planVerified = false;
            LogPlan("weapon", weaponPlan);
            LogPlan("health", healthPlan);
            LogPlan("armour", armourPlan);
            LogPlan("wanted", wantedPlan);
            // The prompt restyle needs no hiding: the help box is ours.
            try { Engine.Ui.HelpDrawnByHud = !probeMode && config.Enabled && config.Prompt.Enabled; } catch (Exception error) { RuntimeLog.Error("hud_help_hook_failed error=" + error.Message); }
            ReleaseUnplanned();
        }

        // The four reticle components belong to the gunplay module (it hides and restores them); the HUD never touches them.
        private static bool IsReticleComponent(string name)
        {
            return name == HudReticle.Crosshair || name == HudReticle.HealthTarget || name == HudReticle.ArmourTarget || name == HudReticle.Dot;
        }

        private static string ModeText(HudElementMode mode)
        {
            return mode == HudElementMode.Liberty ? "liberty" : mode == HudElementMode.VanillaKept ? "vanilla_kept" : "off";
        }

        private void LogPlan(string name, HudDecision plan)
        {
            RuntimeLog.Info("hud_element_plan element=" + name + " mode=" + ModeText(plan.Mode) + " reason=\"" + plan.Reason + "\"");
        }

        private IEnumerable<string> PlannedNames()
        {
            if (!config.Enabled) { yield break; }
            foreach (HudDecision plan in new[] { weaponPlan, healthPlan, armourPlan, wantedPlan })
            {
                if (plan.Mode != HudElementMode.Liberty) { continue; }
                foreach (string name in plan.ToHide) { yield return name; }
            }
        }

        private void ReleaseUnplanned()
        {
            if (hider == null) { return; }
            HashSet<string> wanted = new HashSet<string>(PlannedNames());
            foreach (string name in new List<string>(planHidden))
            {
                if (wanted.Contains(name) || probeHidden.Contains(name)) { continue; }
                hider.Restore(name);
                planHidden.Remove(name);
            }
        }

        // Hide (and keep hidden: the call is repeated each frame like the reticle's) every planned component. A component that could
        // not be hidden demotes its element back to vanilla, so nothing is ever duplicated.
        private void HideVanilla()
        {
            if (hider == null) { return; }
            HidePlanned(weaponPlan);
            HidePlanned(healthPlan);
            HidePlanned(armourPlan);
            HidePlanned(wantedPlan);
            if (planVerified) { return; }
            planVerified = true;
            Verify("weapon", ref weaponPlan, config.Weapon.DrawWithoutHidingVanilla);
            Verify("health", ref healthPlan, config.Health.DrawWithoutHidingVanilla);
            Verify("armour", ref armourPlan, config.Armour.DrawWithoutHidingVanilla);
            Verify("wanted", ref wantedPlan, config.Wanted.DrawWithoutHidingVanilla);
            ReleaseUnplanned();
        }

        private void HidePlanned(HudDecision plan)
        {
            if (plan.Mode != HudElementMode.Liberty) { return; }
            List<string> names = plan.ToHide;
            for (int index = 0; index < names.Count; index++)
            {
                hider.Hide(names[index]);
                planHidden.Add(names[index]);
            }
        }

        private void Verify(string element, ref HudDecision plan, bool drawAnyway)
        {
            if (plan.Mode != HudElementMode.Liberty || plan.ToHide.Count == 0 || drawAnyway || layoutTest) { return; }
            foreach (string name in plan.ToHide)
            {
                if (hider.IsHidden(name)) { continue; }
                HudDecision kept = new HudDecision();
                kept.Mode = HudElementMode.VanillaKept;
                kept.Reason = name + " could not be hidden (its saved values were unreadable)";
                plan = kept;
                LogPlan(element, plan);
                return;
            }
        }

        // --- events -----------------------------------------------------------------------------------------------------------

        private void SubscribeEvents()
        {
            Engine.Events.Subscribe<global::Liberty.Sdk.Events.PlayerWeaponChanged>(this, e => TriggerWeapon("change", config.Weapon.ShowOnChange));
            Engine.Events.Subscribe<global::Liberty.Sdk.Events.PlayerShot>(this, e => { TriggerWeapon("shot", config.Weapon.ShowOnShot); TriggerCombat(); });
            Engine.Events.Subscribe<global::Liberty.Sdk.Events.PlayerDamaged>(this, e => { TriggerBars("damage"); TriggerCombat(); });
            Engine.Events.Subscribe<global::Liberty.Sdk.Events.PlayerDied>(this, e => ResetAll());
        }

        private void TriggerWeapon(string reason, bool enabled)
        {
            if (!enabled || config == null) { return; }
            weaponPresence.Trigger(clock.Elapsed.TotalSeconds, config.Weapon.HoldSeconds);
            lastReason["weapon"] = reason;
        }

        private void TriggerBars(string reason)
        {
            double now = clock.Elapsed.TotalSeconds;
            if (config.Health.ShowOnChange) { healthPresence.Trigger(now, config.Health.HoldSeconds); lastReason["health"] = reason; }
            if (config.Armour.ShowOnChange) { armourPresence.Trigger(now, config.Armour.HoldSeconds); lastReason["armour"] = reason; }
        }

        private void TriggerCombat()
        {
            double now = clock.Elapsed.TotalSeconds;
            if (config.Health.CombatHoldSeconds > 0) { healthPresence.Trigger(now, config.Health.CombatHoldSeconds); lastReason["health"] = "combat"; }
            if (config.Armour.CombatHoldSeconds > 0) { armourPresence.Trigger(now, config.Armour.CombatHoldSeconds); lastReason["armour"] = "combat"; }
        }

        private void ResetAll()
        {
            weaponPresence.Reset(); healthPresence.Reset(); armourPresence.Reset(); wantedPresence.Reset(); promptPresence.Reset();
        }

        // --- the tick ---------------------------------------------------------------------------------------------------------

        protected internal override void OnUpdate()
        {
            long started = Stopwatch.GetTimestamp();
            try
            {
                if (disabled) { return; }
                LoadConfig(false);
                UpdateNativeDisplay();
                UpdateBaselineTask();
                if (config == null || !config.Enabled)
                {
                    // Switched off: the vanilla HUD comes back whole (RestoreAll also covers a config reload while hidden).
                    if (hider != null) { hider.RestoreAll(); planHidden.Clear(); probeHidden.Clear(); }
                    Engine.Ui.HelpDrawnByHud = false;
                    frame = null;
                    return;
                }
                Engine.Ui.HelpDrawnByHud = !probeMode && config.Prompt.Enabled;
                HideVanilla();
                foreach (string name in probeHidden) { hider.Hide(name); }
                if (probeMode) { frame = null; return; }
                Step();
            }
            catch (Exception error) { Fail("tick", error); }
            finally { CostMeter.Add("tick.hud", started); }
        }

        private void UpdateNativeDisplay()
        {
            bool safe = false;
            if (nativeDisplay.Requested && Liberty.World.HasPlayer)
            {
                PlayerState player = Liberty.World.Player;
                WorldInfo info = Liberty.World.Info;
                safe = probeMode && !layoutTest && !Engine.Ui.HudHiddenByOtherOwner(this) && player.IsPlaying && !player.IsDead && player.Health > 0 &&
                    !info.Paused && !info.CutscenePlaying && !info.FadedOut;
            }
            bool wasApplied = nativeDisplay.Applied;
            int action = nativeDisplay.Update(clock.ElapsedMilliseconds, !disabled && config != null && config.Enabled, safe);
            if (action > 0)
            {
                // Use the existing owner ledger for unload/failure restoration. The explicit pair is held every tick for this experiment.
                Engine.Ui.SetHudVisible(this, false);
                GTA.Native.Function.Call("DISPLAY_HUD", false);
                GTA.Native.Function.Call("DISPLAY_RADAR", true);
                if (!wasApplied) { RuntimeLog.Info("hud_native_display applied hud=False radar=True visual=unproven"); }
            }
            else if (action < 0)
            {
                Engine.Ui.SetHudVisible(this, true);
                if (Engine.Ui.HudHiddenByOtherOwner(this))
                {
                    GTA.Native.Function.Call("DISPLAY_HUD", false);
                    GTA.Native.Function.Call("DISPLAY_RADAR", false);
                }
                RuntimeLog.Info("hud_native_display released owner (vanilla returns when no other owner hides it)");
            }
        }

        private void Step()
        {
            double now = clock.Elapsed.TotalSeconds;
            double delta = Math.Min(0.25, Math.Max(0.0, now - lastSeconds));
            lastSeconds = now;
            long nowMs = clock.ElapsedMilliseconds;
            if (!Liberty.World.HasPlayer) { frame = null; return; }
            PlayerState player = Liberty.World.Player;
            WorldInfo info = Liberty.World.Info;
            bool suppressed = !player.IsPlaying || (config.Suppress.InCutscenes && info.CutscenePlaying) || (config.Suppress.WhenPaused && info.Paused) ||
                (config.Suppress.WhenFadedOut && info.FadedOut) || (config.Suppress.WhenDead && (player.IsDead || player.Health <= 0));

            SampleInput(nowMs, player);
            bool armed = player.Weapon > 0;
            if (player.Weapon != lastWeapon)
            {
                if (lastWeapon != -1) { TriggerWeapon("change", config.Weapon.ShowOnChange); }
                lastWeapon = player.Weapon;
                clipMaximumSeen = 0;
                ammoSample.Reset();
            }
            if (player.AmmoInClip > clipMaximumSeen) { clipMaximumSeen = player.AmmoInClip; }
            if (player.IsReloading != lastReloading)
            {
                lastReloading = player.IsReloading;
                RuntimeLog.Info("hud_reload reloading=" + (lastReloading ? 1 : 0) + " weapon=" + player.Weapon + " clip=" + player.AmmoInClip);
            }
            if (armed && ammoSample.NeedsRefresh(player.Weapon, player.AmmoInClip, nowMs, config.Sampling.AmmoPollMilliseconds))
            {
                // Existing SDK reads, together on the tick thread. Render/log only this pair, never a fresh snapshot clip plus cached total.
                int total = Liberty.Weapons.GetAmmo(player.Ped, player.Weapon);
                int clip = Liberty.Weapons.GetAmmoInClip(player.Ped, player.Weapon);
                ammoSample.Capture(player.Weapon, player.AmmoInClip, clip, total, nowMs);
            }
            if (nowMs - lastWantedPollMilliseconds >= config.Sampling.WantedPollMilliseconds)
            {
                lastWantedPollMilliseconds = nowMs;
                wantedLevel = Liberty.Player.WantedLevel;
            }
            if (lastHealth != -1 && player.Health != lastHealth) { TriggerBars("health"); }
            else if (lastArmour != -1 && player.Armour != lastArmour) { TriggerBars("armour"); }
            lastHealth = player.Health;
            lastArmour = player.Armour;

            // Weapon group: up while reloading or aiming (when configured), always when so configured, else for the hold time
            // after a change or shot. Nothing is drawn unarmed.
            bool weaponCondition = armed && (config.Weapon.AlwaysWhenArmed || (config.Weapon.ShowWhileReloading && player.IsReloading) ||
                (config.Weapon.ShowWhileAiming && aiming));
            if (player.IsReloading) { lastReason["weapon"] = "reload"; } else if (aiming && weaponCondition) { lastReason["weapon"] = "aim"; }
            if (!armed) { weaponPresence.Reset(); }
            bool forcedWeapon = Forced("weapon");
            weaponPresence.Update(now, delta, weaponCondition || forcedWeapon, config.Weapon.FadeInSeconds, config.Weapon.FadeOutSeconds);

            double healthFill = HudText.Fill(player.Health, config.Health.Maximum);
            bool healthLow = config.Health.LowFraction > 0 && healthFill <= config.Health.LowFraction && player.Health > 0;
            if (healthLow) { lastReason["health"] = "low"; }
            if (healthLow != lastHealthLow)
            {
                lastHealthLow = healthLow;
                RuntimeLog.Info("hud_health_low low=" + (healthLow ? 1 : 0) + " health=" + player.Health + " fill=" + healthFill.ToString("0.00", CultureInfo.InvariantCulture));
            }
            healthPresence.Update(now, delta, healthLow || Forced("health"), config.Health.FadeInSeconds, config.Health.FadeOutSeconds);
            armourPresence.Update(now, delta, Forced("armour"), config.Armour.FadeInSeconds, config.Armour.FadeOutSeconds);
            wantedPresence.Update(now, delta, wantedLevel > 0 || Forced("wanted"), config.Wanted.FadeInSeconds, config.Wanted.FadeOutSeconds);
            if (wantedLevel > 0) { wantedPresence.Trigger(now, config.Wanted.HoldSeconds); }

            string help = Engine.Ui.CurrentHelp();
            List<string> unknown;
            if (promptExpansion.Update(help, config.Prompt.Glyphs, device.Current, out unknown))
            {
                promptText = promptExpansion.Text;
                if (unknown != null) { RuntimeLog.Info("hud_prompt_unknown_glyph tokens=" + string.Join(",", unknown.ToArray())); }
            }
            promptPresence.Update(now, delta, (help != null && config.Prompt.Enabled) || Forced("prompt"), config.Prompt.FadeInSeconds, config.Prompt.FadeOutSeconds);
            if (help != null && promptLogged != promptText)
            {
                promptLogged = promptText;
                RuntimeLog.Info("hud_prompt device=" + device.Current.ToString().ToLowerInvariant() + " text=\"" + promptText + "\"");
            }
            if (help == null) { promptLogged = ""; }

            Frame next = new Frame();
            next.Config = config;
            next.Star = star;
            bool libertyWeapon = weaponPlan.Mode == HudElementMode.Liberty;
            bool libertyHealth = healthPlan.Mode == HudElementMode.Liberty;
            bool libertyArmour = armourPlan.Mode == HudElementMode.Liberty;
            bool libertyWanted = wantedPlan.Mode == HudElementMode.Liberty;
            next.WeaponAlpha = weaponPresence.Alpha;
            next.DrawWeapon = !suppressed && libertyWeapon && armed && weaponPresence.Visible;
            if (next.DrawWeapon)
            {
                next.Icon = Engine.Ui.WeaponIcon(player.Weapon);
                if (ammoSample.Clip != ammoShownClip || ammoSample.Total != ammoShownTotal || ammoShownIncludesClip != config.Weapon.TotalIncludesClip)
                {
                    ammoShownClip = ammoSample.Clip;
                    ammoShownTotal = ammoSample.Total;
                    ammoShownIncludesClip = config.Weapon.TotalIncludesClip;
                    ammoShownText = HudText.FormatAmmo(ammoShownClip, ammoShownTotal, ammoShownIncludesClip);
                }
                next.Ammo = ammoShownText;
                next.ClipLow = HudText.IsClipLow(ammoSample.Clip, clipMaximumSeen, config.Weapon.LowClipFraction);
            }
            next.HealthAlpha = healthPresence.Alpha;
            next.HealthFill = healthFill;
            next.HealthLow = healthLow;
            next.HealthPulse = healthLow ? HudText.Pulse(now, config.Health.PulsePerSecond, config.Health.PulseDepth) : 1.0;
            next.DrawHealth = !suppressed && libertyHealth && healthPresence.Visible;
            next.ArmourAlpha = armourPresence.Alpha;
            next.ArmourFill = HudText.Fill(player.Armour, config.Armour.Maximum);
            next.DrawArmour = !suppressed && libertyArmour && armourPresence.Visible && (player.Armour > 0 || !config.Armour.HideWhenZero);
            next.WantedAlpha = wantedPresence.Alpha;
            next.Stars = Math.Min(wantedLevel, config.Wanted.MaximumStars);
            next.DrawWanted = !suppressed && libertyWanted && wantedPresence.Visible;
            next.PromptAlpha = promptPresence.Alpha;
            next.DrawPrompt = !suppressed && config.Prompt.Enabled && promptPresence.Visible && promptText.Length > 0;
            next.Prompt = promptText;
            frame = next;
            LogTransitions(next);
        }

        // One log line when an element starts or stops being drawn: the scenario and playtest reports read these. The detail text is
        // built only on a change, so a steady frame allocates nothing here.
        private void LogTransitions(Frame next)
        {
            if (Changed("weapon", next.DrawWeapon)) { Logged("weapon", next.DrawWeapon, "clip/total=" + ammoSample.Clip + "/" + ammoSample.Total + " ammo=\"" + next.Ammo + "\""); }
            if (Changed("health", next.DrawHealth)) { Logged("health", next.DrawHealth, "fill=" + next.HealthFill.ToString("0.00", CultureInfo.InvariantCulture) + " low=" + next.HealthLow); }
            if (Changed("armour", next.DrawArmour)) { Logged("armour", next.DrawArmour, "fill=" + next.ArmourFill.ToString("0.00", CultureInfo.InvariantCulture)); }
            if (Changed("wanted", next.DrawWanted)) { Logged("wanted", next.DrawWanted, "stars=" + next.Stars); }
            if (Changed("prompt", next.DrawPrompt)) { Logged("prompt", next.DrawPrompt, ""); }
        }

        private bool Changed(string element, bool drawn)
        {
            bool was;
            shownLogged.TryGetValue(element, out was);
            if (drawn == was) { return false; }
            shownLogged[element] = drawn;
            return true;
        }

        private void Logged(string element, bool drawn, string detail)
        {
            string reason;
            lastReason.TryGetValue(element, out reason);
            RuntimeLog.Info("hud_element " + element + (drawn ? " shown" : " hidden") + (drawn ? " reason=" + (reason ?? "condition") + " " + detail : ""));
        }

        private bool Forced(string element)
        {
            string mode;
            if (forced.TryGetValue("all", out mode) && mode == "on") { return true; }
            return forced.TryGetValue(element, out mode) && mode == "on";
        }

        private void SampleInput(long nowMs, PlayerState player)
        {
            if (nowMs - lastDevicePollMilliseconds < config.Sampling.InputPollMilliseconds) { return; }
            lastDevicePollMilliseconds = nowMs;
            IInput input = Liberty.Input;
            bool pad = false;
            if (input.PadConnected)
            {
                pad = input.Down(PadButton.A) || input.Down(PadButton.B) || input.Down(PadButton.X) || input.Down(PadButton.Y) ||
                    input.Down(PadButton.LeftShoulder) || input.Down(PadButton.RightShoulder) || input.Down(PadButton.DPadUp) || input.Down(PadButton.DPadDown) ||
                    Math.Abs(input.LeftX) > 0.4f || Math.Abs(input.LeftY) > 0.4f || Math.Abs(input.RightX) > 0.4f || Math.Abs(input.RightY) > 0.4f ||
                    input.LeftTrigger > 0.1f || input.RightTrigger > 0.1f;
            }
            bool keyboard = false;
            foreach (int code in DeviceKeys) { if (input.KeyDown((VirtualKey)code)) { keyboard = true; break; } }
            padActive = pad;
            keyboardActive = keyboard;
            aiming = input.LeftTrigger >= config.Weapon.AimTriggerLevel || input.KeyDown((VirtualKey)RightMouseButton);
            string setting = deviceOverride ?? config.Prompt.Device;
            device.Update(setting, padActive, keyboardActive, (int)nowMs, config.Prompt.DeviceSwitchMilliseconds);
        }

        // --- drawing ----------------------------------------------------------------------------------------------------------

        protected internal override void OnDraw(ICanvas canvas)
        {
            Frame drawn = frame;
            if (drawn == null) { return; }
            long started = Stopwatch.GetTimestamp();
            HudConfig c = drawn.Config;
            HudLayoutSettings layout = c.Layout;
            HudPalette palette = c.Palette;
            float shadow = layout.ShadowOffset;
            try
            {
                if (drawn.DrawWeapon)
                {
                    canvas.Opacity = (float)drawn.WeaponAlpha;
                    HudRect icon = HudLayout.Icon(layout, canvas.Width);
                    if (!drawn.Icon.IsNone)
                    {
                        if (shadow > 0) { canvas.Sprite(drawn.Icon, icon.X + shadow, icon.Y + shadow, icon.Width, icon.Height, Colour(palette.TextShadow)); }
                        canvas.Sprite(drawn.Icon, icon.X, icon.Y, icon.Width, icon.Height, Colour(palette.Icon));
                    }
                    if (drawn.Ammo.Length > 0)
                    {
                        HudRect ammo = HudLayout.Ammo(layout, canvas.Width);
                        if (shadow > 0) { canvas.Text(drawn.Ammo, ammo.X + shadow, ammo.Y + shadow, ammo.Width, ammo.Height, TextStyle.Emphasis, TextAlign.Right, Colour(palette.TextShadow)); }
                        canvas.Text(drawn.Ammo, ammo.X, ammo.Y, ammo.Width, ammo.Height, TextStyle.Emphasis, TextAlign.Right, Colour(drawn.ClipLow ? palette.LowClip : palette.Text));
                    }
                }
                if (drawn.DrawHealth)
                {
                    canvas.Opacity = (float)(drawn.HealthAlpha * drawn.HealthPulse);
                    Bar(canvas, HudLayout.HealthBar(layout, canvas.Width), drawn.HealthFill, palette.BarBack, drawn.HealthLow ? palette.HealthLow : palette.Health);
                }
                if (drawn.DrawArmour)
                {
                    canvas.Opacity = (float)drawn.ArmourAlpha;
                    Bar(canvas, HudLayout.ArmourBar(layout, canvas.Width), drawn.ArmourFill, palette.BarBack, palette.Armour);
                }
                if (drawn.DrawWanted && !drawn.Star.IsNone)
                {
                    canvas.Opacity = (float)drawn.WantedAlpha;
                    int count = c.Wanted.MaximumStars;
                    HudRect stars = HudLayout.Stars(layout, canvas.Width, count);
                    for (int index = 0; index < count; index++)
                    {
                        float x = stars.X + index * (layout.StarSize + layout.StarGap);
                        canvas.Sprite(drawn.Star, x, stars.Y, layout.StarSize, layout.StarSize, Colour(index < drawn.Stars ? palette.WantedStar : palette.WantedEmpty));
                    }
                }
                if (drawn.DrawPrompt)
                {
                    canvas.Opacity = (float)drawn.PromptAlpha;
                    canvas.Rect(layout.PromptX, layout.PromptY, layout.PromptWidth, layout.PromptHeight, Colour(palette.PromptBack));
                    canvas.Text(drawn.Prompt, layout.PromptX + 12, layout.PromptY + 10, layout.PromptWidth - 24, layout.PromptHeight - 14, TextStyle.Body, TextAlign.Left, Colour(palette.PromptText));
                }
            }
            finally
            {
                canvas.Opacity = 1f;
                CostMeter.Add("draw.hud", started);
            }
        }

        private static void Bar(ICanvas canvas, HudRect rect, double fill, int[] back, int[] colour)
        {
            canvas.Rect(rect.X, rect.Y, rect.Width, rect.Height, Colour(back));
            float width = (float)(rect.Width * Math.Max(0.0, Math.Min(1.0, fill)));
            if (width >= 1f) { canvas.Rect(rect.X, rect.Y, width, rect.Height, Colour(colour)); }
        }

        private static Rgba Colour(int[] argb)
        {
            return new Rgba((byte)argb[1], (byte)argb[2], (byte)argb[3], (byte)argb[0]);
        }

        // --- commands ---------------------------------------------------------------------------------------------------------

        private void RegisterCommands()
        {
            Engine.Commands.Register(this, "hudctl",
                "hudctl status | table | check | ammo | hide <NAME> | hide-matching <text> | restore [NAME] | force <weapon|health|armour|wanted|prompt|all> on|off | " +
                "hurt <n> | health <n> | armour <n> | prompt <text> | device auto|pad|keyboard | layout-test on|off | probe-mode on|off | native-display on <milliseconds>|off | baseline state|check <weapon> | baseline idle <weapon> <ms> | baseline aim <weapon> <distance> <height> <ms> | baseline off | cash-test pulse|restore | config-test off|restore - Liberty HUD (T-049): state, hud.dat table probe, test hooks", HudCommand);
        }

        private string HudCommand(string[] args)
        {
            if (args.Length == 0) { return Status(); }
            switch (args[0].ToLowerInvariant())
            {
                case "status": return Status();
                case "table": return Table();
                case "check": return Check();
                case "ammo": return Ammo();
                case "baseline": return Baseline(args);
                case "cash-test":
                    if (args.Length == 2 && args[1] == "restore") { RestoreCashTest(); return "hud_cash_test restored"; }
                    if (args.Length != 2 || args[1] != "pulse" || !probeMode || disabled || !Liberty.World.HasPlayer)
                    { return "error: hudctl cash-test pulse|restore (pulse requires probe-mode and a player)"; }
                    if (!cashTestOriginal.HasValue) { cashTestOriginal = Liberty.Player.Money; }
                    // Change the score so the vanilla cash counter has a visible baseline; restore the exact wallet afterwards.
                    int original = cashTestOriginal.Value;
                    Liberty.Player.Money = original == int.MaxValue ? original - 1 : original + 1;
                    return "hud_cash_test pulse original=" + original + " current=" + Liberty.Player.Money;
                case "native-display":
                    if (args.Length == 2 && args[1] == "off")
                    {
                        nativeDisplay.Cancel();
                        UpdateNativeDisplay();
                        return "hud_native_display off";
                    }
                    int duration;
                    if (args.Length != 3 || args[1] != "on" || !int.TryParse(args[2], out duration) || duration <= 0 || duration > 120000)
                    { return "error: hudctl native-display on <1..120000 milliseconds>|off"; }
                    if (disabled || !config.Enabled || !probeMode || layoutTest)
                    { return "error: native-display requires enabled HUD, probe-mode on and layout-test off"; }
                    nativeDisplay.Arm(clock.ElapsedMilliseconds, duration);
                    return "hud_native_display armed milliseconds=" + duration + " visual=unproven";
                case "probe-mode":
                    if (args.Length < 2 || (args[1] != "on" && args[1] != "off")) { return "error: hudctl probe-mode on|off"; }
                    ProbeRestore(null);
                    probeMode = args[1] == "on";
                    ResetAll();
                    ApplyPlan();
                    return "hud_probe_mode " + args[1];
                case "config-test":
                    if (args.Length < 2) { return "error: hudctl config-test off|restore"; }
                    if (args[1] == "restore") { RestoreConfigTest(); return "hud_config_test restored"; }
                    if (args[1] != "off") { return "error: hudctl config-test off|restore"; }
                    if (configTestOriginal != null) { return "error: config test already active"; }
                    configTestOriginal = JsonStore.ReadBytes(LibertyPaths.HudConfig);
                    RuntimeLog.Info("hud_config_test saved hash=" + JsonStore.Hash(configTestOriginal));
                    HudConfig testConfig = JsonStore.Parse<HudConfig>(configTestOriginal);
                    testConfig.Enabled = false;
                    JsonStore.Save(LibertyPaths.HudConfig, testConfig);
                    return "hud_config_test wrote enabled=False (await normal config poll)";
                case "hide": return args.Length < 2 ? "error: hudctl hide <NAME>" : ProbeHide(args[1]);
                case "hide-matching": return args.Length < 2 ? "error: hudctl hide-matching <text>" : ProbeHideMatching(args[1]);
                case "restore": return ProbeRestore(args.Length > 1 ? args[1] : null);
                case "force":
                    if (args.Length < 3) { return "error: hudctl force <element|all> on|off"; }
                    forced[args[1].ToLowerInvariant()] = args[2].ToLowerInvariant();
                    return "force " + args[1] + " " + args[2];
                case "hurt": return args.Length < 2 ? "error: hudctl hurt <amount>" : SetPlayerHealth(-ParseInt(args[1]), true);
                case "health": return args.Length < 2 ? "error: hudctl health <value>" : SetPlayerHealth(ParseInt(args[1]), false);
                case "armour": return args.Length < 2 ? "error: hudctl armour <value>" : SetPlayerArmour(ParseInt(args[1]));
                case "prompt":
                    if (args.Length < 2) { return "error: hudctl prompt <text>"; }
                    Engine.Ui.ShowHelp(this, string.Join(" ", args, 1, args.Length - 1), 8000);
                    return "prompt shown";
                case "layout-test":
                    if (args.Length < 2 || (args[1] != "on" && args[1] != "off")) { return "error: hudctl layout-test on|off"; }
                    layoutTest = args[1] == "on";
                    ApplyPlan();
                    return "layout-test " + args[1];
                case "device":
                    if (args.Length < 2 || (args[1] != "auto" && args[1] != "pad" && args[1] != "keyboard")) { return "error: hudctl device auto|pad|keyboard"; }
                    deviceOverride = args[1] == "auto" ? null : args[1];
                    return "device " + args[1];
                default: return "error: unknown hudctl subcommand " + args[0];
            }
        }

        private static int ParseInt(string text)
        {
            int value;
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : 0;
        }

        private void RestoreConfigTest()
        {
            if (configTestOriginal == null) { return; }
            System.IO.File.WriteAllBytes(LibertyPaths.HudConfig, configTestOriginal);
            string restoredHash = JsonStore.Hash(JsonStore.ReadBytes(LibertyPaths.HudConfig));
            if (restoredHash != JsonStore.Hash(configTestOriginal)) { throw new System.IO.IOException("HUD config restore readback differs"); }
            configTestOriginal = null;
            RuntimeLog.Info("hud_config_test original bytes restored hash=" + restoredHash);
        }

        private void RestoreCashTest()
        {
            if (!cashTestOriginal.HasValue) { return; }
            Liberty.Player.Money = cashTestOriginal.Value;
            cashTestOriginal = null;
            RuntimeLog.Info("hud_cash_test original wallet restored");
        }

        private bool BaselineGameplaySafe(PlayerState player, WorldInfo info)
        {
            return player.IsPlaying && player.HasControl && !player.IsDead && !player.InVehicle && player.Health > 0 &&
                !info.Paused && !info.CutscenePlaying && !info.FadedOut && !info.MissionActive &&
                !Engine.Ui.AnyMenuOpen && Liberty.Input.CapturedBy == null && !Engine.Ui.HudHiddenByOtherOwner(this);
        }

        private void UpdateBaselineTask()
        {
            RetireExpiredBaselineTask();
            if (!baselineTaskPed.IsNone && (disabled || config == null || !config.Enabled || !probeMode || layoutTest ||
                nativeDisplay.Requested || nativeDisplay.Applied || cashTestOriginal.HasValue || !Liberty.World.HasPlayer ||
                Liberty.World.Player.Ped != baselineTaskPed || !BaselineGameplaySafe(Liberty.World.Player, Liberty.World.Info)))
            { ReleaseBaselineTask(); }
        }

        private string Baseline(string[] args)
        {
            RetireExpiredBaselineTask();
            if (args.Length == 2 && args[1] == "off") { ReleaseBaselineTask(); return "hud_baseline task released"; }
            int expected;
            if (args.Length < 3 || !int.TryParse(args[2], out expected) || expected <= 0)
            { return "error: hudctl baseline state|check|idle|aim <positive weapon> ... | off"; }
            if ((args[1] == "state" || args[1] == "check") && args.Length != 3)
            { return "error: hudctl baseline state|check <positive weapon>"; }
            if (!Liberty.World.HasPlayer) { return "error: no player for baseline"; }
            PlayerState player = Liberty.World.Player;
            WorldInfo info = Liberty.World.Info;
            // Read inventory for the expected id independently of what's actually selected; do not reuse the cached HUD pair in probe mode.
            int held = Liberty.Weapons.Current(player.Ped);
            bool owned = Liberty.Weapons.Has(player.Ped, expected);
            int clip = owned ? Liberty.Weapons.GetAmmoInClip(player.Ped, expected) : -1;
            int total = owned ? Liberty.Weapons.GetAmmo(player.Ped, expected) : -1;
            bool numericReady = HudBaselineReadback.HasHeldAmmo(expected, held, player.Weapon, owned, clip, total);
            bool safe = BaselineGameplaySafe(player, info);
            if (args[1] == "state" || args[1] == "check")
            {
                string text = "hud_baseline expected=" + expected + " held=" + held + " snapshot=" + player.Weapon + " owned=" + owned +
                    " clip=" + clip + " total=" + total + " numeric_ready=" + numericReady + " gameplay_safe=" + safe +
                    " playing=" + player.IsPlaying + " control=" + player.HasControl + " dead=" + player.IsDead + " vehicle=" + player.InVehicle +
                    " paused=" + info.Paused + " cutscene=" + info.CutscenePlaying + " faded=" + info.FadedOut + " mission=" + info.MissionActive +
                    " menu=" + Engine.Ui.AnyMenuOpen + " input_owner=" + (Liberty.Input.CapturedBy == null ? "none" : Liberty.Input.CapturedBy.Id) +
                    " other_hud_owner=" + Engine.Ui.HudHiddenByOtherOwner(this) + " hidden=" + (planHidden.Count + probeHidden.Count) +
                    " native_requested=" + nativeDisplay.Requested + " native_applied=" + nativeDisplay.Applied + " task_requested=" + baselineTask +
                    " aim_input=" + (Liberty.Input.KeyDown((VirtualKey)RightMouseButton) || Liberty.Input.LeftTrigger > config.Weapon.AimTriggerLevel) +
                    " game_cam_pos=" + Liberty.Cameras.GameCameraPosition + " game_cam_rot=" + Liberty.Cameras.GameCameraRotation +
                    " game_cam_fov=" + Liberty.Cameras.GameCameraFov.ToString("0.###", CultureInfo.InvariantCulture) + " visual=unproven";
                RuntimeLog.Info(text);
                return args[1] == "check" && (!numericReady || !safe) ? "error: baseline numeric/gameplay precondition failed; " + text : text;
            }
            int duration;
            int durationIndex = args[1] == "idle" ? 3 : 5;
            if ((args[1] != "idle" && args[1] != "aim") || args.Length != durationIndex + 1 ||
                !int.TryParse(args[durationIndex], out duration) || duration <= 0 || duration > 120000)
            { return "error: baseline idle <weapon> <1..120000 ms> | aim <weapon> <distance> <height> <1..120000 ms>"; }
            if (!config.Enabled || disabled || !probeMode || layoutTest || nativeDisplay.Requested || nativeDisplay.Applied ||
                cashTestOriginal.HasValue || !numericReady || !safe)
            { return "error: baseline tasks require safe held-weapon gameplay, probe mode and no native/cash diagnostic"; }
            float distance = 0, height = 0;
            if (args[1] == "aim" && (!float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out distance) ||
                !float.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out height) ||
                float.IsNaN(distance) || float.IsInfinity(distance) || distance <= 0 || float.IsNaN(height) || float.IsInfinity(height)))
            { return "error: baseline aim needs finite positive distance and finite height"; }
            ReleaseBaselineTask();
            baselineTaskPed = player.Ped;
            baselineTask = args[1];
            baselineTaskExpiresMilliseconds = clock.ElapsedMilliseconds + duration;
            if (baselineTask == "idle") { Liberty.Tasks.StandStill(player.Ped, duration); }
            else { Liberty.Tasks.AimAt(player.Ped, player.Position + Vec3.FromHeading(player.Heading) * distance + new Vec3(0, 0, height), duration); }
            return "hud_baseline task requested=" + baselineTask + " milliseconds=" + duration + " visual=unproven";
        }

        private void ReleaseBaselineTask()
        {
            // Off/stop can arrive without another tick. Never clear tasks using an expired fixture owner.
            RetireExpiredBaselineTask();
            if (baselineTaskPed.IsNone) { return; }
            if (Liberty.Peds.Exists(baselineTaskPed)) { Liberty.Tasks.Clear(baselineTaskPed); }
            baselineTaskPed = PedRef.None;
            baselineTask = "none";
            baselineTaskExpiresMilliseconds = 0;
            RuntimeLog.Info("hud_baseline owned task cleared (previous tasks are not snapshotted)");
        }

        private void RetireExpiredBaselineTask()
        {
            // This is a conservative wall-time ownership limit, not a native task completion readback.
            // Paused game time may lag this monotonic clock; expiry must still not clear later unrelated work.
            if (baselineTaskPed.IsNone || clock.ElapsedMilliseconds < baselineTaskExpiresMilliseconds) { return; }
            baselineTaskPed = PedRef.None;
            baselineTask = "none";
            baselineTaskExpiresMilliseconds = 0;
            RuntimeLog.Info("hud_baseline task ownership expired (native completion unproven)");
        }

        private string SetPlayerHealth(int amount, bool relative)
        {
            if (!Liberty.World.HasPlayer) { return "error: no player"; }
            PlayerState player = Liberty.World.Player;
            int target = relative ? player.Health + amount : amount;
            Liberty.Peds.SetHealth(player.Ped, Math.Max(1, target));
            return "health " + Math.Max(1, target);
        }

        private string SetPlayerArmour(int value)
        {
            if (!Liberty.World.HasPlayer) { return "error: no player"; }
            PlayerState player = Liberty.World.Player;
            Liberty.Peds.SetArmour(player.Ped, Math.Max(0, value));
            return "armour " + Math.Max(0, value);
        }

        // The numbers behind the ammo line: the clip, the game's total and what the line shows. Records whether the total counts the clip.
        private string Ammo()
        {
            if (!Liberty.World.HasPlayer) { return "error: no player"; }
            PlayerState player = Liberty.World.Player;
            int clip = ammoSample.Weapon == player.Weapon ? ammoSample.Clip : -1;
            int total = ammoSample.Weapon == player.Weapon ? ammoSample.Total : -1;
            string line = "hud_ammo weapon=" + player.Weapon + " clip=" + clip + " total=" + total + " includes_clip=" + config.Weapon.TotalIncludesClip +
                " shown=\"" + HudText.FormatAmmo(clip, total, config.Weapon.TotalIncludesClip) + "\"";
            RuntimeLog.Info(line);
            return line;
        }

        private string Status()
        {
            StringBuilder text = new StringBuilder();
            text.Append("enabled=").Append(config != null && config.Enabled).Append(" disabled=").Append(disabled);
            text.Append(" table=").Append(hider != null ? addresses.HudComponents.Count.ToString(CultureInfo.InvariantCulture) : "none");
            if (weaponPlan != null)
            {
                text.Append(" weapon=").Append(ModeText(weaponPlan.Mode)).Append(" health=").Append(ModeText(healthPlan.Mode));
                text.Append(" armour=").Append(ModeText(armourPlan.Mode)).Append(" wanted=").Append(ModeText(wantedPlan.Mode));
            }
            text.Append(" device=").Append(device.Current.ToString().ToLowerInvariant()).Append(" layout_test=").Append(layoutTest).Append(" probe_mode=").Append(probeMode);
            text.Append(" wanted_stars=").Append(wantedLevel);
            text.Append(" native_requested=").Append(nativeDisplay.Requested).Append(" native_applied=").Append(nativeDisplay.Applied);
            text.Append(" hidden=").Append(planHidden.Count + probeHidden.Count).Append(" help_by_hud=").Append(Engine.Ui.HelpDrawnByHud);
            Frame now = frame;
            text.Append(" drawn=");
            if (now != null)
            {
                text.Append(now.DrawWeapon ? "weapon," : "").Append(now.DrawHealth ? "health," : "").Append(now.DrawArmour ? "armour," : "")
                    .Append(now.DrawWanted ? "wanted," : "").Append(now.DrawPrompt ? "prompt," : "");
            }
            string result = text.ToString();
            RuntimeLog.Info("hud_status " + result);
            return result;
        }

        // The game's hud.dat table as the resolver read it: every component with its live values, and the calls it could not parse.
        private string Table()
        {
            if (hider == null) { return "error: hud.dat table unavailable (" + (addresses != null ? addresses.HudComponents.Count + " components" : "engine memory unresolved") + ")"; }
            foreach (GameAddresses.HudComponentGlobals component in addresses.HudComponents)
            {
                RuntimeLog.Info("hud_component name=" + component.Name + " layout=" + (component.LayoutConsistent ? "ok" : "differs") +
                    " " + (component.LayoutConsistent ? hider.Describe(component.Name) : "unreadable") +
                    " alpha_global=0x" + component.AlphaGlobal.ToString("X8") + " size_global=0x" + component.SizeGlobal.ToString("X8") + " pos_global=0x" + component.PositionGlobal.ToString("X8"));
            }
            foreach (string line in addresses.HudUnparsed) { RuntimeLog.Info("hud_unparsed " + line); }
            return "hud_table components=" + addresses.HudComponents.Count + " unparsed=" + addresses.HudUnparsed.Count;
        }

        // Probe hiding (research): hides one component by name so a screenshot shows what it was; `hudctl restore` brings it back.
        private string ProbeHide(string name)
        {
            // Not an "error" reply: the research scenario names components it has not seen yet and must run on.
            if (hider == null || !hider.Knows(name) || IsReticleComponent(name)) { return "not found: " + name + " is not in the hideable hud.dat table (hudctl table lists it; the reticle components belong to gunplay)"; }
            hider.Hide(name);
            probeHidden.Add(name);
            RuntimeLog.Info("hud_probe_hide name=" + name + " hidden=" + hider.IsHidden(name));
            return "hidden " + name;
        }

        private string ProbeHideMatching(string text)
        {
            if (hider == null) { return "not found: hud.dat table unavailable"; }
            int count = 0;
            StringBuilder names = new StringBuilder();
            foreach (GameAddresses.HudComponentGlobals component in addresses.HudComponents)
            {
                if (component.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0 || !hider.Knows(component.Name) || IsReticleComponent(component.Name)) { continue; }
                hider.Hide(component.Name);
                probeHidden.Add(component.Name);
                names.Append(count > 0 ? "," : "").Append(component.Name);
                count++;
            }
            RuntimeLog.Info("hud_probe_hide_matching text=" + text + " count=" + count + " names=" + names);
            return count == 0 ? "none matched " + text : "hidden " + count + ": " + names;
        }

        private string ProbeRestore(string name)
        {
            if (hider == null) { return "error: hud.dat table unavailable"; }
            List<string> names = name == null || name == "all" ? new List<string>(probeHidden) : new List<string> { name };
            foreach (string item in names)
            {
                if (planHidden.Contains(item)) { probeHidden.Remove(item); continue; }
                hider.Restore(item);
                probeHidden.Remove(item);
            }
            RuntimeLog.Info("hud_probe_restore names=" + string.Join(",", names.ToArray()));
            return "restored " + names.Count;
        }

        // Check saved-value ownership and layout. IsHidden is a restoration ledger, not proof of visible disappearance.
        private string Check()
        {
            if (config == null || weaponPlan == null) { return "error: hud not started"; }
            if (disabled) { return "error: hud module disabled after an error"; }
            List<string> problems = new List<string>();
            foreach (KeyValuePair<string, HudDecision> pair in new Dictionary<string, HudDecision> { { "weapon", weaponPlan }, { "health", healthPlan }, { "armour", armourPlan }, { "wanted", wantedPlan } })
            {
                if (pair.Value.Mode != HudElementMode.Liberty) { continue; }
                foreach (string name in pair.Value.ToHide) { if (hider == null || !hider.IsHidden(name)) { problems.Add(pair.Key + ":" + name + " not hidden"); } }
            }
            HudRect group = HudLayout.Group(config.Layout, 720f * 16f / 9f, config.Wanted.MaximumStars);
            if (group.X < 720f * 16f / 9f / 2f || group.Y > 720f / 2f) { problems.Add("group not in the top right"); }
            if (problems.Count > 0) { return "error: " + string.Join("; ", problems.ToArray()); }
            return "hud_check ok weapon=" + ModeText(weaponPlan.Mode) + " health=" + ModeText(healthPlan.Mode) + " armour=" + ModeText(armourPlan.Mode) +
                " wanted=" + ModeText(wantedPlan.Mode) + " hidden=" + planHidden.Count;
        }
    }
}
