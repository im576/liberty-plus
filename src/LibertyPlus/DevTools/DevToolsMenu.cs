using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Keys = System.Windows.Forms.Keys;
using GTA;
using LibertyFramework.Arsenal;
using LibertyFramework.Core.Config;
using LibertyFramework.Core.Input;
using LibertyFramework.Core.Logging;
using LibertyFramework.DevTools.Menu;
using LibertyFramework.DevTools.Teleport;
using LibertyFramework.DevTools.TestRange;
using LibertyFramework.Gunplay;
using LibertyFramework.Gunplay.Profiles;
using LibertyFramework.Weapons;
using LibertyFramework.Weapons.Logic;

namespace LibertyFramework.DevTools
{
    // Liberty DevTools. Open/close: hold L3+R3 (0.7 s) or F10. D-pad/arrows move, left/right adjust,
    // A/Enter select, B/Backspace back. Player controls are locked while open so the D-pad never
    // reaches the phone; they are restored on close, error and script unload.
    [global::Liberty.Sdk.Module("devtools", Order = 90, Capabilities = new[] { global::Liberty.Sdk.Capabilities.EngineInternal }, Description = "Liberty DevTools: controller menu, live tuning, teleports, debug overlay")]
    public sealed class DevToolsMenu : LibertyFramework.Engine.Module
    {
        private const int ChordHoldMilliseconds = 700;
        private const int RepeatDelayMilliseconds = 400;
        private const int RepeatIntervalMilliseconds = 90;
        private const int VisibleRows = 14;

        internal static bool IsOpen { get; private set; }

        private readonly GTA.Font titleFont;
        private readonly GTA.Font font;
        private readonly ControllerInput controller = new ControllerInput();
        private readonly TeleportService teleports = new TeleportService();
        private readonly TestRangeService testRange = new TestRangeService();
        private readonly Stack<MenuPage> stack = new Stack<MenuPage>();
        private readonly MenuPage root;
        private bool disabled;
        private bool controlLocked;
        private bool chordTriggered;
        private DateTime chordStartUtc = DateTime.MinValue;
        private bool previousToggle, previousUp, previousDown, previousLeft, previousRight, previousActivate, previousBack;
        private DateTime adjustHeldSinceUtc;
        private DateTime lastRepeatUtc;
        private MenuItem pendingConfirmation;
        private string message = "";
        private int tuningWeaponIndex;
        private int presetIndex;

        public DevToolsMenu()
        {
            Interval = 30;
            titleFont = new GTA.Font(20.0F, FontScaling.Pixel, true, false);
            titleFont.Color = Color.FromArgb(255, 235, 200, 90);
            font = new GTA.Font(17.0F, FontScaling.Pixel);
            font.Color = Color.White;
            root = BuildRoot();
            Tick += OnTick;
            PerFrameDrawing += OnDraw;
            AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
            RuntimeLog.Info("devtools_started");
        }

        // Console / autopilot: "goto <location id>" uses the same data-driven teleports as the TELEPORT page.
        protected internal override void OnStart()
        {
            Engine.Commands.Register(this, "goto", "goto <location id> - teleport to a DevTools location (config/devtools/locations.json)", args =>
            {
                TeleportLocation location = args.Length > 0 ? teleports.Find(args[0]) : null;
                if (location != null) { return teleports.Start(Player, location); }
                string error;
                return "unknown location; known: " + string.Join(",", teleports.Load(out error).ConvertAll(l => l.Id).ToArray());
            });
        }

        private static GunplayController Gunplay { get { return GunplayController.Instance; } }
        private static GunplayConfig Config { get { return Gunplay != null ? Gunplay.Config : null; } }

