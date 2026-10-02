using System;

namespace LibertyFramework.Hud.Logic
{
    // T-049: one HUD element's fade. An element is wanted while its condition holds or a trigger's hold time has not run out; its
    // opacity then rises at 1/fadeIn per second, and falls at 1/fadeOut per second once nothing wants it. Times are seconds on
    // one monotonic clock (the module's stopwatch), so the logic is the same in the game and in the offline checks.
    internal sealed class HudPresence
    {
        private double alpha;
        private double holdUntilSeconds = double.NegativeInfinity;

        internal double Alpha { get { return alpha; } }
        internal bool Visible { get { return alpha > 0.001; } }

        // Something happened that should show the element for at least holdSeconds from now.
        internal void Trigger(double nowSeconds, double holdSeconds)
        {
            if (holdSeconds <= 0) { return; }
            holdUntilSeconds = Math.Max(holdUntilSeconds, nowSeconds + holdSeconds);
        }

        internal bool Held(double nowSeconds) { return nowSeconds < holdUntilSeconds; }

        internal double Update(double nowSeconds, double deltaSeconds, bool condition, double fadeInSeconds, double fadeOutSeconds)
        {
            bool wanted = condition || nowSeconds < holdUntilSeconds;
            double step = Math.Max(0.0, deltaSeconds);
            if (wanted) { alpha = Math.Min(1.0, alpha + step / Math.Max(0.001, fadeInSeconds)); }
            else { alpha = Math.Max(0.0, alpha - step / Math.Max(0.001, fadeOutSeconds)); }
            return alpha;
        }

        internal void Reset()
        {
            alpha = 0;
            holdUntilSeconds = double.NegativeInfinity;
        }
    }
}
