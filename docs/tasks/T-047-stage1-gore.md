# T-047 — Harsh gore (exact-damage driven)

Status: **READY** · Lane C · Depends on: T-040; blood pools/trails/surface blood wait for T-051 (R3)
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

Fill in when done.
