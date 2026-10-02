# T-049 — Basic Liberty HUD

Status: **BLOCKED** · Lane D · Depends on: T-040 · Design: STAGE1 section 7 Slice A "Basic Liberty HUD", 10 Pillar 1;
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

## Progress (2026-10-01, Claude, lane D, cloud session)

The following is the inherited offline implementation history. The latest local findings below supersede its hiding claims and unrun status.

Built and verified offline in a cloud session (no Windows, no game): **nothing below has run in the game.** The in-game checks are queued
(`T049-hud-components`, `T049-stage1-hud`, `T049-hud-look`); until `verify-local.ps1` has run them this card stays NEEDS-PLAYTEST with
those checks unproven.

**What was built**
- **Module `hud`** (`src/LibertyFramework/Hud`, `config/hud.json`, documented in CONFIG_SCHEMA): the engine tick gathers state into an immutable
  frame and `OnDraw(ICanvas)` draws it (no game calls while drawing). The group is anchored **top right** (28/24 units from the corner) in the
  order of the ART-007 r2 board: weapon silhouette, `17 / 383` ammo line right-aligned under it, thin sage-green health bar, pale blue-grey armour
  bar, wanted stars. Slots never move while elements fade. The vanilla radar stays where it is (bottom left); nothing is drawn in a lower corner.
- **Contextual** (`HudPresence`, all times in config): the weapon group appears on weapon change, shot, reload and while aiming (left trigger or right
  mouse), holds 4 s and fades; health shows on any change, damage or combat (6 s), and stays up and pulses at or below 30%; armour shows with health when
  there is some; wanted stars show while wanted and fade 1 s after; the help box (prompt) fades in and out. Hidden in cutscenes, pause, fades and when dead.
- **Prompts** (`UiService.CurrentHelp`, `HelpDrawnByHud`): `ShowHelp` texts may carry `{interact}`, `{accept}`, `{cancel}`... and show `X` or `E` according to the device used
  last (`HudInputDevice`: pad buttons/sticks/triggers against keyboard keys, with a switch delay). The Arsenal's trunk and stash prompts now use `{interact}`
  (they said "X / E" before). Without the HUD module the old box draws both names ("X / E").
- **Hiding only what Liberty replaces** (`GameAddresses.ResolveHudTable`, `HudReticle`, `HudPlan`): the hud.dat register routine's call sites give the whole
  component table; each element lists the vanilla components it replaces in `hud.json`; Liberty draws an element only if all of them are in the table and
  were hidden, otherwise the vanilla element stays and **no duplicate is drawn**. Defaults: weapon = `HUD_WEAPON_ICON` + `HUD_AMMO`, wanted = `HUD_WANTED_BACK` + `HUD_WANTED_FRONT`.
  `"enabled": false` (or `hideVanilla: "none"`) restores the complete vanilla HUD. The reticle's four components stay gunplay's.
- **Research recorded** in [HudComponents.md](../research/HudComponents.md); test and probe commands `lf hudctl ...` (CONFIG_SCHEMA).

**Evidence**
- RAN-PASS offline (cloud): `tools/cloud/test-all.sh` all 12 steps PASS, including the offline verifier `RESULT passed=419 failed=0 notrun=8` (was 347; 72 new
  checks in section "Stage 1 HUD": hud.json equals the code defaults, older/partial files, bad values refused (text under 14 px, palette, fade, names, glyph tokens),
  fade-in/hold/fade-out timing at 60 fps, top-right anchoring and bounds at four aspect ratios, no overlap with the weapon wheel, the default list menu, the prompt box,
  the lower corners or a right-centre panel as on the board, the vanilla-hiding plan in eight cases, glyph/ammo/low-clip/pulse text, the input-device rule, the Arsenal's
  prompt tokens exist in config, and the generic hud.dat table resolver against a synthetic executable image: extra components, a flagged layout, a bad name, a duplicate,
  another argument shape and a broken registration).
- NOT RUN (needs Windows and the game): everything in game. Queued: `T049-hud-components` (research: the component table and what each hide removes),
  `T049-stage1-hud` (the walk-through: change, shot, reload, damage, low health, armour, wanted, prompts on both devices, draw cost, plan before/after), `T049-hud-look` (owner).
