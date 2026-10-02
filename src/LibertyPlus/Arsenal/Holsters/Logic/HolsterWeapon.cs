using System.Runtime.Serialization;
using LibertyFramework.Arsenal.Contracts;

#pragma warning disable 0649

namespace LibertyFramework.Arsenal.Holsters.Logic
{
    [DataContract]
    internal sealed class HolsterWeapon
    {
        [DataMember(Name = "weaponId", IsRequired = true)] internal int WeaponId;
        [DataMember(Name = "weaponInfoType", IsRequired = true)] internal string WeaponInfoType;
        [DataMember(Name = "category", IsRequired = true)] internal WeaponCategory Category;
    }
}
