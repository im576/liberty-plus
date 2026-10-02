# T-044 — Physical weapons: 2 long guns + 1 sidearm

Status: **NEEDS-PLAYTEST** · Lane B · Depends on: T-040 (audit) · Design: STAGE1 section 7 Slice A "Physical weapons", 12.1,
10 Pillar 3

## Goal

Turn the Arsenal/holster/sling test features into the production physical loadout: **2 long guns + 1 sidearm**, one
long gun equipped at a time, the other slung and visible; everything else lives in the trunk or safehouse.

## Starting point (read first)

`src/LibertyFramework/Arsenal/` (`ArsenalCore.cs`, `Logic/ArsenalPolicy.cs`, `Logic/CategoryRule.cs`,
`Holsters/HolsterController.cs`, `Holsters/Logic/*`), `config/arsenal.json`, `config/holsters.json`,
`config/models/sling.json`, scenarios `sling-review` and `trunk-review`. The current policy carries more slots
(two sidearms, melee): change it through config and `CategoryRule`, not a rewrite.

## Scope

- Loadout rules from config: 1 sidearm, 2 long guns, limited carried ammunition. Melee and thrown weapons keep their
  current rules unless the owner decides otherwise (open question below). Overflow and ownership rules unchanged.
- Placement per weapon size and class (back/sling), both long guns visible without intersecting each other.
- **Vehicles and cutscenes:** carried props hidden or stowed on vehicle entry and in cutscenes, restored on exit; no
  orphaned or floating props.
- **Draw/holster transitions:** use the game's own animations where they exist (research the animation dictionaries,
  rule 4); otherwise a clean hide/show timed to the weapon switch.
- **Outfits:** placement offsets per Niko outfit class so straps and guns do not clip; review screenshots per outfit.
- Mission weapons, busted/wasted and save/load keep working (Arsenal already tracks mission flags).

## Acceptance (STAGE1 Pillar 3)

- 100% visible on foot; hidden in vehicles and cutscenes; 0 orphaned props after 100 autopilot vehicle enter/exit
  cycles (new scenario `stage1-loadout-vehicles`).
- Clipping: front/side/back screenshots for every Stage 1 outfit class and weapon class; owner pass.
- Inventory integrity: 0 lost owned weapons across 50 death cycles; identical state after save/load.
- Budget with T-042's: gunplay + arsenal + holsters ≤ 1.5 ms average.

## What was built (Claude, lane B, 2026-09-30)

- **Loadout rules** (`config/arsenal.json` block `loadout`, `Arsenal/Logic/LoadoutRules.cs`, `ArsenalPolicy`): 1 sidearm
  and 2 long guns, SMGs count as long guns (they are slung; the one sidearm slot is the handgun), carried-ammunition caps
  per weapon category (proposals: handgun 150, shotgun 60, SMG 240, rifle 240, sniper 40, heavy 12; not applied in
  missions or cutscenes). The block is optional and `enabled: false` restores the old 2 + 2 + 1 rules, so an install that
  keeps an old `arsenal.json` still loads (packaging adds the block). Overflow rule unchanged: the least recently used
  weapon of a group over its limit goes to the last car's trunk or the last safehouse stash.
- **Inventory integrity fix.** A death with no safehouse known yet (none discovered, none visited) used to drop the
  owned weapons (`arsenal_loss_no_safehouse`). They now go to a stash with no address that the first safehouse Niko
  reaches adopts (`arsenal_unassigned_adopted`). Found by the 50-death-cycle scenario.
- **Holsters** (`HolsterController`): props react in the frame of the engine events (`PlayerWeaponChanged`, vehicle
  enter/exit, `CutsceneChanged`) instead of the next 50 ms tick, the weapon in hand is read live, cutscene hiding comes
  from the event (no native polling), props are cleared when the module stops (mod-off runs used to leave them on Niko),
  and there is a `holsters` command (`status`, `outfits`, `outfit <component> <drawable> [texture]`, `outfit restore`).
- **Draw/holster transitions.** The game's own draw animation is what plays when a weapon is selected; the prop of the
  weapon that goes into Niko's hands is removed and the previous one appears in the same frame the engine reports the
  change (no animation dictionary exists for slinging; none was invented).
- **Outfits.** Niko has 17 upper-body drawables (component 1; `LibertyModel outfits` on `playerped.rpf`, and the game
  accepted 0 to 16 in the outfit scenario). The torso stands 0.101 to 0.145 m behind `Char_Spine2` at the gun's height.
  The `bulky` class (0.13 m or more: drawables 0, 1, 2, 3, 8, 9, 14) moves both slung guns 3 cm further back
  (`outfitClasses` and `loadoutPlacements` in `config/holsters.json`; a missing block in an installed file is added by
  packaging). Placement specificity: outfit class, then model, then weapon category, then the slot default.
- **Arsenal cost.** State polling default 200 → 500 ms and direct natives in the storage check: arsenal 1.3 → 0.53 ms
  average.
