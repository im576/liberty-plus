using System;
using System.Collections.Generic;
using Liberty.Sdk;
using LibertyFramework.Arsenal.Contracts;
using LibertyFramework.Arsenal.Logic;
using LibertyFramework.Core.Config;
using LibertyFramework.Core.Logging;
using LibertyFramework.DevTools;
using LibertyFramework.Engine.Ui;
using LibertyFramework.Weapons.Logic;

namespace LibertyFramework.Arsenal.Ui
{
    // T-045: the weapon wheel shows the physical loadout (sidearm, two long guns, melee, thrown), opens while a button or key
    // is held and equips the highlighted slot on release. A tap keeps it open for stick / arrows and A / Enter; B / Backspace
    // closes without a change. It is a Liberty.Ui radial menu (the same component as the storage wheel). Bindings are the
    // "weaponWheel" block of arsenal.json.
    [global::Liberty.Sdk.Module("weapon-wheel", Order = 45, Capabilities = new[] { global::Liberty.Sdk.Capabilities.EngineInternal }, Description = "Liberty weapon wheel: switch between the carried loadout")]
    public sealed class WeaponWheelModule : LibertyFramework.Engine.Module
    {
        private WeaponWheelConfig config = WeaponWheelConfig.Defaults();
        private string configHash;
        private WeaponCatalog catalog;
        private bool catalogLoaded;
        private IMenu menu;
        private bool sticky;
        private bool wasDown;
        private int openedAtTicks, openedAtFrame, lastUpdateTicks;
        private bool firstDrawLogged;
        private int pendingEquipId, pendingEquipFrame;
        // Per-slot snapshot, refreshed at most once per frame (labels, icons and badges are asked for every frame).
        private readonly int[] ids = new int[WeaponWheelLogic.SegmentCount];
        private readonly string[] names = new string[WeaponWheelLogic.SegmentCount];
        private readonly string[] finishes = new string[WeaponWheelLogic.SegmentCount];
        private readonly string[] ammo = new string[WeaponWheelLogic.SegmentCount];
        private int snapshotFrame = -1, ammoReadTicks;

        public WeaponWheelModule()
        {
            // Every frame: the press must open the wheel in the frame it happens (the polling is two button/key reads).
            Interval = 0;
        }

        protected override void OnStart()
        {
            LoadConfig();
            Engine.ModuleConfig.WatchFile(this, LibertyPlus.Configuration.PlusPaths.ArsenalConfig, LoadConfig);
            Engine.Commands.Register(this, "wheel", "wheel [status] | open | select <slot 0-4> | confirm | close - weapon wheel (T-045)", WheelCommand);
        }

        protected override void OnStop() { CloseMenu("module stopped"); }

        protected internal override void OnUpdate()
        {
            if (pendingEquipId > 0 && Engine.Frame > pendingEquipFrame)
            {
                int actual = Liberty.Weapons.Current(Liberty.Player.Ped);
                RuntimeLog.Info("weapon_wheel_equipped requested=" + pendingEquipId + " actual=" + actual + " match=" + (actual == pendingEquipId));
                pendingEquipId = 0;
            }
            if (!config.Enabled) { CloseMenu("disabled"); wasDown = false; return; }
            bool down = Engine.Input.Down(config.Pad) || Engine.Input.KeyDown(config.Key);
            // Measure the observed press in monotonic wall time. Capping elapsed frames incorrectly turns a real hold
            // into a tap under load. Short presses wholly inside an unsampled stall remain unobservable.
            int now = Environment.TickCount;
            int gap = lastUpdateTicks == 0 ? 0 : unchecked(now - lastUpdateTicks);
            lastUpdateTicks = now;
            if (menu != null && gap > 200) { RuntimeLog.Info("weapon_wheel_hitch gap_ms=" + gap); }
            if (menu != null && !menu.IsOpen) { menu = null; sticky = false; }
            if (menu == null)
            {
                if (down && !wasDown && CanOpen()) { Open(false); }
            }
            else
            {
                if (!StillAllowed()) { CloseMenu("state changed"); }
                else
                {
                    ReportFirstDraw();
                    if (down && !wasDown && sticky) { ConfirmAndClose(); }
                    else if (!down && wasDown && !sticky)
                    {
                        int heldMs = unchecked(Environment.TickCount - openedAtTicks), heldFrames = Engine.Frame - openedAtFrame;
                        WeaponWheelLogic.Release decision = WeaponWheelLogic.OnRelease(heldMs, config.TapMilliseconds);
                        RuntimeLog.Info("weapon_wheel_release held_ms=" + heldMs + " held_frames=" + heldFrames + " tap_ms=" + config.TapMilliseconds + " decision=" + decision);
                        if (decision == WeaponWheelLogic.Release.StayOpen) { sticky = true; }
                        else { ConfirmAndClose(); }
                    }
                }
            }
            wasDown = down;
        }

