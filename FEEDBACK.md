# Lil Foots: Big Adventure — Feedback Ledger (Bude ↔ BIGagent404)

## How feedback gets tracked (agreed Sept 17, 2026)
1. **Receipt rule:** BIGagent404 replies to confirm EVERY message that reaches him. No reply within a few minutes = it never arrived → resend. (Telegram bots cannot scroll back through chat history — an undelivered message is unrecoverable.)
2. **This ledger:** every suggestion/verdict from Bude is logged here with date + status, committed to the repo. Chat is where decisions happen; this file is where they live.
3. **Numbered deliveries:** review images are sent ONE AT A TIME and numbered (Image 1: backgrounds sky/treeline, etc.) so verdicts are unambiguous.
4. **Text feedback separate from images:** if feedback is attached to a photo and the photo doesn't land, the feedback is lost with it — text it separately when possible.

## Open items
| Date | Item | Status |
|------|------|--------|
| Sept 17 | Bude (~5:01 PM ET): "we need to clean up the terrain the character runs on and make it cartoony like the characters, same with the background it needs to match and portray a 3d depth even tho its not" | DONE v0.13: whole world regenerated in the cast's flat cartoon style — new layered depth plate (pale far hills -> darker near bands), flat vector platform slabs, flat earth bodies w/ stones, flat fg undergrowth; painterly glow/shafts removed. QC zero errors. World shot DM'd |
| Sept 17 | Bude (~4:49 PM ET): "we also need to make the big tokens all gold with Big as the symbol inside so it looks better black lettering for BIG" | DONE v0.12: token regenerated as all-gold coin w/ black stamped BIG (Lily-ref style anchor), mag-cutout + rim de-magenta'd; world tokens, HUD counter icon and gate counter all restyled gold. QC zero errors. Token shot DM'd |
| Sept 17 | Bude (~4:35 PM ET): "They need to match the artstyle of the lil foots" | DONE in v0.11: all 5 enemies regenerated as flat 2D vector cartoons using Bude's own Lily ref image as the style anchor (same thick uniform outlines, flat fills, cel shading). Lineup sheet v4 DM'd — awaiting verdict |
| Sept 17 | Bude (~4:35 PM ET): "lets make the artwork of what the characters are running on match the background style of quality and art too please" | DONE v0.11: platform slabs regenerated as full painterly gouache cross-sections (grass cap w/ wildflowers, layered sediment, embedded stones, hanging roots, alpha-fade base dissolving into the earth gradient); native-aspect draw w/ grass bumps revealed above the walk line. QC'd phone3/port/land zero errors. Both queued passes (enemy cast v0.10 + platform slab v0.11) live on demo |
| Sept 17 | Bude (~4:23 PM ET): "keep browser native for now; make the background + terrain changes, then work on the characters and other art so the game looks better" | BG+terrain = shipped (v0.9/v0.9.1). ENEMY ART REDO executed same pass: all 5 enemies (Carl, Steve, hounds, trail cam, drone) regenerated in painterly storybook gouache matching the depth-plate world, mag-cutout + halo-cleaned, integrated live. Lineup sheet v3 DM'd — awaiting verdict |
| Sept 17 | Bude v0.9.1 verdict (~4:12 PM ET): "lower the plane where the player is actually running and jumping — it seems so high up in the screen, creating those giant earth blocks" | FIXED in v0.9.1: old zoom was width-only so tall phone screens pushed the walk line to ~22-27% of the screen (hence the giant earth mass below). Zoom now guarantees the walk line sits ~68% down in every orientation + pixel ratio; upper 2/3 = sky/distance, lower 1/3 = the ground you run on. Landscape framing unchanged |
| Sept 17 | Bude v0.9 review pass (~3:52 PM ET): "look over the background again — upper/lower still an issue"; "terrain the model runs and jumps on needs to make sense, not just floating"; "then redo the enemy characters to fit better" | BG+TERRAIN fixed in v0.9 (treeline veil removed — its bottom edge was the split line; ground runs now extend to screen bottom as solid earth; floating platforms grow from earth spires w/ hanging roots). Enemy art redo = NEXT PASS queued after his v0.9 verdict |
| Sept 17 | Bude's background verdict (image 1, ~3:38 PM ET): "background is split into two sections — lower half should be where the player runs, upper should read as distance, looks weird" | RECEIVED → fixed in v0.8 (one continuous depth plate: sky→hazy distance→mist→near-forest floor; treeline demoted to light mid-veil). Reworked shot sent — awaiting verdict |
| Sept 17 | Bude's earlier background feedback | was never received (pre-ledger); superseded by the 3:38 PM verdict above |
| Sept 17 | One-at-a-time image rule for changes/approvals | ACTIVE — standing law |
| Sept 17 | v0.7 painted platforms pass (grass/earth/roots slabs + foreground) | Built, pushed — awaiting Bude verdict (shot delivered after image-1 verdict per one-at-a-time rule) |
| Sept 17 | GitHub Pages demo hosting (Bude request) | Workflow added — repo is PRIVATE on free plan, Pages needs the repo PUBLIC (Bude's call) |
| Sept 17 | CAST NAME CORRECTION (Bude): the pink Lil Foot is named EMMA | FIXED same day — character select now reads LILY / BUDDY / EMMA; roster locked |

## Resolved / implemented
| Date | Item | Outcome |
|------|------|---------|
| Sept 17 | Controls feel verdict | "the controls feel good" — locked |
| Sept 17 | Facing flip, portal escape, cast select tap fix, sound+music | v0.4 shipped |
| Sept 17 | Destructible cams/drones, lives/checkpoints, detection spawns hounds | v0.5 shipped |
| Sept 17 | Real generated backgrounds (sky, treeline) replace code-drawn | v0.6 shipped |