- **Tools and tests.** Autopilot commands `strip`, `buy`, `die`, `enter`, `leave`, `cycle-vehicle`, `cycle-weapons`,
  `cycle-deaths`, `spawncar ... [heading]`, a reusable review camera; engine command `arsenal` (`status`, `roundtrip`);
  `tools/verify/LoadoutChecks.cs` (limits, caps, overrides, 50 death cycles of the loss rule, save/load identity, placement
  specificity); `tools/perf/New-LoadoutScenarios.ps1` (outfit and weapon-class reviews) with
  `tools/tests/LoadoutScenarios.Tests.ps1`; `LibertyModel outfits` (torso thickness per outfit piece).
- **Queue checks** (`tests/local/checks.json`): `T044-loadout-vehicles`, `-weapons`, `-deaths-a`, `-deaths-b`,
  `-outfits`, `-review`.

## Evidence (this PC, 2026-09-30; machine shared with other sessions, frame times 30 to 110 ms)

Official runs with `tools/verify-local.ps1` (results-local `20260930-150531-8c4e409` and `20260930-163234-9647ab4`):

- `T044-loadout-vehicles`: PASS (NEEDS-REVIEW only for one 5 s `engine_stall` in the world-build phase, a streaming stall
  during the run), 100 cycles: `hidden_failures=0 restored_failures=0 enter_timeouts=0 leave_timeouts=0 object_growth=-3`.
- `T044-loadout-weapons`: passed. A third long gun moved the least recently used one out (`arsenal_overflow id=10`), 300
  rifle rounds were taken back to 240, 12 weapon switches consistent, latency 59 to 128 ms average across runs (worst
  141 ms), always within 1 frame of the engine event (frames were 30 to 115 ms long).
- `T044-loadout-deaths-a` / `-b`: PASS, 50 death cycles, `lost_owned=0 failed_cycles=0`, stash growth equal to the owned
  weapons carried at death, `arsenal_roundtrip identical=True` after each half.
- `T044-loadout-outfits`: passed, all 17 upper-body drawables classified as configured, 51 screenshots (front, side, back).
- `T044-loadout-review`: passed, 18 screenshots (3 class pairs × slim/bulky × 3 views).
- Offline: `tools/verify.ps1` 962 passed, `Run-Tests.ps1` 180 passed, content self-test 364 passed, `checks.py` ok,
  `artq.py validate` ok.
- Budget (idle, full loadout carried): gunplay 1.27 ms, arsenal 0.53 to 0.82 ms (was 1.31 before the polling change),
  holsters 0.08 ms. Arsenal + holsters together are at most 0.9 ms; gunplay is lane A's (T-042) and alone uses most of the
  1.5 ms budget, so the combined budget is not met by T-044's work alone.
- NEEDS OWNER: the clipping judgement on the screenshots, the feel of draw/holster timing.
## Open questions

1. Melee and thrown weapons: keep one melee slot and uncounted thrown weapons (current rule)? Owner to decide; not
   changed here.
2. SMGs (Uzi, MP5) count as long guns in this build (slung, sharing the two long-gun slots). If the owner prefers them as
   the sidearm, change `categoryOverrides` (SMG → `sidearm`, body slot 1) and `sidearmLimit`; no code change.
3. Ammunition caps are proposals (see above).
4. Weapons left in the unassigned stash (death before any safehouse is known) become available only after Niko reaches a
   safehouse; is that acceptable, or should the car trunk take them?
5. Placement of the SMG and the shotgun is the rifle's (only the `bulky` outfit class differs); the owner's review of the
   screenshots decides whether per-weapon-class offsets are needed.

## Human test steps

1. Start a free-roam save. Give yourself a pistol, a shotgun and an AK (DevTools weapons, or buy them). You carry the
   pistol (right thigh or in hand) and two long guns slung on your back with two leather straps. Walk, run, crouch and turn the
   camera around Niko: say where a gun or strap pokes through the body or floats off it.
2. Pick up or buy a third long gun (an SMG counts as a long gun). The oldest-used long gun disappears from you and is in
   the trunk of your last car (or the safehouse stash). Niko never carries more than one sidearm and two long guns.
3. Press the weapon-change input a few times: the gun in your hands leaves your back in the same instant and the other
   reappears on it. Nothing doubles or stays floating.
4. Get into any car, drive, get out; repeat a few times. Nothing is on you inside the car; both guns and straps are back as you
   step out. Do the same during a cutscene (start a story mission): nothing should be visible.
5. Change clothes in a store (upper body): a puffy jacket and a thin shirt. The guns should sit clear of the body in both;
   tell me which outfits clip.
6. Buy a lot of rifle ammunition: the total stays at 240 (a line `arsenal_ammo_capped` appears in the log).
7. Die (wasted) with a bought weapon, then open the safehouse or trunk stash: the weapon is there.
8. Review the screenshots of the queue checks (`T044-loadout-outfits`, `T044-loadout-review`, `T044-loadout-vehicles`,
   `T044-loadout-weapons`) and say pass or fail per weapon class and outfit.
