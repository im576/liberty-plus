using System;
using GTA;
using LibertyFramework.Core.Logging;
using LibertyFramework.Gunplay.Profiles;

namespace LibertyFramework.Weapons
{
    // Give/select/refill actions for the registered test weapons. A custom weapon shares its
    // inventory slot with its vanilla counterpart (T-007), so switching moves the total ammo.
    internal static class TestWeaponActions
    {
        internal static string Select(Player player, WeaponProfile profile, bool useTest)
        {
            Ped ped = RequirePed(player);
            Weapon custom = (Weapon)profile.WeaponId;
            Weapon vanilla = (Weapon)profile.VanillaWeaponId;
            bool customPresent = ped.Weapons.FromType(custom).isPresent;
            bool vanillaPresent = ped.Weapons.FromType(vanilla).isPresent;
            int ammo = customPresent ? ped.Weapons.FromType(custom).Ammo : vanillaPresent ? ped.Weapons.FromType(vanilla).Ammo : 0;
            Weapon target = useTest ? custom : vanilla;
            ped.Weapons.Select(target);
            ped.Weapons.FromType(target).Ammo = customPresent || vanillaPresent ? ammo : profile.InitialAmmo;
            RuntimeLog.Info("weapon_select id=" + (int)target + " label=" + (useTest ? profile.Label : "vanilla " + profile.VanillaWeaponId) +
                " ammo=" + ped.Weapons.FromType(target).Ammo);
            return Status(player, null);
        }

        internal static string GiveAll(Player player, GunplayConfig config)
        {
            // Test weapons only: the Stage 1 catalog weapons are given by the catalog command (T-041).
            System.Collections.Generic.List<WeaponProfile> testWeapons = config.TestWeapons();
            foreach (WeaponProfile profile in testWeapons) { Select(player, profile, true); }
            // Leave the pistol in hand; it is the first profile and the finish test weapon.
            if (testWeapons.Count > 0) { Select(player, testWeapons[0], true); }
            return "All test weapons given; " + Status(player, config);
        }

        internal static string Refill(Player player, int rounds)
        {
            Ped ped = RequirePed(player);
            GTA.value.Weapon current = ped.Weapons.Current;
            if (current == null || current.Type == Weapon.Unarmed || current.Type == Weapon.None) { return "No weapon in hand"; }
            current.Ammo = Math.Max(current.Ammo, rounds);
            int clip = current.MaxAmmoInClip;
            if (clip > 0) { current.AmmoInClip = clip; }
            RuntimeLog.Info("weapon_refill id=" + (int)current.Type + " ammo=" + current.Ammo + " clip=" + current.AmmoInClip);
            return "Refilled: " + Status(player, null);
        }

        // Called every frame while infinite ammo is on: keeps the reserve topped up, leaves reloads intact.
        internal static void KeepReserve(Player player, int rounds)
        {
            if (player == null || player.Character == null) { return; }
            GTA.value.Weapon current = player.Character.Weapons.Current;
            if (current == null || current.Type == Weapon.Unarmed || current.Type == Weapon.None) { return; }
            if (current.Ammo < rounds / 2) { current.Ammo = rounds; }
        }

        internal static string Status(Player player, GunplayConfig config)
        {
            Ped ped = RequirePed(player);
            int id = (int)ped.Weapons.CurrentType;
            GTA.value.Weapon current = ped.Weapons.Current;
            string label = LabelFor(config, id);
            return "ID=" + id + " " + label + " ammo=" + (current != null ? current.Ammo : 0) + " clip=" + (current != null ? current.AmmoInClip : 0);
        }

        internal static string LabelFor(GunplayConfig config, int weaponId)
        {
            if (config != null)
            {
                WeaponProfile profile = config.FindWeapon(weaponId);
                if (profile != null) { return profile.Label; }
                foreach (WeaponProfile weapon in config.TestWeapons())
                {
                    if (weapon.VanillaWeaponId == weaponId) { return "Vanilla (" + weapon.Label.Replace("Gold ", "") + ")"; }
                }
            }
            return Enum.IsDefined(typeof(Weapon), weaponId) ? ((Weapon)weaponId).ToString() : "Weapon " + weaponId;
        }

        private static Ped RequirePed(Player player)
        {
            if (player == null || player.Character == null) { throw new InvalidOperationException("Player is not ready"); }
            return player.Character;
        }
    }
}
