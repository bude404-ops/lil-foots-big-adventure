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
            if (string.IsNullOrEmpty(v)) v = "map001.json";
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
        foreach (var po in plats.Cast<System.Collections.Generic.List<object>>()) {
            float x = F(po[0]), y = F(po[1]), w = F(po[2]), h = F(po[3]);
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

        // ---- hounds ----
        var hounds = (System.Collections.Generic.List<object>)data["hounds"];
        foreach (var ho in hounds.Cast<System.Collections.Generic.Dictionary<string, object>>()) {
            var h = new GameObject("Hound");
            h.transform.SetParent(root.transform);
            h.transform.position = new Vector3(F(ho["x"])/100f, GY + 0.35f, 0); // spawn ABOVE the slab
            // (Sept 20: spawning at GY-0.3 embedded the hound's 0.6-tall box INTO the ground -
            // depenetration jitter + a "stale model" look. Box half-height is 0.3, so GY+0.35
            // falls 0.05 to a clean rest.)
            var hc = h.AddComponent<HoundController>();
            hc.minX = F(ho["min"])/100f; hc.maxX = F(ho["max"])/100f;
            hc.dir = (int)F(ho["dir"]); hc.speed = F(ho["spd"])/100f;
            var bc = h.AddComponent<BoxCollider2D>(); bc.size = new Vector2(0.9f, 0.6f);
            var tr = h.AddComponent<CircleCollider2D>(); tr.isTrigger = true; tr.radius = 0.55f;
            var rb = h.AddComponent<Rigidbody2D>(); rb.freezeRotation = true;
            h.layer = LayerMask.NameToLayer("Enemy");
            HoundManager.Register(hc);
        }

        // ---- trail cams (tree-mounted snitches) ----
        var cams = (System.Collections.Generic.List<object>)data["cams"];
        foreach (var co in cams.Cast<System.Collections.Generic.Dictionary<string, object>>()) {
            var c = new GameObject("TrailCam");
            c.transform.SetParent(root.transform);
            c.transform.position = new Vector3(F(co["x"])/100f, 2f * GY - F(co["y"])/100f, 0); // canvas-y flip
            var cc = c.AddComponent<CircleCollider2D>(); cc.isTrigger = true; cc.radius = 0.45f;
            c.AddComponent<TrailCamController>();
            BuildTreeArt(root.transform, c.transform.position); // REAL TREE, not a pole (playtest fix)
        }

        // ---- drone (v2 M1: maps carry no drone - the clean floor ships without it) ----
        var drone = data.ContainsKey("drone") ? data["drone"] as System.Collections.Generic.Dictionary<string, object> : null;
        if (drone != null) {
            var d = new GameObject("Drone");
            d.transform.SetParent(root.transform);
            d.transform.position = new Vector3(F(drone["x"])/100f, 2f * GY - F(drone["y"])/100f, 0); // canvas-y flip
            d.AddComponent<DroneController>();
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
        Debug.Log("[LilFoots] Map 001 built: " + plats.Count + " plats, " + hounds.Count + " hounds, "
                  + cams.Count + " cams, " + tokens.Count + " tokens, " + cpList.Count + " checkpoints. Ctrl+S to save the scene.");
    }

    static float F(object o) { return System.Convert.ToSingle(o); }

    /// <summary>Placeholder white slab sprite (visible until the art pass swaps in art-bible surfaces).</summary>
    static Sprite SlabSprite(float w, float h) {
        var tex = new Texture2D((int)(w*32), (int)(h*32), TextureFormat.RGBA32, false);
        for (int y = 0; y < tex.height; y++) for (int x = 0; x < tex.width; x++) tex.SetPixel(x, y, new Color(0.32f, 0.52f, 0.36f));
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 32f);
    }

    /// <summary>Chunky woodland tree the cam mounts on (flared trunk + bark + canopy, per art bible).</summary>
    static void BuildTreeArt(Transform root, Vector3 camPos) {
        var tree = new GameObject("CamTree");
        tree.transform.SetParent(root);
        tree.transform.position = new Vector3(camPos.x, GameManager.GroundY/2f, 0.5f);
        // art pass: replace primitives with the drawn tree (trunk + bark ridges + 6-blob canopy + branch stub)
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
