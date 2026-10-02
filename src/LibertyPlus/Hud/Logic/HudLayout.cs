using System;

namespace LibertyFramework.Hud.Logic
{
    internal struct HudRect
    {
        internal float X, Y, Width, Height;
        internal HudRect(float x, float y, float width, float height) { X = x; Y = y; Width = width; Height = height; }
        internal float Right { get { return X + Width; } }
        internal float Bottom { get { return Y + Height; } }
        internal bool Intersects(HudRect other)
        {
            return X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
        }
    }

    // T-049: where the HUD group sits, in virtual units (720 high, 720 x aspect wide). The group is anchored to the TOP RIGHT
    // (owner direction, ART-007 r2) and its slots do not move while elements fade, so nothing jumps when one of them appears.
    // From the top: weapon icon, ammo line, health bar, armour bar, wanted stars; everything right-aligned to the margin.
    internal static class HudLayout
    {
        internal static float Right(HudLayoutSettings layout, float canvasWidth) { return canvasWidth - layout.MarginRight; }

        internal static float GroupWidth(HudLayoutSettings layout) { return Math.Max(layout.IconWidth, layout.BarWidth); }

        internal static HudRect Icon(HudLayoutSettings layout, float canvasWidth)
        {
            return new HudRect(Right(layout, canvasWidth) - layout.IconWidth, layout.MarginTop, layout.IconWidth, layout.IconHeight);
        }

        internal static HudRect Ammo(HudLayoutSettings layout, float canvasWidth)
        {
            HudRect icon = Icon(layout, canvasWidth);
            float width = GroupWidth(layout);
            return new HudRect(Right(layout, canvasWidth) - width, icon.Bottom + layout.RowGap, width, layout.AmmoHeight);
        }

        internal static HudRect HealthBar(HudLayoutSettings layout, float canvasWidth)
        {
            HudRect ammo = Ammo(layout, canvasWidth);
            return new HudRect(Right(layout, canvasWidth) - layout.BarWidth, ammo.Bottom + layout.RowGap, layout.BarWidth, layout.BarHeight);
        }

        internal static HudRect ArmourBar(HudLayoutSettings layout, float canvasWidth)
        {
            HudRect health = HealthBar(layout, canvasWidth);
            return new HudRect(health.X, health.Bottom + layout.BarGap, layout.BarWidth, layout.BarHeight);
        }

        // The row of stars, right-aligned; count is how many are laid out (the most the config allows).
        internal static HudRect Stars(HudLayoutSettings layout, float canvasWidth, int count)
        {
            HudRect armour = ArmourBar(layout, canvasWidth);
            float width = count * layout.StarSize + Math.Max(0, count - 1) * layout.StarGap;
            return new HudRect(Right(layout, canvasWidth) - width, armour.Bottom + layout.RowGap, width, layout.StarSize);
        }

        // Everything the group can occupy: menus and panels must stay out of it.
        internal static HudRect Group(HudLayoutSettings layout, float canvasWidth, int maximumStars)
        {
            HudRect icon = Icon(layout, canvasWidth);
            HudRect stars = Stars(layout, canvasWidth, maximumStars);
            float left = Math.Min(Ammo(layout, canvasWidth).X, Math.Min(icon.X, stars.X));
            return new HudRect(left, icon.Y, Right(layout, canvasWidth) - left, stars.Bottom - icon.Y);
        }
    }
}
