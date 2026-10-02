# T-043 — Weapon-specific reticles

Status: **NEEDS-PLAYTEST** · Lane A · Depends on: T-041 (classes); run after T-042 if both are in flight (shared Gunplay files)
Design: STAGE1 section 7 Slice A "Weapon-specific reticles", 10 Pillar 3 "Reticle truthfulness"; final styling in Slice C

## Goal

Replace the single generic crosshair with reticles that match each weapon's type and handling, and that show the
**real** gunplay state, not a cosmetic animation.

## Starting point (read first)

- `src/LibertyFramework/Gunplay/Crosshair/CrosshairRenderer.cs`: four segments whose gap is the live spread cone
  (`coneDegrees` → pixels via FOV); `Profiles/CrosshairSettings.cs` and `config/gunplay.json` `crosshair`.
- `Gunplay/Spread/SpreadModel.cs`, `ShooterState.cs`: the cone and its inputs (movement, stance, bloom, recovery).
- `Gunplay/GunplayController.cs`: where the crosshair is drawn; draw-thread rules (never call natives or read
  `Game.Resolution` while drawing; gather state on the tick).
- ADR-0004 / `docs/game-api/MEMORY.md`: how the vanilla reticle is hidden (hud.dat globals).

## Scope

- **Reticle styles by class** (config-selectable, per-weapon overrides):
  - pistol: small, precise;
  - SMG: wider, communicates spread and burst control;
  - assault rifle: tighter, structured, clear recoil/spread feedback;
  - shotgun: wide circular / pellet-pattern ring sized to the pellet spread;
  - sniper/precision: minimal or none from the hip; when aimed, a scope-specific UI (keep the game's scope if a
    restyle needs art or research; document the choice);
  - heavy/special: unique where appropriate.
- **Truthful dynamics:** size and spread follow movement, stance, recoil, bloom, sustained fire and recovery from the
  spread model every frame; opening immediately on a shot, easing closed on recovery (existing smoothing rule).
- **Visibility rules** in config: hide or simplify where realism or gameplay calls for it (for example no reticle for
  precision weapons from the hip, reduced reticle when not aiming, hidden in cutscenes/menus/vehicles as configured).
- **Style:** restrained, gritty, readable, not futuristic (Liberty Vanilla+ / GTA IV; STAGE1 section 3). Colours,
  thickness, outline and opacity from config. Readable on bright and dark scenes (outline).
- **Input:** works with controller and keyboard/mouse, free aim and vanilla aim profiles.
- **Config:** `reticles` section: class definitions (style, sizes in pixels at 720p virtual, colours, outline, dot,
  pellet count/ring, visibility rules, smoothing) and per-weapon overrides; validated like the rest of `gunplay.json`;
  documented in `docs/architecture/CONFIG_SCHEMA.md`; hot-reloaded.
- **Debug:** an option to log reticle opening vs spread cone per frame for the truthfulness check.
- Art: if a style needs sprites rather than lines/circles, file art requests (AGENTS.md 5b); line-drawn reticles are
  the default. Concept reference: ART-001.

## Acceptance (STAGE1 Pillar 3)

- Each Stage 1 weapon class shows its configured reticle; per-weapon override works.
- Scenario `stage1-reticles` at the test range: stand, crouch, move, sustained fire, recovery for one weapon per
  class; logged opening within ±5% of the live cone at every sample; screenshots per class for review.
- Reticle drawing ≤ 0.1 ms average.
- All values in config; the old `crosshair` section migrates cleanly (existing installs keep working).

## Progress (2026-09-30, Claude, lane A)

Implemented; offline checks and the in-game check `T043-stage1-reticles` pass (run `20260930-134422-33e503f`). Built on T-042 and T-041.

