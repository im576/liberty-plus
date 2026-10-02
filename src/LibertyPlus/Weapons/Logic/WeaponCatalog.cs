using LibertyFramework.GameApi;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

#pragma warning disable 0649
namespace LibertyFramework.Weapons.Logic
{
    [DataContract]
    internal sealed class WeaponCatalog
    {
        [DataMember(Name = "schemaVersion", IsRequired = true)] internal int SchemaVersion;
        [DataMember(Name = "entries", IsRequired = true)] internal List<WeaponCatalogEntry> Entries;
        [DataMember(Name = "attachmentOptions", IsRequired = false)] internal List<AttachmentOption> AttachmentOptions;
        // Stage 1 (T-041), optional: rarity tiers, the classes that are never normal availability, and whether
        // the packager writes the entries' stats into WeaponInfo.xml (false = the game's own values stay).
        [DataMember(Name = "tiers", IsRequired = false)] internal List<WeaponTier> Tiers;
        [DataMember(Name = "restrictedClasses", IsRequired = false)] internal List<string> RestrictedClasses;
        [DataMember(Name = "applyWeaponInfoStats", IsRequired = false)] internal bool ApplyWeaponInfoStats;

        internal WeaponCatalogEntry Find(int weaponId)
        {
            foreach (WeaponCatalogEntry entry in Entries) { if (entry.WeaponId == weaponId) { return entry; } }
            return null;
        }

        internal WeaponCatalogEntry FindById(string id)
        {
            foreach (WeaponCatalogEntry entry in Entries) { if (string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase)) { return entry; } }
            return null;
        }

        // The stats WeaponInfo.xml must hold for an entry under this catalog: the Stage 1 values, or the game's own when the switch is off.
        internal WeaponStats ExpectedStats(WeaponCatalogEntry entry) { return ApplyWeaponInfoStats ? entry.Stats : entry.VanillaStats; }

        internal WeaponTier FindTier(string id)
        {
            if (Tiers == null || id == null) { return null; }
            foreach (WeaponTier tier in Tiers) { if (tier.Id == id) { return tier; } }
            return null;
        }

        internal bool IsRestrictedClass(string weaponClass)
        {
            return RestrictedClasses != null && weaponClass != null && RestrictedClasses.Contains(weaponClass);
        }

        // The Stage 1 arsenal in catalog order.
        internal List<WeaponCatalogEntry> Stage1Entries()
        {
            List<WeaponCatalogEntry> result = new List<WeaponCatalogEntry>();
            foreach (WeaponCatalogEntry entry in Entries) { if (entry.Stage1) { result.Add(entry); } }
            return result;
        }

        internal AttachmentOption FindAttachment(string id)
        {
            if (AttachmentOptions == null) { return null; }
            foreach (AttachmentOption option in AttachmentOptions) { if (option.Id == id) { return option; } }
            return null;
        }

        internal void Validate()
        {
            if (SchemaVersion != 1 || Entries == null) { throw new InvalidOperationException("Invalid weapon catalog schema."); }
            Dictionary<int, bool> ids = new Dictionary<int, bool>();
            Dictionary<string, bool> catalogIds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (WeaponCatalogEntry entry in Entries)
            {
                if (entry.WeaponId <= 0 || string.IsNullOrEmpty(entry.Id) || string.IsNullOrEmpty(entry.Family) ||
                    entry.Finishes == null || entry.Finishes.Count == 0 || entry.Attachments == null || ids.ContainsKey(entry.WeaponId) ||
                    catalogIds.ContainsKey(entry.Id))
                    { throw new InvalidOperationException("Invalid or duplicate weapon catalog entry."); }
                ids.Add(entry.WeaponId, true);
                catalogIds.Add(entry.Id, true);
            }
            if (AttachmentOptions != null)
            {
                Dictionary<string, bool> optionIds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                foreach (AttachmentOption option in AttachmentOptions)
                {
                    if (option == null || string.IsNullOrEmpty(option.Id) || string.IsNullOrEmpty(option.Label) ||
                        option.Price < 0 || option.PerShotBloomMultiplier <= 0 || option.PerShotBloomMultiplier > 1 ||
                        optionIds.ContainsKey(option.Id)) { throw new InvalidOperationException("Invalid attachment option."); }
                    optionIds.Add(option.Id, true);
                }
                foreach (WeaponCatalogEntry entry in Entries)
                    foreach (string id in entry.Attachments)
                        if (!optionIds.ContainsKey(id)) { throw new InvalidOperationException("Unknown attachment option " + id); }
            }
            else
            {
                foreach (WeaponCatalogEntry entry in Entries)
                    if (entry.Attachments.Count > 0) { throw new InvalidOperationException("Attachment definitions are missing."); }
            }
            ValidateStage1();
        }

