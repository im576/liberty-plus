using System;
using System.Collections.Generic;

namespace LibertyFramework.Hud.Logic
{
    internal enum HudElementMode
    {
        // The element is switched off in config: the vanilla one (if any) is left alone.
        Off,
        // Liberty draws the element; ToHide lists the vanilla components to hide for it (may be empty).
        Liberty,
        // The vanilla element stays and Liberty draws nothing for it (no duplicate).
        VanillaKept
    }

    internal sealed class HudDecision
    {
        internal HudElementMode Mode;
        internal string Reason = "";
        internal readonly List<string> ToHide = new List<string>();
    }

    // T-049: may Liberty draw an element, and which vanilla components must go for it. The rule is the card's: hide only what Liberty
    // replaces; if the vanilla element cannot be hidden on its own, keep it and draw no duplicate. "Can be hidden" means every listed
    // component is resolved AND its visible disappearance was verified. A successful globals write alone is not that proof.
    internal static class HudPlan
    {
        internal static HudDecision Decide(bool enabled, string hideVanilla, IList<string> vanillaComponents, bool drawWithoutHidingVanilla,
            Func<string, bool> isResolved, Func<string, bool> isHidingVerified)
        {
            HudDecision decision = new HudDecision();
            if (!enabled) { decision.Mode = HudElementMode.Off; decision.Reason = "disabled in config"; return decision; }
            if (hideVanilla != HudConfig.HideComponents)
            {
                decision.Mode = drawWithoutHidingVanilla ? HudElementMode.Liberty : HudElementMode.VanillaKept;
                decision.Reason = "hideVanilla=" + hideVanilla;
                return decision;
            }
            if (vanillaComponents == null || vanillaComponents.Count == 0)
            {
                decision.Mode = drawWithoutHidingVanilla ? HudElementMode.Liberty : HudElementMode.VanillaKept;
                decision.Reason = "no separable vanilla component is configured for this element";
                return decision;
            }
            List<string> missing = new List<string>();
            foreach (string name in vanillaComponents)
            {
                if (isResolved(name) && isHidingVerified(name)) { decision.ToHide.Add(name); } else { missing.Add(name); }
            }
            if (missing.Count == 0) { decision.Mode = HudElementMode.Liberty; decision.Reason = "vanilla hidden: " + string.Join(",", decision.ToHide.ToArray()); return decision; }
            decision.Reason = "visible hiding unavailable or unverified: " + string.Join(",", missing.ToArray());
            if (drawWithoutHidingVanilla) { decision.Mode = HudElementMode.Liberty; return decision; }
            decision.ToHide.Clear();
            decision.Mode = HudElementMode.VanillaKept;
            return decision;
        }
    }
}