        private MenuPage BuildRoot()
        {
            MenuPage weapons = new MenuPage("WEAPONS", BuildWeapons);
            MenuPage gunplay = new MenuPage("GUNPLAY", BuildGunplay);
            MenuPage tuning = new MenuPage("LIVE TUNING", BuildTuning);
            MenuPage presets = new MenuPage("PRESETS & CONFIG", BuildPresets);
            MenuPage teleport = new MenuPage("TELEPORT", BuildTeleport);
            MenuPage range = new MenuPage("TEST RANGE", BuildRange);
            MenuPage inspect = new MenuPage("INSPECT STATE", BuildInspect);
            MenuPage help = new MenuPage("HELP", BuildHelp);
            return new MenuPage("LIBERTY DEVTOOLS", () =>
            {
                List<MenuItem> items = new List<MenuItem> {
                    MenuItem.Page(weapons), MenuItem.Page(gunplay), MenuItem.Page(tuning), MenuItem.Page(presets),
                    MenuItem.Page(teleport), MenuItem.Page(range), MenuItem.Page(inspect) };
                // Pages registered by feature scripts (Arsenal, Holsters, ...), see DevToolsPages.
                foreach (MenuPage page in DevToolsPages.All()) { items.Add(MenuItem.Page(page)); }
                items.Add(MenuItem.Page(help));
                return items;
            });
        }

        private List<MenuItem> BuildWeapons()
        {
            List<MenuItem> items = new List<MenuItem>();
            GunplayConfig config = Config;
            if (config == null) { items.Add(MenuItem.Info(() => "gunplay.json not loaded; see log")); return items; }
            // T-041/T-042: the Stage 1 arsenal from the weapon catalog (same as the `catalog give` command).
            try
            {
                WeaponCatalog catalog = JsonStore.Load<WeaponCatalog>(LibertyPaths.WeaponCatalog);
                // Owner decision 2026-09-30: the weapons outside normal Stage 1 availability (P90 look, MG36, snipers) stay in the game
                // and are always obtainable from here; the test weapons have their own entries below.
                List<WeaponCatalogEntry> menuEntries = catalog.Stage1Entries();
                foreach (WeaponCatalogEntry entry in catalog.Entries) { if (!entry.Stage1 && entry.Tier == "restricted") { menuEntries.Add(entry); } }
                foreach (WeaponCatalogEntry entry in menuEntries)
                {
                    WeaponCatalogEntry captured = entry;
                    items.Add(MenuItem.Confirmed("Give " + entry.Label + (entry.Stage1 ? " (Stage 1, " + entry.Tier + ")" : " (not normal availability)"), () =>
                    {
                        Engine.Weapons.Give(Engine.World.Player.Ped, captured.WeaponId, config.TestRange.AmmoRefillRounds);
                        Engine.Weapons.Select(Engine.World.Player.Ped, captured.WeaponId);
                        RuntimeLog.Info("catalog_give id=" + captured.Id + " weapon=" + captured.WeaponId + " stage1=" + captured.Stage1 + " source=devtools");
                        return "Gave " + captured.Label;
                    }));
                }
            }
            catch (Exception error) { RuntimeLog.Error("devtools_stage1_weapons_failed error=" + error.Message); }
            foreach (WeaponProfile profile in config.TestWeapons())
            {
                WeaponProfile captured = profile;
                items.Add(MenuItem.Confirmed("Give " + profile.Label + " (ID " + profile.WeaponId + ")", () => TestWeaponActions.Select(Player, captured, true)));
            }
            foreach (WeaponProfile profile in config.TestWeapons())
            {
                WeaponProfile captured = profile;
                items.Add(MenuItem.Confirmed("Give vanilla " + profile.Label.Replace("Gold ", "").ToLowerInvariant() + " (ID " + profile.VanillaWeaponId + ")",
                    () => TestWeaponActions.Select(Player, captured, false)));
            }
            items.Add(MenuItem.Confirmed("Give all test weapons", () => TestWeaponActions.GiveAll(Player, config)));
            items.Add(MenuItem.Action("Refill ammo", () => TestWeaponActions.Refill(Player, config.TestRange.AmmoRefillRounds)));
            items.Add(MenuItem.Toggle("Infinite ammo", () => Gunplay.InfiniteAmmo, value => Gunplay.InfiniteAmmo = value));
            items.Add(MenuItem.Action("Weapon status", () => TestWeaponActions.Status(Player, config)));
            return items;
        }

