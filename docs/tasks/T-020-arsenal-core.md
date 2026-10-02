# T-020 — Liberty Arsenal core: loadout, ownership, storage, death/arrest

Status: **NEEDS-PLAYTEST** (Codex Agent A, branch `arsenal/core`). Owner decisions 2026-09-24; see the rules below — they are requirements, not suggestions.

## Owner rules (requirements)

1. **Loadout (RDR2 + melee).** On the body at most: 2 sidearms, 2 long guns, 1 melee. Thrown weapons are carried but uncounted.
   - Sidearm categories: `Handgun`, `SMG` (GTA IV already allows one per category, so the pair is handgun + SMG).
   - Long-gun categories: `Shotgun`, `Rifle`, `Sniper`, `Heavy` — at most **two** of these four carried at once.
   - Melee: 1. Limits and category→body-slot mapping come from `config/arsenal.json`.
2. **Overflow ("the horse").** When the player gains a weapon beyond the limit, the least-recently-used weapon of that group moves to storage: the **last used vehicle's trunk** (RDR2 horse style, even if far away); if there is no last vehicle, to the **most recently used safehouse stash**. When the game itself replaces a same-category weapon (picking up a different pistol), an **owned** replaced weapon is also moved to that storage instead of vanishing. Log every move.
3. **Ownership.** Owned = bought (a purchase is a weapon gain in the same tick window as a money decrease; window in config) or taken from the player's trunk/stash. A picked-up weapon is unowned until the player stores it once (then it is owned). Mission-given weapons are unowned.
4. **Busted:** every carried weapon is permanently gone (owned or not). **Wasted:** carried *owned* weapons go to the most recently used safehouse stash; unowned are lost. Snapshot the carried set every tick so the result does not depend on when the game strips weapons.
5. **Missions and cutscenes:** never remove or move weapons while a mission is active or a cutscene/fade is running. When the mission ends, over-limit weapons go to overflow storage per rule 2.
6. **Trunk storage.** Standing at a vehicle's rear (distance in config) offers a trunk menu: store carried weapon / take stored weapon, with the boot opened (`OPEN_CAR_DOOR`, boot door index from natives/SHDN docs — verify) and closed after. Contents persist only for **owned vehicles** (below). Random vehicles keep contents while they exist and lose them when deleted, burnt out or sunk (log it).
7. **Safehouses.** A configured list (id, name, episode, x, y, z, radius). "Most recently used" = last one the player entered the radius of or saved at. Each has a stash spot with the same store/take menu. Coordinates must come from a documented source (game data file, script, or a value you log in game); mark unverified ones `"verified": false` in config. Add a DevTools action "Mark safehouse here" through the page hook.
8. **Owned vehicles.** Liberty Vehicle Services CE (MIT, ekzestean) is the vehicle base and is installed alongside us. Read its source (extracted at `...\work\research\lvs`; `OwnedVehicleRecord`, `LoadOwnedVehicles`, `[owned.<id>]` blocks) and implement a **read-only** reader of its owned-vehicle state so a trunk can be keyed by its owned-vehicle `Id`. If LVS is absent or its file is unreadable, fall back to an Arsenal-owned marker: the last vehicle the player stored weapons in and exited is remembered by model + position. You may adapt LVS code (MIT) with credit in `third_party/README.md`.
9. **Persistence.** `scripts/LibertyFramework/state/arsenal_<episode>.json` via `LibertyPaths.ArsenalState` and `JsonStore.Save` (atomic, `.bak`). Corrupt file → keep the `.bak`, log, start empty, never crash.

## Shared contracts (read-only for agents; request changes from the orchestrator)

- `src/LibertyFramework/Arsenal/Contracts/`: `BodySlot`, `WeaponCategory`, `WeaponRecord`, `CarriedWeapon`, `ICarriedWeaponsSource`, `ArsenalRegistry`.
- Agent A implements `ICarriedWeaponsSource` and assigns it to `ArsenalRegistry.CarriedWeapons` once running, bumping `Revision` on every change, and calls `ArsenalRegistry.RaiseWeaponsRemoving(reason)` before stripping weapons.
- DevTools: register at most one page with `DevToolsPages.Register(title, factory)`; do not edit `DevToolsMenu.cs`.
- Pure logic goes in folders named `Logic` (no `GTA` namespace), which `tools/verify.ps1` compiles automatically. Tests go in `tools/verify/ArsenalCoreChecks.cs`.
- Paths: `LibertyPaths.ArsenalConfig`, `LibertyPaths.ArsenalState(episode)`.

## Acceptance (offline)

