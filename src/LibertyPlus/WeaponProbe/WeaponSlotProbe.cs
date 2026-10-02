using System;
using GTA;
using LibertyFramework.Core.Logging;
using LibertyFramework.Gunplay;
using LibertyFramework.Gunplay.Profiles;
using LibertyFramework.Weapons;

namespace LibertyFramework.WeaponProbe
{
    // Console commands kept from T-007 (LFWeaponStatus / LFWeaponGive / LFWeaponVanilla); they now
    // use the configured test weapons. Weapon grants only happen on an explicit command.
    [global::Liberty.Sdk.Module("weapon-probe", Order = 110, Capabilities = new[] { global::Liberty.Sdk.Capabilities.EngineInternal }, Description = "Weapon slot probe (diagnostics)")]
    public sealed class WeaponSlotProbe : LibertyFramework.Engine.Module
    {
        public WeaponSlotProbe()
        {
            BindConsoleCommand("LFWeaponStatus", new ConsoleCommandDelegate(Status), "- log the current weapon identifier");
            BindConsoleCommand("LFWeaponGive", new ConsoleCommandDelegate(GiveTest), "- select the gold test pistol (ID 58)");
            BindConsoleCommand("LFWeaponVanilla", new ConsoleCommandDelegate(GiveVanilla), "- select the vanilla pistol");
            RuntimeLog.Info("weapon console commands registered");
        }

        private void Status(ParameterCollection parameters)
        {
            Execute("status", () => TestWeaponActions.Status(Player, Config));
        }

        private void GiveTest(ParameterCollection parameters)
        {
            Execute("give_test", () => TestWeaponActions.Select(Player, Pistol(), true));
        }

        private void GiveVanilla(ParameterCollection parameters)
        {
            Execute("give_vanilla", () => TestWeaponActions.Select(Player, Pistol(), false));
        }

        private static GunplayConfig Config
        {
            get { return GunplayController.Instance != null ? GunplayController.Instance.Config : null; }
        }

        private static WeaponProfile Pistol()
        {
            if (Config == null || Config.Weapons.Count == 0) { throw new InvalidOperationException("gunplay.json not loaded"); }
            return Config.Weapons[0];
        }

        private void Execute(string action, Func<string> operation)
        {
            try
            {
                if (Player == null || Player.Character == null)
                {
                    RuntimeLog.Info("console " + action + " skipped: player not ready");
                    return;
                }
                string result = operation();
                RuntimeLog.Info("console " + action + " " + result);
                Game.Console.Print("[LibertyFramework] " + result);
            }
            catch (Exception error)
            {
                RuntimeLog.Error("console " + action + " failed: " + error);
                Game.Console.Print("[LibertyFramework] " + action + " failed; see log");
            }
        }
    }
}
