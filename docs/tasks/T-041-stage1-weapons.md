# T-041 — Stage 1 arsenal and availability

Status: **NEEDS-PLAYTEST** · Lane A · Depends on: T-040 (audit) · Design: STAGE1 sections 4-5 (spec), 7 Slice A, 10 Pillar 3

## Goal

Define the Stage 1 arsenal as data and make every catalog weapon a Liberty weapon: its own identity, class, tier and
availability, driven by config. Extends `config/weapon-catalog.json`, `config/gunplay.json` (per-weapon profiles),
Arsenal (`src/LibertyFramework/Arsenal`) and the installed Realistic Weapon Overhaul models. AGENTS.md rule 2: weapons
outside the catalog stay vanilla; every system can be switched off.

## Scope

- **Arsenal list** with class and tier (STAGE1 Slice A): common (cheap/service pistols, revolver where it fits, basic
  shotgun, Uzi-class SMG), less common (better pistols/shotguns, MP5-type SMG, better criminal-market guns), rare
  (AK-type rifle, tactical weapons by circumstance). Not normal in Stage 1: high-end snipers, LMGs, P90-type, military
  gear. Map each to a GTA IV weapon slot/ID (vanilla IDs, FusionFix ExtendedLimits IDs if needed, ADR-0002).
- **Per-weapon identity data:** fire rate, damage, accuracy fields (WeaponInfo through the existing override path) plus
  a Liberty gunplay profile per weapon (recoil, spread, recovery) as a starting point for T-042. Extend the gunplay gate
  from test weapons 58/59/60 to "any weapon in the Stage 1 catalog".
- **Availability:** document what a script can control in IV (gun shop stock, pickups, NPC loadouts, prices, mission
  rewards) from `docs/game-api` and research notes; implement the controllable parts from config (tier by story
  progress / money / contact), write the rest as open questions. No XP bars.
- A DevTools/autopilot command to list the catalog and give any catalog weapon (for tests).

**Owner decision (2026-09-30):** keep every catalog weapon, including the P90-type, MG36 and snipers. They are excluded from normal Stage 1 availability but must be obtainable at any time from the DevTools/mod menu (a give entry per weapon). SMGs (Uzi, MP5) are long guns.

## Acceptance

- Every Stage 1 weapon has a catalog entry (class, tier, availability rule, profile, model) and the verifier checks it.
- No sniper/LMG/P90/military weapon is in normal Stage 1 availability.
- Scenario `stage1-arsenal`: gives each catalog weapon, fires it, confirms bullet events and the Liberty profile in the
  log. Vanilla weapons outside the catalog behave as vanilla.

## Progress (2026-09-30, Claude, lane A)

Implemented; offline checks and the in-game check `T041-stage1-arsenal` pass (run `20260930-134422-33e503f`, branch tip of `stage1/T-043`, which contains this task).

