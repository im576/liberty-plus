using System.Collections.Generic;
using System.Runtime.Serialization;

// Fields are populated by DataContractJsonSerializer.
#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Profiles
{
    // config/presets/*.json: a named set of per-weapon recoil/spread profiles.
    [DataContract]
    internal sealed class PresetFile
    {
        [DataMember(Name = "schemaVersion", IsRequired = true, Order = 0)] internal int SchemaVersion;
        [DataMember(Name = "name", IsRequired = true, Order = 1)] internal string Name;
        [DataMember(Name = "description", IsRequired = true, Order = 2)] internal string Description;
        [DataMember(Name = "weapons", IsRequired = true, Order = 3)] internal List<PresetWeapon> Weapons;
    }
}
