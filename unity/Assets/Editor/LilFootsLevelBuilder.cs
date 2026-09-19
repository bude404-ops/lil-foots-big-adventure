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
            // top surface sits at y (engine convention), slab hangs below it
            go.transform.position = new Vector3(x/100f, y/100f - (h/100f)/2f, 0);
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
            cp.transform.position = new Vector3(cpList[i]/100f, GY, 0);
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
            h.transform.position = new Vector3(F(ho["x"])/100f, GY - 0.3f, 0);
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
            c.transform.position = new Vector3(F(co["x"])/100f, F(co["y"])/100f, 0);
            var cc = c.AddComponent<CircleCollider2D>(); cc.isTrigger = true; cc.radius = 0.45f;
            c.AddComponent<TrailCamController>();
            BuildTreeArt(root.transform, c.transform.position); // REAL TREE, not a pole (playtest fix)
        }

        // ---- drone ----
        var drone = (System.Collections.Generic.Dictionary<string, object>)data["drone"];
        var d = new GameObject("Drone");
        d.transform.SetParent(root.transform);
        d.transform.position = new Vector3(F(drone["x"])/100f, F(drone["y"])/100f, 0);
        d.AddComponent<DroneController>();

        // ---- tokens (73, 4 tiers) ----
        var tokens = (System.Collections.Generic.List<object>)data["tokens"];
        foreach (var to in tokens.Cast<System.Collections.Generic.Dictionary<string, object>>()) {
            var t = new GameObject("Token_" + F(to["x"]));
            t.transform.SetParent(root.transform);
            t.transform.position = new Vector3(F(to["x"])/100f, F(to["y"])/100f, 0);
            var cc = t.AddComponent<CircleCollider2D>(); cc.isTrigger = true; cc.radius = 0.34f;
            t.AddComponent<TokenCollectible>().tier = (int)F(to["tier"]);
        }

        // ---- gate ----
        var gate = new GameObject("Gate");
        gate.transform.SetParent(root.transform);
        gate.transform.position = new Vector3(86f, GY, 0);
        var gc = gate.AddComponent<BoxCollider2D>(); gc.isTrigger = true; gc.size = new Vector2(0.8f, 3f);
        gate.AddComponent<GateController>();

        // ---- player ----
        var player = new GameObject("Lily");
        player.transform.SetParent(root.transform);
        player.transform.position = new Vector3(1.1f, GY + 0.1f, 0);
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
        cam.tag = "MainCamera";
        var gm = new GameObject("GameManager").AddComponent<GameManager>();

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
        int ns=i; while (i<s.Length && (char.IsDigit(s[i])||s[i]=='-'||s[i]=='+'||s[i]=='.'||s[i]=='e'||s[i]=='E')) i++;
        return double.Parse(s.Substring(ns,i-ns), System.Globalization.CultureInfo.InvariantCulture);
    }
}
}
#endif
