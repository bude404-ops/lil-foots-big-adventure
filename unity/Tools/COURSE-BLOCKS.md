# Lil Foots Course Block System — Region 1: Pacific Northwest

HOW WE STOP FIGHTING MAPS (Bude doctrine, Sept 20 2026: "we need to figure out a way so we
are not fighting these maps, especially since we need like 10 levels per region").

Mario doesn't hand-place every piece — courses are assembled from proven patterns. Ours:

1. A block is built ONCE, physics-proven once (auditor gate), then reused forever.
2. A level is a RECIPE: which blocks, what order, difficulty dial, seed, lore name.
3. The AUDITOR solves the level before Unity ever builds it. Red recipe = red in seconds,
   with a reason — never a 40-minute red forge run.
4. Region skins: same block skeletons, biome art + lore names per region.

## Physics limits (from the locked PlayerController - auditor enforces hard)
- Max jump gap: 360px (3.6u) — fail at >360, warn at >330
- Max rise: 185px (1.85u)
- runSpeed 460, jumpVelocity -900, gravity 2400, jumpHold 0.28 (map physics block)
- Ground top = canvas y 620. Plats are [x, y(top), w, h].

## Region 1 block library (PNW) — names are Bude's to bless or rename

### TEACH
- **Mossy Stretch** — flat safe run, ground token trail. The "you can move" moment.

### PLAY
- **Fern Hop** — hop-block chain over ground, rhythm timing. First real jumps.
- **Cedar Steps** — staircase rise to a plateau. Teaches verticality.
- **Stream Logs** — log crossings over water. Teaches precision + recovery.

### DEVELOP
- **Fog Bank** — mid-height platform path over a wide water gap; low risk route below.
- **Snag Run** — deadfall hops at ground level, speed section.
- **Canopy Climb** — vertical zig-zag climb, checkpoint at the top.

### CHALLENGE
- **Bramble Gauntlet** — small platforms, near-max gaps over water. The wall.
- **Ranger Tower** — tall narrow climb, hidden cache at the top (exploration reward).

### FINALE
- **Wendigo Approach** — elevated tension runway into the gate clearing.

### REWARD
- **Flag Clearing** — the flag + portal gate megalith (placed by the level builder at gate x).

### SECRETS (at least one per level)
- **Gully Secret** — sunken fern gully with a heart cache.
- **Moss Cave** — hidden token cache under an overhang.

## Rules the auditor enforces on EVERY composed level
- 6-phase order: TEACH -> PLAY -> DEVELOP -> CHALLENGE -> FINALE -> REWARD
- Every gap jumpable (<= 360px), every rise reachable (<= 185px)
- Checkpoints at most ~30u apart
- >= 62 Big Tokens (smoke gate), 4 token tiers present (easy/exploration/difficult/hidden)
- >= 1 secret, >= 1 memorable moment (finale/gate set piece)
- No blind jumps on the main route (every landing visible from takeoff)

## Usage
```
python3 unity/Tools/course_composer.py unity/Tools/recipes/r1_1_1.json
python3 unity/Tools/course_auditor.py unity/Assets/LevelData/map_r1_1.json
```
Composer emits a map JSON identical in schema to map011.json; the auditor gates it.
