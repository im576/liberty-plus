using System.Collections.Generic;

namespace LibertyFramework.Hud.Logic
{
    // The same prompt often remains on screen for seconds. Rebuild only when its text,
    // device or the hot-reloaded glyph table changes; retain the last text during fade-out.
    internal sealed class HudPromptText
    {
        private string source;
        private IList<HudGlyph> glyphs;
        private HudDevice device;
        internal string Text { get; private set; } = "";

        internal bool Update(string help, IList<HudGlyph> currentGlyphs, HudDevice currentDevice, out List<string> unknown)
        {
            unknown = null;
            if (help == null) { source = null; return false; }
            if (source == help && ReferenceEquals(glyphs, currentGlyphs) && device == currentDevice) { return false; }
            source = help;
            glyphs = currentGlyphs;
            device = currentDevice;
            Text = HudText.ExpandGlyphs(help, glyphs, device, out unknown);
            return true;
        }
    }
}
