using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Keys = System.Windows.Forms.Keys;
using GTA;
using GTA.Native;
using LibertyFramework.Arsenal.Contracts;
using LibertyFramework.Arsenal.Logic;
using LibertyFramework.Core.Config;
using LibertyFramework.Core.Input;
using LibertyFramework.Core.Logging;
using LibertyFramework.DevTools;
using LibertyFramework.DevTools.Menu;
using LibertyFramework.Weapons.Logic;

namespace LibertyFramework.Arsenal
{
    // All game API access stays on Script ticks, including DevTools actions.
    [global::Liberty.Sdk.Module("arsenal", Order = 30, Capabilities = new[] { global::Liberty.Sdk.Capabilities.EngineInternal }, Description = "Arsenal: carried weapons, trunks, stashes, weapon wheel")]
    public sealed class ArsenalCore : LibertyFramework.Engine.Module, ICarriedWeaponsSource
    {
        internal static bool StorageOpen { get; private set; }
        private ArsenalConfig config;
        private WeaponCatalog weaponCatalog;
        private ArsenalState state;
        private string episode;
        private string statePath;
        private readonly List<WeaponRecord> carried = new List<WeaponRecord>();
        private readonly List<WeaponRecord> pendingReplacements = new List<WeaponRecord>();
        private readonly List<CarriedWeapon> presentation = new List<CarriedWeapon>();
        private readonly Dictionary<int, long> lastUsed = new Dictionary<int, long>();
        private readonly Dictionary<string, StorageBin> temporaryTrunks = new Dictionary<string, StorageBin>();
        private readonly Dictionary<string, Vehicle> temporaryVehicles = new Dictionary<string, Vehicle>();
        private readonly Dictionary<int, string> identifiedVehicles = new Dictionary<int, string>();
        private List<LvsOwnedVehicleEntry> lvsOwned = new List<LvsOwnedVehicleEntry>();
        private readonly string lvsPath = Path.Combine(LibertyPaths.GameDirectory, "scripts\\LibertyVehicleServicesCE.owned.ini");
        private DateTime lvsLastReadUtc = DateTime.MinValue;
        private bool disabled;
        private bool deadHandled;
        private int previousMoney = -1;
        private long moneyDecreaseAt = -1;
        private int revision;
        private Vehicle lastVehicle;
        private Vehicle openedTrunk;
        private List<SafehouseRule> discoveredSafehouses = new List<SafehouseRule>();
        private DateTime lastDiscoveryUtc = DateTime.MinValue;
        private bool discoveryDisabled;
        private bool overflowDeferredLogged;
        private readonly ControllerInput storageInput = new ControllerInput();
        private StorageBin activeStorage;
        private Vehicle nearbyTrunk;
        private SafehouseRule nearbySafehouse;
        // S-2/S-3: every container opens the weapon wheel (Liberty.Ui radial menu); trunks are used with Niko's own trunk
        // animations (engine choreography).
        private readonly LibertyFramework.Arsenal.Ui.StorageWheel wheel;
        private readonly LibertyFramework.Arsenal.Ui.TrunkSequence trunkAnimation;
        private WheelHost wheelHost;
        private string promptShown;
        private bool storageClosing;
        private bool storageControlLocked;
        private bool previousStorageKey;
        private int lastStorageScanTicks;
        private int lastStateReadTicks, lastReconcileTicks;
        private bool cachedArrested, cachedDead, cachedMission, cachedGated;
        private volatile bool inventoryDirty = true;
        private int lastSafehouseObserveTicks;
        private int lastTemporaryPruneTicks;
        private DateTime lastSnapshotUtc = DateTime.MinValue;

        public ArsenalCore()
        {
            Interval = 30;
            wheel = new LibertyFramework.Arsenal.Ui.StorageWheel(this);
            trunkAnimation = new LibertyFramework.Arsenal.Ui.TrunkSequence(this);
            wheelHost = new WheelHost(this);
            Tick += OnTick;
            AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
            DevToolsPages.Register("ARSENAL", BuildPage);
            RuntimeLog.Info("arsenal_started");
        }

        // Engine events that change what the player carries mark the inventory for re-reading on the next tick.
        protected override void OnStart()
        {
            Engine.Perf.ObserveUiBudgetState(this, () => "storage_open=" + StorageOpen + " storage_closing=" + storageClosing +
                " wheel_open=" + wheel.IsOpen + " storage_locked=" + storageControlLocked + " " + trunkAnimation.Observation());
            Engine.Events.Subscribe<global::Liberty.Sdk.Events.PlayerWeaponChanged>(this, e => inventoryDirty = true);
            Engine.Events.Subscribe<global::Liberty.Sdk.Events.PlayerShot>(this, e => inventoryDirty = true);
            Engine.Events.Subscribe<global::Liberty.Sdk.Events.ReloadFinished>(this, e => inventoryDirty = true);
            Engine.Events.Subscribe<global::Liberty.Sdk.Events.PlayerDied>(this, e => inventoryDirty = true);
            Engine.Commands.Register(this, "storage", "storage status | select <slot 0-4> | store | take [index] - the open trunk/stash interface through its own actions (T-046)", StorageCommand);
            Engine.Commands.Register(this, "arsenal","arsenal [status] | roundtrip - carried and stored weapons; roundtrip saves the state, loads it back and compares (T-044)", ArsenalCommand);
        }

        private string StorageCommand(string[] args)
        {
            string verb = args.Length > 0 ? args[0] : "status";
            if (verb == "state")
            {
                return "storage_state open=" + StorageOpen + " closing=" + storageClosing + " animation=" + trunkAnimation.Active +
                    " locked=" + storageControlLocked + " control=" + (Player != null && Player.CanControlCharacter);
            }
            int number;
            if (verb == "select") { return args.Length > 1 && int.TryParse(args[1], out number) ? wheel.SelectSegment(number) : "storage select <slot 0-4>"; }
            if (verb == "store") { return RunAction(() => wheel.StoreHighlighted()); }
            if (verb == "take") { number = 0; if (args.Length > 1) { int.TryParse(args[1], out number); } int index = number; return RunAction(() => wheel.TakeAt(index)); }
            return wheel.StatusLine();
        }

        // Test surface of T-044: what Niko carries and what every storage bin holds, and the save/load identity check.
        private string ArsenalCommand(string[] args)
        {
            if (state == null || config == null) { return "arsenal not ready"; }
            string verb = args.Length > 0 ? args[0] : "status";
            if (verb == "roundtrip")
            {
                inventoryDirty = true;
                Persist();
                ArsenalState loaded = ArsenalStateStore.LoadOrEmpty(statePath, error => RuntimeLog.Error("arsenal_roundtrip_load_failed error=" + error));
                string before = Summarize(state), after = Summarize(loaded);
                return "arsenal_roundtrip identical=" + (before == after) + " records=" + (loaded.CarriedRecords.Count + StoredCount(loaded)) +
                    (before == after ? "" : " before=" + before + " after=" + after);
            }
            if (verb != "status") { return "arsenal [status] | roundtrip"; }
            int ownedCarried = 0;
            List<string> carriedText = new List<string>();
            foreach (WeaponRecord record in carried)
            {
                if (record.Owned) { ownedCarried++; }
                carriedText.Add(record.WeaponId + "/" + record.Category + "/" + (record.Owned ? "owned" : "found") + "/" + record.Ammo);
            }
            List<string> bins = new List<string>();
            int ownedStored = 0;
            foreach (StorageBin bin in state.SafehouseStashes) { bins.Add("stash:" + bin.Id + ":" + bin.Weapons.Count); ownedStored += bin.Weapons.Count; }
            foreach (StorageBin bin in state.VehicleTrunks) { bins.Add("trunk:" + bin.Id + ":" + bin.Weapons.Count); ownedStored += bin.Weapons.Count; }
            return "arsenal_status loadout=" + (ArsenalPolicy.LoadoutActive(config) ? "on" : "off") + " episode=" + episode +
                " carried=" + (carriedText.Count == 0 ? "none" : string.Join(",", carriedText.ToArray())) + " owned_carried=" + ownedCarried +
                " stored=" + ownedStored + " owned_total=" + (ownedCarried + ownedStored) + " bins=" + (bins.Count == 0 ? "none" : string.Join(",", bins.ToArray())) +
                " last_safehouse=" + (string.IsNullOrEmpty(state.LastSafehouseId) ? "none" : state.LastSafehouseId) + " disabled=" + disabled;
        }

