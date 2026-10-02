# Archived one-shot scripts (Phase 1 bring-up)

Historical scripts for the T-001/T-002/T-003/T-007 spikes, the Phase 1 gold-weapon package/install/rollback, and the
one-time SSD moves. They still run (they resolve the repository root from this folder) but nothing in the current
engine flow uses them. The live tools index is [../README.md](../README.md). Installed builds now go through
`package-phase2.ps1` / `install-phase2.ps1` and the local verifier.

Scripts here: `deploy-t001`, `deploy-t002`, `deploy-t007`, `prepare-t007`, `upgrade-t007`, `remove-t007`,
`test-t002-live`, `package-phase1`, `install-phase1`, `rollback-phase1`, `move-game-to-ssd`,
`move-streaming-to-ssd`, `restore-streaming-from-ssd`.

## Old per-task notes

T-001 is a load/log/reload probe only. It has no gameplay changes. The build uses the Windows .NET Framework compiler already installed on the tester's machine. The project targets .NET Framework 4.0 and x86; in-game load and reload were verified on 2026-09-24.

1. Download [Tomasak's v1.7.1.8 release](https://github.com/Tomasak/gta4_scripthookdotnet/releases/tag/release): `scripthookdotnet_v1.7.1.8.zip`. Expected SHA256: `5669E4423F93BEDFB0AE34579E922213775B46BBEE4DB6ADC953CB53E7AD9058`.
2. From the repository root, build with `./tools/build.ps1 -ScriptHookDotNetReference <path-to-extracted-ScriptHookDotNet.asi>`. This writes `src/LibertyFramework/bin/Release/LibertyFramework.net.dll`. The runtime binary is a local compiler reference and is not committed.
3. After closing GTA IV, run `./tools/archive/deploy-t001.ps1 -GameDirectory <path-containing-GTAIV.exe> -RuntimeArchivePath <path-to-release-zip>`. It verifies the game version and archive hash, checks for existing files, then installs only the three required runtime files and the probe DLL. It does not copy upstream examples or alter FusionFix.
4. Launch through Steam and follow [T-001 human test steps](../../docs/archive/tasks/T-001-runtime-spike.md).

On this machine the game directory is `D:\SteamLibrary\steamapps\common\Grand Theft Auto IV\GTAIV`. No absolute machine path is baked into the scripts.

## T-002 config probe

Run the same build command above to compile the T-002 source. After GTA IV closes, deploy with `./tools/archive/deploy-t002.ps1 -GameDirectory 'D:\SteamLibrary\steamapps\common\Grand Theft Auto IV\GTAIV'`. When the installed DLL differs, the installer backs it up; it preserves the first `LibertyFramework.net.dll.t001.bak` backup on later deployments. It installs the new DLL and copies `config/probe.json` into `scripts/LibertyFramework/config/probe.json` only if that file does not exist. It leaves ScriptHookDotNet and FusionFix untouched. Follow the exact [T-002 human test steps](../../docs/archive/tasks/T-002-config-logging.md). Do not copy the new DLL into a running game.

For the remaining live reload test, launch GTA IV once and load gameplay, then run `./tools/archive/test-t002-live.ps1 -GameDirectory 'D:\SteamLibrary\steamapps\common\Grand Theft Auto IV\GTAIV'` from the repository root. The script waits for the valid edit, malformed edit, and recovery log events, and restores the original config even if a check fails. Keep gameplay active until it finishes and report whether the game remained responsive. The script changes only `probe.json`; it does not redeploy the DLL.

## T-003 DevTools menu

Build with the same command. The DLL adds a controller menu script. The first version opened the phone when D-pad navigation was used. The revised build temporarily disables player controls while the menu is open and restores them on close, error, or script-domain unload. The owner verified that controller navigation, phone suppression, and normal control restoration worked. The guarded `deploy-t002.ps1` command backs up the installed DLL and preserves the current config. Do not copy a DLL while GTA IV is running.

## T-007 custom weapon identity probe

`./tools/archive/prepare-t007.ps1 -GameDirectory '<GTAIV folder>'` generates an ignored, local-only `staging/t007/WeaponInfo.xml` by cloning the installed vanilla pistol, M4, and pump shotgun entries as `LF_GOLD_PISTOL`, `LF_GOLD_CARBINE`, and `LF_GOLD_SHOTGUN`. The models and stats remain vanilla for identity testing. The live pistol test selected custom ID 58; the vanilla pistol disappeared from the handgun inventory. The owner verified the menu switch and total-ammo transfer. A pistol-only XML is already installed on the tester's machine; do not rerun `deploy-t007.ps1` against the existing override. The installer refuses to overwrite an existing weapon override or install while the game runs. `remove-t007.ps1` removes only a hash-matching T-007 override while the game is closed. See [T-007](../../docs/tasks/T-007-weapon-slots.md) for findings.

For the next identity expansion, `prepare-t007.ps1` now stages three cloned entries: pistol, M4 carbine, and pump shotgun. The local menu build includes the added guarded actions. While the game is closed, first upgrade the existing hash-matching weapon override with `upgrade-t007.ps1`, then deploy the rebuilt DLL with `deploy-t002.ps1`. The upgrade keeps a backup of the pistol-only XML and updates the receipt hash. The original `common/data/WeaponInfo.xml` is untouched. See [T-007](../../docs/tasks/T-007-weapon-slots.md) for the next playtest.

## Phase 1 (T-010) — build, verify, package, install, rollback

All steps except install/rollback are read-only on the game folder and safe while GTA IV runs.

| Step | Command |
|---|---|
| Build DLL | `./tools/build.ps1 -ScriptHookDotNetReference <ScriptHookDotNet.asi>` (all `src/LibertyFramework/**/*.cs`, warnings are errors) |
| Offline verification | `./tools/verify.ps1 -GameDirectory <GTAIV>` — resolver vs disassembly, native registration and ScriptHook.dll name→CE hash mapping, config/logic tests |
| Gold finish | `./tools/build-finishes.ps1 -GameDirectory <GTAIV>` — reads `weapons.img`, writes `staging/phase1/update/...` and PNG previews |
| Presets | `./tools/generate-presets.ps1` — regenerate `config/presets` from `config/gunplay.json` |
| Package | `./tools/archive/package-phase1.ps1 -GameDirectory <GTAIV> -ScriptHookDotNetReference <asi>` — runs all of the above, writes `staging/phase1/manifest.json` |
| Install (game closed) | `./tools/archive/install-phase1.ps1 -GameDirectory <GTAIV>` — checks, backs up to `scripts/LibertyFramework/backups/phase1-*`, installs, hash-verifies |
| Rollback (game closed) | `./tools/archive/rollback-phase1.ps1 -GameDirectory <GTAIV>` — restores the newest phase1 backup |

Installed files: `scripts/LibertyFramework.net.dll`, `scripts/LibertyFramework/config/{gunplay.json,presets/*.json,devtools/locations.json}`, `update/common/data/{WeaponInfo.xml,default.dat,lf_finishes.ide}`, `update/LibertyFramework/LibertyFramework.img`. `default.dat` is the installed Various Pedestrian Actions copy plus one `IDE common:/data/lf_finishes.ide` line; `WeaponInfo.xml` is the T-007 file with `LF_GOLD_PISTOL` using model `lf_gold_pistol`. The superseded `deploy-t00x`/`upgrade-t007` scripts remain for history only.