        private void LoadConfig()
        {
            try
            {
                byte[] bytes = JsonStore.ReadBytes(LibertyPlus.Configuration.PlusPaths.ArsenalConfig);
                string hash = JsonStore.Hash(bytes);
                if (hash == configHash) { return; }
                ArsenalConfig parsed = JsonStore.Parse<ArsenalConfig>(bytes);
                WeaponWheelConfig candidate = parsed.WeaponWheel ?? WeaponWheelConfig.Defaults();
                candidate.Validate();
                config = candidate;
                configHash = hash;
                RuntimeLog.Info("weapon_wheel_config enabled=" + config.Enabled + " pad=" + config.PadButtonName + " key=" + config.KeyboardKeyName + " tap_ms=" + config.TapMilliseconds);
            }
            catch (Exception error) { RuntimeLog.Error("weapon_wheel_config_rejected keeping previous error=" + error.Message); }
        }

        private bool CanOpen()
        {
            PlayerState player = Liberty.World.Player;
            return ArsenalRegistry.CarriedWeapons != null && player.IsPlaying && !player.IsDead && player.HasControl &&
                (!player.InVehicle || config.AllowInVehicle) && !Engine.Ui.AnyMenuOpen && !DevToolsMenu.IsOpen && !ArsenalCore.StorageOpen;
        }

        // While open the wheel locks the player's control itself, so HasControl is not asked again.
        private bool StillAllowed()
        {
            PlayerState player = Liberty.World.Player;
            return player.IsPlaying && !player.IsDead && (!player.InVehicle || config.AllowInVehicle) && !DevToolsMenu.IsOpen && !ArsenalCore.StorageOpen;
        }

        private void Open(bool alreadySticky)
        {
            Refresh(true);
            RadialMenu radial = new RadialMenu();
            radial.Title = "WEAPONS";
            radial.Size = 0.62f;
            for (int i = 0; i < WeaponWheelLogic.SegmentCount; i++)
            {
                int slot = i;
                RadialSegment segment = new RadialSegment();
                segment.Label = () => { Refresh(false); return ids[slot] > 0 ? names[slot] : "-"; };
                segment.Icon = () => { Refresh(false); return ids[slot] > 0 ? Engine.Ui.WeaponIcon(ids[slot]) : TextureRef.None; };
                segment.Badge = () => { Refresh(false); return ids[slot] > 0 ? ammo[slot] : null; };
                radial.Segments.Add(segment);
            }
            radial.CenterLines = Centre;
            radial.OnAccept = slot => { Equip(slot, true); return null; };
            radial.OnClosed = () => { menu = null; sticky = false; };
            int held = Liberty.Weapons.Current(Liberty.Player.Ped);
            // Centre evaluates during OpenRadial, so tap/command hints must already reflect this opening.
            sticky = alreadySticky;
            firstDrawLogged = false;
            // Include initial snapshot preparation in the existing tick/wall-time measurement.
            openedAtTicks = Environment.TickCount;
            openedAtFrame = Engine.Frame;
            menu = Engine.Ui.OpenRadial(this, radial, WeaponWheelLogic.StartSegment(ids, held));
            if (!menu.IsOpen) { menu = null; return; }
            RuntimeLog.Info("weapon_wheel_open frame=" + openedAtFrame + " held=" + held + " slots=" + string.Join(",", Array.ConvertAll(ids, id => id.ToString())));
        }