        private static int StoredCount(ArsenalState source)
        {
            int count = 0;
            foreach (StorageBin bin in source.SafehouseStashes) { count += bin.Weapons.Count; }
            foreach (StorageBin bin in source.VehicleTrunks) { count += bin.Weapons.Count; }
            return count;
        }

        // Every record with its identity, in order: two states are "identical" when these lines match.
        private static string Summarize(ArsenalState source)
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            text.Append("last=" + source.LastSafehouseId + "|" + source.LastVehicleKey + "|owned=" + string.Join(",", source.OwnedCarried.ConvertAll(id => id.ToString()).ToArray()));
            foreach (WeaponRecord record in source.CarriedRecords) { text.Append(";c:" + Line(record)); }
            foreach (StorageBin bin in source.SafehouseStashes) { foreach (WeaponRecord record in bin.Weapons) { text.Append(";s:" + bin.Id + ":" + Line(record)); } }
            foreach (StorageBin bin in source.VehicleTrunks) { foreach (WeaponRecord record in bin.Weapons) { text.Append(";t:" + bin.Id + ":" + Line(record)); } }
            return text.ToString();
        }

        private static string Line(WeaponRecord record)
        {
            return record.WeaponId + "/" + record.Category + "/" + record.Ammo + "/" + record.Owned + "/" + record.InstanceId + "/" + record.Finish + "/" + record.CatalogId;
        }

        int ICarriedWeaponsSource.Revision { get { return revision; } }
        IList<CarriedWeapon> ICarriedWeaponsSource.Carried { get { return new List<CarriedWeapon>(presentation); } }
        string ICarriedWeaponsSource.FinishOf(int weaponId)
        {
            WeaponRecord record = Find(carried, weaponId);
            return record != null && record.Finish != null ? record.Finish : "";
        }
        double ICarriedWeaponsSource.PerShotBloomMultiplier(int weaponId)
        {
            if (weaponCatalog == null) { return 1.0; }
            WeaponRecord record = Find(carried, weaponId);
            if (record == null || record.Attachments == null) { return 1.0; }
            double multiplier = 1.0;
            foreach (string id in record.Attachments)
            {
                AttachmentOption option = weaponCatalog.FindAttachment(id);
                if (option != null) { multiplier *= option.PerShotBloomMultiplier; }
            }
            return multiplier;
        }

