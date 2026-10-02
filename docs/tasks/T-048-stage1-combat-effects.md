# T-048 — Contextual combat effects

Status: **NEEDS-PLAYTEST** · Lane C · Depends on: T-041 (weapon classes); material impacts wait for T-050 (R2); new effect
textures wait for T-051 (R3) and the art queue · Design: STAGE1 section 7 Slice A "Combat effects", 10 Pillar 2

## Goal

Better contextual effects, not more particles: each weapon class has its own muzzle flash, smoke, casings and sparks;
impacts look like the material that was hit; night muzzle flashes light their surroundings.

## Scope

- Per-weapon/per-class effect sets in config (effect names from the game's particle dictionaries, scale, duration),
  triggered from `BulletFired` and exact damage events.
- Muzzle light at night using a light-drawing native the game has (look it up; rule 4), budgeted and capped.
- Impacts by material once T-050 says whether the hit surface material is available; until then, by entity kind
  (ped, vehicle, world, object) from the raycast/bullet data.
- New muzzle-flash/impact textures: requests ART-005 and ART-006 exist; integrate when approved and when T-051 says
  where particle textures live.
- Caps on active effects (config), shared with T-047's budget.

## Acceptance

- Each Stage 1 weapon has its own configured effect set; scenario `stage1-effects` fires each class at a wall, a car and
  a ped by day and night, with screenshots.
- Gore + effects ≤ 0.8 ms average, ≤ 4 ms peak in the 10-ped firefight.

## Human test steps

1. Launch the Lane C build. Press F10 and select Teleport > Gun Test Range with Down/Enter; close with F10.
2. Equip each Stage 1 weapon in DevTools (Glock, AutoMag, Street Sweeper, Remington, Uzi and AK). Hold right mouse and left mouse to fire at a wall, car and NPC.
3. Set day then night in DevTools; check distinct muzzle/vehicle effects, sustained-fire smoke and brief warm illumination at night.
4. Watch NPCs fight: effects should also originate from their own exact BulletFired events.
5. Toggle weapon_effects.json enabled=false; smoke loops/lights must stop. Re-enable and repeat. Material-specific surfaces and authored decals remain deferred.
6. Review the paired firefight budget report; any FAIL prevents acceptance even if scenario commands completed. Only the owner sets DONE.


## Codex continuation (September 30, 2026)

Implementation and evidence fixes are on stage1/T-048 (includes T-047). [Continuation report](../reports/2026-09-30-lane-c-continuation.md) records the prior failed batch, runtime changes, remaining criteria and archived future verifier commands (no-game instruction currently applies). Gameplay acceptance remains pending.

## Offline cleanup milestone — October 1, 2026

Current runtime receipt: production compile passes at clean merge `844c6a1`; NoGame 442/0/5, tools 285/0.
Quick setup control is NEEDS-REVIEW (43/0 plus shader ERROR); separate active attempt timed out pre-engine
readiness at 300 s without a scenario result. Both restored, latest phase2-165758. No effects/day/night/
dismember follow-up ran. Runtime generation-gate lifecycle, failed-release debt and two fresh zero cleanup
reports remain unproven; no threshold or enabled-state requirement is relaxed. C offline, slot released;
do not retry unchanged cases. [Exact receipts and next diagnostic](../handoffs/sol/receipts/20261001-lane-c/README.md).
Offline preparation statements below are historical; only production compile has since been established.

The preserved `d96aaf2` relaxation to three lights/four effects is replaced by the original zero-owned-resource
requirement. `gore effects-pause`, `gore effects-resume` and `gore effects-state` are registered diagnostic commands
in this lane's combat controller. Pause suppresses new blood bursts/leaks/pulses and weapon muzzle/smoke/impact/light
admission. It does not change `combat_effects.json.enabled` or `weapon_effects.json.enabled`, clear counters/resources,
or stop damage/death/body/dismemberment processing. Existing effects retain their ownership until explicit cleanup
or expiry. Resume removes the admission pause; a configured-off source stays off. Duplicate pause/resume requests fail.

The day/night fixtures verify the original enabled settings, fire the unchanged catalog stimulus, pause admission,
call `gore clear` once, then require two independently marked reports with `lights=0 owned_loops=0 effects=0`,
800 ms and another 2500 ms after cleanup. They resume admission and verify the same enabled settings. No restart
or counter reset occurs between the cleanup reports. The broad `gore enabled off/on` draft was never committed or
tested in game; it is superseded by the effect-only gate. If a diagnostic aborts while paused, restore with
`gore effects-resume` (or restart combat); scheduled verifier runs must still restore the installation.

Pre-slot lifecycle review adds gate reset at start/stop/caught combat failure. A failed expectation itself does not
call resume: the approved verifier's `finally` ends that game session. Duplicate pause remains an error without
altering the existing scope. On manual interruption, explicit resume or combat restart is required; these fixtures
are not authorized for standalone execution. Reset/resume never changes either original enabled config flag.
SDK FX stop now forgets ownership only after the native call returns normally. Failed weapon/blood loop stops stay
locally counted; the FX ledger retains failed releases through every release path and across combat instances.
Stop transfers outstanding FX into `combat_cleanup` budget debt, with retry only at explicit cleanup/start.
Config-off ticks do not retry retired instances. `gore clear` reports incomplete cleanup if owned FX remains.
Non-FX ledger failure semantics, native signatures, SDK material API and all acceptance thresholds remain unchanged.
Focused fault injection compiles the actual FX/ledger/blood/weapon sources against fake game calls: 13/0; gate/
effects checks now 14/0; setup 9/0, total 36/0. Production integration and runtime release remain unproven.

Scoped offline checks: EffectsScenarios 13/0, GoreSetupScenarios 9/0, existing GoreScenarios 8/0 and AutopilotLogic
58/0. Only the small game-independent gate was compiled by the focused unit test; production build and runtime
validation remain pending the central slot. The queue and regenerated plan validate at 93 checks. New runtime
check IDs are unchanged `T048-effects-day`/`T048-effects-night`; no new gameplay PASS is claimed.

Ambient evidence: full `20261001-103314-0b4f558` includes `shots_weapon_15=222`, although the fixture equips only
7/9/10/11/12/14, and ends at `lights=1 effects=2` after clearing. `clear` deletes only autopilot entities and
`events off` controls logging, so neither suppresses ambient bullet admission. Aggregate class counters still
cannot prove that the fixture shooter caused each effect. All 18 latest night captures were inspected in contact
sheets: some wall/ped frames show flash/smoke, but car frames omit the car and several classes lack a visible flash.
Neither these images nor zero accounting leases establish visual acceptance or one-shot particle extinction.

See [the live handoff](../handoffs/Lane-C-live.md) for scheduled commands, failed receipts and review needs.
