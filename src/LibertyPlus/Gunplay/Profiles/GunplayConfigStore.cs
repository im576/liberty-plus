using System;
using System.Collections.Generic;
using System.IO;
using LibertyFramework.Core.Config;

namespace LibertyFramework.Gunplay.Profiles
{
    // Owns the live gunplay config: polls gunplay.json for edits, keeps the last valid copy on
    // errors, applies presets, and saves live-tuned values back with a .bak of the old file.
    internal sealed class GunplayConfigStore
    {
        private readonly string path;
        private readonly Action<string> info;
        private readonly Action<string> error;
        private string lastHash;

        internal GunplayConfigStore(string path, Action<string> info, Action<string> error)
        {
            this.path = path;
            this.info = info;
            this.error = error;
        }

        internal GunplayConfig Active { get; private set; }
        internal string ActivePresetName { get; private set; }
        internal int Revision { get; private set; }
        internal string LastError { get; private set; }

        // Returns true when a new valid config became active.
        internal bool Poll(bool force)
        {
            try
            {
                if (!File.Exists(path))
                {
                    if (lastHash != "missing") { lastHash = "missing"; Fail("gunplay_config_missing path=" + path); }
                    return false;
                }
                byte[] data = JsonStore.ReadBytes(path);
                string hash = JsonStore.Hash(data);
                if (!force && hash == lastHash) { return false; }
                lastHash = hash;
                GunplayConfig candidate = JsonStore.Parse<GunplayConfig>(data);
                GunplayConfigValidator.Validate(candidate);
                Active = candidate;
                ActivePresetName = "gunplay.json";
                Revision++;
                LastError = null;
                info("gunplay_config_loaded path=" + path + " weapons=" + candidate.Weapons.Count + " revision=" + Revision);
                return true;
            }
            catch (Exception exception)
            {
                Fail("gunplay_config_rejected path=" + path + " kept_revision=" + Revision + " error=" + exception.Message);
                return false;
            }
        }

        internal bool ApplyPreset(string presetPath)
        {
            if (Active == null) { return false; }
            try
            {
                PresetFile preset = JsonStore.Load<PresetFile>(presetPath);
                if (preset.SchemaVersion != 1 || preset.Weapons == null) { throw new InvalidDataException("preset schemaVersion must be 1 with weapons"); }
                List<string> errors = new List<string>();
                foreach (PresetWeapon weapon in preset.Weapons)
                {
                    GunplayConfigValidator.ValidateProfiles(errors, "preset weapon " + weapon.WeaponId, weapon.Recoil, weapon.Spread);
                    if (Active.FindWeapon(weapon.WeaponId) == null) { errors.Add("preset weapon " + weapon.WeaponId + " is not in gunplay.json"); }
                }
                if (errors.Count > 0) { throw new InvalidDataException(string.Join("; ", errors.ToArray())); }
                foreach (PresetWeapon weapon in preset.Weapons)
                {
                    WeaponProfile target = Active.FindWeapon(weapon.WeaponId);
                    target.Recoil = weapon.Recoil;
                    target.Spread = weapon.Spread;
                    target.ProfileName = weapon.ProfileName;
                }
                ActivePresetName = preset.Name;
                Revision++;
                LastError = null;
                info("preset_loaded name=" + preset.Name + " path=" + presetPath + " revision=" + Revision);
                return true;
            }
            catch (Exception exception)
            {
                Fail("preset_rejected path=" + presetPath + " error=" + exception.Message);
                return false;
            }
        }

        internal bool SavePreset(string presetPath, string name, string description)
        {
            if (Active == null) { return false; }
            try
            {
                PresetFile preset = new PresetFile();
                preset.SchemaVersion = 1;
                preset.Name = name;
                preset.Description = description;
                preset.Weapons = new List<PresetWeapon>();
                foreach (WeaponProfile weapon in Active.Weapons)
                {
                    PresetWeapon entry = new PresetWeapon();
                    entry.WeaponId = weapon.WeaponId;
                    entry.ProfileName = weapon.ProfileName;
                    entry.Recoil = weapon.Recoil.Clone();
                    entry.Spread = weapon.Spread.Clone();
                    preset.Weapons.Add(entry);
                }
                JsonStore.Save(presetPath, preset);
                info("preset_saved name=" + name + " path=" + presetPath);
                return true;
            }
            catch (Exception exception)
            {
                Fail("preset_save_failed path=" + presetPath + " error=" + exception.Message);
                return false;
            }
        }

        // Writes live values into gunplay.json (previous file kept as gunplay.json.bak).
        internal bool SaveActiveToConfig()
        {
            if (Active == null) { return false; }
            try
            {
                GunplayConfigValidator.Validate(Active);
                JsonStore.Save(path, Active);
                lastHash = JsonStore.Hash(JsonStore.ReadBytes(path));
                ActivePresetName = "gunplay.json";
                info("gunplay_config_saved path=" + path);
                return true;
            }
            catch (Exception exception)
            {
                Fail("gunplay_config_save_failed error=" + exception.Message);
                return false;
            }
        }

        internal void MarkLiveEdit()
        {
            Revision++;
            ActivePresetName = "live-edited";
        }

        private void Fail(string message)
        {
            LastError = message;
            error(message);
        }
    }
}
