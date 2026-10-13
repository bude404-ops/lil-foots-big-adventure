# Mossveil Terrain Kit - batch 1

Storybook gouache terrain pieces for hand-building maps. Generated in the
Mossveil PNW style to match the existing world art (paint_grass/paint_dirt
palette, sage sky, warm loam). White backgrounds are flood-keyed to real
transparency - safe to drop straight into scenes.

## Pieces

| File | What it is | Recommended use |
|---|---|---|
| slope_grass_gentle.png | low grassy hill ramp, tall left -> flat right | gentle rises between platforms |
| slope_grass_steep.png | tall grassy hill ramp | bigger climbs, back-layer hills |
| slope_loam.png | bare loam ramp with worn grass cap | dirt paths, eroded banks |
| slope_stone_mossy.png | mossy stone ledges stepping down | stone staircases, cliff steps |
| cliff_loam.png | vertical loam cliff face with grass top | walls, level boundaries |
| shore_bank.png | grass bank breaking to loam + river stones | stream edges |
| water_stream.png | painterly water strip | streams, ponds (stack strips) |
| floor_forest.png | old-growth forest floor strip | alternate ground texture |

## Using them

- Suggested import: PPU 128 (a 512-wide piece = 4 world units). Drop the
  Art/MossveilKit folder in, set Texture Type = Sprite (2D and UI),
  Pixels Per Unit 128, Filter Point or Bilinear.
- Slopes read best snapped edge-to-edge (flat right side meets next piece's
  tall left side, or mirror/flip a copy to descend the other way).
- Match colliders to the visual top surface; use EdgeCollider2D or a simple
  PolygonCollider2D on the grass cap.
- These are painted art pieces, not autotiles - overlap 1-2px to hide seams.
- _style_reference.png is the palette/style board used to generate them -
  keep it around as the reference for future batches (bridges, ladders,
  background hills, cave mouths are good next candidates).

Batch 2 candidates on request: mirrored slopes, background hill strips,
wooden bridge, ladder, cave entrance, big cedar trunks.
