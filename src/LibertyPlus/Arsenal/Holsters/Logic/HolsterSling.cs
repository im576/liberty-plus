using System.Runtime.Serialization;

#pragma warning disable 0649

namespace LibertyFramework.Arsenal.Holsters.Logic
{
    // W-5: a strap model shown while a long-gun slot is occupied. The model is authored in the bone's bind-pose space
    // by tools/models (config/models/sling.json), so it attaches with zero offset and zero rotation.
    [DataContract]
    internal sealed class HolsterSling
    {
        [DataMember(Name = "slot", IsRequired = true)] internal string Slot;
        [DataMember(Name = "model", IsRequired = true)] internal string Model;
        [DataMember(Name = "bone", IsRequired = true)] internal string Bone;
    }
}
