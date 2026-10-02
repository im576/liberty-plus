using System.Runtime.Serialization;
using LibertyFramework.Arsenal.Contracts;

#pragma warning disable 0649
namespace LibertyFramework.Arsenal.Logic
{
    [DataContract]
    internal sealed class CategoryRule
    {
        [DataMember(Name = "category", IsRequired = true)] internal WeaponCategory Category;
        [DataMember(Name = "group", IsRequired = true)] internal string Group;
        [DataMember(Name = "bodySlot", IsRequired = true)] internal BodySlot BodySlot;
    }
}
