# T-023 — LVS CE body-part labels

Status: **NEEDS-PLAYTEST** (implemented and installed 2026-09-24, Claude). Previously blocked on identifying which vehicle extras are body parts; that now comes from the model files themselves.

## How it works

`tools/vehicles` reads every vehicle fragment (`.wft`) in the player's own `pc/models/cdimages/vehicles.img` and finds each `extra_N` bone record (name +0x00, parent bone +0x10, model-space position +0x60 in the 0xE0-byte bone records). The part is named from its parent bone and position: bonnet → hood scoop/panel, boot → trunk spoiler/boot panel, bumpers → bumper parts, high and central → roof item (rack, sign or light), high at the rear → rear spoiler/wing, low → lower trim, bones at the car origin → "Body part". 80 models / 295 extras. Offline checks: Sultan RS extra 1 = hood scoop (bonnet, 0/1.224/0.514), police extra 1 = roof light, Infernus extra 1 = trunk spoiler.

`tools/package-phase2.ps1 -LvsDirectory <LVS release>` generates `scripts/LibertyFramework/config/vehicle_extras.json` and applies `tools/vehicles/patch-lvs-labels.ps1` to a copy of `LibertyVehicleServicesCE.CS` (MIT, ekzestean): the six workshop "Extra N" texts become e.g. "Hood scoop / hood panel (extra 1)". The dealer list is unchanged. The patch refuses an unexpected LVS version, and the patched script is compiled against ScriptHookDotNet before staging. Unknown models fall back to "Extra N". LVS ownership, pricing and persistence are untouched.

This names existing geometry truthfully. New body-kit parts (new bumpers, spoilers) need new models and are future asset work.

## Human test steps

1. Drive a **Sultan RS** into an LVS workshop, open **Extras**: the row reads "Hood scoop / hood panel (extra 1)". Preview on/off and confirm the hood intake appears/disappears.
2. Repeat with a **taxi or cabby** (roof sign rows), **police cruiser** (roof light) and **Infernus** (trunk spoiler). Photograph any row whose label does not match the part that changes; report model + extra number.
3. Buy one extra, save, relaunch, return to the car: still fitted (LVS persistence unchanged). Log `LibertyVehicleServicesCE.log` has no compile/runtime errors.
