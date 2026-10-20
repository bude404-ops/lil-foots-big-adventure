# Fern Hollow Kit - batch 1 (course 1-2)

Storybook gouache pieces for 1-2 Fern Hollow, the verticality course
(climbing branches, canopy route, Webwick ambush territory). Generated in
the Mossveil PNW style from the same style board (back/middle/tileset/props)
so it sits seamlessly next to the Mossveil kit. All props are white
flood-keyed to real transparency; the backdrop is full-frame by design.

## Pieces

| File | What it is | Recommended use |
|---|---|---|
| backdrop_fernhollow.png | shaded fern-hollow parallax backdrop: sword ferns, moss-draped cedars, god-rays | the course backdrop layer (full frame, no transparency) |
| fern_giant.png | giant sword fern cluster | hollow dressing, base cover, soft landmarks (no collider) |
| branch_mossy.png | long horizontal mossy cedar branch | canopy-route platforms (oneway, thin collider) |
| branch_fork_snag.png | tall forked dead cedar snag | verticality dressing, branch ladder anchors |
| floor_ferns.png | fern-dappled forest floor strip | alternate ground texture for the hollow floor |
| log_hollow.png | big hollow mossy log with dark opening | tunnel/dress piece, bump and heart hide spots |

## Using them

- Import PPU 100 to match the course spine scale (1 unit = 100px).
- branch_mossy is the canonical canopy platform: thin BoxCollider2D (h=24)
  set to one-way (trigger from below, land on top), or use the spine's
  oneway flag and let the builder place them.
- floor_ferns pairs with the smooth-terrain pipeline (PaintGrounds); it can
  also sit as a visual strip over ground colliders.
- fern_giant, branch_fork_snag, log_hollow are dressing - never colliders,
  dim slightly (0.72-0.85) when standing behind the play layer.

## Course data

`unity/Assets/LevelData/map_region1_2_spine.json` is the full 1-2 course:
26 plats (ground route + branch ladder + canopy run + secret perch),
76 tokens, 11 checkpoints, 1 secret heart (the high snag perch, x14600),
10 bumps, 2 ramps, no water (that's 1-3 Cedar Run's identity), 6 setpieces.
Validated against the game's jump physics (max step 140u, max gap 280u) -
CLEAN.

Two wiring notes for the live build session:
1. New setpiece types "fern_giant" and "snag" need registration in
   LilFootsLevelBuilder's setpiece lookup (currently only cedar/rock;
   they fall back to rock_spine until then). One line each.
2. Enemies for 1-2 are M3 (art + AI pass): Webwick ambushers in the
   vertical sections per the enemy roster law (no trail cams - the gadget
   era is retired).
