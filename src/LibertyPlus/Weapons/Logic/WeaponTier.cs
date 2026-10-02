using System.Collections.Generic;
using System.Runtime.Serialization;

#pragma warning disable 0649
namespace LibertyFramework.Weapons.Logic
{
    // A rarity tier of the Stage 1 arsenal (common, less-common, rare) or a tier outside normal availability
    // (restricted: only where a mission hands the weapon over; test: developer weapons).
    [DataContract]
    internal sealed class WeaponTier
    {
        [DataMember(Name = "id", IsRequired = true)] internal string Id;
        [DataMember(Name = "label", IsRequired = true)] internal string Label;
        // False = never for sale or in the world under Stage 1's own rules.
        [DataMember(Name = "normalAvailability", IsRequired = true)] internal bool NormalAvailability;
        [DataMember(Name = "minimumStoryProgress", IsRequired = false)] internal double MinimumStoryProgress;
        [DataMember(Name = "sources", IsRequired = false)] internal List<string> Sources;
    }
}
