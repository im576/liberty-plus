using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using Liberty.Sdk;
using LibertyFramework.Core.Config;
using LibertyFramework.Core.Memory;
using LibertyFramework.Hud.Logic;

namespace LibertyFramework.Verify
{
    // T-049: the Liberty HUD as data and logic. config/hud.json against the code's defaults, fades and holds, the top-right layout and
    // its clearance from the menus, which vanilla components Liberty may hide, glyph/ammo text, the input-device rule, and the
    // generic hud.dat table resolver against a synthetic executable image (the real image is checked on the PC by AddressChecks).
    internal static class Stage1HudChecks
    {
        internal static void Run(string repoRoot, Checker check)
        {
            Config(repoRoot, check);
            Presence(check);
            Layout(repoRoot, check);
            Plan(check);
            Text(repoRoot, check);
            InputDevice(check);
            PromptCache(check);
            AcceptanceBudgets(repoRoot, check);
            AmmoSampling(check);
            NativeDisplayProbe(check);
            BaselineReadback(check);
            TableResolver(check);
        }

        private static byte[] Serialize<T>(T value)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                return stream.ToArray();
            }
        }

        private static HudConfig Parse(string json)
        {
            HudConfig config = JsonStore.Parse<HudConfig>(Encoding.UTF8.GetBytes(json));
            config.Validate();
            return config;
        }

        private static bool Refused(string json)
        {
            try { Parse(json); return false; }
            catch (InvalidDataException) { return true; }
        }

        private static void Config(string repoRoot, Checker check)
        {
            HudConfig file = JsonStore.Load<HudConfig>(Path.Combine(repoRoot, "config/hud.json"));
            file.Validate();
            check.True("hud.json validates", file.Enabled && file.HidesVanilla, "");
            // The code's defaults are what an older file falls back to: they must not drift from the shipped file.
            check.True("hud.json equals the code defaults (one set of numbers)",
                Encoding.UTF8.GetString(Serialize(file)) == Encoding.UTF8.GetString(Serialize(HudConfig.Defaults())), "");
            HudConfig empty = Parse("{\"schemaVersion\":1}");
            check.True("a file with only schemaVersion takes every default",
                Encoding.UTF8.GetString(Serialize(empty)) == Encoding.UTF8.GetString(Serialize(HudConfig.Defaults())), "");
            HudConfig partial = Parse("{\"schemaVersion\":1,\"weapon\":{\"holdSeconds\":9},\"palette\":{\"health\":[255,1,2,3]}}");
            check.True("a member absent from a partial section keeps its default, the present one wins",
                partial.Weapon.HoldSeconds == 9 && partial.Weapon.FadeOutSeconds == HudConfig.Defaults().Weapon.FadeOutSeconds &&
                partial.Palette.Health[3] == 3 && partial.Palette.Armour.Length == 4 && partial.Layout.MarginRight == HudConfig.Defaults().Layout.MarginRight, "");
            check.True("health and armour defaults differ as designed (armour hides at zero and never pulses)",
                !file.Health.HideWhenZero && file.Armour.HideWhenZero && file.Armour.PulseDepth == 0 && file.Health.LowFraction > 0 && file.Armour.LowFraction == 0, "");
            check.True("the health and armour rings are not claimed as separable vanilla components (they belong to the radar)",
                file.Health.VanillaComponents.Count == 0 && file.Armour.VanillaComponents.Count == 0 && !file.Health.DrawWithoutHidingVanilla, "");
            check.True("a bad schema version is refused", Refused("{\"schemaVersion\":2}"), "");
            check.True("an unknown hideVanilla mode is refused", Refused("{\"schemaVersion\":1,\"hideVanilla\":\"radar\"}"), "");
            check.True("text under 14 px is refused (ammo line height)", Refused("{\"schemaVersion\":1,\"layout\":{\"ammoHeight\":12}}"), "");
            check.True("a palette colour needs four 0-255 values", Refused("{\"schemaVersion\":1,\"palette\":{\"text\":[255,0,0]}}") &&
                Refused("{\"schemaVersion\":1,\"palette\":{\"text\":[256,0,0,0]}}"), "");
            check.True("a zero fade time is refused", Refused("{\"schemaVersion\":1,\"weapon\":{\"fadeOutSeconds\":0}}"), "");
            check.True("a vanilla component name must be a hud.dat identifier", Refused("{\"schemaVersion\":1,\"weapon\":{\"vanillaComponents\":[\"hud ammo\"]}}"), "");
            check.True("a glyph token must be unique and lower case",
                Refused("{\"schemaVersion\":1,\"prompt\":{\"glyphs\":[{\"token\":\"Go\",\"pad\":\"A\",\"keyboard\":\"E\"}]}}") &&
                Refused("{\"schemaVersion\":1,\"prompt\":{\"glyphs\":[{\"token\":\"go\",\"pad\":\"A\",\"keyboard\":\"E\"},{\"token\":\"go\",\"pad\":\"B\",\"keyboard\":\"F\"}]}}"), "");
            check.True("hideVanilla none and enabled false parse", Parse("{\"schemaVersion\":1,\"hideVanilla\":\"none\"}").HideVanilla == "none" && !Parse("{\"schemaVersion\":1,\"enabled\":false}").Enabled, "");

            // Every {token} a prompt in the code uses has a glyph in the shipped config.
            string arsenal = File.ReadAllText(Path.Combine(repoRoot, "src/LibertyFramework/Arsenal/ArsenalCore.cs"));
            List<string> missing = new List<string>();
            int found = 0;
            foreach (Match match in Regex.Matches(arsenal, "\"Press \\{(?<t>[a-z]+)\\}"))
            {
                found++;
                string token = match.Groups["t"].Value;
                if (file.Prompt.Glyphs.Find(g => g.Token == token) == null) { missing.Add(token); }
            }
            check.True("the Arsenal's prompts use glyph tokens that hud.json defines", found >= 2 && missing.Count == 0, "prompts=" + found + " missing=" + string.Join(",", missing.ToArray()));
        }

        private static void Presence(Checker check)
        {
            HudPresence p = new HudPresence();
            check.True("a new element is invisible", !p.Visible && p.Alpha == 0, "");
            // 60 fps frames: fade in over 0.5 s, hold 2 s after a trigger, fade out over 1 s.
            double now = 0;
            p.Trigger(now, 2.0);
            double reachedFull = -1;
            for (int frame = 0; frame < 60; frame++)
            {
                now += 1.0 / 60;
                p.Update(now, 1.0 / 60, false, 0.5, 1.0);
                if (reachedFull < 0 && p.Alpha >= 0.999) { reachedFull = now; }
            }
            check.True("a trigger fades the element in over its fade-in time", reachedFull > 0.45 && reachedFull < 0.55, "full at " + reachedFull.ToString("0.000") + " s");
            check.True("it is still fully shown inside the hold time", p.Alpha > 0.999 && p.Held(now), "t=" + now.ToString("0.00"));
            double hiddenAt = -1;
            for (int frame = 0; frame < 600; frame++)
            {
                now += 1.0 / 60;
                p.Update(now, 1.0 / 60, false, 0.5, 1.0);
                if (hiddenAt < 0 && !p.Visible) { hiddenAt = now; }
            }
            check.True("after the hold it fades out over the fade-out time and disappears", hiddenAt > 2.9 && hiddenAt < 3.2 && p.Alpha == 0, "hidden at " + hiddenAt.ToString("0.00") + " s");
            p.Update(now, 0.1, true, 0.5, 1.0);
            check.True("a standing condition keeps it up with no trigger", p.Alpha > 0, "");
            for (int frame = 0; frame < 120; frame++) { now += 1.0 / 60; p.Update(now, 1.0 / 60, true, 0.5, 1.0); }
            check.True("while the condition holds it stays fully shown", p.Alpha > 0.999, "");
            p.Trigger(now, 1.0);
            p.Trigger(now, 0.2);
            check.True("a shorter trigger never shortens a longer hold", p.Held(now + 0.9), "");
            p.Reset();
            check.True("reset clears it at once", !p.Visible && !p.Held(now), "");
            p.Update(now, 10.0, true, 0.5, 1.0);
            check.True("a long frame never overshoots full opacity", p.Alpha == 1.0, "");
            p.Update(now, -1.0, false, 0.5, 1.0);
            check.True("a negative step changes nothing", p.Alpha == 1.0, "");
        }

        private static void Layout(string repoRoot, Checker check)
        {
            HudConfig config = JsonStore.Load<HudConfig>(Path.Combine(repoRoot, "config/hud.json"));
            HudLayoutSettings layout = config.Layout;
            foreach (float width in new[] { 960f, 1280f, 1680f, 2400f })
            {
                HudRect group = HudLayout.Group(layout, width, config.Wanted.MaximumStars);
                bool inside = group.X >= 0 && group.Right <= width + 0.01f && group.Y >= 0 && group.Bottom <= 720;
                bool topRight = group.X > width / 2 && group.Bottom < 720f / 2 && Math.Abs(group.Right - (width - layout.MarginRight)) < 0.01f && Math.Abs(group.Y - layout.MarginTop) < 0.01f;
                check.True("the HUD group is inside the screen and anchored top right at width " + width, inside && topRight,
                    "x=" + group.X + " y=" + group.Y + " right=" + group.Right + " bottom=" + group.Bottom);
            }
            HudRect icon = HudLayout.Icon(layout, 1280f), ammo = HudLayout.Ammo(layout, 1280f), health = HudLayout.HealthBar(layout, 1280f);
            HudRect armour = HudLayout.ArmourBar(layout, 1280f), stars = HudLayout.Stars(layout, 1280f, config.Wanted.MaximumStars);
            check.True("rows stack from the top: icon, ammo, health, armour, stars, none overlapping",
                icon.Bottom <= ammo.Y && ammo.Bottom <= health.Y && health.Bottom <= armour.Y && armour.Bottom <= stars.Y, "");
            check.True("every row is right-aligned to the margin",
                Math.Abs(icon.Right - ammo.Right) < 0.01f && Math.Abs(ammo.Right - health.Right) < 0.01f && Math.Abs(health.Right - armour.Right) < 0.01f && Math.Abs(armour.Right - stars.Right) < 0.01f, "");
            check.True("the ammo line is at least 14 px tall (smallest text at 720p)", ammo.Height >= 14, "height=" + ammo.Height);

            // Clearance from the menus: the radial wheel (centred, 0.66 of the height), the default list menu (x 60, 430 wide), the prompt box.
            HudRect group16 = HudLayout.Group(layout, 1280f, config.Wanted.MaximumStars);
            RadialMenu radial = new RadialMenu();
            float diameter = 720f * radial.Size;
            HudRect wheel = new HudRect(1280f / 2 - diameter / 2, 360f - diameter / 2, diameter, diameter);
            ListMenu list = new ListMenu();
            HudRect listMenu = new HudRect(list.X, list.Y, 430f, 44f + 2f + list.VisibleRows * 30f + 8f + 60f);
            HudRect prompt = new HudRect(layout.PromptX, layout.PromptY, layout.PromptWidth, layout.PromptHeight);
            check.True("the weapon wheel does not cover the HUD group", !wheel.Intersects(group16), "wheel right=" + wheel.Right + " group left=" + group16.X);
            check.True("the default list menu does not cover the HUD group", !listMenu.Intersects(group16), "");
            check.True("the prompt box (top left) does not cover the HUD group", !prompt.Intersects(group16), "");
            HudRect radarCorner = new HudRect(0, 720f * 0.55f, 1280f * 0.3f, 720f * 0.45f);
            check.True("nothing of the group is in the lower-left radar corner or any lower corner", !radarCorner.Intersects(group16) && group16.Bottom < 720f * 0.5f, "");
            // The trunk panel of the ART-007 r2 board sits right-centre below the group: its top edge must clear the group's bottom.
            HudRect trunkPanel = new HudRect(1280f - 440f, 720f * 0.30f, 412f, 720f * 0.41f);
            check.True("a right-centre panel as drawn on the ART-007 r2 board clears the group", !trunkPanel.Intersects(group16), "group bottom=" + group16.Bottom + " panel top=" + trunkPanel.Y);
        }

        private static void Plan(Checker check)
        {
            Func<string, bool> all = name => true, none = name => false, onlyAmmo = name => name == "HUD_AMMO";
            List<string> weapon = new List<string> { "HUD_WEAPON_ICON", "HUD_AMMO" };
            HudDecision a = HudPlan.Decide(true, "components", weapon, false, all, all);
            check.True("every component resolved and visibly verified: Liberty draws and hides both", a.Mode == HudElementMode.Liberty && a.ToHide.Count == 2, a.Reason);
            HudDecision b = HudPlan.Decide(true, "components", weapon, false, onlyAmmo, all);
            check.True("one component missing: the vanilla element stays and nothing is hidden", b.Mode == HudElementMode.VanillaKept && b.ToHide.Count == 0 && b.Reason.Contains("HUD_WEAPON_ICON"), b.Reason);
            HudDecision c = HudPlan.Decide(true, "components", weapon, true, onlyAmmo, all);
            check.True("drawWithoutHidingVanilla draws anyway and hides only what it can", c.Mode == HudElementMode.Liberty && c.ToHide.Count == 1 && c.ToHide[0] == "HUD_AMMO", "");
            HudDecision d = HudPlan.Decide(true, "components", new List<string>(), false, all, all);
            check.True("no configured vanilla component (the health ring): kept vanilla, no duplicate", d.Mode == HudElementMode.VanillaKept && d.ToHide.Count == 0, d.Reason);
            HudDecision e = HudPlan.Decide(true, "none", weapon, false, all, all);
            check.True("hideVanilla none leaves the vanilla HUD alone", e.Mode == HudElementMode.VanillaKept && e.ToHide.Count == 0, "");
            HudDecision f = HudPlan.Decide(false, "components", weapon, false, all, all);
            check.True("a disabled element does not touch the vanilla HUD", f.Mode == HudElementMode.Off && f.ToHide.Count == 0, "");
            HudDecision g = HudPlan.Decide(true, "components", null, false, all, all);
            check.True("a null component list is handled", g.Mode == HudElementMode.VanillaKept, "");
            HudDecision h = HudPlan.Decide(true, "components", weapon, false, none, all);
            check.True("an empty table keeps everything vanilla", h.Mode == HudElementMode.VanillaKept && h.ToHide.Count == 0, "");
            HudDecision unverified = HudPlan.Decide(true, "components", weapon, false, all, none);
            check.True("resolved globals alone never permit replacement", unverified.Mode == HudElementMode.VanillaKept && unverified.ToHide.Count == 0, unverified.Reason);
            HudDecision partial = HudPlan.Decide(true, "components", weapon, false, all, onlyAmmo);
            check.True("one visually unverified component prevents the entire replacement", partial.Mode == HudElementMode.VanillaKept && partial.ToHide.Count == 0, partial.Reason);
            HudDecision diagnostic = HudPlan.Decide(true, "components", weapon, true, all, none);
            check.True("explicit diagnostic override draws without writing unverified globals", diagnostic.Mode == HudElementMode.Liberty && diagnostic.ToHide.Count == 0, diagnostic.Reason);
        }

        private static void BaselineReadback(Checker check)
        {
            check.True("inventory clip/total do not prove held weapon", !HudBaselineReadback.HasHeldAmmo(7, 0, 0, true, 17, 150), "");
            check.True("cached selected snapshot cannot override fresh unarmed readback", !HudBaselineReadback.HasHeldAmmo(7, 0, 7, true, 17, 150), "");
            check.True("fresh selection waits for snapshot agreement", !HudBaselineReadback.HasHeldAmmo(7, 7, 0, true, 17, 150), "");
            check.True("missing ownership prevents held baseline", !HudBaselineReadback.HasHeldAmmo(7, 7, 7, false, 17, 150), "");
            check.True("unknown or empty clip prevents fixture baseline", !HudBaselineReadback.HasHeldAmmo(7, 7, 7, true, -1, 150) && !HudBaselineReadback.HasHeldAmmo(7, 7, 7, true, 0, 150), "");
            check.True("empty total or unarmed expected id prevents fixture baseline", !HudBaselineReadback.HasHeldAmmo(7, 7, 7, true, 17, 0) && !HudBaselineReadback.HasHeldAmmo(0, 0, 0, true, 17, 150), "");
            check.True("matching held readings and ammo satisfy only the numeric baseline", HudBaselineReadback.HasHeldAmmo(7, 7, 7, true, 17, 150), "");
        }

        private static void NativeDisplayProbe(Checker check)
        {
            HudNativeDisplayProbe probe = new HudNativeDisplayProbe();
            check.True("native display diagnostic starts inactive and writes nothing", !probe.Requested && !probe.Applied && probe.Update(0, true, true) == 0, "");
            probe.Arm(10, 100);
            check.True("explicit lease enforces every frame before expiry", probe.Update(10, true, true) == 1 && probe.Update(109, true, true) == 1, "");
            check.True("expiry releases exactly once", probe.Update(110, true, true) == -1 && probe.Update(111, true, true) == 0, "");
            probe.Arm(200, 100);
            probe.Update(200, true, true);
            check.True("config off cancels and releases", probe.Update(201, false, true) == -1 && !probe.Requested && !probe.Applied, "");
            check.True("restoring config does not rearm native hiding", probe.Update(202, true, true) == 0, "");
            probe.Arm(300, 100);
            probe.Update(300, true, true);
            check.True("unsafe gameplay releases and does not resume automatically", probe.Update(301, true, false) == -1 && probe.Update(302, true, true) == 0, "");
            probe.Arm(400, 100);
            probe.Update(400, true, true);
            probe.Cancel();
            check.True("stop or failure cancel releases applied lease once", probe.Update(401, true, true) == -1 && probe.Update(402, true, true) == 0, "");
            probe.Arm(500, 100);
            check.True("unsafe start cancels without taking ownership", probe.Update(500, true, false) == 0 && !probe.Requested, "");
        }

        private static void AmmoSampling(Checker check)
        {
            HudAmmoSample sample = new HudAmmoSample();
            check.True("first ammo observation requires a sample", sample.NeedsRefresh(7, 17, 0, 100), "");
            sample.Capture(7, 17, 17, 150, 0);
            check.True("unchanged clip respects the configured poll interval", !sample.NeedsRefresh(7, 17, 99, 100) && sample.NeedsRefresh(7, 17, 100, 100), "");
            check.True("shot invalidates cached total immediately", sample.NeedsRefresh(7, 16, 20, 100), "");
            check.True("a fresh observed clip cannot mutate the stored display pair", HudText.FormatAmmo(sample.Clip, sample.Total, true) == "17 / 133", "");
            sample.Capture(7, 16, 16, 149, 20);
            check.True("shot sample preserves reserve rather than inventing one round", HudText.FormatAmmo(sample.Clip, sample.Total, true) == "16 / 133", "");
            sample.Capture(7, 0, 0, 133, 40);
            check.True("reload clip change invalidates the pair before the next timer poll", sample.NeedsRefresh(7, 17, 50, 100), "");
            sample.Capture(7, 17, 17, 133, 50);
            check.True("reload transfers reserve to clip in one display sample", HudText.FormatAmmo(sample.Clip, sample.Total, true) == "17 / 116", "");
            check.True("weapon switch refreshes even when its clip count is identical", sample.NeedsRefresh(10, 17, 51, 100), "");
            sample.Reset();
            check.True("reset discards old weapon clip and total", sample.Weapon == -1 && HudText.FormatAmmo(sample.Clip, sample.Total, true) == "" && sample.NeedsRefresh(7, 17, 52, 100), "");
        }

        private static void Text(string repoRoot, Checker check)
        {
            HudConfig config = JsonStore.Load<HudConfig>(Path.Combine(repoRoot, "config/hud.json"));
            List<string> unknown;
            string pad = HudText.ExpandGlyphs("Press {interact} to use the trunk.", config.Prompt.Glyphs, HudDevice.Pad, out unknown);
            string keyboard = HudText.ExpandGlyphs("Press {interact} to use the trunk.", config.Prompt.Glyphs, HudDevice.Keyboard, out unknown);
            check.True("a prompt shows the pad button on a pad and the key on the keyboard", pad == "Press X to use the trunk." && keyboard == "Press E to use the trunk." && unknown == null, pad + " | " + keyboard);
            string twice = HudText.ExpandGlyphs("{accept} select, {cancel} back", config.Prompt.Glyphs, HudDevice.Keyboard, out unknown);
            check.True("several tokens in one text", twice == "Enter select, Backspace back", twice);
            string odd = HudText.ExpandGlyphs("Hold {nothing} or {", config.Prompt.Glyphs, HudDevice.Pad, out unknown);
            check.True("an unknown token stays visible and is reported; a stray brace is left alone", odd == "Hold {nothing} or {" && unknown != null && unknown.Count == 1 && unknown[0] == "nothing", odd);
            check.True("text without braces is returned as is", HudText.ExpandGlyphs("Plain", config.Prompt.Glyphs, HudDevice.Pad, out unknown) == "Plain" && HudText.ExpandGlyphs(null, config.Prompt.Glyphs, HudDevice.Pad, out unknown) == null, "");
            check.True("without the HUD a token shows both names", HudText.ExpandBoth("Press {interact}", config.Prompt.Glyphs) == "Press X / E", "");
            check.True("ammo shows clip / reserve with the total including the clip", HudText.FormatAmmo(17, 85, true) == "17 / 68", HudText.FormatAmmo(17, 85, true));
            check.True("ammo can treat the total as reserve only", HudText.FormatAmmo(17, 68, false) == "17 / 68", "");
            check.True("reserve never goes negative; an unknown clip shows nothing", HudText.FormatAmmo(30, 10, true) == "30 / 0" && HudText.FormatAmmo(-1, 10, true) == "" && HudText.FormatAmmo(5, -1, true) == "", "");
            check.True("bar fill is the value over the maximum, clamped", HudText.Fill(50, 100) == 0.5 && HudText.Fill(-5, 100) == 0 && HudText.Fill(150, 100) == 1 && HudText.Fill(10, 0) == 0, "");
            check.True("the clip is low at or under the fraction of the largest clip seen; empty is always low",
                HudText.IsClipLow(4, 17, 0.25) && !HudText.IsClipLow(5, 17, 0.25) && HudText.IsClipLow(0, 1, 0.25) && !HudText.IsClipLow(-1, 17, 0.25) && !HudText.IsClipLow(3, 0, 0.25), "");
            double peak = HudText.Pulse(0, 1.2, 0.45), trough = HudText.Pulse(0.5 / 1.2, 1.2, 0.45);
            check.True("the low-health pulse swings between full and 1 - depth", Math.Abs(peak - 1.0) < 1e-9 && Math.Abs(trough - 0.55) < 1e-9 && HudText.Pulse(3, 0, 0.5) == 1.0 && HudText.Pulse(3, 1, 0) == 1.0, "");
        }

        private static void InputDevice(Checker check)
        {
            HudInputDevice device = new HudInputDevice();
            check.True("the keyboard is the starting device", device.Current == HudDevice.Keyboard, "");
            check.True("a pad press switches an automatic device to the pad", device.Update("auto", true, false, 1000, 400) == HudDevice.Pad, "");
            check.True("a key press right after is held back by the switch time", device.Update("auto", false, true, 1200, 400) == HudDevice.Pad, "");
            check.True("a key press after the switch time goes back to the keyboard", device.Update("auto", false, true, 1500, 400) == HudDevice.Keyboard, "");
            check.True("both devices active in one frame changes nothing", device.Update("auto", true, true, 5000, 400) == HudDevice.Keyboard, "");
            check.True("idle input changes nothing", device.Update("auto", false, false, 9000, 400) == HudDevice.Keyboard, "");
            check.True("a forced device wins over activity", device.Update("pad", false, true, 9500, 400) == HudDevice.Pad && device.Update("keyboard", true, false, 9600, 400) == HudDevice.Keyboard, "");
        }

        // --- the generic hud.dat table resolver against a synthetic image --------------------------------------------------------

        private static void PromptCache(Checker check)
        {
            HudPromptText prompt = new HudPromptText();
            List<HudGlyph> glyphs = HudConfig.Defaults().Prompt.Glyphs;
            List<string> unknown;
            check.True("prompt cache expands the first prompt", prompt.Update("Press {interact}", glyphs, HudDevice.Pad, out unknown) && prompt.Text == "Press X", "");
            check.True("steady prompt needs no expansion or unknown-token list", !prompt.Update("Press {interact}", glyphs, HudDevice.Pad, out unknown) && unknown == null, "");
            check.True("device switch rebuilds a held prompt", prompt.Update("Press {interact}", glyphs, HudDevice.Keyboard, out unknown) && prompt.Text == "Press E", "");
            List<HudGlyph> replacement = HudConfig.Defaults().Prompt.Glyphs;
            replacement.Find(g => g.Token == "interact").Keyboard = "F";
            check.True("hot-reloaded glyphs rebuild even unchanged prompt text", prompt.Update("Press {interact}", replacement, HudDevice.Keyboard, out unknown) && prompt.Text == "Press F", "");
            check.True("help expiry retains text for fade-out", !prompt.Update(null, replacement, HudDevice.Keyboard, out unknown) && prompt.Text == "Press F", "");
            check.True("same help reopening is evaluated again", prompt.Update("Press {interact}", replacement, HudDevice.Keyboard, out unknown), "");
            check.True("unknown glyph is reported on prompt change", prompt.Update("Press {missing}", glyphs, HudDevice.Keyboard, out unknown) && unknown != null && unknown.Count == 1, "");
            check.True("held unknown glyph does not flood the log every frame", !prompt.Update("Press {missing}", glyphs, HudDevice.Keyboard, out unknown) && unknown == null, "");
        }

        private static void AcceptanceBudgets(string repoRoot, Checker check)
        {
            string scenario = File.ReadAllText(Path.Combine(repoRoot, "tools/autopilot/scenarios/stage1-hud.txt"));
            foreach (string section in new[] { "hud", "ui" })
            {
                string pattern = null;
                foreach (string raw in scenario.Split('\n'))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("expect \"costs_ms") && line.Contains("draw\\." + section + "="))
                    {
                        int end = line.LastIndexOf('"');
                        pattern = line.Substring(8, end - 8);
                    }
                }
                check.True("HUD scenario has an enforced draw." + section + " budget", pattern != null, "");
                if (pattern == null) { continue; }
                string prefix = "costs_ms(avg/max/count@thread) draw." + section + "=";
                string inside = section == "hud" ? "0.349" : "0.500";
                string outside = section == "hud" ? "0.350" : "0.501";
                check.True("draw." + section + " accepts in-budget samples", Regex.IsMatch(prefix + inside + "/1.0/240@1 total=100", pattern), "");
                check.True("draw." + section + " rejects over-budget and multi-ms samples", !Regex.IsMatch(prefix + outside + "/1.0/240@1 total=100", pattern) && !Regex.IsMatch(prefix + "840.000/900.0/8@1 total=6720", pattern), "");
                check.True("draw." + section + " rejects missing cost evidence", !Regex.IsMatch("costs_ms(avg/max/count@thread)", pattern), "");
            }
        }

        private const uint ImageBase = 0x400000, TextVirtual = 0x1000, RdataVirtual = 0x2000;

        private static void TableResolver(Checker check)
        {
            string path = Path.Combine(Path.GetTempPath(), "lf-hud-synthetic-" + Guid.NewGuid().ToString("N") + ".exe");
            try
            {
                byte[] image = BuildImage();
                File.WriteAllBytes(path, image);
                ImageFileMemory memory = new ImageFileMemory(path);
                GameAddresses addresses = GameAddresses.Resolve(new CodeScanner(memory));
                foreach (string line in addresses.Report) { if (line.StartsWith("hud")) { Console.WriteLine("    resolver: " + line); } }
                check.True("synthetic image: the proven reticle resolver still reads its four components", addresses.HudResolved && addresses.ReticleComponents.Count == 4, addresses.ReticleComponents.Count + " components");
                check.Equal("synthetic image: register function found from the reticle registration", 0x401800u, addresses.HudRegisterFunction);
                check.True("synthetic image: the table lists the reticle components and the extra ones", addresses.HudTableResolved && addresses.HudComponents.Count == 7, "count=" + addresses.HudComponents.Count);
                GameAddresses.HudComponentGlobals ammo = Find(addresses, "HUD_AMMO");
                check.True("synthetic image: HUD_AMMO globals are read from its registration",
                    ammo != null && ammo.PositionGlobal == 0x118F100u && ammo.SizeGlobal == 0x118F108u && ammo.AlphaGlobal == 0x118F110u && ammo.ColourGlobal == 0x118F0F0u && ammo.IndexGlobal == 0x118F0E0u && ammo.LayoutConsistent, "");
                GameAddresses.HudComponentGlobals wanted = Find(addresses, "HUD_WANTED_BACK");
                check.True("synthetic image: a component whose globals are not laid out as proven is listed but flagged, so it is never written to", wanted != null && !wanted.LayoutConsistent, "");
                check.True("synthetic image: the reticle components keep the proven layout flag", Find(addresses, "HUD_WEAPON_CROSSHAIR") != null && Find(addresses, "HUD_WEAPON_CROSSHAIR").LayoutConsistent, "");
                check.True("synthetic image: calls of another shape, with a bad name and a duplicate name are kept with their bytes, not guessed at", addresses.HudUnparsed.Count == 3 &&
                    addresses.HudUnparsed.TrueForAll(line => line.StartsWith("call=0x")) && addresses.HudUnparsed.Exists(line => line.Contains("bytes=") && line.Contains("68 ")) && addresses.HudUnparsed.Exists(line => line.Contains("duplicate name HUD_AMMO")),
                    string.Join(" | ", addresses.HudUnparsed.ToArray()));
                check.True("synthetic image: the names include the extras", Find(addresses, "HUD_WEAPON_ICON") != null && Find(addresses, "HUD_NOT_THERE") == null && Find(addresses, "bad_name") == null, "");

                // An image without any registration leaves the table empty and does not throw.
                byte[] blank = BuildImage();
                File.WriteAllBytes(path, blank);
                ImageFileMemory broken = new ImageFileMemory(path);
                broken.Patch(ImageBase + TextVirtual + 0x100 + 31, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90 }); // the first reticle call becomes NOPs
                GameAddresses degraded = GameAddresses.Resolve(new CodeScanner(broken));
                check.True("a damaged registration disables the table without an exception", !degraded.HudResolved && !degraded.HudTableResolved && degraded.Report.Exists(line => line.StartsWith("hud_components FAILED")), string.Join(" | ", degraded.Report.ToArray()));
            }
            finally { try { File.Delete(path); } catch (IOException) { } }
        }

        private static GameAddresses.HudComponentGlobals Find(GameAddresses addresses, string name)
        {
            foreach (GameAddresses.HudComponentGlobals component in addresses.HudComponents) { if (component.Name == name) { return component; } }
            return null;
        }

        // One registration as the game's code has it (see GameAddresses.ResolveHud): 26 bytes of arguments, the name push at +26, the call at
        // +31, then the store of the returned index and the load from the component array.
        private static void Registration(List<byte> code, uint codeAddress, uint register, uint alpha, uint colour, uint size, uint position, uint nameAddress, uint index, uint array)
        {
            Append(code, 0xFF, 0x35); AppendU32(code, alpha);
            Append(code, 0xFF, 0x35); AppendU32(code, colour);
            Append(code, 0x6A, 0x00);
            Append(code, 0x68); AppendU32(code, size);
            Append(code, 0x68); AppendU32(code, position);
            Append(code, 0x6A, 0x01);
            Append(code, 0x68); AppendU32(code, nameAddress);
            uint call = codeAddress + (uint)code.Count;
            Append(code, 0xE8); AppendU32(code, register - (call + 5));
            Append(code, 0xA3); AppendU32(code, index);
            Append(code, 0x8B, 0x0C, 0x85); AppendU32(code, array);
            Append(code, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90);
        }

        private static byte[] BuildImage()
        {
            // .rdata: the component names (and a few strings that must be refused).
            List<byte> rdata = new List<byte>();
            Dictionary<string, uint> names = new Dictionary<string, uint>();
            foreach (string name in new[] { "HUD_WEAPON_CROSSHAIR", "HUD_WEAPON_HEALTH_TARGET", "HUD_WEAPON_ARMOUR_TARGET", "HUD_WEAPON_DOT", "HUD_AMMO", "HUD_WEAPON_ICON",
                "HUD_WANTED_BACK", "bad_name", "HUD_SHAPE_DIFFERS" })
            {
                names[name] = ImageBase + RdataVirtual + (uint)rdata.Count;
                rdata.AddRange(Encoding.ASCII.GetBytes(name));
                rdata.Add(0);
            }

            uint codeAddress = ImageBase + TextVirtual + 0x100;
            uint register = ImageBase + TextVirtual + 0x800;
            uint array = 0x118E7F8;
            List<byte> code = new List<byte>();
            // Reticle components: size = position + 8, alpha = size + 8, as proven for the real image.
            string[] reticle = { "HUD_WEAPON_CROSSHAIR", "HUD_WEAPON_HEALTH_TARGET", "HUD_WEAPON_ARMOUR_TARGET", "HUD_WEAPON_DOT" };
            for (int index = 0; index < reticle.Length; index++)
            {
                uint position = 0x118EE30u + (uint)(index * 0x1C);
                Registration(code, codeAddress, register, position + 16, position - 6, position + 8, position, names[reticle[index]], position - 4, array);
            }
            // Extra components: HUD_AMMO and HUD_WEAPON_ICON with the proven layout, HUD_WANTED_BACK with scattered globals.
            Registration(code, codeAddress, register, 0x118F110, 0x118F0F0, 0x118F108, 0x118F100, names["HUD_AMMO"], 0x118F0E0, array);
            Registration(code, codeAddress, register, 0x118F150, 0x118F130, 0x118F148, 0x118F140, names["HUD_WEAPON_ICON"], 0x118F120, array);
            Registration(code, codeAddress, register, 0x118F290, 0x118F230, 0x118F250, 0x118F240, names["HUD_WANTED_BACK"], 0x118F220, array);
            // Refused: a name that is not an upper-case identifier, a duplicate name, and another argument shape (alpha pushed as an immediate).
            Registration(code, codeAddress, register, 0x118F310, 0x118F2F0, 0x118F308, 0x118F300, names["bad_name"], 0x118F2E0, array);
            Registration(code, codeAddress, register, 0x118F350, 0x118F330, 0x118F348, 0x118F340, names["HUD_AMMO"], 0x118F320, array);
            int differs = code.Count;
            Registration(code, codeAddress, register, 0x118F390, 0x118F370, 0x118F388, 0x118F380, names["HUD_SHAPE_DIFFERS"], 0x118F360, array);
            code[differs] = 0x68; code[differs + 1] = 0x00; code[differs + 2] = 0x00; code[differs + 3] = 0x00; code[differs + 4] = 0x00; code[differs + 5] = 0x90; // push imm32 instead of push [alpha]

            byte[] file = new byte[0x2400];
            // DOS header + PE headers (one 0xE0-byte optional header, two sections).
            file[0] = (byte)'M'; file[1] = (byte)'Z';
            BitConverter.GetBytes(0x80).CopyTo(file, 0x3C);
            int pe = 0x80;
            file[pe] = (byte)'P'; file[pe + 1] = (byte)'E';
            BitConverter.GetBytes((ushort)0x14C).CopyTo(file, pe + 4);
            BitConverter.GetBytes((ushort)2).CopyTo(file, pe + 6);
            BitConverter.GetBytes((ushort)0xE0).CopyTo(file, pe + 20);
            int optional = pe + 24;
            BitConverter.GetBytes(ImageBase).CopyTo(file, optional + 28);
            BitConverter.GetBytes(0x3000).CopyTo(file, optional + 56);
            BitConverter.GetBytes(0x400).CopyTo(file, optional + 60);
            int table = optional + 0xE0;
            WriteSection(file, table, ".text", 0x1000, TextVirtual, 0x400, 0x1000, 0x60000020);
            WriteSection(file, table + 40, ".rdata", 0x1000, RdataVirtual, 0x1400, 0x1000, 0x40000040);
            Array.Copy(code.ToArray(), 0, file, 0x400 + 0x100, code.Count);
            file[0x400 + 0x800] = 0xC3; // the register function: ret
            Array.Copy(rdata.ToArray(), 0, file, 0x1400, rdata.Count);
            return file;
        }

        private static void WriteSection(byte[] file, int at, string name, int virtualSize, uint virtualAddress, int rawPointer, int rawSize, uint characteristics)
        {
            Encoding.ASCII.GetBytes(name).CopyTo(file, at);
            BitConverter.GetBytes(virtualSize).CopyTo(file, at + 8);
            BitConverter.GetBytes(virtualAddress).CopyTo(file, at + 12);
            BitConverter.GetBytes(rawSize).CopyTo(file, at + 16);
            BitConverter.GetBytes(rawPointer).CopyTo(file, at + 20);
            BitConverter.GetBytes(characteristics).CopyTo(file, at + 36);
        }

        private static void Append(List<byte> list, params byte[] bytes) { list.AddRange(bytes); }

        private static void AppendU32(List<byte> list, uint value) { list.AddRange(BitConverter.GetBytes(value)); }
    }
}
