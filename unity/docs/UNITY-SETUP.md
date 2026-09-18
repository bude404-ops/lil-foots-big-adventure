# Lil Foots: Big Adventure — Unity Setup

Unity port of the playtested map 001. The level is **data-identical** to the shipped engine build:
`Assets/LevelData/map001.json` was exported straight from the v0.3 canvas build, so every platform,
hound, cam, token (73, 4 tiers), checkpoint (5) and the gate rule (18) match what Bude playtested.

## Open the project

1. Unity **2022.3 LTS** (or newer 2D-capable version) → Open Project → this `unity/` folder.
2. Set up layers: Edit → Project Settings → Tags and Layers → add **Player**, **Enemy**, **Ground**
   (the level builder assigns these).
3. Recommended: Edit → Project Settings → Physics 2D → keep defaults.

## Build the map (one click)

Menu: **Tools → Lil Foots → Build Map 001**

Constructs the whole playable level from `map001.json`: 24 platforms, 3 hounds, 5 trail cams
(each on a tree), drone, 73 tokens, 5 checkpoints, the 18-token gate, Lily, camera, GameManager.
Ctrl+S to save the scene (e.g. `Assets/Scenes/Map001.unity`).

## Feel tuning (already carried over)

All values came from the shipped engine build, converted at 100px = 1 Unity unit:

| Engine (JS) | Unity (C#) | Value |
|---|---|---|
| RUNSPD 460 px/s | runSpeed | 4.6 |
| JUMPVEL -900 | jumpVelocity | 9.0 |
| GRAV 2400 | gravity (manual, FixedUpdate) | 24.0 |
| jumpHold 0.28s @ 0.9 grav | jumpHoldTime / jumpHoldFactor | 0.28 / 0.9 |
| coyote 0.12 / buffer 0.14 | coyoteTime / jumpBuffer | same |
| accel 3400 / 2100 | accelGround / accelAir | 34 / 21 |
| stomp bounce -520 | stompBounce | 5.2 |
| GROUND_Y 620 | GameManager.GroundY | 6.2 |

Playtest fixes are carried over as code law:
- **Hound ledge guard** — probes ground ahead, turns at pit edges, never chases across a gap.
- **Stomp hitbox** — falling contact from above the back line kills the hound (swept, forgiving);
  side contact is lethal to the player.
- **Gate** — bounce-back + transient notice, never a soft-lock.

## Art

`Assets/Art/Characters/` holds the approved rig-ready T-poses (Lily/Buddy/Emma).
`Assets/Art/Animation/LilyRun/` holds the 6-frame Lily run cycle.
Placeholder green slabs render the terrain until the art pass swaps in art-bible surfaces.

## Next passes (in order)

1. Art pass — art-bible surfaces, trees, parallax (5 layers), Lily run-cycle animation wired to velocity.
2. Unity skeleton/rigging pass on the T-poses (the reason they exist).
3. Snare trap, secret gully heart, Carl camp chase, flagpole + portal, objective rating screen.
4. Mobile build: touch control deck (TouchDeck.LeftHeld/RightHeld/JumpHeld hooks are ready).
