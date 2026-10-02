using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using GTA;
using GTA.Native;
using LibertyFramework.Arsenal.Contracts;
using LibertyFramework.Arsenal.Holsters.Logic;
using LibertyFramework.Core.Config;
using LibertyFramework.Core.Logging;
using LibertyFramework.DevTools;
using LibertyFramework.DevTools.Menu;
using LibertyFramework.GameApi;

namespace LibertyFramework.Arsenal.Holsters
{
    [global::Liberty.Sdk.Module("holsters", Order = 40, Capabilities = new[] { global::Liberty.Sdk.Capabilities.EngineInternal }, Description = "Visible holstered and slung weapons")]
    public sealed class HolsterController : LibertyFramework.Engine.Module
    {
        private readonly Dictionary<BodySlot, GTA.Object> props = new Dictionary<BodySlot, GTA.Object>();
        private readonly Dictionary<BodySlot, int> shownIds = new Dictionary<BodySlot, int>();
        private readonly Dictionary<BodySlot, int> shownModels = new Dictionary<BodySlot, int>();
        private readonly Dictionary<BodySlot, GTA.Object> slingProps = new Dictionary<BodySlot, GTA.Object>();
        private readonly HashSet<string> slingProblemsLogged = new HashSet<string>();
        private readonly HashSet<string> calibrated = new HashSet<string>();
        private readonly List<KeyValuePair<string, GTA.Object>> pendingCalibration = new List<KeyValuePair<string, GTA.Object>>();
        private int calibrationDueTicks;
        private PedSkeleton skeleton;
        // The WeaponInfo.xml the game loaded; fixed for the session (resolved once).
        private string activeXmlPath;
        private bool skeletonUnavailable;
        private readonly Dictionary<string, string> modelNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly string journalPath = Path.Combine(LibertyPaths.StateDirectory, "holsters_props.json");
        private bool recovered;
        private bool removing;
        private int removingRevision;
        private HolsterConfig config;
        private bool disabled;
        private BodySlot selectedSlot = BodySlot.SidearmPrimary;
        private string configHash;
        private DateTime lastConfigCheckUtc = DateTime.MinValue;
        // T-044: why the props are hidden right now ("" while shown), the outfit class the placements were resolved for, and
        // the cutscene state from the engine's CutsceneChanged event (no native polling).
        private string hiddenReason = "";
        private string outfit = HolsterConfig.DefaultOutfit;
        private int lastOutfitReadTicks;
        private bool cutscenePlaying;
        private bool syncing;
        private int lastHeldWeapon = -1;
        private readonly Dictionary<int, int[]> savedOutfit = new Dictionary<int, int[]>();

        public HolsterController()
        {
            // Attached props follow the ped in the engine; scanning carried weapons every frame
            // only allocates lists and repeats model lookups without improving animation.
            Interval = 50;
            LoadConfig();
            ArsenalRegistry.WeaponsRemoving += OnWeaponsRemoving;
            DevToolsPages.Register("Holsters", MenuItems);
            Tick += OnTick;
            AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
        }

        // T-044: the holster props react in the frame the engine reports the change instead of waiting for the next 50 ms tick.
        protected override void OnStart()
        {
            Engine.Events.Subscribe<global::Liberty.Sdk.Events.PlayerWeaponChanged>(this, e => SyncNow());
            Engine.Events.Subscribe<global::Liberty.Sdk.Events.PlayerEnteredVehicle>(this, e => SyncNow());
            Engine.Events.Subscribe<global::Liberty.Sdk.Events.PlayerExitedVehicle>(this, e => SyncNow());
            Engine.Events.Subscribe<global::Liberty.Sdk.Events.CutsceneChanged>(this, e => { cutscenePlaying = e.Playing; SyncNow(); });
            Engine.Commands.Register(this, "holsters", "holsters [status] | outfits | outfit <component> <drawable> [texture] | outfit restore - visible-loadout state and outfit test", HolsterCommand);
        }

        // A stopped module must not leave props on Niko (mod-off runs and restarts stop it mid-session).
        protected override void OnStop()
        {
            ArsenalRegistry.WeaponsRemoving -= OnWeaponsRemoving;
            try { Clear(); } catch (Exception error) { RuntimeLog.Error("holsters_stop_cleanup_failed error=" + error); }
        }