        private List<MenuItem> BuildGunplay()
        {
            List<MenuItem> items = new List<MenuItem>();
            if (Gunplay == null) { items.Add(MenuItem.Info(() => "Gunplay script not running")); return items; }
            items.Add(MenuItem.Toggle("Free aim (no lock-on/snap/health ring)", () => Gunplay.FreeAimEnabled, value => Gunplay.FreeAimEnabled = value));
            items.Add(MenuItem.Toggle("Custom crosshair (hides vanilla reticle)", () => Gunplay.CrosshairEnabled, value => Gunplay.CrosshairEnabled = value));
            items.Add(MenuItem.Toggle("Camera kick (test weapons)", () => Gunplay.CameraKickEnabled, value => Gunplay.CameraKickEnabled = value));
            items.Add(MenuItem.Toggle("Spread control (test weapons)", () => Gunplay.SpreadControlEnabled, value => Gunplay.SpreadControlEnabled = value));
            items.Add(MenuItem.Toggle("Auto-calibrate spread", () => Config != null && Config.SpreadCalibration.AutoCalibrate,
                value => { if (Config != null) { Config.SpreadCalibration.AutoCalibrate = value; } }));
            items.Add(MenuItem.Toggle("Debug overlay", () => Gunplay.DebugOverlay, value => Gunplay.DebugOverlay = value));
            items.Add(MenuItem.Action("Reset spread calibration + audit", () => { Gunplay.ResetCalibration(); return "Calibration reset"; }));
            return items;
        }

        private List<MenuItem> BuildTuning()
        {
            List<MenuItem> items = new List<MenuItem>();
            GunplayConfig config = Config;
            if (config == null || config.Weapons.Count == 0) { items.Add(MenuItem.Info(() => "gunplay.json not loaded")); return items; }
            tuningWeaponIndex = Math.Max(0, Math.Min(config.Weapons.Count - 1, tuningWeaponIndex));
            MenuItem weaponSelector = MenuItem.Info(() => "Weapon: < " + SafeTuningWeapon().Label + " >  (" + SafeTuningWeapon().ProfileName + ")");
            weaponSelector.Adjust = direction =>
            {
                tuningWeaponIndex = (tuningWeaponIndex + direction + Config.Weapons.Count) % Config.Weapons.Count;
                return "Tuning " + SafeTuningWeapon().Label;
            };
            items.Add(weaponSelector);
            foreach (TuningParameter parameter in config.Tuning)
            {
                TuningParameter captured = parameter;
                MenuItem item = MenuItem.Info(() => ProfileParameters.ShortName(captured.Key) + " = " +
                    ProfileParameters.Get(SafeTuningWeapon(), captured.Key).ToString("0.###") + "   < >");
                item.Adjust = direction => AdjustParameter(captured, direction);
                items.Add(item);
            }
            items.Add(MenuItem.Confirmed("Save live values to gunplay.json", () => Gunplay.Store.SaveActiveToConfig() ? "Saved (backup: gunplay.json.bak)" : "Save failed; see log"));
            items.Add(MenuItem.Confirmed("Revert: reload gunplay.json", () => ReloadConfig()));
            return items;
        }

        private WeaponProfile SafeTuningWeapon()
        {
            GunplayConfig config = Config;
            return config.Weapons[Math.Max(0, Math.Min(config.Weapons.Count - 1, tuningWeaponIndex))];
        }

        private string AdjustParameter(TuningParameter parameter, int direction)
        {
            WeaponProfile profile = SafeTuningWeapon();
            double value = ProfileParameters.Get(profile, parameter.Key) + parameter.Step * direction;
            value = Math.Max(parameter.Minimum, Math.Min(parameter.Maximum, Math.Round(value / parameter.Step) * parameter.Step));
            ProfileParameters.Set(profile, parameter.Key, value);
            Gunplay.Store.MarkLiveEdit();
            RuntimeLog.Info("tuning_live weapon=" + profile.WeaponId + " " + parameter.Key + "=" + value.ToString("0.####"));
            return profile.Label + " " + ProfileParameters.ShortName(parameter.Key) + " = " + value.ToString("0.###");
        }

