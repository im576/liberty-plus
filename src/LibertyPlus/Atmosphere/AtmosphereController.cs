using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using GTA;
using GTA.Native;
using LibertyFramework.Core.Config;
using LibertyFramework.Core.Logging;
using LibertyFramework.Core.Performance.Logic;
using LibertyFramework.GameApi;
using LibertyFramework.Atmosphere.Logic;

namespace LibertyFramework.Atmosphere
{
    // Atmosphere (M-2 weather director, M-3 cold breath, E-5 adaptive density), config/atmosphere.json.
    // - Weather: blocks of a few in-game hours drawn from grey/wet-heavy weights; transitions that would look wrong
    //   (clear sky straight into a storm) are excluded. Missions own the weather: we release it while one runs.
    // - Breath: on cold nights and grey/wet days the player and a few nearby pedestrians exhale vapour puffs.
    // - Density: ped/car density eases down when the frame time rises and back up when it recovers.
    [global::Liberty.Sdk.Module("atmosphere", Order = 50, Capabilities = new[] { global::Liberty.Sdk.Capabilities.EngineInternal }, Description = "Atmosphere: breath, weather and mood")]
    public sealed class AtmosphereController : LibertyFramework.Engine.Module
    {
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Random random = new Random();
        private AtmosphereConfig config;
        private DateTime lastConfigCheckUtc;
        private string configHash;
        private bool disabled;
        private long lastTickMilliseconds;
        private readonly DensityGovernor governor = new DensityGovernor();
        private long lastDensityLogMilliseconds;

        private WeatherForecast forecast;
        private int forcedWeather = -1;
        private int lastHour = -1;
        private int hoursLeftInBlock;
        private bool forcing;
        private long lastWeatherCheckMilliseconds;
        private int currentWeather = -1;
        private int currentHour = 12;

        private int breathEffectIndex;
        private bool breathUnavailable;
        private long nextPlayerBreath;
        private long lastBreathScan;
        private readonly Dictionary<Ped, long> breathers = new Dictionary<Ped, long>();

        public AtmosphereController()
        {
            Interval = 0;
            forecast = new WeatherForecast(Environment.TickCount);
            LoadConfig();
            Tick += OnTick;
            RuntimeLog.Info("atmosphere_started");
        }

        private void LoadConfig()
        {
            if ((DateTime.UtcNow - lastConfigCheckUtc).TotalMilliseconds < 1000) { return; }
            lastConfigCheckUtc = DateTime.UtcNow;
            try
            {
                byte[] bytes = JsonStore.ReadBytes(LibertyPaths.AtmosphereConfig);
                string hash = JsonStore.Hash(bytes);
                if (hash == configHash) { return; }
                AtmosphereConfig candidate = JsonStore.Parse<AtmosphereConfig>(bytes);
                candidate.Validate();
                config = candidate;
                configHash = hash;
                RuntimeLog.Info("atmosphere_config_loaded weather=" + config.Weather.Enabled + " breath=" + config.Breath.Enabled + " density=" + config.Density.Enabled);
            }
            catch (Exception error) { RuntimeLog.Error("atmosphere_config_rejected error=" + error.Message); }
        }

        private void OnTick(object sender, EventArgs args)
        {
            if (disabled) { return; }
            long started = Stopwatch.GetTimestamp();
            try
            {
                LoadConfig();
                if (config == null || !config.Enabled) { return; }
                long now = clock.ElapsedMilliseconds;
                double deltaSeconds = lastTickMilliseconds == 0 ? 0 : (now - lastTickMilliseconds) / 1000.0;
                double frameMilliseconds = now - lastTickMilliseconds;
                lastTickMilliseconds = now;
                Player player = Player;
                Ped ped = player != null ? player.Character : null;
                if (ped == null || !Natives.PedExists(ped)) { return; }

                if (config.Density.Enabled && deltaSeconds > 0) { UpdateDensity(frameMilliseconds, deltaSeconds, now); }
                if (now - lastWeatherCheckMilliseconds >= 1000)
                {
                    lastWeatherCheckMilliseconds = now;
                    ReadClockAndWeather();
                    if (config.Weather.Enabled) { UpdateWeather(); }
                }
                if (config.Breath.Enabled && !breathUnavailable) { UpdateBreath(ped, now); }
            }
            catch (Exception error)
            {
                RuntimeLog.Error("feature_disabled atmosphere error=" + error);
                disabled = true;
                try { Release(); } catch (Exception restore) { RuntimeLog.Error("atmosphere_release_failed error=" + restore.Message); }
            }
            finally { CostMeter.Add("tick.atmosphere", started); }
        }