- Build 0 errors / 0 warnings; verify all green.
- Tests: overflow picks least-recently-used; long-gun cap 2 across 4 categories; ownership transitions (purchase window, stash → owned, mission → unowned); bust clears all; death moves only owned to last safehouse; mission gate defers moves; storage JSON round-trip and corrupt-file recovery; LVS owned-file parse on a sample built from the LVS source format.
- Every native in `native-hashes.csv`, registered and ScriptHook-mapped (verify proves it).
- `config/arsenal.json` documented in `docs/architecture/CONFIG_SCHEMA.md` (Arsenal section).
- Human test steps below, status `NEEDS-PLAYTEST`, report in `docs/archive/agent-reports/agent-a.md`.

## Human test steps

Precondition: orchestrator merges T-020, packages `config/arsenal.json` with the DLL, and installs only while GTA IV is closed. Use a save with an accessible vehicle and a safehouse. Keep the log at `scripts/LibertyFramework/logs/LibertyFramework.log` for the report. GTA IV itself has not been run by Agent A.

1. Start a free-roam save. Hold **L3+R3 for 0.7 seconds** (or press **F10**), move the D-pad to **ARSENAL**, press **A**. Select **Mark safehouse here** with **A** while at the safehouse stash spot. Expect `Marked marked_...`, a new verified coordinate in `scripts/LibertyFramework/config/arsenal.json`, and an `arsenal_safehouse_marked` log line. Press **B** back to the root, then **B** to close; D-pad phone control must return.
2. Stand directly behind a parked car, within `trunkDistanceMeters` of its boot. Open DevTools and enter **ARSENAL**. Expect a **TRUNK** heading and an open boot. Select **Store** for a carried pistol with **A**. Back out with **B**, reopen the page, and select **Take** for that pistol. Expect the weapon and its ammo to return, and `arsenal_store` / `arsenal_take` log lines. Close DevTools; expect the boot to close and normal controls to return.
3. With a car used at least once, use DevTools **WEAPONS** to give the test shotgun and carbine (confirm each action with **A** twice). Obtain a third long gun in normal gameplay, then watch the log. Expect the least recently selected of the three long guns to move to the last vehicle trunk (`arsenal_overflow`) while two remain carried. Stand at that car's rear and reopen ARSENAL to confirm the moved weapon is listed under **Take**. Do the same after driving the car away; overflow should still target that last car.
4. Store one picked-up weapon, then take it. Confirm it is logged as owned on later gain. Pick up a different weapon in the same GTA inventory category; expect the formerly owned replacement to appear in the last vehicle trunk, with `arsenal_replaced_owned` in the log. Pick up an unowned weapon and check that no ownership is inferred without a money decrease or storage. Buy a weapon and confirm `arsenal_gain ... owned=True` when the gain follows the money decrease within the configured window.
5. Enter a mission with more than two long guns available and trigger a cutscene/fade. During mission and cutscene, confirm no `arsenal_overflow`, `arsenal_store`, or `arsenal_replaced_owned` movement occurs. After mission end and fade-in, expect deferred overflow/replacement entries to appear in storage. Report the exact mission and log excerpt.
6. With one owned and one unowned carried weapon, save, then get **wasted**. After respawn, open ARSENAL at the marked safehouse. Expect only the owned weapon in the stash; the unowned one is lost. Repeat from a save and get **busted**; expect neither carried weapon to appear in storage or on the player. Include `arsenal_loss` lines in the report.
7. With Liberty Vehicle Services CE installed, register/own a car, store a weapon in its trunk, close GTA IV, relaunch, and revisit that car. Expect the weapon still under **Take** and a `lvs:owned_...` trunk key in logs/state. Repeat without LVS or with its owned INI temporarily unavailable (restore it afterward): store in a random car, exit it, close/relaunch, and revisit the remembered model and position. Expect the fallback trunk to be available. For a random unmarked car, destroy, sink, or delete it after storing: expect `arsenal_temporary_trunk_lost` and no retained contents.
8. Confirm `state/arsenal_iv.json` exists for the IV episode and is separate from TLAD/TBOGT state if those episodes are available. With the game closed, back up this file, replace it with malformed JSON, and relaunch. Expect `arsenal_state_corrupt`, a retained `.bak`, a renamed `.corrupt_...` file, empty storage, and continued gameplay. Restore the backup after the check.

Offline evidence: `tools/build.ps1` built 77 sources with zero errors and zero warnings; `tools/verify.ps1` returned `RESULT passed=199 failed=0` against the installed GTAIV.exe/ScriptHook.dll. In-game behavior remains unverified.

## Integration note

`tools/archive/package-phase1.ps1` currently copies named config files and omits `arsenal.json`. The orchestrator must add it to packaging/install manifests before a playtest. `docs/PROJECT_STATE.md` is outside Agent A's file ownership; orchestrator should add the T-020 entry after merge.
