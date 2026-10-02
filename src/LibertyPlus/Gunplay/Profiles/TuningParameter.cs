using System.Runtime.Serialization;

// Fields are populated by DataContractJsonSerializer.
#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Profiles
{
    [DataContract]
    internal sealed class TuningParameter
    {
        [DataMember(Name = "key", IsRequired = true)] internal string Key;
        [DataMember(Name = "step", IsRequired = true)] internal double Step;
        [DataMember(Name = "min", IsRequired = true)] internal double Minimum;
        [DataMember(Name = "max", IsRequired = true)] internal double Maximum;
    }
}
