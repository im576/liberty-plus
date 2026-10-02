using System.Runtime.Serialization;
using System.Collections.Generic;

// Fields are populated by DataContractJsonSerializer.
#pragma warning disable 0649

namespace LibertyFramework.Arsenal.Contracts
{
    // One physical weapon the player owns or carries: what moves between body, trunk and stash.
    // Extend with new optional DataMembers (IsRequired = false) only; never rename existing ones.
    [DataContract]
    internal sealed class WeaponRecord
    {
        [DataMember(Name = "weaponId", IsRequired = true, Order = 0)] internal int WeaponId;
        [DataMember(Name = "category", IsRequired = true, Order = 1)] internal WeaponCategory Category;
        [DataMember(Name = "ammo", IsRequired = true, Order = 2)] internal int Ammo;
        [DataMember(Name = "owned", IsRequired = true, Order = 3)] internal bool Owned;
        [DataMember(Name = "finish", IsRequired = false, Order = 4)] internal string Finish;
        [DataMember(Name = "acquiredUtc", IsRequired = false, Order = 5)] internal string AcquiredUtc;
        [DataMember(Name = "instanceId", IsRequired = false, Order = 6)] internal string InstanceId;
        [DataMember(Name = "catalogId", IsRequired = false, Order = 7)] internal string CatalogId;
        [DataMember(Name = "attachments", IsRequired = false, Order = 8)] internal List<string> Attachments;
        [DataMember(Name = "progression", IsRequired = false, Order = 9)] internal int Progression;

        internal WeaponRecord Clone()
        {
            WeaponRecord copy = (WeaponRecord)MemberwiseClone();
            copy.Attachments = Attachments == null ? null : new List<string>(Attachments);
            return copy;
        }
    }
}
