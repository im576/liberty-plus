# T-017 — Feel: camera shake and aiming FOV

Status: **NEEDS-PLAYTEST** (Codex Agent B). Per-shot pitch/heading jitter is applied through the validated aim camera after the recoil step. Camera FOV is saved on entry, eased toward the configured reduction and restored on aim/weapon exit or error. All values live in `gunplay.json`; vanilla weapons do not enter the effect. The ScriptHookDotNet `Camera.FOV` setter requires live confirmation on the game camera.

## Human test steps

1. Aim and fire the gold pistol, carbine and shotgun on foot. Expect subtle per-shot shake after normal recoil, with no sustained vibration. Aim FOV should narrow slightly and return when L2/LT is released.
2. Switch to each vanilla counterpart and repeat. There should be no LF shake or FOV change. Aim from cover and in a vehicle; note unusual camera movement.
3. While aimed with a gold weapon, set `feel.enabled` to false in runtime `gunplay.json`, then use DevTools > PRESETS & CONFIG > `Reload config from disk`. FOV should restore. Attach the log and describe any nausea or camera snap.
