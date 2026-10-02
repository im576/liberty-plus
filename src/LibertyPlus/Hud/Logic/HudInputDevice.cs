using System;

namespace LibertyFramework.Hud.Logic
{
    internal enum HudDevice { Keyboard, Pad }

    // T-049: which device's button names a prompt shows. "pad" and "keyboard" are forced by config; "auto" follows the device
    // used last, with a minimum time between switches so a stray key press during pad play does not flip the glyphs. The game has
    // no "last used device" that can be read, so the module feeds in what it saw: pad buttons/sticks/triggers, or keyboard keys.
    internal sealed class HudInputDevice
    {
        private HudDevice current = HudDevice.Keyboard;
        private int lastSwitchMilliseconds = int.MinValue;

        internal HudDevice Current { get { return current; } }

        internal HudDevice Update(string setting, bool padActive, bool keyboardActive, int nowMilliseconds, int minimumSwitchMilliseconds)
        {
            if (setting == "pad") { current = HudDevice.Pad; return current; }
            if (setting == "keyboard") { current = HudDevice.Keyboard; return current; }
            bool settled = lastSwitchMilliseconds == int.MinValue || unchecked(nowMilliseconds - lastSwitchMilliseconds) >= minimumSwitchMilliseconds;
            if (padActive == keyboardActive || !settled) { return current; }
            HudDevice wanted = padActive ? HudDevice.Pad : HudDevice.Keyboard;
            if (wanted != current)
            {
                current = wanted;
                lastSwitchMilliseconds = nowMilliseconds;
            }
            return current;
        }
    }
}
