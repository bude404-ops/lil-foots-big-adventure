# LIL FOOTS v3 — UNITY CLI RESTART DOCTRINE (BudE, Oct 5 2026)

> "Restart the Lil Foots Big Adventure with the new Unity CLI as we realistically
> aren't anywhere with real progress and the lore is saved in the repo."

Supersedes the v2 build path. RESTART-V2.md doctrine (command scripts build
everything; QC binds real game state; DM only on playable green) carries forward
unchanged. What changes is the toolchain.

## Engine + toolchain

- Unity 6 LTS editor 6000.3.25f1 (installed via Unity CLI 1.0.0-beta.12)
- Fresh project at unity-v3/ from the Universal 2D template (URP + 2D packages):
  com.unity.template.universal-2d. Do NOT use the Built-in 2D template.
- Unity Pipeline package (com.unity.pipeline) installed BEFORE first open.
- Builds happen through the Unity CLI against a live Editor:
  unity open -> unity status --until-ready -> unity command eval / unity build.
  Batch-mode -executeMethod is the CI fallback only. Every batch-mode product
  gets opened in a live Editor and inspected before it counts as done.

## What restarts and what survives

RESTARTS: the unity/ (v2) project code, maps, and art pipeline. v2 stays intact
at unity/ (frozen reference) until v3 M1 goes green. Never break the working
build path while replacing it.

SURVIVES (canon, already at repo root / docs/ - untouched):
- docs/ (LORE.md, CHARACTERS.md, bosses, villains, regions 2-5, music, world map)
- DESIGN-DOC.md, LEVEL-DESIGN-PHILOSOPHY.md, ART-STYLE-GUIDE.md, ENEMY-ROSTER.md
- whole_lily.png / whole_buddy.png / whole_emma.png (BudE's original approved art,
  arms-down; T-pose renders are pose reference only, never in-game assets)
- bude-ref-* reference images + lil-foots-refs audio
- Physics envelope: rise <= 1.85u, gap <= 2.8u bridged
- splash_sting_big.wav + splash_sting_bude.wav
- Forge CI pattern: build -> smoke -> publish -> playtest gate -> DM only on green

## v3 milestones (unchanged shape from v2 doctrine)

1. M1 - Clean floor on Unity 6: fresh unity-v3 project, character (his art, rig
   bound at natural stance, rest-0 curves), run/jump/touch controls, camera
   centered (lookAhead 0.15), flat meadow, flag. Nothing else. Green gate + DM.
2. M2 - 1-1 course: audited Mario-inspired course from LevelData, tokens,
   checkpoints.
3. M3 - Life: idle/walk/jump polish, music, the two splash stings.

Each milestone: built via live-Editor eval commands, QC against real render,
WebGL build via unity build, publish to lil-foots-v2-live, agent playtests, ONE DM.

## Separation

Lil Foots and Avalon are BOTH BudE's games and stay fully separate: separate
repos (bude404-ops/lil-foots-big-adventure vs bude404-ops/avalon-the-waking-gates),
separate projects, separate domains (play.bigfoot404.biz = Lil Foots only).
Shared resource = the bude404-ops GitHub account (Actions minutes + Unity
license): before any forge dispatch, check Actions quota and that no Avalon run
is in flight.
