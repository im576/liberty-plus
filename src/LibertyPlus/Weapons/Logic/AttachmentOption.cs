using System.Runtime.Serialization;

#pragma warning disable 0649
namespace LibertyFramework.Weapons.Logic
{
    [DataContract]
    internal sealed class AttachmentOption
    {
        [DataMember(Name = "id", IsRequired = true)] internal string Id;
        [DataMember(Name = "label", IsRequired = true)] internal string Label;
        [DataMember(Name = "price", IsRequired = true)] internal int Price;
        [DataMember(Name = "perShotBloomMultiplier", IsRequired = true)] internal double PerShotBloomMultiplier;
    }
}
