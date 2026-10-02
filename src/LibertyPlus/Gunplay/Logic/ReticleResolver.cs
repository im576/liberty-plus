using System;
using LibertyFramework.Gunplay.Profiles;
using LibertyFramework.Weapons.Logic;

namespace LibertyFramework.Gunplay.Logic
{
    // A reticle with every field decided (T-043): what the renderer draws.
    internal sealed class ResolvedReticle
    {
        internal string Style;
        internal string WeaponClass;
        internal double LineLengthPixels;
        internal double LineThicknessPixels;
        internal double OutlinePixels;
        internal double MinimumGapPixels;
        internal double MaximumGapPixels;
        internal double CenterDotPixels;
        internal int RingDots;
        internal int[] ColorArgb;
        internal int[] OutlineArgb;
        internal double GapSmoothingPerSecond;

        internal bool IsNone { get { return Style == "none"; } }
    }

    // Decides which reticle a held weapon gets: the old `crosshair` section is the base, the weapon's class changes what it
    // names, and a per-weapon entry changes the rest. Pure (no game calls); the controller supplies the class.
    internal static class ReticleResolver
    {
        internal static readonly string[] Styles = { "cross", "bracket", "ring", "dot", "none" };
        private const int DefaultRingDots = 12;

        // The weapon's class: its catalog entry's class when it has one, else the class the caller derived from the inventory slot.
        internal static string ClassOf(WeaponCatalog catalog, int weaponId, string slotClass)
        {
            WeaponCatalogEntry entry = catalog == null ? null : catalog.Find(weaponId);
            return entry != null && !string.IsNullOrEmpty(entry.WeaponClass) ? entry.WeaponClass : slotClass;
        }

        internal static ResolvedReticle Resolve(GunplayConfig config, string weaponClass, int weaponId)
        {
            CrosshairSettings legacy = config.Crosshair;
            ResolvedReticle result = new ResolvedReticle();
            result.Style = "cross";
            result.WeaponClass = weaponClass;
            result.LineLengthPixels = legacy.LineLengthPixels;
            result.LineThicknessPixels = legacy.LineThicknessPixels;
            result.OutlinePixels = legacy.OutlinePixels;
            result.MinimumGapPixels = legacy.MinimumGapPixels;
            result.MaximumGapPixels = legacy.MaximumGapPixels;
            result.CenterDotPixels = legacy.ShowCenterDot ? legacy.CenterDotPixels : 0;
            result.RingDots = DefaultRingDots;
            result.ColorArgb = legacy.ColorArgb;
            result.OutlineArgb = legacy.OutlineArgb;
            result.GapSmoothingPerSecond = legacy.GapSmoothingPerSecond;
            if (config.Reticles == null || !config.Reticles.Enabled) { return result; }

            string styleClass = weaponClass;
            ReticleWeaponSettings weapon = FindWeapon(config.Reticles, weaponId);
            if (weapon != null && !string.IsNullOrEmpty(weapon.WeaponClass)) { styleClass = weapon.WeaponClass; }
            ReticleClassSettings classSettings = FindClass(config.Reticles, styleClass);
            if (classSettings != null) { Apply(result, classSettings.Style); }
            if (weapon != null) { Apply(result, weapon.Style); }
            result.WeaponClass = weaponClass;
            return result;
        }

        private static void Apply(ResolvedReticle target, ReticleStyleSettings style)
        {
            if (style == null) { return; }
            if (!string.IsNullOrEmpty(style.Style)) { target.Style = style.Style; }
            if (style.LineLengthPixels.HasValue) { target.LineLengthPixels = style.LineLengthPixels.Value; }
            if (style.LineThicknessPixels.HasValue) { target.LineThicknessPixels = style.LineThicknessPixels.Value; }
            if (style.OutlinePixels.HasValue) { target.OutlinePixels = style.OutlinePixels.Value; }
            if (style.MinimumGapPixels.HasValue) { target.MinimumGapPixels = style.MinimumGapPixels.Value; }
            if (style.MaximumGapPixels.HasValue) { target.MaximumGapPixels = style.MaximumGapPixels.Value; }
            if (style.CenterDotPixels.HasValue) { target.CenterDotPixels = style.CenterDotPixels.Value; }
            if (style.RingDots.HasValue) { target.RingDots = style.RingDots.Value; }
            if (style.ColorArgb != null) { target.ColorArgb = style.ColorArgb; }
            if (style.OutlineArgb != null) { target.OutlineArgb = style.OutlineArgb; }
            if (style.GapSmoothingPerSecond.HasValue) { target.GapSmoothingPerSecond = style.GapSmoothingPerSecond.Value; }
        }

        internal static ReticleClassSettings FindClass(ReticleSettings settings, string weaponClass)
        {
            if (settings == null || settings.Classes == null || weaponClass == null) { return null; }
            foreach (ReticleClassSettings entry in settings.Classes) { if (entry.WeaponClass == weaponClass) { return entry; } }
            return null;
        }

        internal static ReticleWeaponSettings FindWeapon(ReticleSettings settings, int weaponId)
        {
            if (settings == null || settings.Weapons == null) { return null; }
            foreach (ReticleWeaponSettings entry in settings.Weapons) { if (entry.WeaponId == weaponId) { return entry; } }
            return null;
        }

        // Screen pixels the cone opens to: the game's own projection when measured, else the FOV formula. No cosmetic factor:
        // the reticle is the cone (T-043, STAGE1 Pillar 3 reticle truthfulness), clamped only by the style's minimum and maximum.
        internal static double TargetPixels(double coneDegrees, double pixelsPerTangent, double fallbackPixelsPerTangent, ResolvedReticle style)
        {
            double perTangent = pixelsPerTangent > 0 ? pixelsPerTangent : fallbackPixelsPerTangent;
            double raw = Math.Tan(coneDegrees * Math.PI / 180.0) * perTangent;
            return Math.Max(style.MinimumGapPixels, Math.Min(style.MaximumGapPixels, raw));
        }
    }
}
