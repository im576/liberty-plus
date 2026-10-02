using System;
using System.Collections.Generic;

namespace LibertyFramework.Weapons.Logic
{
    // What a caller knows about the player when it asks "may this weapon be offered?" (T-041).
    internal sealed class AvailabilityContext
    {
        internal int Money;
        // Fraction of the story finished (0-1); null = not known (no verified source in the game API yet).
        internal double? StoryProgress;
        internal HashSet<string> Contacts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Developer/test give commands: ignores everything except that the weapon is in the catalog.
        internal bool Override;
    }

    internal sealed class AvailabilityResult
    {
        internal bool Available;
        internal string Reason;
        internal static AvailabilityResult Yes() { return new AvailabilityResult { Available = true, Reason = "available" }; }
        internal static AvailabilityResult No(string reason) { return new AvailabilityResult { Available = false, Reason = reason }; }
    }

    // Pure evaluation of a catalog entry's availability rule: tier (restricted tiers are never normal availability),
    // story progress, contact, price. Unknown story progress fails a rule that needs progress (conservative).
    internal static class WeaponAvailability
    {
        internal static AvailabilityResult Evaluate(WeaponCatalog catalog, WeaponCatalogEntry entry, AvailabilityContext context)
        {
            if (entry == null) { return AvailabilityResult.No("not in the catalog"); }
            if (context.Override) { return AvailabilityResult.Yes(); }
            if (!entry.Stage1) { return AvailabilityResult.No("not a Stage 1 weapon"); }
            WeaponTier tier = catalog.FindTier(entry.Tier);
            if (tier == null || !tier.NormalAvailability) { return AvailabilityResult.No("tier " + entry.Tier + " is not normal availability"); }
            WeaponAvailabilityRule rule = entry.Availability;
            double needed = rule != null && rule.MinimumStoryProgress.HasValue ? rule.MinimumStoryProgress.Value : tier.MinimumStoryProgress;
            if (needed > 0)
            {
                if (!context.StoryProgress.HasValue) { return AvailabilityResult.No("needs story progress " + needed.ToString("0.##") + ", which is unknown"); }
                if (context.StoryProgress.Value + 1e-9 < needed) { return AvailabilityResult.No("needs story progress " + needed.ToString("0.##")); }
            }
            if (rule != null && !string.IsNullOrEmpty(rule.Contact) && !context.Contacts.Contains(rule.Contact))
            {
                return AvailabilityResult.No("needs contact " + rule.Contact);
            }
            if (rule != null && rule.Price > context.Money) { return AvailabilityResult.No("costs $" + rule.Price); }
            return AvailabilityResult.Yes();
        }

        // The catalog entries offered under a context (Stage 1 weapons only), in catalog order.
        internal static List<WeaponCatalogEntry> Offered(WeaponCatalog catalog, AvailabilityContext context)
        {
            List<WeaponCatalogEntry> offered = new List<WeaponCatalogEntry>();
            foreach (WeaponCatalogEntry entry in catalog.Entries)
            {
                if (entry.Stage1 && Evaluate(catalog, entry, context).Available) { offered.Add(entry); }
            }
            return offered;
        }
    }
}