        private void SyncNow()
        {
            if (syncing) { return; }
            syncing = true;
            try { OnTick(this, EventArgs.Empty); }
            finally { syncing = false; }
        }

        private void LoadConfig()
        {
            // Check the file for edits at most once per second.
            if (config != null && (DateTime.UtcNow - lastConfigCheckUtc).TotalMilliseconds < 1000) { return; }
            lastConfigCheckUtc = DateTime.UtcNow;
            try
            {
                byte[] bytes = JsonStore.ReadBytes(LibertyPlus.Configuration.PlusPaths.HolstersConfig);
                string hash = JsonStore.Hash(bytes);
                if (hash == configHash) { return; }
                configHash = hash;
                HolsterConfig candidate = JsonStore.Parse<HolsterConfig>(bytes);
                candidate.Validate();
                foreach (HolsterPlacement placement in candidate.AllPlacements())
                {
                    Bone bone;
                    if (!Enum.TryParse<Bone>(placement.Bone, out bone) || !Enum.IsDefined(typeof(Bone), bone))
                    { throw new InvalidDataException("holsters unknown ScriptHookDotNet bone " + placement.Bone); }
                }
                if (props.Count > 0) { Clear(); }
                config = candidate;
                modelNames.Clear();
                RuntimeLog.Info("holsters_config_loaded weapons=" + config.Weapons.Count);
            }
            catch (Exception error) { RuntimeLog.Error("holsters_config_rejected error=" + error); }
        }

        // T-026: every tick's wall-clock cost goes to the shared CostMeter report.
        private void OnTick(object sender, EventArgs args)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            try { TickBody(sender, args); }
            finally { LibertyFramework.Core.Performance.Logic.CostMeter.Add("tick.holsters", started); }
        }

        private void TickBody(object sender, EventArgs args)
        {
            if (disabled) { return; }
            try
            {
                if (!recovered) { RecoverPreviousProps(); recovered = true; }
                LoadConfig();
                if (config == null || !config.Enabled) { hiddenReason = "disabled"; Clear(); return; }
                Player player = Player;
                Ped ped = player == null ? null : player.Character;
                if (ped == null) { hiddenReason = "no player"; Clear(); return; }
                bool inVehicle = Natives.IsInAnyCar(ped);
                bool onBike = inVehicle && ped.CurrentVehicle != null && ped.CurrentVehicle.Model.isBike;
                bool alive = !Natives.PedDead(ped);
                bool faded = Natives.IsScreenFadedOut();
                bool visible = HolsterRules.Visible(false, alive,
                    // DevTools locks player control while open; keep props visible so Holsters nudges show live.
                    Natives.IsPlayerPlaying(player) && !cutscenePlaying && (Natives.IsPlayerControlOn(player) || DevToolsMenu.IsOpen || ArsenalCore.StorageOpen),
                    faded, inVehicle, onBike, config.ShowOnBikes);
                if (!visible)
                {
                    hiddenReason = !alive ? "dead" : inVehicle ? "vehicle" : cutscenePlaying ? "cutscene" : faded ? "fade" : "no control";
                    Clear();
                    return;
                }
                hiddenReason = "";
                long probe = Stopwatch.GetTimestamp();
                ICarriedWeaponsSource source = ArsenalRegistry.CarriedWeapons;
                if (removing && source != null && source.Revision <= removingRevision) { Clear(); return; }
                removing = false;
                int held = Natives.CurrentWeapon(ped);
                if (held != lastHeldWeapon)
                {
                    RuntimeLog.Info("holsters_held_changed from=" + lastHeldWeapon + " to=" + held);
                    lastHeldWeapon = held;
                }
                RefreshOutfit(ped);
                IList<CarriedWeapon> carried = source != null ? source.Carried : ReadInventory(ped, held);
                if (carried == null) { Clear(); return; }
                LibertyFramework.Core.Performance.Logic.CostMeter.Add("ho.carried", probe); probe = Stopwatch.GetTimestamp();
                HashSet<BodySlot> wanted = new HashSet<BodySlot>();
                foreach (CarriedWeapon weapon in carried)
                {
                    // The weapon in Niko's hands is read live (an engine event can arrive before Arsenal refreshes its list).
                    if (weapon == null || weapon.Slot == BodySlot.None || weapon.WeaponId == held) { continue; }
                    if (!wanted.Add(weapon.Slot)) { continue; }
                    Show(ped, weapon);
                    if (props.ContainsKey(weapon.Slot)) { ShowSling(ped, weapon.Slot); }
                }
                foreach (BodySlot slot in new List<BodySlot>(props.Keys)) { if (!wanted.Contains(slot)) { Remove(slot); } }
                LibertyFramework.Core.Performance.Logic.CostMeter.Add("ho.show", probe);
            }
            catch (Exception error)
            {
                RuntimeLog.Error("feature_disabled holsters error=" + error);
                try { Clear(); } catch (Exception restoreError) { RuntimeLog.Error("holsters_cleanup_failed error=" + restoreError); }
                disabled = true;
            }
        }

