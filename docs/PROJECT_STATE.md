# Liberty+ — current state

Updated 2026-10-02. Authoritative mod dashboard; old cards/reports describe history.

## What is here

The integrated preview was extracted from framework source `56ee760` into a
separate LibertyPlus.dll: weapon catalog/tuning, gunplay and reticles, physical
inventory/holsters, weapon wheel, trunk storage, atmosphere/combat baseline code,
DevTools, AI-generated art, mood tuning and presets. Gameplay configuration values
were preserved. Generic UI/native/SDK services remain in Liberty Framework.

The unfinished gore/HUD/other lane snapshots are archive branches with provenance;
they are not silently enabled or represented as passing main. Framework-side
candidate APIs remain on their preserved original branches.

## Target and outstanding work

[PRODUCT.md](PRODUCT.md) records the owner's complete showcase target. Gore is
required, including wounds, severed limbs/heads, bleeding/pools and reactions.
Vehicles include trunks and the full connected ownership experience. The custom
HUD and existing wheel/holsters/loadout must be finished and polished. Gloomy
visuals must keep color; clouds need major work; target 1080p/60 FPS.

Runtime blockers remain: startup/stalls, gore crashes/cleanup/cost, trunk frame
budgets, selective HUD hiding/restoration, geometry, blood pools, real save/load,
controller/story compatibility and combined performance. The split does not fix
these gameplay problems or establish final acceptance.

## Build and evidence

Standalone framework + Liberty+ compilation PASS. Initial combined tooling checks
312/0; combined NoGame verifier 432/0/7 NOT-RUN on fresh staging. Framework-only
verifier 135/0/4 NOT-RUN. Final staged verifier 441/0/5 NOT-RUN; PowerShell 7 and
5.1 each 317/0, boundary 17/0, preservation audit 82/0 and 45 package hashes checked.
Final results and limitations are in
[the split report](../../GTAIV-Reborn/docs/reports/2026-10-02-repository-split.md).

Installed preview remains `36901ab`; nothing was installed, launched or restored
during the split. Source may be newer than installed gameplay.

## Work order

Repository separation is authorized. Building new showcase features remains paused
until the detailed feature briefs are agreed. Eventual parallel development should
use existing evidence, focused checks, then an assembled candidate for owner testing.
Further deep engine research follows the first mod.
