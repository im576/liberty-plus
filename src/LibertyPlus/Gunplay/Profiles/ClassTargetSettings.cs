using System.Runtime.Serialization;

// Fields are populated by DataContractJsonSerializer.
#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Profiles
{
    // gunplay.json `classTargets[]` (T-042, optional): what a Stage 1 weapon class must deliver. STAGE1 Pillar 3 proposals until
    // the owner confirms them; GunplaySimulation measures each Stage 1 profile against its class (verifier and `gunplay sim`).
    [DataContract]
    internal sealed class ClassTargetSettings
    {
        // Catalog class (pistol, shotgun, smg, rifle).
        [DataMember(Name = "class", IsRequired = true)] internal string WeaponClass;
        // Standing, aiming, first shot: the cone must not exceed this.
        [DataMember(Name = "firstShotConeMaxDegrees", IsRequired = true)] internal double FirstShotConeMaxDegrees;
        // A burst of this many shots at the weapon's own fire rate, then a pause.
        [DataMember(Name = "burstShotCount", IsRequired = true)] internal int BurstShotCount;
        // After the burst the cone must be back to the first-shot cone (within recoveryToleranceFraction) within this time.
        [DataMember(Name = "burstRecoveryMaxMilliseconds", IsRequired = true)] internal double BurstRecoveryMaxMilliseconds;
        [DataMember(Name = "recoveryToleranceFraction", IsRequired = true)] internal double RecoveryToleranceFraction;
        // Sustained full-auto fire: 0 = the class has no sustained-fire target (semi-automatic classes).
        [DataMember(Name = "sustainedShots", IsRequired = true)] internal int SustainedShots;
        // Camera climb (degrees of pitch) at the end of the sustained burst must be inside this range.
        [DataMember(Name = "climbMinDegrees", IsRequired = true)] internal double ClimbMinDegrees;
        [DataMember(Name = "climbMaxDegrees", IsRequired = true)] internal double ClimbMaxDegrees;
    }
}
