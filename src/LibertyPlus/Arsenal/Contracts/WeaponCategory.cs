namespace LibertyFramework.Arsenal.Contracts
{
    // GTA IV inventory slot category of a weapon (one weapon per category in the game's inventory).
    // Adapters map ScriptHookDotNet's WeaponSlot onto this; pure logic and config use only this enum.
    internal enum WeaponCategory
    {
        Unarmed = 0,
        Melee = 1,
        Handgun = 2,
        Shotgun = 3,
        SMG = 4,
        Rifle = 5,
        Sniper = 6,
        Heavy = 7,
        Thrown = 8,
        Other = 9
    }
}
