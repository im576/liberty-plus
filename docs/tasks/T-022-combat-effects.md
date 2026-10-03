# T-022
> **Update 2026-09-25 (Claude, limb rework after playtest) â€” NEEDS-PLAYTEST.**
>
> **Owner-reported bugs:** a leg "falls up" and floats; an arm shows as the whole NPC, then vanishes. Research ([research/Dismemberment.md](../../../GTAIV-Reborn/docs/research/Dismemberment.md)) confirms our approach matches the leading GTA V mod: bone collapse through a hook, plus a cloned ped for the severed part. The missing piece is stump caps.
>
> **Fixes:**
> - The clone spawns 0.7 m beside the corpse at ground height.
> - It stays invisible until the engine confirms the collapse on its current skeleton for 3 ticks.
> - Skeleton moves are detected every tick with `CPed::BoneMatrix`. A moved clone is hidden and reconfirmed.
> - After 1.5 s, a limb whose joint is more than 0.45 m above ground is removed.
> - An unresolved knee or elbow cut falls back to the hip or shoulder.
>
> **Config:** `severedLimbSpawnOffsetMeters`, `limbConfirmTicks`, `limbSettleMilliseconds`, `limbFloatingHeightMeters`. `dismemberRefreshMilliseconds` is now 0, because per-tick detection is cheap.
>
> **Log lines:** `dismember_limb_visible ... confirm_ticks=`, `dismember_limb_rehidden`/`reshown`, `dismember_limb_floating_removed`, `combat_sever_fallback`. — Combat effects detection and safe prototype

