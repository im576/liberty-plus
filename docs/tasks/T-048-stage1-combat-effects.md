# T-048 — Contextual combat effects

Status: **READY** · Lane C · Depends on: T-041 (weapon classes); material impacts wait for T-050 (R2); new effect
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

Fill in when done.
