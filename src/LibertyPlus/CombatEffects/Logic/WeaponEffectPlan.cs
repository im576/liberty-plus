using System;
using System.Collections.Generic;

namespace LibertyFramework.CombatEffects.Logic
{
    // The effect set one weapon resolves to: its class's set with the weapon's own scale overrides applied.
    internal sealed class WeaponEffectSet
    {
        internal int WeaponId;
        internal WeaponEffectClass Class;
        internal float MuzzleScale;
        internal float LightScale;
    }

    // Pure decisions of the weapon effects (T-048): which set a weapon uses, when it is night, when a shooter's sustained
    // fire has earned smoke, and which muzzle lights are still lit. No game calls; the offline verifier tests all of it.
    internal static class WeaponEffectPlan
    {
        internal static Dictionary<int, WeaponEffectSet> Build(WeaponEffectsConfig config)
        {
            Dictionary<string, WeaponEffectClass> classes = new Dictionary<string, WeaponEffectClass>();
            foreach (WeaponEffectClass weaponClass in config.Classes) classes[weaponClass.Name] = weaponClass;
            Dictionary<int, WeaponEffectSet> sets = new Dictionary<int, WeaponEffectSet>();
            foreach (WeaponEffectWeapon weapon in config.Weapons)
            {
                WeaponEffectClass weaponClass = classes[weapon.ClassName];
                WeaponEffectSet set = new WeaponEffectSet();
                set.WeaponId = weapon.WeaponId;
                set.Class = weaponClass;
                set.MuzzleScale = weapon.MuzzleScale > 0 ? weapon.MuzzleScale : weaponClass.MuzzleScale;
                set.LightScale = weapon.LightScale > 0 ? weapon.LightScale : weaponClass.LightScale;
                sets[weapon.WeaponId] = set;
            }
            return sets;
        }

        // Night is startHour inclusive to endHour exclusive and may wrap midnight (20 to 6: 20:00-05:59).
        internal static bool IsNight(int hour, int startHour, int endHour)
        {
            if (startHour == endHour) return false;
            return startHour < endHour ? hour >= startHour && hour < endHour : hour >= startHour || hour < endHour;
        }

        // Which entity kind's impact list a hit uses (0 world, 1 ped, 2 vehicle, 3 object = RayEntityKind values); null = none.
        internal static string[] ImpactEffects(WeaponEffectClass weaponClass, int kind)
        {
            switch (kind)
            {
                case 0: return weaponClass.ImpactWorld;
                case 2: return weaponClass.ImpactVehicle;
                case 3: return weaponClass.ImpactObject;
                default: return null; // peds: the gore module owns them
            }
        }
    }

    // Counts a shooter's recent shots: smoke starts once a shooter fired afterShots shots inside the window, then waits for the cooldown.
    internal sealed class ShotHeat
    {
        private sealed class Heat
        {
            internal readonly List<long> Shots = new List<long>();
            internal long CooldownUntil;
        }

        private readonly Dictionary<int, Heat> shooters = new Dictionary<int, Heat>();

        internal int Tracked { get { return shooters.Count; } }

        internal bool RecordShot(int shooter, long now, int afterShots, int windowMilliseconds, int cooldownMilliseconds)
        {
            Heat heat;
            if (!shooters.TryGetValue(shooter, out heat)) { heat = new Heat(); shooters[shooter] = heat; }
            heat.Shots.Add(now);
            heat.Shots.RemoveAll(shot => now - shot > windowMilliseconds);
            if (heat.Shots.Count < afterShots || now < heat.CooldownUntil) return false;
            heat.CooldownUntil = now + cooldownMilliseconds;
            heat.Shots.Clear();
            return true;
        }

        // Forgets shooters that have not fired for a while (a long-lived module must not keep every ped it ever saw).
        internal void Prune(long now, int idleMilliseconds)
        {
            List<int> gone = null;
            foreach (KeyValuePair<int, Heat> pair in shooters)
            {
                long last = pair.Value.Shots.Count > 0 ? pair.Value.Shots[pair.Value.Shots.Count - 1] : pair.Value.CooldownUntil;
                if (now - last > idleMilliseconds && now >= pair.Value.CooldownUntil) (gone = gone ?? new List<int>()).Add(pair.Key);
            }
            if (gone != null) foreach (int key in gone) shooters.Remove(key);
        }

        internal void Clear() { shooters.Clear(); }
    }

    // Muzzle lights: a pulse is lit for durationMilliseconds, fading linearly; at most 'maximum' at once, the oldest replaced.
    internal sealed class LightPulses
    {
        internal sealed class Pulse
        {
            internal float X, Y, Z;
            internal float Scale;
            internal long Start;
            internal long End;
            internal string SourceClass;
            internal bool DrawReported;
        }

        private readonly List<Pulse> pulses = new List<Pulse>();

        internal int Count { get { return pulses.Count; } }
        internal int Replaced;

        internal bool Add(float x, float y, float z, float scale, long now, int durationMilliseconds, int maximum, string sourceClass = null)
        {
            if (maximum <= 0) return false;
            while (pulses.Count >= maximum) { pulses.RemoveAt(0); Replaced++; }
            Pulse pulse = new Pulse();
            pulse.X = x; pulse.Y = y; pulse.Z = z; pulse.Scale = scale; pulse.Start = now; pulse.End = now + durationMilliseconds;
            pulse.SourceClass = sourceClass;
            pulses.Add(pulse);
            return true;
        }

        // The lights to draw this frame with their brightness fraction (1 at the start, 0 at the end); expired pulses are dropped.
        internal void Active(long now, List<KeyValuePair<Pulse, float>> lit)
        {
            lit.Clear();
            for (int i = pulses.Count - 1; i >= 0; i--)
            {
                if (now >= pulses[i].End) { pulses.RemoveAt(i); continue; }
                float fraction = 1.0f - (float)(now - pulses[i].Start) / (float)Math.Max(1, pulses[i].End - pulses[i].Start);
                lit.Add(new KeyValuePair<Pulse, float>(pulses[i], Math.Max(0f, Math.Min(1f, fraction))));
            }
        }

        internal void Clear() { pulses.Clear(); }
    }
}
