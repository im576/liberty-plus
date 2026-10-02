# Liberty Vanilla+ — Stage 1 (first production mod)

Status: **IMPLEMENTATION / INTEGRATION; approved direction (owner, 2026-09-30), awaiting full acceptance and owner playtest.** Working title: *Liberty Vanilla+ / Gunplay V2 — Stage 1*.
This document is the build plan for the first mod on the Liberty Engine. Engine facts it relies on are in
[PROJECT_STATE.md](../PROJECT_STATE.md) and the [regression report](../reports/2026-09-30-regression.md).

**Owner clarification (2026-10-02):** finish this first mod as the near-term goal, using focused research to
resolve feature blockers. Broader engine research follows. The visual overhaul is part of the first mod:
the current grade is mostly liked, but gloomy weather needs natural color and game clouds need major work.
Target 1080p at 60 FPS. Body/injury gore includes wounds, severing, reactions and persistent aftermath;
environmental damage is outside that first package. The owned-vehicle system should ultimately cover
personal cars, garages, dealerships, trunks, customization, fuel/repairs, insurance and recovery.
Experimental saves are acceptable during development. This clarification supersedes the October 1 ordering below.

**Owner priority update (2026-10-01): gameplay features first, remaster later.** The immediate deliverable is an
integrated feature test build: weapons/gunplay/reticles, physical loadout, wheel, trunk/storage, gore/combat effects,
functional HUD/menus and their supporting systems. Review existing and in-progress feature modules, fix them and
validate compatibility, performance and restoration before the owner's playtest. Functional UI work stays in this
priority even where its later visual styling is listed under Slice C.
Environment remaster, texture/art deployment and lighting/weather/atmosphere polish follow that feature milestone;
their acceptance does not block the feature test build. Art direction may continue independently through reference
boards, palette/type/icon/HUD concepts and Hove Beach mood decisions. The longer-term Stage 1 design below is retained.
The owner's temporary build hold is revoked by "fix that and then resume"; builds/tests resume through the orchestrator's
shared schedule. Existing gameplay requirements, budgets, density OFF and the preserved baseline remain unchanged.

## 1. Goal

> What would GTA IV feel like if Rockstar remastered and expanded it today, without changing what GTA IV is?

**Keep:** the grime, the cold grey identity, physicality, melancholy, danger, density, grounded weapons, the late-2000s
setting. **Modernize:** gunplay, weapons, violence, UI/HUD, inventory, audio, ambience, visuals, effects, usability,
performance. It is not GTA V transplanted into GTA IV.

**Success test:** someone who knows GTA IV plays Stage 1 and thinks *"this still feels like GTA IV, just far more
polished, brutal, atmospheric and modern"*, never *"a pile of unrelated mods"*. Every system (graphics, UI, gunplay,
blood, inventory, audio, ambience) must read as one product.

## 2. Pillars

1. **GTA IV first.** Every redesigned system must look and feel as if it belongs in GTA IV.
2. **Harsh violence.** Graphic and grounded, never comedic. Gunshots are consequential.
3. **Physical world.** Weapons, ammunition and storage are objects, not menu entries.
4. **Dangerous Liberty City.** The city sounds, looks and reacts like a hostile place (unsafe, not horror).
5. **Performance conscious.** Designed around measured cost. *"Do not optimize Liberty City by removing Liberty City."*
   Ped/traffic reduction is the last resort.

## 3. Visual philosophy

> **Use existing rendering technology where it is already good, while creating an original Liberty Vanilla+ art pass on
> top of it.**

- "Vanilla+" is **not** "reuse vanilla/FusionFix assets and settings". It is a real remaster pass with newly authored art
  wherever that materially improves the game.
