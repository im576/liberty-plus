using System.IO;
using System.Runtime.Serialization;

#pragma warning disable 0649
namespace LibertyFramework.CombatEffects
{
    // config/weapon_effects.json (T-048): the contextual effect set of every weapon. A weapon takes its class's set and may
    // override the scales. Effect names are stock particle effects (docs/research/ParticleEffects.md lists them).
    [DataContract]
    internal sealed class WeaponEffectsConfig
    {
        [DataMember(Name="schemaVersion", IsRequired=true)] internal int SchemaVersion;
        [DataMember(Name="enabled", IsRequired=true)] internal bool Enabled;
        // Shots by shooters farther than this from the player produce nothing; at most maximumShotsPerFrame get effects per frame.
        [DataMember(Name="maximumDistanceMeters", IsRequired=true)] internal float MaximumDistanceMeters;
        [DataMember(Name="maximumShotsPerFrame", IsRequired=true)] internal int MaximumShotsPerFrame;
        // Impact tests (one raycast each, player-relative) per frame and how far from the player a bullet's end may be.
        [DataMember(Name="maximumImpactTestsPerFrame", IsRequired=true)] internal int MaximumImpactTestsPerFrame;
        [DataMember(Name="impactMaximumDistanceMeters", IsRequired=true)] internal float ImpactMaximumDistanceMeters;
        [DataMember(Name="effectLifetimeMilliseconds", IsRequired=true)] internal int EffectLifetimeMilliseconds;
        [DataMember(Name="smokeDurationMilliseconds", IsRequired=true)] internal int SmokeDurationMilliseconds;
        [DataMember(Name="smokeOffsetMeters", IsRequired=true)] internal float SmokeOffsetMeters;
        [DataMember(Name="impactRayExtensionMeters", IsRequired=true)] internal float ImpactRayExtensionMeters;
        // Share of the shared active-effect cap (combat_effects.json maximumActiveEffects) the weapon effects may hold at most. The rest is kept
        // for gore: T-047 measured severe head/trauma effects dropping to half when muzzle effects filled the whole cap.
        [DataMember(Name="maximumEffectShare", IsRequired=false)] internal float MaximumEffectShare;

        [OnDeserializing]
        private void ApplyDefaults(StreamingContext context) { MaximumEffectShare = 0.5f; }
        [DataMember(Name="night", IsRequired=true)] internal NightConfig Night;
        [DataMember(Name="light", IsRequired=true)] internal LightConfig Light;
        [DataMember(Name="classes", IsRequired=true)] internal WeaponEffectClass[] Classes;
        [DataMember(Name="weapons", IsRequired=true)] internal WeaponEffectWeapon[] Weapons;

        internal void Validate()
        {
            if (SchemaVersion != 1 || MaximumDistanceMeters < 5 || MaximumDistanceMeters > 500 || MaximumShotsPerFrame < 1 || MaximumShotsPerFrame > 64 ||
                MaximumImpactTestsPerFrame < 0 || MaximumImpactTestsPerFrame > 16 || ImpactMaximumDistanceMeters < 5 || ImpactMaximumDistanceMeters > 200 ||
                EffectLifetimeMilliseconds < 16 || EffectLifetimeMilliseconds > 5000 || SmokeDurationMilliseconds < 16 || SmokeDurationMilliseconds > 5000 ||
                MaximumEffectShare < 0.05f || MaximumEffectShare > 1f || SmokeOffsetMeters < 0 || SmokeOffsetMeters > 2 || ImpactRayExtensionMeters < 0 || ImpactRayExtensionMeters > 2 ||
                Night == null || Light == null || Classes == null || Classes.Length == 0 || Classes.Length > 16 || Weapons == null || Weapons.Length == 0)
                throw new InvalidDataException("weapon_effects.json invalid bounds");
            if (Night.StartHour < 0 || Night.StartHour > 23 || Night.EndHour < 0 || Night.EndHour > 23)
                throw new InvalidDataException("weapon_effects.json night hours must be 0-23");
            if (Light.Maximum < 0 || Light.Maximum > 8 || Light.MaximumDistanceMeters < 5 || Light.MaximumDistanceMeters > 200 ||
                Light.DurationMilliseconds < 16 || Light.DurationMilliseconds > 500 || Light.Range <= 0 || Light.Range > 40 || Light.Intensity <= 0 || Light.Intensity > 40 ||
                Light.Red < 0 || Light.Red > 255 || Light.Green < 0 || Light.Green > 255 || Light.Blue < 0 || Light.Blue > 255)
                throw new InvalidDataException("weapon_effects.json light bounds invalid");
            System.Collections.Generic.HashSet<string> names = new System.Collections.Generic.HashSet<string>();
            foreach (WeaponEffectClass weaponClass in Classes)
            {
                if (weaponClass == null || string.IsNullOrEmpty(weaponClass.Name) || !names.Add(weaponClass.Name) || weaponClass.MuzzleEffects == null || weaponClass.SmokeEffects == null ||
                    weaponClass.ImpactVehicle == null || weaponClass.ImpactWorld == null || weaponClass.ImpactObject == null ||
                    weaponClass.MuzzleScale <= 0 || weaponClass.MuzzleScale > 5 || weaponClass.SmokeScale <= 0 || weaponClass.SmokeScale > 5 ||
                    weaponClass.LightScale < 0 || weaponClass.LightScale > 5 || weaponClass.ImpactScale <= 0 || weaponClass.ImpactScale > 5 ||
                    weaponClass.SmokeAfterShots < 1 || weaponClass.SmokeAfterShots > 100 || weaponClass.SmokeWindowMilliseconds < 100 || weaponClass.SmokeWindowMilliseconds > 60000 ||
                    weaponClass.SmokeCooldownMilliseconds < 0 || weaponClass.SmokeCooldownMilliseconds > 60000)
                    throw new InvalidDataException("weapon_effects.json class entry invalid");
            }
            System.Collections.Generic.HashSet<int> ids = new System.Collections.Generic.HashSet<int>();
            foreach (WeaponEffectWeapon weapon in Weapons)
            {
                if (weapon == null || weapon.WeaponId < 0 || weapon.WeaponId > 255 || !ids.Add(weapon.WeaponId) || !names.Contains(weapon.ClassName) ||
                    weapon.MuzzleScale < 0 || weapon.MuzzleScale > 5 || weapon.LightScale < 0 || weapon.LightScale > 5)
                    throw new InvalidDataException("weapon_effects.json weapon entry invalid (unknown class or duplicate id)");
            }
        }
    }

