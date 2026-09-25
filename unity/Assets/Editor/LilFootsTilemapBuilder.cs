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
            if (string.IsNullOrEmpty(v)) v = "map_r1_depth_test.json";
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
        foreach (var p in plats) {
            var go = new GameObject("Plat_" + p.x);
            go.transform.SetParent(root.transform);
            go.transform.position = new Vector3(p.x / 100f, 2f * GY - p.y / 100f - (p.w / 100f) / 2f, 0);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = new Vector2(p.z / 100f, p.w / 100f);
            go.layer = ground;
        }

        // ================= 2. TILEMAP TERRAIN (the skin layer) =================
        var tiles = EnsureTiles();
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
            int c0 = (int)Mathf.Floor(p.x / 100f);
            int c1 = (int)Mathf.Floor((p.x + p.z - 1f) / 100f);
            float su = 2f * GY - p.y / 100f;                         // surface unity y
            int rTop = (int)Mathf.Floor(su - GRID_Y_OFF + 0.0001f);  // tile row whose top edge = surface
            for (int c = c0; c <= c1; c++)
                for (int r = rTop; r > rTop - BEDROCK_ROWS; r--)
                    solid.Add(((long)(uint)c << 32) ^ (uint)r);
        }
        bool Has(int c, int r) { return solid.Contains(((long)(uint)c << 32) ^ (uint)r); }

        int painted = 0, grass = 0;
        foreach (var key in solid) {
            int c = (int)(key >> 32); int r = (int)(key & 0xffffffffL);
            bool top = !Has(c, r + 1);
            bool openL = !Has(c - 1, r);
            bool openR = !Has(c + 1, r);
            UnityEngine.Tilemaps.TileBase t;
            if (top) {
                t = openL ? tiles.grassTopL : openR ? tiles.grassTopR : tiles.grassTop;
                grass++;
            } else {
                t = openL ? tiles.dirtL : openR ? tiles.dirtR : tiles.dirt;
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
            go.transform.position = new Vector3(p.x / 100f + p.z / 200f, cy, 0f);
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
            float l0 = p.x / 100f, r0 = (p.x + p.z) / 100f;
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

        // ================= 9. PLAYER (spawn-at-clamp framing law) =================
        var player = new GameObject("Lily");
        player.transform.SetParent(root.transform);
        float viewHalfW0 = 3.75f * (16f / 9f);
        player.transform.position = new Vector3(viewHalfW0 - 0.15f, GY + 0.1f, 0);
        var pc = player.AddComponent<PlayerController>();
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
        cam.transform.position = new Vector3(6.7f, 7.0f, -10f);
        var camc = cam.AddComponent<Camera>(); camc.orthographic = true; camc.orthographicSize = 3.75f;
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
        public UnityEngine.Tilemaps.TileBase grassTop, grassTopL, grassTopR, dirt, dirtL, dirtR;
    }

    static TileSet EnsureTiles() {
        Directory.CreateDirectory("Assets/Art/Tiles");
        return new TileSet {
            grassTop  = Tile("lf_grass_top",   TileTex(new Color(0.35f, 0.55f, 0.35f), new Color(0.55f, 0.75f, 0.55f), false, false)),
            grassTopL = Tile("lf_grass_top_l", TileTex(new Color(0.35f, 0.55f, 0.35f), new Color(0.55f, 0.75f, 0.55f), true, false)),
            grassTopR = Tile("lf_grass_top_r", TileTex(new Color(0.35f, 0.55f, 0.35f), new Color(0.55f, 0.75f, 0.55f), false, true)),
            dirt      = Tile("lf_dirt",       TileTex(new Color(0.14f, 0.18f, 0.12f), new Color(0.22f, 0.28f, 0.18f), false, false)),
            dirtL     = Tile("lf_dirt_l",     TileTex(new Color(0.14f, 0.18f, 0.12f), new Color(0.22f, 0.28f, 0.18f), true, false)),
            dirtR     = Tile("lf_dirt_r",     TileTex(new Color(0.14f, 0.18f, 0.12f), new Color(0.22f, 0.28f, 0.18f), false, true)),
        };
    }

    /// <summary>Loads or creates a persistent Tile asset whose sprite lives at
    /// Assets/Art/Tiles/<name>.png. Drop a hand-painted PNG over that path and the
    /// whole world reskins on the next import - no code changes.</summary>
    static UnityEngine.Tilemaps.TileBase Tile(string name, Texture2D tex) {
        const string dir = "Assets/Art/Tiles";
        string png = dir + "/" + name + ".png";
        string asset = dir + "/" + name + ".asset";
        var t = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(asset);
        if (t != null && t.sprite != null) return t;
        if (!File.Exists(png)) { File.WriteAllBytes(png, tex.EncodeToPNG()); AssetDatabase.ImportAsset(png); }
        var ti = (TextureImporter)AssetImporter.GetAtPath(png);
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 128f;
        ti.filterMode = FilterMode.Bilinear;
        ti.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png);
        if (t == null) {
            t = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            AssetDatabase.CreateAsset(t, asset);
        }
        t.sprite = sprite;
        EditorUtility.SetDirty(t);
        return t;
    }

    static Texture2D TileTex(Color body, Color cap, bool shadeL, bool shadeR) {
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        var rnd = new System.Random(404);
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) {
            Color c = y > N - 30 ? cap : body;
            float n = (rnd.Next() % 1000) / 1000f;
            c = new Color(c.r * (0.92f + 0.08f * n), c.g * (0.92f + 0.08f * n), c.b * (0.92f + 0.08f * n), 1f);
            if (shadeL && x < 12) c *= 0.75f;
            if (shadeR && x > N - 13) c *= 0.75f;
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    static Sprite SlabSprite() {
        if (_slab == null) {
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) {
                float r = Mathf.Max(Mathf.Abs(x - 31.5f) / 30f, Mathf.Abs(y - 31.5f) / 30f);
                bool inside = r < 1f;
                var c = new Color(0.35f, 0.55f, 0.35f, inside ? 1f : 0f);
                if (y > 52 && inside) c = new Color(0.55f, 0.75f, 0.55f, 1f);
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