- NEEDS OWNER: look, size and timing of every element; both input devices.

**Open questions (conservative choice made, recorded for the owner)**
1. **Health and armour bars are not drawn by default.** The vanilla health/armour arcs are part of the radar's drawing and no separable hud.dat component is known
   (reuse audit F9). The card says not to duplicate what cannot be hidden alone, so `health`/`armour` list no component and stay vanilla. `T049-hud-components`
   finds out whether a component carries the arcs; put its name in `vanillaComponents` (hot-reloaded) and the bars appear. If none does, the bars wait for the radar redraw
   (R5 / T-054) or the owner accepts a duplicate (`drawWithoutHidingVanilla: true`). The scenario draws them with `hudctl layout-test on`, so the layout is reviewable either way.
2. The names `HUD_WEAPON_ICON`, `HUD_AMMO`, `HUD_WANTED_BACK`, `HUD_WANTED_FRONT` come from the owner's hud.dat (reuse audit), but whether their registrations have the parsed
   shape and whether the game re-reads their globals every frame (as it does for the reticle) is not proven. If not, those two elements stay vanilla (log `hud_element_plan ... vanilla_kept`) and the card says what to change.
3. `GET_AMMO_IN_CHAR_WEAPON` is assumed to include the clip (reserve = total - clip, `weapon.totalIncludesClip`); the current scenario expects `17 / 133` for the existing 150-round pistol cap. This remains unverified in game.
4. Weapon icons are drawn at 2:1 (96 x 48) like the wheel's; Codex checked packaging output for ids 7/10/14: all are 256x128.
5. `DISPLAY_HUD(false)` alone was not tried as a coarser fallback: it risks removing mission and help text (HudComponents question 5).
6. No art was needed: the star is drawn in code, everything else is rectangles, text and the existing weapon icons. No art request filed (ART-007 r2 is the reference).

## Local continuation (2026-09-30, Codex)

Continuation: `codex/T-049-hud-continuation`, isolated checkout `C:/Users/IM576/GTAIV-Reborn-lane-d`, based on `origin/stage1/T-049` at `e2f8181`.
Current main, including `db08832` and `45561b4`, is already an ancestor. No B/C/research worktree was edited.
The first run `20260930-194804-e2f8181` passed Windows build/native tests and content tests, then was stopped **before installation** because the recovered
config still enabled population thinning. The continuation disables the atmosphere density governor per the owner's decision; T-040 evidence is preserved.
The component probe now suspends replacement HUD drawing to capture a real vanilla baseline, reapplies probe hides every tick, and restores them between groups.
The HUD scenario gates combined `draw.ui <=0.5 ms`, tests actual JSON config-off through normal polling, restores original bytes, and uses the existing
T-044 pistol cap (150 total: expected `17 / 133`) rather than requesting 400 rounds that the loadout caps. Layout-test remains diagnostic only.
Final offline `-NoGame`: `passed=469 failed=0 notrun=7`; tooling tests: `passed=212 failed=0`; content selftest 364/364, fixtures 5/5; art lifecycle 18/18 and 12 requests valid.
The Windows build succeeds with zero errors (SDK, 210 engine sources, both mods); native fault-containment and ray-walk tests pass. The package-flags model tool compiles with warnings as errors.
The second verifier `20260930-195611-c07dbb5` also stopped before installation after the owner prohibited in-game testing. Both summaries remain incomplete and are not gameplay evidence.
Those two interrupted batches produced no D install or gameplay evidence. The later owner-authorized batch below did install and run both scenarios.
The pre-restriction file-only executable scan resolved 31 components and 23 unparsed registrations (see HudComponents); its full verifier had 2 failures and 1 NOT-RUN and was not accepted.
Prompt text is now cached until text/device/glyph-table changes, preventing per-frame expansion and repeated unknown-token logs. Mouse buttons now select keyboard/mouse glyphs.
Offline tests cover device/config changes, fade text retention and reopening, unknown-token log suppression, and rejection of missing/over-budget/multi-ms draw samples.
Health/armour remain vanilla by default; layout-test is diagnostic only. Owner recommendation: keep the vanilla arcs until future evidence supports clean replacement, rather than approving duplicates implicitly.
Matching Claude Sonnet must review **all** Codex changes (including any uncommitted files) before continuing. Handoff: [Codex-Lane-D-2026-09-30](../handoffs/Codex-Lane-D-2026-09-30.md).