        private List<MenuItem> BuildPresets()
        {
            List<MenuItem> items = new List<MenuItem>();
            if (Gunplay == null) { items.Add(MenuItem.Info(() => "Gunplay script not running")); return items; }
            string[] files = Directory.Exists(LibertyPaths.PresetDirectory) ? Directory.GetFiles(LibertyPaths.PresetDirectory, "*.json") : new string[0];
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            items.Add(MenuItem.Info(() => "Active: " + (Gunplay.Store.ActivePresetName ?? "-")));
            if (files.Length > 0)
            {
                presetIndex = Math.Max(0, Math.Min(files.Length - 1, presetIndex));
                MenuItem load = MenuItem.Info(() => "Load preset: < " + Path.GetFileNameWithoutExtension(files[presetIndex]) + " >  (A to load)");
                load.Adjust = direction => { presetIndex = (presetIndex + direction + files.Length) % files.Length; return Path.GetFileName(files[presetIndex]); };
                load.Activate = () =>
                {
                    bool ok = Gunplay.Store.ApplyPreset(files[presetIndex]);
                    if (ok) { Gunplay.OnProfilesChanged(); }
                    return ok ? "Loaded " + Gunplay.Store.ActivePresetName : "Preset rejected; see log";
                };
                items.Add(load);
            }
            else
            {
                items.Add(MenuItem.Info(() => "No presets in " + LibertyPaths.PresetDirectory));
            }
            items.Add(MenuItem.Confirmed("Save live values as preset 'user_saved'", () =>
                Gunplay.Store.SavePreset(Path.Combine(LibertyPaths.PresetDirectory, "user_saved.json"), "User saved",
                    "Saved in game " + DateTime.Now.ToString("yyyy-MM-dd HH:mm")) ? "Saved presets/user_saved.json" : "Save failed; see log"));
            items.Add(MenuItem.Confirmed("Save live values to gunplay.json", () => Gunplay.Store.SaveActiveToConfig() ? "Saved (backup: gunplay.json.bak)" : "Save failed; see log"));
            items.Add(MenuItem.Action("Reload config from disk", () => ReloadConfig()));
            items.Add(MenuItem.Info(() => Gunplay.Store.LastError != null ? "Last error: " + Gunplay.Store.LastError : "Config OK (revision " + Gunplay.Store.Revision + ")"));
            return items;
        }

        private string ReloadConfig()
        {
            bool ok = Gunplay.Store.Poll(true);
            if (ok) { Gunplay.OnProfilesChanged(); }
            return ok ? "Reloaded gunplay.json (revision " + Gunplay.Store.Revision + ")" : "Reload rejected; kept previous config";
        }

        private List<MenuItem> BuildTeleport()
        {
            List<MenuItem> items = new List<MenuItem>();
            string error;
            foreach (TeleportLocation location in teleports.Load(out error))
            {
                TeleportLocation captured = location;
                items.Add(MenuItem.Action(location.Name, () => teleports.Start(Player, captured)));
            }
            if (error != null) { items.Add(MenuItem.Info(() => "locations.json error: " + error)); }
            items.Add(MenuItem.Confirmed("Set Gun Test Range here", () => teleports.SaveGunTestRangeHere(Player)));
            items.Add(MenuItem.Action("Return to previous position", () => teleports.Return(Player)));
            return items;
        }

