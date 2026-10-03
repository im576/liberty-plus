# Liberty+

A substantial GTA IV overhaul built on **[Liberty Framework](../GTAIV-Reborn/README.md)**.
This repository owns the player experience: weapons, reactive reticles, physical
inventory/holsters, weapon wheel, trunk storage, gore, vehicle ownership, custom
HUD, atmosphere, visual tuning and AI-generated artwork.

**Current state:** extracted preview source, not the finished overhaul. Gore,
vehicle ownership, clouds and HUD still have documented unfinished or unverified
work. Start with [status](docs/PROJECT_STATE.md), [target feature brief](docs/PRODUCT.md),
[development instructions](docs/DEVELOPMENT.md) and [migration provenance](docs/migration/extraction.json).

## Build

Keep the framework checkout beside this repo, or pass `-FrameworkDirectory`.

```powershell
pwsh -NoProfile -File tools/build.ps1 -ScriptHookDotNetReference '<game>/ScriptHookDotNet.asi'
pwsh -NoProfile -File tools/test.ps1 -NoGame
```

The build produces `bin/LibertyPlus.dll` separately from the framework. The test
entry point creates an ignored, provenance-recorded integration workspace. It
never launches the game. `tools/package.ps1` assembles the combined installation
from your own game archives but does not install it.

The extracted preview uses a pinned privileged compatibility interface. New
ordinary mods should use only Liberty.Sdk; see the framework's repository contract.
Original source namespaces remain for compatibility, while folder/assembly
ownership separates the products. No framework source is vendored here.

No GitHub remote has been created or pushed. Original framework history and local
worktrees remain intact. Archived mod lane snapshots are unaccepted research work.
