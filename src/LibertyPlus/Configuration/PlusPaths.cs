using System.IO;
using LibertyFramework.Core.Config;
namespace LibertyPlus.Configuration
{
    internal static class PlusPaths
    {
        private static string ConfigDirectory { get { return LibertyPaths.ConfigDirectory; } }
        private static string StateDirectory { get { return LibertyPaths.StateDirectory; } }
        internal static string PresetDirectory { get { return Path.Combine(ConfigDirectory, "presets"); } }
        internal static string GunplayConfig { get { return Path.Combine(ConfigDirectory, "gunplay.json"); } }
        internal static string Locations { get { return Path.Combine(ConfigDirectory, Path.Combine("devtools", "locations.json")); } }
        internal static string Finishes { get { return Path.Combine(ConfigDirectory, "finishes.json"); } }
        internal static string FreeAimRestoreState { get { return Path.Combine(StateDirectory, "freeaim_restore.json"); } }
        internal static string ArsenalConfig { get { return Path.Combine(ConfigDirectory, "arsenal.json"); } }
        internal static string AtmosphereConfig { get { return Path.Combine(ConfigDirectory, "atmosphere.json"); } }
        internal static string WeaponCatalog { get { return Path.Combine(ConfigDirectory, "weapon-catalog.json"); } }
        internal static string ArsenalState(string episode) { return Path.Combine(StateDirectory, "arsenal_" + episode + ".json"); }
        internal static string HolstersConfig { get { return Path.Combine(ConfigDirectory, "holsters.json"); } }
    }
}
