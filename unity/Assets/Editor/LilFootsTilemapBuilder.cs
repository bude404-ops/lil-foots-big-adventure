#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace LilFoots {
/// <summary>
/// TILEMAP PIVOT (BudE green light, Sept 25 2026: "only take the geometric setting and
/// inlay them in a new map and then use unity to skin it").
///
/// The old pipeline painted one big canvas picture and stretched it over the level -
/// the running surface was a painted line, not real terrain. This builder constructs
/// NATIVE Unity terrain from the audited level geometry:
///
///   - Grid + Tilemap terrain: every ground collider becomes real tiles (1 tile = 1
///     unit): grass-cap tiles on the surface row, dirt tiles below, edge variants on
///     exposed sides, solid down to bedrock. TILES ARE THE SKIN: hand-painted tile art
///     dropped at Assets/Art/Tiles/<name>.png reskins the whole world on reimport -
///     no code changes. Until real art exists, canon-green placeholder tiles are
///     generated automatically so the level is playable and readable immediately.
///   - Gameplay physics stay EXACT: every audited collider keeps its real
///     BoxCollider2D (Ground layer). The audited geometry is untouched.
///   - Hop platforms: exact colliders + mossy slab sprites (skin hooks).
///   - Suppressed flat backdrop plates (sky + far forest) behind the play plane with
///     ParallaxProp sweep - skin hooks, replaced by real backdrop art later.
///   - Tokens / checkpoints / gate / portal / secret heart / player / camera / audio
///     identical to LilFootsLevelBuilder - the smoke gate passes unchanged.
///
/// Entry: Tools > Lil Foots > Build Tilemap Level, or the forge with LILFOOTS_TILEMAP=1
/// (ArtPass.BuildAndShootCore routes here; touch deck + character menu + smoke test
/// still run around it).
/// </summary>
public static class LilFootsTilemapBuilder {
    static string DataPath {
        get {
            var v = System.Environment.GetEnvironmentVariable("MAP_DATA");
            if (string.IsNullOrEmpty(v)) v = "map_region1_spine.json";
            if (!v.Contains("/")) v = "Assets/LevelData/" + v;
            return v;
        }
    }

    const float GY = 6.2f;         // surface law: canvas y=620 -> unity y=6.2 (canvas-y flip)
    const float GRID_Y_OFF = 0.2f; // tiles' row-6 top edge lands exactly at 6.2 with this offset
    const int BEDROCK_ROWS = 12;   // tiles filled below each surface (solid earth under the world)

