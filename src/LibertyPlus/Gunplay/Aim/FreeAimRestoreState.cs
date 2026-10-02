using System.Runtime.Serialization;

// Fields are populated by DataContractJsonSerializer.
#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Aim
{
    // scripts/LibertyFramework/state/freeaim_restore.json: the player's own settings, written
    // before free aim changes anything so a crash cannot leave the game profile altered.
    [DataContract]
    internal sealed class FreeAimRestoreState
    {
        [DataMember(Name = "schemaVersion", IsRequired = true, Order = 0)] internal int SchemaVersion;
        [DataMember(Name = "autoAimPreference", IsRequired = true, Order = 1)] internal int AutoAimPreference;
        [DataMember(Name = "lockOnDisabledBefore", IsRequired = true, Order = 2)] internal bool LockOnDisabledBefore;
        [DataMember(Name = "savedUtc", IsRequired = true, Order = 3)] internal string SavedUtc;
    }
}