        // Niko's outfit class from the drawables he wears in the components the classes look at (at most twice a second; the
        // component read is one native per component). A change re-attaches every prop with the new class's offsets.
        private void RefreshOutfit(Ped ped)
        {
            if (config.OutfitClasses == null || config.OutfitClasses.Count == 0) { return; }
            int now = Environment.TickCount;
            if (lastOutfitReadTicks != 0 && unchecked(now - lastOutfitReadTicks) < 500) { return; }
            lastOutfitReadTicks = now;
            string resolved = config.OutfitFor(ReadDrawables(ped));
            if (resolved == outfit) { return; }
            RuntimeLog.Info("holsters_outfit_changed from=" + outfit + " to=" + resolved);
            outfit = resolved;
            Clear();
        }

        private Dictionary<int, int> ReadDrawables(Ped ped)
        {
            Dictionary<int, int> drawables = new Dictionary<int, int>();
            foreach (HolsterOutfitClass outfitClass in config.OutfitClasses)
            {
                if (drawables.ContainsKey(outfitClass.Component)) { continue; }
                drawables[outfitClass.Component] = Function.Call<int>("GET_CHAR_DRAWABLE_VARIATION", ped, outfitClass.Component);
            }
            return drawables;
        }

        private string HolsterCommand(string[] args)
        {
            string verb = args.Length > 0 ? args[0] : "status";
            Ped ped = Player == null ? null : Player.Character;
            if (verb == "outfits")
            {
                if (ped == null) { return "no player"; }
                List<string> parts = new List<string>();
                for (int component = 0; component < 11; component++)
                {
                    parts.Add(component + "=" + Function.Call<int>("GET_CHAR_DRAWABLE_VARIATION", ped, component) + "/" +
                        Function.Call<int>("GET_CHAR_TEXTURE_VARIATION", ped, component));
                }
                string line = "holsters_outfit_components model=" + ped.Model.Hash.ToString("X8") + " drawable/texture " + string.Join(" ", parts.ToArray());
                return line;
            }
            if (verb == "outfit" && args.Length == 2 && args[1] == "restore")
            {
                if (ped == null) { return "no player"; }
                foreach (KeyValuePair<int, int[]> saved in savedOutfit) { Function.Call("SET_CHAR_COMPONENT_VARIATION", ped, saved.Key, saved.Value[0], saved.Value[1]); }
                int restored = savedOutfit.Count;
                savedOutfit.Clear();
                lastOutfitReadTicks = 0;
                return "holsters_outfit_restored components=" + restored;
            }
            if (verb == "outfit")
            {
                int component, drawable, texture = 0;
                if (ped == null || args.Length < 3 || !int.TryParse(args[1], out component) || !int.TryParse(args[2], out drawable) ||
                    (args.Length > 3 && !int.TryParse(args[3], out texture)))
                { return "holsters outfit <component> <drawable> [texture]"; }
                // The first change of a component remembers what Niko wore so "holsters outfit restore" can put it back.
                if (!savedOutfit.ContainsKey(component))
                {
                    savedOutfit[component] = new int[] { Function.Call<int>("GET_CHAR_DRAWABLE_VARIATION", ped, component), Function.Call<int>("GET_CHAR_TEXTURE_VARIATION", ped, component) };
                }
                Function.Call("SET_CHAR_COMPONENT_VARIATION", ped, component, drawable, texture);
                int now = Function.Call<int>("GET_CHAR_DRAWABLE_VARIATION", ped, component);
                string line = "holsters_outfit_set component=" + component + " requested=" + drawable + " now=" + now;
                lastOutfitReadTicks = 0;
                return line;
            }
            if (verb != "status") { return "holsters [status] | outfits | outfit <component> <drawable> [texture] | outfit restore"; }
            int existing = 0;
            List<string> slots = new List<string>();
            foreach (KeyValuePair<BodySlot, GTA.Object> pair in props)
            {
                bool exists = pair.Value != null && pair.Value.Exists();
                if (exists) { existing++; }
                slots.Add(pair.Key + ":" + shownIds[pair.Key] + (exists ? "" : "!missing"));
            }
            int slings = 0;
            foreach (GTA.Object sling in slingProps.Values) { if (sling != null && sling.Exists()) { slings++; } }
            string status = "holsters_status props=" + props.Count + " existing=" + existing + " slings=" + slingProps.Count + " slings_existing=" + slings +
                " hidden=" + (hiddenReason.Length == 0 ? "none" : hiddenReason) + " outfit=" + outfit + " disabled=" + disabled +
                " slots=" + (slots.Count == 0 ? "none" : string.Join(",", slots.ToArray())) + " held=" + lastHeldWeapon;
            return status;
        }

