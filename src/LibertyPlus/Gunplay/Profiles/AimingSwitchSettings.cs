using System.Runtime.Serialization;

#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Profiles
{
    [DataContract]
    internal sealed class AimingSwitchSettings
    {
        [DataMember(Name = "enabled", IsRequired = true)] internal bool Enabled;
        [DataMember(Name = "previousButton", IsRequired = true)] internal string PreviousButton;
        [DataMember(Name = "nextButton", IsRequired = true)] internal string NextButton;
    }
}
