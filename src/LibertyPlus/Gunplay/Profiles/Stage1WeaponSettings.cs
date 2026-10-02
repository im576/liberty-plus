using System.Runtime.Serialization;

// Fields are populated by DataContractJsonSerializer.
#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Profiles
{
    // gunplay.json `stage1Weapons` (T-041, optional; absent = enabled): the switch for the Liberty profiles of the Stage 1
    // catalog weapons. Off = those weapons behave as vanilla; the registered test weapons (58+) are not affected.
    [DataContract]
    internal sealed class Stage1WeaponSettings
    {
        [DataMember(Name = "enabled", IsRequired = true)] internal bool Enabled;
    }
}
