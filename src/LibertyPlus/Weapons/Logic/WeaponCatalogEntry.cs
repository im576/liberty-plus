using System.Collections.Generic;
using System.Runtime.Serialization;

#pragma warning disable 0649
namespace LibertyFramework.Weapons.Logic
{
    [DataContract]
    internal sealed class WeaponCatalogEntry
    {
        [DataMember(Name = "id", IsRequired = true)] internal string Id;
        [DataMember(Name = "weaponId", IsRequired = true)] internal int WeaponId;
        [DataMember(Name = "family", IsRequired = true)] internal string Family;
        [DataMember(Name = "label", IsRequired = true)] internal string Label;
        [DataMember(Name = "role", IsRequired = true)] internal string Role;
        [DataMember(Name = "model", IsRequired = true)] internal string Model;
        [DataMember(Name = "finishes", IsRequired = true)] internal List<string> Finishes;
        [DataMember(Name = "attachments", IsRequired = true)] internal List<string> Attachments;

        // Stage 1 fields (T-041), all optional so older catalogs still load.
        // WeaponInfo.xml type of this weapon id (the game's own name, for example AK47).
        [DataMember(Name = "weaponInfoType", IsRequired = false)] internal string WeaponInfoType;
        // pistol, revolver, shotgun, smg, rifle, sniper, lmg, pdw or military; T-042/T-043 select handling and reticle by class.
        [DataMember(Name = "class", IsRequired = false)] internal string WeaponClass;
        // Tier id from the catalog's tiers[].
        [DataMember(Name = "tier", IsRequired = false)] internal string Tier;
        // True = a Stage 1 arsenal weapon: it gets the Liberty gunplay profile and the availability rules.
        // False/absent = it keeps the game's own behaviour (AGENTS.md rule 2).
        [DataMember(Name = "stage1", IsRequired = false)] internal bool Stage1;
        // Name (`profileName`) of this weapon's gunplay.json weapons[] entry, which points back with `catalogId` = this entry's id.
        [DataMember(Name = "profile", IsRequired = false)] internal string Profile;
        [DataMember(Name = "availability", IsRequired = false)] internal WeaponAvailabilityRule Availability;
        // The identity stats Stage 1 writes into WeaponInfo.xml, and the game's own values they replace (what the file holds
        // when the catalog's applyWeaponInfoStats is false).
        [DataMember(Name = "stats", IsRequired = false)] internal WeaponStats Stats;
        [DataMember(Name = "vanillaStats", IsRequired = false)] internal WeaponStats VanillaStats;
    }
}