**What was built**
- `config/weapon-catalog.json`: `tiers[]` (common / less-common / rare / restricted / test), `restrictedClasses[]` (`sniper`, `lmg`, `pdw`, `military`), and per entry `weaponInfoType`, `class`, `tier`, `stage1`, `profile`, `availability` (sources, price, contact, story progress), `stats` and `vanillaStats`. Stage 1 arsenal: Glock 17 (7, pistol, common), .44 AutoMag (9, pistol, less common), Street Sweeper (10, shotgun, less common), Remington 1100 (11, shotgun, common), IMI Uzi (12, SMG, common), AK-47 (14, rifle, rare). Not Stage 1 (restricted, vanilla behaviour): id 13 (the pack's P90 look on the MP5 slot), 15 MG36, 16 M40A1, 17 DSR-1.
- `config/gunplay.json`: a Liberty profile for each Stage 1 weapon on its **vanilla id** (`catalogId` links it to the catalog); `stage1Weapons.enabled` switches them all off. The gate (`Gunplay/Logic/Stage1Gate.cs`) applies the recoil/spread/reticle model to a vanilla-id weapon only when the catalog lists it as `stage1`; test weapons 58-60 are unchanged. `WeaponInfoTable` validates each catalog id against the loaded WeaponInfo.xml on its own (a wrong id cannot disable the test weapons) and gives the game its own accuracy back when the player switches away.
- Identity stats: `Merge-WeaponInfoStats` (`tools/PackageMerge.psm1`) writes `stats` into the installed `update\common\data\WeaponInfo.xml` at packaging (`applyWeaponInfoStats: false` writes `vanillaStats`, restoring the game's values). Deliberate changes from the game's own values: Uzi 66 to 85 ms and damage 55 to 45; AK-47 133 to 110 ms. Everything else equals the game's values (caliber differences already exist in them). Starting points for T-042.
- Availability: `WeaponAvailability` evaluates tier, story progress, contact and price (unknown story progress fails a rule that needs it). What a script can control is in [research/WeaponAvailability.md](../../../GTAIV-Reborn/docs/research/WeaponAvailability.md); the rest are open questions below.
- Commands: `catalog list|give|offer|check` (console and autopilot); DevTools > WEAPONS gets the same "Give" entries in T-042.
- Verifier: `Stage1ArsenalChecks` (62 checks: class/tier/availability/profile/model/stats per weapon, restricted classes never Stage 1, gate cases, availability cases, bad data refused). Scenario `stage1-arsenal`: gives, holds and fires each Stage 1 weapon at the test range, expects `weapon_changed ... profile=<Liberty profile>`, `BulletFired`, `gunplay_state`, and `catalog check` (catalog, gunplay.json, gate and loaded WeaponInfo.xml agree); controls: restricted weapons, a test weapon and a vanilla weapon stay as they were.

**Evidence**
- RAN-PASS: build; `tools/verify.ps1` 943 passed 0 failed on the lane tip (62 checks in the Stage 1 arsenal section); `Run-Tests.ps1` 175 passed; `checks.py`; `artq.py validate`; `LOOP-package-install` on the PC (package, offline verifier, content self-test, install with backup and restore; `weaponinfo: 3 stat changes: MICRO_UZI timebetweenshots 66->85; MICRO_UZI base 55->45; AK47 timebetweenshots 133->110`).
- RAN-PASS in game: `T041-stage1-arsenal`, 100 steps, 0 failed. `catalog check` replied "catalog ok: 6 Stage 1 weapons of 13 entries; profiles, gate and WeaponInfo.xml agree" (so each of the six ids was validated against the loaded WeaponInfo.xml and the loaded XML holds the catalog stats); `catalog offer` gave 3 weapons for $600 at the start and 6 for a rich, late, connected player; for each of the six weapons `weapon_changed ... profile=<Liberty profile> catalog=<id>`, `event BulletFired ... by_player=True weapon=<id>` and `gunplay_state weapon=<id> profile=<Liberty profile>` were logged; the controls (m40a1, fn-p90, hk-mg36 given with `force`, a bat, the gold test pistol) logged `profile=vanilla` for the restricted and vanilla weapons and `pistol_gta4plus` for the test weapon; switching away from a catalog weapon logged `weaponinfo_restored`. Screenshots reviewed: the HUD weapon icon and clip size are the catalog weapon's each time.
- Log errors in that run were not from this task: three `dismember_corpse_lost` (combat module, lane C territory) and one `shot_audit_owner_mismatch` (existing).
- NEEDS OWNER: how each weapon looks and handles (visuals and feel).
## Open questions

1. **Id 13** is the MP5 slot, but the installed weapon pack draws a P90 there, and the design excludes P90-type weapons. Per the owner decision (2026-09-30: keep every weapon, out of normal availability, always obtainable from the mod menu) it stays `restricted` and DevTools > WEAPONS has a give entry for it and for ids 15, 16 and 17 ("not normal availability"). If you accept the P90 as the "MP5-type" less-common SMG, change its `class` to `smg`, `tier` to `less-common`, `stage1` to true and give it a profile.
2. The design's "revolver" and "MP5-type" slots have no weapon in the installed pack (ids 7-17 are ten weapons); the rare tier has only the AK-47 ("tactical by circumstance" is not represented). Adding weapons needs FusionFix ExtendedLimits ids and models (ADR-0002 registered three).
3. Availability: story progress, contacts, Ammu-Nation stock, pickups and ambient NPC loadouts have no verified script control (research note). The tiers rule only Liberty's own offers; the game's own shops still sell every vanilla weapon.
4. A catalog weapon shares its WeaponInfo entry with every NPC that carries it, so while the player holds a Stage 1 pistol the model's accuracy applies to NPC pistols too (restored on switching away). Acceptable for Stage 1 or gate it further?

## Human test steps

1. Install the build (the verifier does it, or `tools/install-phase2.ps1`) and start the game with an audio output connected.
2. Give yourself each Stage 1 weapon with the console command `catalog give service-pistol` (then `combat-pistol`, `street-sweeper`, `remington-1100`, `imi-uzi`, `ak-47`; `catalog list` names them). From T-042 on, DevTools (F10 or L3+R3) > WEAPONS also lists them as "Give ... (Stage 1, <tier>)", and the four restricted weapons (P90 look, MG36, both snipers) as "Give ... (not normal availability)". Go to the test range (DevTools > TELEPORT), aim and fire: you should see the pack's model for each and the Liberty crosshair opening as you fire.
3. Compare feel with the vanilla weapon it replaces only through the numbers: the Uzi fires a little slower and hits softer, the AK-47 a little faster; everything else matches the game's own values for now (T-042 tunes handling).
4. The sniper rifles, the MG36 and the P90-looking SMG (ids 13, 15, 16, 17) still behave exactly as in the game: give one with the console `catalog give m40a1 25 force` if you want to compare.
5. In `scripts\LibertyFramework\logs\LibertyFramework.log` look for `stage1_gate` (one line per catalog weapon: `gunplay=liberty` for the six, `vanilla` for the others) and `weaponinfo_stage1_validated`.
6. To switch it all off: set `"stage1Weapons": { "enabled": false }` in `config\gunplay.json` (hot-reloaded) and set `"applyWeaponInfoStats": false` in `config\weapon-catalog.json`, then run the package/install again to put the game's own stats back.