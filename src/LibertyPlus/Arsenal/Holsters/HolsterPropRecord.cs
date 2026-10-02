using System.Runtime.Serialization;

#pragma warning disable 0649

namespace LibertyFramework.Arsenal.Holsters
{
    [DataContract]
    internal sealed class HolsterPropRecord
    {
        [DataMember(Name = "handle", IsRequired = true)] internal int Handle;
        [DataMember(Name = "modelHash", IsRequired = true)] internal int ModelHash;
    }
}
