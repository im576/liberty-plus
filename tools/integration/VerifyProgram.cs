using System;

namespace LibertyFramework.Verify
{
    // Offline verification for Liberty Framework: engine address resolution against the real
    // GTAIV.exe, plus the pure gunplay/config logic. Exit code 0 only when every check passes.
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("usage: OfflineVerify <GTAIV.exe | --no-game> <repo root>");
                return 2;
            }
            string exe = args[0];
            string repo = args[1];
            Checker check = new Checker();
            // --no-game (the cloud container): sections that read GTAIV.exe or game archives are reported NOT-RUN, the
            // repository-only sections still run. A no-game run is never a full verification.
            check.NoGame = exe == "--no-game";

            Game(check, "Engine address resolution", delegate { AddressChecks.Run(exe, check); });
            Section(check, "Native names used by the DLL are listed (repository)", delegate { NativeChecks.RunListed(repo, check); });
            Game(check, "Native names used by the DLL", delegate { NativeChecks.Run(exe, repo, check); });
            Game(check, "Core native table (ADR-0006)", delegate { EngineChecks.Run(exe, repo, check); });
            Section(check, "Gunplay logic and configuration", delegate { LogicChecks.Run(repo, check); });
            Section(check, "Arsenal core (T-020)", delegate { ArsenalCoreChecks.Run(repo, check); });
            Section(check, "Phase 2 ownership and catalog", delegate { Phase2SystemsChecks.Run(repo, check); });
Section(check, "Stage 1 arsenal: catalog, profiles, gate, availability (T-041)", delegate { Stage1ArsenalChecks.Run(repo, check); });
Section(check, "Stage 1 gunplay: class targets against the spread and recoil model (T-042)", delegate { Stage1GunplayChecks.Run(repo, check); });
Section(check, "Stage 1 reticles: class styles, overrides, migration, truthfulness (T-043)", delegate { Stage1ReticleChecks.Run(repo, check); });
            Section(check, "Physical loadout: 2 long guns + 1 sidearm, ammunition caps, outfits (T-044)", delegate { LoadoutChecks.Run(repo, check); });
            Section(check, "Weapon wheel: segments, press handling, bindings (T-045)", delegate { WeaponWheelChecks.Run(repo, check); });
            Section(check, "Trunk interface: capacity per vehicle class, swap rule (T-046)", delegate { TrunkUiChecks.Run(repo, check); });
            Section(check, "Feel and presentation (T-011, T-013..T-017, T-021)", delegate { FeelChecks.Run(repo, check); });
            Game(check, "Vehicle body parts (T-023)", delegate { VehicleChecks.Run(exe, check); });
            Game(check, "Dismemberment plans and particles (T-022)", delegate { CombatChecks.Run(exe, repo, check); });
            WindowsOnly(check, "Dismemberment collapse engine machine code (ADR-0005)", delegate { CollapseEngineChecks.Run(check); });
            Section(check, "Atmosphere: weather director and density governor (M-2, E-5)", delegate { AtmosphereChecks.Run(repo, check); });
            Section(check, "Hot reload file watcher (M5)", delegate { HotReloadChecks.Run(check); });
            Section(check, "LibertyCore C ABI: liberty_core.h against CoreAbi.cs and CoreBridge", delegate { CoreAbiChecks.Run(repo, check); });
            Section(check, "SDK examples in the docs compile against the current SDK", delegate { SdkExampleChecks.Run(repo, check); });
            Section(check, "Engine plumbing: scheduler, events, ledger, commands, manifests (engine audit)", delegate { EnginePlumbingChecks.Run(check); });
            Section(check, "Engine configuration (engine.json, ADR-0008 raycast fields)", delegate { EngineConfigChecks.Run(repo, check); });
            Section(check, "Config watches: idle polling, shared paths and owner cleanup (T-045)", delegate { ConfigWatchChecks.Run(check); });
            Section(check, "World objects: config and streaming (T-033)", delegate { WorldObjectsChecks.Run(repo, check); });

            Console.WriteLine("RESULT passed=" + check.Passed + " failed=" + check.Failed + (check.NotRun > 0 ? " notrun=" + check.NotRun : ""));
            return check.Failed == 0 ? 0 : 1;
        }

        // Each section is isolated: an exception fails that section and the remaining sections still run, so one broken
        // check cannot hide the results after it.
        private static void Section(Checker check, string name, Action run)
        {
            Console.WriteLine("== " + name);
            try { run(); }
            catch (Exception error) { check.True(name + ": section completed without exception", false, error.ToString()); }
        }

        // A section that reads GTAIV.exe or the game's archives.
        private static void Game(Checker check, string name, Action run)
        {
            if (check.NoGame)
            {
                Console.WriteLine("== " + name);
                check.Skip(name, "needs GTAIV.exe and game files; run tools/verify.ps1 -GameDirectory on the PC");
                return;
            }
            Section(check, name, run);
        }

        // A section that executes 32-bit x86 machine code through VirtualAlloc: it needs Windows, whatever the game files.
        private static void WindowsOnly(Checker check, string name, Action run)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Console.WriteLine("== " + name);
                check.Skip(name, "executes x86 machine code through VirtualAlloc; needs Windows");
                return;
            }
            Section(check, name, run);
        }
    }
}
