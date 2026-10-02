# First mod milestone and research order

2026-10-02. Updated from the owner's voice answers. The first mod is the priority. Use research to finish its
features, then expand the framework as a separate program. Keep one coordinator for now; additional agents are
an option when a task has clean file ownership and a clear deliverable.
Current source integration is tracked by [T-060](../tasks/T-060-local-integration.md).

## Deliver one reliable playable slice

Start with the existing weapons/loadout/wheel/trunk preview and the prepared visual candidate. Establish
repeatable startup and restoration, then test the combined source with the original acceptance thresholds.
Keep combat/atmosphere disabled in the preview profile until their individual and combined gates pass.
The lighting candidate is generated/installed separately; merging its source does not install its tables.

The first owner playtest should cover walking/driving between two locations, combat with the existing
weapon catalog, switching the 2+1 loadout, storing/retrieving a weapon, and an actual game save/reload.
Use one current build/config/plugin receipt and record controller, mission and visual limitations explicitly.
After this baseline, complete the first mod as one product: gameplay, body/injury effects, a unified owned-vehicle
system and the visual pass. Develop and validate these in small steps so a failure has a traceable cause.

## Sequence

1. **Source integration:** reviewed preview/mood and B radial/diagnostic work join the tooling fixes.
   Build the engine; run offline verifier, both PowerShell versions, radial and metrics harnesses,
   generator checks and queue validation. Keep experimental C/D/R code on its preserved branches.
2. **Runtime baseline:** schedule one bounded readiness attempt with a fresh journal and exact restoration.
   A launch failure is investigated at its first failed stage; do not repeat an unchanged batch. Once startup
   is available, run a short combined smoke, then the affected full wheel/trunk and config-lifecycle gates.
   The historical trunk p95/p99 failure remains unresolved even if the new preview disables combat.
3. **Visual iteration:** use the approved references and matched actual captures. Separate global color/lighting
   from cloud shapes, material textures and local lights. Record one change family per candidate and inspect
   street-level moving scenes, interiors and weather transitions in addition to the existing static views.
   Overcast must retain natural color; the v3 gloomy captures look too desaturated to the owner. GTA IV's cloud
   shapes need a substantial overhaul beyond timecycle color values. Target 1080p/60 FPS; measure frame times
   in motion, not only static screenshot quality. The exact resolution beyond 1080p is undecided.
4. **First deeper engine experiment:** distinguish the C setup/scene crash from the actual sever operation.
   Complete the matched control/active setup pair before assuming skeleton changes cause a crash that occurred
   before a scripted cut. Then instrument one ped/cut with object generation, skeleton pointers, counts and
   release order. Require cleanup and original performance gates before promoting gore to the combined preview.
5. **Vehicle ownership:** deliver one coherent design covering persistent personal cars, garages, dealerships,
   trunks, customization, fuel/repairs, insurance and recovery. Build its data model and transaction rules
   together; implement and test vertical slices within that design. Selectively adapt licensed LVS behavior.
   A purchase, storage and actual save/load round-trip precedes broader integration and feel testing.
6. **HUD/materials/assets:** promote only the specific contracts each next feature needs. Vanilla HUD remains
   until hiding/restoration and native story text are demonstrated. Material names require effective-table and
   visible hit-target evidence. Multi-geometry/collision research becomes priority when required for chosen assets.
7. **Deeper framework program:** after the first mod's systems work together, audit its remaining engine limits,
   profile real gameplay, and pursue broader renderer, asset, collision and scripting control with pinned
   reverse-engineering evidence. Framework work can still happen earlier when a first-mod blocker requires it.

## Agent organization

Use one coordinating agent initially. No new agents have been created. Revisit parallel work when there are
separate, bounded assignments, such as visual asset authoring and isolated source research.

| Role | Owns | Deliverable and boundary |
|---|---|---|
| Coordinator | Main, test host, shared SDK contracts, schedule and owner playtest | Reviews patches/evidence, runs the sole game/install slot, combines accepted changes |
| Optional gameplay/engine worker | One chosen blocker, initially setup/gore lifetime or vehicle transactions | One hypothesis or vertical slice, bounded files, focused tests; no speculative unrelated hooks |
| Optional visual worker | Mood generator/config, capture fixtures and later selected assets | Matched baseline/candidate evidence against approved targets; coordinates host changes with coordinator |

If parallel work becomes useful, start each worker from the reviewed integration commit, in a separate worktree. Do not give both ownership of
shared host/SDK/UI files. Workers request the game slot through the integrator; heavy builds are serialized.
Every handoff includes source commit, changed behavior, actual check status, first failure and the next experiment.
Create or resume threads only for actual bounded assignments; do not dispatch from this document automatically.

## Owner decisions and remaining design detail

- Body/injury package: realistic wounds, dismemberment, reactions and persistent blood/bodies belong together.
  Environmental damage is outside this initial gore package.
- Vehicle package: all named ownership features belong in one design and eventual first-mod system, with staged
  implementation and testing.
- Visual direction: the recent candidate is mostly liked, with more color in gloomy weather and substantially
  better clouds. Performance goal is 60 FPS at 1080p.
- Saves: separate experimental saves are acceptable during development. Keep backups and later verify real
  save/load and story compatibility before calling the mod complete.
- Agent/research order: keep coordination simple now; research first-mod blockers, then broaden engine research.
- Still useful to specify: the ideal first five minutes and which gameplay behavior should be refined first.

Detailed experiment/provenance requirements: [research program](../research/RESEARCH_PROGRAM.md).
