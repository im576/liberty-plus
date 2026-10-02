using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

#pragma warning disable 0649
namespace LibertyFramework.Hud.Logic
{
    // T-049 Basic Liberty HUD (config/hud.json): which elements Liberty draws, when they show and fade, where they sit and how
    // they look. Every number is data (AGENTS.md rule 3); HudPresence, HudLayout and HudPlan decide. The defaults written here
    // mirror config/hud.json, because DataContractJsonSerializer runs no constructor: a member absent from an older file would
    // otherwise be 0/null (a check keeps the file and these defaults identical).
    [DataContract]
    internal sealed class HudConfig
    {
        // "components": hide the vanilla elements Liberty replaces (hud.dat globals) and draw the Liberty ones in their place.
        // "none": leave the vanilla HUD untouched and draw nothing that would duplicate it (only the prompt restyle remains).
        internal const string HideComponents = "components";
        internal const string HideNone = "none";

        [DataMember(Name = "schemaVersion")] internal int SchemaVersion;
        // False restores the complete vanilla HUD and draws nothing.
        [DataMember(Name = "enabled")] internal bool Enabled;
        [DataMember(Name = "hideVanilla")] internal string HideVanilla;
        [DataMember(Name = "layout")] internal HudLayoutSettings Layout;
        [DataMember(Name = "palette")] internal HudPalette Palette;
        [DataMember(Name = "weapon")] internal WeaponElementSettings Weapon;
        [DataMember(Name = "health")] internal BarElementSettings Health;
        [DataMember(Name = "armour")] internal BarElementSettings Armour;
        [DataMember(Name = "wanted")] internal WantedElementSettings Wanted;
        [DataMember(Name = "prompt")] internal PromptElementSettings Prompt;
        [DataMember(Name = "suppress")] internal HudSuppressSettings Suppress;
        [DataMember(Name = "sampling")] internal HudSamplingSettings Sampling;

        internal HudConfig() { SetDefaults(); }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) { SetDefaults(); }

        private void SetDefaults()
        {
            SchemaVersion = 1;
            Enabled = true;
            HideVanilla = HideComponents;
            Layout = new HudLayoutSettings();
            Palette = new HudPalette();
            Weapon = new WeaponElementSettings();
            Health = BarElementSettings.HealthDefaults();
            Armour = BarElementSettings.ArmourDefaults();
            Wanted = new WantedElementSettings();
            Prompt = new PromptElementSettings();
            Suppress = new HudSuppressSettings();
            Sampling = new HudSamplingSettings();
        }

        internal static HudConfig Defaults() { return new HudConfig(); }

        internal bool HidesVanilla { get { return HideVanilla == HideComponents; } }

        internal void Validate()
        {
            if (SchemaVersion != 1) { throw new InvalidDataException("hud.json schemaVersion must be 1"); }
            if (HideVanilla != HideComponents && HideVanilla != HideNone) { throw new InvalidDataException("hud.json hideVanilla must be \"components\" or \"none\""); }
            if (Layout == null || Palette == null || Weapon == null || Health == null || Armour == null || Wanted == null ||
                Prompt == null || Suppress == null || Sampling == null) { throw new InvalidDataException("hud.json: a section is null"); }
            Layout.Validate();
            Palette.Validate();
            Weapon.Validate("weapon");
            Health.Validate("health");
            Armour.Validate("armour");
            Wanted.Validate();
            Prompt.Validate();
            Sampling.Validate();
        }

        internal static void RequireFade(string section, double fadeIn, double fadeOut, double hold)
        {
            if (fadeIn <= 0 || fadeIn > 5 || fadeOut <= 0 || fadeOut > 10) { throw new InvalidDataException("hud." + section + " fade times must be in (0, 5] and (0, 10] seconds"); }
            if (hold < 0 || hold > 600) { throw new InvalidDataException("hud." + section + " hold seconds must be 0-600"); }
        }

