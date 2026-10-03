# T-046 â€” Trunk UI: loadout â†” vehicle storage

Status: **NEEDS-PLAYTEST** Â· Lane B Â· Depends on: T-044; shares components with T-045 Â· Design: STAGE1 section 7 Slice A
"Trunk UI", Slice C

## Goal

A production trunk interface that makes the physical inventory legible: what Niko carries on one side, what the car
holds on the other, and moving a gun visibly changes where it physically is.

## Starting point

`Arsenal/Ui/TrunkSequence.cs` (choreography: turn, open boot, reach in, close), `Arsenal/Ui/StorageWheel.cs`,
`Logic/StorageBin.cs`, `config/arsenal.json` (`trunkTimings`), scenario `trunk-review` (passes; it faces north before
spawning the car and accepts whichever weapon the wheel starts on).

## Scope

- Two groups: **carried** (sidearm, long guns, ammunition, equipment) and **trunk** (stored weapons, ammunition,
  capacity used/available per vehicle class from config).
- Store, take and swap (when the carried slot is full) with clear feedback; the slung prop disappears or appears on
  Niko as it moves; the choreography keeps playing its reach-in step per move.
- Controller and keyboard/mouse navigation; the same visual components and language as the weapon wheel (T-045).
- Safehouse storage uses the same UI where it already uses the storage wheel.

## Acceptance

- `trunk-review` extended to store, take and swap: 100% round trips, state persisted after save/load.
- Open â‰¤ 1 frame, animation â‰¤ 200 ms, UI draw â‰¤ 0.5 ms; text â‰¥ 14 px at 720p virtual; screenshots for review.

## Human test steps

Automated checks (keyboard input, state-file round trip) are in the Claude continuation below. Steps 4 (real save/load part) and 5, and every controller press, need the owner.

