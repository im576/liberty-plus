using LibertyFramework.Arsenal.Contracts;

namespace LibertyFramework.Arsenal.Holsters.Logic
{
    internal static class HolsterRules
    {
        internal static BodySlot SlotFor(WeaponCategory category, bool firstLongGun)
        {
            if (category == WeaponCategory.Handgun) { return BodySlot.SidearmPrimary; }
            if (category == WeaponCategory.SMG) { return BodySlot.SidearmSecondary; }
            if (category == WeaponCategory.Melee) { return BodySlot.Melee; }
            if (category == WeaponCategory.Rifle || category == WeaponCategory.Shotgun ||
                category == WeaponCategory.Sniper || category == WeaponCategory.Heavy)
            {
                return firstLongGun ? BodySlot.LongGun1 : BodySlot.LongGun2;
            }
            return BodySlot.None;
        }

        internal static bool Visible(bool inHand, bool alive, bool playing, bool faded, bool inVehicle,
            bool onBike, bool showOnBikes)
        {
            return !inHand && alive && playing && !faded && (!inVehicle || (onBike && showOnBikes));
        }
    }
}
