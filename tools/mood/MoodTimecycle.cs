using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace LibertyFramework.Mood
{
    // M-1 "Liberty Mood": generates timecyc.dat / timecycext.dat from the pristine FusionFix files and config/mood.json.
    // Every rule matches a weather class and a time class; its multipliers and adds compound in order. Numbers are
    // rewritten in place with their original formatting (ints stay ints, decimal places kept), so the output diffs
    // cleanly against the source and FusionFix's parser sees the same layout.
    //
    // usage: MoodTimecycle <mood.json> <source timecyc.dat> <source timecycext.dat> <out timecyc.dat> <out timecycext.dat>
    internal static class MoodTimecycle
    {
        // FusionFix 5.0.1 timecyc_scanf layout, verified at upstream commit 619f52d.
        // The legacy X360 header is misleading: columns 9-11 are NOT sky RGB (10 is grain, 11 fog alpha).
        private const int FogStart = 24, Bloom = 38, ColourCorrectR = 39, Desaturation = 45, Contrast = 46,
            DesaturationFar = 48, ContrastFar = 49, DirLightMult = 57, AmbLightMult0 = 58, AmbLightMult1 = 59,
            SkyLightMult = 60, DirectionalSpecMult = 61;
        private const int VolFogDensity = 0; // timecycext.dat

        private static int Main(string[] args)
        {
            if (args.Length != 5) { Console.WriteLine("usage: MoodTimecycle <mood.json> <src timecyc> <src ext> <out timecyc> <out ext>"); return 2; }
            try
            {
                Dictionary<string, object> mood = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(File.ReadAllText(args[0]));
                Config config = new Config(mood);
                int rows = Transform(args[1], args[3], config, false);
                int extRows = Transform(args[2], args[4], config, true);
                Console.WriteLine("mood: timecyc rows=" + rows + " ext rows=" + extRows);
                return rows == 8 * 11 && extRows >= 8 * 11 ? 0 : 1;
            }
            catch (Exception error) { Console.WriteLine("mood failed: " + error); return 1; }
        }

        private static int Transform(string source, string target, Config config, bool ext)
        {
            string[] lines = File.ReadAllLines(source);
            string weather = null, time = null;
            int changed = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();
                if (trimmed.StartsWith("//////////")) { weather = trimmed.Trim('/', ' '); time = null; continue; }
                if (trimmed.StartsWith("//"))
                {
                    string label = trimmed.Substring(2).Trim();
                    if (config.KnownTime(label)) { time = label; }
                    continue;
                }
                if (weather == null || time == null || trimmed.Length == 0 || !char.IsDigit(trimmed[0])) { continue; }
                if (!config.KnownWeather(weather)) { continue; } // e.g. TEMP
                List<Rule> rules = config.Matching(weather, time);
                lines[i] = ext ? ApplyExt(line, rules) : Apply(line, rules, config);
                time = null; // one data row per time label
                changed++;
            }
            File.WriteAllLines(target, lines, new UTF8Encoding(false));
            return changed;
        }

        private static string Apply(string line, List<Rule> rules, Config config)
        {
            List<Token> tokens = Tokenize(line);
            if (tokens.Count < 134) { throw new InvalidDataException("timecyc row is shorter than the verified FusionFix 5.0.1 layout"); }
            foreach (Rule rule in rules)
            {
                SetColour(tokens, 0, rule.Ambient0);
                SetColour(tokens, 3, rule.Ambient1);
                SetColour(tokens, 6, rule.DirectColour);
                SetSkyColour(tokens, 64, rule.SkyTop);
                SetSkyColour(tokens, 67, rule.SkyBottom);
                SetSkyColour(tokens, 70, rule.SkyBottom);
                SetSkyColour(tokens, 73, rule.LowClouds);
                SetSkyColour(tokens, 81, rule.BottomClouds);
                SetSkyColour(tokens, 99, rule.LowClouds);
                SetColour(tokens, 12, rule.SkyBottom);
                SetColour(tokens, 25, rule.LowClouds);
                SetColour(tokens, 28, rule.BottomClouds);
                if (rule.CloudAlpha.HasValue) { tokens[56].Value = rule.CloudAlpha.Value; tokens[56].Changed = true; }
                SetColour(tokens, ColourCorrectR, rule.ColourCorrectRgb);
                Scale(tokens, AmbLightMult0, rule.AmbientScale); Scale(tokens, AmbLightMult1, rule.AmbientScale);
                Scale(tokens, SkyLightMult, rule.SkyScale);
                Scale(tokens, DirLightMult, rule.DirectScale);
                // Keep the historical JSON name sunScale compatible: FusionFix uses this column for directional specular.
                Scale(tokens, DirectionalSpecMult, rule.SunScale);
                Scale(tokens, Bloom, rule.BloomScale);
                Scale(tokens, FogStart, rule.FogStartScale);
                Add(tokens, Desaturation, rule.DesaturationAdd, 0, config.DesaturationMax);
                Add(tokens, DesaturationFar, rule.DesaturationAdd, 0, config.DesaturationMax);
                Add(tokens, Contrast, rule.ContrastAdd, 0.5, config.ContrastMax);
                Add(tokens, ContrastFar, rule.ContrastAdd, 0.5, config.ContrastMax);
                if (rule.ColourCorrect != null)
                {
                    for (int c = 0; c < 3; c++) { Scale(tokens, ColourCorrectR + c, rule.ColourCorrect[c], 0, 255); }
                }
            }
            return Join(line, tokens);
        }

        private static void SetColour(List<Token> tokens, int index, double[] colour)
        {
            if (colour == null) { return; }
            for (int c = 0; c < 3; c++) { tokens[index + c].Value = colour[c]; tokens[index + c].Changed = true; }
        }

        private static void SetSkyColour(List<Token> tokens, int index, double[] colour)
        {
            if (colour == null) { return; }
            for (int c = 0; c < 3; c++) { tokens[index + c].Value = colour[c] / 255.0; tokens[index + c].Changed = true; }
        }

        private static string ApplyExt(string line, List<Rule> rules)
        {
            List<Token> tokens = Tokenize(line);
            foreach (Rule rule in rules) { Scale(tokens, VolFogDensity, rule.VolumetricFogScale); }
            return Join(line, tokens);
        }

        private sealed class Token
        {
            internal int Start, Length, Decimals;
            internal double Value;
            internal bool Changed;
        }

        private static List<Token> Tokenize(string line)
        {
            List<Token> tokens = new List<Token>();
            foreach (Match match in Regex.Matches(line, @"-?\d+(\.\d+)?"))
            {
                Token token = new Token();
                token.Start = match.Index; token.Length = match.Length;
                token.Decimals = match.Groups[1].Success ? match.Groups[1].Value.Length - 1 : 0;
                token.Value = double.Parse(match.Value, CultureInfo.InvariantCulture);
                tokens.Add(token);
            }
            return tokens;
        }

        private static void Scale(List<Token> tokens, int index, double factor, double minimum = double.MinValue, double maximum = double.MaxValue)
        {
            if (factor == 1.0 || index >= tokens.Count) { return; }
            tokens[index].Value = Math.Max(minimum, Math.Min(maximum, tokens[index].Value * factor));
            tokens[index].Changed = true;
        }

        private static void Add(List<Token> tokens, int index, double amount, double minimum, double maximum)
        {
            if (amount == 0 || index >= tokens.Count) { return; }
            tokens[index].Value = Math.Max(minimum, Math.Min(maximum, tokens[index].Value + amount));
            tokens[index].Changed = true;
        }

        // Rewrites changed numbers right-aligned in their original column width where possible.
        private static string Join(string line, List<Token> tokens)
        {
            StringBuilder text = new StringBuilder(line);
            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                Token token = tokens[i];
                if (!token.Changed) { continue; }
                string value = token.Decimals == 0 ? Math.Round(token.Value).ToString(CultureInfo.InvariantCulture)
                    : token.Value.ToString("F" + token.Decimals, CultureInfo.InvariantCulture);
                if (value.Length < token.Length) { value = value.PadLeft(token.Length); }
                text.Remove(token.Start, token.Length).Insert(token.Start, value);
            }
            return text.ToString();
        }

        private sealed class Rule
        {
            internal string Weather, Time;
            internal double AmbientScale = 1, SkyScale = 1, DirectScale = 1, SunScale = 1, BloomScale = 1, FogStartScale = 1,
                VolumetricFogScale = 1, DesaturationAdd, ContrastAdd;
            internal double[] ColourCorrect;
            internal double[] SkyTop, SkyBottom, LowClouds, BottomClouds;
            internal double[] Ambient0, Ambient1, DirectColour, ColourCorrectRgb;
            internal double? CloudAlpha;
        }

        private sealed class Config
        {
            private readonly Dictionary<string, string> weatherClass = new Dictionary<string, string>();
            private readonly Dictionary<string, string> timeClass = new Dictionary<string, string>();
            private readonly List<Rule> rules = new List<Rule>();
            internal readonly double DesaturationMax, ContrastMax;

            internal Config(Dictionary<string, object> mood)
            {
                if (Convert.ToInt32(mood["schemaVersion"]) != 1) { throw new InvalidDataException("mood.json schemaVersion must be 1"); }
                foreach (KeyValuePair<string, object> pair in (Dictionary<string, object>)mood["weatherClasses"])
                    foreach (object name in (IEnumerable)pair.Value) { weatherClass[(string)name] = pair.Key; }
                foreach (KeyValuePair<string, object> pair in (Dictionary<string, object>)mood["timeClasses"])
                    foreach (object name in (IEnumerable)pair.Value) { timeClass[(string)name] = pair.Key; }
                foreach (object item in (IEnumerable)mood["rules"])
                {
                    Dictionary<string, object> raw = (Dictionary<string, object>)item;
                    Rule rule = new Rule();
                    rule.Weather = (string)raw["weather"]; rule.Time = (string)raw["time"];
                    rule.AmbientScale = Number(raw, "ambientScale", 1); rule.SkyScale = Number(raw, "skyScale", 1);
                    rule.DirectScale = Number(raw, "directScale", 1); rule.SunScale = Number(raw, "sunScale", 1);
                    rule.BloomScale = Number(raw, "bloomScale", 1); rule.FogStartScale = Number(raw, "fogStartScale", 1);
                    rule.VolumetricFogScale = Number(raw, "volumetricFogScale", 1);
                    rule.DesaturationAdd = Number(raw, "desaturationAdd", 0); rule.ContrastAdd = Number(raw, "contrastAdd", 0);
                    rule.SkyTop = Colour(raw, "skyTop", 0, 255);
                    rule.SkyBottom = Colour(raw, "skyBottom", 0, 255);
                    rule.LowClouds = Colour(raw, "lowClouds", 0, 255);
                    rule.BottomClouds = Colour(raw, "bottomClouds", 0, 255);
                    rule.Ambient0 = Colour(raw, "ambient0", 0, 255);
                    rule.Ambient1 = Colour(raw, "ambient1", 0, 255);
                    rule.DirectColour = Colour(raw, "directColour", 0, 255);
                    rule.ColourCorrectRgb = Colour(raw, "colourCorrectRgb", 0, 255);
                    if (raw.ContainsKey("cloudAlpha"))
                    {
                        double alpha = Number(raw, "cloudAlpha", 0);
                        if (double.IsNaN(alpha) || double.IsInfinity(alpha) || alpha < 0 || alpha > 255)
                            throw new InvalidDataException("cloudAlpha must be finite and between 0 and 255");
                        rule.CloudAlpha = alpha;
                    }
                    if (raw.ContainsKey("colourCorrect"))
                    {
                        List<double> rgb = new List<double>();
                        foreach (object v in (IEnumerable)raw["colourCorrect"]) { rgb.Add(Convert.ToDouble(v, CultureInfo.InvariantCulture)); }
                        if (rgb.Count != 3) { throw new InvalidDataException("colourCorrect needs 3 values"); }
                        rule.ColourCorrect = rgb.ToArray();
                    }
                    foreach (double factor in new[] { rule.AmbientScale, rule.SkyScale, rule.DirectScale, rule.SunScale, rule.BloomScale, rule.FogStartScale, rule.VolumetricFogScale })
                        if (factor < 0.1 || factor > 3) { throw new InvalidDataException("scale out of range 0.1-3"); }
                    rules.Add(rule);
                }
                Dictionary<string, object> limits = (Dictionary<string, object>)mood["limits"];
                DesaturationMax = Number(limits, "desaturationMax", 0.85); ContrastMax = Number(limits, "contrastMax", 1.35);
            }

            private static double Number(Dictionary<string, object> raw, string key, double fallback)
            {
                return raw.ContainsKey(key) ? Convert.ToDouble(raw[key], CultureInfo.InvariantCulture) : fallback;
            }

            private static double[] Colour(Dictionary<string, object> raw, string key, double minimum, double maximum)
            {
                if (!raw.ContainsKey(key)) { return null; }
                List<double> values = new List<double>();
                foreach (object item in (IEnumerable)raw[key])
                {
                    double value = Convert.ToDouble(item, CultureInfo.InvariantCulture);
                    if (double.IsNaN(value) || double.IsInfinity(value) || value < minimum || value > maximum)
                        throw new InvalidDataException(key + " values must be finite and between " + minimum + " and " + maximum);
                    values.Add(value);
                }
                if (values.Count != 3) { throw new InvalidDataException(key + " needs 3 values"); }
                return values.ToArray();
            }

            internal bool KnownWeather(string name) { return weatherClass.ContainsKey(name); }
            internal bool KnownTime(string name) { return timeClass.ContainsKey(name); }

            internal List<Rule> Matching(string weather, string time)
            {
                List<Rule> result = new List<Rule>();
                foreach (Rule rule in rules)
                {
                    bool weatherOk = rule.Weather == "*" || rule.Weather == weatherClass[weather] || rule.Weather == weather;
                    bool timeOk = rule.Time == "*" || rule.Time == timeClass[time] || rule.Time == time;
                    if (weatherOk && timeOk) { result.Add(rule); }
                }
                return result;
            }
        }
    }
}