## Blocked — visible hiding (local run September 30, 2026)

The owner lifted the testing restriction; D ran `verify-local.ps1 -AnyBranch -Only T049-hud-components,T049-stage1-hud -ScenarioTimeoutMinutes 6 -NoPush -Restore -NoManual`.
Batch `20260930-205120-4ac4fcc`: install PASS; component script NEEDS-REVIEW; HUD scenario FAIL (one wanted transition expectation, 110 steps, no log errors, game alive).
Normal verifier restoration completed at 8:57 p.m. Pacific. See [local evidence report](../reports/2026-09-30-lane-d-hud-local.md) and the [handoff](../handoffs/Codex-Lane-D-2026-09-30.md).

The screenshots show vanilla wanted stars and radar still visible while the hider reports ownership. Normal HUD captures show duplicate weapon/ammo and wanted displays.
Resolved globals and `HudReticle.IsHidden` are not proof of rendered hiding. A source guard now requires separate visible-hiding evidence; none of D's components has it.
Shipped weapon/ammo/health/armour/wanted therefore stay vanilla with zero planned hides. Prompts continue; `layout-test` or explicit `drawWithoutHidingVanilla` permits diagnostic duplicates.
Final guard has offline verification only; it was not installed. Health/armour arcs and radar are preserved. Deeper memory/hook research remains deferred.

Useful partial evidence: weapon change/shot/reload, health/armour conditions, both forced prompt devices, real config-off/restoration and sampled draw gates passed.
The final measured diagnostic sample was `draw.hud=0.306 ms`, `draw.ui=0.310 ms`; this excludes B's newer wheel/trunk integration and does not certify the task.
Vanilla ammo `133 17` matches the initial Liberty `17 / 133` for total 150; firing/reload comparison supports total-includes-clip for this pistol only.
The first-shot log briefly combined fresh clip with the previous total; sample consistency remains a follow-up.
The wanted screenshot correctly shows three Liberty stars but the transition-only log assertion failed because wanted was already visible; the next scenario checks sampled wanted state instead.

Required unblock: evidence-backed isolated hiding (including restoration), then a fresh committed-build run reviewing every screenshot, combined B/D captures and real-device/owner sign-off.
No guessed addresses, broad HUD suppression or unapproved duplicate bars added. Sonnet must review ALL Codex source and documentation, including any uncommitted edits, before continuing.

## Human test steps

1. After source/offline review and assignment of the shared game slot, install a fresh committed build through `tools/verify-local.ps1` with restoration; load a free-roam save and go to the test range (DevTools > TELEPORT).
2. Unarmed and idle: the top-right corner shows nothing of Liberty's HUD. The radar is at the bottom left.
3. Give yourself a Glock 17 with 150 total rounds (`lf catalog give service-pistol 150 clear`). Shipped mode must retain vanilla weapon/ammo/wanted and draw no duplicate group (`hudctl check`: all four plans vanilla_kept, hidden=0).
   For authorized layout inspection, `lf hudctl layout-test on`: Liberty silhouette and `17 / 133` appear top right, hold about four seconds and fade. Aim (LT / right mouse), shoot, reload: the group returns; a low clip turns orange.
   Vanilla duplicates in this diagnostic are expected and cannot satisfy clean replacement acceptance. `lf hudctl layout-test off` returns to vanilla.
