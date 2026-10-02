# T-059: Citywide environment overhaul

Integration note (2026-10-02): this card was T-058 on the preserved preview branch. It is now T-059 to avoid the repository-review ID collision; historical commits and receipts retain their original IDs.

Status: IN-PROGRESS

## Owner direction (2026-10-02)

Begin the approved visual overhaul across all Liberty City. One accountable implementation and one integrated candidate covering all weather/time conditions; avoid agent handoffs and elongated separate passes. This supersedes the earlier art-only hold and station-only prototype sequence for this work. Existing gameplay and density OFF remain intact. No new workers dispatched.

## Scope and acceptance

Citywide lighting, weather appearance and color: gritty grey/rain atmosphere, natural color and readable brighter nights with stronger existing light/sign accents. Eight weather types, eleven time samples, both source timecycle tables. Reuse FusionFix/DXVK and the existing mood generator. Sky/cloud controls use FusionFix 5.0.1's parser at commit 619f52d, rather than the misleading legacy X360 header. No added geometry or fictional light fixtures.

Approved references: results-local/art-direction/20261001/ai-tuning/star-junction-batch-v02.json and station-batch/station-batch-v01.json; Night C is the approved night direction. Actual appearance is judged against these targets, not merely against a brighter numeric preset.

One integrated capture batch covers all five benchmark conditions at the station, Star Junction, Hove Beach shops and East Hook docks. Global file overrides apply citywide; four sampled sites do not prove every interior/mission or pixel-equivalence to the AI images. Material/light/sky capability gaps found in review remain part of the overhaul; this initial implementation must not be called the full completed remaster.

## Changes

- `config/mood.json`: author all weather/time conditions together; remove blanket dark/desaturated night direction, preserve readable ambient light and natural accents, neutral grey/rain skies.
- `tools/mood/MoodTimecycle.cs`: optional sky/cloud RGB overrides and validation against the verified column layout.
- `tools/install-mood.ps1`: use the machine-wide game lock, validate pristine sources, stage output before mutation, exact pre-install backup/receipt and `-Rollback`.
- `tools/mood/check-citywide.py`: actual source/generator coverage, unchanged unrelated columns, extended-field isolation, deterministic output and invalid RGB rejection.
- `tools/mood/Run-CitywideReview.ps1`: one bounded installation/capture run under the game lock, dynamic player-position cleanup and exact rollback on runtime/cleanup failure. Restarting an already-open game requires human authorization.

## Evidence

**Final continuation state:** candidate v3 is prepared in source and captured at all 20 scenes in `run-20261002-06/citywide-environment-20261002-040338`. It completed 171 steps, zero failed steps, but is **NEEDS-REVIEW**, not PASS: a pre-capture engine.commands stall at 11:03:33.555Z wrote its dump completion at 11:03:41.683Z inside the capture window. Settled cleanup verifies original location within 0.1 m, health 100, closed menus, no owned test entities and unchanged carried/stored weapons within the batch. This result supersedes earlier pending statements below.

The final one-attempt readiness launch exited before the engine loaded; exact rollback ran, then the two prior candidate receipts were walked back to the original owner's look. Both final hashes match `final-v03-baseline-restoration.json`. The game is closed and no test lock remains. No candidate is currently installed. Automatic retries stop. The one-page v3 comparison and provenance are retained under results-local/citywide-overhaul; 50 images/20 rows passed desktop/320/360 px QA.

Actual grey/rain daylight is closer; v3 restores night cloud detail and improves unlit readability, but station night remains below the approved brightness/warm local-light target. Star Junction still lacks the target's warm facade depth; wet-material detail is not supplied by a global grade. The overhaul remains IN-PROGRESS rather than claiming those assets/lighting complete. Stable startup and moving/interior/mission/performance acceptance remain open.

Latest continuation supersedes the earlier startup status below: after the owner's manual launch, `run-20261002-03` reached gameplay and passed 171 steps with 20 screenshots, zero failed steps and zero log errors. Visual review found excess grain and green/cyan bias. Candidate v1 incorrectly treated columns 9–11 as sky RGB; upstream renderer source shows grain/fog alpha there. Candidate v2 preserves those fields, writes actual normalized sky/cloud RGB, and authors ambient/directional/color-correction colors. All 88 rows pass the corrected isolation/range/determinism checks. Runtime v2 review is in progress; v1 is retained as evidence, not visual acceptance.

See `results-local/citywide-overhaul/` for generator, installer rollback, runtime and visual review receipts. Status stays IN-PROGRESS until runtime screenshots are inspected; NEEDS-PLAYTEST is not owner acceptance.

V2 retry `run-20261002-05` passed all 171 steps and 20 frames with zero log errors, after the first v2 run's empty command-reply publication race was fixed. Settled cleanup verifies original position, closed menus, no owned props and unchanged carried/stored weapons within the batch. Actual review found night too dark; v3 raises night palettes together and is undergoing the same integrated capture. Day/dusk tuning is retained. The first v2 failure, including its engine stall, remains recorded separately.

## Earlier startup evidence (before the owner's manual launch)

Owner authorized restart on continuation. Three Steam candidate startup attempts failed before engine load and rolled back; a restored-baseline control reached gameplay with a FusionFix shader-warning acknowledgment. One controlled candidate retry and one direct-local launch also failed before gameplay and rolled back. Windows fault records report 0xc0000005/unknown module; the preserved module list includes MTLX/DXVK/ASI loader but no FusionFix. This suggests an early startup problem, not a proven timecycle cause. No candidate screenshots exist and visual acceptance remains unproven. Exact original timecycle hashes are restored; see final-mood-restoration.json. Do not mark the overhaul complete.

The direct-local diagnostic's boot.txt contains the earlier baseline boot because its helper had no new session boundary. That receipt is explicitly invalid as candidate readiness evidence; its pending command failed after process exit. Subsequent runtime work must use Start-GameReady's session boundary or filter by the new launch timestamp.

The final two baseline-return launch attempts produced no observed GTAIV process. Baseline files remain restored and no game/test process or lock remains. Stop automatic retry loops. Next external-state check: owner launches GTA IV normally; then continue the candidate's actual runtime/visual review. No full-overhaul completion or candidate screenshot pass is claimed.

## Human test steps

1. Current game files are the original appearance. For a future candidate playtest, close GTA IV, run `tools/install-mood.ps1 -GameDirectory <game>`, then launch normally. Walk and drive through Broker and Algonquin in ordinary free roam.
2. Check cloudy/rain daylight: grey atmosphere and distance depth, readable pedestrians/platform shade, natural material colors.
3. Check clear daylight and dusk: distinct sunlight and dusk, with no sudden color/exposure jumps during the clock cycle.
4. Check night, including rain: readable unlit roads/subjects; existing signs/lights retain color, text and local separation without broad bloom washout.
5. Enter/exit a safehouse or tunnel and run a mission/cutscene that changes weather. Check return to normal gameplay and HUD/reticle readability.
6. Compare the single review page's original/AI target/actual frames. Record remaining gaps and feel/performance feedback.
7. With the game closed, `tools/install-mood.ps1 -GameDirectory <game> -Rollback` restores the exact pre-install look. It restores one receipt at a time when several candidates were installed. `-Restore` instead explicitly restores pristine FusionFix. The final continuation already walked back the candidate chain and verified the original owner's hashes.

## Remaining acceptance

Moving gameplay/presented frame-time comparison, interior/mission exposure and owner visual sign-off. Texture/material upgrades and localized lighting are not implemented by the global grade; annotate actual target gaps before selecting those changes.
