# LIL FOOTS 2.0 — THE PRODUCTION PLAN
(Bude, Sept 19 2026: "do research how to make the game properly using the mario inspired gameplay but updated graphics and to our Sasquatch lore and the lil foots, so we wont have a bunch of fixes to go over")

Research-backed. Doctrine-compliant. Built to END the fix cycle.

---

## PART 1 — WHAT THE RESEARCH SAYS
(Nintendo level-design method analyses, platformer game-feel literature, Rayman/UbiArt production method, Unity Test Framework docs)

### 1. The Mario level design method (introduce -> expand -> remix)
- Every level owns ONE main mechanic. Introduced in a SAFE SPACE (zero threat), then tested with danger, then COMBINED with a second mechanic, then a final exam - all inside one level, zero tutorial text. Mario 1-1 teaches entirely through level composition.
- Mechanics introduce solo BEFORE combining. Never two new things on one screen.
- Pacing curve: quiet -> build -> spike -> BREATHE -> finale. Calm breathers between spikes are mandatory.
- Secrets reward curiosity, never punish it.
- The finish is always telegraphed; the player always knows what "done" looks like.
- WE ALREADY HOLD THIS DOCTRINE: TEACH -> PLAY -> DEVELOP -> CHALLENGE -> FINALE -> REWARD. 2.0 enforces it per-level: every level states its one mechanic + its final exam BEFORE it gets built.

### 2. Game feel — the invisible half of "Mario-like"
- Classics of platformer feel we ALREADY SHIP (frozen core): coyote time, jump input buffering, variable jump height (hold = higher), native gravity scaling. This is why the core felt right.
- 2.0 ADDS (the research-canon juice list, all Unity-native):
  - Squash & stretch: land = squash, jump = stretch (Animator + clips)
  - Anticipation: tiny pre-jump crouch frames
  - Landing dust puffs + footstep rustle (Particle System + AudioSource)
  - Camera lookahead in run direction (CameraFollow upgrade)
  - Readability blob shadow at jump apex so mobile players judge landings
- Feel polish happens ONCE in the core, is validated by the smoke test, never re-touched per level.

