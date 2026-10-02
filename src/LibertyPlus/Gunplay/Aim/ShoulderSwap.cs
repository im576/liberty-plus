using System;
using Keys = System.Windows.Forms.Keys;
using GTA;
using LibertyFramework.Core.Input;
using LibertyFramework.Core.Logging;
using LibertyFramework.GameApi;
using LibertyFramework.Gunplay.Profiles;

namespace LibertyFramework.Gunplay.Aim
{
    // T-015: press the configured button (default LB / Z) to slide the aim camera to the other shoulder.
    // The chosen side persists until swapped back; the slide eases over transitionMilliseconds.
    internal sealed class ShoulderSwap
    {
        private readonly AimCameraSettings settings;
        private double target = 1.0;
        private double current = 1.0;
        private bool previousKey;
        private string loggedBadKey;

        internal ShoulderSwap(AimCameraSettings settings)
        {
            this.settings = settings;
        }

        internal bool Left { get { return target < 0; } }
        internal double Target { get { return target; } }
        internal double Current { get { return current; } }

        // Test hook (T-042 `swap` command): choose a side without the aim button; the slide then runs in Update as usual.
        internal void Force(bool left)
        {
            target = left ? -1.0 : 1.0;
            RuntimeLog.Info("shoulder_swap side=" + (left ? "left" : "right") + " forced=true");
        }

        internal void Update(ShoulderSwapSettings config, ControllerInput controller, bool aiming, bool blocked, double deltaSeconds)
        {
            if (config == null || !config.Enabled)
            {
                target = 1.0;
                current = 1.0;
                settings.Apply(1.0);
                return;
            }
            bool keyDown = KeyDown(config.KeyboardKey);
            bool pressed = !blocked && (!config.RequireAiming || aiming) &&
                (controller.WasPressed(Button(config.ControllerButton)) || (keyDown && !previousKey));
            previousKey = keyDown;
            if (pressed)
            {
                target = -target;
                RuntimeLog.Info("shoulder_swap side=" + (target < 0 ? "left" : "right"));
            }
            double step = config.TransitionMilliseconds <= 0 ? 2.0 : 2.0 * (deltaSeconds * 1000.0) / config.TransitionMilliseconds;
            if (current < target) { current = Math.Min(target, current + step); }
            else if (current > target) { current = Math.Max(target, current - step); }
            settings.Apply(current);
        }

        internal void Restore()
        {
            target = 1.0;
            current = 1.0;
            settings.Restore();
        }

        private bool KeyDown(string name)
        {
            Keys key;
            if (string.IsNullOrEmpty(name) || !Enum.TryParse<Keys>(name, true, out key))
            {
                if (loggedBadKey != name) { loggedBadKey = name; RuntimeLog.Error("shoulder_swap_bad_key " + name); }
                return false;
            }
            return Game.isKeyPressed(key);
        }

        private static ushort Button(string name)
        {
            switch (name)
            {
                case "RightShoulder": return ControllerInput.RightShoulder;
                case "LeftThumb": return ControllerInput.LeftThumb;
                case "RightThumb": return ControllerInput.RightThumb;
                case "XButton": return ControllerInput.XButton;
                case "YButton": return ControllerInput.YButton;
                default: return ControllerInput.LeftShoulder;
            }
        }
    }
}
