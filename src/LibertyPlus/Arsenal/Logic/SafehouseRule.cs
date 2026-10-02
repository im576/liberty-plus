using System.Runtime.Serialization;

#pragma warning disable 0649
namespace LibertyFramework.Arsenal.Logic
{
    [DataContract]
    internal sealed class SafehouseRule
    {
        [DataMember(Name = "id", IsRequired = true)] internal string Id;
        [DataMember(Name = "name", IsRequired = true)] internal string Name;
        [DataMember(Name = "episode", IsRequired = true)] internal string Episode;
        [DataMember(Name = "x", IsRequired = true)] internal float X;
        [DataMember(Name = "y", IsRequired = true)] internal float Y;
        [DataMember(Name = "z", IsRequired = true)] internal float Z;
        [DataMember(Name = "radius", IsRequired = true)] internal float Radius;
        [DataMember(Name = "verified", IsRequired = true)] internal bool Verified;
    }
}
