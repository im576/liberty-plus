using System;
using System.IO;
using LibertyFramework.Core.Config;

namespace LibertyFramework.Arsenal.Logic
{
    internal static class ArsenalStateStore
    {
        internal static ArsenalState LoadOrEmpty(string path, Action<Exception> report)
        {
            if (!File.Exists(path)) { return new ArsenalState(); }
            try
            {
                ArsenalState state = JsonStore.Load<ArsenalState>(path);
                if (state.SchemaVersion != 1 || state.OwnedCarried == null || state.VehicleTrunks == null || state.SafehouseStashes == null)
                    { throw new InvalidDataException("Invalid Arsenal state schema."); }
                WeaponIdentity.Normalize(state);
                return state;
            }
            catch (Exception error)
            {
                report(error);
                string damaged = path + ".corrupt_" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                File.Move(path, damaged);
                return new ArsenalState();
            }
        }
    }
}
