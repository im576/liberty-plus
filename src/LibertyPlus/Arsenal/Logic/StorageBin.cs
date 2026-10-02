using System.Collections.Generic;
using System.Runtime.Serialization;
using LibertyFramework.Arsenal.Contracts;

#pragma warning disable 0649
namespace LibertyFramework.Arsenal.Logic
{
    [DataContract]
    internal sealed class StorageBin
    {
        [DataMember(Name = "id", IsRequired = true)] internal string Id;
        [DataMember(Name = "weapons", IsRequired = true)] internal List<WeaponRecord> Weapons = new List<WeaponRecord>();
    }
}
