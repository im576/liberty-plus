using System.Runtime.Serialization;

#pragma warning disable 0649

namespace LibertyFramework.Gunplay
{
    [DataContract]
    internal sealed class FeelFovRestoreState
    {
        [DataMember(Name = "processId", IsRequired = true)] internal int ProcessId;
        [DataMember(Name = "processStartUtcTicks", IsRequired = true)] internal long ProcessStartUtcTicks;
        [DataMember(Name = "cameraHandle", IsRequired = true)] internal int CameraHandle;
        [DataMember(Name = "originalFov", IsRequired = true)] internal float OriginalFov;
    }
}