- Rendering technology (FusionFix, DXVK, the game's shaders) is reused where it already solves a problem; Liberty does
  not rewrite it. Reuse never limits the creative scope of the art.
- Target look (owner-approved references, 2026-10-02): cold daylight, heavy overcast, a strong wet-city atmosphere,
  brighter/readable nights with rich existing light/sign colors, muted worn architecture, dirty glass, wet asphalt
  and dense urban depth, still recognizably GTA IV. Preserve natural surface color rather than blanket desaturation.
- Not a blanket 4K pass: good materials, correct mipmaps, fixed assets and better lighting beat raw resolution.

### Asset policy

> **Identity-defining assets are hand-designed; repeatable material surfaces can be procedural or CC0-based.**

- **Hand-designed / original:** signage, graffiti, UI icons, HUD artwork, weapon finishes and decals, blood and wound
  art, important storefronts, hero props, visually distinctive surfaces, screen-effect art (blood and rain overlays).
- **Procedural, original photography/material work, or CC0:** asphalt, concrete, brick, generic metal, wood, grime,
  generic glass, sidewalks, dirt, generic vegetation materials.
- **Never:** ripped commercial assets (AGENTS.md rule 7). All third-party art and audio needs a licence that allows use
  in the project, recorded in `third_party/README.md` with its source.
- **Image generation (primary path: the art-request queue).** Agents file structured requests in
  `docs/art/requests/` ([art queue](../art/README.md)) and keep working; a separate image-capable agent or app (the
  owner's GPT Image 2.5 subscription) follows [GENERATOR.md](../art/GENERATOR.md), writes results to
  `art/generated/`, and registers them. Agents then review, approve or reject, prep and integrate them. Every prompt,
  revision, file hash and review decision is recorded in the request, so the repository is the source of truth.
  **Fallback only:** if no image agent is available, the owner may generate from a request's prompt by hand and drop the
  files into the same `art/generated/ART-NNN/rN/` folder; the same registration, review and provenance rules apply.
  Generated images count as original project art (the owner confirmed on 2026-09-30 that the provider's terms allow this use), and they go through
  the same review, tiling, atlasing, mipmap and compression steps as all other art.
- Agents build the pipelines, procedural materials, variants and prompts; final hero-art quality is an owner sign-off.

## 4. Scope

**Stage 1 = Broker/Dukes and early-game Liberty City.** What that scopes, and what it does not:

- **Broker/Dukes scopes gameplay progression and the deepest environment art pass.** Weapon availability, contacts and
  the economy slice follow early-game Broker/Dukes, and it receives the most thorough per-street art work.
- **These are citywide systems, active everywhere from day one:** HUD and UI, gore and violence, combat systems
  (weapons, gunplay, physical inventory, effects), core lighting/timecycle/weather, screen effects, and ambient audio.
- **Broker/Dukes is the showcase area for the visual remaster, not the only area that improves.** Citywide texture and
  material fixes (shared road, sidewalk, glass, emissive and prop textures used across the map) apply everywhere they are
  used; Broker/Dukes additionally gets the area-specific pass (storefronts, signage, graffiti, facades, interiors).

**Not in Stage 1:** full crime or police overhaul, businesses, heists, the full economy, a full vehicle ownership
overhaul, all-island progression, TLAD/TBoGT, complete reverse engineering.

## 5. Engine constraints (what Phase 1 proved)

- Authored models: **one geometry / one material per model** (texture atlases). Multi-geometry drawables crash at spawn.
- **No authored collision**: use collision proxies (hidden vanilla props, `Liberty.World` `collisionProxies`).
- **No LOD slots 1-3** in authored drawables: separate models or IDE draw distances.
- Map geometry cannot be rebuilt; the environment pass is **textures and materials on existing geometry**, plus new
  single-material props.
- No silent unsafe workarounds. A genuinely missing primitive gets the smallest safe engine capability, documented for
  Phase 3 reverse engineering.

## 6. Reuse first

Before any slice starts, audit and extend: Gunplay, Arsenal, CombatEffects, Holsters/slings, Trunk, `Liberty.World`,
the SDK, exact damage, raycast, Liberty.Ui radial/list menus (`StorageWheel`), the weapon catalog, Atmosphere/Mood
configs, the content compiler, the Blender add-on, the finishes (weapon texture) pipeline and the performance tooling.
Nothing is rebuilt because a new version would look cleaner. Stage 1 exists to prove the engine.

## 7. Build plan

Each feature lists its engine status: **Ready** (existing code or data), **Extend** (existing system, new work),
**Spike** (depends on a research item in section 8, not promised until it passes).

### P0 / Slice A — Combat & Inventory

Functional first; Slice C gives these screens their final unified look.

| Feature | Status | Notes / acceptance |
|---|---|---|
| Weapons and availability | Ready/Extend | Weapon catalog, per-weapon config, WeaponInfo. Tiers: common (cheap/service pistols, revolvers, basic shotguns, Uzi), less common (better pistols/shotguns, MP5-type), rare (AK-type, tactical by circumstance). No snipers/LMGs/P90/military gear as normal Stage 1 availability. Availability comes from the world, contacts and money, never XP |
| Gunplay tuning | Extend | Per-weapon recoil, first-shot accuracy, burst control, sustained-fire climb, stance/movement influence, caliber differences. Shoulder swap finished (T-015), not recreated. Accept: tuned numbers in `config/`, owner feel sign-off per weapon class |
| Weapon-specific reticles | Extend | Replaces the one generic crosshair (`Gunplay/Crosshair`, whose gap already follows the live spread cone). Per class: pistol small and precise; SMG wider, showing spread and burst control; assault rifle tighter, structured, with clear recoil/spread feedback; shotgun wide circular/pellet pattern; sniper/precision minimal or none from the hip, scope UI when aimed; heavy/special unique where it fits. Size and spread follow the **actual** gunplay values (movement, stance, recoil, bloom, sustained fire, recovery), never cosmetic. Restrained, gritty, readable, not futuristic; hidden or simplified where realism or gameplay calls for it; controller and keyboard/mouse. Shape, sizes, colours and behaviour per class with per-weapon overrides in `config/`. Final styling revisited in Slice C |
| Physical weapons | Extend | **Decided: 2 long guns + 1 sidearm**, one long gun equipped at a time, the other slung and visible (as the sling system already proves); limited equipment and ammunition; the rest in trunk/safehouse. Must handle weapon size, sling/back placement, clipping, outfits, vehicle entry/exit, cutscenes and draw/holster transitions. Carried long guns may be hidden or stowed in vehicles and cutscenes to avoid clipping or animation problems |
| Harsh gore | Extend / Spike | *"Harsh realism, not gore for gore's sake."* Exact-damage driven (no proximity guessing): severe wounds, strong head and shotgun trauma, caliber-sensitive presentation, limb damage, dismemberment where reliable, bleeding, wounded NPCs crawling/writhing, brief pain behaviour, strong panic nearby, contextual grounded executions only (no finisher system, no arcade or comedic gore). Blood pools, trails and surface blood: **Spike R3** |
| Combat effects | Extend / Spike | Per-weapon muzzle flash, smoke, casings, sparks, night muzzle light. Material-specific impacts (concrete, wood, glass, metal): **Spike R2** |
| Weapon wheel | Ready/Extend | Liberty.Ui radial menu; shows the physical loadout (carried slots, equipped, ammo, category, finish), not every owned gun |
| Trunk UI | Ready/Extend | Loadout ↔ vehicle storage, carried vs stored, ammo, capacity. Moving a gun makes it visibly no longer carried |
| Basic Liberty HUD | Extend | Our own health/armour/ammo/wanted/prompts drawn by Liberty.Ui, contextual (shown when relevant). Owner-directed layout: current weapon, ammo, health and armour form a compact **top-right** group, in the GTA IV-era visual language; do not relocate them to a lower corner. IV's radar stays **bottom-left** until **Spike R5**. Art reference: ART-007 r2 |

### P1 / Slice B — Visual Remaster & Atmosphere

| Feature | Status | Notes / acceptance |
|---|---|---|
| Lighting, timecycle, weather (citywide) | Ready/Extend | Mood timecycle generator and weather director exist; tuned to the target look |
| Citywide texture/material fixes | Extend (pipeline) | Shared textures used across the map (roads, sidewalks, glass, emissives, common props, weak vanilla textures, visibly broken LOD/material assets) improve wherever they appear. Needs a **texture override pipeline** (replace textures in map dictionaries via FusionFix's update-folder overloading; our WTD writer round-trips 68/68) |
| Broker/Dukes showcase art pass | Extend (pipeline + art) | The deepest pass on existing geometry: area-specific storefronts, signage, graffiti, facades, visible interiors, selected props and vegetation. Order in section 10a; Hove Beach is the benchmark |
| Weapon materials/textures | Ready/Extend | The finishes pipeline already rewrites weapon texture dictionaries |
| Blood/wound/decal and muzzle/effect art | Extend / Spike | New textures in the particle/decal dictionaries; where they live and what IV allows: **Spike R3** |
| Screen effects | Spike | Rain on the lens (droplets, streaks, movement clearing), blood on screen (directional, severe hits), injury (restrained desaturation, vignette, blur, audio muffling). Timecycle modifiers for grading; sprites on the Liberty.Ui canvas. Cost: **Spike R4**. No Call of Duty red overlays |
| Ambient city audio | Ready | Scripted events using the game's own sounds and speech: screams after severe violence, shouting, panic, arguments, sirens, alarms, distant violence |
| Population and vehicle variety | Extend | Popgroups/neighbourhood data, clothing variations, props, contextual street activity. New ped/vehicle *models* are deferred (skinned meshes/fragments) |
| Performance-conscious art pipeline | Extend | Mipmaps, sizes per surface class, a VRAM budget report per pack, measured before/after |

### P1 / Slice C — Unified Liberty UI

| Feature | Status | Notes |
|---|---|---|
| Design language | New (design) | *"Modern functionality designed through GTA IV's visual language."* Keep: dark, grey/black translucent surfaces, restrained amber/orange/red accents, gritty type, industrial feel, sharp iconography. Modernize: hierarchy, spacing, animation, readability, navigation. No mobile UI, big rounded cards, neon, live-service or GTA Online styling |
| Weapon wheel, trunk UI, HUD, prompts, reticles | Extend | Slice A's screens and reticles restyled in the final language |
| Inventory/stats menu | Extend | A Liberty menu (inventory, weapons, progress, stats, settings) as its own screen; room for vehicles/properties/contacts later without showing unfinished features. **Not** the Rockstar pause menu (Phase 3) |
| Icon family | New (art) | One coherent GTA IV-inspired family for weapons, HUD, menus and later map categories |

## 8. Research track (parallel)

**Research-dependent features stay in the long-term design.** They are not removed because they are not Stage 1-ready;
they are only not *promised for Stage 1* until their spike passes. A spike that fails moves its feature to a later stage
or Phase 3, never out of the design. Each spike ends with a written answer (works / works with limits / Phase 3) and, if
it works, the smallest documented engine capability.

| # | Question | Unlocks |
|---|---|---|
| R1 | Can new weapon sounds be injected into IV's audio banks (format, tools, licensing of tools)? | Weapon audio overhaul (reports, action, reloads, tails, cracks, indoor/outdoor) |
| R2 | Can the bullet impact / line test report the hit surface material? | Material-specific impacts and impact audio |
| R3 | Which decal and blood-pool capabilities does IV expose (natives, particle/decal dictionaries), and their limits? | Blood pools, trails, wall splatter, persistent aftermath |
| R4 | What does full-screen sprite drawing cost through the draw path at 60 fps? | Rain/blood screen effects |
| R5 | Can the radar be hidden and redrawn (map tiles, blips) at acceptable cost? | Custom radar |
| R6 | Where are the frontend/pause-menu entry points, and are they hookable safely? | Informs Phase 3 frontend work |

## 9. Deferred to Phase 3

Real Rockstar pause-menu/frontend replacement; real map replacement or integration; deeper audio engine control;
authored collision; multi-geometry drawables and LOD slots; deeper renderer and internal hooks.

## 10. Acceptance criteria and budgets per pillar

Every number below is a **proposal until the owner confirms it**; once confirmed it lives in `config/` or in the
scenario that measures it, not in code. "Mod-off" means the same scene, save and settings with Stage 1 disabled.
Measurement tools already exist: autopilot scenarios, `perf`/`costs` logs (frame p50/p95/p99, per-module cost), the
watchdog, memory probes and screenshot review. Each criterion gets an autopilot scenario or a named manual check in
`tests/local/checks.json`.

**Fixed capture points:** 8 Broker/Dukes spots (day, overcast, night, rain) plus 4 elsewhere in the city (citywide
systems). Visual and performance comparisons always use these.

### Pillar 1 — GTA IV first (visual remaster, UI)

| Criterion | Measure | Target |
|---|---|---|
| Owner identity check | Side-by-side captures, mod-off vs mod-on, at all 12 capture points | Owner judges every pair "GTA IV, remastered"; any "looks like another game" is a fail |
| UI design language | Every screen uses only the Liberty UI tokens (palette, fonts, spacing, corner radius) | 100% of screens; token check automated where the UI is data-driven |
| UI readability | Smallest text at 1280x720 virtual | ≥ 14 px; full navigation with controller **and** keyboard/mouse |
| UI responsiveness | Menu open to first drawn frame / open animation | ≤ 1 frame / ≤ 200 ms |
| Art pack quality | Every shipped texture has a full mip chain, a size within its surface class, a recorded source/licence | 100% (tool-verified); 0 unlicensed assets |
| Showcase coverage | Broker/Dukes capture points with an area-specific art change | 8/8 |
| Citywide coverage | Shared road/sidewalk/glass/emissive textures replaced where used | Listed per texture in the pack manifest |

Budget: UI draw ≤ **0.5 ms** average; no new streaming hitch > **100 ms** on the scripted Broker/Dukes drive.
**VRAM (measured GPU: RX 570 4 GB):** original budget: normal overhead **+250 to +300 MB** over vanilla; hard ceiling **+350 MB** in the
worst-case scene (Pillar 5). 350 MB is a ceiling, not a target to fill. Every texture has proper mipmaps and sensible
compression; no blanket 4K; higher resolution only where it visibly improves the result. The owner's decision on a
proposed **+300 MB hard ceiling** is pending; retain +350 MB as the written criterion until answered. Measure actual
headroom on the 4 GB card alongside CPU, streaming and frame pacing.

### Pillar 2 — Harsh violence (gore, combat effects)

| Criterion | Measure | Target |
|---|---|---|
| Exact attribution | Gore/effects driven by exact damage events, never proximity scans | 100% (code review + event log) |
| Hit response latency | Exact `PedDamaged` to visible entry effect | ≤ 1 engine frame |
| Severe headshot | Pistol headshot ≤ 10 m on a spawned ped, autopilot trials | severe effect in ≥ 95% |
| Shotgun trauma | Shotgun hit ≤ 5 m, autopilot trials | trauma/limb effect in ≥ 90%; dismemberment only where reliable (0 floating or flashing limbs in 50 trials) |
| Contextual effects | Muzzle flash/smoke per weapon class; impact by material once R2 passes | Each Stage 1 weapon has its own configured effect set |
| Persistent aftermath | Bodies, blood decals/pools, active effects | Bodies stay **3–5 minutes** normally; blood may outlast bodies where performance allows. Hard caps in `config/` (proposal: 10 bodies, 64 decals, 24 active effects), distance-based cleanup, adaptive cleanup under performance pressure; oldest removed first. Gameplay performance wins over keeping every body or decal |
| Suffering and panic | Wounded non-fatal NPCs; nearby peds after severe violence | crawl/writhe/pain behaviour on a configurable share of survivable severe hits; brief, never a scripted loop |

Budget: gore + effects **≤ 0.8 ms** average, **≤ 4 ms** peak during the 10-ped firefight scenario; frame p95 in that
scenario ≤ **+15%** vs mod-off.

### Pillar 3 — Physical world (weapons, gunplay, inventory)

| Criterion | Measure | Target |
|---|---|---|
| First-shot accuracy | Aimed, standing, first shot at 25 m on the test range (bullet events) | pistols/SMGs within a configured cone (proposal ≤ 0.5°); owner feel sign-off per class |
| Burst control | 3-round burst then pause | recovers to the first-shot cone within the weapon's configured recovery time |
| Sustained fire | 30-round full-auto AK burst | vertical climb within the configured range for that weapon (proposal 6–12°); never "laser" (spread grows every shot) |
| Reticle truthfulness | Reticle opening vs the spread model's live cone, across stand/crouch/move/sustained fire/recovery (logged per frame in a test-range scenario) | opening within ±5% of the cone at every sample; each Stage 1 weapon class shows its own configured reticle; reticle drawing ≤ 0.1 ms |
| Shoulder swap | On foot, in cover, near walls (scripted camera scenario) | works in all states; 0 camera clips into walls at the test spots |
| Visible loadout | Carried weapons visible on the body | 100% on foot; hidden in vehicles and cutscenes; 0 orphaned/floating props after 100 autopilot vehicle enter/exit cycles |
| Clipping | Front/side/back review for every Stage 1 outfit and weapon class | owner pass on every pair |
| Inventory integrity | Store/take trunk round trips; death/busted rules; save/load | 100% round trips; 0 lost owned weapons across 50 death cycles; state identical after save/load |
| Mission compatibility | Main Broker/Dukes story missions | 0 blocked missions; mission-given weapons behave as the game expects |

Budget: gunplay + arsenal + holsters **≤ 1.5 ms** average combined (today about 1.2–1.5 ms), no single frame spike
> **5 ms** from these modules outside menus.

### Pillar 4 — Dangerous Liberty City (atmosphere, audio, population)

| Criterion | Measure | Target |
|---|---|---|
| Ambient events | Scripted city events (screams, arguments, sirens, alarms, distant violence) in Broker/Dukes | configurable rate (proposal: one noticeable event every 60–180 s at night, rarer by day); never two within 20 s |
| Reaction to violence | Nearby peds after severe violence | panic/scream/flee in ≥ 90% of autopilot trials |
| Population variety | 60 s at each capture point, snapshot of visible peds (model + variation) | ≥ 8 distinct looks among any 15 visible peds; no identical pair within 10 m |
| Vehicle variety | Scripted 2-minute Broker/Dukes drive | ≥ 12 distinct models |
| Density preserved | Ped and traffic density | never below vanilla; the density governor is off (owner decision 2026-09-30) |
| Lighting/weather | Target look (cold day, overcast, wet, dark readable nights) | owner sign-off at the 12 capture points, day/night/rain |

Budget: atmosphere + ambient audio + population scripts **≤ 0.5 ms** average.

### Pillar 5 — Performance conscious (whole mod)

**Worst-case scene** (scripted, repeatable): Broker/Dukes, night, rain, normal or high traffic, normal pedestrians, a
firefight in progress, blood and effects active, Liberty UI/HUD on. Measured in it and at every capture point: average
frame time, p50, p95, p99, module cost, VRAM, memory pressure, streaming stalls and visible hitching (owner check).

| Criterion | Target |
|---|---|
| VRAM over vanilla | normal +250 to +300 MB; **≤ +350 MB** in the worst-case scene |
| Stage 1 script cost inside `engine.frame` | ≤ **3 ms** average at every capture point; no module above its `moduleBudgetMs` |
| Frame time vs mod-off | p95 ≤ **+10%**, p99 ≤ **+15%** at every capture point |
| Stalls | none > **1 s** outside teleports and loading; no `engine_stall` in a 1-hour soak |
| Memory | private bytes growth ≤ **50 MB/hour** in the soak; free address space never below `lowAddressSpaceMegabytes` (600 MB) |
| Screen effects (after R4) | ≤ **0.5 ms** |
| Density | no ped/traffic reduction used to meet any budget above |

## 10a. Environment art-pass order

Broker/Dukes scopes progression and where the deepest art pass happens **first**; citywide systems and shared-texture
fixes apply everywhere (section 4). Each area is finished to the Hove Beach standard before the next starts.

1. **Hove Beach — the benchmark.** Storefronts, signage, roads, sidewalks, apartment facades, grime, glass,
   underpasses, lighting, wet streets, night atmosphere, graffiti, street clutter. *If Hove Beach looks like a believable
   Rockstar remaster, the art direction is working.*
2. **Firefly Island / Firefly Projects.** Boardwalk, amusement area, neon and emissives, wet surfaces, signs,
   storefronts, environmental lighting. One of the strongest night showcases.
3. **Schottler / Rotterdam Hill.** Dense residential and commercial streets, neighbourhood storefronts, road and
   sidewalk quality, building materials, graffiti, street furniture: the everyday city after the remaster.
4. **BOABO / downtown Broker.** Industrial-commercial look: concrete, metal, glass, larger roads, facades, clutter,
   lighting.
5. **East Hook, docks and industrial areas.** Grime, rusted metal, concrete, industrial lighting, warehouses, night,
   fog and wet-weather presentation.
6. **Dukes.** Major roads, East Island City, airport-adjacent areas, commercial corridors, heavily travelled routes.

## 11. Compatibility

- Story missions: mission-given weapons, cutscenes, busted/wasted and saves must work with the physical inventory
  (Arsenal already tracks mission weapons). Every Stage 1 system is disabled during cutscenes where it could conflict.
- FusionFix is the rendering baseline; DXVK is benchmarked (DX9 vs Vulkan pacing), not reimplemented.
- Liberty Vehicle Services CE is a reference and compatibility target, not a required dependency.
- Keyboard/mouse and controller both supported.

## 12. Owner decisions (2026-09-30)

1. **Loadout:** 2 long guns + 1 sidearm; one long gun equipped, the other slung (section 7, Slice A). SMGs (Uzi, MP5) count as long guns.
2. **Art sourcing:** identity-defining assets hand-designed; repeatable surfaces procedural or CC0 (section 3). GPT Image
   2.5 available through the owner's subscription.
3. **VRAM:** original +250 to +300 MB normal, +350 MB hard ceiling in the worst-case scene (Pillars 1 and 5).
   Hardware correction 2026-10-01: RX 570 **4 GB**; proposed +300 MB hard ceiling awaits the owner, not yet adopted.
   The other section 10 numbers remain proposals, tuned when their scenarios exist.
4. **Gore:** very harsh, grounded; suffering, crawling and contextual executions allowed; bodies 3–5 minutes with hard
   caps and adaptive cleanup (Pillar 2).
5. **Art-pass order:** Hove Beach first, as the benchmark (section 10a).
6. **Population:** no automatic thinning of peds or traffic for performance; find other optimizations (density governor off).
7. **Excluded weapons** (P90-type, MG36, snipers) stay in the game, out of normal Stage 1 availability, always obtainable from the DevTools/mod menu.
