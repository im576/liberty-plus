# T-013 — Debug overlay hit information

Status: **NEEDS-PLAYTEST** (Codex Agent B). The debug overlay now shows attributed ped/vehicle damage, ped damage bone, distance, health delta and measured fire interval/RPM. A shot with no attributed entity damage becomes `world/unknown` after the configured delay. Nearby entities are sampled only while the overlay is enabled.

## Human test steps

1. Open DevTools with L3+R3 held (or F10), enter GUNPLAY, select `Debug overlay` and press Cross to turn it ON. Close DevTools.
2. At TEST RANGE, fire a gold pistol shot into a ped. The `Hit` line should say `ped`, show a bone number, plausible distance in metres and positive damage. Repeat at different distances and with carbine bursts; `fire` should show interval in ms and RPM.
3. Shoot a vehicle, then a wall. The line should change to `vehicle`, then `world/unknown` after the configured delay. Turn the overlay OFF and check that gameplay continues. Attach log and screenshots.
