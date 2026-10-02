using System.Collections.Generic;
using System.Runtime.Serialization;

#pragma warning disable 0649
namespace LibertyFramework.Weapons.Logic
{
    // How a Stage 1 weapon reaches the player (T-041). Data only; WeaponAvailability evaluates it.
    // Availability comes from the world, contacts and money, never XP.
    [DataContract]
    internal sealed class WeaponAvailabilityRule
    {
        // Where it can be found or bought: gun-shop, street-dealer, contact, mission-reward, mission-only.
        [DataMember(Name = "sources", IsRequired = false)] internal List<string> Sources;
        // Dollars charged by the sources that sell it; 0 = not sold for money.
        [DataMember(Name = "price", IsRequired = false)] internal int Price;
        // Contact id that has to be unlocked first (for example little-jacob); empty = none.
        [DataMember(Name = "contact", IsRequired = false)] internal string Contact;
        // Fraction of the story (0-1) that must be finished; null = the tier's value.
        [DataMember(Name = "minimumStoryProgress", IsRequired = false)] internal double? MinimumStoryProgress;
        [DataMember(Name = "note", IsRequired = false)] internal string Note;
    }
}