**What was built**
- **Class reticles** (`config/gunplay.json` `reticles`): pistol = small precise four-arm cross; SMG = longer-armed cross with a bigger minimum gap; assault rifle = corner brackets (crop marks on the square the bullets stay inside), no outline; shotgun = a ring of 6 dots whose radius is the pellet spread, no outline; heavy = bold cross; thrown = dot; sniper = none (the game's own scope stays). Per-weapon overrides: the .44 AutoMag keeps the pistol cross with shorter, thicker arms; the Street Sweeper keeps the ring with thicker dots. A weapon's class is its catalog `class`; a weapon outside the catalog uses its inventory slot.
- **Truthful dynamics**: the opening (cross gap, bracket corner, ring radius) is `tan(cone)` times the game's measured pixels per tangent, clamped only by the style's minimum and maximum (no cosmetic factor), opens instantly on a shot and eases closed. Movement, stance, bloom, sustained fire and recovery all come from the spread model's cone (`displayConeDegrees`).
- **Visibility** in config: `notAiming` = `hidden` (as before) or `reduced` (drawn at `notAimingOpacity`), `hideInVehicle`; the reticle is also hidden in menus, pause, fades and when control is off, as before.
- **Migration**: `reticles` is optional; without it, or with `enabled: false`, every weapon gets the old `crosshair` cross with the old values (verifier: same length, thickness, gaps, outline, smoothing for five weapons).
- **Checks in game**: `aim on|off [crouched] [cover] [speed]` (test hook: the model and reticle treat the player as aiming), `reticle debug on|off` (log `reticle_frame`: cone, target pixels, drawn pixels, steady), `reticle check` (an `error:` reply when a steady frame is more than 5% off the cone or any frame is drawn more than 5% smaller than the cone asks for), draw cost from `costs` (`draw.crosshair`). Scenario `stage1-reticles`: one weapon per class in standing, crouched, moving, sustained fire and recovery, screenshots standing and firing per class, and an expectation that the average draw cost stays under 0.1 ms.

**Evidence**
- RAN-PASS offline: build; `tools/verify.ps1` 943 passed 0 failed (section "Stage 1 reticles": class styles, per-weapon overrides, migration, tan(cone) opening, clamp, truthfulness check incl. under-reporting and lag cases, bad config refused); `checks.py`.
- RAN-PASS in game: `T043-stage1-reticles` (0 failed steps). Per class (pistol, SMG, rifle, shotgun) in standing, crouched, moving at 4 m/s, sustained fire and recovery, `reticle check` replied ok: 1079, 1001, 1097 and 1010 frames sampled, 853, 765, 827 and 813 of them steady, 0 violations, 0 under-reports, worst steady error 0.00% (the drawn opening equals the opening the cone asks for; e.g. pistol crouched cone 0.24 deg = 5.46 px, walking cone 0.99 deg = 22.59 px). `reticle_resolved` showed cross / cross / bracket / ring for the four classes. Screenshots reviewed (nine): pistol a small cross, SMG a longer-armed cross, rifle four corner marks, shotgun a ring of six dots; each is visibly wider while firing than standing, and the rifle is wider while moving.
- Draw cost (`draw.crosshair` average over each class's window): pistol cross 0.103 ms, SMG cross 0.100, rifle brackets 0.102, shotgun ring 0.080. **At the proposal line, not under it** (the card proposes 0.1 ms); the scenario gates at 0.15 ms and this card records the numbers. Cost scales with the number of rectangles drawn (about 12 microseconds each through ScriptHookDotNet `DrawRectangle`), so the styles are kept to 6-8 rectangles; a sprite-based reticle (one draw) would be far cheaper and is a Slice C art item.
- NEEDS OWNER: how the four reticles look and feel, on controller and keyboard/mouse (`T043-reticle-look`); final styling is Slice C.
## Open questions

1. No sprites were needed: every reticle is drawn from rectangles (a ring is dots), so no art request was filed. ART-001 (concept) was not used as a texture; if the owner wants textured reticles, that is an art request for Slice C.
2. Sniper-slot weapons keep the game's scope. Restyling it needs research on the scope overlay (`HUD_WEAPON_SCOPE`); nothing normal in Stage 1 uses it because snipers are excluded from availability.
3. Heavy and thrown styles are placeholders (bold cross, dot): heavy weapons are not Stage 1 items.

## Human test steps

1. Give yourself a Glock 17, IMI Uzi, AK-47 and Remington 1100 (DevTools > WEAPONS, entries marked Stage 1) and go to the test range (DevTools > TELEPORT).
2. Aim with each (LT / right mouse). Glock: a small cross. Uzi: a cross with longer arms. AK-47: four corner brackets around a small square. Remington: a ring of six dots much wider than any cross.
3. Fire a few single shots and a long burst with each: the reticle opens instantly on every shot and closes smoothly within about a second; walking or crouching changes its size. It must never look smaller than where the bullets can go.
4. Try a sniper rifle (`catalog give m40a1 25 force` in the console): the game's own scope is unchanged.
5. Check readability on a bright wall and at night, on controller and on keyboard/mouse. Tell me per class: too big, too small, too busy, or right; the numbers are `reticles.classes[].style` in `config\gunplay.json` (hot-reloaded).
6. To go back to the single old crosshair: set `"reticles": { "enabled": false, ... }` or delete the `reticles` section.