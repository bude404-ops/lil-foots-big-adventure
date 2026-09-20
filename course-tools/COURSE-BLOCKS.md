# Lil Foots — Course Block System (Region 1: PNW)

Mario-style course assembly, 100% our art and lore. Levels are RECIPES, not
hand-fought geometry. Every block is physics-proven ONCE against the actual
jump math; a broken pattern cannot enter a level because the library refuses
to hold one.

## Physics law (from the live game, ppu 100)
- runSpeed 460 px/s, jumpVelocity 900, gravity 2400, hold 0.28s @ 0.9
- Full-hold jump: range 3.66u, apex 1.85u. Tap jump: 3.45u, apex 1.69u.
- HARD LIMITS: gap <= 3.6u, rise <= 1.6u standard / 1.85u max, landing >= 1.2u wide.
- Difficulty dials stretch toward the limits; they can never cross them.

## Phase fit (Bude's 6-phase doctrine)
TEACH -> PLAY -> DEVELOP -> CHALLENGE -> FINALE -> REWARD

## Region 1 block library (names = DRAFT, BudE picks final lore names)

TEACH
- T1 "Fern Gap" — first jump over a stream gap. Dial 0: 2.2u gap, wide landing.
- T2 "Moss Steps" — two low rises (1.0u, 1.2u) teaching climb.

PLAY
- P1 "Log Hop" — fallen-cedar platform chain over a brook, 3 hops, rhythm timing.
- P2 "Cedar Stairs" — up 3 steps, token arc over the top, down the far side.
- P3 "Fern Run" — flat sprint, token line, tufts and fringe dressing.

DEVELOP
- D1 "Brook Crossing" — mixed gaps + one hop block mid-gap (must use the hop).
- D2 "Canopy Shelf" — high road / low road: shelf route carries tier-1 tokens,
  low route is safe. Risk = reward, both reachable, no blind jumps.
- D3 "Fog Bank" — visibility twist: mist bank (gameplay fog, not vista mist)
  over a known-safe pattern. Pattern first, fog second.

CHALLENGE
- C1 "Gauntlet Hollow" — gap-rise-gap chain, tighter dials, checkpoint before.
- C2 "Snag Ridge" — deadfall props + staggered platforms over a long pit.

FINALE
- F1 "Wendigo Approach" — finale runway: long sightline, token arc, big visual
  moment foreshadowing the region boss (Wendigo art lands with M3).
- F2 "Meadow Gate" — flag + portal terminus (builder places gate at x=86).

REWARD (attach to any block)
- S1 "Gully Secret" — hidden alcove under a fern ledge: heart + token cache.
- S2 "Treetop Cache" — exploration tier above the main route.

Enemy lane slots (M3 only, Enemy Doctrine — made-up PNW creatures, zero humans):
walker=Barkling, swooper=Frostwing, charger=Brambleboar, turret=Sporepuff,
web=Webwick. Names = DRAFT, BudE picks. No enemies ship before M3.

## Recipe format (level = ~10 lines)
{ "name": "1-1 Fern Gully Run", "role": "teach",
  "blocks": [ {"id":"T1","dial":0}, {"id":"P3","dial":0}, {"id":"P1","dial":0},
              {"id":"P2","dial":0}, {"id":"T2","dial":1}, {"id":"D1","dial":0},
              {"id":"P3","dial":0}, {"id":"C1","dial":0}, {"id":"F1","dial":0},
              {"id":"F2","dial":0} ],
  "secret": {"block":"P2","id":"S1"} }

## Auditor (composer runs it before Unity ever sees the map)
- every gap <= 3.6u, every rise <= 1.85u, landing >= 1.2u
- checkpoints every 1500-2500px, one before every CHALLENGE block
- >= 1 secret; token tiers: >=60% tier-0, >=4 exploration, >=2 difficult
- pacing follows TEACH->PLAY->DEVELOP->CHALLENGE->FINALE order
- course >= 30 tokens (smoke gate), finale runway clear to the gate
- FAIL = named reason in seconds. Red maps cannot be built.
