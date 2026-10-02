using System;
using System.Collections.Generic;
using LibertyFramework.Core.Config;
using LibertyFramework.Core.Logging;
using LibertyFramework.GameApi;
using LibertyFramework.Gunplay.Profiles;

namespace LibertyFramework.Weapons
{
    // Runtime view of weapon finishes. A finish is baked offline into a model variant
    // (tools/finishes, assets/finishes/finishes.json); GTA IV weapons cannot swap textures per
    // instance, so the runtime only reports which model each test weapon actually loads.
    internal static class WeaponFinishCatalog
    {
        private static Dictionary<string, string> models;

        internal static string Describe(WeaponProfile profile)
        {
            if (profile == null) { return "-"; }
            string model = ModelFor(profile.WeaponInfoName);
            return profile.Finish + " (model " + (model ?? "?") + ")";
        }

        private static string ModelFor(string weaponType)
        {
            try
            {
                if (models == null)
                {
                    models = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                string model;
                if (!models.TryGetValue(weaponType, out model))
                {
                    model = WeaponInfoXml.ModelFor(WeaponInfoXml.ActivePath(LibertyPaths.GameDirectory), weaponType);
                    models[weaponType] = model;
                }
                return model;
            }
            catch (Exception error)
            {
                RuntimeLog.Error("finish_model_lookup_failed type=" + weaponType + " error=" + error.Message);
                return null;
            }
        }
    }
}
