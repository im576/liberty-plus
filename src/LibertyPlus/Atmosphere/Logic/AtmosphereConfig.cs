using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

#pragma warning disable 0649
namespace LibertyFramework.Atmosphere.Logic
{
    // M-2/M-3/E-5 atmosphere: weather director, cold breath, adaptive population density (config/atmosphere.json).
    [DataContract]
    internal sealed class AtmosphereConfig
    {
        [DataMember(Name = "schemaVersion", IsRequired = true)] internal int SchemaVersion;
        [DataMember(Name = "enabled", IsRequired = true)] internal bool Enabled;
        [DataMember(Name = "weather", IsRequired = true)] internal WeatherSettings Weather;
        [DataMember(Name = "breath", IsRequired = true)] internal BreathSettings Breath;
        [DataMember(Name = "density", IsRequired = true)] internal DensitySettings Density;

        // GTA IV weather ids = row order of timecyc.dat.
        internal static readonly string[] WeatherNames = { "EXTRASUNNY", "SUNNY", "SUNNY_WINDY", "CLOUDY", "RAIN", "DRIZZLE", "FOGGY", "LIGHTNING" };

        internal static int WeatherId(string name)
        {
            int id = Array.IndexOf(WeatherNames, name);
            if (id < 0) { throw new InvalidDataException("unknown weather " + name); }
            return id;
        }

        internal void Validate()
        {
            if (SchemaVersion != 1 || Weather == null || Breath == null || Density == null) { throw new InvalidDataException("atmosphere.json schema"); }
            Weather.Validate(); Breath.Validate(); Density.Validate();
        }
    }

    [DataContract]
    internal sealed class WeatherWeight
    {
        [DataMember(Name = "weather", IsRequired = true)] internal string Weather;
        [DataMember(Name = "weight", IsRequired = true)] internal double Weight;
    }

    [DataContract]
    internal sealed class WeatherSettings
    {
        [DataMember(Name = "enabled", IsRequired = true)] internal bool Enabled;
        [DataMember(Name = "blockMinimumHours", IsRequired = true)] internal int BlockMinimumHours;
        [DataMember(Name = "blockMaximumHours", IsRequired = true)] internal int BlockMaximumHours;
        [DataMember(Name = "dayWeights", IsRequired = true)] internal List<WeatherWeight> DayWeights;
        [DataMember(Name = "nightWeights", IsRequired = true)] internal List<WeatherWeight> NightWeights;
        [DataMember(Name = "nightStartHour", IsRequired = true)] internal int NightStartHour;
        [DataMember(Name = "nightEndHour", IsRequired = true)] internal int NightEndHour;
        [DataMember(Name = "forbiddenTransitions", IsRequired = true)] internal List<string> ForbiddenTransitions;
        [DataMember(Name = "releaseDuringMissions", IsRequired = true)] internal bool ReleaseDuringMissions;

        internal void Validate()
        {
            if (BlockMinimumHours < 1 || BlockMaximumHours < BlockMinimumHours || BlockMaximumHours > 24 ||
                NightStartHour < 0 || NightStartHour > 23 || NightEndHour < 0 || NightEndHour > 23 ||
                DayWeights == null || NightWeights == null || ForbiddenTransitions == null) { throw new InvalidDataException("atmosphere.weather bounds"); }
            foreach (WeatherWeight w in DayWeights) { AtmosphereConfig.WeatherId(w.Weather); if (w.Weight < 0) throw new InvalidDataException("negative weight"); }
            foreach (WeatherWeight w in NightWeights) { AtmosphereConfig.WeatherId(w.Weather); if (w.Weight < 0) throw new InvalidDataException("negative weight"); }
            foreach (string pair in ForbiddenTransitions)
            {
                string[] parts = pair.Split('>');
                if (parts.Length != 2) { throw new InvalidDataException("forbiddenTransitions entry " + pair); }
                AtmosphereConfig.WeatherId(parts[0]); AtmosphereConfig.WeatherId(parts[1]);
            }
        }