        private IList<CarriedWeapon> ReadInventory(Ped ped, int held)
        {
            List<CarriedWeapon> result = new List<CarriedWeapon>();
            bool firstLong = true;
            foreach (HolsterWeapon weapon in config.Weapons)
            {
                GTA.value.Weapon instance = ped.Weapons.FromType((Weapon)weapon.WeaponId);
                if (instance == null || !instance.isPresent) { continue; }
                BodySlot slot = HolsterRules.SlotFor(weapon.Category, firstLong);
                if (slot == BodySlot.LongGun1) { firstLong = false; }
                result.Add(new CarriedWeapon(weapon.WeaponId, weapon.Category, slot, weapon.WeaponId == held));
            }
            if (Game.CurrentEpisode != GameEpisode.GTAIV)
            {
                for (int weaponId = 21; weaponId <= 41; weaponId++)
                {
                    GTA.value.Weapon instance = ped.Weapons.FromType((Weapon)weaponId);
                    if (instance == null || !instance.isPresent) { continue; }
                    WeaponCategory category = CategoryFor(instance.Slot);
                    BodySlot slot = HolsterRules.SlotFor(category, firstLong);
                    if (slot == BodySlot.LongGun1) { firstLong = false; }
                    result.Add(new CarriedWeapon(weaponId, category, slot, weaponId == held));
                }
            }
            return result;
        }

        private static WeaponCategory CategoryFor(WeaponSlot slot)
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

