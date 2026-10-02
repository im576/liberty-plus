using System.Runtime.Serialization;

#pragma warning disable 0649
namespace LibertyFramework.Arsenal.Logic
{
    // S-3 trunk choreography timings (arsenal.json "trunkTimings", milliseconds). A step with a min/max pair ends when
    // its clip stops after min, or at max, so a clip that fails to load never traps the player.
    [DataContract]
    internal sealed class TrunkTimings
    {
        [DataMember(Name = "turnMilliseconds", IsRequired = true)] internal int TurnMilliseconds;
        [DataMember(Name = "lidOpenAtMilliseconds", IsRequired = true)] internal int LidOpenAtMilliseconds;
        [DataMember(Name = "openMinMilliseconds", IsRequired = true)] internal int OpenMinMilliseconds;
        [DataMember(Name = "openMaxMilliseconds", IsRequired = true)] internal int OpenMaxMilliseconds;
        [DataMember(Name = "idleMinMilliseconds", IsRequired = true)] internal int IdleMinMilliseconds;
        [DataMember(Name = "withdrawMinMilliseconds", IsRequired = true)] internal int WithdrawMinMilliseconds;
        [DataMember(Name = "withdrawMaxMilliseconds", IsRequired = true)] internal int WithdrawMaxMilliseconds;
        [DataMember(Name = "browseStepTimeoutMilliseconds", IsRequired = true)] internal int BrowseStepTimeoutMilliseconds;
        [DataMember(Name = "lidCloseAtMilliseconds", IsRequired = true)] internal int LidCloseAtMilliseconds;
        [DataMember(Name = "closeMinMilliseconds", IsRequired = true)] internal int CloseMinMilliseconds;
        [DataMember(Name = "closeMaxMilliseconds", IsRequired = true)] internal int CloseMaxMilliseconds;

        internal bool Valid
        {
            get
            {
                return In(TurnMilliseconds, 0, 5000) && In(LidOpenAtMilliseconds, 0, 5000) && MinMax(OpenMinMilliseconds, OpenMaxMilliseconds) &&
                    In(IdleMinMilliseconds, 0, 5000) && MinMax(WithdrawMinMilliseconds, WithdrawMaxMilliseconds) &&
                    In(BrowseStepTimeoutMilliseconds, 100, 30000) && In(LidCloseAtMilliseconds, 0, 5000) && MinMax(CloseMinMilliseconds, CloseMaxMilliseconds);
            }
        }

        private static bool In(int value, int min, int max) { return value >= min && value <= max; }
        private static bool MinMax(int min, int max) { return In(min, 0, 10000) && In(max, 100, 10000) && min <= max; }
    }
}
