using System;
using System.IO;
using LibertyFramework.Atmosphere.Logic;
using LibertyFramework.Core.Config;

namespace LibertyFramework.Verify
{
    // M-2 / E-5: weather forecast and density governor logic, and the shipped atmosphere.json.
    internal static class AtmosphereChecks
    {
        internal static void Run(string repoRoot, Checker check)
        {
            AtmosphereConfig config = JsonStore.Load<AtmosphereConfig>(Path.Combine(repoRoot, Path.Combine("config", "atmosphere.json")));
            config.Validate();
            check.True("atmosphere.json validates with owner-required density thinning disabled", config.Enabled && config.Weather.Enabled && config.Breath.Enabled && !config.Density.Enabled, "");

            WeatherForecast forecast = new WeatherForecast(1234);
            int[] counts = new int[AtmosphereConfig.WeatherNames.Length];
            int current = AtmosphereConfig.WeatherId("SUNNY"), forbidden = 0, draws = 20000;
            for (int i = 0; i < draws; i++)
            {
                int next = forecast.Next(config.Weather, current, 13);
                if (WeatherForecast.Forbidden(config.Weather, current, next)) { forbidden++; }
                counts[next]++;
                current = next;
            }
            int greyWet = counts[AtmosphereConfig.WeatherId("CLOUDY")] + counts[AtmosphereConfig.WeatherId("RAIN")] +
                counts[AtmosphereConfig.WeatherId("DRIZZLE")] + counts[AtmosphereConfig.WeatherId("FOGGY")] + counts[AtmosphereConfig.WeatherId("LIGHTNING")];
            check.True("forecast never draws a forbidden transition", forbidden == 0, "forbidden=" + forbidden);
            check.True("daytime is mostly grey or wet (>= 70% of blocks)", greyWet >= draws * 0.70, "grey_wet=" + greyWet + "/" + draws);
            check.True("clear skies still happen by day", counts[AtmosphereConfig.WeatherId("SUNNY")] > 0, "");
            int nightFog = 0;
            for (int i = 0; i < draws; i++) { if (forecast.Next(config.Weather, AtmosphereConfig.WeatherId("CLOUDY"), 2) == AtmosphereConfig.WeatherId("FOGGY")) nightFog++; }
            check.True("fog is more likely at night than by day", nightFog > counts[AtmosphereConfig.WeatherId("FOGGY")], "night_fog=" + nightFog);
            check.True("cold breath at 2h in clear weather and in daytime drizzle, not on a sunny afternoon",
                config.Breath.IsCold(2, AtmosphereConfig.WeatherId("SUNNY")) && config.Breath.IsCold(14, AtmosphereConfig.WeatherId("DRIZZLE")) &&
                !config.Breath.IsCold(14, AtmosphereConfig.WeatherId("SUNNY")), "");

            DensityGovernor governor = new DensityGovernor();
            for (int i = 0; i < 300; i++) { governor.Observe(config.Density, 18, 1.0 / 60); }
            check.True("density stays full at 18 ms frames", governor.PedDensity == 1 && governor.CarDensity == 1, "peds=" + governor.PedDensity);
            for (int i = 0; i < 60 * 30; i++) { governor.Observe(config.Density, 60, 1.0 / 20); }
            check.True("density reaches its minimum at 60 ms frames", Math.Abs(governor.PedDensity - config.Density.MinimumPedDensity) < 1e-6 &&
                Math.Abs(governor.CarDensity - config.Density.MinimumCarDensity) < 1e-6, "peds=" + governor.PedDensity + " cars=" + governor.CarDensity);
            DensityGovernor rate = new DensityGovernor();
            rate.Observe(config.Density, 18, 0.1);
            for (int i = 0; i < 10; i++) { rate.Observe(config.Density, 80, 0.1); }
            check.True("density never drops faster than the configured rate", 1 - rate.PedDensity <= config.Density.MaximumChangePerSecond * 1.0 + 1e-9, "after_1s=" + rate.PedDensity);
        }
    }
}
