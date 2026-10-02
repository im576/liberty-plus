# T-014 — Aim profiles

Status: **NEEDS-PLAYTEST** (Codex Agent B). `freeAim.profile` accepts `vanilla` and `free`; live config reload switches the guarded `FreeAimMode`. `slowdown` and `light` are rejected with a clear message, and the last valid config remains active. A CE control for independent slowdown/magnetism is not documented in MEMORY.md or NATIVES.md; an engine research spike is needed before implementing them.

## Human test steps

1. Set `freeAim.profile` to `free` in runtime `gunplay.json`. Aim at a ped with a gold gun and a vanilla gun; neither should snap or lock. Exit and check the player's Auto-Aim setting was restored.
2. Set the profile to `vanilla`; open DevTools > PRESETS & CONFIG > `Reload config from disk` with Cross. Close the menu and aim again; the player's own Auto-Aim setting should apply.
3. Set the profile to `slowdown` and reload. The menu should report rejection, the log should name the missing CE assist control, and the previous valid profile should remain. Restore `free` afterward.
