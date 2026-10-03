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

Damage should drive one injury record per victim lifetime. Wounds, reactions,
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