        internal bool IsNight(int hour)
        {
            return NightStartHour > NightEndHour ? hour >= NightStartHour || hour < NightEndHour : hour >= NightStartHour && hour < NightEndHour;
        }
    }

    [DataContract]
    internal sealed class BreathSettings
    {
        [DataMember(Name = "enabled", IsRequired = true)] internal bool Enabled;
        [DataMember(Name = "effectNames", IsRequired = true)] internal List<string> EffectNames;
        [DataMember(Name = "coldNightStartHour", IsRequired = true)] internal int ColdNightStartHour;
        [DataMember(Name = "coldNightEndHour", IsRequired = true)] internal int ColdNightEndHour;
        [DataMember(Name = "coldWeathers", IsRequired = true)] internal List<string> ColdWeathers;
        [DataMember(Name = "intervalMinimumMilliseconds", IsRequired = true)] internal int IntervalMinimumMilliseconds;
        [DataMember(Name = "intervalMaximumMilliseconds", IsRequired = true)] internal int IntervalMaximumMilliseconds;
        [DataMember(Name = "scale", IsRequired = true)] internal float Scale;
        [DataMember(Name = "nearbyPeds", IsRequired = true)] internal int NearbyPeds;
        [DataMember(Name = "nearbyRadiusMeters", IsRequired = true)] internal float NearbyRadiusMeters;
        [DataMember(Name = "scanIntervalMilliseconds", IsRequired = true)] internal int ScanIntervalMilliseconds;

        internal void Validate()
        {
            if (EffectNames == null || EffectNames.Count == 0 || ColdWeathers == null || IntervalMinimumMilliseconds < 500 ||
                IntervalMaximumMilliseconds < IntervalMinimumMilliseconds || Scale <= 0 || Scale > 4 || NearbyPeds < 0 || NearbyPeds > 8 ||
                NearbyRadiusMeters <= 0 || NearbyRadiusMeters > 30 || ScanIntervalMilliseconds < 200) { throw new InvalidDataException("atmosphere.breath bounds"); }
            foreach (string weather in ColdWeathers) { AtmosphereConfig.WeatherId(weather); }
        }

        // Cold = inside the cold-night hours, or a grey/wet weather at any time.
        internal bool IsCold(int hour, int weather)
        {
            bool night = ColdNightStartHour > ColdNightEndHour ? hour >= ColdNightStartHour || hour < ColdNightEndHour
                : hour >= ColdNightStartHour && hour < ColdNightEndHour;
            return night || (weather >= 0 && weather < AtmosphereConfig.WeatherNames.Length && ColdWeathers.Contains(AtmosphereConfig.WeatherNames[weather]));
        }
    }

    [DataContract]
    internal sealed class DensitySettings
    {
        [DataMember(Name = "enabled", IsRequired = true)] internal bool Enabled;
        [DataMember(Name = "fullDensityFrameMilliseconds", IsRequired = true)] internal double FullDensityFrameMilliseconds;
        [DataMember(Name = "minimumDensityFrameMilliseconds", IsRequired = true)] internal double MinimumDensityFrameMilliseconds;
        [DataMember(Name = "minimumPedDensity", IsRequired = true)] internal double MinimumPedDensity;
        [DataMember(Name = "minimumCarDensity", IsRequired = true)] internal double MinimumCarDensity;
        [DataMember(Name = "smoothingSeconds", IsRequired = true)] internal double SmoothingSeconds;
        [DataMember(Name = "maximumChangePerSecond", IsRequired = true)] internal double MaximumChangePerSecond;

        internal void Validate()
        {
            if (FullDensityFrameMilliseconds <= 0 || MinimumDensityFrameMilliseconds <= FullDensityFrameMilliseconds ||
                MinimumPedDensity < 0.1 || MinimumPedDensity > 1 || MinimumCarDensity < 0.1 || MinimumCarDensity > 1 ||
                SmoothingSeconds <= 0 || SmoothingSeconds > 30 || MaximumChangePerSecond <= 0 || MaximumChangePerSecond > 2)
            { throw new InvalidDataException("atmosphere.density bounds"); }
        }
    }
}