> **Update 2026-09-24 (Codex, bleed and limb presentation pass) — NEEDS-PLAYTEST.** Owner confirmed Violent Liberty enlarged stains and added streaks, but ordinary bodies did not visibly leak and thrown limbs were inconsistent. The fresh log showed `START_PTFX_ON_PED_BONE` returned zero for all tested looping blood effects, while one-shot blood played. New bounded one-shot leaks follow hit/stump bones and fade/slow over time; fatal wounds last longer. The companion INI now selects high head/neck pressure, long fatal duration and 75–100% shotgun body-bleed chance. A thrown-limb clone remains mission-owned until timed cleanup, failed spawns retry up to the configured limit, capacity replaces the oldest clone, and one blood effect plays near landing. Duplicate same-part cuts are suppressed. Build: 112 sources, zero errors/warnings; offline verifier 339/339. Updated install/rollback fixture restored bytes exactly; real install verified all 7 files. Backup `scripts/LibertyFramework/backups/violent-liberty-20260924-231558`. Exact owner checks: [section 7e](../../../GTAIV-Reborn/docs/archive/testing/PHASE2_PLAYTEST.md#7e-bleed-and-limb-presentation-pass). No in-game result for this pass yet; cut cap geometry and exact impact-position visuals remain open.

> **Update 2026-09-24 (Codex, companion visual pass) — NEEDS-PLAYTEST.** Owner confirms pass 3 throws limbs and keeps them removed on lethal limb shots; visual quality remains poor. The local Violent Liberty 1.2.2 archive has wound/surface visuals but no source. The `bloodVisualMode` switch now lets its ASI own ordinary blood while our cut engine owns limb removal and focused stump effects. See [research/ViolentLiberty.md](../../../GTAIV-Reborn/docs/research/ViolentLiberty.md). The companion is installed with Vulkan selected; startup and combined visuals await an in-game test, especially because an earlier DXVK-only startup attempt crashed. The cut still lacks cap geometry/material. Build: 112 sources, zero errors/warnings. Offline verifier: 336/336. Copied install/rollback test restored every file hash. Real install verified all 7 files; backup `scripts/LibertyFramework/backups/violent-liberty-20260924-224005`.

> **Update 2026-09-24 (Claude, gore pass 3 after playtest 2) â€” NEEDS-PLAYTEST.**
>
> **What the Gore Test log showed:**
> - 9 of 15 blood effects are looping effects, which the one-shot trigger refuses.
> - The fragInst hooks never matched a ped (`hook_calls=0`), so the collapse only held on frames the engine skipped. That caused the flickering head.
> - Severed corpses vanished right after the thrown-limb clone was spawned.
>
> **Now:**
> - Looping effects are started and stopped (`BloodEffects`), so wounds and stumps give continuous streams, drips and chunks. The game's `SET_CHAR_BLEEDING` is on for every wounded ped.
> - The collapse runs natively after every `crSkeleton::Update` call (8 call sites) and after the ragdoll sync (ADR-0005 rev 2).
> - Corpses are made mission-owned while severed.
> - Collapsed bones get a 0.01 scale, and severing waits 300 ms after death.
>
> **Evidence to collect:**
> - `engine_resolve skeleton_update ok ... sites=8`
> - `skeleton_collapse_engine_installed patches=9`
> - `dismember_evidence ... engine_hits=N` (N > 0 means the engine path works)
> - `ptfx ... loop`
> - no `dismember_corpse_lost`
>
> **Update 2026-09-24 (Claude, gore overhaul): superseded by pass 3 above** — NEEDS-PLAYTEST.**
>
> **Why the owner saw no gore:**
> - The ped-skeleton resolver failed at runtime, so dismemberment never armed.
> - Every particle call passed an int 0 scale. `TRIGGER_PTFX_ON_PED_BONE` reads a float, so nothing showed.
> - Effects only ran for gold weapons.
> - Victims weren't dead yet on the frame they were hit.
> - `blood_stun_punch` is tiny.
>
> **Now:**
> - **Hits:** every firearm, and mission peds too (`allFirearms`, `includeMissionPeds`), gets weapon-specific stock blood:
>   - entry spray, plus exit spray at 25+ damage;
>   - chunks at 60+ damage, or for shotguns and snipers;
>   - blood from the mouth on head/torso hits.
> - **Bleeding:** each wound drips for 25 s (up to 64 emitters).
> - **Severing:** a lethal limb hit (20+ damage, or the ped dies within 1.5 s) severs the limb. A lethal head hit (40+ damage) decapitates.
>   - Burst and mist fire at the stump, which then sprays arterially for 9 s.
>   - The limb is thrown off.
>   - The collapse is re-applied after every engine skeleton rebuild through the ADR-0005 hooks, with a per-tick fallback.
>   - Decapitation falls back to `EXPLODE_CHAR_HEAD`.
>
> All names and thresholds are in `combat_effects.json`.
>
> **Evidence:** `skeleton_hook_installed` ×2, `dismemberment_ready ... hooks=True`, `combat_hit`, `combat_sever`, and `dismember_evidence ... hook_calls=N`. A nonzero `hook_calls` means the hooks are doing the work.

> **Update 2026-09-24 (Claude): arm and leg dismemberment implemented and installed — NEEDS-PLAYTEST.**
> A **lethal** hit with a gold weapon on an ambient NPC's arm or leg severs it: upper-arm/thigh hits cut at the shoulder/hip, forearm/hand/calf/foot hits at the elbow/knee (`CombatEffects/Logic/LimbCutPlan.cs`). The corpse's limb bones are collapsed into the joint every tick through the engine's own bone-matrix functions (`GameApi/PedSkeleton.cs`, resolved and verified in MEMORY.md), a blood effect starts on the stump, and a severed limb is thrown: a clone of the same ped wearing the same clothes with every other bone collapsed into the limb, ragdolled with an outward push. The thrown limb only spawns after the engine has been shown to keep the collapsed bones on that corpse, so a full-body clone is never shown. Before arming, the feature validates the ped pool and skeleton on the player's own ped. Limits: 6 severed corpses, corpses kept severed for 120 s, limbs removed after 60 s (`combat_effects.json`: `dismembermentEnabled`, `severedLimbEnabled`, `severedLimbForce`, lifetimes, `maximumSeveredPeds`, `stumpEffectName`).
> **Evidence to collect:** `dismemberment_ready`, `dismember limb=...`, `dismember_evidence persisted_ticks=N overwritten_ticks=M`, `dismember_limb_thrown`. If `overwritten_ticks` dominates, the engine rebuilds the skeleton each frame, so the limb will not visibly disappear and no limb is thrown. Report it; a render-time hook would then be the next step. Stumps are the collapsed mesh around the joint (no new stump model yet).
> Human test: kill ambient NPCs with a gold weapon shot to a leg, then an arm (e.g. aim low with the carbine). Expect the limb gone at the knee/elbow/hip/shoulder, blood at the stump and the limb flying off. Also shoot a corpse's other limbs, switch to a vanilla weapon (the limb must not come back), and run `ReloadScripts` (clones are dropped; corpses may regain limbs).

Status: **NEEDS-PLAYTEST**. Offline build and native registration verification pass. Physical reaction force, bone-attached stock blood PTFX, and a corpse-only head removal spike are implemented but unverified in game. The owner requested that corpse head removal be enabled in the next installed playtest build. Actual arm/leg mesh removal is **not implemented**; limb-loss candidate logs are diagnostic. The effect uses the player's installed GTA IV resources, with no copied third-party asset.

## Implemented

- Separate `CombatEffectsController` script, live `combat_effects.json`, master and per-feature toggles.
- Polls at a configured interval, only within radius/cap, only for registered test weapons 58–60, outside missions, and only for non-player, non-mission peds. Attributes health loss to the player with the existing SHDN wrapper and uses the CE-registered last-damage-bone native.
- Maps GTA IV bone IDs to head, torso, left/right arm, left/right leg; unmapped bones stay unknown. Tracks per-ped region hits, bounded wound records, and a non-mutating limb-loss candidate flag. Applies distinct configured directional force per region, a one-shot impact PTFX at the damaged bone, and a bounded attached wound PTFX. Stops handles on expiry, despawn, range exit, disable, weapon switch, mission, or script reload.
- Optional `headLossPrototypeEnabled` calls the game's own `EXPLODE_CHAR_HEAD` only after an attributed lethal head hit to a non-mission NPC, once per corpse. There is no arm/leg mesh removal. No new memory access or redistributed art.

## Human test steps

1. With GTA IV **closed**, include `combat_effects.json` in the staged Phase 2 config and install the integrated DLL/config package. The integrated package enables bounded reactions/blood for gold weapons, with `headLossPrototypeEnabled=false`. Launch IV, use L3+R3 to give/select gold pistol 58, and confirm gameplay and stock hit reactions work. Look for `combat_effects_config_loaded enabled=True` in `scripts/LibertyFramework/logs/LibertyFramework.log`.
2. If the effects cause a problem, set `enabled` to `false` in the installed `scripts/LibertyFramework/config/combat_effects.json`; the script should stop its tracked PTFX within two seconds. Re-enable for the checks below.
3. In free roam, use gold pistol 58 to shoot an ambient non-mission NPC once in the torso, head, each arm and each leg (separate fresh targets if needed). Confirm distinct push directions/strengths and bone-local blood effects. Confirm `combat_reaction region=... bone=... damage=...` in the log; record any `Unknown` bone value or missing effect. Repeat with 59 and 60. Test Range spawned peds may be mission peds and are deliberately excluded.
4. Fire two qualifying hits to one limb with `limbLossPrototypeEnabled=true`. Look for exactly one `combat_limb_loss_candidate` line for that limb. Confirm there is no arm/leg model change; this is a detection safety test only.
5. With the installed default `headLossPrototypeEnabled=true`, score an attributed lethal head hit on an ambient NPC with enough damage. Confirm one `combat_head_loss` line and whether the corpse head changes. If the game hangs or the model looks wrong, set `headLossPrototypeEnabled=false` in the installed config and attach the log.
6. Switch to a vanilla weapon, enter a mission, and then disable `enabled`. Confirm no further `combat_reaction` entries from those contexts. Trigger ScriptHookDotNet `ReloadScripts`; check fresh config-load entry, wound PTFX stops, and gameplay continues. Attach log and visual observations.

## Engine questions before visual implementation

- Confirm `GET_CHAR_LAST_DAMAGE_BONE` returns the documented GTA IV PedBone IDs (head 1205, etc.) on this CE build.
- Verify that the CE-registered `APPLY_FORCE_TO_PED` produces the intended forces without disrupting AI, missions, or ragdolls. It has only offline signature and hash checks so far.
- Verify the stock `blood_stun_punch` PTFX can trigger/start/stop on damaged bones. If its START form is not persistent, replace it with an original decal/particle asset after obtaining a CE-safe path.
- A collision-safe arm/leg replacement and original stump meshes require an asset pipeline and game test. There is no validated native that hides individual limbs.

## Offline evidence

`tools/build.ps1`: 99 files, zero errors/warnings. `tools/verify.ps1`: 252/252, including CE handler registration and installed ScriptHook.dll name-to-hash mapping for the six newly called natives. In-game behavior awaits the owner's test.
