namespace LibertyFramework.Arsenal.Contracts
{
    // Hand-off point between the Arsenal scripts, which ScriptHookDotNet constructs in any order.
    // Arsenal Core sets CarriedWeapons when ready; consumers must treat null as "not available yet"
    // and fall back to their own read of the player's inventory.
    internal static class ArsenalRegistry
    {
        internal static ICarriedWeaponsSource CarriedWeapons { get; set; }

        // Raised by Arsenal Core before it strips weapons (bust, death, loadout overflow) so
        // presentation can delete props first. Handlers run on the script tick; never from drawing.
        internal static event System.Action<string> WeaponsRemoving;

        internal static void RaiseWeaponsRemoving(string reason)
        {
            System.Action<string> handler = WeaponsRemoving;
            if (handler != null) { handler(reason); }
        }
    }
}