        private List<MenuItem> BuildRange()
        {
            List<MenuItem> items = new List<MenuItem>();
            GunplayConfig config = Config;
            items.Add(MenuItem.Action("Teleport to Gun Test Range", () =>
            {
                TeleportLocation range = teleports.Find(TeleportService.GunTestRangeId);
                return range == null ? "gun_test_range missing in locations.json" : teleports.Start(Player, range);
            }));
            if (config != null)
            {
                items.Add(MenuItem.Action("Spawn target lane (5/10/25/50 m + car)", () => testRange.SpawnLane(Player, config.TestRange)));
                items.Add(MenuItem.Action("Restore health & armor", () => TestRangeService.RestoreHealth(Player, config.TestRange)));
                items.Add(MenuItem.Action("Refill ammo", () => TestWeaponActions.Refill(Player, config.TestRange.AmmoRefillRounds)));
            }
            items.Add(MenuItem.Action("Clear targets", () => testRange.Clear()));
            items.Add(MenuItem.Action("Clear wanted level", () => TestRangeService.ClearWanted(Player)));
            return items;
        }

        private List<MenuItem> BuildInspect()
        {
            List<MenuItem> items = new List<MenuItem>();
            for (int index = 0; index < 12; index++)
            {
                int captured = index;
                items.Add(MenuItem.Info(() =>
                {
                    if (Gunplay == null) { return captured == 0 ? "Gunplay script not running" : ""; }
                    List<string> lines = Gunplay.StatusLines();
                    return captured < lines.Count ? lines[captured] : "";
                }));
            }
            return items;
        }

        private List<MenuItem> BuildHelp()
        {
            return new List<MenuItem> {
                MenuItem.Info(() => "Open/close: hold L3+R3 or press F10"),
                MenuItem.Info(() => "Move: D-pad / arrows   Adjust: D-pad left/right"),
                MenuItem.Info(() => "Select: Cross/A / Enter   Back: Circle/B / Backspace"),
                MenuItem.Info(() => "Give actions ask for a second press to confirm"),
                MenuItem.Info(() => "Config: scripts\\LibertyFramework\\config\\gunplay.json"),
                MenuItem.Info(() => "Log: scripts\\LibertyFramework\\logs\\LibertyFramework.log"),
                MenuItem.Info(() => "Runtime: " + RuntimeProbe.ActiveProbeLabel + (IsOpen ? "  menu open" : "")) };
        }

        private MenuPage Current { get { return stack.Count > 0 ? stack.Peek() : root; } }

        // T-026: every tick's wall-clock cost goes to the shared CostMeter report.
        private void OnTick(object sender, EventArgs args)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            try { TickBody(sender, args); }
            finally { LibertyFramework.Core.Performance.Logic.CostMeter.Add("tick.devtools", started); }
        }

        private void TickBody(object sender, EventArgs args)
        {
            if (disabled)
            {
                if (controlLocked) { SafeRestoreControl(); }
                return;
            }
            try
            {
                if (!IsOpen && controlLocked) { RestorePlayerControl(); }
                controller.Poll();
                teleports.Update(Player);

                bool toggle = Game.isKeyPressed(Keys.F10);
                if (toggle && !previousToggle) { ToggleMenu(); }
                previousToggle = toggle;

                bool chord = controller.IsDown((ushort)(ControllerInput.LeftThumb | ControllerInput.RightThumb)) ||
                    (Game.isGameKeyPressed(GameKey.Crouch) && Game.isGameKeyPressed(GameKey.LookBehind));
                if (chord)
                {
                    if (chordStartUtc == DateTime.MinValue) { chordStartUtc = DateTime.UtcNow; }
                    if (!chordTriggered && (DateTime.UtcNow - chordStartUtc).TotalMilliseconds >= ChordHoldMilliseconds)
                    {
                        chordTriggered = true;
                        ToggleMenu();
                    }
                }
                else
                {
                    chordStartUtc = DateTime.MinValue;
                    chordTriggered = false;
                }

                bool up = Game.isKeyPressed(Keys.Up) || controller.IsDown(ControllerInput.DPadUp) || Game.isGameKeyPressed(GameKey.NavUp);
                bool down = Game.isKeyPressed(Keys.Down) || controller.IsDown(ControllerInput.DPadDown) || Game.isGameKeyPressed(GameKey.NavDown);
                bool left = Game.isKeyPressed(Keys.Left) || controller.IsDown(ControllerInput.DPadLeft) || Game.isGameKeyPressed(GameKey.NavLeft);
                bool right = Game.isKeyPressed(Keys.Right) || controller.IsDown(ControllerInput.DPadRight) || Game.isGameKeyPressed(GameKey.NavRight);
                bool activate = Game.isKeyPressed(Keys.Enter) || controller.IsDown(ControllerInput.AButton) || Game.isGameKeyPressed(GameKey.NavEnter);
                bool back = Game.isKeyPressed(Keys.Back) || controller.IsDown(ControllerInput.BButton);
                if (IsOpen)
                {
                    MenuPage page = Current;
                    if (up && !previousUp) { Move(page, -1); }
                    if (down && !previousDown) { Move(page, 1); }
                    HandleAdjust(page, left, right);
                    if (activate && !previousActivate) { Activate(page); }
                    if (back && !previousBack) { Back(); }
                }
                previousUp = up;
                previousDown = down;
                previousLeft = left;
                previousRight = right;
                previousActivate = activate;
                previousBack = back;
            }
            catch (Exception error)
            {
                DisableAfterError(error);
            }
        }

