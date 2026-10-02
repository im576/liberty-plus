# T-021 — Visible weapons on the body (holsters)

Status: **NEEDS-PLAYTEST** (Codex Agent B, branch `arsenal/feel`). Built from scratch, using ScriptHookDotNet wrappers and documented natives.

**September 29 focused review:** `14010f6` passed 31/31 scripted steps with no log errors; images show a live player,
one slung rifle and its strap. The scenario granted two rifles that share one game slot. It now grants shotgun 10
and rifle 14 and expects both sling attachment slots before capture. Two-gun placement still needs the revised
in-game review. Evidence: `verification-results/results/20260929-115421-14010f6/`.

**Later run `6931061`:** both attachment slots logged and all 32 script steps passed, but the player took lethal
NPC damage after an accepted `god on`. Front/back are death scenes, left is black, and right/close show an unarmed
respawn. Visual result is FAIL. Repeated SDK invincibility requests now reapply native flags while retaining one
ownership entry. The scenario reasserts after teleport, checks that entry, and requires a live player before every
capture. The reason the original flags ceased protecting the player remains unknown; the fix needs Windows proof.
Cleanup is registered before native application so a partial native failure still belongs to the module's ledger.
Evidence: `verification-results/results/20260929-121911-6931061/T021-sling-review/`.

**Follow-up `0d849f0`:** hot reload passed 24/24 and selftest 49/49, but sling failed its first survival check
(1/41 failed). The player was already dead before the first command. Later captures show the live respawned player
with two slung long guns; the close-up establishes distinct weapon props and straps, but motion/clipping behavior
and the intended scene remain unverified. The scenario now runs `await-alive` through the SDK scheduler with a
60-second bound before teleporting. Its fresh readiness log, teleport completion and subsequent survival checks must
all pass; a timeout is still a failed expectation. No resurrection or guessed native was added. Windows proof pending.
Evidence: `verification-results/results/20260929-142632-0d849f0/`.

## Requirements

1. Every carried weapon that is **not in hand** is shown as a prop of its own model on the player: `SidearmPrimary` (handgun) on the right thigh, `SidearmSecondary` (SMG) on the left hip, `LongGun1`/`LongGun2` slung on the back (two distinct positions, no overlap), `Melee` on the belt/back. Gold variants (`lf_gold_*`) show gold.
2. Source of truth: `ArsenalRegistry.CarriedWeapons` (T-020). While it is null (Arsenal not running), derive the carried set yourself from the player's inventory with the same category→slot rule so the feature works and is testable on its own.
3. Model name per weapon: read from the active `WeaponInfo.xml` (`WeaponInfoXml.ModelFor`) so custom/gold weapons work; request/load models before creating objects; never block the tick waiting for streaming.
4. Hide/cleanup: hide the in-hand weapon; hide all in vehicles except bikes (config), during cutscenes/fades, and when `ArsenalRegistry.WeaponsRemoving` fires; delete every prop on death, bust, script unload and error. No orphaned objects after `ReloadScripts` (track handles; clean up on the next start as well).
5. Placement: bone + position + rotation per `BodySlot` with optional per-category/per-model overrides in `config/holsters.json`. Prefer ScriptHookDotNet wrappers (`World.CreateObject`, `Object.AttachToPed`, `GTA.Bone`; sources in `...\work\shdn-source`) over raw natives. A DevTools page (via `DevToolsPages.Register`) nudges the selected slot's offsets live and saves to `holsters.json` (with `.bak`).
6. Performance: at most one prop per body slot; re-attach only when `Revision`, weapon or visibility changes.

## Acceptance (offline)

Build/verify green; tests in `tools/verify/FeelChecks.cs` for slot mapping, hide rules, and `holsters.json` validation; config documented in `CONFIG_SCHEMA.md`; human test steps (per slot: is it visible, placed well, hidden in hand/vehicle/cutscene, gone after death/bust/reload); report in `docs/archive/agent-reports/agent-b.md`.

## Human test steps

1. After the orchestrator installs the merged build, start on foot. Open Liberty DevTools with L3+R3 held (or F10). In WEAPONS, give all three gold test weapons (Cross twice), then close the menu.
2. Hold the gold pistol. The carbine and shotgun should appear at separate back positions; the pistol should have no duplicate thigh prop. Select the carbine, then shotgun: the one in hand disappears from the back and the others return. Report colour and clipping.
3. Equip a vanilla SMG and a melee weapon. Check the SMG at the left hip, pistol at the right thigh, and melee at the belt/back. Move, crouch and take cover; props should follow the body.
4. Enter a car: props disappear. Exit: props return. Mount a bike: props remain visible with `showOnBikes=true`. During a cutscene/fade they should disappear and then return.
5. Open DevTools > Holsters. Use Cross on `Next slot` and D-pad left/right on the Position/Rotation rows to nudge each slot. Close and inspect. Reopen, select `Save offsets` with Cross, and confirm `holsters.json.bak` exists.
6. Die or get busted while carrying weapons: props disappear. Run `ReloadScripts` from the ScriptHookDotNet console: no old floating props remain and only one set returns. Attach the log, especially `holster_orphan_removed` and `holsters_removed` lines.

Offline: build 0 errors/0 warnings; verifier passes against GTAIV.exe and ScriptHook.dll. Placement and cutscene behaviour await the owner test. Because natives cannot run at DomainUnload, surviving props are journalled and removed on the next script tick after same-process reload.

Known source-data limit: the installed IV `WeaponInfo.xml` gives `FTHROWER` no `<assets model>`, so ID 19 cannot display a prop. Episodic IDs 21–41 resolve `EPISODIC_N` from the current episode XML; the mapping needs in-game confirmation.