        // Every Stage 1 entry needs class, tier, availability, profile and WeaponInfo type; a Stage 1 weapon can never be
        // in a restricted class or in a tier outside normal availability (no sniper, LMG, P90-type or military weapon).
        private void ValidateStage1()
        {
            List<string> errors = new List<string>();
            if (Tiers != null)
            {
                Dictionary<string, bool> tierIds = new Dictionary<string, bool>();
                foreach (WeaponTier tier in Tiers)
                {
                    if (tier == null || string.IsNullOrEmpty(tier.Id) || string.IsNullOrEmpty(tier.Label) || tierIds.ContainsKey(tier.Id) ||
                        tier.MinimumStoryProgress < 0 || tier.MinimumStoryProgress > 1) { errors.Add("invalid or duplicate tier"); continue; }
                    tierIds.Add(tier.Id, true);
                }
            }
            foreach (WeaponCatalogEntry entry in Entries)
            {
                if (!string.IsNullOrEmpty(entry.Tier) && FindTier(entry.Tier) == null) { errors.Add(entry.Id + ": unknown tier " + entry.Tier); }
                if (entry.Stats != null)
                {
                    if ((entry.Stats.TimeBetweenShotsMilliseconds.HasValue && entry.Stats.TimeBetweenShotsMilliseconds.Value <= 0) ||
                        (entry.Stats.DamageBase.HasValue && entry.Stats.DamageBase.Value <= 0) ||
                        (entry.Stats.ClipSize.HasValue && entry.Stats.ClipSize.Value <= 0) ||
                        (entry.Stats.AmmoMax.HasValue && entry.Stats.AmmoMax.Value <= 0))
                    { errors.Add(entry.Id + ": stats values must be positive"); }
                }
                if (entry.Availability != null)
                {
                    if (entry.Availability.Price < 0) { errors.Add(entry.Id + ": availability.price is negative"); }
                    if (entry.Availability.MinimumStoryProgress.HasValue &&
                        (entry.Availability.MinimumStoryProgress.Value < 0 || entry.Availability.MinimumStoryProgress.Value > 1))
                    { errors.Add(entry.Id + ": availability.minimumStoryProgress must be 0-1"); }
                }
                if (!entry.Stage1) { continue; }
                WeaponTier tierOfEntry = FindTier(entry.Tier);
                if (string.IsNullOrEmpty(entry.WeaponClass)) { errors.Add(entry.Id + ": stage1 needs a class"); }
                if (tierOfEntry == null) { errors.Add(entry.Id + ": stage1 needs a known tier"); }
                else if (!tierOfEntry.NormalAvailability) { errors.Add(entry.Id + ": stage1 weapon in tier " + tierOfEntry.Id + " which is not normal availability"); }
                if (IsRestrictedClass(entry.WeaponClass)) { errors.Add(entry.Id + ": class " + entry.WeaponClass + " is never normal Stage 1 availability"); }
                if (string.IsNullOrEmpty(entry.Profile)) { errors.Add(entry.Id + ": stage1 needs a gunplay profile"); }
                if (string.IsNullOrEmpty(entry.WeaponInfoType)) { errors.Add(entry.Id + ": stage1 needs weaponInfoType"); }
                if (entry.Availability == null || entry.Availability.Sources == null || entry.Availability.Sources.Count == 0)
                    { errors.Add(entry.Id + ": stage1 needs availability sources"); }
                if (entry.Stats == null || entry.VanillaStats == null) { errors.Add(entry.Id + ": stage1 needs stats and vanillaStats"); }
            }
            if (errors.Count > 0) { throw new InvalidOperationException("Weapon catalog: " + string.Join("; ", errors.ToArray())); }
        }
    }

}
