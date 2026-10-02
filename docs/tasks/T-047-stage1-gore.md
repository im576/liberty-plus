# T-047 — Harsh gore (exact-damage driven)

Status: **NEEDS-PLAYTEST** · Lane C · Depends on: T-040; blood pools/trails/surface blood wait for T-051 (R3)
Design: STAGE1 section 7 Slice A "Harsh gore", 10 Pillar 2, 12.4

## Goal

*"Harsh realism, not gore for gore's sake."* Graphic, grounded, consequential violence driven by the exact damage
event (attacker, weapon, bone, hit point, direction, amount), never by scanning nearby peds and guessing.

## Starting point

`src/LibertyFramework/CombatEffects/` (`CombatEffectsController.cs`, `HitClassifier.cs`, `BloodEffects.cs`,
`Dismemberment.cs`, `PedInjuryState.cs`), `config/combat_effects.json`, the SDK `PedDamaged`/`PedDied` exact events
(ADR-0007), the ADR-0005 skeleton hook, the Violent Liberty companion (`docs/research/ViolentLiberty.md`), scenarios
`gore-review`, `exact-damage`. Audit first: remove any remaining proximity-scan attribution in favour of exact events.

## Scope

- Entry-hit responses, severe head trauma, shotgun trauma, caliber-sensitive presentation (weapon class and distance
  from the exact event), limb damage, dismemberment where reliable (no floating/flashing limbs), bleeding.
- Wounded, survivable NPCs: brief crawling/writhing/pain behaviour using natives/tasks the game has (check
  `docs/game-api/NATIVES.md` and FusionFix `natives.ixx`; rule 4), never a scripted loop.
- Strong panic from nearby NPCs after severe violence.
- Contextual executions only if they fit naturally (grounded, no finisher system); otherwise leave as a design note.
- **Persistence:** bodies 3-5 minutes (config), blood may outlast bodies; hard caps on bodies/decals/effects,
  distance-based cleanup, adaptive cleanup under engine pressure (`Liberty.Perf.Pressure`); performance wins.
- Blood pools, trails and surface blood: design the interface now, implement after T-051 answers what IV allows.

## Acceptance (STAGE1 Pillar 2)

- 100% of gore driven by exact events (code review + log); entry effect ≤ 1 engine frame after the event.
- Severe headshot effect in ≥ 95% of pistol headshots ≤ 10 m; shotgun trauma in ≥ 90% at ≤ 5 m; 0 floating or
  flashing limbs in 50 dismemberment trials (autopilot scenarios with `hurt`/`fire` against spawned peds).
- Panic/scream/flee in ≥ 90% of nearby peds after severe violence.
- Budget: gore + effects ≤ 0.8 ms average, ≤ 4 ms peak in the 10-ped firefight; frame p95 ≤ +15% vs mod-off there.

## Human test steps

1. Launch the installed Lane C build. Press F10, use Down/Enter to select **Gore Test**, then press F10 to close the menu after requesting a leak or cut on a nearby NPC.
2. With the Glock, aim at a nearby NPC's head (right mouse, then left mouse). Inspect the entry spray, severe response and resulting body; repeat with the Remington at <=5 m.
3. Inspect thrown arms/legs for floating or full-body flashes. This is currently an unresolved acceptance failure.
4. Remain nearby for 3-5 minutes: bodies should remain until their configured lifetime unless bounded pressure/cap cleanup applies.
5. Toggle combat_effects.json enabled=false and wait for reload: retained pins, loops and clones must release; re-enable and repeat a hit.
6. Judge feel, visibility and event-to-visible latency separately from particle-native success counters. Only the owner sets DONE.


## Codex continuation (September 30, 2026)

Implementation and evidence fixes are on stage1/T-048 (includes T-047). [Continuation report](../reports/2026-09-30-lane-c-continuation.md) records the prior failed batch, runtime changes, remaining criteria and archived future verifier commands (no-game instruction currently applies). Gameplay acceptance remains pending.