        private void Show(Ped ped, CarriedWeapon weapon)
        {
            // Already showing this weapon in this slot: nothing to resolve (the path lookup below touches the file system).
            int alreadyShown;
            if (shownIds.TryGetValue(weapon.Slot, out alreadyShown) && alreadyShown == weapon.WeaponId && props.ContainsKey(weapon.Slot)) { return; }
            HolsterWeapon entry = config.FindWeapon(weapon.WeaponId);
            if (entry == null && (weapon.WeaponId < 21 || weapon.WeaponId > 41 || Game.CurrentEpisode == GameEpisode.GTAIV))
            { Remove(weapon.Slot); return; }
            string weaponType = entry != null ? entry.WeaponInfoType : "EPISODIC_" + (weapon.WeaponId - 20);
            if (activeXmlPath == null) { activeXmlPath = WeaponInfoXml.ActivePath(LibertyPaths.GameDirectory); }
            string xmlPath = entry != null ? activeXmlPath :
                Path.Combine(LibertyPaths.GameDirectory, Path.Combine(Game.CurrentEpisode == GameEpisode.TLAD ? "TLAD" : "TBoGT",
                    Path.Combine("common", Path.Combine("data", "WeaponInfo.xml"))));
            string modelKey = xmlPath + "|" + weaponType;
            string modelName;
            if (!modelNames.TryGetValue(modelKey, out modelName))
            {
                modelName = WeaponInfoXml.ModelFor(xmlPath, weaponType);
                modelNames[modelKey] = modelName;
                if (string.IsNullOrEmpty(modelName))
                { RuntimeLog.Error("holster_no_weaponinfo_model type=" + weaponType + " id=" + weapon.WeaponId); }
            }
            if (string.IsNullOrEmpty(modelName)) { Remove(weapon.Slot); return; }
            int shown;
            if (shownIds.TryGetValue(weapon.Slot, out shown) && shown == weapon.WeaponId && props.ContainsKey(weapon.Slot)) { return; }
            Remove(weapon.Slot);
            Model model = new Model(modelName);
            if (!model.isValid) { RuntimeLog.Error("holster_model_invalid " + modelName); return; }
            HolsterNatives.RequestModel(model);
            if (!HolsterNatives.HasModelLoaded(model)) { return; }
            HolsterPlacement placement = config.FindPlacement(weapon.Slot, weapon.Category, modelName, outfit);
            if (placement == null) { return; }
            GTA.Object prop = World.CreateObject(model, ped.Position);
            if (prop == null) { return; }
            try
            {
                prop.Collision = false;
                prop.AttachToPed(ped, (Bone)Enum.Parse(typeof(Bone), placement.Bone),
                    new Vector3(placement.Position[0], placement.Position[1], placement.Position[2]),
                    new Vector3(placement.Rotation[0], placement.Rotation[1], placement.Rotation[2]));
                props[weapon.Slot] = prop;
                shownIds[weapon.Slot] = weapon.WeaponId;
                shownModels[weapon.Slot] = model.Hash;
                SaveJournal();
                QueueCalibration(weapon.Slot + ":" + modelName + "|" + placement.Bone + "|" + Vector(placement.Position) + "|" + Vector(placement.Rotation), prop);
            }
            catch (Exception error) { prop.Delete(); RuntimeLog.Error("holster_attach_failed error=" + error); throw; }
        }

        // W-5: the strap for a slung long gun; its model is authored in the bone's space (zero offset and rotation).
        private void ShowSling(Ped ped, BodySlot slot)
        {
            HolsterSling sling = config.FindSling(slot);
            if (sling == null || slingProps.ContainsKey(slot)) { return; }
            Model model = new Model(sling.Model);
            if (!model.isValid)
            {
                if (slingProblemsLogged.Add(sling.Model)) { RuntimeLog.Error("holster_sling_model_invalid " + sling.Model + " (LibertyModels.img not installed?)"); }
                return;
            }
            HolsterNatives.RequestModel(model);
            if (!HolsterNatives.HasModelLoaded(model)) { return; }
            GTA.Object prop = World.CreateObject(model, ped.Position);
            if (prop == null) { return; }
            try
            {
                prop.Collision = false;
                prop.AttachToPed(ped, (Bone)Enum.Parse(typeof(Bone), sling.Bone), new Vector3(0, 0, 0), new Vector3(0, 0, 0));
                slingProps[slot] = prop;
                SaveJournal();
                RuntimeLog.Info("holster_sling_attached slot=" + slot + " model=" + sling.Model + " bone=" + sling.Bone);
                QueueCalibration("sling:" + sling.Model + "|" + sling.Bone + "|(0,0,0)|(0,0,0)", prop);
            }
            catch (Exception error) { prop.Delete(); RuntimeLog.Error("holster_sling_attach_failed error=" + error); throw; }
        }

        // One log line per placement per session: the attached prop's origin and axes in the bone's own frame. This pins
        // down AttachToPed's offset/rotation convention (units and order) from real data (docs/research/ModelFormat.md).
        private void QueueCalibration(string key, GTA.Object prop)
        {
            if (calibrated.Contains(key)) { return; }
            // The engine applies an attachment on a later frame; measuring on the attach tick reads the spawn pose.
            pendingCalibration.Add(new KeyValuePair<string, GTA.Object>(key, prop));
            calibrationDueTicks = Environment.TickCount + CalibrationDelayMilliseconds;
        }

        // Long enough for the attachment to be applied and the ped to be drawn at least once.
        private const int CalibrationDelayMilliseconds = 1500;

