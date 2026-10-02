namespace LibertyFramework.Arsenal.Contracts
{
    // A weapon currently on the player, as Holsters needs it: which model to show and where.
    internal sealed class CarriedWeapon
    {
        internal CarriedWeapon(int weaponId, WeaponCategory category, BodySlot slot, bool inHand)
        {
            WeaponId = weaponId;
            Category = category;
            Slot = slot;
            InHand = inHand;
        }

        internal int WeaponId { get; private set; }
        internal WeaponCategory Category { get; private set; }
        internal BodySlot Slot { get; private set; }
        // True while this weapon is the one in the player's hands (its holster prop is hidden).
        internal bool InHand { get; private set; }
    }
}