        private void HandleAdjust(MenuPage page, bool left, bool right)
        {
            int direction = right ? 1 : left ? -1 : 0;
            if (direction == 0) { return; }
            bool fresh = (right && !previousRight) || (left && !previousLeft);
            DateTime now = DateTime.UtcNow;
            if (fresh) { adjustHeldSinceUtc = now; lastRepeatUtc = now; }
            else if ((now - adjustHeldSinceUtc).TotalMilliseconds < RepeatDelayMilliseconds ||
                (now - lastRepeatUtc).TotalMilliseconds < RepeatIntervalMilliseconds) { return; }
            lastRepeatUtc = now;
            if (page.Items.Count == 0) { return; }
            MenuItem item = page.Items[page.Selected];
            if (item.Adjust == null) { return; }
            pendingConfirmation = null;
            message = Run(item.Adjust, direction);
        }

        private void Move(MenuPage page, int delta)
        {
            if (page.Items.Count == 0) { return; }
            page.Selected = (page.Selected + delta + page.Items.Count) % page.Items.Count;
            if (page.Selected < page.Scroll) { page.Scroll = page.Selected; }
            if (page.Selected >= page.Scroll + VisibleRows) { page.Scroll = page.Selected - VisibleRows + 1; }
            pendingConfirmation = null;
            message = "";
        }

        private void Activate(MenuPage page)
        {
            if (page.Items.Count == 0) { return; }
            MenuItem item = page.Items[page.Selected];
            if (item.Submenu != null)
            {
                item.Submenu.Rebuild();
                stack.Push(item.Submenu);
                message = "";
                pendingConfirmation = null;
                return;
            }
            if (item.Activate == null) { return; }
            if (item.RequiresConfirmation && pendingConfirmation != item)
            {
                pendingConfirmation = item;
                message = "Press Cross/A again to confirm";
                return;
            }
            pendingConfirmation = null;
            message = Run(item.Activate);
            RuntimeLog.Info("devtools_action page=" + page.Title + " item=" + item.Label() + " result=" + message);
        }

        private string Run(Func<string> action)
        {
            try { return action() ?? ""; }
            catch (Exception error)
            {
                RuntimeLog.Error("devtools_action_failed error=" + error);
                return "Action failed; see log";
            }
        }

        private string Run(Func<int, string> action, int direction)
        {
            try { return action(direction) ?? ""; }
            catch (Exception error)
            {
                RuntimeLog.Error("devtools_adjust_failed error=" + error);
                return "Adjust failed; see log";
            }
        }

        private void Back()
        {
            pendingConfirmation = null;
            message = "";
            if (stack.Count > 0) { stack.Pop(); }
            else { ToggleMenu(); }
        }