        private void Calibrate(Ped ped)
        {
            if (pendingCalibration.Count == 0 || skeletonUnavailable || unchecked(Environment.TickCount - calibrationDueTicks) < 0) { return; }
            if (skeleton == null)
            {
                LibertyFramework.Gunplay.GunplayController gunplay = LibertyFramework.Gunplay.GunplayController.Instance;
                LibertyFramework.Core.Memory.GameAddresses addresses = gunplay != null ? gunplay.Addresses : null;
                if (addresses == null) { return; }
                if (!addresses.PedSkeletonResolved) { skeletonUnavailable = true; pendingCalibration.Clear(); return; }
                skeleton = new PedSkeleton(LibertyFramework.Engine.LibertyEngine.Current.Memory.Live, addresses);
            }
            uint pointer = skeleton.PedFromHandle(ped.GetHashCode());
            foreach (KeyValuePair<string, GTA.Object> pending in pendingCalibration)
            {
                if (!calibrated.Add(pending.Key) || pending.Value == null || !pending.Value.Exists()) { continue; }
                string bone = pending.Key.Split('|')[1];
                float[] m = skeleton.WorldMatrix(pointer, (int)(Bone)Enum.Parse(typeof(Bone), bone));
                if (m == null) { continue; }
                Vector3 origin = pending.Value.GetOffsetPosition(new Vector3(0, 0, 0));
                Vector3 x = pending.Value.GetOffsetPosition(new Vector3(1, 0, 0)) - origin;
                Vector3 y = pending.Value.GetOffsetPosition(new Vector3(0, 1, 0)) - origin;
                Vector3 z = pending.Value.GetOffsetPosition(new Vector3(0, 0, 1)) - origin;
                Vector3 relative = origin - new Vector3(m[12], m[13], m[14]);
                RuntimeLog.Info("holster_frame " + pending.Key + " origin_in_bone=" + InBone(m, relative) +
                    " x_in_bone=" + InBone(m, x) + " y_in_bone=" + InBone(m, y) + " z_in_bone=" + InBone(m, z));
            }
            pendingCalibration.Clear();
        }

        private static string InBone(float[] m, Vector3 v)
        {
            return "(" + (v.X * m[0] + v.Y * m[1] + v.Z * m[2]).ToString("0.000") + "," + (v.X * m[4] + v.Y * m[5] + v.Z * m[6]).ToString("0.000") + "," +
                (v.X * m[8] + v.Y * m[9] + v.Z * m[10]).ToString("0.000") + ")";
        }

        private static string Vector(float[] v) { return "(" + v[0] + "," + v[1] + "," + v[2] + ")"; }

        private void Remove(BodySlot slot)
        {
            GTA.Object sling;
            if (slingProps.TryGetValue(slot, out sling))
            {
                slingProps.Remove(slot);
                if (sling != null && sling.Exists()) { sling.Delete(); }
                SaveJournal();
            }
            GTA.Object prop;
            if (props.TryGetValue(slot, out prop))
            {
                props.Remove(slot);
                shownIds.Remove(slot);
                shownModels.Remove(slot);
                if (prop != null && prop.Exists()) { prop.Delete(); }
                SaveJournal();
            }
        }

        private void Clear()
        {
            foreach (BodySlot slot in new List<BodySlot>(props.Keys)) { Remove(slot); }
            foreach (BodySlot slot in new List<BodySlot>(slingProps.Keys)) { Remove(slot); }
        }

        private void OnWeaponsRemoving(string reason)
        {
            try
            {
                ICarriedWeaponsSource source = ArsenalRegistry.CarriedWeapons;
                removing = true;
                removingRevision = source != null ? source.Revision : -1;
                Clear();
                RuntimeLog.Info("holsters_removed reason=" + reason);
            }
            catch (Exception error) { RuntimeLog.Error("holsters_remove_failed error=" + error); disabled = true; }
        }

        private void OnDomainUnload(object sender, EventArgs args)
        {
            // SHDN natives are unavailable during DomainUnload. A same-process ReloadScripts
            // consumes this journal on its first tick and deletes any surviving matching props.
            ArsenalRegistry.WeaponsRemoving -= OnWeaponsRemoving;
        }

