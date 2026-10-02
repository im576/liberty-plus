using System;
using System.Collections.Generic;

namespace LibertyFramework.Atmosphere.Logic
{
    // M-2 weather director logic (no game calls; offline-tested). Weather comes in blocks of a few in-game hours; the
    // next block is drawn from the day or night weights, excluding transitions that would look wrong from the street
    // (e.g. clear sky straight into a storm). Same-weather repeats are allowed, so grey spells last.
    internal sealed class WeatherForecast
    {
        private readonly Random random;

        internal WeatherForecast(int seed) { random = new Random(seed); }

        internal int BlockHours(WeatherSettings settings)
        {
            return random.Next(settings.BlockMinimumHours, settings.BlockMaximumHours + 1);
        }

        internal int Next(WeatherSettings settings, int current, int hour)
        {
            List<WeatherWeight> weights = settings.IsNight(hour) ? settings.NightWeights : settings.DayWeights;
            double total = 0;
            List<KeyValuePair<int, double>> options = new List<KeyValuePair<int, double>>();
            foreach (WeatherWeight weight in weights)
            {
                int id = AtmosphereConfig.WeatherId(weight.Weather);
                if (weight.Weight <= 0 || Forbidden(settings, current, id)) { continue; }
                options.Add(new KeyValuePair<int, double>(id, weight.Weight));
                total += weight.Weight;
            }
            if (options.Count == 0 || total <= 0) { return current >= 0 ? current : AtmosphereConfig.WeatherId("CLOUDY"); }
            double pick = random.NextDouble() * total;
            foreach (KeyValuePair<int, double> option in options)
            {
                pick -= option.Value;
                if (pick <= 0) { return option.Key; }
            }
            return options[options.Count - 1].Key;
        }

        internal static bool Forbidden(WeatherSettings settings, int from, int to)
        {
            if (from < 0 || from >= AtmosphereConfig.WeatherNames.Length) { return false; }
            return settings.ForbiddenTransitions.Contains(AtmosphereConfig.WeatherNames[from] + ">" + AtmosphereConfig.WeatherNames[to]);
        }
    }
}
