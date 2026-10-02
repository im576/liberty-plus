using System;

namespace LibertyFramework.CombatEffects.Logic
{
    // What one exact hit means for the gore presentation (T-047): pure decisions from the weapon class table, the hit
    // region and the attacker-to-victim distance. No game calls, so the offline verifier tests every branch.
    internal static class GoreClassifier
    {
        internal enum Severity { None, Head, Trauma }

        // The class of a weapon id, or null when no configured class lists it.
        internal static GoreWeaponClass Find(GoreWeaponClass[] classes, int weaponId)
        {
            if (classes == null) return null;
            foreach (GoreWeaponClass weaponClass in classes)
                if (weaponClass != null && Array.IndexOf(weaponClass.WeaponIds, weaponId) >= 0) return weaponClass;
            return null;
        }

        // Caliber and distance: the class multiplier, falling from 1 at point blank to distanceScaleFloor at
        // effectFalloffMeters (linear) and staying there beyond. No class = neutral.
        internal static float DistanceScale(GoreWeaponClass weaponClass, float distanceMeters)
        {
            if (weaponClass == null) return 1.0f;
            float fraction = Math.Max(0f, Math.Min(1f, distanceMeters / weaponClass.EffectFalloffMeters));
            float falloff = 1.0f - (1.0f - weaponClass.DistanceScaleFloor) * fraction;
            return weaponClass.ScaleMultiplier * falloff;
        }

        // A firearm head hit inside the class's range is severe head trauma; a hit to any region inside the class's
        // trauma range (shotgun pellets at close range) is trauma. Head wins when both apply.
        internal static Severity Classify(GoreWeaponClass weaponClass, HitRegion region, float distanceMeters)
        {
            if (weaponClass == null) return Severity.None;
            if (region == HitRegion.Head && distanceMeters <= weaponClass.SevereHeadMaximumMeters) return Severity.Head;
            if (weaponClass.TraumaMaximumMeters > 0 && distanceMeters <= weaponClass.TraumaMaximumMeters) return Severity.Trauma;
            return Severity.None;
        }
    }
}