        internal static void RequireComponents(string section, List<string> components)
        {
            if (components == null) { throw new InvalidDataException("hud." + section + ".vanillaComponents must be a list"); }
            foreach (string name in components)
            {
                if (string.IsNullOrEmpty(name) || name.Length > 48) { throw new InvalidDataException("hud." + section + ".vanillaComponents holds an invalid name"); }
                foreach (char c in name)
                {
                    if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_')) { throw new InvalidDataException("hud." + section + ".vanillaComponents: " + name + " is not a hud.dat name"); }
                }
            }
        }
    }

    // Virtual 1280x720 units (Canvas): the screen is always 720 high, 720 x aspect wide. The group is anchored to the top right.
    [DataContract]
    internal sealed class HudLayoutSettings
    {
        [DataMember(Name = "marginRight")] internal float MarginRight;
        [DataMember(Name = "marginTop")] internal float MarginTop;
        [DataMember(Name = "iconWidth")] internal float IconWidth;
        [DataMember(Name = "iconHeight")] internal float IconHeight;
        [DataMember(Name = "ammoHeight")] internal float AmmoHeight;
        [DataMember(Name = "barWidth")] internal float BarWidth;
        [DataMember(Name = "barHeight")] internal float BarHeight;
        [DataMember(Name = "barGap")] internal float BarGap;
        [DataMember(Name = "rowGap")] internal float RowGap;
        [DataMember(Name = "starSize")] internal float StarSize;
        [DataMember(Name = "starGap")] internal float StarGap;
        [DataMember(Name = "promptX")] internal float PromptX;
        [DataMember(Name = "promptY")] internal float PromptY;
        [DataMember(Name = "promptWidth")] internal float PromptWidth;
        [DataMember(Name = "promptHeight")] internal float PromptHeight;
        [DataMember(Name = "shadowOffset")] internal float ShadowOffset;

        internal HudLayoutSettings() { SetDefaults(); }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) { SetDefaults(); }

        private void SetDefaults()
        {
            MarginRight = 28;
            MarginTop = 24;
            IconWidth = 96;
            IconHeight = 48;
            AmmoHeight = 22;
            BarWidth = 112;
            BarHeight = 5;
            BarGap = 4;
            RowGap = 6;
            StarSize = 16;
            StarGap = 4;
            PromptX = 34;
            PromptY = 30;
            PromptWidth = 360;
            PromptHeight = 44;
            ShadowOffset = 1;
        }

        internal void Validate()
        {
            if (MarginRight < 0 || MarginRight > 400 || MarginTop < 0 || MarginTop > 300) { throw new InvalidDataException("hud.layout margins out of range"); }
            if (IconWidth < 16 || IconWidth > 300 || IconHeight < 8 || IconHeight > 200) { throw new InvalidDataException("hud.layout icon size out of range"); }
            // Text below 14 px at 720p is unreadable (STAGE1 Pillar 1): the ammo line must be tall enough for the smallest style.
            if (AmmoHeight < 14 || AmmoHeight > 60) { throw new InvalidDataException("hud.layout.ammoHeight must be 14-60 (smallest text is 14 px)"); }
            if (BarWidth < 20 || BarWidth > 400 || BarHeight < 2 || BarHeight > 30 || BarGap < 0 || RowGap < 0 || StarSize < 6 || StarSize > 60 || StarGap < 0) { throw new InvalidDataException("hud.layout bars/stars out of range"); }
            if (PromptWidth < 100 || PromptWidth > 900 || PromptHeight < 20 || PromptHeight > 200 || PromptX < 0 || PromptY < 0) { throw new InvalidDataException("hud.layout prompt out of range"); }
            if (ShadowOffset < 0 || ShadowOffset > 4) { throw new InvalidDataException("hud.layout.shadowOffset must be 0-4"); }
        }
    }

    // One palette for the HUD (Slice C finalises it). Colours are [alpha, red, green, blue].
    [DataContract]
    internal sealed class HudPalette
    {
        [DataMember(Name = "text")] internal int[] Text;
        [DataMember(Name = "textShadow")] internal int[] TextShadow;
        [DataMember(Name = "icon")] internal int[] Icon;
        [DataMember(Name = "barBack")] internal int[] BarBack;
        [DataMember(Name = "health")] internal int[] Health;
        [DataMember(Name = "healthLow")] internal int[] HealthLow;
        [DataMember(Name = "armour")] internal int[] Armour;
        [DataMember(Name = "wantedStar")] internal int[] WantedStar;
        [DataMember(Name = "wantedEmpty")] internal int[] WantedEmpty;
        [DataMember(Name = "promptBack")] internal int[] PromptBack;
        [DataMember(Name = "promptText")] internal int[] PromptText;
        [DataMember(Name = "lowClip")] internal int[] LowClip;

        internal HudPalette() { SetDefaults(); }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) { SetDefaults(); }

        private void SetDefaults()
        {
            Text = new[] { 240, 232, 230, 224 };
            TextShadow = new[] { 200, 10, 10, 12 };
            Icon = new[] { 235, 232, 230, 224 };
            BarBack = new[] { 150, 12, 13, 15 };
            Health = new[] { 235, 134, 158, 104 };
            HealthLow = new[] { 245, 190, 58, 42 };
            Armour = new[] { 235, 150, 170, 196 };
            WantedStar = new[] { 240, 222, 170, 70 };
            WantedEmpty = new[] { 90, 120, 120, 120 };
            PromptBack = new[] { 200, 0, 0, 0 };
            PromptText = new[] { 255, 240, 240, 242 };
            LowClip = new[] { 245, 214, 120, 52 };
        }

        internal void Validate()
        {
            Check("text", Text); Check("textShadow", TextShadow); Check("icon", Icon); Check("barBack", BarBack); Check("health", Health);
            Check("healthLow", HealthLow); Check("armour", Armour); Check("wantedStar", WantedStar); Check("wantedEmpty", WantedEmpty);
            Check("promptBack", PromptBack); Check("promptText", PromptText); Check("lowClip", LowClip);
        }

        private static void Check(string name, int[] argb)
        {
            if (argb == null || argb.Length != 4) { throw new InvalidDataException("hud.palette." + name + " must be [alpha, red, green, blue]"); }
            foreach (int value in argb) { if (value < 0 || value > 255) { throw new InvalidDataException("hud.palette." + name + " values must be 0-255"); } }
        }
    }

    // Current weapon icon and ammunition: shown when something about the weapon happens, not permanently.
    [DataContract]
    internal sealed class WeaponElementSettings
    {
        [DataMember(Name = "enabled")] internal bool Enabled;
        [DataMember(Name = "fadeInSeconds")] internal double FadeInSeconds;
        [DataMember(Name = "fadeOutSeconds")] internal double FadeOutSeconds;
        // How long the group stays after its latest trigger.
        [DataMember(Name = "holdSeconds")] internal double HoldSeconds;
        [DataMember(Name = "showOnChange")] internal bool ShowOnChange;
        [DataMember(Name = "showOnShot")] internal bool ShowOnShot;
        [DataMember(Name = "showWhileReloading")] internal bool ShowWhileReloading;
        [DataMember(Name = "showWhileAiming")] internal bool ShowWhileAiming;
        // True keeps the group up whenever a weapon with ammunition is held.
        [DataMember(Name = "alwaysWhenArmed")] internal bool AlwaysWhenArmed;
        // The clip count turns the low-clip colour at or below this fraction of the clip's largest size seen this weapon.
        [DataMember(Name = "lowClipFraction")] internal double LowClipFraction;
        // GET_AMMO_IN_CHAR_WEAPON counts the rounds in the clip too: reserve = total - clip. Set false if a playtest shows it does not.
        [DataMember(Name = "totalIncludesClip")] internal bool TotalIncludesClip;
        // The pad trigger level (0-1) that counts as aiming with a controller; the right mouse button counts on the keyboard.
        [DataMember(Name = "aimTriggerLevel")] internal double AimTriggerLevel;
        [DataMember(Name = "vanillaComponents")] internal List<string> VanillaComponents;
        // Draw the Liberty element even when its vanilla counterpart could not be hidden (a duplicate). Off by default.
        [DataMember(Name = "drawWithoutHidingVanilla")] internal bool DrawWithoutHidingVanilla;

        internal WeaponElementSettings() { SetDefaults(); }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) { SetDefaults(); }

        private void SetDefaults()
        {
            Enabled = true;
            FadeInSeconds = 0.15;
            FadeOutSeconds = 0.6;
            HoldSeconds = 4;
            ShowOnChange = true;
            ShowOnShot = true;
            ShowWhileReloading = true;
            ShowWhileAiming = true;
            AlwaysWhenArmed = false;
            LowClipFraction = 0.25;
            TotalIncludesClip = true;
            AimTriggerLevel = 0.3;
            VanillaComponents = new List<string> { "HUD_WEAPON_ICON", "HUD_AMMO" };
            DrawWithoutHidingVanilla = false;
        }

        internal void Validate(string section)
        {
            HudConfig.RequireFade(section, FadeInSeconds, FadeOutSeconds, HoldSeconds);
            HudConfig.RequireComponents(section, VanillaComponents);
            if (LowClipFraction < 0 || LowClipFraction > 1 || AimTriggerLevel <= 0 || AimTriggerLevel > 1) { throw new InvalidDataException("hud." + section + " fractions must be 0-1"); }
        }
    }

    // Health or armour: a thin bar that shows on change, damage, low health and in combat.
    [DataContract]
    internal sealed class BarElementSettings
    {
        [DataMember(Name = "enabled")] internal bool Enabled;
        [DataMember(Name = "fadeInSeconds")] internal double FadeInSeconds;
        [DataMember(Name = "fadeOutSeconds")] internal double FadeOutSeconds;
        [DataMember(Name = "holdSeconds")] internal double HoldSeconds;
        [DataMember(Name = "showOnChange")] internal bool ShowOnChange;
        // Seconds the bar stays after a shot fired or damage taken (the "combat" trigger); 0 turns the trigger off.
        [DataMember(Name = "combatHoldSeconds")] internal double CombatHoldSeconds;
        // The bar stays up at or below this fraction of the maximum (health only; 0 = never).
        [DataMember(Name = "lowFraction")] internal double LowFraction;
        // Pulse of the bar's brightness while low (cycles per second, depth 0-1 of the alpha taken away at the dimmest point).
        [DataMember(Name = "pulsePerSecond")] internal double PulsePerSecond;
        [DataMember(Name = "pulseDepth")] internal double PulseDepth;
        // The value that fills the bar (gameplay scale: health is the game's raw value minus 100).
        [DataMember(Name = "maximum")] internal int Maximum;
        // Armour is only drawn while there is some (or it just changed); health is always drawn when the element shows.
        [DataMember(Name = "hideWhenZero")] internal bool HideWhenZero;
        // hud.dat components to hide when Liberty draws this bar. Empty = the vanilla counterpart is not known to be separable
        // (the health and armour rings belong to the radar): the bar is then NOT drawn, so nothing is duplicated.
        [DataMember(Name = "vanillaComponents")] internal List<string> VanillaComponents;
        [DataMember(Name = "drawWithoutHidingVanilla")] internal bool DrawWithoutHidingVanilla;

        internal BarElementSettings() { SetDefaults(); }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) { SetDefaults(); }

        private void SetDefaults()
        {
            Enabled = true;
            FadeInSeconds = 0.15;
            FadeOutSeconds = 0.8;
            HoldSeconds = 4;
            ShowOnChange = true;
            CombatHoldSeconds = 6;
            LowFraction = 0.3;
            PulsePerSecond = 1.2;
            PulseDepth = 0.45;
            Maximum = 100;
            HideWhenZero = false;
            VanillaComponents = new List<string>();
            DrawWithoutHidingVanilla = false;
        }

        internal static BarElementSettings HealthDefaults() { return new BarElementSettings(); }

        internal static BarElementSettings ArmourDefaults()
        {
            BarElementSettings armour = new BarElementSettings();
            armour.LowFraction = 0;
            armour.PulseDepth = 0;
            armour.HideWhenZero = true;
            return armour;
        }

        internal void Validate(string section)
        {
            HudConfig.RequireFade(section, FadeInSeconds, FadeOutSeconds, HoldSeconds);
            HudConfig.RequireComponents(section, VanillaComponents);
            if (CombatHoldSeconds < 0 || CombatHoldSeconds > 600) { throw new InvalidDataException("hud." + section + ".combatHoldSeconds must be 0-600"); }
            if (LowFraction < 0 || LowFraction > 1 || PulseDepth < 0 || PulseDepth > 1 || PulsePerSecond < 0 || PulsePerSecond > 10) { throw new InvalidDataException("hud." + section + " low/pulse values out of range"); }
            if (Maximum < 1 || Maximum > 1000) { throw new InvalidDataException("hud." + section + ".maximum must be 1-1000"); }
        }
    }

    [DataContract]
    internal sealed class WantedElementSettings
    {
        [DataMember(Name = "enabled")] internal bool Enabled;
        [DataMember(Name = "fadeInSeconds")] internal double FadeInSeconds;
        [DataMember(Name = "fadeOutSeconds")] internal double FadeOutSeconds;
        // The stars stay this long after the wanted level drops to zero, so the fade-out is seen.
        [DataMember(Name = "holdSeconds")] internal double HoldSeconds;
        // Stars drawn (GTA IV has six).
        [DataMember(Name = "maximumStars")] internal int MaximumStars;
        [DataMember(Name = "vanillaComponents")] internal List<string> VanillaComponents;
        [DataMember(Name = "drawWithoutHidingVanilla")] internal bool DrawWithoutHidingVanilla;

        internal WantedElementSettings() { SetDefaults(); }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) { SetDefaults(); }

        private void SetDefaults()
        {
            Enabled = true;
            FadeInSeconds = 0.2;
            FadeOutSeconds = 0.8;
            HoldSeconds = 1;
            MaximumStars = 6;
            VanillaComponents = new List<string> { "HUD_WANTED_BACK", "HUD_WANTED_FRONT" };
            DrawWithoutHidingVanilla = false;
        }

        internal void Validate()
        {
            HudConfig.RequireFade("wanted", FadeInSeconds, FadeOutSeconds, HoldSeconds);
            HudConfig.RequireComponents("wanted", VanillaComponents);
            if (MaximumStars < 1 || MaximumStars > 10) { throw new InvalidDataException("hud.wanted.maximumStars must be 1-10"); }
        }
    }

    // The IV-style help box (top left) that interaction prompts use, restyled, with button glyphs for the device in use.
    [DataContract]
    internal sealed class PromptElementSettings
    {
        [DataMember(Name = "enabled")] internal bool Enabled;
        [DataMember(Name = "fadeInSeconds")] internal double FadeInSeconds;
        [DataMember(Name = "fadeOutSeconds")] internal double FadeOutSeconds;
        // "auto" follows the device the player used last; "pad" and "keyboard" force one.
        [DataMember(Name = "device")] internal string Device;
        // Minimum milliseconds between two automatic device switches, so one stray key press during pad play does not flip the glyphs.
        [DataMember(Name = "deviceSwitchMilliseconds")] internal int DeviceSwitchMilliseconds;
        // {token} in a help text becomes the pad button or the keyboard key. Tokens are lower case.
        [DataMember(Name = "glyphs")] internal List<HudGlyph> Glyphs;

        internal PromptElementSettings() { SetDefaults(); }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) { SetDefaults(); }

        private void SetDefaults()
        {
            Enabled = true;
            FadeInSeconds = 0.12;
            FadeOutSeconds = 0.25;
            Device = "auto";
            DeviceSwitchMilliseconds = 400;
            Glyphs = new List<HudGlyph>
            {
                new HudGlyph("interact", "X", "E"),
                new HudGlyph("accept", "A", "Enter"),
                new HudGlyph("cancel", "B", "Backspace"),
                new HudGlyph("reload", "X", "R"),
                new HudGlyph("aim", "LT", "Right Mouse"),
                new HudGlyph("fire", "RT", "Left Mouse"),
                new HudGlyph("wheel", "RB", "Tab"),
            };
        }

        internal void Validate()
        {
            HudConfig.RequireFade("prompt", FadeInSeconds, FadeOutSeconds, 0);
            if (Device != "auto" && Device != "pad" && Device != "keyboard") { throw new InvalidDataException("hud.prompt.device must be auto, pad or keyboard"); }
            if (DeviceSwitchMilliseconds < 0 || DeviceSwitchMilliseconds > 10000) { throw new InvalidDataException("hud.prompt.deviceSwitchMilliseconds must be 0-10000"); }
            if (Glyphs == null) { throw new InvalidDataException("hud.prompt.glyphs must be a list"); }
            List<string> seen = new List<string>();
            foreach (HudGlyph glyph in Glyphs)
            {
                if (glyph == null || string.IsNullOrEmpty(glyph.Token) || string.IsNullOrEmpty(glyph.Pad) || string.IsNullOrEmpty(glyph.Keyboard)) { throw new InvalidDataException("hud.prompt.glyphs: each entry needs token, pad and keyboard"); }
                if (glyph.Token != glyph.Token.ToLowerInvariant() || seen.Contains(glyph.Token)) { throw new InvalidDataException("hud.prompt.glyphs: token " + glyph.Token + " must be unique and lower case"); }
                seen.Add(glyph.Token);
            }
        }
    }

    [DataContract]
    internal sealed class HudGlyph
    {
        [DataMember(Name = "token")] internal string Token;
        [DataMember(Name = "pad")] internal string Pad;
        [DataMember(Name = "keyboard")] internal string Keyboard;

        internal HudGlyph() { }
        internal HudGlyph(string token, string pad, string keyboard) { Token = token; Pad = pad; Keyboard = keyboard; }
    }

    // When the whole HUD steps aside.
    [DataContract]
    internal sealed class HudSuppressSettings
    {
        [DataMember(Name = "inCutscenes")] internal bool InCutscenes;
        [DataMember(Name = "whenPaused")] internal bool WhenPaused;
        [DataMember(Name = "whenFadedOut")] internal bool WhenFadedOut;
        [DataMember(Name = "whenDead")] internal bool WhenDead;

        internal HudSuppressSettings() { SetDefaults(); }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) { SetDefaults(); }

        private void SetDefaults()
        {
            InCutscenes = true;
            WhenPaused = true;
            WhenFadedOut = true;
            WhenDead = true;
        }
    }

    // How often the slower readings are taken (each is a game call).
    [DataContract]
    internal sealed class HudSamplingSettings
    {
        [DataMember(Name = "wantedPollMilliseconds")] internal int WantedPollMilliseconds;
        [DataMember(Name = "ammoPollMilliseconds")] internal int AmmoPollMilliseconds;
        [DataMember(Name = "inputPollMilliseconds")] internal int InputPollMilliseconds;

        internal HudSamplingSettings() { SetDefaults(); }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) { SetDefaults(); }

        private void SetDefaults()
        {
            WantedPollMilliseconds = 250;
            AmmoPollMilliseconds = 100;
            InputPollMilliseconds = 100;
        }

        internal void Validate()
        {
            if (WantedPollMilliseconds < 16 || AmmoPollMilliseconds < 16 || InputPollMilliseconds < 16 ||
                WantedPollMilliseconds > 5000 || AmmoPollMilliseconds > 5000 || InputPollMilliseconds > 5000) { throw new InvalidDataException("hud.sampling intervals must be 16-5000 ms"); }
        }
    }
}
