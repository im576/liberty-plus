# T-049 — Basic Liberty HUD

Status: **READY** · Lane D · Depends on: T-040 · Design: STAGE1 section 7 Slice A "Basic Liberty HUD", 10 Pillar 1;
radar stays vanilla until T-054 (R5); final styling in Slice C

## Goal

Contextual health, armour, ammunition, wanted level and interaction prompts drawn by Liberty.Ui in the Liberty
Vanilla+ language, shown when relevant instead of permanently covering the screen.

## Scope

- **Find out first** (rule 4) which vanilla HUD elements can be hidden individually without hiding the radar:
  existing hud.dat global work (ADR-0004, `docs/game-api/MEMORY.md`), `DISPLAY_HUD`/`DISPLAY_RADAR`-type natives in the
  registry. Hide only what Liberty replaces; if an element cannot be hidden alone, keep the vanilla one and do not
  draw a duplicate. Record the finding in `docs/research/`.
- Liberty elements: health and armour (show on change, damage, low health, combat), ammo clip/reserve (while armed or
  reloading), wanted level (while wanted), interaction prompts (reuse the IV-style help box from the Arsenal).
  Fade in/out timings in config.
- Owner-directed placement (ART-007 r2): current-weapon silhouette and ammo, with compact health and armour indicators
  directly below, are anchored at the **top right**. Keep the vanilla radar at the **bottom left**. Preserve the
  understated GTA IV-era visual language with original artwork; menu panels must not overlap either HUD anchor.
- Coexists with the reticle (T-043) and weapon wheel (T-045); one shared palette/typography definition in config that
  Slice C will finalise.
- Controller and keyboard/mouse prompts show the right glyphs/keys.

## Acceptance (STAGE1 Pillar 1)

- Every element appears when its condition holds and fades when it does not (scenario `stage1-hud` walks through
  damage, low health, armed, reload, wanted, prompt; screenshots).
- UI draw ≤ 0.5 ms average (all Liberty UI combined); smallest text ≥ 14 px at 720p virtual.
- Switching the HUD off in config restores the complete vanilla HUD.
- Screenshots verify the compact weapon/ammo/health/armour group is at the **top right**, the radar remains
  **bottom left**, and the weapon wheel/trunk panel does not cover either group. No lower-corner HUD relocation.

## Human test steps

Fill in when done.
