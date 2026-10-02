using System.Runtime.Serialization;

// Fields are populated by DataContractJsonSerializer.
#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Profiles
{
    [DataContract]
    internal sealed class FreeAimSettings
    {
        [DataMember(Name = "enabledOnStartup", IsRequired = true)] internal bool EnabledOnStartup;
        [DataMember(Name = "disableLockOn", IsRequired = true)] internal bool DisableLockOn;
        [DataMember(Name = "forceAutoAimOff", IsRequired = true)] internal bool ForceAutoAimOff;
        [DataMember(Name = "hideTargetHealth", IsRequired = true)] internal bool HideTargetHealth;
        [DataMember(Name = "profile", IsRequired = true)] internal string Profile;
    }
}
