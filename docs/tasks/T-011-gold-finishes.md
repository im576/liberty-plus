# T-011 — Gold carbine and gold shotgun

Status: **NEEDS-PLAYTEST** (Codex Agent B). Two variants were added to `assets/finishes/finishes.json` through the existing finish pipeline.

- `lf_gold_carbine` from `w_m4`: textures `bm_m4a1` diffuse, `bm_m4a1_s` specular, `icon` icon (keep `bm_m4a1_n`); IDE `gun@ak47`, draw distance 50, `CM_WEAPONS_M4`; `weaponInfoType` `LF_GOLD_CARBINE`.
- `lf_gold_shotgun` from `w_shotgun`: `cj_shotgun_comp` diffuse, `cj_shotgun_comp_s` specular, `icon` icon (keep `_n`); `gun@shotgun`, 50, `CM_WEAPONS_SHOTGUN`; `LF_GOLD_SHOTGUN`.
(Texture names were listed from the installed `weapons.img`; IDE values from `common/data/default.ide`.)

Acceptance: `tools/build-finishes.ps1` succeeds (read-only on the game), previews for both exist under `staging/phase1/previews`, a verify check asserts both variants, config docs updated. Human test: both guns look gold in hand and on the HUD icon.

Offline: finish builder succeeded and read back all 6 IMG entries; before/after diffuse, specular and icon previews exist for both weapons. Normal maps were retained.

## Human test steps

1. Open DevTools with L3+R3 held (or F10), enter WEAPONS, select `Give Gold Carbine (ID 59)`, and press Cross twice. Close DevTools. Inspect the held gun and HUD icon: both should be gold, with normal-map detail.
2. Repeat with `Give Gold Shotgun (ID 60)`. Reload, aim and fire both; the finish should remain.
3. If holsters are active, put each gold weapon away and inspect its back prop. Attach screenshots and the runtime log.
