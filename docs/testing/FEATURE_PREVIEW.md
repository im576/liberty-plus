# Gameplay preview — October 1, 2026

Limited owner preview, based on reviewed main `aa6d463`, separate branch `codex/feature-preview-2026-10-01`.
Exact tested commit/package hashes/run and final installation backup are in the preview receipt.

## Contents

- Six Stage 1 catalog weapons: Glock 17, .44 AutoMag, Street Sweeper, Remington 1100, IMI Uzi and AK-47;
  existing installed models, configured identity stats and Liberty recoil/spread profiles.
- Free aim, class reticles and shoulder swap.
- Two long guns plus one sidearm, SMGs as long guns; visible holsters/slings and configured ammo caps.
- Existing integrated wheel and trunk store/take/swap/capacity interface.
- DevTools weapons, teleports and live tuning; vanilla health/ammo/radar/story HUD behavior.
- Restricted P90/MG36/sniper catalog entries remain obtainable in DevTools with existing vanilla behavior,
  outside normal Stage 1 availability.

Preview-only change: `engine.json.disabledModules` is `combat, atmosphere`. Unvalidated Liberty gore/dismemberment/
combat effects and atmosphere are excluded; vanilla game effects remain. Density remains OFF; budgets unchanged.
Unmerged B timing/trunk observations, C cleanup, D HUD and R research fixtures are not imported. Remaster is deferred.
Existing base mods remain; models depend on the already installed Realistic Weapon Overhaul.

## Controls

After the receipt says installed, launch GTA IV normally and load a free-roam save.

| Action | Keyboard/mouse | Controller |
|---|---|---|
| DevTools open/close | F10 | Hold L3+R3 about 0.7 s |
| DevTools navigation | Arrows; Enter select/confirm; Backspace back | D-pad; A confirm; B back |
| Give catalog weapon | DevTools > WEAPONS > Give marked Stage 1 | Same menu |
| Range | DevTools > TELEPORT > gun test range | Same menu |
| Aim/fire | Right mouse / left mouse | LT / RT |
| Shoulder swap while aiming | Z | LB |
| Hold wheel | Hold Tab; Left/Right; release Tab | Hold Back/View; right stick/D-pad; release |
| Sticky wheel | Tap Tab; Left/Right; Enter; Backspace cancel | Tap Back/View; select; A; B cancel |
| Open trunk behind car | E | X |
| Store highlighted carried gun | Space | X |
| Choose stored gun | Page Up/Down | LB/RB |
| Take/swap stored gun | Enter | A |
| Close trunk | Backspace | B |

## Short test session

1. Give a pistol, a shotgun and AK/Uzi. Check slings/holsters and the 2+1 carry limit.
   Try the other catalog weapons one at a time so the carry limit does not conceal a selection.
2. At the range, try single shots, three-shot bursts, sustained fire and recovery. Aim and swap shoulders;
   report camera clipping in cover or beside walls.
3. Test held/sticky wheel selection and cancellation.
4. Park a car, store a gun, take it back and compare ammo. Try a swap with both long-gun slots full.
   Report wrong inventory, stuck controls, unreadable text or close behavior.
5. Optional: four-slot sports-car refusal and real save/load in a separate test save. These remain owner checks.

Report weapon/action, expected versus observed result, keyboard or pad and approximate time for log lookup.
Longer reports: PLAYTEST_REPORT_TEMPLATE.md. This preview does not mark tasks DONE.

## Validation and limits

Full-mode `T040-feature-preview-smoke` checks catalog/module availability, keyboard wheel selection,
live shoulder-table probes, trunk store/take ammo and close/control release, with four screenshots.
This limited functional smoke does not certify original UI timing/performance budgets, swap/capacity,
physical controller, real save/load, mission/cutscene, long stability or owner feel. Original full task checks stay intact.
The parent backs up the owner state folder before smoke and restores it after verifier shutdown/restoration.
Only reviewed successful smoke is installed. The receipt includes the exact rollback command.
Other lanes remain offline while the owner preview is installed.
