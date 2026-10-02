using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using GTA;
using LibertyFramework.Core.Config;
using LibertyFramework.Core.Logging;
using LibertyFramework.DevTools.Teleport.Logic;
using LibertyFramework.GameApi;

namespace LibertyFramework.DevTools.Teleport
{
    // Data-driven teleports from config/devtools/locations.json. A teleport first moves the player
    // and requests collision; once the world has streamed in (SnapDelay), the position is snapped
    // to pavement or ground as the location asks and the heading is applied.
    internal sealed class TeleportService
    {
        private const double SnapDelayMilliseconds = 900;
        private const float GroundClearance = 1.0f;
        internal const string GunTestRangeId = "gun_test_range";

        private TeleportLocation pending;
        private DateTime pendingSinceUtc;
        private bool hasPrevious;
        private Vector3 previousPosition;
        private float previousHeading;

        internal List<TeleportLocation> Load(out string error)
        {
            error = null;
            try
            {
                LocationFile file = JsonStore.Load<LocationFile>(LibertyPlus.Configuration.PlusPaths.Locations);
                if (file.SchemaVersion != 1 || file.Locations == null) { throw new InvalidDataException("schemaVersion must be 1 with locations"); }
                return file.Locations;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                RuntimeLog.Error("locations_load_failed path=" + LibertyPlus.Configuration.PlusPaths.Locations + " error=" + exception.Message);
                return new List<TeleportLocation>();
            }
        }

        internal TeleportLocation Find(string id)
        {
            string error;
            foreach (TeleportLocation location in Load(out error))
            {
                if (location.Id == id) { return location; }
            }
            return null;
        }

        internal string Start(Player player, TeleportLocation location)
        {
            if (player == null || player.Character == null) { return "Player not ready"; }
            Ped ped = player.Character;
            previousPosition = ped.Position;
            previousHeading = ped.Heading;
            hasPrevious = true;
            RunStage(location.Id, "move", () => MoveTo(ped, location.X, location.Y, location.Z));
            RunStage(location.Id, "request_collision", () => Natives.RequestCollisionAt(location.X, location.Y, location.Z));
            RunStage(location.Id, "load_scene", () => LoadSceneDeclared(location.X, location.Y, location.Z));
            pending = location;
            pendingSinceUtc = DateTime.UtcNow;
            RuntimeLog.Info("teleport_start id=" + location.Id + " target=" + location.X + "," + location.Y + "," + location.Z + " snap=" + location.Snap);
            return "Teleporting to " + location.Name;
        }

        // LOAD_SCENE blocks the game thread by design; declare it so the engine watchdog does not report a stall.
        private static void LoadSceneDeclared(float x, float y, float z)
        {
            Engine.LibertyEngine engine = Engine.LibertyEngine.Current;
            if (engine != null && engine.Watchdog != null) { engine.Watchdog.ExpectBlocking(engine.Config.TeleportBlockingWindowMilliseconds); }
            try { Natives.LoadScene(x, y, z); }
            finally { if (engine != null && engine.Watchdog != null) { engine.Watchdog.EndBlocking(); } }
        }

        // Logs how long each teleport stage blocked (the stress-run stall was the load_scene stage).
        private static void RunStage(string id, string stage, Action action)
        {
            RuntimeLog.Info("teleport_stage id=" + id + " stage=" + stage + " begin");
            Stopwatch elapsed = Stopwatch.StartNew();
            try { action(); }
            finally { RuntimeLog.Info("teleport_stage id=" + id + " stage=" + stage + " elapsed_ms=" + elapsed.ElapsedMilliseconds); }
        }

        internal string Return(Player player)
        {
            if (!hasPrevious) { return "No previous position"; }
            TeleportLocation back = new TeleportLocation();
            back.Id = "previous";
            back.Name = "previous position";
            back.X = previousPosition.X;
            back.Y = previousPosition.Y;
            back.Z = previousPosition.Z;
            back.Heading = previousHeading;
            back.Snap = "none";
            return Start(player, back);
        }

        // Called every frame by the DevTools script.
        internal void Update(Player player)
        {
            if (pending == null || player == null || player.Character == null) { return; }
            if ((DateTime.UtcNow - pendingSinceUtc).TotalMilliseconds < SnapDelayMilliseconds) { return; }
            TeleportLocation location = pending;
            pending = null;
            Ped ped = player.Character;
            Vector3 target = new Vector3(location.X, location.Y, location.Z);
            if (location.Snap == "pavement")
            {
                Vector3 pavement = World.GetNextPositionOnPavement(target);
                if (TeleportSnap.IsUsablePavement(target.X, target.Y, target.Z, pavement.X, pavement.Y, pavement.Z))
                { target = pavement + new Vector3(0, 0, GroundClearance); }
                else { RuntimeLog.Info("teleport_snap_rejected id=" + location.Id + " snap=pavement; using configured position"); }
            }
            else if (location.Snap == "ground")
            {
                float ground = Natives.GroundZ(location.X, location.Y, location.Z);
                if (TeleportSnap.IsUsableGround(location.Z, ground))
                { target = new Vector3(location.X, location.Y, ground + GroundClearance); }
                else { RuntimeLog.Info("teleport_snap_rejected id=" + location.Id + " snap=ground; using configured position"); }
            }
            MoveTo(ped, target.X, target.Y, target.Z);
            if (!ped.isInVehicle()) { Natives.SetCharHeading(ped, location.Heading); }
            RuntimeLog.Info("teleport_done id=" + location.Id + " final=" + target.X.ToString("0.0") + "," + target.Y.ToString("0.0") + "," + target.Z.ToString("0.0"));
        }

        internal string SaveGunTestRangeHere(Player player)
        {
            if (player == null || player.Character == null) { return "Player not ready"; }
            string error;
            List<TeleportLocation> locations = Load(out error);
            if (error != null) { return "locations.json unreadable; not saved"; }
            TeleportLocation range = null;
            foreach (TeleportLocation location in locations) { if (location.Id == GunTestRangeId) { range = location; } }
            if (range == null)
            {
                range = new TeleportLocation();
                range.Id = GunTestRangeId;
                range.Name = "Gun Test Range";
                locations.Insert(0, range);
            }
            Vector3 position = player.Character.Position;
            range.X = position.X;
            range.Y = position.Y;
            range.Z = position.Z;
            range.Heading = player.Character.Heading;
            range.Snap = "none";
            range.Note = "Saved in game with DevTools on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ".";
            LocationFile file = new LocationFile();
            file.SchemaVersion = 1;
            file.Locations = locations;
            JsonStore.Save(LibertyPlus.Configuration.PlusPaths.Locations, file);
            RuntimeLog.Info("gun_test_range_saved position=" + position.X + "," + position.Y + "," + position.Z + " heading=" + range.Heading);
            return "Gun Test Range saved here";
        }

        private static void MoveTo(Ped ped, float x, float y, float z)
        {
            if (ped.isInVehicle() && ped.CurrentVehicle != null)
            {
                ped.CurrentVehicle.Position = new Vector3(x, y, z);
            }
            else
            {
                Natives.SetCharCoordinates(ped, x, y, z);
            }
        }
    }
}
