using LibertyFramework.Gunplay.Profiles;
using LibertyFramework.Weapons.Logic;

namespace LibertyFramework.Gunplay.Logic
{
    // The gunplay gate (T-041): which weapon profiles the recoil/spread/reticle model may apply to.
    // Test weapons (58+) always pass. A catalog weapon (vanilla id) passes only when the Stage 1 switch is on and the weapon
    // catalog lists an entry for it that is a Stage 1 weapon with the same weapon id; every other vanilla weapon stays vanilla.
    internal static class Stage1Gate
    {
        internal static bool Allows(GunplayConfig config, WeaponCatalog catalog, WeaponProfile profile)
        {
            if (profile == null) { return false; }
            if (profile.IsTestWeapon) { return true; }
            if (config == null || !config.Stage1WeaponsEnabled || catalog == null || string.IsNullOrEmpty(profile.CatalogId)) { return false; }
            WeaponCatalogEntry entry = catalog.FindById(profile.CatalogId);
            return entry != null && entry.Stage1 && entry.WeaponId == profile.WeaponId;
        }

        // The profile the model applies to a held weapon, or null (vanilla behaviour).
        internal static WeaponProfile ProfileFor(GunplayConfig config, WeaponCatalog catalog, int weaponId)
        {
            WeaponProfile profile = config == null ? null : config.FindWeapon(weaponId);
            return Allows(config, catalog, profile) ? profile : null;
        }
    }
}
