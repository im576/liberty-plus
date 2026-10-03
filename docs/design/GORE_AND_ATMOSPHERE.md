# Liberty+ — gore and world atmosphere briefs

Prepared 2026-10-02. These are planning briefs, not implemented features or runtime
promises. The owner selected these as the next focus after settling the workflow.
Gore direction is grounded and severe. Environment covers every time of day/weather;
feature development stays paused.

## Gore: intended player experience

Combat should produce a convincing, coordinated injury aftermath: localized wounds,
bleeding, splatter, growing pools, injury reactions, dismemberment with detached
heads/limbs and convincing cut surfaces, plus bounded persistent blood and bodies.
Presentation should look cleaner and more deliberate than simply adding more blood.
Environmental destruction is outside this package. Existing Violent Liberty findings
are research references; shipped source/assets need established reuse permission.

### Required wound and blood overhaul

The full target includes the behaviors below. These are requirements to develop and
validate, not claims that the current code implements them. A missing capability
remains an explicit research dependency; it does not silently shrink the product.

| Required behavior | Observable completion criterion | Starting point / research dependency |
|---|---|---|
| Impact-position bullet-hole wounds | Visible wounds correspond to the actual bullet contact and remain attached to the correct body surface during animation, ragdoll and repeated hits, without floating, sliding or obvious clipping. Multiple wounds remain individually represented. | Existing damage events are a starting point, not proof of surface placement. Validate contact attribution and an original wound rendering/attachment path, including animated surface coordinates where needed. |
| Weapon- and anatomy-dependent wounds | Representative pistol, rifle and shotgun hits produce deliberate differences in wound appearance, bleeding and severe trauma according to weapon/impact, body region and injury severity. The same treatment is not stamped onto every hit. | Reuse existing classification and tuning where valid; verify what damage/impact data is reliable and author original wound variations. Artistic thresholds remain tuning decisions. |
| Blood leakage over clothing/body surfaces | Blood visibly spreads or runs downward from the wound and remains coherent as the victim moves or falls; it is not merely a repeated particle burst near a bone. | Existing fading/slowing pulses are a baseline. Investigate surface-following blood, animated attachment, gravity/orientation and material behavior. |
| Pressure streams and post-death bleeding | Severe injuries can produce directed streams that weaken over time, with distinct post-death leakage. Wound and severed-stump bleeding agree with injury location; overlapping systems do not double the same effect. | Validate original stream rendering, direction, timing and emission lifetime. Existing stump/wound emitters do not establish the complete behavior. |
| Splatter, surface drips, growing pools and trails | Blood reaches the struck surface with appropriate orientation; ground, walls, glass and vehicles show coherent stains/drips. Pools grow beneath bleeding bodies and trails follow bleeding movement or dragging. Moving vehicles retain attached marks. Effects have bounded persistence and cleanup. | Research surface contact, rendering/decal ownership, moving-surface attachment and pool/trail control. The archived no-op blood-surface implementation does not count as completion. |
| Advanced dismemberment and cut surfaces | Supported head/limb cuts retain the missing part, present a matching detached piece and an original convincing exposed cut surface, and coordinate stump bleeding. Animation/ragdoll, repeated hits and cleanup do not produce flashing/floating parts or unstable clones. | Stabilize the existing skeleton/clone approach first. Validate original cap geometry/material attachment and supported cut sites; unresolved anatomy/model coverage must be recorded. |
| Injury reactions and persistent aftermath | Survivable injuries, death, severed parts and blood share the same injury history. Reactions respect animation/mission behavior; bodies and blood persist for explicit measured durations without contaminating a recycled entity. | Reuse candidate reaction/persistence work selectively after evidence review. Establish victim identity/generation, wound state and resource ownership before integration. |

