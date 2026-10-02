using System;
using System.IO;
using LibertyFramework.Arsenal.Contracts;
using LibertyFramework.Arsenal.Logic;
using LibertyFramework.Core.Config;

namespace LibertyFramework.Verify
{
    // Offline tests for the Liberty weapon wheel (T-045): segments, press handling and the bindings in arsenal.json.
    internal static class WeaponWheelChecks
    {
        internal static void Run(string repoRoot, Checker check)
        {
            BudgetChecks(check);
            TextLayoutChecks(check);
            check.True("wheel segments: sidearm, two long guns, melee, thrown",
                WeaponWheelLogic.SegmentOf(BodySlot.SidearmPrimary, WeaponCategory.Handgun) == WeaponWheelLogic.Sidearm &&
                WeaponWheelLogic.SegmentOf(BodySlot.LongGun1, WeaponCategory.Rifle) == WeaponWheelLogic.LongGun1 &&
                WeaponWheelLogic.SegmentOf(BodySlot.LongGun2, WeaponCategory.SMG) == WeaponWheelLogic.LongGun2 &&
                WeaponWheelLogic.SegmentOf(BodySlot.Melee, WeaponCategory.Melee) == WeaponWheelLogic.Melee &&
                WeaponWheelLogic.SegmentOf(BodySlot.None, WeaponCategory.Thrown) == WeaponWheelLogic.Thrown, "");
            check.True("wheel segments: a second sidearm and unknown weapons have no segment",
                WeaponWheelLogic.SegmentOf(BodySlot.SidearmSecondary, WeaponCategory.SMG) == -1 && WeaponWheelLogic.SegmentOf(BodySlot.None, WeaponCategory.Other) == -1, "");
            check.True("wheel press: a tap keeps it open, a hold equips on release",
                WeaponWheelLogic.OnRelease(120, 250) == WeaponWheelLogic.Release.StayOpen && WeaponWheelLogic.OnRelease(250, 250) == WeaponWheelLogic.Release.Equip &&
                WeaponWheelLogic.OnRelease(900, 250) == WeaponWheelLogic.Release.Equip, "");
            int[] slots = { 7, 0, 14, 0, 0 };
            check.True("wheel highlight starts on the weapon in hand, else the first filled slot, else the top",
                WeaponWheelLogic.StartSegment(slots, 14) == 2 && WeaponWheelLogic.StartSegment(slots, 99) == 0 && WeaponWheelLogic.StartSegment(new int[] { 0, 0, 5, 0, 0 }, 99) == 2 &&
                WeaponWheelLogic.StartSegment(new int[5], 0) == 0, "");

            ArsenalConfig config = JsonStore.Load<ArsenalConfig>(Path.Combine(repoRoot, "config/arsenal.json"));
            ArsenalConfigValidator.Validate(config);
            check.True("wheel config ships enabled with a pad button, a key and a tap time",
                config.WeaponWheel != null && config.WeaponWheel.Enabled && config.WeaponWheel.TapMilliseconds == 250 && !config.WeaponWheel.AllowInVehicle, "");
            check.True("wheel bindings avoid the wheel's own navigation and the DevTools chord",
                config.WeaponWheel.Pad != Liberty.Sdk.PadButton.LeftThumb && config.WeaponWheel.Pad != Liberty.Sdk.PadButton.RightThumb &&
                config.WeaponWheel.Key != Liberty.Sdk.VirtualKey.F10, "");
            check.True("a file without the weaponWheel block still loads with defaults", WeaponWheelConfig.Defaults().Enabled, "");

            Rejected(check, repoRoot, "wheel keyboard key Enter is rejected (navigation key)", c => c.WeaponWheel.KeyboardKeyName = "Enter");
            Rejected(check, repoRoot, "wheel pad button A is rejected (navigation button)", c => c.WeaponWheel.PadButtonName = "A");
            Rejected(check, repoRoot, "wheel pad button X is rejected (trunk store)", c => c.WeaponWheel.PadButtonName = "X");
            Rejected(check, repoRoot, "wheel right shoulder is rejected (storage list navigation)", c => c.WeaponWheel.PadButtonName = "RightShoulder");
            Rejected(check, repoRoot, "wheel keyboard E is rejected (trunk open)", c => c.WeaponWheel.KeyboardKeyName = "E");
            Rejected(check, repoRoot, "wheel keyboard Space is rejected (trunk store)", c => c.WeaponWheel.KeyboardKeyName = "Space");
            Rejected(check, repoRoot, "wheel unknown key name is rejected", c => c.WeaponWheel.KeyboardKeyName = "NoSuchKey");
            Rejected(check, repoRoot, "wheel numeric button name is rejected", c => c.WeaponWheel.PadButtonName = "12345");
            Rejected(check, repoRoot, "wheel tap time outside 50-1000 ms is rejected", c => c.WeaponWheel.TapMilliseconds = 5000);
        }

