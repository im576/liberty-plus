# T-015 — Shoulder swap

Status: **NEEDS-PLAYTEST** (implemented and installed 2026-09-24, Claude). Previously blocked on a validated camera control; the control was found by static analysis of GTAIV.exe 1.2.0.59.

## How it works

`CCamAimWeapon`'s update chooses a 40-byte settings record per camera state from a table in game data (0x103C118, 15 records). Field `+0x10` is multiplied by the camera's right vector to place the camera beside the shoulder: 0.475 m on foot, 0.2 m in cover, 0.375 m in another state. Fields `+0x1C/+0x20` are the pitch limits clamped by 0xA25230, which proves the table drives the aim camera. See [MEMORY.md](../game-api/MEMORY.md).

Shoulder swap scales `+0x10` of every record by a side factor that slides between +1 (right, vanilla) and −1 (left) over `transitionMilliseconds`. It is a data write only (no code patch, ADR-0004): the resolver finds the table from code shapes, the runtime validates the originals (each |x| ≤ 1.5 m and a right-shoulder value present) and restores them on toggle-off, error, script unload and process exit. Verify checks table address, record count, field offset and the 0.475/0.2 values against GTAIV.exe; FusionFix patches no byte the resolver reads.

Config `gunplay.json` → `shoulderSwap`: `enabled`, `controllerButton` (default `LeftShoulder` = LB/L1), `keyboardKey` (default `Z`), `transitionMilliseconds` (180), `requireAiming` (true). The chosen side persists after aiming ends until swapped back. Liberty Tweaks and Liberty Shoulder were not used (reference only; no code, no binary).

## Human test steps

1. On foot, hold L2/LT to aim and press **L1/LB** (or **Z**): the camera slides to the left shoulder in about 0.2 s; aim and crosshair stay centred. Press again: back to the right. Log: `shoulder_settings_validated`, `shoulder_swap side=left/right`.
2. Swap to the left, release aim, walk, aim again: still left. Take cover and aim over the edge on both sides; report clipping.
3. Aim from a vehicle (drive-by) and check nothing odd happens with the car camera.
4. Swap left, open the pause menu, then run `ReloadScripts`: after the reload the camera is back on the right. Exit the game from the pause menu.
