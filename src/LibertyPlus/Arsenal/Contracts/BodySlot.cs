namespace LibertyFramework.Arsenal.Contracts
{
    // Where a carried weapon lives on the player's body (RDR2 model + melee): two sidearms,
    // two long guns, one melee. Thrown weapons are carried but have no body slot.
    // Shared contract between Arsenal Core (fills it) and Holsters (draws it). T-020/T-021.
    internal enum BodySlot
    {
        None = 0,
        SidearmPrimary = 1,
        SidearmSecondary = 2,
        LongGun1 = 3,
        LongGun2 = 4,
        Melee = 5
    }
}
