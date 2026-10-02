using System.Collections.Generic;
using System.Runtime.Serialization;

#pragma warning disable 0649

namespace LibertyFramework.Arsenal.Holsters
{
    [DataContract]
    internal sealed class HolsterPropJournal
    {
        [DataMember(Name = "processId", IsRequired = true)] internal int ProcessId;
        [DataMember(Name = "processStartUtcTicks", IsRequired = true)] internal long ProcessStartUtcTicks;
        [DataMember(Name = "props", IsRequired = true)] internal List<HolsterPropRecord> Props;
    }
}
