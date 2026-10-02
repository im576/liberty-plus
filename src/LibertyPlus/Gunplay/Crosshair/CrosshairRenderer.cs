using System;
using System.Drawing;
using GTA;
using LibertyFramework.Gunplay.Logic;
using LibertyFramework.Gunplay.Profiles;

namespace LibertyFramework.Gunplay.Crosshair
{
    // The reticle (T-043: styles per weapon class; before that one four-segment crosshair). Its opening is the current spread
    // cone projected to screen pixels, so it opens exactly as far as bullets can deviate: the gap between the arms (cross), the
    // corner of each bracket (bracket) or the radius of the ring (ring). Opening follows a shot immediately and eases closed.
    internal sealed class CrosshairRenderer
    {
        private double displayedGapPixels = -1;

        internal double DisplayedGapPixels { get { return displayedGapPixels; } }
        // What the cone asked for on the last drawn frame (after the style's clamp), for the truthfulness log.
        internal double LastTargetPixels { get; private set; }

        internal static double ConeToPixels(double coneDegrees, double fovDegrees, bool fovIsVertical, Size resolution)
        {
            double halfFov = Math.Max(1.0, fovDegrees) * 0.5 * Math.PI / 180.0;
            double axisPixels = fovIsVertical ? resolution.Height * 0.5 : resolution.Width * 0.5;
            return Math.Tan(coneDegrees * Math.PI / 180.0) / Math.Tan(halfFov) * axisPixels;
        }

        internal void Reset()
        {
            displayedGapPixels = -1;
        }

        internal void Draw(GTA.Graphics graphics, ResolvedReticle style, string fovAxis, double opacityScale, double coneDegrees, double fovDegrees,
            double pixelsPerTangent, Size resolution, float frameSeconds)
        {
            // Prefer the game's own projection (measured each frame from GET_VIEWPORT_POSITION_OF_COORD); the FOV formula is the fallback.
            double halfFov = Math.Max(1.0, fovDegrees) * 0.5 * Math.PI / 180.0;
            double fallbackPerTangent = (fovAxis == "vertical" ? resolution.Height * 0.5 : resolution.Width * 0.5) / Math.Tan(halfFov);
            double target = ReticleResolver.TargetPixels(coneDegrees, pixelsPerTangent, fallbackPerTangent, style);
            LastTargetPixels = target;
            if (displayedGapPixels < 0) { displayedGapPixels = target; }
            else
            {
                double blend = 1.0 - Math.Exp(-style.GapSmoothingPerSecond * Math.Max(0.0, frameSeconds));
                // Opening follows immediately so the display never under-reports spread after a shot.
                displayedGapPixels = target > displayedGapPixels ? target : displayedGapPixels + (target - displayedGapPixels) * blend;
            }
            if (style.IsNone) { return; }

            float centerX = resolution.Width * 0.5f;
            float centerY = resolution.Height * 0.5f;
            float gap = (float)displayedGapPixels;
            float length = (float)style.LineLengthPixels;
            float thickness = (float)style.LineThicknessPixels;
            float outline = (float)style.OutlinePixels;
            Color color = ToColor(style.ColorArgb, opacityScale);
            Color outlineColor = ToColor(style.OutlineArgb, opacityScale);

            graphics.Scaling = FontScaling.Pixel;
            switch (style.Style)
            {
                case "bracket":
                    if (outline > 0) { DrawBrackets(graphics, centerX, centerY, gap, length + outline * 2, thickness + outline * 2, outlineColor); }
                    DrawBrackets(graphics, centerX, centerY, gap, length, thickness, color);
                    break;
                case "ring":
                    DrawRing(graphics, centerX, centerY, gap, style.RingDots, thickness, outline, color, outlineColor);
                    break;
                case "dot":
                    break;
                default:
                    if (outline > 0) { DrawSegments(graphics, centerX, centerY, gap - outline, length + outline * 2, thickness + outline * 2, outlineColor); }
                    DrawSegments(graphics, centerX, centerY, gap, length, thickness, color);
                    break;
            }
            float dotSide = (float)style.CenterDotPixels;
            if (style.Style == "dot" && dotSide <= 0) { dotSide = thickness * 2; }
            if (dotSide > 0)
            {
                if (outline > 0) { graphics.DrawRectangle(centerX, centerY, dotSide + outline * 2, dotSide + outline * 2, outlineColor); }
                graphics.DrawRectangle(centerX, centerY, dotSide, dotSide, color);
            }
        }

        // DrawRectangle(centerX, centerY, width, height) is centre-based in ScriptHookDotNet.
        private static void DrawSegments(GTA.Graphics graphics, float x, float y, float gap, float length, float thickness, Color color)
        {
            float offset = gap + length * 0.5f;
            graphics.DrawRectangle(x, y - offset, thickness, length, color);
            graphics.DrawRectangle(x, y + offset, thickness, length, color);
            graphics.DrawRectangle(x - offset, y, length, thickness, color);
            graphics.DrawRectangle(x + offset, y, length, thickness, color);
        }

        // Crop marks on the corners of the square the bullets stay inside: the corner at (+-gap, +-gap), each arm running outward.
        private static void DrawBrackets(GTA.Graphics graphics, float x, float y, float gap, float length, float thickness, Color color)
        {
            foreach (int sx in new[] { -1, 1 })
            {
                foreach (int sy in new[] { -1, 1 })
                {
                    float cornerX = x + sx * gap;
                    float cornerY = y + sy * gap;
                    // Horizontal arm from the corner outward, vertical arm from the corner outward (corner square included once).
                    graphics.DrawRectangle(cornerX + sx * (length - thickness) * 0.5f, cornerY, length + thickness, thickness, color);
                    graphics.DrawRectangle(cornerX, cornerY + sy * (length - thickness) * 0.5f, thickness, length + thickness, color);
                }
            }
        }

        // Dots evenly on a circle of radius = the opening: the pellets' spread ring.
        private static void DrawRing(GTA.Graphics graphics, float x, float y, float radius, int dots, float thickness, float outline, Color color, Color outlineColor)
        {
            float side = thickness + 1.0f;
            for (int index = 0; index < dots; index++)
            {
                double angle = index * 2.0 * Math.PI / dots;
                float dotX = x + (float)(Math.Cos(angle) * radius);
                float dotY = y + (float)(Math.Sin(angle) * radius);
                if (outline > 0) { graphics.DrawRectangle(dotX, dotY, side + outline * 2, side + outline * 2, outlineColor); }
                graphics.DrawRectangle(dotX, dotY, side, side, color);
            }
        }

        private static Color ToColor(int[] argb, double opacityScale)
        {
            int alpha = (int)Math.Max(0, Math.Min(255, Math.Round(argb[0] * opacityScale)));
            return Color.FromArgb(alpha, argb[1], argb[2], argb[3]);
        }
    }
}
