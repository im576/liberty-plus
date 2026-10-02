using System;
using System.Collections.Generic;
using GTA;
using LibertyFramework.Core.Logging;
using LibertyFramework.GameApi;
using LibertyFramework.Gunplay.Profiles;

namespace LibertyFramework.DevTools.TestRange
{
    // Repeatable target lane in front of the player: frozen peds at the configured distances
    // (5/10/25/50 m by default) plus one parked vehicle, all removed with Clear Targets.
    internal sealed class TestRangeService
    {
        private readonly List<Ped> peds = new List<Ped>();
        private readonly List<Vehicle> vehicles = new List<Vehicle>();

        internal int Count { get { return peds.Count + vehicles.Count; } }

        internal string SpawnLane(Player player, TestRangeSettings settings)
        {
            if (player == null || player.Character == null) { return "Player not ready"; }
            Clear();
            Ped self = player.Character;
            Vector3 origin = self.Position;
            Vector3 forward = self.Direction;
            forward.Z = 0;
            forward.Normalize();
            Vector3 right = new Vector3(forward.Y, -forward.X, 0);
            float facePlayer = self.Heading + 180.0f;
            foreach (double distance in settings.TargetDistancesMeters)
            {
                Vector3 spot = origin + forward * (float)distance;
                spot.Z = World.GetGroundZ(new Vector3(spot.X, spot.Y, origin.Z + 3.0f)) + 1.0f;
                Ped target = World.CreatePed(spot);
                if (target == null || !target.Exists()) { continue; }
                target.Heading = facePlayer;
                target.BlockPermanentEvents = true;
                target.FreezePosition = true;
                peds.Add(target);
            }
            Vector3 carSpot = origin + forward * (float)settings.VehicleDistanceMeters + right * (float)settings.VehicleLateralMeters;
            carSpot.Z = World.GetGroundZ(new Vector3(carSpot.X, carSpot.Y, origin.Z + 3.0f)) + 1.0f;
            Vehicle car = World.CreateVehicle(carSpot);
            if (car != null && car.Exists())
            {
                car.Heading = self.Heading + 90.0f;
                car.PlaceOnGroundProperly();
                vehicles.Add(car);
            }
            RuntimeLog.Info("test_range_spawned peds=" + peds.Count + " vehicles=" + vehicles.Count + " origin=" + origin);
            return "Spawned " + peds.Count + " targets + " + vehicles.Count + " vehicle";
        }

        internal string Clear()
        {
            int removed = 0;
            foreach (Ped ped in peds)
            {
                try { if (ped != null && ped.Exists()) { ped.Delete(); removed++; } }
                catch (Exception error) { RuntimeLog.Error("test_range_delete_failed error=" + error.Message); }
            }
            foreach (Vehicle vehicle in vehicles)
            {
                try { if (vehicle != null && vehicle.Exists()) { vehicle.Delete(); removed++; } }
                catch (Exception error) { RuntimeLog.Error("test_range_delete_failed error=" + error.Message); }
            }
            peds.Clear();
            vehicles.Clear();
            if (removed > 0) { RuntimeLog.Info("test_range_cleared removed=" + removed); }
            return "Cleared " + removed + " targets";
        }

        internal static string RestoreHealth(Player player, TestRangeSettings settings)
        {
            if (player == null || player.Character == null) { return "Player not ready"; }
            player.Character.Health = settings.HealthRefill;
            player.Character.Armor = settings.ArmorRefill;
            RuntimeLog.Info("player_restored health=" + player.Character.Health + " armor=" + player.Character.Armor);
            return "Health and armor restored";
        }

        internal static string ClearWanted(Player player)
        {
            if (player == null) { return "Player not ready"; }
            Natives.ClearWantedLevel(player);
            RuntimeLog.Info("wanted_cleared");
            return "Wanted level cleared";
        }
    }
}