        private void ToggleMenu()
        {
            if (IsOpen)
            {
                IsOpen = false;
                pendingConfirmation = null;
                RestorePlayerControl();
                RuntimeLog.Info("devtools_menu_closed");
                return;
            }
            if (ArsenalCore.StorageOpen) { return; }
            if (Player != null && Player.CanControlCharacter)
            {
                controlLocked = true;
                Player.CanControlCharacter = false;
            }
            root.Rebuild();
            foreach (MenuPage page in stack) { page.Rebuild(); }
            IsOpen = true;
            message = "";
            RuntimeLog.Info("devtools_menu_opened controller_connected=" + controller.Connected + " player_control_locked=" + controlLocked);
        }

        private void RestorePlayerControl()
        {
            if (!controlLocked || Player == null) { return; }
            Player.CanControlCharacter = true;
            controlLocked = false;
        }

        private void SafeRestoreControl()
        {
            try { RestorePlayerControl(); }
            catch (Exception error) { RuntimeLog.Error("devtools_control_restore_failed error=" + error); }
        }

        private void OnDraw(object sender, GraphicsEventArgs args)
        {
            if (disabled || !IsOpen) { return; }
            try
            {
                GTA.Graphics graphics = args.Graphics;
                graphics.Scaling = FontScaling.Pixel;
                MenuPage page = Current;
                int rows = Math.Min(VisibleRows, page.Items.Count);
                float width = 620;
                float height = 118 + rows * 27 + 60;
                graphics.DrawRectangle(new RectangleF(36, 70, width, height), Color.FromArgb(205, 8, 12, 18));
                string breadcrumb = stack.Count == 0 ? "LIBERTY DEVTOOLS" : "LIBERTY DEVTOOLS  >  " + page.Title;
                graphics.DrawText(breadcrumb, new RectangleF(52, 82, width - 30, 28), TextAlignment.Left, titleFont);
                for (int row = 0; row < rows; row++)
                {
                    int index = page.Scroll + row;
                    if (index >= page.Items.Count) { break; }
                    float y = 120 + row * 27;
                    if (index == page.Selected)
                    {
                        graphics.DrawRectangle(new RectangleF(46, y - 2, width - 20, 26), Color.FromArgb(120, 190, 145, 35));
                    }
                    MenuItem item = page.Items[index];
                    string label;
                    try { label = item.Label(); }
                    catch (Exception error) { label = "(" + error.GetType().Name + ")"; }
                    graphics.DrawText(label, new RectangleF(58, y, width - 40, 25), TextAlignment.Left, font);
                }
                float footerY = 120 + rows * 27 + 8;
                if (page.Items.Count > VisibleRows)
                {
                    graphics.DrawText("(" + (page.Selected + 1) + "/" + page.Items.Count + ")", new RectangleF(width - 60, 84, 80, 24), TextAlignment.Left, font);
                }
                graphics.DrawText(message.Length > 0 ? message : "A select   B back   left/right adjust", new RectangleF(52, footerY, width - 30, 26), TextAlignment.Left, font);
                graphics.DrawText(controller.Connected ? "Controller ready; L3+R3 or F10 closes" : "Controller not detected; F10 and keyboard work",
                    new RectangleF(52, footerY + 28, width - 30, 26), TextAlignment.Left, font);
            }
            catch (Exception error)
            {
                // Drawing runs from ScriptHookDotNet's Direct3D hook, outside the script tick: no natives here.
                // OnTick sees 'disabled' and restores player control on the next tick.
                IsOpen = false;
                disabled = true;
                RuntimeLog.Error("devtools_disabled error=" + error);
            }
        }

        private void DisableAfterError(Exception error)
        {
            IsOpen = false;
            disabled = true;
            SafeRestoreControl();
            RuntimeLog.Error("devtools_disabled error=" + error);
        }

        private void OnDomainUnload(object sender, EventArgs args)
        {
            SafeRestoreControl();
            try { testRange.Clear(); }
            catch (Exception error) { RuntimeLog.Error("devtools_unload_cleanup_failed error=" + error.Message); }
        }
    }
}