        // T-026: every tick's wall-clock cost goes to the shared CostMeter report.
        private void OnTick(object sender, EventArgs args)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            try { TickBody(sender, args); }
            finally { LibertyFramework.Core.Performance.Logic.CostMeter.Add("tick.arsenal", started); }
        }

        private void TickBody(object sender, EventArgs args)
        {
            if (disabled) { if (storageControlLocked) { CloseStorageSafely(); } return; }
            try
            {
                if (config == null) { Initialize(); }
                if (config == null || Player == null || Player.Character == null) { return; }
                Ped ped = Player.Character;
                int money = Player.Money;
                if (previousMoney >= 0 && money < previousMoney) { moneyDecreaseAt = Environment.TickCount; }
                previousMoney = money;
                long probe = System.Diagnostics.Stopwatch.GetTimestamp();
                RefreshLvs();
                LibertyFramework.Core.Performance.Logic.CostMeter.Add("ar.lvs", probe); probe = System.Diagnostics.Stopwatch.GetTimestamp();
                ObserveVehicle(ped);
                LibertyFramework.Core.Performance.Logic.CostMeter.Add("ar.vehicle", probe); probe = System.Diagnostics.Stopwatch.GetTimestamp();
                DiscoverSafehouses();
                LibertyFramework.Core.Performance.Logic.CostMeter.Add("ar.discover", probe); probe = System.Diagnostics.Stopwatch.GetTimestamp();
                int nowTicks = Environment.TickCount;
                if (lastSafehouseObserveTicks == 0 || unchecked(nowTicks - lastSafehouseObserveTicks) >= 250)
                {
                    lastSafehouseObserveTicks = nowTicks;
                    ObserveSafehouse(ped);
                }
                if (lastTemporaryPruneTicks == 0 || unchecked(nowTicks - lastTemporaryPruneTicks) >= 1000)
                {
                    lastTemporaryPruneTicks = nowTicks;
                    PruneTemporaryTrunks();
                }
                if (openedTrunk != null && activeStorage == null && !DevToolsMenu.IsOpen) { CloseTrunk(); }

                // Arrest/death/mission/cutscene/fade state: seven SHDN natives (about 0.15-0.4 ms each), refreshed at most every stateRefresh ms (default 500).
                int stateRefresh = config.StateRefreshMilliseconds > 0 ? config.StateRefreshMilliseconds : 500;
                if (lastStateReadTicks == 0 || unchecked(nowTicks - lastStateReadTicks) >= stateRefresh)
                {
                    lastStateReadTicks = nowTicks;
                    cachedArrested = Function.Call<bool>("IS_PLAYER_BEING_ARRESTED");
                    cachedDead = Function.Call<bool>("IS_PLAYER_DEAD", Player.ID);
                    cachedMission = Function.Call<bool>("GET_MISSION_FLAG");
                    cachedGated = !ArsenalPolicy.MayMoveWeapons(cachedMission, (Function.Call<bool>("HAS_CUTSCENE_LOADED") && !Function.Call<bool>("HAS_CUTSCENE_FINISHED")) ||
                        Function.Call<bool>("IS_SCREEN_FADING") || Function.Call<bool>("IS_SCREEN_FADED_OUT"));
                }
                bool arrested = cachedArrested, dead = cachedDead, mission = cachedMission, gated = cachedGated;
                if (arrested || dead)
                {
                    CloseStorage();
                    if (!deadHandled) { HandleLoss(arrested); deadHandled = true; }
                    return;
                }
                deadHandled = false;
                LibertyFramework.Core.Performance.Logic.CostMeter.Add("ar.safehouse_flags", probe); probe = System.Diagnostics.Stopwatch.GetTimestamp();
                // The inventory is re-read when the engine reports a weapon change, shot or reload, and at least every
                // inventoryRefresh ms for pickups and purchases (which change it without an event).
                int inventoryRefresh = config.InventoryRefreshMilliseconds > 0 ? config.InventoryRefreshMilliseconds : 500;
                if (inventoryDirty || lastReconcileTicks == 0 || unchecked(nowTicks - lastReconcileTicks) >= inventoryRefresh)
                {
                    inventoryDirty = false;
                    lastReconcileTicks = nowTicks;
                    Reconcile(ped, mission, gated);
                }
                LibertyFramework.Core.Performance.Logic.CostMeter.Add("ar.reconcile", probe); probe = System.Diagnostics.Stopwatch.GetTimestamp();
                UpdateStorageInteraction(ped, gated);
                LibertyFramework.Core.Performance.Logic.CostMeter.Add("ar.storage", probe);
            }
            catch (Exception error)
            {
                Disable(error);
            }
        }

        private void Initialize()
        {
            config = JsonStore.Load<ArsenalConfig>(LibertyPlus.Configuration.PlusPaths.ArsenalConfig);
            ArsenalConfigValidator.Validate(config);
            try
            {
                weaponCatalog = JsonStore.Load<WeaponCatalog>(LibertyPlus.Configuration.PlusPaths.WeaponCatalog);
                weaponCatalog.Validate();
            }
            catch (Exception error)
            {
                weaponCatalog = null;
                RuntimeLog.Error("arsenal_catalog_unavailable legacy_weapons_active error=" + error);
            }
            int index = Function.Call<int>("GET_CURRENT_EPISODE");
            episode = index == 0 ? "iv" : index == 1 ? "tlad" : index == 2 ? "tbogt" : "episode_" + index;
            statePath = LibertyPlus.Configuration.PlusPaths.ArsenalState(episode);
            state = ArsenalStateStore.LoadOrEmpty(statePath,
                error => RuntimeLog.Error("arsenal_state_corrupt starting_empty path=" + statePath + " error=" + error));
            WeaponIdentity.Normalize(state);
            ArsenalRegistry.CarriedWeapons = this;
            RuntimeLog.Info("arsenal_ready episode=" + episode + " state=" + statePath);
        }

        private static WeaponCategory Category(WeaponSlot slot)
        {
            switch (slot)
            {
                case WeaponSlot.Melee: return WeaponCategory.Melee;
                case WeaponSlot.Handgun: return WeaponCategory.Handgun;
                case WeaponSlot.Shotgun: return WeaponCategory.Shotgun;
                case WeaponSlot.SMG: return WeaponCategory.SMG;
                case WeaponSlot.Rifle: return WeaponCategory.Rifle;
                case WeaponSlot.Sniper: return WeaponCategory.Sniper;
                case WeaponSlot.Heavy: return WeaponCategory.Heavy;
                case WeaponSlot.Thrown: return WeaponCategory.Thrown;
                default: return WeaponCategory.Other;
            }
        }

        private static List<WeaponRecord> ReadInventory(Ped ped)
        {
            List<WeaponRecord> result = new List<WeaponRecord>();
            foreach (WeaponSlot slot in new WeaponSlot[] { WeaponSlot.Melee, WeaponSlot.Handgun, WeaponSlot.Shotgun,
                WeaponSlot.SMG, WeaponSlot.Rifle, WeaponSlot.Sniper, WeaponSlot.Heavy, WeaponSlot.Thrown })
            {
                GTA.value.Weapon weapon = ped.Weapons.inSlot(slot);
                if (weapon == null || !weapon.isPresent || weapon.Type == Weapon.Unarmed || weapon.Type == Weapon.None) { continue; }
                WeaponRecord record = new WeaponRecord(); record.WeaponId = (int)weapon.Type;
                record.Category = Category(slot); record.Ammo = weapon.Ammo;
                result.Add(record);
            }
            return result;
        }

        private void Reconcile(Ped ped, bool mission, bool gated)
        {
            List<WeaponRecord> observed = ReadInventory(ped);
            int current = (int)ped.Weapons.CurrentType;
            if (current > 0) { lastUsed[current] = Environment.TickCount; }
            foreach (WeaponRecord prior in carried)
            {
                if (Find(observed, prior.WeaponId) != null) { continue; }
                bool replaced = false;
                foreach (WeaponRecord gained in observed) { if (gained.Category == prior.Category && Find(carried, gained.WeaponId) == null) { replaced = true; break; } }
                if (prior.Owned && replaced && !gated)
                {
                    StorageBin destination = OverflowDestination();
                    if (destination != null) { destination.Weapons.Add(prior.Clone()); Persist(); RuntimeLog.Info("arsenal_replaced_owned id=" + prior.WeaponId + " to=" + destination.Id); }
                }
                else if (prior.Owned && replaced) { pendingReplacements.Add(prior.Clone()); RuntimeLog.Info("arsenal_replacement_deferred id=" + prior.WeaponId); }
                state.OwnedCarried.Remove(prior.WeaponId);
            }
            foreach (WeaponRecord record in observed)
            {
                WeaponRecord prior = Find(carried, record.WeaponId);
                if (prior == null) { prior = Find(state.CarriedRecords, record.WeaponId); }
                if (prior != null)
                {
                    record.Owned = prior.Owned; record.Finish = prior.Finish; record.AcquiredUtc = prior.AcquiredUtc;
                    record.InstanceId = prior.InstanceId; record.CatalogId = prior.CatalogId;
                    record.Attachments = prior.Attachments == null ? null : new List<string>(prior.Attachments);
                    record.Progression = prior.Progression;
                }
                else
                {
                    record.Owned = state.OwnedCarried.Contains(record.WeaponId) ||
                        ArsenalPolicy.IsOwnedGain(false, mission, Environment.TickCount, moneyDecreaseAt, config.PurchaseWindowMilliseconds);
                    record.AcquiredUtc = DateTime.UtcNow.ToString("o");
                    if (record.Owned && !state.OwnedCarried.Contains(record.WeaponId)) { state.OwnedCarried.Add(record.WeaponId); Persist(); }
                    RuntimeLog.Info("arsenal_gain id=" + record.WeaponId + " owned=" + record.Owned + " mission=" + mission);
                }
                WeaponIdentity.Ensure(record);
                WeaponCatalogEntry entry = weaponCatalog == null ? null : weaponCatalog.Find(record.WeaponId);
                if (entry != null && string.IsNullOrEmpty(record.CatalogId))
                {
                    record.CatalogId = entry.Id;
                    if (string.IsNullOrEmpty(record.Finish)) { record.Finish = entry.Finishes[0]; }
                }
            }
            // Mission weapons and cutscenes are the game's business: the ammunition limit only applies in free play.
            if (!mission && !gated) { ClampAmmunition(ped, observed); }
            SetCarried(observed, current);
            if (CarriedIdentityChanged()) { Persist(); }
            if ((DateTime.UtcNow - lastSnapshotUtc).TotalSeconds >= 5)
            {
                if (CarriedSnapshotChanged()) { Persist(); }
                lastSnapshotUtc = DateTime.UtcNow;
            }
            if (gated) { return; }
            if (pendingReplacements.Count > 0)
            {
                StorageBin replacementDestination = OverflowDestination();
                if (replacementDestination != null)
                {
                    foreach (WeaponRecord pending in pendingReplacements)
                    {
                        replacementDestination.Weapons.Add(pending);
                        RuntimeLog.Info("arsenal_replaced_owned id=" + pending.WeaponId + " to=" + replacementDestination.Id);
                    }
                    pendingReplacements.Clear(); Persist();
                }
            }
            int overflow;
            while ((overflow = ArsenalPolicy.OverflowIndex(config, carried, lastUsed)) >= 0)
            {
                StorageBin destination = OverflowDestination();
                if (destination == null)
                {
                    // Logged once per episode of waiting: this loop runs every tick until a car or safehouse is known.
                    if (!overflowDeferredLogged) { overflowDeferredLogged = true; RuntimeLog.Info("arsenal_overflow_deferred no_vehicle_or_safehouse"); }
                    break;
                }
                overflowDeferredLogged = false;
                WeaponRecord moved = carried[overflow];
                ArsenalRegistry.RaiseWeaponsRemoving("overflow");
                ped.Weapons.FromType((Weapon)moved.WeaponId).Remove();
                moved.Owned = true;
                destination.Weapons.Add(moved.Clone());
                state.OwnedCarried.Remove(moved.WeaponId);
                RuntimeLog.Info("arsenal_overflow id=" + moved.WeaponId + " to=" + destination.Id);
                carried.RemoveAt(overflow);
                Persist();
                RefreshPresentation((int)ped.Weapons.CurrentType);
            }
        }

        // T-044 limited carried ammunition: rounds beyond the loadout's cap for the weapon's category are removed.
        private void ClampAmmunition(Ped ped, List<WeaponRecord> observed)
        {
            foreach (WeaponRecord record in observed)
            {
                int cap = ArsenalPolicy.AmmoCap(config, record.Category);
                if (cap < 0 || record.Ammo <= cap) { continue; }
                GTA.value.Weapon weapon = ped.Weapons.FromType((Weapon)record.WeaponId);
                if (weapon == null || !weapon.isPresent) { continue; }
                weapon.Ammo = cap;
                RuntimeLog.Info("arsenal_ammo_capped id=" + record.WeaponId + " category=" + record.Category + " from=" + record.Ammo + " to=" + cap);
                record.Ammo = cap;
            }
        }

        private static WeaponRecord Find(IList<WeaponRecord> records, int id)
        {
            foreach (WeaponRecord record in records) { if (record.WeaponId == id) { return record; } }
            return null;
        }

        private void SetCarried(List<WeaponRecord> observed, int current)
        {
            bool changed = observed.Count != carried.Count;
            foreach (WeaponRecord record in observed) { if (Find(carried, record.WeaponId) == null) { changed = true; } }
            carried.Clear(); carried.AddRange(observed);
            if (changed) { RefreshPresentation(current); }
            else
            {
                foreach (CarriedWeapon item in presentation)
                {
                    if (item.InHand != (item.WeaponId == current)) { RefreshPresentation(current); break; }
                }
            }
        }

        private void RefreshPresentation(int current)
        {
            presentation.Clear();
            bool longOneUsed = false, longTwoUsed = false, sideOneUsed = false, sideTwoUsed = false;
            foreach (WeaponRecord record in carried)
            {
                string group = ArsenalPolicy.Group(config, record.Category);
                BodySlot slot = ArsenalPolicy.Slot(config, record.Category);
                if (group == "longGun")
                {
                    if (slot == BodySlot.LongGun1 && longOneUsed) { slot = BodySlot.LongGun2; }
                    else if (slot == BodySlot.LongGun2 && longTwoUsed) { slot = BodySlot.LongGun1; }
                    if (slot == BodySlot.LongGun1) { longOneUsed = true; } else { longTwoUsed = true; }
                }
                if (group == "sidearm")
                {
                    if (slot == BodySlot.SidearmPrimary && sideOneUsed) { slot = BodySlot.SidearmSecondary; }
                    else if (slot == BodySlot.SidearmSecondary && sideTwoUsed) { slot = BodySlot.SidearmPrimary; }
                    if (slot == BodySlot.SidearmPrimary) { sideOneUsed = true; } else { sideTwoUsed = true; }
                }
                presentation.Add(new CarriedWeapon(record.WeaponId, record.Category, slot, record.WeaponId == current));
            }
            revision++;
        }

        private void HandleLoss(bool busted)
        {
            ArsenalRegistry.RaiseWeaponsRemoving(busted ? "busted" : "wasted");
            string safehouseId = state.LastSafehouseId;
            if (!busted && string.IsNullOrEmpty(safehouseId))
            {
                // No safehouse visited yet this episode: the nearest known one keeps owned weapons from vanishing.
                SafehouseRule nearest = NearestSafehouse(Player.Character.Position);
                if (nearest != null) { safehouseId = nearest.Id; RuntimeLog.Info("arsenal_loss_nearest_safehouse id=" + nearest.Id); }
            }
            // T-044 inventory integrity: with no safehouse known yet the owned weapons still go somewhere: a stash with no
            // address, which the first safehouse Niko reaches adopts (AdoptUnassignedStash).
            if (!busted && string.IsNullOrEmpty(safehouseId)) { safehouseId = UnassignedStashId; RuntimeLog.Info("arsenal_loss_unassigned_stash no safehouse known yet"); }
            StorageBin destination = !busted ? ArsenalPolicy.FindOrAdd(state.SafehouseStashes, safehouseId) : null;
            foreach (WeaponRecord record in carried)
            {
                RuntimeLog.Info("arsenal_loss reason=" + (busted ? "busted" : "wasted") + " id=" + record.WeaponId + " owned=" + record.Owned);
                try
                {
                    GTA.value.Weapon weapon = Player.Character.Weapons.FromType((Weapon)record.WeaponId);
                    if (weapon.isPresent) { weapon.Remove(); }
                }
                catch (Exception error) { RuntimeLog.Error("arsenal_loss_remove_failed id=" + record.WeaponId + " error=" + error); }
            }
            ArsenalPolicy.ResolveLoss(carried, busted, destination);
            pendingReplacements.Clear();
            state.OwnedCarried.Clear();
            state.CarriedRecords.Clear();
            presentation.Clear(); revision++;
            Persist();
        }

        private List<SafehouseRule> AllSafehouses()
        {
            List<SafehouseRule> all = new List<SafehouseRule>(config.Safehouses);
            all.AddRange(discoveredSafehouses);
            return all;
        }

        private SafehouseRule NearestSafehouse(Vector3 position)
        {
            SafehouseRule best = null;
            double bestDistance = double.MaxValue;
            foreach (SafehouseRule house in AllSafehouses())
            {
                if (!string.Equals(house.Episode, episode, StringComparison.OrdinalIgnoreCase)) { continue; }
                double distance = Distance(position, house.X, house.Y, house.Z);
                if (distance < bestDistance) { bestDistance = distance; best = house; }
            }
            return best;
        }

        // The game marks every unlocked safehouse on the radar with its own sprite. Reading those blips gives
        // real, story-aware safehouse positions without shipping coordinates. Runs every 10 s on the tick.
        private void DiscoverSafehouses()
        {
            if (discoveryDisabled || config.SafehouseBlipSprite <= 0 || (DateTime.UtcNow - lastDiscoveryUtc).TotalSeconds < 10) { return; }
            lastDiscoveryUtc = DateTime.UtcNow;
            try
            {
                List<SafehouseRule> found = new List<SafehouseRule>();
                int blip = Function.Call<int>("GET_FIRST_BLIP_INFO_ID", config.SafehouseBlipSprite);
                for (int guard = 0; blip != 0 && guard < 32; guard++)
                {
                    if (Function.Call<bool>("DOES_BLIP_EXIST", blip))
                    {
                        Pointer coords = typeof(Vector3);
                        Function.Call("GET_BLIP_COORDS", blip, coords);
                        Vector3 position = (Vector3)coords;
                        if (Math.Abs(position.X) > 1 || Math.Abs(position.Y) > 1)
                        {
                            SafehouseRule house = new SafehouseRule();
                            house.Id = "blip_" + episode + "_" + ((int)Math.Round(position.X)) + "_" + ((int)Math.Round(position.Y));
                            house.Name = "Safehouse (map)";
                            house.Episode = episode;
                            house.X = position.X; house.Y = position.Y; house.Z = position.Z;
                            house.Radius = config.DiscoveredSafehouseRadiusMeters;
                            house.Verified = true;
                            found.Add(house);
                        }
                    }
                    blip = Function.Call<int>("GET_NEXT_BLIP_INFO_ID", config.SafehouseBlipSprite);
                }
                if (found.Count != discoveredSafehouses.Count) { RuntimeLog.Info("arsenal_safehouses_discovered count=" + found.Count); }
                discoveredSafehouses = found;
            }
            catch (Exception error)
            {
                discoveryDisabled = true;
                RuntimeLog.Error("arsenal_safehouse_discovery_disabled use Mark safehouse here error=" + error.Message);
            }
        }

        private const string UnassignedStashId = "unassigned";

        // Weapons stored before any safehouse was known join the first safehouse Niko reaches (nothing is lost, nothing duplicated).
        private void AdoptUnassignedStash(string safehouseId)
        {
            StorageBin unassigned = state.SafehouseStashes.Find(bin => bin.Id == UnassignedStashId);
            if (unassigned == null || unassigned.Weapons.Count == 0) { return; }
            StorageBin home = ArsenalPolicy.FindOrAdd(state.SafehouseStashes, safehouseId);
            home.Weapons.AddRange(unassigned.Weapons);
            RuntimeLog.Info("arsenal_unassigned_adopted count=" + unassigned.Weapons.Count + " by=" + safehouseId);
            state.SafehouseStashes.Remove(unassigned);
        }

        private void ObserveSafehouse(Ped ped)
        {
            foreach (SafehouseRule house in AllSafehouses())
            {
                if (!string.Equals(house.Episode, episode, StringComparison.OrdinalIgnoreCase)) { continue; }
                if (Distance(ped.Position, house.X, house.Y, house.Z) <= house.Radius && state.LastSafehouseId != house.Id)
                {
                    state.LastSafehouseId = house.Id; AdoptUnassignedStash(house.Id); Persist(); RuntimeLog.Info("arsenal_safehouse_enter id=" + house.Id);
                }
            }
        }

        private void ObserveVehicle(Ped ped)
        {
            Vehicle current = ped.CurrentVehicle;
            if (current != null && current.Exists()) { lastVehicle = current; }
            if (current == null && lastVehicle != null && lastVehicle.Exists())
            {
                string key = VehicleKey(lastVehicle);
                if (key != null && key.StartsWith("lvs:") && state.LastVehicleKey != key) { state.LastVehicleKey = key; Persist(); }
                else if (key != null && temporaryTrunks.ContainsKey(key))
                {
                    string marker = "fallback:" + lastVehicle.Model.Hash + ":" + lastVehicle.Position.X.ToString("0.0") + ":" + lastVehicle.Position.Y.ToString("0.0");
                    if (state.LastVehicleKey == marker) { return; }
                    state.LastVehicleKey = marker;
                    state.FallbackModelHash = lastVehicle.Model.Hash;
                    state.FallbackX = lastVehicle.Position.X; state.FallbackY = lastVehicle.Position.Y; state.FallbackZ = lastVehicle.Position.Z;
                    StorageBin stored = ArsenalPolicy.FindOrAdd(state.VehicleTrunks, state.LastVehicleKey);
                    stored.Weapons.Clear(); foreach (WeaponRecord weapon in temporaryTrunks[key].Weapons) { stored.Weapons.Add(weapon.Clone()); }
                    Persist();
                }
            }
        }

        private void RefreshLvs()
        {
            if ((DateTime.UtcNow - lvsLastReadUtc).TotalSeconds < 5) { return; }
            lvsLastReadUtc = DateTime.UtcNow;
            if (!File.Exists(lvsPath)) { lvsOwned.Clear(); return; }
            try { lvsOwned = LvsOwnedVehicleReader.Parse(File.ReadAllText(lvsPath)); }
            catch (Exception error) { lvsOwned.Clear(); RuntimeLog.Error("arsenal_lvs_read_failed fallback_enabled error=" + error); }
        }

        private string VehicleKey(Vehicle vehicle)
        {
            if (vehicle == null || !vehicle.Exists()) { return null; }
            string id = LvsOwnedVehicleReader.Match(lvsOwned, episode, vehicle.Model.Hash, vehicle.Position.X,
                vehicle.Position.Y, vehicle.Position.Z, config.OwnedVehicleMatchMeters);
            if (id != null) { identifiedVehicles[vehicle.GetHashCode()] = id; return "lvs:" + id; }
            if (identifiedVehicles.TryGetValue(vehicle.GetHashCode(), out id)) { return "lvs:" + id; }
            if (vehicle.Model.Hash == state.FallbackModelHash &&
                Distance(vehicle.Position, state.FallbackX, state.FallbackY, state.FallbackZ) <= config.FallbackVehicleMatchMeters)
                { return state.LastVehicleKey; }
            return "temporary:" + vehicle.GetHashCode().ToString("X8");
        }

        // T-046: weapons the open container holds at most (0 = unlimited): the trunk's by vehicle class, the stash's from config.
        private TrunkCapacityRules capacityRules;
        private Dictionary<int, int> trunkSlotsByModel;

        private int ContainerCapacity()
        {
            if (capacityRules == null) { capacityRules = config.TrunkCapacity ?? TrunkCapacityRules.Defaults(); }
            if (openedTrunk == null) { return capacityRules.SafehouseSlots; }
            if (trunkSlotsByModel == null)
            {
                trunkSlotsByModel = new Dictionary<int, int>();
                if (capacityRules.Classes != null)
                {
                    foreach (TrunkClass vehicleClass in capacityRules.Classes)
                    {
                        foreach (string name in vehicleClass.Models)
                        {
                            Model model = new Model(name);
                            if (!model.isValid) { RuntimeLog.Error("arsenal_trunk_class_unknown_model class=" + vehicleClass.Id + " model=" + name); continue; }
                            trunkSlotsByModel[model.Hash] = vehicleClass.Slots;
                        }
                    }
                }
            }
            int slots;
            return trunkSlotsByModel.TryGetValue(openedTrunk.Model.Hash, out slots) ? slots : capacityRules.DefaultSlots;
        }

        protected override void OnDraw(global::Liberty.Sdk.ICanvas canvas) { wheel.Draw(canvas); }

        private StorageBin Trunk(Vehicle vehicle)
        {
            string key = VehicleKey(vehicle);
            if (key == null) { return null; }
            if (!key.StartsWith("temporary:")) { return ArsenalPolicy.FindOrAdd(state.VehicleTrunks, key); }
            StorageBin bin;
            if (!temporaryTrunks.TryGetValue(key, out bin)) { bin = new StorageBin(); bin.Id = key; temporaryTrunks.Add(key, bin); temporaryVehicles.Add(key, vehicle); }
            return bin;
        }

        private StorageBin OverflowDestination()
        {
            if (lastVehicle != null && lastVehicle.Exists() && lastVehicle.Health > 0 && !Function.Call<bool>("IS_CAR_IN_WATER", lastVehicle)) { return Trunk(lastVehicle); }
            if (!string.IsNullOrEmpty(state.LastVehicleKey) && !state.LastVehicleKey.StartsWith("temporary:"))
                { return ArsenalPolicy.FindOrAdd(state.VehicleTrunks, state.LastVehicleKey); }
            if (!string.IsNullOrEmpty(state.LastSafehouseId)) { return ArsenalPolicy.FindOrAdd(state.SafehouseStashes, state.LastSafehouseId); }
            return null;
        }

        private void PruneTemporaryTrunks()
        {
            List<string> gone = new List<string>();
            foreach (KeyValuePair<string, Vehicle> pair in temporaryVehicles)
            {
                if (!pair.Value.Exists() || pair.Value.Health <= 0 || pair.Value.isOnFire || Function.Call<bool>("IS_CAR_IN_WATER", pair.Value)) { gone.Add(pair.Key); }
            }
            foreach (string key in gone) { RuntimeLog.Info("arsenal_temporary_trunk_lost id=" + key); temporaryTrunks.Remove(key); temporaryVehicles.Remove(key); }
        }

        private static double Distance(Vector3 position, float x, float y, float z)
        {
            double dx = position.X - x, dy = position.Y - y, dz = position.Z - z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private void SnapshotCarried()
        {
            state.CarriedRecords.Clear();
            foreach (WeaponRecord record in carried) { state.CarriedRecords.Add(record.Clone()); }
        }

        private bool CarriedSnapshotChanged()
        {
            if (CarriedIdentityChanged()) { return true; }
            foreach (WeaponRecord record in carried)
            {
                WeaponRecord saved = Find(state.CarriedRecords, record.WeaponId);
                if (saved == null || saved.Ammo != record.Ammo || saved.Finish != record.Finish ||
                    saved.CatalogId != record.CatalogId || saved.Progression != record.Progression)
                    { return true; }
                if ((saved.Attachments == null ? 0 : saved.Attachments.Count) !=
                    (record.Attachments == null ? 0 : record.Attachments.Count)) { return true; }
                if (record.Attachments != null)
                {
                    for (int i = 0; i < record.Attachments.Count; i++)
                    {
                        if (saved.Attachments[i] != record.Attachments[i]) { return true; }
                    }
                }
            }
            return false;
        }

        private bool CarriedIdentityChanged()
        {
            if (state.CarriedRecords.Count != carried.Count) { return true; }
            foreach (WeaponRecord record in carried)
            {
                WeaponRecord saved = Find(state.CarriedRecords, record.WeaponId);
                if (saved == null || saved.InstanceId != record.InstanceId || saved.Owned != record.Owned)
                    { return true; }
            }
            return false;
        }

        private void Persist() { SnapshotCarried(); JsonStore.Save(statePath, state); }

        private void UpdateStorageInteraction(Ped ped, bool gated)
        {
            storageInput.Poll();
            bool openKey = Game.isKeyPressed(Keys.E) || storageInput.IsDown(ControllerInput.XButton);

            if (activeStorage != null)
            {
                if (gated || DevToolsMenu.IsOpen) { CloseStorage(); }
                else if (openedTrunk != null)
                {
                    // Trunk: the wheel only exists while Niko stands at the open lid; closing plays the lid animation first.
                    trunkAnimation.Update();
                    if (!trunkAnimation.Active) { CloseStorage(); }
                    else if (!storageClosing && !wheel.IsOpen && trunkAnimation.WheelReady) { wheel.Open(wheelHost); }
                }
                // Safehouse: the wheel's own close (B) ends the visit through WheelHost.WheelClosed.
            }
            else if (!gated && !DevToolsMenu.IsOpen && LibertyFramework.GameApi.Natives.IsPlayerControlOn(Player) &&
                !LibertyFramework.GameApi.Natives.IsInAnyCar(ped))
            {
                int now = Environment.TickCount;
                if (lastStorageScanTicks == 0 || unchecked(now - lastStorageScanTicks) >= 250)
                {
                    lastStorageScanTicks = now;
                    FindNearbyStorage(ped);
                }
                if (openKey && !previousStorageKey && (nearbyTrunk != null || nearbySafehouse != null))
                {
                    if (nearbyTrunk != null && !nearbyTrunk.Exists()) { nearbyTrunk = null; }
                    if (nearbyTrunk == null && nearbySafehouse == null) { previousStorageKey = openKey; return; }
                    activeStorage = nearbyTrunk != null ? Trunk(nearbyTrunk) :
                        ArsenalPolicy.FindOrAdd(state.SafehouseStashes, nearbySafehouse.Id);
                    if (activeStorage == null) { nearbyTrunk = null; previousStorageKey = openKey; return; }
                    StorageOpen = true;
                    storageClosing = false;
                    if (nearbyTrunk != null)
                    {
                        CloseTrunk();
                        openedTrunk = nearbyTrunk;
                        trunkAnimation.Begin(LibertyFramework.Engine.Handles.Ref(ped), LibertyFramework.Engine.Handles.Ref(nearbyTrunk),
                            LibertyFramework.Engine.Handles.V(nearbyTrunk.Position), config.TrunkTimings);
                    }
                    else { wheel.Open(wheelHost); }
                    Engine.Player.LockControl(this);
                    storageControlLocked = true;
                    RuntimeLog.Info("arsenal_storage_open id=" + activeStorage.Id);
                }
            }
            else { nearbyTrunk = null; nearbySafehouse = null; }

            UpdatePrompt();
            previousStorageKey = openKey;
        }

        // GTA IV's help box (Liberty.Ui) while a container is in reach and closed.
        private void UpdatePrompt()
        {
            string prompt = activeStorage != null || DevToolsMenu.IsOpen ? null :
                nearbyTrunk != null ? "Press X / E to use the trunk." : nearbySafehouse != null ? "Press X / E to open the weapon stash." : null;
            if (prompt == promptShown) { return; }
            promptShown = prompt;
            if (prompt != null) { Engine.Ui.ShowHelp(this, prompt, 0); } else { Engine.Ui.ClearHelp(this); }
        }

        private void FindNearbyStorage(Ped ped)
        {
            nearbyTrunk = null;
            nearbySafehouse = null;
            Vehicle vehicle = World.GetClosestVehicle(ped.Position, config.TrunkDistanceMeters + config.TrunkRearOffsetMeters);
            if (vehicle != null && vehicle.Exists() && vehicle.Health > 0 && !Function.Call<bool>("IS_CAR_IN_WATER", vehicle))
            {
                Vector3 rear = vehicle.GetOffsetPosition(new Vector3(0, -config.TrunkRearOffsetMeters, 0));
                if (Distance(ped.Position, rear.X, rear.Y, rear.Z) <= config.TrunkDistanceMeters) { nearbyTrunk = vehicle; }
            }
            if (nearbyTrunk != null) { return; }
            foreach (SafehouseRule house in AllSafehouses())
            {
                if (house.Episode == episode && Distance(ped.Position, house.X, house.Y, house.Z) <=
                    Math.Min(house.Radius, config.TrunkDistanceMeters)) { nearbySafehouse = house; break; }
            }
        }

        // Adapter the weapon wheel sees; every action runs on this script's tick through RunAction.
        private sealed class WheelHost : LibertyFramework.Arsenal.Ui.StorageWheel.IHost
        {
            private readonly ArsenalCore core;
            internal WheelHost(ArsenalCore owner) { core = owner; }

            public string Title { get { return core.openedTrunk != null ? "TRUNK" : "SAFEHOUSE"; } }
            public IList<WeaponRecord> Carried { get { return core.carried; } }
            public IList<WeaponRecord> Stored
                { get { return core.activeStorage != null ? (IList<WeaponRecord>)core.activeStorage.Weapons : new List<WeaponRecord>(); } }
            public bool GunsmithAvailable { get { return core.openedTrunk == null; } }
            public string Name(int weaponId) { return core.WeaponName(weaponId); }
            public int Capacity { get { return core.ContainerCapacity(); } }
            public int Segment(WeaponRecord carried)
            {
                foreach (CarriedWeapon item in core.presentation)
                {
                    if (item.WeaponId == carried.WeaponId) { return LibertyFramework.Arsenal.Logic.WeaponWheelLogic.SegmentOf(item.Slot, item.Category); }
                }
                return -1;
            }
            public WeaponRecord DisplacedBy(WeaponRecord stored)
            {
                int index = ArsenalPolicy.DisplacedOnTake(core.config, core.carried, stored, core.lastUsed);
                return index >= 0 ? core.carried[index] : null;
            }

            public string Store(WeaponRecord record)
            {
                StorageBin bin = core.activeStorage;
                if (bin == null) { return "Storage closed"; }
                string result = core.RunAction(() => core.Store(record, bin));
                if (result.StartsWith("Stored", StringComparison.Ordinal)) { core.trunkAnimation.Handle(); }
                return result;
            }

            public string Take(WeaponRecord record)
            {
                StorageBin bin = core.activeStorage;
                if (bin == null) { return "Storage closed"; }
                string result = core.RunAction(() => core.Take(record, bin));
                if (result.StartsWith("Taken", StringComparison.Ordinal) || result.StartsWith("Swapped", StringComparison.Ordinal)) { core.trunkAnimation.Handle(); }
                return result;
            }

            public List<MenuItem> GunsmithItems()
            {
                List<MenuItem> items = new List<MenuItem>();
                core.AppendGunsmithItems(items);
                return items;
            }

            // B on the wheel: a trunk closes with the lid animation first; a stash closes at once.
            public void WheelClosed()
            {
                if (core.activeStorage == null) { return; }
                if (core.openedTrunk != null && core.trunkAnimation.Active)
                {
                    core.trunkAnimation.Close();
                    core.storageClosing = true;
                }
                else { core.CloseStorage(); }
            }
        }

        private string WeaponName(int weaponId)
        {
            WeaponCatalogEntry entry = weaponCatalog == null ? null : weaponCatalog.Find(weaponId);
            if (entry != null && !string.IsNullOrEmpty(entry.Label)) { return entry.Label; }
            LibertyFramework.Gunplay.GunplayController gunplay = LibertyFramework.Gunplay.GunplayController.Instance;
            return LibertyFramework.Weapons.TestWeaponActions.LabelFor(gunplay != null ? gunplay.Config : null, weaponId);
        }

        private void CloseStorage()
        {
            if (activeStorage == null && !storageControlLocked) { return; }
            string id = activeStorage != null ? activeStorage.Id : "unknown";
            activeStorage = null;
            StorageOpen = false;
            storageClosing = false;
            wheel.Close();
            try { trunkAnimation.Abort(); CloseTrunk(); }
            finally
            {
                if (storageControlLocked) { Engine.Player.ReleaseControl(this); }
                storageControlLocked = false;
            }
            RuntimeLog.Info("arsenal_storage_closed id=" + id);
        }

        private List<MenuItem> BuildPage()
        {
            if (disabled) { return new List<MenuItem> { MenuItem.Info(() => "Arsenal disabled; see log") }; }
            try { return BuildPageCore(); }
            catch (Exception error)
            {
                Disable(error);
                return new List<MenuItem> { MenuItem.Info(() => "Arsenal disabled; see log") };
            }
        }

        private List<MenuItem> BuildPageCore()
        {
            List<MenuItem> items = new List<MenuItem>();
            if (config == null || state == null || Player == null || Player.Character == null)
                { items.Add(MenuItem.Info(() => "Arsenal unavailable; see log")); return items; }
            items.Add(MenuItem.Action("Mark safehouse here", () => RunAction(MarkSafehouse)));
            if (!StorageAllowed())
                { items.Add(MenuItem.Info(() => "Storage locked during mission or fade")); return items; }
            Ped ped = Player.Character;
            Vehicle vehicle = World.GetClosestVehicle(ped.Position, config.TrunkDistanceMeters + 3);
            StorageBin bin = null;
            bool trunk = false;
            if (vehicle != null && vehicle.Exists() && vehicle.Health > 0 &&
                !Function.Call<bool>("IS_CAR_IN_WATER", vehicle) &&
                Distance(ped.Position, vehicle.GetOffsetPosition(new Vector3(0, -config.TrunkRearOffsetMeters, 0)).X,
                    vehicle.GetOffsetPosition(new Vector3(0, -config.TrunkRearOffsetMeters, 0)).Y,
                    vehicle.GetOffsetPosition(new Vector3(0, -config.TrunkRearOffsetMeters, 0)).Z) <= config.TrunkDistanceMeters)
            {
                bin = Trunk(vehicle); trunk = true;
                if (openedTrunk != vehicle) { CloseTrunk(); vehicle.Door(VehicleDoor.Trunk).Open(); openedTrunk = vehicle; }
            }
            if (bin == null)
            {
                foreach (SafehouseRule house in AllSafehouses())
                {
                    if (house.Episode == episode && Distance(ped.Position, house.X, house.Y, house.Z) <= house.Radius)
                        { bin = ArsenalPolicy.FindOrAdd(state.SafehouseStashes, house.Id); break; }
                }
            }
            if (bin == null) { items.Add(MenuItem.Info(() => "Stand at a trunk rear or safehouse stash")); return items; }
            StorageBin selected = bin;
            items.Add(MenuItem.Info(() => (trunk ? "TRUNK " : "SAFEHOUSE ") + selected.Id));
            if (!trunk) { AppendGunsmithItems(items); }
            foreach (WeaponRecord record in carried)
            {
                WeaponRecord choice = record;
                items.Add(MenuItem.Action("Store " + Describe(choice), () => RunAction(() => Store(choice, selected))));
            }
            foreach (WeaponRecord record in new List<WeaponRecord>(bin.Weapons))
            {
                WeaponRecord choice = record;
                items.Add(MenuItem.Action("Take " + Describe(choice), () => RunAction(() => Take(choice, selected))));
            }
            return items;
        }

        private void AppendGunsmithItems(List<MenuItem> items)
        {
            if (weaponCatalog == null) { return; }
            WeaponRecord pistol = Find(carried, 7) ?? Find(carried, 58);
            if (pistol != null)
            {
                WeaponRecord choice = pistol;
                items.Add(MenuItem.Info(() => "GUNSMITH: service pistol finish"));
                if (choice.WeaponId == 7 && (choice.Progression > 0 || config.GunsmithGoldFinishPrice > 0))
                {
                    string label = choice.Progression > 0 ? "Equip gold finish" :
                        "Buy gold finish ($" + config.GunsmithGoldFinishPrice + ")";
                    items.Add(choice.Progression > 0 ? MenuItem.Action(label, () => RunAction(() => ChangePistolFinish(choice, true))) :
                        MenuItem.Confirmed(label, () => RunAction(() => ChangePistolFinish(choice, true))));
                }
                else if (choice.WeaponId == 58)
                    { items.Add(MenuItem.Action("Equip factory finish", () => RunAction(() => ChangePistolFinish(choice, false)))); }
            }
            foreach (WeaponRecord carriedRecord in carried)
            {
                if (carriedRecord.WeaponId < 58 || carriedRecord.WeaponId > 60) { continue; }
                WeaponCatalogEntry entry = weaponCatalog.Find(carriedRecord.WeaponId);
                if (entry == null) { continue; }
                foreach (string attachmentId in entry.Attachments)
                {
                    AttachmentOption option = weaponCatalog.FindAttachment(attachmentId);
                    if (option == null) { continue; }
                    WeaponRecord choice = carriedRecord;
                    AttachmentOption choiceOption = option;
                    if (choice.Attachments != null && choice.Attachments.Contains(option.Id))
                        { items.Add(MenuItem.Info(() => entry.Label + ": " + choiceOption.Label + " fitted")); }
                    else
                        { items.Add(MenuItem.Confirmed("Buy " + entry.Label + " " + option.Label + " ($" + option.Price + ")", () => RunAction(() => BuyAttachment(choice, choiceOption)))); }
                }
            }
        }

        private string BuyAttachment(WeaponRecord record, AttachmentOption option)
        {
            if (Player == null || Player.Character == null || !StorageAllowed() || Find(carried, record.WeaponId) != record ||
                record.WeaponId < 58 || record.WeaponId > 60 || weaponCatalog == null ||
                weaponCatalog.Find(record.WeaponId) == null ||
                !weaponCatalog.Find(record.WeaponId).Attachments.Contains(option.Id) ||
                weaponCatalog.FindAttachment(option.Id) != option)
                { return "Attachment unavailable"; }
            WeaponIdentity.Ensure(record);
            if (record.Attachments.Contains(option.Id)) { return "Already fitted"; }
            if (Player.Money < option.Price) { return "Not enough money"; }
            Player.Money = Player.Money - option.Price;
            previousMoney = Player.Money; moneyDecreaseAt = -1;
            record.Attachments.Add(option.Id);
            Persist();
            RuntimeLog.Info("arsenal_gunsmith_attachment instance=" + record.InstanceId + " id=" + option.Id +
                " bloom_multiplier=" + option.PerShotBloomMultiplier.ToString("0.00") + " price=" + option.Price);
            return option.Label + " fitted";
        }

        private static string Describe(WeaponRecord record)
        {
            LibertyFramework.Gunplay.GunplayController gunplay = LibertyFramework.Gunplay.GunplayController.Instance;
            string name = LibertyFramework.Weapons.TestWeaponActions.LabelFor(gunplay != null ? gunplay.Config : null, record.WeaponId);
            return name + "  (" + record.Ammo + " rounds" + (record.Owned ? ", owned" : "") + ")";
        }

        private string Store(WeaponRecord record, StorageBin bin)
        {
            if (Player == null || Player.Character == null || !StorageAllowed()) { return "Storage unavailable"; }
            GTA.value.Weapon weapon = Player.Character.Weapons.FromType((Weapon)record.WeaponId);
            if (!weapon.isPresent) { return "Weapon no longer carried"; }
            int capacity = ContainerCapacity();
            if (!TrunkCapacityRules.CanStore(bin.Weapons.Count, capacity)) { RuntimeLog.Info("arsenal_store_refused full id=" + record.WeaponId + " bin=" + bin.Id + " capacity=" + capacity); return (openedTrunk != null ? "Trunk full (" : "Stash full (") + capacity + ")"; }
            ArsenalRegistry.RaiseWeaponsRemoving("store");
            WeaponRecord stored = record.Clone(); stored.Owned = true; stored.Ammo = weapon.Ammo;
            weapon.Remove(); bin.Weapons.Add(stored);
            carried.Remove(record); state.OwnedCarried.Remove(record.WeaponId); Persist();
            RefreshPresentation((int)Player.Character.Weapons.CurrentType);
            RuntimeLog.Info("arsenal_store id=" + record.WeaponId + " to=" + bin.Id + " instance=" + stored.InstanceId + " ammo=" + stored.Ammo + " owned=" + stored.Owned);
            return "Stored " + WeaponName(record.WeaponId);
        }

        private string ChangePistolFinish(WeaponRecord record, bool gold)
        {
            if (Player == null || Player.Character == null || !StorageAllowed()) { return "Gunsmith unavailable"; }
            if (string.IsNullOrEmpty(state.LastSafehouseId) ||
                Find(carried, record.WeaponId) != record || (record.WeaponId != 7 && record.WeaponId != 58))
                { return "Service pistol not available"; }
            bool purchase = gold && record.Progression == 0;
            if (purchase && config.GunsmithGoldFinishPrice <= 0) { return "Gold finish price not configured"; }
            if (purchase && Player.Money < config.GunsmithGoldFinishPrice) { return "Not enough money"; }
            int sourceId = record.WeaponId;
            int targetId = gold ? 58 : 7;
            GTA.value.Weapon source = Player.Character.Weapons.FromType((Weapon)sourceId);
            if (!source.isPresent) { return "Pistol no longer carried"; }
            int ammo = source.Ammo;
            ArsenalRegistry.RaiseWeaponsRemoving("gunsmith_finish_switch");
            Player.Character.Weapons.Select((Weapon)targetId);
            GTA.value.Weapon target = Player.Character.Weapons.FromType((Weapon)targetId);
            if (!target.isPresent) { return "Finish switch failed; no charge"; }
            target.Ammo = ammo;
            if (purchase)
            {
                Player.Money = Player.Money - config.GunsmithGoldFinishPrice;
                previousMoney = Player.Money; moneyDecreaseAt = -1;
                record.Progression = 1;
            }
            record.WeaponId = targetId;
            record.CatalogId = gold ? "gold-test-pistol" : "service-pistol";
            record.Finish = gold ? "gold-test" : "factory";
            record.Owned = true;
            state.OwnedCarried.Remove(sourceId);
            if (!state.OwnedCarried.Contains(targetId)) { state.OwnedCarried.Add(targetId); }
            Persist(); RefreshPresentation((int)Player.Character.Weapons.CurrentType);
            RuntimeLog.Info("arsenal_gunsmith_finish instance=" + record.InstanceId + " from=" + sourceId +
                " to=" + targetId + " paid=" + purchase + " ammo=" + ammo);
            return gold ? "Gold finish equipped" : "Factory finish equipped";
        }

        private string Take(WeaponRecord record, StorageBin bin)
        {
            if (Player == null || Player.Character == null || !StorageAllowed()) { return "Storage unavailable"; }
            if (!WeaponIdentity.CanTake(carried, record)) { return "Already carrying this weapon type; store it first"; }
            // T-046: the carried weapon this take swaps out: the same category (the game holds one per category), else the least
            // recently used of the incoming weapon's group when that group is full.
            int displacedIndex = ArsenalPolicy.DisplacedOnTake(config, carried, record, lastUsed);
            WeaponRecord displaced = displacedIndex >= 0 ? carried[displacedIndex] : null;
            // A displaced weapon of another category (the group was full) is taken off Niko here and always kept in the container
            // (as the overflow rule does); one of the same category is replaced by the game when the new weapon is selected.
            bool crossCategory = displaced != null && displaced.Category != record.Category;
            WeaponRecord savedDisplaced = null;
            if (displaced != null && (displaced.Owned || crossCategory))
            {
                GTA.value.Weapon priorWeapon = Player.Character.Weapons.FromType((Weapon)displaced.WeaponId);
                savedDisplaced = displaced.Clone(); savedDisplaced.Ammo = priorWeapon.Ammo; savedDisplaced.Owned = true;
            }
            if (crossCategory)
            {
                ArsenalRegistry.RaiseWeaponsRemoving("swap");
                GTA.value.Weapon outgoing = Player.Character.Weapons.FromType((Weapon)displaced.WeaponId);
                if (outgoing.isPresent) { outgoing.Remove(); }
            }
            Player.Character.Weapons.Select((Weapon)record.WeaponId);
            Player.Character.Weapons.FromType((Weapon)record.WeaponId).Ammo = record.Ammo;
            bin.Weapons.Remove(record);
            if (savedDisplaced != null)
            {
                bin.Weapons.Add(savedDisplaced);
                RuntimeLog.Info("arsenal_take_displaced id=" + savedDisplaced.WeaponId + " instance=" + savedDisplaced.InstanceId + " to=" + bin.Id + " ammo=" + savedDisplaced.Ammo + " owned=" + savedDisplaced.Owned);
            }
            if (displaced != null) { carried.Remove(displaced); state.OwnedCarried.Remove(displaced.WeaponId); }
            WeaponRecord restored = record.Clone(); restored.Owned = true;
            WeaponRecord stale = Find(carried, restored.WeaponId);
            if (stale != null) { carried.Remove(stale); }
            carried.Add(restored);
            if (!state.OwnedCarried.Contains(record.WeaponId)) { state.OwnedCarried.Add(record.WeaponId); }
            Persist(); RefreshPresentation((int)Player.Character.Weapons.CurrentType);
            RuntimeLog.Info("arsenal_take id=" + record.WeaponId + " instance=" + record.InstanceId + " from=" + bin.Id + " ammo=" + restored.Ammo + " owned=" + restored.Owned + (displaced != null ? " swapped=" + displaced.WeaponId : ""));
            return savedDisplaced != null ? "Swapped " + WeaponName(savedDisplaced.WeaponId) + " for " + WeaponName(record.WeaponId) : "Taken " + WeaponName(record.WeaponId);
        }

        private string MarkSafehouse()
        {
            if (Player == null || Player.Character == null) { return "Player unavailable"; }
            Vector3 position = Player.Character.Position;
            SafehouseRule house = new SafehouseRule();
            house.Id = "marked_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"); house.Name = "Marked safehouse";
            house.Episode = episode; house.X = position.X; house.Y = position.Y; house.Z = position.Z;
            house.Radius = config.TrunkDistanceMeters; house.Verified = true;
            config.Safehouses.Add(house); ArsenalConfigValidator.Validate(config);
            JsonStore.Save(LibertyPlus.Configuration.PlusPaths.ArsenalConfig, config);
            state.LastSafehouseId = house.Id; AdoptUnassignedStash(house.Id); Persist();
            RuntimeLog.Info("arsenal_safehouse_marked id=" + house.Id + " x=" + house.X + " y=" + house.Y + " z=" + house.Z);
            return "Marked " + house.Id;
        }

        private static bool StorageAllowed()
        {
            return !Function.Call<bool>("GET_MISSION_FLAG") &&
                !(Function.Call<bool>("HAS_CUTSCENE_LOADED") && !Function.Call<bool>("HAS_CUTSCENE_FINISHED")) &&
                !Function.Call<bool>("IS_SCREEN_FADING") && !Function.Call<bool>("IS_SCREEN_FADED_OUT");
        }

        private string RunAction(Func<string> action)
        {
            if (disabled) { return "Arsenal disabled; see log"; }
            inventoryDirty = true;
            try { return action(); }
            catch (Exception error) { Disable(error); return "Arsenal disabled; see log"; }
        }

        private void Disable(Exception error)
        {
            disabled = true;
            RuntimeLog.Error("arsenal_disabled error=" + error);
            CloseStorageSafely();
            CloseTrunkSafely();
            if (ArsenalRegistry.CarriedWeapons == this) { ArsenalRegistry.CarriedWeapons = null; }
        }

        private void CloseTrunk()
        {
            if (openedTrunk != null && openedTrunk.Exists()) { openedTrunk.Door(VehicleDoor.Trunk).Close(); }
            openedTrunk = null;
        }

        private void CloseTrunkSafely()
        {
            try { CloseTrunk(); } catch (Exception error) { RuntimeLog.Error("arsenal_trunk_restore_failed error=" + error); }
        }

        private void CloseStorageSafely()
        {
            try { CloseStorage(); } catch (Exception error) { RuntimeLog.Error("arsenal_storage_restore_failed error=" + error); }
        }

        private void OnDomainUnload(object sender, EventArgs args)
        {
            StorageOpen = false;
            // Release only Arsenal's owned lock; another open SDK menu may still need player control captured.
            if (storageControlLocked)
            {
                try { Engine.Player.ReleaseControl(this); storageControlLocked = false; }
                catch (Exception error) { RuntimeLog.Error("arsenal_storage_unload_restore_failed error=" + error); }
            }
            if (ArsenalRegistry.CarriedWeapons == this) { ArsenalRegistry.CarriedWeapons = null; }
        }
    }
}