Violent Liberty is a behavioral and visual comparison reference, not a required
runtime companion or a source/asset bundle to copy. Use the
[existing inspection](https://github.com/im576/liberty-framework/blob/main/docs/research/ViolentLiberty.md)
and [author's feature description](https://www.reddit.com/r/GTAIV/comments/1wi7cak/violent_liberty_dynamic_blood_overhaul_launch/)
to prepare a comparison checklist and reference captures where available. Record
which observations were actually inspected and the tested version/configuration.
Develop original implementation/assets unless specific reuse permission is established.

"Improved over Violent Liberty" means demonstrating improvements in wound alignment,
bleeding/surface behavior, cut fidelity, visual consistency, stability or measured
cost under comparable conditions. It is not an unsupported claim of superiority or
a requirement to increase blood volume indiscriminately. The owner reviews the
assembled moving result against those explicit comparison goals.

Damage should drive one injury record per victim lifetime, containing individual
wounds and cut states rather than one shared location for all hits. Wounds, reactions,
bleeding and severed-part presentation should agree about location, severity and
timing. This is a proposed contract, not a claim that current main has it. Ped handle
reuse must not transfer old injuries to a new person. Rain/blood interaction is an
integration question to investigate, not an already promised wash-away feature.

### Existing foundation and unknowns

Main has a combat baseline; unaccepted lane snapshots contain further gore work.
Framework damage records and skeleton/clone techniques exist. Exact wound surface
placement, convincing authored caps, blood pool control and stable combined lifetime
are not established. A particle return value does not prove visible placement.

Start from existing crash evidence: compare scene setup with cuts off and the same
fixture with one controlled cut before attributing a pre-cut crash to severing.
Instrument victim generation, skeleton/clone lifetime and release order. Research
cap geometry/material attachment and blood rendering separately once the relevant
path is isolated. Do not import the entire unaccepted lane or replace it with another
approximate health-polling system without evidence.

### Acceptance for an assembled candidate

- Each required behavior above has a recorded implementation status, dependency,
  relevant test/capture and remaining limitation. A partial feature is labelled partial.
- Visible wounds, bleeding, pools and severed parts agree with the actual injury.
- Cut surfaces look deliberate; missing geometry is not disguised as completed gore.
- Reactions coordinate with animation/ragdoll and mission behavior; unknown behavior
  is reported rather than forced globally.
- Death, victim despawn, streaming, repeated hits, disable and reload release owned
  resources and cannot corrupt the next entity using a recycled handle.
- Persistence has explicit measured duration/capacity rules, chosen from evidence
  and owner expectations; no arbitrary numeric limits are treated as settled here.
- Combined combat across representative weather/time scenes meets agreed frame-time
  gates without hiding the feature, reducing population or counting an offline build
  as gameplay acceptance.
- Owner reviews motion and appearance on the assembled candidate.

Use controlled single-hit fixtures for representative weapon classes and body
regions, then movement/ragdoll, repeated hits, post-death bleeding, surface contact,
streaming and sustained multi-victim combat. Include surfaces and detached parts in
cleanup/disable/reload checks. Distinguish exact engine-observed damage from inferred
events; an inferred hit must not be presented as proven impact-position wound data.
Capture matched baseline/candidate motion under representative lighting and weather.

Record the applicable existing acceptance budgets and any explicitly agreed additions
before runtime acceptance. Measure dedicated gore work and combined frame times,
including average/peak work, p95/p99 frames, active wounds/emitters/parts/marks and
cleanup cost. Keep scene population, input load and renderer/plugins comparable;
the combined target remains 1080p/60 FPS. Do not weaken established gates or omit
required effects to pass. Final thresholds, persistence limits and artistic tuning
remain open where not already agreed.

### Framework and agent development responsibilities

Liberty+ owns injury rules, weapon/anatomy tuning, original gore art, presentation and
the comparison/acceptance checklist. Framework owns reusable validated damage,
attachment/rendering and resource-lifetime capabilities. Review main and archived
candidate evidence before selecting what to extend; isolate existing crashes before
adding dependent rendering and effects. Promote proven mechanisms into narrow
Framework services and use them to assemble the full Liberty+ system.

Use the research and evidence skills to investigate gaps, and feature-delivery to
coordinate the shared injury contract and assembled checks. After a technique has
been demonstrated, capture it in maintained references, tools and a relevant gore
skill so later work does not rediscover it. Skills support the agent's choice of
method and exceptions; they do not prohibit a better approach or substitute for
reasoning. Repetitive validated transformations belong in tools. This brief does
not dispatch agents, authorize implementation/game runs or claim those tools exist.

Approved artistic direction: **grounded and severe**. Effects should follow injury
severity, weapon/impact and anatomy; avoid indiscriminate severing or exaggerated
blood output purely for spectacle. Exact assets, injury thresholds and persistence
limits still need evidence and tuning.

## World atmosphere: intended player experience

Liberty City should feel polished throughout daytime, nighttime, sunny, cloudy,
overcast, foggy and rainy conditions. Continue tuning the existing visual work,
including convincing clear and heavy skies/clouds, readable lighting, and appropriate
rain, wet streets/materials and reflections in wet weather.
Preserve natural color in gloomy scenes. Build on the visual direction the owner
already likes; avoid turning the city gray or crushing dark areas. Target remains
1080p/60 FPS with the whole mod, not a static screenshot alone.

This focus is atmosphere/world presentation. It does not add environmental destruction
or silently expand into a complete map replacement, new population AI or simulation.

### Existing foundation and unknowns

Mood presets, a timecycle generator and capture history exist. Restored original
game tables are the current installation baseline. Timecycle color/lighting edits
do not overhaul cloud shapes. Cloud assets/render consumers, controllable wetness,
reflection behavior and rain effects must each be checked against existing code and
research before selecting an implementation. Do not assume an engine replacement
or an extra post-processing dependency is required.

Separate the work into color/lighting, clouds, rain, wet materials/reflections and
ambient presentation. Independent research/asset preparation can proceed together
when authorized, with shared renderer/plugin changes coordinated. Reuse existing
generators and install/rollback receipts rather than building a new tuning pipeline.

### Acceptance for an assembled candidate

- Gloom retains visible natural color, readable characters/HUD and useful shadow detail.
- Clouds visibly improve in motion; grading alone is not counted as cloud completion.
- Rain/wetness/reflections form a consistent scene with no obvious clipping, sparkling
  or exaggerated mirror surfaces; implementation limits remain explicit.
- Matched baseline/candidate sunny, cloudy, overcast, foggy and rainy daylight,
  dusk and night captures include street-level walking/driving, weather transitions
  and interior/exterior changes.
- Original tables/settings restore byte-for-byte when the candidate is removed.
- Combined gore/atmosphere gameplay is profiled at 1080p, separating cold asset work from
  sustained frame times. Target 60 FPS is not yet proven.
- Owner signs off on the actual moving game, with concept references kept separate.

Approved scope: tune the existing overall look across day/night and all relevant
weather, preserving natural color, alongside a substantial **cloud overhaul**.
Rain was an example, not the whole assignment. Final weather/time presets and
cloud assets need actual captures and owner review; no new rain mechanic is implied.
