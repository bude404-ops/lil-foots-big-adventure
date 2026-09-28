#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Linq;

namespace LilFoots {
/// <summary>
/// Tools > Lil Foots > Build Map 001 — constructs the entire playable map from
/// Assets/LevelData/map001.json (exported from the shipped JS engine build), so the Unity
/// level is data-identical to the playtested one. One click = full scene.
/// </summary>
public static class LilFootsLevelBuilder {
    // MULTI-MAP LAW (Lil Foots 2.0): the pipeline is level-agnostic - MAP_DATA picks the
    // level json (default map001), MAP_SCENE names the scene to build (default Map001).
    static string DataPath {
        get {
            // MAP_DATA accepts a bare filename (map011.json) or a full path (Assets/LevelData/map011.json).
            var v = System.Environment.GetEnvironmentVariable("MAP_DATA");
            if (string.IsNullOrEmpty(v)) v = "map_region1_spine.json";
            if (!v.Contains("/")) v = "Assets/LevelData/" + v;
            return v;
        }
    }

    [MenuItem("Tools/Lil Foots/Build Map 001")]
    public static void Build() {
        if (!File.Exists(DataPath)) { Debug.LogError("map data not found at " + DataPath); return; }
        var json = File.ReadAllText(DataPath);
        // simple manual parse (no external deps)
        var data = MiniJson.Deserialize(json) as System.Collections.Generic.Dictionary<string, object>;

        float GY = 6.2f; // px/100
        var root = new GameObject("MAP001");
        var ground = LayerMask.NameToLayer("Ground");

        // ---- ground platforms ----
        var plats = (System.Collections.Generic.List<object>)data["plats"];
        var smallSegs = new System.Collections.Generic.List<float[]>();
        // [BUD-E Sept 27: "issues with the hit box" + "hidden or invisible boxes"] same-height
        // grounds that OVERLAP leave internal vertical seams the player capsule catches on
        // mid-run, and micro-gaps (<0.6u) between same-height segments read as invisible
        // snags/pits. Weld same-height overlapping/near-touching grounds into ONE collider:
        // union shape identical to the audited geometry - only the seams disappear.
        var groundSegs = new System.Collections.Generic.List<System.Collections.Generic.List<float[]>>();
        foreach (var po in plats.Cast<System.Collections.Generic.List<object>>()) {
            float x = F(po[0]), y = F(po[1]), w = F(po[2]), h = F(po[3]);
            if (!(y >= 600f || w >= 400f) || h < 200f) { smallSegs.Add(new float[]{x,y,w,h}); continue; }
            System.Collections.Generic.List<float[]> row = null;
            foreach (var rw in groundSegs) {
                if (Mathf.Abs(rw[0][1] - y) < 0.5f && (x - w / 2f) / 100f <= rw.Max(s => s[4]) + 0.6f) { row = rw; break; } // [FIX Sept 27] row MAX right edge, not last-added - same-height segs touching an EARLIER member were missed (0.2u seam gaps)
            }
            if (row == null) { row = new System.Collections.Generic.List<float[]>(); groundSegs.Add(row); }
            row.Add(new float[]{x, y, w, h, (x + w / 2f) / 100f});
        }
        foreach (var rw in groundSegs) {
            if (rw.Count == 1) continue;
            float l = rw.Min(s => (s[0] - s[2] / 2f) / 100f), r = rw.Max(s => (s[0] + s[2] / 2f) / 100f);
            float yTop = rw[0][1], depth = rw.Max(s => s[3]);
            var weld = new GameObject("PlatWeld_" + (int)(l * 100f));
            weld.transform.SetParent(root.transform);
            weld.transform.position = new Vector3((l + r) / 2f, 2f * GY - yTop / 100f - (depth / 100f) / 2f, 0);
            var wbc = weld.AddComponent<BoxCollider2D>();
            wbc.size = new Vector2(r - l, depth / 100f);
            weld.layer = ground;
        }
        var weldedX = new System.Collections.Generic.HashSet<float>(
            groundSegs.Where(rw => rw.Count > 1).SelectMany(rw => rw.Select(s => s[0])));
        foreach (var po in plats.Cast<System.Collections.Generic.List<object>>()) {
            float x = F(po[0]), y = F(po[1]), w = F(po[2]), h = F(po[3]);
            if (weldedX.Contains(x)) continue;
            var go = new GameObject("Plat_" + x);
            go.transform.SetParent(root.transform);
            // CANVAS-Y FLIP (found Sept 20, root cause of Bude's 'random floating objects' +
            // 'no falling points' on the live build): map JSON is authored in the JS engine's
            // canvas space where y grows DOWN from the top of an 880px canvas (GROUND_Y = 620,
            // JUMPVEL = -900). Unity is y-up. unityY = 2*GY - y/100 flips it back, so steps
            // RISE above ground, tokens hover over the grass, cams/drone fly above it, and the
            // secret heart sits on its high route - exactly the playtested JS layout.
            go.transform.position = new Vector3(x/100f, 2f * GY - y/100f - (h/100f)/2f, 0);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = new Vector2(w/100f, h/100f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SlabSprite(w/100f, h/100f); // placeholder slab (art pass replaces w/ art-bible surfaces)
            go.layer = ground;
        }

        // ---- lives/checkpoints ----
        var lm = new GameObject("Lives").AddComponent<LivesManager>();
        var cps = (System.Collections.Generic.List<object>)data["checkpoints"];
        var cpList = cps.Cast<object>().Select(c => F(c)).ToList();
        var cpTransforms = new Transform[cpList.Count];
        for (int i = 0; i < cpList.Count; i++) {
            var cp = new GameObject("Checkpoint_" + i);
            cp.transform.SetParent(root.transform);
            cp.transform.position = new Vector3(cpList[i]/100f, GY + 0.85f, 0); // SURFACE = GY (canvas-y flip: map y=620 -> surface 6.2). Totem art is center-pivoted 1.7u tall, so its center sits surface+0.85 to STAND on the grass (was half-sunk at GY). Trigger still overlaps the standing player.
            var col = cp.AddComponent<BoxCollider2D>(); col.isTrigger = true;
            cp.AddComponent<CheckpointController>().index = i;
            cpTransforms[i] = cp.transform;
        }
        lm.checkpoints = cpTransforms;

        // ---- SLOPED TERRAIN (BudE Sept 28: wants hills the player RUNS on, not just flat
        // planes with platforms as the only height): map data "ramps" = [[x1,y1,x2,y2],...]
        // in the SAME canvas space as plats (y grows down from the top). Each ramp becomes a
        // thin rotated BoxCollider2D whose TOP edge lies exactly on the line - the capsule +
        // contact-normal ground check (normal.y > 0.5) runs slopes natively, so no engine
        // change is needed. The art is a painted wedge (grass cap + dirt cross-section from
        // the same storybook paint_grass/paint_dirt the grounds use) so a hill READS as
        // terrain rising out of the meadow, not a floating plank. RampEnd marker children
        // record the two world endpoints for the smoke BFS walk-links. ----
        if (data.ContainsKey("ramps") && data["ramps"] is System.Collections.IEnumerable rampList) {
            int ri = 0;
            foreach (var ro in rampList) {
                var r = (System.Collections.Generic.List<object>)ro;
                float x1 = F(r[0]), y1 = F(r[1]), x2 = F(r[2]), y2 = F(r[3]);
                float ux1 = x1 / 100f, uy1 = 2f * GY - y1 / 100f, ux2 = x2 / 100f, uy2 = 2f * GY - y2 / 100f;
                float dx = ux2 - ux1, dy = uy2 - uy1;
                float len = Mathf.Sqrt(dx * dx + dy * dy);
                if (len < 0.5f) continue;
                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                float ext = 0.25f;                       // weld 0.25u INTO the neighbors so the flat/slope seam can never snag the capsule
                float L = len + 2f * ext;
                float thick = 0.28f, face = 2.2f;        // face depth: art reaches down INTO the ground body below
                float nx = -Mathf.Sin(ang * Mathf.Deg2Rad), ny = Mathf.Cos(ang * Mathf.Deg2Rad); // surface normal (up)
                float mx = (ux1 + ux2) / 2f, my = (uy1 + uy2) / 2f;
                var ramp = new GameObject("Ramp_" + (ri++));
                ramp.transform.SetParent(root.transform);
                // sprite center sits face/2 BELOW the surface line along the normal; the collider is a thin
                // slab whose top edge is ON the line (local offset accounts for the sprite center offset)
                ramp.transform.position = new Vector3(mx - nx * face / 2f, my - ny * face / 2f, 0);
                ramp.transform.rotation = Quaternion.Euler(0f, 0f, ang);
                var bc = ramp.AddComponent<BoxCollider2D>();
                bc.size = new Vector2(L, thick);
                bc.offset = new Vector2(0f, face / 2f - thick / 2f);
                var sr = ramp.AddComponent<SpriteRenderer>();
                sr.sprite = SlopeSprite(L, face);
                sr.sortingOrder = 2;                     // above ground art, below hops/fringe
                ramp.layer = ground;
                var e0 = new GameObject("RampEnd0"); e0.transform.SetParent(ramp.transform, false);
                e0.transform.position = new Vector3(ux1, uy1, 0);
                var e1 = new GameObject("RampEnd1"); e1.transform.SetParent(ramp.transform, false);
                e1.transform.position = new Vector3(ux2, uy2, 0);
            }
            Debug.Log("[LevelBuilder] built " + ri + " ramps");
        }

        // ---- tokens (73, 4 tiers) ----
        var tokens = (System.Collections.Generic.List<object>)data["tokens"];
        foreach (var to in tokens.Cast<System.Collections.Generic.Dictionary<string, object>>()) {
            var t = new GameObject("Token_" + F(to["x"]));
            t.transform.SetParent(root.transform);
            t.transform.position = new Vector3(F(to["x"])/100f, 2f * GY - F(to["y"])/100f, 0); // canvas-y flip
            var cc = t.AddComponent<CircleCollider2D>(); cc.isTrigger = true; cc.radius = 0.34f;
            t.AddComponent<TokenCollectible>().tier = (int)F(to["tier"]);
        }

        // ---- BUMP TILES (BudE Sept 27 PM: "can we make certain tiles hittable if you [hit]
        // below them like in mario? For where hidden stuff can be?") solid little blocks you
        // jump into from below; contents burst out. hidden=true blocks are INVISIBLE until
        // first bumped (classic secret blocks). ----
        if (data.ContainsKey("bumps")) {
            var bumps = (System.Collections.Generic.List<object>)data["bumps"];
            int bi = 0;
            foreach (var bo in bumps.Cast<System.Collections.Generic.Dictionary<string, object>>()) {
                float bx = F(bo["x"]), by = F(bo["y"]);
                var b = new GameObject("BumpBlock_" + bi++);
                b.transform.SetParent(root.transform);
                b.transform.position = new Vector3(bx / 100f, 2f * GY - by / 100f, 0);
                var bc = b.AddComponent<BoxCollider2D>();
                bc.size = new Vector2(0.9f, 0.9f);
                var btile = b.AddComponent<LilFoots.BumpTile>();
                btile.content = bo.ContainsKey("content") ? bo["content"].ToString() : "token";
                btile.hidden = bo.ContainsKey("hidden") && System.Convert.ToBoolean(bo["hidden"]);
            }
            Debug.Log("[LevelBuilder] bump tiles placed: " + bi + " (contents: mario-style underside hits)");
        }

        // ---- GULLY WATER [Sept 28 - BudE: platforms cross "rivers"]: {x,y,w} canvas planes of
        // the locked stream art tiling across a carved gully bottom. Visual (no kill plane):
        // falling in means running the gully floor to the shore ramp - never a softlock. ----
        if (data.ContainsKey("water")) {
            var waters = (System.Collections.Generic.List<object>)data["water"];
            var stream = Art("art_stream.png");
            int wi = 0;
            if (stream != null) {
                foreach (var wo in waters.Cast<System.Collections.Generic.Dictionary<string, object>>()) {
                    float wx = F(wo["x"]) / 100f, wy = 2f * GY - F(wo["y"]) / 100f, ww = F(wo["w"]) / 100f;
                    float wf = 1.6f / stream.bounds.size.y;   // stream surface strip ~1.6u tall
                    float tileW = stream.bounds.size.x * wf;
                    for (float tx = wx - ww / 2f; tx < wx + ww / 2f - 0.05f; tx += tileW) {
                        var tw = new GameObject("Water_" + (wi++));
                        tw.transform.SetParent(root.transform);
                        tw.transform.position = new Vector3(tx + tileW / 2f, wy - 0.28f, 0);
                        tw.transform.localScale = new Vector3(wf, wf, 1f);
                        var tsr = tw.AddComponent<SpriteRenderer>();
                        tsr.sprite = stream; tsr.sortingOrder = -3;   // behind hops, above gully dirt
                    }
                }
            }
            Debug.Log("[LevelBuilder] water planes placed: " + wi);
        }

        // ---- DEPTH SETPIECES [Sept 28 kit]: {type,x,base,dim} cedar giants / rock spines as
        // bare-object kit sprites standing BEHIND the play layer, dimmed to sit into their depth
        // plane - the diorama's landmarks (they are never colliders). ----
        if (data.ContainsKey("setpieces")) {
            var sps = (System.Collections.Generic.List<object>)data["setpieces"];
            int si = 0;
            foreach (var so in sps.Cast<System.Collections.Generic.Dictionary<string, object>>()) {
                string type = so.ContainsKey("type") ? so["type"].ToString() : "cedar";
                var sprite = LilFootsProcTiles.KitSprite(type == "cedar" ? "t_cedar_giant.png" : "t_rock_spine.png");
                if (sprite == null) continue;
                float spx = F(so["x"]) / 100f, baseY = 2f * GY - F(so["base"]) / 100f;
                float spH = type == "cedar" ? 6.5f : 4.0f;   // sizing law: cedar 6.5u, rock spine 4u
                var go = new GameObject("Setpiece_" + type + "_" + (si++));
                go.transform.SetParent(root.transform);
                go.transform.position = new Vector3(spx, baseY + spH / 2f, 0);
                float f = spH / sprite.bounds.size.y;
                go.transform.localScale = new Vector3(f, f, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite; sr.sortingOrder = -58;   // behind play layer (-2), above far bands (-70)
                float dim = so.ContainsKey("dim") ? (float)System.Convert.ToDouble(so["dim"]) : 0.72f;
                sr.color = new Color(dim, dim + 0.06f, dim + 0.03f, 1f);   // dimmed into its depth plane
            }
            Debug.Log("[LevelBuilder] setpieces placed: " + si);
        }

        // ---- BUILD IDENTITY (BudE Sept 27 PM: 'only sends the fixed updated versions'):
        // every shipped scene carries proof of what it is. The smoke gate asserts this
        // matches the MAP_DATA/BUILD_SHA the dispatcher asked for — a stale or wrong map
        // can never reach the live link silently again.
        {
            var idGo = new GameObject("MapIdentity");
            var id = idGo.AddComponent<LilFoots.MapIdentity>();
            id.dataFile = System.IO.Path.GetFileName(DataPath);
            var sha = System.Environment.GetEnvironmentVariable("BUILD_SHA");
            var stampv = System.Environment.GetEnvironmentVariable("BUILD_STAMP");
            id.buildSha = string.IsNullOrEmpty(sha) ? "local" : sha;
            id.buildStamp = string.IsNullOrEmpty(stampv) ? "local" : stampv;
            if (data.ContainsKey("plats") && data["plats"] is System.Collections.IEnumerable pe) { foreach (var _ in pe) id.plats++; }
            if (data.ContainsKey("tokens") && data["tokens"] is System.Collections.IEnumerable te) { foreach (var _ in te) id.tokens++; }
            if (data.ContainsKey("bumps") && data["bumps"] is System.Collections.IEnumerable be) { foreach (var _ in be) id.bumps++; }
            Debug.Log("[LevelBuilder] identity: " + id.Describe() + " plats=" + id.plats + " tokens=" + id.tokens + " bumps=" + id.bumps);
        }

        // ---- gate ----
        var gate = new GameObject("Gate");
        gate.transform.SetParent(root.transform);
        // TERMINUS LAW (BudE: flag + portal at the course END, not mid-course): the old
        // hardcoded x=86 put the gate inside long courses (map_r1_1 is 225u wide); place
        // it from the map's own width instead, a few units before the final edge.
        var metaW = (System.Collections.Generic.Dictionary<string, object>)data["meta"];
        float gateX = (float)System.Convert.ToDouble(metaW["width"]) / 100f - 4f;
        if (metaW.ContainsKey("gateX")) gateX = (float)System.Convert.ToDouble(metaW["gateX"]) / 100f;   // [LONG MAP] trigger at the PAINTED gate
        // [STORY MAP Sept 21] courses can END on raised ground (summit runway at 11.8u) - the
        // gate trigger + props must sit on the ACTUAL surface under gateX, not hardcoded GY.
        float gateSurf = GY;
        foreach (var po in ((System.Collections.Generic.List<object>)data["plats"]).Cast<System.Collections.Generic.List<object>>()) {
            float px0 = F(po[0]), pw0 = F(po[2]), ptop = F(po[1]), ph0 = F(po[3]);
            float l0 = (px0 - pw0 / 2f) / 100f, r0 = (px0 + pw0 / 2f) / 100f;
            if (gateX >= l0 && gateX <= r0 && (ptop >= 600f || ph0 >= 400f)) {
                float s0 = 2f * GY - ptop / 100f; if (s0 > gateSurf) gateSurf = s0;
            }
        }
        gate.transform.position = new Vector3(gateX, gateSurf, 0);
        var gc = gate.AddComponent<BoxCollider2D>(); gc.isTrigger = true; gc.size = new Vector2(0.8f, 3f);
        gate.AddComponent<GateController>();

        // ---- FINISH PROPS (terminus law): flag + portal exist even in greybox as placeholder
        // geometry (the course must visibly terminate); ArtPass re-skins them with the HQ art at L(2).
        var flag = new GameObject("FlagGateArt"); flag.transform.SetParent(root.transform);
        flag.transform.position = new Vector3(gate.transform.position.x, gateSurf + 1.2f, 0);
        var fsr = flag.AddComponent<SpriteRenderer>(); fsr.sprite = SlabSprite(1.1f, 2.2f); fsr.color = new Color(0.20f, 0.45f, 0.25f); fsr.sortingOrder = 4;
        var portal = new GameObject("PortalArt"); portal.transform.SetParent(root.transform);
        portal.transform.position = new Vector3(gate.transform.position.x + 2.8f, gateSurf + 1.6f, 0); // [SCALE LAW Sept 21] past the flag
        var psr = portal.AddComponent<SpriteRenderer>(); psr.sprite = SlabSprite(3.2f, 3.2f); psr.color = new Color(0.28f, 0.50f, 0.42f); psr.sortingOrder = 3;

        // ---- player ----
        var player = new GameObject("Lily");
        player.transform.SetParent(root.transform);
        // SPAWN AT THE CAMERA CLAMP LINE (BudE, Sept 20: 'the beginning camera isnt locked onto
        // the character'): the camera clamps at x=viewHalfW (~6.67) but the old spawn x=1.1 left
        // the character 5.5u left of center on frame 1. Spawning AT the clamp line minus the
        // lookAhead bias puts the character dead-center on frame 1; walking LEFT slides the
        // character toward the screen edge (classic Mario level-start), walking RIGHT pans.
        float viewHalfW0 = 3.75f * (16f / 9f);
        player.transform.position = new Vector3(viewHalfW0 - 0.15f, GY + 0.1f, 0);
        var pc = player.AddComponent<PlayerController>();
        var pcol = player.AddComponent<CapsuleCollider2D>(); pcol.size = new Vector2(0.44f, 0.7f); pcol.offset = new Vector2(0, 0.35f);
        var prb = player.AddComponent<Rigidbody2D>();
        prb.freezeRotation = true; prb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        prb.gravityScale = 2.446f; // native Physics2D gravity (GRAV 2400 @ PPU 100)
        pc.rb = prb;
        var srp = player.AddComponent<SpriteRenderer>();
        // wire the approved rig-ready/T-pose or run-cycle sprite in the art pass
        player.layer = LayerMask.NameToLayer("Player");

        // ---- camera ----
        var cam = new GameObject("MainCamera");
        cam.transform.SetParent(root.transform);
        cam.transform.position = new Vector3(6.7f, 7.0f, -10f); // DEPTH DOCTRINE FRAMING (Bude, Sept 19 'layer 2 is too high on screen'): vista owns the top ~60% (treeline mid-frame), gameplay strip in the lower ~40%
        var camc = cam.AddComponent<Camera>(); camc.orthographic = true; camc.orthographicSize = 3.75f;
        camc.backgroundColor = new Color(0.10f, 0.20f, 0.14f);
        var cf = cam.AddComponent<CameraFollow>(); cf.target = player.transform;
        // CAMERA BOUNDS FROM MAP DATA (BudE, Sept 20: 'why is it the same shitty map design' —
        // CameraFollow's defaults still carried the old M1 bounds (maxX 93) while the composed
        // course runs 225u: the player walked OFF SCREEN at 40% and never saw the rest of the
        // level. Bounds now derive from the map width; the smoke gate enforces them.
        float viewHalfW = camc.orthographicSize * (16f / 9f);
        var metaD = data["meta"] as System.Collections.Generic.Dictionary<string, object>;
        cf.minX = viewHalfW;
        if (metaD != null && metaD.ContainsKey("width")) {
            float lvlW = F(metaD["width"]) / 100f;
            cf.maxX = Mathf.Max(cf.minX + 1f, lvlW - viewHalfW);
        }
        cam.tag = "MainCamera";
        var gm = new GameObject("GameManager").AddComponent<GameManager>();

        // ---- AUDIO (BudE, Sept 20: "add sound effects and music") ----
        // Unity-native: pooled AudioSources on the manager; clips wired from Assets/Audio.
        var audioGo = new GameObject("AudioManager");
        var am = audioGo.AddComponent<AudioManager>();
        am.musicForestLoop = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/music_forest_loop.wav");
        am.sfxJump         = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/sfx_jump.wav");
        am.sfxToken        = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/sfx_token.wav");
        am.sfxCheckpoint   = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/sfx_checkpoint.wav");
        am.sfxDeath        = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/sfx_death.wav");
        am.sfxLevelComplete= AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/sfx_levelcomplete.wav");
        am.sfxHeart        = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/sfx_heart.wav");
        Debug.Log("[LilFoots] AudioManager wired: music=" + (am.musicForestLoop != null) +
                  " sfx(jump/token/cp/die/win/heart)=" + (am.sfxJump != null) + (am.sfxToken != null) +
                  (am.sfxCheckpoint != null) + (am.sfxDeath != null) + (am.sfxLevelComplete != null) + (am.sfxHeart != null));

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[LilFoots] Map built: " + plats.Count + " plats, " + tokens.Count + " tokens, " + cpList.Count + " checkpoints. Ctrl+S to save the scene.");
    }

    static float F(object o) { return System.Convert.ToSingle(o); }
    /// <summary>[GULLY WATER Sept 28] loads a plain art sprite (fallback null) for LevelBuilder-side
    /// prop placement (the locked stream water).</summary>
    static Sprite Art(string file) {
        string path = "Assets/Art/" + file;
        if (!System.IO.File.Exists(path)) return null;
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null) return null;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100f;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>Placeholder white slab sprite (visible until the art pass swaps in art-bible surfaces).</summary>
    /// <summary>Painted slope wedge: grass cap band on top + dirt cross-section below,
    /// cropped from the storybook paint_grass / paint_dirt tile templates so hills match
    /// the flat ground art exactly (same files, same palette).</summary>
    static Sprite SlopeSprite(float lenU, float faceU) {
        int W = Mathf.Clamp((int)(lenU * 100f), 32, 4096), H = Mathf.Clamp((int)(faceU * 100f), 32, 1024);
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        Texture2D grass = LoadStoryTex("Assets/Art/paint_grass.png");
        Texture2D dirt = LoadStoryTex("Assets/Art/paint_dirt.png");
        int cap = Mathf.Min((int)(H * 0.30f), grass != null ? grass.height / 4 : 64);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) {
            Color c;
            if (y >= H - cap && grass != null) c = grass.GetPixel(x % grass.width, (grass.height - cap + (y - (H - cap))) % grass.height); // TOP band of paint_grass holds the canopy (Unity row flip)
            else if (dirt != null) c = dirt.GetPixel(x % dirt.width, y % dirt.height);
            else c = new Color(0.32f, 0.52f, 0.36f);
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
    }
    static Texture2D LoadStoryTex(string path) {
        if (!System.IO.File.Exists(path)) return null;
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        t.LoadImage(System.IO.File.ReadAllBytes(path));
        return t;
    }

    static Sprite SlabSprite(float w, float h) {
        var tex = new Texture2D((int)(w*32), (int)(h*32), TextureFormat.RGBA32, false);
        for (int y = 0; y < tex.height; y++) for (int x = 0; x < tex.width; x++) tex.SetPixel(x, y, new Color(0.32f, 0.52f, 0.36f));
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 32f);
    }

}

/// <summary>Tiny JSON parser (no external packages) — map001.json only needs dict/list/number/string.</summary>
public static class MiniJson {
    public static object Deserialize(string json) {
        int pos = 0;
        return ParseValue(json, ref pos);
    }
    static object ParseValue(string s, ref int i) {
        while (i < s.Length && (s[i]==' '||s[i]=='\n'||s[i]=='\r'||s[i]=='\t')) i++;
        char c = s[i];
        if (c=='{') { i++; var d = new System.Collections.Generic.Dictionary<string,object>();
            while (true) { while (char.IsWhiteSpace(s[i])) i++;
                if (s[i]=='}') { i++; return d; }
                while (s[i]!='"') i++; i++; int en=i; while(!(s[i]=='"'&&s[i-1]!='\\')) i++;
                var key=s.Substring(en,i-en); i++;
                while (s[i]!=':') i++; i++;
                d[key]=ParseValue(s, ref i);
                while (char.IsWhiteSpace(s[i])) i++;
                if (s[i]==',') i++;
            } }
        if (c=='[') { i++; var l=new System.Collections.Generic.List<object>();
            while (true) { while (char.IsWhiteSpace(s[i])) i++;
                if (s[i]==']') { i++; return l; }
                l.Add(ParseValue(s, ref i));
                while (char.IsWhiteSpace(s[i])) i++;
                if (s[i]==',') i++;
            } }
        if (c=='"') { i++; int st=i; while(!(s[i]=='"'&&s[i-1]!='\\')) i++; var v=s.Substring(st,i-st); i++; return v; }
        if (c=='t'||c=='f') { bool b=s[i]=='t'; while(s[i]!='e'&&s[i]>' ') i++; if(s[i]=='e')i++; return b; }
        if (c=='n') { i+=4; return null; } // JSON null (map_m1: "drone": null - M1's clean floor carries no drone)
        int ns=i; while (i<s.Length && (char.IsDigit(s[i])||s[i]=='-'||s[i]=='+'||s[i]=='.'||s[i]=='e'||s[i]=='E')) i++;
        return double.Parse(s.Substring(ns,i-ns), System.Globalization.CultureInfo.InvariantCulture);
    }
}
}
#endif
