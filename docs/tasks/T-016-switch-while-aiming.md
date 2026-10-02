# T-016 — Switch weapons while aiming

Status: **NEEDS-PLAYTEST** (Codex Agent B). D-pad left/right while holding aim calls ScriptHookDotNet weapon selection for the next/previous ID in `ArsenalRegistry.CarriedWeapons`, filtering absent inventory IDs. Whether CE preserves the aim camera through `SET_CURRENT_CHAR_WEAPON` needs an in-game check.

## Human test steps

1. Carry at least two Arsenal weapons. On foot, hold L2/LT and press D-pad right, then left. The held weapon should change only among carried items while L2/LT remains held. Note any camera drop or fire interruption.
2. Repeat while aiming out of cover and while crouched. With only one carried weapon, no cycling should occur. In a vehicle, custom cycling should be inactive.
3. Remove a weapon through Arsenal, then cycle again; it must not reappear. Attach `aiming_cycle` log lines and describe any vanilla interruption.