    [MenuItem("Tools/Lil Foots/Build Tilemap Level")]
    public static void Build() {
        if (!File.Exists(DataPath)) { Debug.LogError("[Tilemap] map data not found at " + DataPath); return; }
        var data = (Dictionary<string, object>)MiniJson.Deserialize(File.ReadAllText(DataPath));
        var meta = (Dictionary<string, object>)data["meta"];
        var ground = LayerMask.NameToLayer("Ground");

        var root = new GameObject("MAP_TILEMAP");
        // plats are [x, y_top, w, h] in px -> Vector4(x, y, w, h)
        var plats = ((List<object>)data["plats"]).Cast<List<object>>().Select(p =>
            new Vector4(F(p[0]), F(p[1]), F(p[2]), F(p[3]))).ToList();
        var grounds = plats.Where(p => p.y >= 600f || p.w >= 400f).ToList();
        var hops    = plats.Where(p => !(p.y >= 600f || p.w >= 400f)).ToList();

        // ================= 1. EXACT PHYSICS: every audited collider, unchanged =================
        // [ROOT CAUSE Sept 27 PM - BUD-E: "hidden or invisible boxes" + "issues with the hit
        // box"] the map JSON plat x is CENTER-based (course_auditor.py span(): "x is
        // center-based") and the LevelBuilder physics below centers on x/100 - CORRECT.
        // But the TILE painter + gate surface + hop slab visuals treated x as a LEFT EDGE,
        // shifting the whole visible terrain half-width RIGHT of the physics: you walk on
        // air over the right half of every visible ground and hit invisible ledges on the
        // left. Everything below now uses the center convention end-to-end.
        // Same-height overlapping grounds ALSO leave internal seams the capsule catches on -
        // welded into one collider (union shape identical, seams gone).
        var welds = new List<List<Vector4>>();
        foreach (var g in grounds.OrderBy(g => g.y).ThenBy(g => g.x)) {
            List<Vector4> row = null;
            for (int i = welds.Count - 1; i >= 0; i--) {
                var w = welds[i];
                float lastRight = (w[w.Count - 1].x + w[w.Count - 1].z / 2f) / 100f;
                float gLeft = (g.x - g.z / 2f) / 100f;
                if (Mathf.Abs(w[0].y - g.y) < 0.5f && gLeft <= lastRight + 0.02f) { row = w; break; }
            }
            if (row == null) { row = new List<Vector4>(); welds.Add(row); }
            row.Add(g);
        }
        foreach (var w in welds) {
            if (w.Count == 1) continue;
            float l = w.Min(s => s.x - s.z / 2f), r = w.Max(s => s.x + s.z / 2f);
            float yTop = w[0].y, depth = w.Max(s => s.w);
            var go = new GameObject("PlatWeld_" + l);
            go.transform.SetParent(root.transform);
            go.transform.position = new Vector3((l + r) / 2f / 100f, 2f * GY - yTop / 100f - (depth / 100f) / 2f, 0);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = new Vector2((r - l) / 100f, depth / 100f);
            go.layer = ground;
        }
        var welded = new HashSet<Vector4>(welds.Where(w => w.Count > 1).SelectMany(w => w));
        foreach (var p in plats.Where(p => !welded.Contains(p))) {
            var go = new GameObject("Plat_" + p.x);
            go.transform.SetParent(root.transform);
            go.transform.position = new Vector3(p.x / 100f, 2f * GY - p.y / 100f - (p.w / 100f) / 2f, 0);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = new Vector2(p.z / 100f, p.w / 100f);
            go.layer = ground;
        }

        // ================= 2. TILEMAP TERRAIN (the skin layer) =================
        // SINGLE SOURCE: when the Unity tile skin pass is active (LILFOOTS_TILEMAP, the
        // default) LilFootsTilemapSkin.PaintGrounds paints the terrain FROM THE COLLIDERS -
        // always convention-correct. This builder-side tilemap is the no-skin fallback only.
        bool skinActive = (System.Environment.GetEnvironmentVariable("LILFOOTS_TILEMAP") ?? "1") != "0";
        var tiles = skinActive ? null : EnsureTiles();
        var gridGo = new GameObject("TerrainGrid");
        gridGo.transform.SetParent(root.transform);
        gridGo.transform.position = new Vector3(0f, GRID_Y_OFF, 0f);
        gridGo.AddComponent<UnityEngine.Grid>();
        var tmGo = new GameObject("Terrain");
        tmGo.transform.SetParent(gridGo.transform);
        var tm = tmGo.AddComponent<UnityEngine.Tilemaps.Tilemap>();
        var tmr = tmGo.AddComponent<UnityEngine.Tilemaps.TilemapRenderer>();
        tmr.sortingOrder = 0;

        // solid-cell set: grounds -> tiles, filled down to bedrock
        var solid = new HashSet<long>();
        foreach (var p in grounds) {
            int c0 = (int)Mathf.Floor((p.x - p.z / 2f) / 100f);            // CENTER-based (see physics note)
            int c1 = (int)Mathf.Floor((p.x + p.z / 2f - 1f) / 100f);
            float su = 2f * GY - p.y / 100f;                         // surface unity y
            int rTop = (int)Mathf.Floor(su - GRID_Y_OFF + 0.0001f);  // tile row whose top edge = surface
            for (int c = c0; c <= c1; c++)
                for (int r = rTop; r > rTop - BEDROCK_ROWS; r--)
                    solid.Add(((long)(uint)c << 32) ^ (uint)r);
        }
        bool Has(int c, int r) { return solid.Contains(((long)(uint)c << 32) ^ (uint)r); }

        int painted = 0, grass = 0;
        foreach (var key in (tiles == null ? new HashSet<long>() : solid)) {
            int c = (int)(key >> 32); int r = (int)(key & 0xffffffffL);
            bool top = !Has(c, r + 1);
            bool openL = !Has(c - 1, r);
            bool openR = !Has(c + 1, r);
            UnityEngine.Tilemaps.TileBase t;
            // HASH PICK (BudE Sept 25: "still doesn't look right with the lines"): the old
            // checkerboard pick (c&1)^(r&1) drew a visible alternating GRID across the terrain.
            // This deterministic per-cell hash scatters the 4 hand-painted variants naturally.
            int hc = (c * 73856093) ^ (r * 19349663); hc ^= hc >> 13; hc *= 60493; hc ^= hc >> 11;
            int pick = ((hc & 0x7fffffff) + r * 7919) % tiles.grassTop.Length;
            int pickD = ((hc & 0x7fffffff) + c * 104729) % tiles.dirt.Length;
            UnityEngine.Tilemaps.TileBase tG = tiles.grassTop[pick];
            UnityEngine.Tilemaps.TileBase tD = tiles.dirt[pickD];
            if (top) {
                t = openL ? tiles.grassTopL : openR ? tiles.grassTopR : tG;
                grass++;
            } else {
                t = openL ? tiles.dirtL : openR ? tiles.dirtR : tD;
            }
            tm.SetTile(new Vector3Int(c, r, 0), t);
            painted++;
        }
        tm.CompressBounds();

        // ================= 3. HOP SLABS (visual skin hooks - physics live on the Plat_ colliders) =================
        var slab = SlabSprite();
        foreach (var p in hops) {
            var go = new GameObject("PlatArt_" + p.x);
            go.transform.SetParent(root.transform);
            float cy = 2f * GY - p.y / 100f - (p.w / 100f) / 2f;
            go.transform.position = new Vector3(p.x / 100f, cy, 0f);   // CENTER-based: x IS the slab center
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = slab; sr.sortingOrder = 1;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(p.z / 100f, p.w / 100f);
        }

        // ================= 4. SUPPRESSED BACKDROP PLATES (skin hooks) =================
        var skyGo = new GameObject("SkyPlate");
        skyGo.transform.SetParent(root.transform);
        var skySr = skyGo.AddComponent<SpriteRenderer>();
        skySr.sprite = FlatSprite(new Color(0.78f, 0.83f, 0.82f));   // canon ref: pale sage mist
        skySr.sortingOrder = -20; skySr.drawMode = SpriteDrawMode.Tiled;
        skySr.size = new Vector2(160f, 24f);
        skyGo.transform.position = new Vector3(40f, 6f, 30f);

        var farGo = new GameObject("FarForestPlate");
        farGo.transform.SetParent(root.transform);
        var farSr = farGo.AddComponent<SpriteRenderer>();
        farSr.sprite = FlatSprite(new Color(0.16f, 0.26f, 0.21f));   // deep forest darks
        farSr.sortingOrder = -15; farSr.drawMode = SpriteDrawMode.Tiled;
        farSr.size = new Vector2(160f, 8f);
        farGo.transform.position = new Vector3(40f, 3.5f, 20f);
        var pp = farGo.AddComponent<ParallaxProp>(); pp.factor = 0.25f;

        // ================= 5. LIVES + CHECKPOINTS =================
        var lm = new GameObject("Lives").AddComponent<LivesManager>();
        var cpList = ((List<object>)data["checkpoints"]).Cast<object>().Select(c => F(c)).ToList();
        var cpTransforms = new Transform[cpList.Count];
        for (int i = 0; i < cpList.Count; i++) {
            var cp = new GameObject("Checkpoint_" + i);
            cp.transform.SetParent(root.transform);
            cp.transform.position = new Vector3(cpList[i] / 100f, GY + 0.85f, 0);
            var col = cp.AddComponent<BoxCollider2D>(); col.isTrigger = true;
            cp.AddComponent<CheckpointController>().index = i;
            cpTransforms[i] = cp.transform;
        }
        lm.checkpoints = cpTransforms;

        // ================= 6. TOKENS =================
        foreach (var to in ((List<object>)data["tokens"]).Cast<Dictionary<string, object>>()) {
            var t = new GameObject("Token_" + F(to["x"]));
            t.transform.SetParent(root.transform);
            t.transform.position = new Vector3(F(to["x"]) / 100f, 2f * GY - F(to["y"]) / 100f, 0);
            var cc = t.AddComponent<CircleCollider2D>(); cc.isTrigger = true; cc.radius = 0.34f;
            t.AddComponent<TokenCollectible>().tier = (int)F(to["tier"]);
        }

        // ================= 7. SECRET HEART =================
        var heartData = (Dictionary<string, object>)data["secretHeart"];
        var heart = new GameObject("SecretHeart");
        heart.transform.SetParent(root.transform);
        heart.transform.position = new Vector3(F(heartData["x"]) / 100f, 2f * GY - F(heartData["y"]) / 100f, 0);
        var hcc = heart.AddComponent<CircleCollider2D>(); hcc.isTrigger = true; hcc.radius = 0.5f;
        heart.AddComponent<SecretHeartPickup>();

        // ================= 8. GATE + FINISH PROPS =================
        var gateData = (Dictionary<string, object>)data["gate"];
        float gateX = F(gateData["x"]) / 100f;
        float gateSurf = GY;
        foreach (var p in grounds) {
            float l0 = (p.x - p.z / 2f) / 100f, r0 = (p.x + p.z / 2f) / 100f;   // CENTER-based
            if (gateX >= l0 && gateX <= r0) {
                float s0 = 2f * GY - p.y / 100f; if (s0 > gateSurf) gateSurf = s0;
            }
        }
        var gate = new GameObject("Gate");
        gate.transform.SetParent(root.transform);
        gate.transform.position = new Vector3(gateX, gateSurf, 0);
        var gc = gate.AddComponent<BoxCollider2D>(); gc.isTrigger = true; gc.size = new Vector2(0.8f, 3f);
        gate.AddComponent<GateController>();

        var flag = new GameObject("FlagGateArt"); flag.transform.SetParent(root.transform);
        flag.transform.position = new Vector3(gate.transform.position.x, gateSurf + 1.2f, 0);
        var fsr = flag.AddComponent<SpriteRenderer>(); fsr.sprite = slab; fsr.sortingOrder = 4;
        fsr.drawMode = SpriteDrawMode.Tiled; fsr.size = new Vector2(1.1f, 2.2f);
        var portal = new GameObject("PortalArt"); portal.transform.SetParent(root.transform);
        portal.transform.position = new Vector3(F(((Dictionary<string, object>)data["portal"])["x"]) / 100f, gateSurf + 1.6f, 0);
        var psr = portal.AddComponent<SpriteRenderer>(); psr.sprite = slab; psr.sortingOrder = 3;
        psr.drawMode = SpriteDrawMode.Tiled; psr.size = new Vector2(3.2f, 3.2f);

        // ================= 9. PLAYER (Sept 26 camera law: BudE "the camera needs zoomed out
        // the very beginning is always cut off and the character can disappear if runs to the
        // left") - spawn INSIDE the first visible screen near the left edge: the whole opening
        // of the level is on screen from frame one, and the player's frame bounds (set below)
        // hold him inside the visible screen at both level edges.
        var player = new GameObject("Lily");
        player.transform.SetParent(root.transform);
        float viewHalfW0 = 4.6f * (16f / 9f);
        player.transform.position = new Vector3(2.2f, GY + 0.1f, 0);
        var pc = player.AddComponent<PlayerController>();
        pc.boundMinX = 0.45f;                                        // never off-screen left
        pc.boundMaxX = F(meta["width"]) / 100f - 0.45f;              // never off-screen right
        var pcol = player.AddComponent<CapsuleCollider2D>(); pcol.size = new Vector2(0.44f, 0.7f); pcol.offset = new Vector2(0, 0.35f);
        var prb = player.AddComponent<Rigidbody2D>();
        prb.freezeRotation = true; prb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        prb.gravityScale = 2.446f;
        pc.rb = prb;
        player.AddComponent<SpriteRenderer>().sortingOrder = 10;
        player.layer = LayerMask.NameToLayer("Player");

        // ================= 10. CAMERA + MANAGERS =================
        var cam = new GameObject("MainCamera");
        cam.transform.SetParent(root.transform);
        cam.transform.position = new Vector3(8.2f, 7.0f, -10f);       // starts AT the minX clamp: the level opening is framed from world x=0
        var camc = cam.AddComponent<Camera>(); camc.orthographic = true;
        camc.orthographicSize = 4.6f;   // [ZOOM OUT Sept 26] 3.75 -> 4.6: BudE "camera needs zoomed out" - the frame breathes
        camc.backgroundColor = new Color(0.78f, 0.83f, 0.82f);
        cam.tag = "MainCamera";
        var cf = cam.AddComponent<CameraFollow>(); cf.target = player.transform;
        float viewHalfW = camc.orthographicSize * (16f / 9f);
        cf.minX = viewHalfW;
        cf.maxX = Mathf.Max(cf.minX + 1f, F(meta["width"]) / 100f - viewHalfW);
        new GameObject("GameManager").AddComponent<GameManager>();

        var am = new GameObject("AudioManager").AddComponent<AudioManager>();
        am.musicForestLoop = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/music_forest_loop.wav");
        am.sfxJump = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/sfx_jump.wav");
        am.sfxToken = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/sfx_token.wav");
        am.sfxCheckpoint = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/sfx_checkpoint.wav");
        am.sfxDeath = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/sfx_death.wav");
        am.sfxLevelComplete = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/sfx_levelcomplete.wav");
        am.sfxHeart = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/sfx_heart.wav");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Tilemap] Built: " + painted + " tiles (" + grass + " grass caps), " + plats.Count +
                  " exact colliders, " + hops.Count + " hop slabs, " + cpList.Count + " checkpoints. Ctrl+S to save.");
    }

    // ---------------- placeholder tiles (canon-green; reskin by dropping real PNGs) ----------------
    class TileSet {
        public UnityEngine.Tilemaps.TileBase grassTopL, grassTopR, dirtL, dirtR;
        public UnityEngine.Tilemaps.TileBase[] grassTop, dirt;   // painted variants, hash-picked
    }

    static TileSet EnsureTiles() {
        Directory.CreateDirectory("Assets/Art/Tiles");
        // [UNITY-BUILT SKIN, BudE Sept 25 ~11:45 PM ET: "remove ant art work and just have
        // unity build it for the maps"] The painted lf_*.png set is RETIRED. LilFootsProcTiles
        // GENERATES every tile texture in Unity (seeded noise moss caps, strata dirt, shaded
        // lips) - the map skin is built by the engine, not composited from art files.
        return new TileSet {
            grassTop  = new UnityEngine.Tilemaps.TileBase[] {
                LilFootsProcTiles.EnsureTile("lf_grass_a"), LilFootsProcTiles.EnsureTile("lf_grass_b"),
                LilFootsProcTiles.EnsureTile("lf_grass_c"), LilFootsProcTiles.EnsureTile("lf_grass_d") },
            grassTopL = LilFootsProcTiles.EnsureTile("lf_grass_l"),
            grassTopR = LilFootsProcTiles.EnsureTile("lf_grass_r"),
            dirt      = new UnityEngine.Tilemaps.TileBase[] {
                LilFootsProcTiles.EnsureTile("lf_dirt_a"), LilFootsProcTiles.EnsureTile("lf_dirt_b"),
                LilFootsProcTiles.EnsureTile("lf_dirt_c"), LilFootsProcTiles.EnsureTile("lf_dirt_d") },
            dirtL     = LilFootsProcTiles.EnsureTile("lf_dirt_l"),
            dirtR     = LilFootsProcTiles.EnsureTile("lf_dirt_r"),
        };
    }

    // [RETIRED Sept 25] Tile()/TileTex() placeholder generators removed with the painted
    // art retirement - LilFootsProcTiles.EnsureTile() is the single tile source now.

    static Sprite SlabSprite() {
        if (_slab == null) {
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            // [BUD-E Sept 27: "hidden or invisible boxes"] the old flat-green slab vanished
            // against the terrain caps and pale sky - colliders with no readable visual.
            // Bolder: dark bark outline, mossy body gradient, bright lit top band, shadow base.
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) {
                bool inside = y >= 1 && y < 63;   // slab fills the tiling cell horizontally
                var c = Color.clear;
                if (inside) {
                    float fy = y / 63f;
                    c = Color.Lerp(new Color(0.24f, 0.34f, 0.22f), new Color(0.36f, 0.50f, 0.28f), fy);
                    if (y > 56) c = new Color(0.62f, 0.78f, 0.50f);           // lit moss top band
                    if (y < 4)  c = new Color(0.14f, 0.20f, 0.13f);            // shadowed base
                    if (y == 1 || y == 62) c = new Color(0.12f, 0.18f, 0.11f); // dark rim
                    if ((x * 7 + y * 13) % 23 == 0 && y < 56) c *= 1.18f;      // bark speckle
                }
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            _slab = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
        }
        return _slab;
    }
    static Sprite _slab;

    static Sprite FlatSprite(Color c) {
        var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) tex.SetPixel(x, y, c);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
    }

    static float F(object o) { return System.Convert.ToSingle(o); }
}
}
#endif