## Offline cleanup and crash-setup milestone — October 1, 2026

Current slot receipt: clean merge `844c6a1` builds with warnings as errors; NoGame verifier 442/0/5 not-run,
tools 285/0. Quick control `20261001-164156-844c6a1` is NEEDS-REVIEW (43/0, one shader-dialog ERROR), restored
phase2-164203. Scene-load elapsed 2896 ms with combat stopped; weather and both fresh heartbeats completed.
Separate unchanged active `20261001-165752-844c6a1` is ERROR after outer 300 s startup timeout, restored
phase2-165758; no scenario result, labels or heartbeat. Late ScriptHook/SHDN initialization exists, but no fresh
Liberty boot marker/crash/WER receipt. No valid control/active gameplay comparison or cut acceptance follows.
Do not retry unchanged active/effects/dismember; C offline and slot released. Next proposed assigned diagnostic
is startup-only T057 heartbeat after parent host review, preserving launch/dialog/boot telemetry on timeout.
Exact evidence and unchanged remaining 4 ms/cleanup gates: [receipt](../handoffs/sol/receipts/20261001-lane-c/README.md).
The preparation history below remains historical; the two cases no longer both have runtime NOT-RUN status.

Prepared `T047-gore-setup-control` and `T047-gore-setup-active` using documented existing engine/autopilot commands.
Each runs the same range teleport, time/weather requests, labeled framestats/costs/perf windows and fresh heartbeat
checks without scripted fixtures, shots or cuts. Control stops only combat before the setup window and restarts it
before cleanup; active leaves it running. `stop combat` follows the existing OnStop hook-removal/cleanup path.
Run the checks in separate restored verifier invocations to avoid reusing the first case's loaded scene. Their
commands and generated files pass scoped offline checks; neither probe has run in game.

Pre-slot review: the generated headers now require separate `verify-local -Restore -StopOnFailure` invocations,
never standalone/KeepInstall. God mode, event capture, wanted level, time/weather and initial combat running state
are not snapshotted or restored within the live session. Verifier `finally` stops its owned game after installation
and restores the backup; inspect both termination and restoration receipts before the next case. A killed host or
failed rollback cannot supply that guarantee. Neither enabled config flag is changed by the setup fixtures.
Blood loop handles now enter the module FX ledger; failed stops remain counted through clear and restart.

Hypothesis: the latest dismember crash is a setup/runtime interaction rather than evidence of a particular bone
matrix error. Full `20261001-103314-0b4f558` timed out at `weather 1` and exited while `gore clear` was pending,
before the first fixture or cut. `crash-20261001-104903.txt` reports an access violation in FusionFix+0xA24E0 with
`liberty_phase=none`; that identifies the fault location, not its cause. The preserved latest stall reports
5046 ms in `module.arsenal`; it predates the 103314 run and is not proof that its crash had the same cause.
If both cases fail before scripted violence, pursue shared scene/weather/startup evidence. If only active fails,
combat participation warrants a narrower next experiment; one intermittent pair is not a causal proof. Do not
change matrices, natives, plugins or shared host tools from these observations. A fresh quick dismember probe is
conditional on reviewing this pair and receiving the next slot, not an automatic retry.

The 0.8 ms average / 4 ms peak criteria remain unchanged. Latest firefight receipt reports average 0.484 ms,
peak 45.373 ms (FAIL), with clone-spawn scope max 10.5 ms, sever-collapse max 10.6 ms and damage max 34.1 ms.
The clone-spawn scope wraps the whole ThrowLimb operation; it does not isolate CreatePed by itself. A synchronous
native call cannot be divided across ticks after entering it; staggering operations cannot fix a single-call
peak above 4 ms. Further attribution/repair needs the next milestone, not relaxed thresholds.

Failed and unavailable runs remain recorded. No new build, launch, installation or rollback occurred in this
offline milestone; both task cards stay NEEDS-PLAYTEST. See [the live handoff](../handoffs/Lane-C-live.md).