        // The view's first draw happens on the draw thread; this module logs it from the tick, so the open-to-draw latency
        // (frames and ms) is in the log for the scenario and the review.
        private void ReportFirstDraw()
        {
            if (firstDrawLogged) { return; }
            RadialMenuView view = menu as RadialMenuView;
            if (view == null || view.FirstDrawFrame < 0) { return; }
            firstDrawLogged = true;
            RuntimeLog.Info("weapon_wheel_first_draw frames=" + (view.FirstDrawFrame - openedAtFrame) + " ms=" + (view.FirstDrawTicks - openedAtTicks));
        }

        private void ConfirmAndClose()
        {
            int slot = menu != null ? menu.Selected : 0;
            Equip(slot, true);
        }

        private void Equip(int slot, bool closeOnSuccess)
        {
            Refresh(true);
            if (slot < 0 || slot >= ids.Length || ids[slot] <= 0)
            {
                if (menu != null) { menu.Message("Slot empty"); }
                RuntimeLog.Info("weapon_wheel_equip slot=" + slot + " empty");
                if (closeOnSuccess) { CloseMenu("confirmed"); }
                return;
            }
            int target = ids[slot];
            int held = Liberty.Weapons.Current(Liberty.Player.Ped);
            // Release our menu's control lock before asking the game to equip, on both accept and hold-release paths.
            // Submission is not proof that the weapon in hand changed; read it back next frame.
            if (closeOnSuccess) { CloseMenu("confirmed"); }
            if (held == ids[slot]) { RuntimeLog.Info("weapon_wheel_equip slot=" + slot + " id=" + ids[slot] + " already_in_hand"); }
            else
            {
                Liberty.Weapons.Select(Liberty.Player.Ped, target);
                RuntimeLog.Info("weapon_wheel_equip slot=" + slot + " id=" + ids[slot] + " from=" + held);
            }
            pendingEquipId = target;
            pendingEquipFrame = Engine.Frame;
        }

        private void CloseMenu(string reason)
        {
            IMenu open = menu;
            menu = null;
            sticky = false;
            if (open != null && open.IsOpen)
            {
                open.Close();
                RuntimeLog.Info("weapon_wheel_closed reason=" + reason);
            }
        }

        private string[] Centre(int slot)
        {
            Refresh(false);
            string hints = sticky ? "A / Enter  Equip     B / Backspace  Close" : "Hold, pick, release to equip     B / Backspace  Close";
            if (ids[slot] <= 0) { return new[] { WeaponWheelLogic.SegmentTitles[slot], "Empty", "", "", hints }; }
            return new[] { WeaponWheelLogic.SegmentTitles[slot], names[slot], ammo[slot] ?? "", string.IsNullOrEmpty(finishes[slot]) ? "" : "Finish: " + finishes[slot], hints };
        }

