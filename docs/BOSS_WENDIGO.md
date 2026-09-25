# Boss Fight Design — THE WENDIGO (Region 1 finale: 1-4 Old Growth Deep)

> First boss of the game = the tutorial for every boss after. Classic platformer
> three-phase structure, all telegraphs natural (frost, sound, shadow), zero tech.

## The Arena — "The Heart-Grove"

A hollow cathedral of ancient cedars, deep in Old Growth Deep. Frozen breath-fog drifts
at floor level. At dead center: **Region 1's cold totem**, frozen in a pillar of ice.
The arena is a single wide gameplay plane (~26 units), snow floor with exposed cedar
roots, two low branch-hops at the edges. Everything painted into the world per the
world-prop law — the Wendigo itself is the only moving sprite besides the player.

The approach: the last 15 units of the course narrow into a root tunnel, and the music
drops to wind and heartbeat. You walk out into the grove and the fog thins to reveal
it standing behind the totem. Frozen beat. Then the fight.

## The Wendigo (design)

Tall, gaunt, antlered silhouette — bark-like hide frosted with ice, long arms that end
in root-claws, a faint ice-blue glow behind hollow eyes. Painted in Region 1's palette
law but colder: the same mossy greens, all desaturated, all rimmed in frost white.

**Movement grammar (fits the existing archetype system):**
- **Stalk** — walks the arena perimeter, half-hidden in treeline mist.
- **Lunge** — crossed the gap in one leap; telegraph = frost crystals sprouting on the floor along the lunge line.
- **Frostbreath** — a low cone across the floor; telegraph = its breath visibly pooling before it fires.
- **Summon Frostwings** — calls the region's swoopers into the arena.

## Phase 1 — "The Stalking Cold" (100% → 66%)

Teaches the core loop: **read the frost, dodge, punish the whiff.**

The Wendigo stalks the perimeter, mist-hid. Every few seconds: frost crystals bloom on
the floor marking a lunge line — then it LUNGES. Player hops the lunge or sidesteps;
if the player is on the line, damage. The punish: a lunge that misses lands it snagged
in the exposed cedar roots for ~1.2s — the hit window. Three hit windows to phase 2.

## Phase 2 — "The Frozen Floor" (66% → 33%)

Adds a new feel: **the floor itself changes.** Every Frostbreath leaves ice slicks
(friction drop — the same tuned low-friction as the Region 2 ice tech, so this phase
is the game teaching Region 2's feel early). It alternates Frostbreath sweeps with
lunges, and summons a pair of Frostwings to harass hops. Hit windows now open after it
rears back from a Frostbreath (recover time), or when a Frostwing is bounced into it
(stomp → ricochet → stagger). Three more windows to phase 3.

## Phase 3 — "The Desperate Winter" (33% → 0%)

Everything faster: stalk gone — it now charges the whole arena width. New telegraph:
the frozen TOTEM begins to glow faintly gold as the Wendigo weakens (the trail's light
is coming back). The kill loop: lure a full-width charge over the center; it slams
into the frozen totem and is stunned ~2s (totem cracks, doesn't break); big hit window.
Loop twice. On the final hit the Wendigo dissolves into drifting snow, the totem
RELIGHTS, the frost on the whole grove melts in one painted transition, the flag gate
drops, and the portal to Region 2 opens in the megalith.

## Failure & fairness

- Player death: checkpoint totem sits right before the root tunnel — instant re-entry.
- No cheap hits: every attack has a natural pre-telegraph of ~0.7s or more.
- Skip/no-tech rule honored: no UI meters on the Wendigo; damage shows as frost cracking off its hide — its "health bar" is how much ice it's lost.

## Sound (matches the established sting doctrine)

- Base bed: wind through cedars + sub-bass heartbeat.
- Lunge telegraph: a wood-crack. Frostbreath: a long inhale.
- Phase 3: the heartbeat doubles.
- Victory: the totem relight is the same gold chime family as the checkpoint sound — the region's theme resolving.

---

*(Fight is a draft for BudE's verdict — pacing numbers, the ricochet mechanic, and the
totem-slam finish are all proposals.)*
