using System.Collections.Generic;
using System.Runtime.Serialization;

// Fields are populated by DataContractJsonSerializer.
#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Profiles
{
    // gunplay.json `reticles` (T-043, optional; absent = the old single `crosshair` for every weapon).
    [DataContract]
    internal sealed class ReticleSettings
    {
        // Whole section on/off: false = the old single crosshair.
        [DataMember(Name = "enabled", IsRequired = true)] internal bool Enabled;
        // What to draw while the weapon is not aimed: hidden, or reduced (the style at notAimingOpacity).
        [DataMember(Name = "notAiming", IsRequired = true)] internal string NotAiming;
        [DataMember(Name = "notAimingOpacity", IsRequired = true)] internal double NotAimingOpacity;
        [DataMember(Name = "hideInVehicle", IsRequired = true)] internal bool HideInVehicle;
        // Log the drawn opening against the live cone (the truthfulness check); the `reticle debug` command turns it on for a session.
        [DataMember(Name = "debugLog", IsRequired = true)] internal bool DebugLog;
        [DataMember(Name = "classes", IsRequired = true)] internal List<ReticleClassSettings> Classes;
        [DataMember(Name = "weapons", IsRequired = false)] internal List<ReticleWeaponSettings> Weapons;
    }
}