4. Under shipped policy, health/armour remain in the vanilla radar arcs. During authorized layout-test, take damage and set low health/armour: the top-right diagnostic bars show, fade or pulse as appropriate. Turn layout-test off.
5. During authorized layout-test, get a wanted level: Liberty's six slots fill for the level and fade after it ends. Shipped policy keeps only vanilla stars. Diagnostic duplicate stars cannot pass replacement acceptance.
6. Walk behind a parked car: the help box (top left) says `Press X to use the trunk.` on a controller and `Press E ...` on keyboard/mouse, following whichever you touched last.
7. Open the weapon wheel and the trunk: nothing may cover the top-right group.
8. Set `"enabled": false` in `scripts\LibertyFramework\config\hud.json`: within a second the complete vanilla HUD is back and nothing of Liberty's is drawn. Set it back to `true`.
9. Tell me per element whether it is too big, too small, too faint, too busy or too slow/fast to fade; the numbers are in `hud.json` (`layout`, `palette`, each element's `holdSeconds` and fade times) and hot-reload.

## Native display diagnostic

October 1 Sol continuation: **diagnostic prepared; NOT RUN in game**. Functional HUD compatibility comes before
styling. Shipped `HudPlan` still reports vanilla_kept for all replacements with zero hides; no default/config tuning
has changed. The inherited ammo pair already refreshes on observed clip changes: no recurrence of the demonstrated
mixed-age defect found in source. Pistol/other weapon runtime sampling still needs validation.

`T049-hud-native-display` runs `hud-native-display.txt` using only registered DISPLAY_HUD/DISPLAY_RADAR and existing
SDK player/weapon calls. `lf hudctl probe-mode on`, `lf hudctl layout-test off`, then
`lf hudctl native-display on 60000` arms a maximum two-minute diagnostic. Every tick enforces HUD=false/radar=true;
no Liberty replacement drawings are enabled. `lf hudctl native-display off` releases it. Config-off, expiry, public
HUD-off, pause/cutscene/fade/death/no player, module stop/unload and failures cancel rather than rearm it. A small
internal UiService owner query lets public HUD-off retain both HUD and radar suppression. This touches no renderer,
material API, ConfigService or gameplay budgets.

`cash-test pulse|restore` is restricted to probe mode and temporarily changes the wallet by one unit to produce a
visible native cash baseline; it restores the original wallet on request/stop/unload/failure. Never transact/save or
edit HUD config during these diagnostics. Config-test restores and independently reads/hashes original bytes.
Normal verifier backup restoration remains necessary after a process crash. Do not use native calls as proof of
visual disappearance. Every named capture must be readable; missing baseline elements make comparison inconclusive.

Original visibility: the existing public visibility-owner ledger is preserved, including other HUD-off owners.
The scenario starts from public `hud on` and verifies return to that visible baseline. The documented API has no
getter for arbitrary raw DISPLAY_HUD/DISPLAY_RADAR state set by other scripts; exact restoration of such unknown
external state is **unproven**, and this diagnostic must not be used alongside outside native visibility overrides.

`T049-hud-native-story-text` is a required separate owner review, not inferred from the free-roam scenario. In the
SHDN console use the commands above; capture with Steam F12 and confirm native_applied=True for each active capture.
Record the chosen story mission/checkpoint and repeat the same native help, spoken subtitle, objective and
street/location/vehicle-name triggers in baseline, active and restored states. No available baseline text means
NOT-RUN for that category. SDK overlays are not substitutes. For domain unload, arm during gameplay then run
`ReloadScripts` in the SHDN console; require `hud_released reason=unload`, restored vanilla and a default inactive
lease on the next load. Stop evidence does not prove domain unload. Cutscene entry cancels the lease deliberately;
this check proves gameplay text preservation only while active, plus safe release on cutscene transition.

Full automated proposal after a slot is assigned: `LOOP-build`, `LOOP-verify`, `LOOP-package-install`,
`T049-hud-native-display`, then `T049-hud-components` / `T049-stage1-hud`. Use -AnyBranch -NoPush -Restore -NoManual
and full mode; inspect all captures and restoration. Real story text/unload remains `T049-hud-native-story-text`.
Owner feel/style remains `T049-hud-look`; neither manual check is automatically accepted. Combined wheel/trunk/HUD
budgets stay unchanged and require later integrated feature evidence. No T-040 rerun or density reduction.

### October 1 full diagnostic result (partial, guard unchanged)

Full 20261001-155808-1f6c289 ran95 steps/zero failures/zero errors and restored phase2-20261001-155855.
Nine captures reviewed: cash/wanted disappear while radar remains; off/config-off/public-on/expiry/stop restore them.
Weapon/ammo absent from the baseline, native story text and domain unload not tested: all unproven. No acceptance or
hiding policy adoption. Production build PASS; first file verifier1119/1/1 failed missing generated XML; package Stage
created it and standalone full file recheck1134/0 passed, original failure retained. BatchB was not run; slot released.
[Complete receipt](../reports/2026-10-01-lane-d-native-display-full.md). Need a visible weapon/ammo baseline and
T049-hud-native-story-text before adopting any native hiding. Existing task remains BLOCKED on complete evidence.

### Held baseline

`T049-hud-held-baseline` is baseline-only: demonstrate owned inventory ammo while selected unarmed, then explicitly
select the pistol with fresh native/snapshot agreement for paired idle and supported task-aim captures. No cash
pulse, native hiding, camera creation, firing or synthetic story text. Logs include pause/control/menu/public owner
and game camera pose/FOV. Requesting a task never certifies actual pose, native pixels or player aim-camera behavior.
Use an idle controlled free-roam player: prior tasks cannot be restored. Fixture off, unsafe/config/probe-off and
shared module release clear the remembered own-ped task; restoring config never rearms. Review both actual captures
for visible native icon/ammo, held pistol, respective idle/aim pose and radar before scheduling a hidden comparison.
Missing views remain unproven. New scenario is QUEUED with no dependency that could run hiding before image review.
Required next slot IDs: LOOP-build,LOOP-verify,LOOP-package-install,T049-hud-held-baseline (full/noQuick/Restore).
Then reviewed T049-hud-native-display pairing; real T049-hud-native-story-text/unload and owner/combined tests remain.
See [focused receipt](../reports/2026-10-01-lane-d-held-baseline-offline.md). Vanilla guard remains unchanged.

Held-baseline ownership correction: monotonic wall-time deadline now retires expired task ownership without native
clear in tick/readback/off/stop, including no intervening tick. Pre-expiry unsafe cleanup unchanged. Game pause can
make wall ownership expire before native game-time completion; expiry is not completion proof. Focused harness67/0,
11 expiry cases plus original56. Actual captures/production compile still pending; full proposal adds -StopOnFailure.

### Full held-baseline attempt

Full20261001-203435-5c0cb8d build/full verifier1141/0/package passed; baseline FAIL on unarmed inventory clip17
expectation versus observedclip0,total150,held0/snapshot0. Stopped before both captures/task requests; visibility and
runtime deadline unproven. Restored phase2-20261001-203521,slotreleased,no rerun. Vanilla guard unchanged.
[Receipt](../reports/2026-10-01-lane-d-held-baseline-full.md). Failed evidence preserved; next offline assertion review.

Negative control repaired offline after source review: unselected clip logged without requiring17; held0/snapshot0/
ownedTrue/total150/numericFalse/safeTrue still required. Positive knownclip17 readback remains unchanged; neither
SDK Give nor Select sets clip, and SDK has no SetAmmoInClip. Focused80/0;originalFAIL preserved;no second run.
Both positive captures still NOT RUN. No HUD regression/visual acceptance claim; native hiding unassigned.

### Reviewed full held baseline (partial)

Full210109-df025f7 build/verifier1141/0/package PASS,runner58/0/zeroerrors. Both JPGs opened: idle nativeicon/ammo
133reserve/17clip heldpistol/radar visible;taskaimpose/icon/radar visible but ammo absent. NEEDS-REVIEW,not full
baseline PASS. Fresh held/snapshot7 clip17,total150 agree both;negativecontrolclip0. Pre-expiryoff releases logged;
expiry/unload not runtime-tested. Restored phase2-20261001-210132,metadata04:04:20.0634250Z,Dslotreleased.
No hidingpolicy/comparison or secondbatch. [Receipt](../reports/2026-10-01-lane-d-held-baseline-reviewed-full.md).