        private void SaveJournal()
        {
            if (props.Count == 0 && slingProps.Count == 0)
            {
                if (File.Exists(journalPath)) { File.Delete(journalPath); }
                return;
            }
            using (Process process = Process.GetCurrentProcess())
            {
                HolsterPropJournal journal = new HolsterPropJournal();
                journal.ProcessId = process.Id;
                journal.ProcessStartUtcTicks = process.StartTime.ToUniversalTime().Ticks;
                journal.Props = new List<HolsterPropRecord>();
                foreach (KeyValuePair<BodySlot, GTA.Object> pair in props)
                {
                    HolsterPropRecord record = new HolsterPropRecord();
                    record.Handle = pair.Value.GetHashCode(); // SHDN HandleObject.GetHashCode returns the native handle.
                    record.ModelHash = shownModels[pair.Key];
                    journal.Props.Add(record);
                }
                foreach (GTA.Object sling in slingProps.Values)
                {
                    HolsterPropRecord record = new HolsterPropRecord();
                    record.Handle = sling.GetHashCode();
                    record.ModelHash = sling.Model.Hash;
                    journal.Props.Add(record);
                }
                JsonStore.Save(journalPath, journal);
            }
        }

        private void RecoverPreviousProps()
        {
            if (!File.Exists(journalPath)) { return; }
            HolsterPropJournal journal = JsonStore.Load<HolsterPropJournal>(journalPath);
            using (Process process = Process.GetCurrentProcess())
            {
                if (journal.ProcessId == process.Id && journal.ProcessStartUtcTicks == process.StartTime.ToUniversalTime().Ticks)
                {
                    if (journal.Props == null) { throw new InvalidDataException("holster journal missing props"); }
                    foreach (HolsterPropRecord prop in journal.Props)
                    {
                        if (prop != null && HolsterNatives.ObjectExists(prop.Handle) && HolsterNatives.ObjectModel(prop.Handle) == prop.ModelHash)
                        {
                            HolsterNatives.DeleteObject(prop.Handle);
                            RuntimeLog.Info("holster_orphan_removed handle=" + prop.Handle);
                        }
                    }
                }
            }
            File.Delete(journalPath);
        }

        private List<MenuItem> MenuItems()
        {
            List<MenuItem> items = new List<MenuItem>();
            if (config == null) { items.Add(MenuItem.Info(() => "holsters.json unavailable; see log")); return items; }
            items.Add(MenuItem.Info(() => "Selected " + selectedSlot));
            items.Add(MenuItem.Action("Next slot", () => { selectedSlot = selectedSlot == BodySlot.Melee ? BodySlot.SidearmPrimary : selectedSlot + 1; return selectedSlot.ToString(); }));
            items.Add(NudgeItem("Position X", false, 0));
            items.Add(NudgeItem("Position Y", false, 1));
            items.Add(NudgeItem("Position Z", false, 2));
            items.Add(NudgeItem("Rotation X", true, 0));
            items.Add(NudgeItem("Rotation Y", true, 1));
            items.Add(NudgeItem("Rotation Z", true, 2));
            items.Add(MenuItem.Action("Save offsets", () => { JsonStore.Save(LibertyPlus.Configuration.PlusPaths.HolstersConfig, config); return "Saved holsters.json (.bak)"; }));
            return items;
        }

        private MenuItem NudgeItem(string label, bool rotation, int axis)
        {
            MenuItem item = new MenuItem();
            item.Label = () =>
            {
                HolsterPlacement placement = config.FindPlacement(selectedSlot, WeaponCategory.Other, null);
                float[] vector = rotation ? placement.Rotation : placement.Position;
                return label + " " + vector[axis].ToString("0.000");
            };
            item.Adjust = direction =>
            {
                HolsterPlacement placement = config.FindPlacement(selectedSlot, WeaponCategory.Other, null);
                float[] vector = rotation ? placement.Rotation : placement.Position;
                vector[axis] += direction * (rotation ? config.NudgeRotationDegrees : config.NudgePositionMeters);
                Remove(selectedSlot);
                return label + " " + vector[axis].ToString("0.000");
            };
            return item;
        }
    }
}