        // Reads the carried weapons into the per-slot arrays: once per frame, and the ammunition at most every 250 ms (each
        // reading is a game native).
        private void Refresh(bool force)
        {
            if (!force && snapshotFrame == Engine.Frame) { return; }
            snapshotFrame = Engine.Frame;
            ICarriedWeaponsSource source = ArsenalRegistry.CarriedWeapons;
            if (source == null) { return; }
            int[] before = (int[])ids.Clone();
            Array.Clear(ids, 0, ids.Length);
            foreach (CarriedWeapon weapon in source.Carried)
            {
                int slot = WeaponWheelLogic.SegmentOf(weapon.Slot, weapon.Category);
                if (slot >= 0 && ids[slot] == 0) { ids[slot] = weapon.WeaponId; }
            }
            bool changed = false;
            for (int i = 0; i < ids.Length; i++) { if (ids[i] != before[i]) { changed = true; } }
            bool readAmmo = force || changed || unchecked(Environment.TickCount - ammoReadTicks) >= 250;
            if (readAmmo) { ammoReadTicks = Environment.TickCount; }
            PedRef ped = Liberty.Player.Ped;
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] <= 0) { names[i] = null; finishes[i] = null; ammo[i] = null; continue; }
                if (changed || names[i] == null) { names[i] = NameOf(ids[i]); finishes[i] = source.FinishOf(ids[i]); }
                if (readAmmo) { ammo[i] = AmmoText(ped, ids[i], i); }
            }
        }

        // Firearms: rounds in the magazine / reserve; melee and thrown show nothing (thrown shows its count).
        private string AmmoText(PedRef ped, int weaponId, int slot)
        {
            if (slot == WeaponWheelLogic.Melee) { return null; }
            int total = Liberty.Weapons.GetAmmo(ped, weaponId);
            if (slot == WeaponWheelLogic.Thrown) { return total > 0 ? "x" + total : null; }
            // The game reports a magazine only for the weapon in hand; the others show their total.
            int clip = weaponId == Liberty.Weapons.Current(ped) ? Liberty.Weapons.GetAmmoInClip(ped, weaponId) : -1;
            if (clip < 0 || total < clip) { return total + " rounds"; }
            return clip + " / " + (total - clip);
        }

        private string NameOf(int weaponId)
        {
            if (!catalogLoaded)
            {
                catalogLoaded = true;
                try { catalog = JsonStore.Load<WeaponCatalog>(LibertyPlus.Configuration.PlusPaths.WeaponCatalog); catalog.Validate(); }
                catch (Exception error) { catalog = null; RuntimeLog.Error("weapon_wheel_catalog_unavailable error=" + error.Message); }
            }
            WeaponCatalogEntry entry = catalog == null ? null : catalog.Find(weaponId);
            if (entry != null && !string.IsNullOrEmpty(entry.Label)) { return entry.Label; }
            LibertyFramework.Gunplay.GunplayController gunplay = LibertyFramework.Gunplay.GunplayController.Instance;
            return LibertyFramework.Weapons.TestWeaponActions.LabelFor(gunplay != null ? gunplay.Config : null, weaponId);
        }

        private string WheelCommand(string[] args)
        {
            string verb = args.Length > 0 ? args[0] : "status";
            if (verb == "open")
            {
                if (menu != null && menu.IsOpen) { return "weapon wheel already open"; }
                if (!CanOpen()) { return "weapon wheel cannot open now (menu, vehicle, cutscene or dead)"; }
                Open(true);
                return "weapon wheel open";
            }
            if (menu == null || !menu.IsOpen)
            {
                if (verb == "status") { return StatusLine(); }
                return "weapon wheel is not open";
            }
            if (verb == "select")
            {
                int slot;
                if (args.Length < 2 || !int.TryParse(args[1], out slot) || slot < 0 || slot >= WeaponWheelLogic.SegmentCount) { return "wheel select <slot 0-4>"; }
                menu.Selected = slot;
                return "weapon wheel highlight " + slot;
            }
            if (verb == "confirm") { ConfirmAndClose(); return "weapon wheel confirmed"; }
            if (verb == "close") { CloseMenu("command"); return "weapon wheel closed"; }
            return StatusLine();
        }

        private string StatusLine()
        {
            Refresh(true);
            return "weapon_wheel_status open=" + (menu != null && menu.IsOpen) + " selected=" + (menu != null ? menu.Selected : -1) + " sticky=" + sticky +
                " held=" + Liberty.Weapons.Current(Liberty.Player.Ped) + " slots=" + string.Join(",", Array.ConvertAll(ids, id => id.ToString())) +
                " enabled=" + config.Enabled;
        }
    }
}
