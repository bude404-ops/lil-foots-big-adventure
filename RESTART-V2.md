# LIL FOOTS v2 — RESTART DOCTRINE (BudE, Sept 20 2026, ~2:18 AM ET)

> "Yeah no changes are being made its the same issues over and over scrap the project
> and restart and use unity with command scripts to build the game"

The v1 project is SCRAPPED. This document is law for everything that follows.

## What "restart" means

- Fresh Unity project (unity-v2/) built from a clean floor. Nothing from v1
  carries over except the canon below.
- Unity is the engine and Unity does the work. Command scripts (editor
  automation, -executeMethod) build EVERYTHING: project config, scene, terrain,
  characters, props, UI, QC shots, and the build itself. No hand-made assets, no
  external editors, no mockups-as-product (UNITY-SOURCE-OF-TRUTH.md still applies).
- QC binds real game state. Any check that renders must bind the exact art and
  scenes the player sees — the v1 T-pose bug passed QC because QC bound measuring
  files while the game bound real art. That class of bug is disqualifying in v2.

## Canon that survives the scrap (BudE's property)

- whole_lily.png, whole_buddy.png, whole_emma.png — his ORIGINAL approved art
  (arms-down). A T-pose render is never an in-game asset, only pose reference.
- Region 1 PNW world art + Bigfoot lore (LEVEL-DESIGN-PHILOSOPHY.md, the 6-phase
  TEACH->PLAY->DEVELOP->CHALLENGE->FINALE->REWARD course doctrine, audited physics
  envelope: rise <=1.85u, gap <=2.8u bridged).
- Audio: splash_sting_big.wav (whoop) + splash_sting_bude.wav (pig attack-cut).
- The forge CI pipeline (build -> smoke -> publish -> auto-DM) and the
  auto-DM-only-on-playable rule (NO art fatigue: no QC shots, no progress pings).

## v2 milestones (one playable at a time, DM only on green)

1. M1 — Clean floor: fresh project, command scripts generate his character
   (his art, rig bound at natural stance, rest-0 curves), run/jump/touch controls,
   camera centered (lookAhead 0.15), flat meadow, flag. Nothing else.
2. M2 — 1-1 course: the audited Mario-inspired course from LevelData, tokens,
   checkpoints, hounds introduced solo.
3. M3 — Life: idle/walk/jump polish, music, the two splash stings.

Each milestone: regenerate from scratch via scripts, QC against real render, build
WebGL+APK, auto-DM BudE. No milestone ships with a red gate.

## Build wiring note (for the next session)

unity-v2/ carries the v2 project; the forge workflow's Unity paths still point at
unity/. When dispatching the first v2 build, either repoint the workflow's project
dir to unity-v2/ or flip the folder names after M1 goes green. v1 stays intact at
unity/ until M1 lands — never break the working build path while replacing it.

## v1 final state (reference, frozen)

Commit 8d4f42d, run 35492951300 (Sept 20 ~2:20 AM ET, green): T-pose dead at root
(art + rest-0 curves + per-char stances), camera left, audited course, live at the
Pages URL. Kept as reference only — the scrap stands regardless.
