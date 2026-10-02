using System;
using System.Collections.Generic;
using LibertyFramework.Arsenal.Contracts;

namespace LibertyFramework.Arsenal.Logic
{
    internal static class WeaponIdentity
    {
        internal static void Ensure(WeaponRecord record)
        {
            if (string.IsNullOrEmpty(record.InstanceId)) { record.InstanceId = Guid.NewGuid().ToString("N"); }
            if (record.Attachments == null) { record.Attachments = new List<string>(); }
            if (record.Progression < 0) { record.Progression = 0; }
        }

        internal static void Normalize(ArsenalState state)
        {
            if (state.CarriedRecords == null) { state.CarriedRecords = new List<WeaponRecord>(); }
            Dictionary<string, bool> seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            NormalizeRecords(state.CarriedRecords, seen);
            foreach (StorageBin bin in state.SafehouseStashes) { NormalizeRecords(bin.Weapons, seen); }
            foreach (StorageBin bin in state.VehicleTrunks) { NormalizeRecords(bin.Weapons, seen); }
        }

        private static void NormalizeRecords(IList<WeaponRecord> records, Dictionary<string, bool> seen)
        {
            foreach (WeaponRecord record in records)
            {
                Ensure(record);
                if (seen.ContainsKey(record.InstanceId)) { record.InstanceId = Guid.NewGuid().ToString("N"); }
                seen.Add(record.InstanceId, true);
            }
        }

        internal static WeaponRecord Find(IList<WeaponRecord> records, int weaponId)
        {
            foreach (WeaponRecord record in records) { if (record.WeaponId == weaponId) { return record; } }
            return null;
        }

        internal static bool CanTake(IList<WeaponRecord> carried, WeaponRecord stored)
        {
            return Find(carried, stored.WeaponId) == null;
        }
    }
}