    [DataContract]
    internal sealed class NightConfig
    {
        // Night is startHour (inclusive) to endHour (exclusive), wrapping midnight: 20 to 6 = 20:00-05:59.
        [DataMember(Name="startHour", IsRequired=true)] internal int StartHour;
        [DataMember(Name="endHour", IsRequired=true)] internal int EndHour;
    }

    [DataContract]
    internal sealed class LightConfig
    {
        [DataMember(Name="enabled", IsRequired=true)] internal bool Enabled;
        [DataMember(Name="maximum", IsRequired=true)] internal int Maximum;
        [DataMember(Name="maximumDistanceMeters", IsRequired=true)] internal float MaximumDistanceMeters;
        [DataMember(Name="durationMilliseconds", IsRequired=true)] internal int DurationMilliseconds;
        [DataMember(Name="red", IsRequired=true)] internal int Red;
        [DataMember(Name="green", IsRequired=true)] internal int Green;
        [DataMember(Name="blue", IsRequired=true)] internal int Blue;
        [DataMember(Name="range", IsRequired=true)] internal float Range;
        [DataMember(Name="intensity", IsRequired=true)] internal float Intensity;
    }

    [DataContract]
    internal sealed class WeaponEffectClass
    {
        [DataMember(Name="name", IsRequired=true)] internal string Name;
        [DataMember(Name="muzzleEffects", IsRequired=true)] internal string[] MuzzleEffects;
        [DataMember(Name="muzzleScale", IsRequired=true)] internal float MuzzleScale;
        [DataMember(Name="smokeEffects", IsRequired=true)] internal string[] SmokeEffects;
        [DataMember(Name="smokeAfterShots", IsRequired=true)] internal int SmokeAfterShots;
        [DataMember(Name="smokeWindowMilliseconds", IsRequired=true)] internal int SmokeWindowMilliseconds;
        [DataMember(Name="smokeCooldownMilliseconds", IsRequired=true)] internal int SmokeCooldownMilliseconds;
        [DataMember(Name="smokeScale", IsRequired=true)] internal float SmokeScale;
        [DataMember(Name="lightScale", IsRequired=true)] internal float LightScale;
        [DataMember(Name="impactScale", IsRequired=true)] internal float ImpactScale;
        // Extra impact effects by what the bullet hit. The game already draws material impacts itself; these add class-scaled
        // character where the surface kind is known. World/object stay empty until T-050 (R2) can name the material.
        [DataMember(Name="impactVehicle", IsRequired=true)] internal string[] ImpactVehicle;
        [DataMember(Name="impactWorld", IsRequired=true)] internal string[] ImpactWorld;
        [DataMember(Name="impactObject", IsRequired=true)] internal string[] ImpactObject;
    }

    [DataContract]
    internal sealed class WeaponEffectWeapon
    {
        [DataMember(Name="weaponId", IsRequired=true)] internal int WeaponId;
        [DataMember(Name="class", IsRequired=true)] internal string ClassName;
        // 0 = the class value.
        [DataMember(Name="muzzleScale", IsRequired=false)] internal float MuzzleScale;
        [DataMember(Name="lightScale", IsRequired=false)] internal float LightScale;
    }
}