### 3. Art production — the Rayman/UbiArt method (matches our locked doctrine)
Modern painterly platformers (Rayman Legends, DKC: Tropical Freeze) do NOT draw levels tile-by-tile. They:
  1. Paint full world plates (the world-skin: Bude's painting IS this)
  2. Rig WHOLE characters with skeletal animation (Unity 2D Animation: our rigging law)
  3. Compose levels from reusable parallax depth layers (Depth Doctrine: L1 static vista / L2 gameplay / L3 foreground garnish)
- One locked art bible (strong outlines, chunky geometry, clean contact shadows, cel accents) - checked at BUILD time, not review time.
- Characters derive ONLY from Bude's reference pixels (the drift lesson, confirmed twice).

### 4. Sasquatch lore, owned
- Regions = real cryptid geographies (Region 1: PNW; future yeti/yowie/skunk-ape/mapinguari), never built without Bude's go.
- The villain grid: Carl's surveillance kit (hounds ground / trail cams trees / drones sky) + region traps (telegraphed, bypassable, CHALLENGE-only on first appearance - approved doctrine).
- Big Tokens (gold footprint coins), hearts, flag + portal at every level's end (level-end rule).
- Cast: Lily / Buddy / Emma - characters, not roles (no-roles law). Mario-like means any character clears any level.

---

## PART 2 — THE ANTI-FIX-CYCLE SYSTEM (the answer to "so we wont have a bunch of fixes")

Every fix-cycle this week traces to ONE root cause: RUNTIME BEHAVIOR WAS NEVER TESTED BEFORE IT REACHED BUDE. Editor QC shots verified art, never the running game. 2.0 closes that with three gates:

### GATE 1 — GREYBOX-FIRST (locked doctrine, hardened)
A level gets ZERO art until the greybox is play-verified: geometry locked -> feel passes -> skin -> garnish. Art passes are forbidden from touching colliders, cameras, canvases, or input wiring.

### GATE 2 — UNITY TEST FRAMEWORK PLAY-MODE SMOKE TEST (new)
Every forge run executes an automated play-mode suite BEFORE the build ships:
1. Scene loads clean: zero errors, ONE EventSystem, one main camera
2. Character select: each card fires a pick, menu dismisses, timeScale restored to 1
3. Player: spawn -> run right -> jump fires (simulated input) -> lands grounded -> coyote jump over a gap
4. Character art VISIBLE above terrain (sorting assertion: player order > terrain order at player position)
5. Collect one Big Token, trigger one checkpoint, touch the gate
6. Zero exceptions during the run
Green smoke test = ship. Red = the run fails and Bude never sees a broken build. This one gate catches every bug class from this week: duplicate EventSystem, dead jump, sunken character, T-pose cards.

### GATE 3 — FROZEN CORE + ONE BUILD STREAM
- The playable core (map data + PlayerController + HoundController + physics) is FROZEN. Changes require a version gate (v2.1-core) + full smoke suite re-run.
- One playable WebGL link + APK per pass (already true). Bude plays ONE build per pass, red-marks from gameplay only (art-fatigue rule in effect).

---

## PART 3 — LIL FOOTS 2.0 GAME DESIGN

### Core loop (per level)
Arrive -> learn the level's mechanic in a safe space -> run the course (tokens, secrets, checkpoints) -> survive Carl's grid + region traps -> finale sprint -> FLAG -> portal = next level. 2-4 minutes. One memorable moment per level.

### The Mario-inspired structure, ours (World 1: Pacific Northwest, 6 levels)
- 1-1 Meadow Run — pure teach: run/jump/tokens/one hound, first secret, flag+portal
- 1-2 Fern Hollow — verticality: climbing branches, trail cams introduced (stealth-lite)
- 1-3 Stream Crossing — moving platforms + water hazards, drones introduced
- 1-4 Old Growth — darkness pockets + firefly lanterns (light mechanic), hound packs
- 1-5 The Ravine — trap doctrine showcase (bear pit, log rollers, deadfall), momentum level
- 1-6 Carl's Outpost — finale: full surveillance grid, gate gauntlet, big finale moment

### Power-ups (Sasquatch lore, original) — TWO only, never required to finish
- Huckleberry Boost (short speed burst), Pine Resin Guard (one-hit shield)

### Collectibles
- Big Tokens (easy/exploration/difficult/hidden tiers), 3 hidden heartberries per level (100% = true-ending tease)
- Every level >= 1 curiosity-rewarding secret

### Art & audio targets (per locked bible)
- One world plate per level from the Region palette (Bude's painting = 1-1 canonical L1)
- Rigged characters (breathing idle, run cycle, jump squash/stretch) from Bude's pixels
- PNW ambient bed + native footsteps/jump/pickup/portal SFX

---

## PART 4 — MILESTONES

| Milestone | Deliverable | Gate |
|---|---|---|
| M0 (now) | Playable core fixed + verified | current build + smoke suite written |
| M1 | Smoke test suite green in CI | Gate 2 live on every run |
| M2 | 1-1 Meadow Run greybox -> skin -> ship | full 3-gate pipeline on ONE level |
| M3 | World 1 complete (6 levels) | each level passes gates independently |
| M4 | Mobile feel polish + APK | play-tested by Bude per level |

## THE ONE-LINE SUMMARY
Frozen Mario-method level design, frozen feel-perfect core, Bude's lore and art, three automated quality gates — no build reaches Bude that a machine hasn't played first.

## REGIONAL COURSE NAMING DOCTRINE (Bude, Sept 20 2026: "make the names regional based for the map layouts so they all flow properly")
World format: `Region # — Name`. Every course is named after that region's own terrain, using ONLY that biome's vocabulary — course lists read like a trail map of that region; regions read like chapters.

- REGION 1 — PACIFIC NORTHWEST (Sasquatch): 1-1 Mossveil Meadow · 1-2 Fern Hollow · 1-3 Cedar Run · 1-4 Old Growth Deep (Wendigo boss course)
- REGION 2 — HIMALAYAS (Yeti): snowfield / glacier names
- REGION 3 — OUTBACK (Yowie): bush / red-desert names
- REGION 4 — EVERGLADES (Skunk Ape): swamp names
- REGION 5 — AMAZON (Mapinguari): jungle names
