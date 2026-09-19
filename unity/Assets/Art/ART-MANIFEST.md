# Lil Foots Unity Art Manifest

**LAW (Bude, Sept 18 2026):** "The art used in Unity shouldnt be any you used through code work for the test demo."
The Unity build uses ONLY real art asset files. Nothing that was code-drawn in the HTML test demo may appear in Unity — every visual the demo drew procedurally now has a generated, inked, palette-matched PNG asset here.

## Asset inventory (all real art — no code-drawn visuals)

### Characters (Bude's refs, recreated/whole-sprite per the character art laws)
- `whole_lily.png` / `whole_buddy.png` / `whole_emma.png` — in-game whole sprites
- `recreated_lily.png` / `recreated_buddy.png` / `recreated_emma.png` — white-bg recreations
- `Characters/lily-rig-ready-v1.png`, `buddy-rig-ready-v1.png`, `emma-rig-ready-v1.png` — approved T-poses for Unity skeleton/rigging
- `rig_*_body/footL/footR.png` — legacy rig-cut pieces (superseded by whole-sprite law; kept for reference)

### Enemies (roster canon: Carl + hounds + trail cams + drones + Skeptic Steve)
- `art_carl.png`, `art_hound.png` (lab v2, yellow/black/chocolate recolors via tint), `art_trailcam.png`, `art_drone.png`, `art_steve.png`

### Props
- `art_flaggate.png`, `art_flagportal.png` — level-flow flagpole + portal gate

### Collectibles / UI
- `art_token.png` — Big Token, FOOTPRINT coin face (footprint symbol law; supersedes the old BIG-lettering version)
- `art_heart.png` — cartoon heart (HUD lives AND the world extra-life pickup)
- `art_btnL.png` / `art_btnJ.png` — chunky touch buttons (right button = mirrored art_btnL)
- `art_panel.png` — wooden HUD sign panel behind the token counter

### World — Region 1: Pacific Northwest
- `art_sky_pnw.png` — overcast sky plate (the demo's pinned sky)
- `art_ridges.png` — Cascade ridges + snow-capped volcano strip (was code-drawn silhouettes in the demo)
- `art_sun.png` — the ONE sun, real art (was a code-drawn disc in the demo; never drawn procedurally in Unity)
- `art_firwall.png` — dense fir midground strip
- `art_fgl.png` / `art_fgr.png` — foreground cartoon trunks (left/right tiles)
- `art_camtree.png` — the tree the trail cams mount on
- `art_mist.png` — PNW mist bank sprite (legacy strip; superseded for banks/wash by art_mist_hq)
- `art_mist_hq.png` — HQ painterly fog bank (generated Sept 19). UNUSED since Bude ordered 'remove the clouds and mist that layer 2 adds' - kept as an asset for a future approved use
- `art_water.png` — HQ stream surface strip (Sept 19 terrain re-skin v2, mirror-tiled for seamless left/right repetition)
- `art_ground_strip.png` — HQ mossy platform top strip (Sept 19 terrain re-skin v2, mirror-tiled)
- `art_earth.png` — HQ platform body earth tile (Sept 19 terrain re-skin v2, mirror-tiled; flip-alternated in-world to break repetition)
- `art_vista_base.png` — vista depth base band: continues the approved vista's fir wall downward (palette-matched top edge) so the frame below the vista reads as deep forest; replaces the removed below-ground mist wash
- `art_bgplate.jpg`, `art_cedar.png`, `art_fg.png`, `art_leaf.png`, `art_plat.png`, `art_puff.png`, `art_spark.png` — earlier real-art set (retained; superseded plates kept for reference)

### Animation
- `Animation/LilyRun/` — run frames

## Demo-vs-Unity rule
If a new visual is ever drawn in code for the test demo (a quick mock), it ships to Unity ONLY after it becomes a real art asset through the standard pipeline: generate -> ink (thick outlines) -> palette remap -> transparent background. No exceptions.
