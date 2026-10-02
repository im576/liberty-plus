using System;
using System.Collections.Generic;
using System.Text;

namespace LibertyFramework.Hud.Logic
{
    // T-049: the strings the HUD shows.
    internal static class HudText
    {
        // "Press {interact} to use the trunk." -> "Press X to use the trunk." (pad) / "... E ..." (keyboard). A token without a
        // glyph entry is left in braces so the gap is visible on screen and in the log rather than silently blank.
        internal static string ExpandGlyphs(string text, IList<HudGlyph> glyphs, HudDevice device, out List<string> unknown)
        {
            unknown = null;
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) { return text; }
            StringBuilder result = new StringBuilder(text.Length + 8);
            int index = 0;
            while (index < text.Length)
            {
                char c = text[index];
                int close = c == '{' ? text.IndexOf('}', index + 1) : -1;
                if (close < 0) { result.Append(c); index++; continue; }
                string token = text.Substring(index + 1, close - index - 1);
                HudGlyph match = null;
                foreach (HudGlyph glyph in glyphs) { if (glyph.Token == token) { match = glyph; break; } }
                if (match == null)
                {
                    if (unknown == null) { unknown = new List<string>(); }
                    if (!unknown.Contains(token)) { unknown.Add(token); }
                    result.Append(text, index, close - index + 1);
                }
                else { result.Append(device == HudDevice.Pad ? match.Pad : match.Keyboard); }
                index = close + 1;
            }
            return result.ToString();
        }

        // Without the Liberty HUD running nothing knows the device in use, so a token shows both: "X / E".
        internal static string ExpandBoth(string text, IList<HudGlyph> glyphs)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) { return text; }
            foreach (HudGlyph glyph in glyphs) { text = text.Replace("{" + glyph.Token + "}", glyph.Pad + " / " + glyph.Keyboard); }
            return text;
        }

        // "17 / 68": rounds in the clip / rounds in reserve. A weapon without a known clip (melee) shows no ammo line.
        internal static string FormatAmmo(int clip, int total, bool totalIncludesClip)
        {
            if (clip < 0 || total < 0) { return ""; }
            int reserve = totalIncludesClip ? Math.Max(0, total - clip) : total;
            return clip + " / " + reserve;
        }

        // Fraction of a bar that is filled, 0-1, from the gameplay-scale value and the bar's maximum.
        internal static double Fill(int value, int maximum)
        {
            if (maximum <= 0) { return 0; }
            return Math.Max(0.0, Math.Min(1.0, (double)value / maximum));
        }

        // The clip counts as low at or below this fraction of the largest clip seen for the weapon; an empty clip always does.
        internal static bool IsClipLow(int clip, int clipMaximumSeen, double lowFraction)
        {
            if (clip < 0 || clipMaximumSeen <= 0) { return false; }
            return clip == 0 || clip <= clipMaximumSeen * lowFraction;
        }

        // Brightness multiplier of a pulsing bar: 1 at the peak, 1 - depth at the dimmest, cyclesPerSecond full cycles per second.
        internal static double Pulse(double seconds, double cyclesPerSecond, double depth)
        {
            if (cyclesPerSecond <= 0 || depth <= 0) { return 1.0; }
            double wave = 0.5 + 0.5 * Math.Cos(2.0 * Math.PI * cyclesPerSecond * seconds);
            return 1.0 - depth * (1.0 - wave);
        }
    }
}
