using System.Runtime.Serialization;

// Fields are populated by DataContractJsonSerializer.
#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Profiles
{
    [DataContract]
    internal sealed class PresetWeapon
    {
        [DataMember(Name = "weaponId", IsRequired = true, Order = 0)] internal int WeaponId;
        [DataMember(Name = "profileName", IsRequired = true, Order = 1)] internal string ProfileName;
        [DataMember(Name = "recoil", IsRequired = true, Order = 2)] internal RecoilProfile Recoil;
        [DataMember(Name = "spread", IsRequired = true, Order = 3)] internal SpreadProfile Spread;
    }
}