        private static void BudgetChecks(Checker check)
        {
            string baseline = "frames=200 avg_ms=20 p95_ms=25 p99_ms=40 stalls_1s=0";
            string good = "frames=180 avg_ms=21 p95_ms=27 p99_ms=45 stalls_1s=0";
            string costs = "costs_ms(avg/max/count@thread) draw.ui=0.100/1.0/180@6 total=18";
            Func<string, string, string> evaluate = (frames, draw) => LibertyFramework.Engine.Ui.Logic.UiBudgetLogic.Evaluate(frames, draw, baseline, 0.5, 1.10, 1.15, 30);
            check.True("UI budgets accept bounded frame and draw samples", evaluate(good, costs) == null, "");
            check.True("UI budgets reject the old 390 ms/menu frame sample", evaluate("frames=11 avg_ms=390 p95_ms=250.1 p99_ms=250.1 stalls_1s=0", costs) != null, "");
            check.True("UI budgets reject excessive draw cost even with fast frames", evaluate(good, costs.Replace("0.100/", "0.800/")) == "draw_average", "");
            check.True("UI budgets reject excessive frame cost even with cheap submission", evaluate(good.Replace("avg_ms=21", "avg_ms=390"), costs) == "frame_average", "");
            check.True("UI budgets reject excessive p95", evaluate(good.Replace("p95_ms=27", "p95_ms=30"), costs) == "frame_p95", "");
            check.True("UI budgets reject excessive p99", evaluate(good.Replace("p99_ms=45", "p99_ms=50"), costs) == "frame_p99", "");
            check.True("UI budgets reject stalls and missing/empty windows", evaluate(good.Replace("stalls_1s=0", "stalls_1s=1"), costs) == "stall" && evaluate("frames=0", costs) != null && evaluate(good, "") != null, "");
        }

        // Sprite text layout (the shared canvas text renderer that replaced the stalling DrawText path).
        private static void TextLayoutChecks(Checker check)
        {
            Func<float, float, float, Liberty.Sdk.TextAlign, float> x = LibertyFramework.Engine.Ui.Logic.UiTextLogic.AlignedX;
            check.True("sprite text: left, centre and right alignment inside the rectangle",
                x(100, 300, 120, Liberty.Sdk.TextAlign.Left) == 100 && x(100, 300, 120, Liberty.Sdk.TextAlign.Center) == 190 && x(100, 300, 120, Liberty.Sdk.TextAlign.Right) == 280, "");
            check.True("sprite text: text as wide as its rectangle starts at the rectangle's edge", x(100, 300, 300, Liberty.Sdk.TextAlign.Center) == 100 && x(100, 300, 410, Liberty.Sdk.TextAlign.Right) == 100, "");
            string a = LibertyFramework.Engine.Ui.Logic.UiTextLogic.CacheKey(1, 16f, 300f, false, "Glock 17");
            check.True("sprite text cache key: equal requests share it, style, size, width and text separate it",
                a == LibertyFramework.Engine.Ui.Logic.UiTextLogic.CacheKey(1, 16f, 300f, false, "Glock 17") &&
                a != LibertyFramework.Engine.Ui.Logic.UiTextLogic.CacheKey(2, 16f, 300f, false, "Glock 17") &&
                a != LibertyFramework.Engine.Ui.Logic.UiTextLogic.CacheKey(1, 24f, 300f, false, "Glock 17") &&
                a != LibertyFramework.Engine.Ui.Logic.UiTextLogic.CacheKey(1, 16f, 600f, false, "Glock 17") &&
                a != LibertyFramework.Engine.Ui.Logic.UiTextLogic.CacheKey(1, 16f, 300f, false, "Glock 18"), "");
            check.True("sprite text cache: nearby widths, subpixel font sizes and case retain distinct content",
                a != LibertyFramework.Engine.Ui.Logic.UiTextLogic.CacheKey(1, 16f, 301f, false, "Glock 17") &&
                a != LibertyFramework.Engine.Ui.Logic.UiTextLogic.CacheKey(1, 16.01f, 300f, false, "Glock 17") &&
                a != LibertyFramework.Engine.Ui.Logic.UiTextLogic.CacheKey(1, 16f, 300f, false, "GLOCK 17"), "");
            Func<string, float> measure = value => { float width = 0; foreach (char c in value) { width += c == 'W' ? 12 : c == '.' ? 2 : 4; } return width; };
            string fitted = LibertyFramework.Engine.Ui.Logic.UiTextLogic.FitText("WWWWiiii", 31, measure);
            check.True("sprite text: proportional label plus ellipsis fits the actual rectangle", fitted == "WW..." && measure(fitted) <= 31, fitted);
            check.True("sprite text: a rectangle narrower than the ellipsis draws no overflowing text",
                LibertyFramework.Engine.Ui.Logic.UiTextLogic.FitText("WW", 5, measure) == "", "");
            check.True("sprite text: fitting content is retained without ellipsis",
                LibertyFramework.Engine.Ui.Logic.UiTextLogic.FitText("iiii", 16, measure) == "iiii", "");
            string combined = LibertyFramework.Engine.Ui.Logic.UiTextLogic.FitText("e\u0301WWW", 15, measure);
            check.True("sprite text: truncation keeps a combining accent with its letter", combined == "e\u0301...", combined);
        }

        private static void Rejected(Checker check, string repoRoot, string name, Action<ArsenalConfig> mutate)
        {
            ArsenalConfig config = JsonStore.Load<ArsenalConfig>(Path.Combine(repoRoot, "config/arsenal.json"));
            mutate(config);
            bool rejected = false;
            try { ArsenalConfigValidator.Validate(config); }
            catch (InvalidDataException) { rejected = true; }
            check.True(name, rejected, "");
        }
    }
}
