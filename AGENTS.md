# Working on Liberty+

Liberty+ is the showcase mod; the sibling GTAIV-Reborn repository owns Liberty
Framework. Read docs/PROJECT_STATE.md and docs/PRODUCT.md before choosing work.
Read the framework's AGENTS.md for native, lifecycle, thread and evidence rules.
Current owner requests override older STAGE1 limits and historical schedules.

## Ownership

Gameplay policy, weapon profiles/reticles, inventory, holsters, wheel, trunks,
vehicle ownership, gore, custom HUD layout, visual tuning and artwork belong here.
Native operations, SDK contracts, generic rendering/menu services, compilers,
GTA research and reusable skills belong in the framework. Do not copy its source.
Legacy preview code is privileged and version-pinned; prefer SDK-only new code.

## Operating procedure

- Edit maintained source here or in the owning framework checkout, never in a
  generated integration workspace. Preserve archive branches, raw evidence and notices.
- Use framework .agents/skills/liberty-evidence/SKILL.md for failures and
  .agents/skills/liberty-research/SKILL.md for reverse engineering/adaptation.
- Build with tools/build.ps1; focused tests with tools/test.ps1 -NoGame -Filter NAME.
  Build/run commands and staging are explained in docs/DEVELOPMENT.md.
- Only an explicit current game assignment permits installation/launch. Check
  the framework's docs/workflow/ORCHESTRATOR.md and machine-wide game lock.
- Do not weaken thresholds, treat offline passes as runtime proof, or retry an
  unchanged failure. Record both revisions and exact input hashes in test evidence.
- Commit locally after relevant validation; do not push or dispatch agents unless requested.

The current assignment is development planning and reusable skill preparation.
Read docs/workflow/NEXT_MILESTONE.md; the next feature focus is grounded/severe gore
and atmosphere tuning across day/night and weather, plus a cloud overhaul.
Unfinished feature development remains paused until a current request
authorizes it. Existing saves/preview installation must remain untouched.

Framework skills liberty-feature-delivery, liberty-presentation and
liberty-persistence support briefs/integration, visual behavior and durable state.
Their SKILL.md files live under the framework's .agents/skills directory.
