using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace LibertyFramework.Arsenal.Logic
{
    internal sealed class LvsOwnedVehicleReader
    {
        internal static List<LvsOwnedVehicleEntry> Parse(string content)
        {
            List<LvsOwnedVehicleEntry> entries = new List<LvsOwnedVehicleEntry>();
            LvsOwnedVehicleEntry current = null;
            using (StringReader reader = new StringReader(content))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.StartsWith(";") || line.StartsWith("#") || line.Length == 0) { continue; }
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        string section = line.Substring(1, line.Length - 2).Trim();
                        current = section.StartsWith("owned.", StringComparison.OrdinalIgnoreCase) ? new LvsOwnedVehicleEntry() : null;
                        if (current != null) { current.Id = section.Substring(6); entries.Add(current); }
                        continue;
                    }
                    if (current == null) { continue; }
                    int separator = line.IndexOf('=');
                    if (separator < 1) { continue; }
                    string key = line.Substring(0, separator).Trim().ToLowerInvariant();
                    string value = line.Substring(separator + 1).Trim();
                    int number; float coordinate;
                    if (key == "episode") { current.Episode = value.ToLowerInvariant(); }
                    else if (key == "modelhash" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) { current.ModelHash = number; }
                    else if (key == "x" && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out coordinate)) { current.X = coordinate; }
                    else if (key == "y" && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out coordinate)) { current.Y = coordinate; }
                    else if (key == "z" && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out coordinate)) { current.Z = coordinate; }
                    else if (key == "destroyed") { current.Destroyed = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase); }
                }
            }
            return entries;
        }

        internal static string Match(IList<LvsOwnedVehicleEntry> entries, string episode, int modelHash, float x, float y, float z, float maximumMeters)
        {
            string best = null; double bestSquared = maximumMeters * maximumMeters;
            foreach (LvsOwnedVehicleEntry entry in entries)
            {
                if (entry.Destroyed || entry.ModelHash != modelHash || !string.Equals(entry.Episode, episode, StringComparison.OrdinalIgnoreCase)) { continue; }
                double distance = (entry.X - x) * (entry.X - x) + (entry.Y - y) * (entry.Y - y) + (entry.Z - z) * (entry.Z - z);
                if (distance <= bestSquared) { best = entry.Id; bestSquared = distance; }
            }
            return best;
        }
    }
}
