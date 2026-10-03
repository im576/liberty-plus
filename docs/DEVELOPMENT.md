# Development and integration

Framework owns generic services; Liberty+ owns gameplay. See
[the ownership contract](https://github.com/im576/liberty-framework/blob/main/docs/architecture/REPOSITORIES.md).

Public commands from this repository:

```powershell
pwsh -NoProfile -File tools/build.ps1 -FrameworkDirectory ../GTAIV-Reborn -ScriptHookDotNetReference '<game>/ScriptHookDotNet.asi'
pwsh -NoProfile -File tools/test.ps1 -FrameworkDirectory ../GTAIV-Reborn -NoGame
pwsh -NoProfile -File tools/prepare-workspace.ps1 -FrameworkDirectory ../GTAIV-Reborn
pwsh -NoProfile -File tools/plan.ps1 -FrameworkDirectory ../GTAIV-Reborn
pwsh -NoProfile -File tools/package.ps1 -FrameworkDirectory ../GTAIV-Reborn -GameDirectory '<game>' -ScriptHookDotNetReference '<game>/ScriptHookDotNet.asi'
```

Build creates a separate privileged legacy module assembly; it does not install or
start GTA IV. `-SkipFrameworkBuild` is only for a freshly built compatible framework.
The framework lock records the tested dependency; revalidate any intentional update.

Tests export current managed files into a fresh `results-local/integration/<id>`.
The receipt records both commits, dirty states, individual file hashes and ownership.
Ignored toolchains/game archives/logs/caches are not source exports. The local compiler
locator is copied explicitly. Never edit workspace sources; fixes belong in their repo.

Legacy scenario/test/package paths are assembled in that workspace to preserve
existing fixtures. These copies are generated test inputs, not another maintained repo.
The workspace has a disposable Git snapshot with no remote for existing source
identity and resume checks; its manifest also records both real source revisions.
The framework assembly excludes every mod-owned mirror. Modules are installed through
the existing loader under scripts/LibertyFramework/mods, with loadModAssemblies enabled.
Old in-game config/state paths are retained so the split does not migrate saves silently.

Package reads the owner's game archives and stages one combined package. It never
installs it. The historical package identifier is retained for installer compatibility;
its manifest identifies Liberty+ and both source revisions. Installation/scenarios
require a separate current game assignment and the shared machine lock.

Artwork tooling stays in the framework:

```powershell
python ../GTAIV-Reborn/tools/art/artq.py --root . validate
```

Approved identical originals now reference their generation; unique/prepped images
remain. Keep prompts, source hashes, notices and research confidence levels.
Archives are unaccepted candidate snapshots and may require companion framework APIs.
