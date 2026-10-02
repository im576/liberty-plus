using System.Runtime.Serialization;

#pragma warning disable 0649

namespace LibertyFramework.Gunplay.Profiles
{
    // T-015 shoulder swap: which inputs flip the aim camera to the other shoulder and how fast it slides.
    [DataContract]
    internal sealed class ShoulderSwapSettings
    {
        [DataMember(Name = "enabled", IsRequired = true)] internal bool Enabled;
        // XInput button name: LeftShoulder, RightThumb, LeftThumb, XButton, YButton.
        [DataMember(Name = "controllerButton", IsRequired = true)] internal string ControllerButton;
        // System.Windows.Forms.Keys name, e.g. "Z".
        [DataMember(Name = "keyboardKey", IsRequired = true)] internal string KeyboardKey;
        [DataMember(Name = "transitionMilliseconds", IsRequired = true)] internal double TransitionMilliseconds;
        // true: the swap only reacts while aiming (the chosen side still persists afterwards).
        [DataMember(Name = "requireAiming", IsRequired = true)] internal bool RequireAiming;
    }
}
