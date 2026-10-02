using System;
using System.IO;
using System.Runtime.Serialization;
using Liberty.Sdk;

#pragma warning disable 0649
namespace LibertyFramework.Arsenal.Logic
{
    // T-045: the Liberty weapon wheel's bindings and rules, the optional "weaponWheel" block of arsenal.json (an install that
    // predates it gets the block from packaging; without it the defaults below apply).
    [DataContract]
    internal sealed class WeaponWheelConfig
    {
        [DataMember(Name = "enabled", IsRequired = true)] internal bool Enabled;
        // Controller button held to open the wheel (a PadButton name). Back (Select / View) is not used by this mod; whether
        // the game itself does something with it on foot is for the owner to confirm (T-045 open question).
        [DataMember(Name = "padButton", IsRequired = true)] internal string PadButtonName;
        // Keyboard key held to open it (a VirtualKey name).
        [DataMember(Name = "keyboardKey", IsRequired = true)] internal string KeyboardKeyName;
        // A press shorter than this keeps the wheel open (sticky) for arrows/stick + Enter/A; a longer hold equips the
        // highlighted slot on release.
        [DataMember(Name = "tapMilliseconds", IsRequired = true)] internal int TapMilliseconds;
        [DataMember(Name = "allowInVehicle", IsRequired = true)] internal bool AllowInVehicle;

        internal PadButton Pad { get { return (PadButton)Enum.Parse(typeof(PadButton), PadButtonName); } }
        internal VirtualKey Key { get { return (VirtualKey)Enum.Parse(typeof(VirtualKey), KeyboardKeyName); } }

        internal static WeaponWheelConfig Defaults()
        {
            WeaponWheelConfig config = new WeaponWheelConfig();
            config.Enabled = true; config.PadButtonName = "Back"; config.KeyboardKeyName = "Tab"; config.TapMilliseconds = 250; config.AllowInVehicle = false;
            return config;
        }

        internal void Validate()
        {
            PadButton pad;
            VirtualKey key;
            if (!Enum.TryParse(PadButtonName, out pad) || pad == PadButton.None || !Enum.IsDefined(typeof(PadButton), pad) ||
                !Enum.TryParse(KeyboardKeyName, out key) || !Enum.IsDefined(typeof(VirtualKey), key))
            { throw new InvalidDataException("weaponWheel padButton or keyboardKey is not a known name"); }
            // The wheel's own navigation keys must stay free.
            if (key == VirtualKey.Enter || key == VirtualKey.Escape || key == VirtualKey.Back || key == VirtualKey.Left || key == VirtualKey.Right || key == VirtualKey.Up || key == VirtualKey.Down || key == VirtualKey.Space || key == VirtualKey.G || key == VirtualKey.E || (int)key == 0x21 || (int)key == 0x22)
            { throw new InvalidDataException("weaponWheel keyboardKey collides with the wheel's own navigation keys"); }
            if (pad == PadButton.A || pad == PadButton.B || pad == PadButton.X || pad == PadButton.Y || pad == PadButton.LeftShoulder || pad == PadButton.RightShoulder || pad == PadButton.DPadLeft || pad == PadButton.DPadRight || pad == PadButton.DPadUp || pad == PadButton.DPadDown)
            { throw new InvalidDataException("weaponWheel padButton collides with the wheel's own navigation buttons"); }
            if (TapMilliseconds < 50 || TapMilliseconds > 1000) { throw new InvalidDataException("weaponWheel tapMilliseconds must be 50-1000"); }
        }
    }
}