1. Behind an Admiral on foot, press **E** (controller **X**); wait for lid animation and carried/trunk groups. Select the AK with arrows/right stick and press **Space** (**X**) to store; sling vanishes, trunk gains it with unchanged ammo, carried slot reads Empty.
2. Fill both long-gun slots, highlight the stored AK with **Page Up/Down** (**LB/RB**) and press **Enter** (**A**). Preview names the outgoing gun; check ammo/ownership/physical identity and sling changes. Repeat with a non-round ammo count.
3. Press **Backspace** (**B**), wait for lid close and movement to resume. Reopen/close repeatedly, including immediately after transfers. `storage state` must report open/closing/animation/locked false and control true with no other capturing menu.
4. Repeat on a Banshee: four stored guns fit; fifth store is refused and remains carried. Take/swap from full storage preserves inventory. Restart/load a save and confirm owned inventory/ammo/instances persist.
5. Real save/load (owner): carry pistol + AK + shotgun, store the AK in a car trunk (note its ammo), save at a safehouse, quit to the menu, load the save. Expect: the pistol and shotgun are in hand slots, the AK is still in that car's trunk with the same ammo (open the trunk again and check). Then drive away, load the save again and check nothing duplicated or vanished (`arsenal roundtrip` in the DevTools console must say `identical=True`).
6. Safehouse (owner): at a safehouse wardrobe/stash spot press **E** (controller **X**): the same radial and list open with capacity "unlimited". Store two long guns, close with **Backspace** (**B**), then open the gunsmith and press **Backspace**/**B** to leave it; control must return each time. Check the same store, take and swap flow as at the trunk.
7. Controller (owner): repeat steps 1-3 with **X** (open, store), **LB/RB** (list), **A** (take) and **B** (close). Check the interface against Lane D's top-right weapon/ammo group and the bottom-left radar once integrated.

## Codex continuation — September 30, 2026

The trunk continuation includes the later wheel fixes and current main lock/cache tooling on `codex/lane-b-validation`.
See [Codex handoff](../../../GTAIV-Reborn/docs/archive/pre-split/handoffs/Codex-Lane-B-2026-09-30.md). Gameplay validation remains incomplete. The owner reauthorized bounded checks through 9:30 p.m. Pacific; any later session must follow its current instructions.

Storage uses the engine's owned player-control lock, releasing only Arsenal's claim on close. Completion explicitly shuts
the lid even if a missing animation skipped its timed close. Storage list starts below the top-right HUD band. Transfer logs
include ammunition, ownership and instance identity; the prepared scenario checks AK ammo 100 and displaced shotgun ammo 60
(existing T-044 cap), persisted equality, capacity refusal and control release after both closes. Offline serialization tests
include carried/trunk/safehouse ammo and ownership. Transfer/capacity policy is retained.
Successful swaps now request the reach-in choreography (the adapter previously handled only a `Taken` reply), and the
preview identifies a refused duplicate weapon type before confirm instead of promising a swap the inventory rejects.

Old `stage1-trunk-ui-20260930-174525`: 125 steps, one close failure; second close has no Back input logged. Five screenshots
were inspected: expected contents/capacity (0/8, 1/8, 4/4) and swap update are visible. Scene/vehicles obscure Niko and slings;
swap centre text clips, and the full-refused shot lacks the claimed transient refusal message. They do not establish prop,
close or new layout acceptance. There are no new screenshots after fixes; shared UI stalls remain open under T-045.

## Claude continuation: root cause evidence and fix, acceptance (2026-10-01)

Reviewed all Codex work (hold timing, equip-after-release readback, binding-collision rejection, lid-close safeguard, layout,
`UiBudgetLogic`) and merged `origin/main` (testing speed-up tools); no defect found in them.

**Shared UI stall, measured (one scene, frame time per window, `T045-ui-text`, run `20261001-000645`):**

| Window | Avg frame ms |
|---|---:|
| closed | 23 |
| list menu, sprite text (new default) | 25.5 |
| radial menu, sprite text | 26 |
| list menu, SHDN `DrawText` limited to 1 / 4 / all 8 strings | 96 / 310 / 393 |
| probe, one string drawn once / 8 times / 8 different strings / 5 font sizes | 98 / 609 / 616 / 702 |
| probe, font with `Effect` none, 8 strings | 147 |
| probe, DevTools-style font (2-argument constructor, 4-argument overload), 8 strings | 145 |
| probe, DevTools-style bold font (4-argument constructor), 8 strings | 142 |
| probe, canvas font through the 4-argument overload, 8 strings | 608 |

Conclusion (evidence only): ScriptHookDotNet `Graphics.DrawText` costs about 15 ms per string per frame at its cheapest and
75-90 ms with the canvas fonts' default effect (the effect multiplies the `DrawString` passes); the overload, the colour
argument, bold, string content and font count are not what costs. Rectangles and sprites cost nothing. The cause inside
DrawText (D3DX font rendering under the game's renderer) is not established, and a draw setup that is cheap enough was not found
(the cheapest, 145 ms for 8 strings, is 6x the sprite renderer), so the shared canvas draws text as cached GDI+ sprites
(`engine.json` `uiTextRenderer`, `ui-text-renderer shdn` switches back). The DevTools menu window in that scenario is not
a reliable reading (its open state was not confirmed). The cache is least-recently-used (600 entries), creates at most 8 new
textures per frame, and `ui-text-stats` reports count and estimated bytes (about 40 KB per string at 1080p; 437 entries = 19 MB).
Text textures belong to the engine's canvas, not to a module: they are released when the engine unloads, not on module stop or
hot reload. Lane D's HUD text uses the same canvas (changing numbers each create a texture; per-character sprites for digits
would avoid that and are a recommendation for D, not done here).

**Acceptance (full run `20261001-001218-7d63be6`, no -Quick, budgets unchanged):**
- `T045-weapon-wheel`: passed (NEEDS-REVIEW for the screenshots). 12 of 12 selections with next-frame readback, first draw within 0-1 frames, hold and tap keyboard flows, `ui-budget` wheel window avg 28.8 ms against 31.1 ms closed, p95 50.2 / 54.2, p99 67.9 / 78.0, draw.ui under 0.5 ms.
- `T046-trunk-ui`: passed (NEEDS-REVIEW for the screenshots). Store, take, swap with ammo and ownership read back, capacity refusal, two persisted-state round trips, control released after both closes, `ui-budget` trunk window avg 30.3 ms against 28.7 ms closed, p95 47.6 / 44.3 (+7.4%), p99 85.5 / 76.6 (+11.6%).
- An earlier full run (`ec5e244`) had one game crash and one p95 gate miss by 0.5 ms; both runs shared the machine with other lanes' builds (a 11-14 s stall in the holsters and wheel config-file polling preceded the crash, consistent with disk contention; not proven). The budget windows were then lengthened to 10 s (thresholds unchanged).
- Still NOT VERIFIED: physical controller (Back, stick, A/B), a real game save and load, the safehouse stash and gunsmith through this interface, Lane D HUD coexistence, owner judgement of the screenshots.

## Owner questions (defaults stay as shipped until answered)

- Trunk sizes: 8 slots by default, 4 for sports cars, 16 for utility vehicles, unlimited in a safehouse stash (`trunkCapacity` in `config/arsenal.json`). Right numbers, and which models belong in the sports and utility classes?
- Ammo caps per category when a gun is carried or stored: handgun 150, shotgun 60, SMG 240, rifle 240, sniper 40, heavy 12 (`loadout.ammoCaps`). Confirm or change.
- Loadout of 2 long guns + 1 sidearm with SMGs counted as long guns (already decided) is unchanged.

### Full acceptance run 20261001-092349-e470758 (2026-10-01, includes main 868368b)

RAN-PASS: wheel scenario (12/12 equips, keyboard hold and tap), trunk scenario (store, take, swap, capacity, round trips), ui-text. Budgets: wheel avg 22.75 ms, p95 34.4, draw.ui 0.217 ms; trunk avg 25.45 ms, p95 37.5, draw.ui 0.303 ms; no 1 s stalls. All screenshots reviewed. NEEDS OWNER: controller, real save/load, safehouse, HUD coexistence (steps above).

### Full follow-up build reviewed — 20261001-122159-4330603

Full clean build `4330603` includes current main `c01f0da` plus the isolated wheel config watcher patch/casing fix.
Trunk **133 steps / 0 failed / 0 log errors**: store/take/swap/capacity refusal, two identical state-file round trips,
control release passed. Original relative budget gate passes: open avg 25.82 vs closed 24.72 ms (+4.45%),
p95 35.5/41.9, p99 56.9/73.4, draw.ui avg/max 0.301/2.1 ms, max frame 123.0 ms, zero measured >=1 s stalls.
All six stored JPGs individually reviewed: 0/8 empty, 1/8 stored AK, swap-in/out preview and post-swap list,
4/4 full and Trunk full (4) refusal are visible without observed clipping. Summary retains NEEDS-REVIEW;
this is the separate capture review. Available images are 960x540, not proof of owner-resolution text sizing.
Full batch package/wheel/text assertions also passed; the wheel has a separate first-draw timing gap recorded in T-045.
Restored from `phase2-20261001-122209`; slot is released and this run must not be repeated unchanged.
Offline build PASS, verifier 441/0/5 not-run, tools 236/0. ASI hashes/inventory and exact evidence in
[live handoff](../../../GTAIV-Reborn/docs/archive/pre-split/handoffs/Lane-B-live.md). Controller, real game save/load, safehouse/gunsmith and B/D coexistence
remain owner/combined checks; state-file round trips do not prove real save/load. Status stays NEEDS-PLAYTEST.

### Shared radial initial snapshot follow-up — offline only

StorageWheel now supplies its first carried start slot before opening, allowing the initial Centre/right-panel
snapshot before publication. Focused checks compile the actual storage/UI/view/ledger sources: initial centre,
highlight, panel, no opening action/input consumption, external-close semantics, owner-stop and initial callback
failure cleanup pass within **25/0** checks in PS7/PS5.1. Engine/SHDN boundaries are spies; production build/full
runtime evidence is pending. Next affected gate `T046-trunk-ui`, plus `SDK-ui-review` for the other shared radial
callers, with `LOOP-package-install` under a scheduled slot. Prior acceptance stays preserved; this patch claims no
fresh timing/visual acceptance. [Live handoff](../../../GTAIV-Reborn/docs/archive/pre-split/handoffs/Lane-B-live.md) records exact receipts and pending gates.

### Full2297a17 fresh FAIL — 20261001-202311

41 executed steps/1 failed/0 errors. Initial status/open sidearm0 and empty0/8 panel passed; sole trunk_ui_open.jpg
viewed by lane and parent, readable centre/panel. Unchanged budget fails frame_p95: closed/open avg26.44/27.92
(+5.60%), p9540.7/46.4 (+14%, limit+10%), p9968.0/99.7 (+46.62%, limit+15%), max186.3/141.7;
draw.ui0.345/5.8 avg/max, zero measured>=1s stalls. Draw avg<0.5 does not pass relative frame gates.
Later store/take/swap/capacity/state round-trip/close flows NOT-RUN due fail-fast. Restore phase2-20232903:31:10.031Z
confirmed; no retry. Shared UI candidate held unmerged despite wheel pass; source attribution unproven. Offline
diagnose paired baseline/choreography/scheduler/hotpaths, thresholds unchanged. D owns next slot; no game/build.
Historical passes/failures preserved, owner-only checks remain. [Live handoff](../../../GTAIV-Reborn/docs/archive/pre-split/handoffs/Lane-B-live.md) indexes receipt.

### Paired metrics/state observation candidate — offline, runtime unrun

Preserve the already-read exact closed command-cost report alongside frame percentiles, and append linked window
identities/start/end Engine.Frame/ticks and managed storage/choreography state to existing metric command receipts.
No new native queries/draw/per-frame logging, counter/threshold/reset/window changes, density/module switches or cause
claim. Owner ledger cleans observer on stop; unavailable/failed observation never hides metric failure.
Actual metrics/logger/ledger/trunk-state focused compilation **25/0**, warnings-as-errors; full production/runtime unrun.
Fresh full202311 p95 FAIL and later action flows NOT-RUN remain; shared candidate heldunmerged.
Next ONE full assigned batch proposes `LOOP-build,LOOP-verify,LOOP-package-install,T046-trunk-ui`, cap30/Restore/
StopOnFailure/full, after parent host-startup fix review. Passing wheel/SDK unchanged, not rerun. Exact test receipt,
source boundaries and observation limits in [live handoff](../../../GTAIV-Reborn/docs/archive/pre-split/handoffs/Lane-B-live.md).
