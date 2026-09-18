# Lil Foots: Big Adventure — Unity Port (map 001)

Unity project for the playable build, per Bude's greenlight ("Yes lets run Unity for it and make it proper").
Everything below is data-identical to the playtested engine build (v0.3).

## Open + run (one time, ~5 min)
1. Open Unity Hub → Add → select this `unity/` folder. Use **Unity 2022.3 LTS or newer** (2D template settings are included).
2. With the project open, run the menu item: **Tools → Lil Foots → Build Map 001**.
   That constructs the entire level from `Assets/LevelData/map001.json` — 24 platforms, 3 hounds,
   5 trail cams, 73 tokens (4 tiers), 5 checkpoints, the drone, the 18-token gate, and the player.
   (If a "Lives"/"Lily" object already exists in the scene, delete MAP001 first and rebuild.)
3. Save the scene (Ctrl+S) as `Assets/Scenes/Map001.unity`.
4. Press Play. Keyboard: arrows/WASD + Space. The touch control deck hooks into `TouchDeck` (wire to on-screen buttons for the mobile build).

## What's ported (engine → Unity, converted at 100px = 1 unit)
- **Tuned feel, exact**: run 4.6 u/s, jump 9 u/s, gravity 24 u/s², jump-hold 0.28s (0.9x gravity while held), coyote 0.12s, buffer 0.14s, stomp bounce 5.2 u/s — measured from the JS build, not re-invented.
- **All four v0.3 playtest fixes**: hound ledge guard (probes ground ahead, turns at pit edges, no chasing across gaps), forgiving stomp (swept from-above check, 52px window, side contact lethal), trail cams on real trees, drone tether removed.
- **Gate soft-lock fix**: arriving under 18 tokens bounces you back with a notice — never freezes.
- Lives (3) + checkpoints, token collection, cam/drone alerts calling the hound pack.

## Layout
- `Assets/Scripts/` — PlayerController, HoundController, TrailCamController, DroneController, LivesManager, TokenCollectible, GateController, CameraFollow, CheckpointController, GameManager (+Sfx/Vfx stand-ins).
- `Assets/Editor/LilFootsLevelBuilder.cs` — the one-click scene builder (+ tiny built-in JSON parser, no packages needed).
- `Assets/LevelData/map001.json` — the level, exported from the engine build. **The map lives here**; edit data, rebuild scene.
- `Assets/Art/Characters/` — the approved rig-ready T-poses (Lily, Buddy, Emma).
- `Assets/Art/Animation/LilyRun/` — Lily's 6-frame run cycle (slice set).

## Next passes (in order)
1. Art pass: swap placeholder slabs for art-bible surfaces, wire Lily's run frames into an AnimationClip, hound/drone/cam art.
2. Skeleton rigging of the approved T-poses (Bude's Unity pipeline) → replace flat sprites.
3. Sound: port the WebAudio synth set to real clips.
4. Missing set pieces to build: Carl's camp chase, snare trap, secret gully heart, flagpole finish + portal, objective rating screen (REWARD).
5. Mobile: touch deck UI, then Android build settings (package `com.bigfoot404.lilfoots`).
