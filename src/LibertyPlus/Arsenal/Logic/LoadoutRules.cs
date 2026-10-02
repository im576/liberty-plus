using System.Collections.Generic;
using System.Runtime.Serialization;
using LibertyFramework.Arsenal.Contracts;

#pragma warning disable 0649
namespace LibertyFramework.Arsenal.Logic
{
    // Liberty Vanilla+ Stage 1 physical loadout (STAGE1 section 7): 1 sidearm + 2 long guns, limited carried ammunition.
    // It is a block of its own in arsenal.json so an installed file that predates it still loads (packaging adds the
    // missing block); "enabled": false restores the general limits and category groups of the file.
    [DataContract]
    internal sealed class LoadoutRules
    {
        [DataMember(Name = "enabled", IsRequired = true)] internal bool Enabled;
        [DataMember(Name = "sidearmLimit", IsRequired = true)] internal int SidearmLimit;
        [DataMember(Name = "longGunLimit", IsRequired = true)] internal int LongGunLimit;
        // Categories whose group or body slot differ under the loadout (SMGs count as long guns).
        [DataMember(Name = "categoryOverrides", IsRequired = false)] internal List<CategoryRule> CategoryOverrides;
        // Most rounds Niko may carry per weapon category; a category without an entry is not limited.
        [DataMember(Name = "ammoCaps", IsRequired = false)] internal List<AmmoCap> AmmoCaps;
    }

    [DataContract]
    internal sealed class AmmoCap
    {
        [DataMember(Name = "category", IsRequired = true)] internal WeaponCategory Category;
        [DataMember(Name = "maximumRounds", IsRequired = true)] internal int MaximumRounds;
    }
}
