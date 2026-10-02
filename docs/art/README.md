# Art-request queue

The repository is the source of truth for every image request and every generated asset. When the project needs
generated art, an agent **does not stop for manual prompting**: it files a request here, keeps working, and picks up
the result when it lands. A separate image-capable agent or app does the generating ([GENERATOR.md](GENERATOR.md)).

Tool: `python tools/art/artq.py <command>` (Pillow required). `artq.py --help` lists every command; `artq.py selftest`
runs the whole lifecycle in a temporary repository; `artq.py validate` runs in `tools/cloud/test-all.sh`.

## Where things live

| Path | What |
|---|---|
| `docs/art/requests/ART-NNN.json` | One request: prompt, specification, status, and the complete history (every prompt revision, generation, review decision and output file with its SHA-256) |
| `docs/art/style.json` | Shared Liberty Vanilla+ style constraints and prohibited elements, copied into each new request |
| `art/generated/ART-NNN/rN/` | What the image agent produced for revision N (it writes only here) |
| `art/approved/ART-NNN/` | The approved image (`rN_<file>`), and `prepped/` with the texture-ready output |
| `art/rejected/ART-NNN/rN/` | Rejected generations, kept for provenance |

## Request fields

| Field | Meaning |
|---|---|
| `id` | `ART-001`, `ART-002`, ... (next free number, never reused) |
| `title` | Short human name |
| `status` | `requested` → `generated` → `approved` → `prepped` → `integrated`; or `rejected` (a revised prompt returns it to `requested` at revision + 1) |
| `priority` | `P0` (Slice A), `P1` (Slices B/C), `P2` (later) |
| `kind` | `texture` (tiling surface), `decal`, `overlay` (screen effect), `ui` (icons, HUD art), `concept` (reference only, never shipped) |
| `prompt` | The exact image prompt for the current revision (older prompts stay in `history`) |
| `intendedUse` | Where and how the asset is used in game |
| `dimensions`, `aspectRatio` | Final size after prep; textures and decals need power-of-two sides, ≤ 2048 px unless `oversizeReason` says why. The image agent may generate larger at the same aspect ratio |
| `transparency` | `none`, `alpha` (smooth alpha channel) or `cutout` (hard 0/255 edges) |
| `styleConstraints`, `prohibited` | GTA IV / Liberty Vanilla+ direction and what must not appear (defaults from `style.json`, editable per request) |
| `outputPath`, `outputName` | `art/generated/ART-NNN/` and the file name the consumer expects |
| `consumer` | Optional repository path the prepped PNG is copied to (for example a `content/` asset's texture) |
| `revision`, `history` | Current prompt revision; the append-only provenance log |

## Lifecycle (the agent side)

1. **Request:** `artq.py new --title ... --prompt ... --use ... --size 1024x1024 [--transparency alpha] [--priority P0]
   [--kind ui] [--consumer content/...png]`, then refine the JSON (prompt detail, constraints) if needed. Commit it.
2. **Generated:** the image agent writes files to `art/generated/ART-NNN/rN/` and runs `artq.py register` (or appends the
   same history entry itself).
3. **Review:** `artq.py check ART-NNN` (size, aspect, alpha), then a visual review against the request and
   [STAGE1.md](../design/STAGE1.md). Hero/identity assets also need the owner's sign-off, noted in `--notes`.
   - `artq.py approve ART-NNN <file> --notes "..."`, or
   - `artq.py reject ART-NNN --notes "why" [--revise-prompt "new prompt"]` (the revision moves to `art/rejected/`).
4. **Prep:** `artq.py prep ART-NNN` converts to the final size and channels (`prepped/`) and copies to `consumer`.
   Mipmaps and DXT compression are done by the content compiler / texture pipeline, never by hand.
5. **Integrate:** build and test in game as usual; then `artq.py mark ART-NNN integrated --notes "<scenario or check>"`.

Licensing: generated images are recorded with the generator and model in their history. They count as original project
art: the owner confirmed on 2026-09-30 that the provider's terms allow this use (STAGE1.md section 3). No request may ask for
real brands, logos, game art or a living artist's style.
