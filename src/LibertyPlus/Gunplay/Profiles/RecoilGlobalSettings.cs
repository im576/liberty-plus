using System.Runtime.Serialization;

// Fields are populated by DataContractJsonSerializer.
#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Profiles
{
    [DataContract]
    internal sealed class RecoilGlobalSettings
    {
        [DataMember(Name = "enableCameraKick", IsRequired = true)] internal bool EnableCameraKick;
        // Real Recoil (WeaponRecoil.net.dll) also kicks the camera; both together would double the recoil.
        [DataMember(Name = "allowWithRealRecoil", IsRequired = true)] internal bool AllowWithRealRecoil;
        [DataMember(Name = "recoveryCancelStickThreshold", IsRequired = true)] internal double RecoveryCancelStickThreshold;
        [DataMember(Name = "cameraValidationToleranceDegrees", IsRequired = true)] internal double CameraValidationToleranceDegrees;
        [DataMember(Name = "cameraValidationSamples", IsRequired = true)] internal int CameraValidationSamples;
        [DataMember(Name = "maximumDeltaSeconds", IsRequired = true)] internal double MaximumDeltaSeconds;
    }
}
