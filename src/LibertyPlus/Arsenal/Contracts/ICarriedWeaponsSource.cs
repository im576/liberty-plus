using System.Collections.Generic;

namespace LibertyFramework.Arsenal.Contracts
{
    // Implemented by Arsenal Core (T-020); read by Holsters (T-021) once per tick.
    // Revision increases whenever the carried set or a body-slot assignment changes.
    internal interface ICarriedWeaponsSource
    {
        int Revision { get; }
        IList<CarriedWeapon> Carried { get; }
        // Active physical weapon modifier; 1.0 when no supported attachment is fitted.
        double PerShotBloomMultiplier(int weaponId);
    }
}
