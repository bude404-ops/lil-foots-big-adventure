# Mossveil Terrain Kit (batch 1)

Storybook gouache terrain pieces for hand-built maps, matching the Mossveil world art
(paint_grass / paint_dirt palette, sage-green caps, warm loam bodies). All PNGs are
transparent-keyed, tight-cropped, no white fringe. Made for BudE's map-building pass.

## Suggested import settings (Unity)
- Texture Type: Sprite (2D and UI)
- Pixels Per Unit: **100** (matches the painted terrain art, ~100px per world unit)
- Filter Mode: Bilinear, Compression: None (keeps the gouache edges clean)

## Pieces (native px @ PPU 100)
| File | What it is | Size (px / units) |
|---|---|---|
| slope_grass_gentle.png | low grassy hill ramp, tall left -> flat right | 875x372 (~8.8 x 3.7 u) |
| slope_grass_steep.png | tall grassy hill ramp | 877x574 (~8.8 x 5.7 u) |
| slope_loam.png | exposed loam slope, thin grass cap | 893x390 (~8.9 x 3.9 u) |
| slope_stone_mossy.png | mossy stone ledge slope (2 steps) | 989x540 (~9.9 x 5.4 u) |
| cliff_loam.png | loam cliff face with grass lip | 817x825 (~8.2 x 8.3 u) |
| shore_bank.png | grass-to-loam stream bank edge | 841x273 (~8.4 x 2.7 u) |
| water_stream.png | painterly stream water strip | 1024x152 (~10.2 x 1.5 u) |
| floor_forest.png | old-growth forest floor strip | 966x151 (~9.7 x 1.5 u) |

## Pairing notes
- Slopes face "downhill to the right". For a left-falling slope, mirror the sprite in
  Unity (terrain mirroring is fine - no characters here).
- Chain slopes: steep -> gentle -> floor_forest reads as one continuous hill.
- shore_bank + water_stream: bank first, water strip tucked behind/below the bank edge.
- Colliders: add PolygonCollider2D from the sprite outline, or BoxCollider2D on the
  ramp face for the walk line (see LilFootsSmoothTerrain for the cap-height recipe).
- Per the doctrine: bake these into terrain layers, don't stack on top of the painted ground art.

Batch 2 on deck (say the word): diagonal/45-degree corner slope caps, grass-to-dirt
transition tiles, stone steps stackable set, cedar root clumps, boulder props.
