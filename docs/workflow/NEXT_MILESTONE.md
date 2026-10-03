# Liberty+ development plan

Updated 2026-10-02. This is the current first-mod plan. It replaces the previous
weapons-first sequential schedule; old task reports remain historical evidence.
Product scope is in [PRODUCT.md](../PRODUCT.md). These are planning decisions,
not instructions to dispatch agents or resume feature implementation automatically.

## Decisions settled

- Finish Liberty+ as a polished showcase before a broad Liberty Engine program.
- Keep the complete scope: weapons/reticles, physical inventory/holsters, wheel,
  trunk storage, connected vehicle ownership, gore, custom HUD and visual overhaul.
- Next feature focus after preparation: **grounded, severe gore and world atmosphere
  across day/night and weather, plus a cloud overhaul**. The
  [gore/atmosphere briefs](../design/GORE_AND_ATMOSPHERE.md) separate the desired
  experience from technical capability and remaining asset/tuning decisions.
- Research concrete mod blockers and reuse accepted findings before new experiments.
- Once authorized, independent features can develop in parallel under one coordinator.
  Integration checkpoints do not require serial feature-by-feature owner playtests.
- Use focused offline checks and short discriminating runtime checks during development;
  use the assembled candidate for longer owner playtesting and subsequent iteration.
- Keep the game installation and experimental saves protected by the existing receipts,
  restoration procedures and single game slot. No game run is authorized by this plan.
- Target natural color in gloom, a substantial cloud improvement and 1080p/60 FPS.
  Environmental destruction stays outside the initial gore package.

## Shared state contracts to establish before dependent implementation

These are design requirements, not claims that the APIs are already implemented.

| State | Producer/owner | Consumers and required agreement |
|---|---|---|
| Actual weapon spread, recoil and recovery | Gunplay simulation | Weapon-specific reticle and shot behavior use the same current state |
| Weapon identity, ammunition and location | Inventory/transfer authority | Holsters, wheel, HUD and trunk operations cannot disagree or duplicate items |
| Durable vehicle identity and ownership | Vehicle ownership authority | Registration, garage, insurance, recovery and trunks refer to the same vehicle |
| Damage and injury lifetime | Injury authority fed by verified damage events | Wounds, bleeding, reactions, severed parts and cleanup share victim generation/state |
| Weather and visual overrides | Atmosphere configuration/runtime owner | Lighting, rain, clouds and wet-surface effects coordinate and restore their original state |
| Input focus and visibility | Shared UI/input services with mod layout policy | HUD, prompts, wheel and menus do not fight for controls or hide required story information |

Specify update timing, invalidation, resource ownership and failure behavior for each
contract actually needed by the next assignment. Do not redesign every framework API
up front. Reusable capabilities go into Framework; Liberty+ keeps gameplay/art choices.

## Focused skills

Framework contains liberty-feature-delivery (briefs, contracts and integration),
liberty-presentation (HUD/reticles/atmosphere) and liberty-persistence (ownership,
transfers and save recovery), alongside liberty-research and liberty-evidence.
Load only the relevant skill. Scripts perform established repetitive operations;
skills guide decisions and evidence. Add automation when observed repetition justifies
it; skills alone do not prove development or testing became faster.

## First assignments when feature work is authorized

**Gore:** review preserved candidate/crash evidence, isolate setup versus cut failure,
then establish reliable injury/clone/resource lifetimes. Investigate convincing cut
geometry and wound/pool rendering with bounded experiments. Preserve the full desired
gore package; do not call current particles or a partial sever demo the final overhaul.
Track every behavior in the brief's required wound/blood checklist, including
impact-position bullet holes, clothing leakage, pressure/post-death bleeding and
surface drips/pools/trails; unresolved capabilities stay explicit research work.

**World atmosphere:** inspect existing presets, generators and visual captures; map
color/lighting, cloud, rain and wet-material/reflection controls separately. Use actual
matched moving captures and measure cost. Correct loss of color and research genuine
cloud improvements rather than promising cloud replacement through grading alone.

These can advance together with distinct owned files. Coordinator owns shared APIs
and integration; only one worker owns the game/install slot at a time. Framework
changes must be reflected in the mod's dependency lock after appropriate validation.

Weapons, trunks, vehicles and HUD remain part of the assembled acceptance scope.
Expand their assignments as dependencies and approved briefs permit; they are not
prerequisites that must all finish before gore/atmosphere work starts.

## Completion and testing

Existing startup/stall, gore lifetime, trunk frame-budget, HUD restoration and actual
save/load problems remain unresolved. Treat them as explicit assembled-release gates.
Run affected checks while building, investigate first failures before repeating, and
retain required final trial counts/budgets. Profile build/setup/startup/lock-wait and
scenario time separately before claiming the testing slowdown is fixed.

The assembled candidate must show repeatable startup/restoration, coordinated
weapons/wheel/holsters/trunks, actual save/reload, controller/story compatibility,
cleanup under sustained combat, readable HUD and convincing moving visuals.
Offline PASS, bounded runtime observations, full acceptance and owner feel approval
are separate statuses. No runtime or 60 FPS success is claimed by this planning pass.

## Remaining decisions

The owner chose grounded, severe gore and full atmosphere tuning across daytime,
nighttime and sunny/cloudy/rainy conditions, plus a cloud overhaul. Rain was an
example, not a rain-only scope. Final assets, persistence limits and technical
feasibility remain open until evidence and owner review settle them.
Exact weapon roster/reticle designs can be specified when
those assignments become active; answered product questions should not be repeated.

Feature implementation is still paused. Planning completion does not lift that pause.