        // SET_*_DENSITY_MULTIPLIER only lasts for the current frame, so it is sent every tick (one tick per frame).
        private void UpdateDensity(double frameMilliseconds, double deltaSeconds, long now)
        {
            governor.Observe(config.Density, frameMilliseconds, deltaSeconds);
            Function.Call("SET_PED_DENSITY_MULTIPLIER", (float)governor.PedDensity);
            Function.Call("SET_CAR_DENSITY_MULTIPLIER", (float)governor.CarDensity);
            if (now - lastDensityLogMilliseconds >= 30000)
            {
                lastDensityLogMilliseconds = now;
                RuntimeLog.Info("density frame_ms=" + governor.SmoothedFrameMilliseconds.ToString("0.0") + " peds=" + governor.PedDensity.ToString("0.00") +
                    " cars=" + governor.CarDensity.ToString("0.00"));
            }
        }

        private void ReadClockAndWeather()
        {
            Pointer hour = typeof(int), minute = typeof(int), weather = typeof(int);
            Function.Call("GET_TIME_OF_DAY", hour, minute);
            Function.Call("GET_CURRENT_WEATHER", weather);
            currentHour = (int)hour;
            currentWeather = (int)weather;
        }

        private void UpdateWeather()
        {
            if (config.Weather.ReleaseDuringMissions && Function.Call<bool>("GET_MISSION_FLAG"))
            {
                if (forcing) { Release(); RuntimeLog.Info("weather_released reason=mission"); }
                lastHour = -1;
                return;
            }
            if (lastHour < 0)
            {
                // First look (or after a mission): keep what the sky shows now for one block, then take over.
                lastHour = currentHour;
                forcedWeather = currentWeather;
                hoursLeftInBlock = forecast.BlockHours(config.Weather);
                return;
            }
            if (currentHour != lastHour)
            {
                lastHour = currentHour;
                hoursLeftInBlock--;
            }
            if (hoursLeftInBlock > 0) { return; }
            int next = forecast.Next(config.Weather, forcedWeather >= 0 ? forcedWeather : currentWeather, currentHour);
            hoursLeftInBlock = forecast.BlockHours(config.Weather);
            Function.Call("FORCE_WEATHER", next);
            forcing = true;
            RuntimeLog.Info("weather_block from=" + WeatherName(forcedWeather) + " to=" + WeatherName(next) + " hours=" + hoursLeftInBlock + " at=" + currentHour + "h");
            forcedWeather = next;
        }

        private static string WeatherName(int weather)
        {
            return weather >= 0 && weather < AtmosphereConfig.WeatherNames.Length ? AtmosphereConfig.WeatherNames[weather] : "?";
        }

        private void Release()
        {
            if (!forcing) { return; }
            Function.Call("RELEASE_WEATHER");
            forcing = false;
        }

        private void UpdateBreath(Ped player, long now)
        {
            if (!config.Breath.IsCold(currentHour, currentWeather)) { breathers.Clear(); return; }
            if (now >= nextPlayerBreath)
            {
                nextPlayerBreath = now + NextInterval();
                if (!Natives.IsInAnyCar(player)) { Breathe(player); }
            }
            if (config.Breath.NearbyPeds <= 0) { return; }
            if (now - lastBreathScan >= config.Breath.ScanIntervalMilliseconds)
            {
                lastBreathScan = now;
                Dictionary<Ped, long> keep = new Dictionary<Ped, long>();
                foreach (Ped ped in World.GetPeds(player.Position, config.Breath.NearbyRadiusMeters))
                {
                    if (keep.Count >= config.Breath.NearbyPeds) { break; }
                    if (ped == null || ped == player || !Natives.PedExists(ped) || Natives.PedDead(ped) || Natives.IsInAnyCar(ped)) { continue; }
                    long next;
                    keep[ped] = breathers.TryGetValue(ped, out next) ? next : now + random.Next(0, config.Breath.IntervalMaximumMilliseconds);
                }
                breathers.Clear();
                foreach (KeyValuePair<Ped, long> pair in keep) { breathers[pair.Key] = pair.Value; }
            }
            foreach (Ped ped in new List<Ped>(breathers.Keys))
            {
                if (now < breathers[ped]) { continue; }
                breathers[ped] = now + NextInterval();
                if (Natives.PedExists(ped)) { Breathe(ped); }
            }
        }

        private int NextInterval()
        {
            return random.Next(config.Breath.IntervalMinimumMilliseconds, config.Breath.IntervalMaximumMilliseconds + 1);
        }

        // Head bone; the stock breath effects are authored around the mouth. The first effect the engine accepts is
        // kept; if none play (TRIGGER refuses looping effects), breath is switched off and logged once.
        private void Breathe(Ped ped)
        {
            while (breathEffectIndex < config.Breath.EffectNames.Count)
            {
                string effect = config.Breath.EffectNames[breathEffectIndex];
                bool ok = Function.Call<bool>("TRIGGER_PTFX_ON_PED_BONE", effect, ped, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0x4B5, config.Breath.Scale);
                if (ok) { return; }
                RuntimeLog.Info("breath_effect_refused effect=" + effect);
                breathEffectIndex++;
            }
            breathUnavailable = true;
            RuntimeLog.Error("breath_unavailable no configured effect plays as a one-shot");
        }


    }
}
