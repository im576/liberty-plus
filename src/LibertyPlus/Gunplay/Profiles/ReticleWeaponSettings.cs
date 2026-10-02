using System.Runtime.Serialization;

// Fields are populated by DataContractJsonSerializer.
#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Profiles
{
    // gunplay.json `reticles.weapons[]`: one weapon's own reticle. It may borrow another class's style (`class`) and then
    // change single fields (`style`).
    [DataContract]
    internal sealed class ReticleWeaponSettings
    {
        [DataMember(Name = "weaponId", IsRequired = true)] internal int WeaponId;
        [DataMember(Name = "class", IsRequired = false)] internal string WeaponClass;
        [DataMember(Name = "style", IsRequired = false)] internal ReticleStyleSettings Style;
    }
}
