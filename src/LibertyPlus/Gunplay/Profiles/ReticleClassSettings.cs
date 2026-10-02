using System.Runtime.Serialization;

// Fields are populated by DataContractJsonSerializer.
#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Profiles
{
    // gunplay.json `reticles.classes[]`: the style of one weapon class (pistol, smg, rifle, shotgun, heavy, sniper).
    [DataContract]
    internal sealed class ReticleClassSettings
    {
        [DataMember(Name = "class", IsRequired = true)] internal string WeaponClass;
        [DataMember(Name = "style", IsRequired = true)] internal ReticleStyleSettings Style;
    }
}
