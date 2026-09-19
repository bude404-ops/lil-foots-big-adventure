#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LilFoots.EditorTools
{
    /// <summary>
    /// UNITY ART LAW (Bude, Sept 18 2026): "The art used in Unity shouldnt be any you used
    /// through code work for the test demo." This pass dresses the built MAP001 scene with
    /// REAL art assets only — no procedural slabs, no code-drawn shapes. Everything visual
    /// comes from Assets/Art (generated + inked PNGs per the art bible).
    /// Art goes on CHILD sprite objects so gameplay colliders are never scaled.
    /// </summary>
    public static class LilFootsArtPass
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        static Sprite Art(string file) {
            if (Cache.TryGetValue(file, out var cached)) return cached;
            string path = "Assets/Art/" + file;
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null) {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 100f;
                ti.mipmapEnabled = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.SaveAndReimport();
            }
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Cache[file] = s;
            if (s == null) Debug.LogWarning("[ArtPass] MISSING ART: " + file);
            return s;
        }

        static GameObject SpriteGo(string name, Sprite s, Vector3 pos, float width, int order, Transform parent = null) {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.sortingOrder = order;
            float f = s != null ? width / s.bounds.size.x : 1f;
            go.transform.localScale = new Vector3(f, f, 1f);
            go.transform.position = pos;
            return go;
        }

        /// <summary>Art child on a gameplay object — parent's colliders/rigidbody stay unscaled.</summary>
        static GameObject ChildSprite(GameObject parent, string name, Sprite s, float height, int order, bool flipX = false) {
            var old = parent.GetComponent<SpriteRenderer>();
            if (old != null) Object.DestroyImmediate(old); // drop any placeholder renderer on the body
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.sortingOrder = order; sr.flipX = flipX;
            if (s != null) {
                float f = height / s.bounds.size.y;
                go.transform.localScale = new Vector3(f, f, 1f);
            }
            return go;
        }

        public static void BuildArt() {
            var map = GameObject.Find("MAP001");
            if (map == null) { Debug.LogError("[ArtPass] MAP001 not found — run Tools > Lil Foots > Build Map 001 first."); return; }
            var data = MiniJson.Deserialize(File.ReadAllText("Assets/LevelData/map001.json")) as Dictionary<string, object>;
            float GY = GameManager.GroundY;

            // LAYER-BY-LAYER BUILD LAW (Bude, Sept 18 2026: "start over completely on the map in unity
            // and add one layer at a time... send me a image with one layer at a time as well and wait
            // for approved"): LILFOOTS_LAYERS=N builds only the first N layers; default 99 = all layers.
            int LN = 99;
            try { LN = System.Convert.ToInt32(System.Environment.GetEnvironmentVariable("LILFOOTS_LAYERS") ?? "99"); } catch { }
            bool L(int n) => LN >= n;
            Debug.Log("[ArtPass] LILFOOTS_LAYERS=" + LN + " (layer-by-layer review mode)");

            // ---- WORLD BACKDROP (Region 1: PNW) ----
            var cam = GameObject.Find("MainCamera");
            // pinned sky + THE one sun ride with the camera (never scroll, never duplicate)
            if (cam != null && L(1)) {
                SpriteGo("SkyPlate", Art("art_sky_pnw.png"), Vector3.zero, 16.2f, -100, cam.transform)
                    .transform.localPosition = new Vector3(0f, 0f, 10f);
                SpriteGo("TheSun", Art("art_sun.png"), Vector3.zero, 1.7f, -95, cam.transform)
                    .transform.localPosition = new Vector3(4.4f, 2.2f, 10f);
            }
            // Cascade ridges + snow-capped volcano — HORIZON BAND ~0.85u tall (demo proportion:
            // camera sees 7.5u tall; the old 19u-wide strip was ~5.6u tall and buried the sky)
            var ridges = Art("art_ridges.png");
            if (ridges != null && cam != null && L(2)) {
                // STATIC VISTA LAW (Bude): ridges ride with the camera - only the gameplay plane scrolls.
                // Camera is locked at y=4.5 by CameraFollow; ridge band world-Y = GY+1.05+rh/2 -> local y = that - 4.5.
                float rh = 0.85f;
                float rw = 14.5f; // wider than the ~13.35u view so edges never show
                SpriteGo("Ridges", ridges, Vector3.zero, rw, -90, cam.transform)
                    .transform.localPosition = new Vector3(0f, (GY + 1.05f + rh / 2f) - 4.5f, 10f);
            }
            // dense fir wall — BAND ~1.5u tall at the ground line (old 12u-wide tile was 10.4u
            // tall — 1.4x the whole screen height; the map read as one zoomed wall texture)
            var firs = Art("art_firwall.png");
            if (firs != null && cam != null && L(3)) {
                // STATIC VISTA LAW (Bude): fir wall rides with the camera too.
                float fh = 1.5f;
                float fw = 14.5f;
                SpriteGo("FirWall", firs, Vector3.zero, fw, -80, cam.transform)
                    .transform.localPosition = new Vector3(0f, (GY - 0.25f + fh / 2f) - 4.5f, 10f);
            }
            // drifting PNW mist banks (soft sprites, ~1.4u tall, upper sky band)
            var mist = Art("art_mist.png");
            if (mist != null && L(4)) {
                // size by HEIGHT so the HQ mist plate (~square) reads as a 1.3u bank, never a sky wall
                // STATIC VISTA LAW (Bude): mist banks ride with the camera, spread across the view.
                float mw = 1.3f * (mist.bounds.size.x / mist.bounds.size.y);
                float[] lx = { -5.2f, -1.6f, 2.2f, 5.4f };
                float[] ly = { (GY + 0.75f) - 4.5f, (GY + 1.35f) - 4.5f, (GY + 1.0f) - 4.5f, (GY + 1.5f) - 4.5f };
                for (int i = 0; i < 4; i++)
                    SpriteGo("MistBank", mist, Vector3.zero, mw, -70, cam != null ? cam.transform : map.transform)
                        .transform.localPosition = new Vector3(lx[i], ly[i], 10f);
            }

            // ---- STREAM WATER in the gaps ----
            var water = Art("art_water.png");
            var plats = (List<object>)data["plats"];
            var sorted = plats.Cast<List<object>>()
                .Select(p => new float[] { F(p[0]), F(p[1]), F(p[2]), F(p[3]) })
                .OrderBy(a => a[0]).ToList();
            if (water != null && L(6)) {
                for (int i = 0; i < sorted.Count - 1; i++) {
                    var a = sorted[i]; var b = sorted[i + 1];
                    float gapL = (a[0] + a[2] / 2f) / 100f, gapR = (b[0] - b[2] / 2f) / 100f;
                    float gw = gapR - gapL;
                    if (gw < 0.3f || gw > 7f) continue;
                    SpriteGo("Stream", water, new Vector3(gapL + gw / 2f, GY - 0.55f, 0), gw + 0.6f, -60, map.transform);
                }
            }

            // ---- PLATFORM SKINS: real earth body + ground strip top (procedural slabs retired) ----
            var earth = Art("art_earth.png");
            var strip = Art("art_ground_strip.png");
            foreach (Transform child in map.transform) {
                if (!child.name.StartsWith("Plat_")) continue;
                var bc = child.GetComponent<BoxCollider2D>();
                if (bc == null) continue;
                float w = bc.size.x, h = bc.size.y;
                float top = child.position.y + h / 2f;
                var oldSr = child.GetComponent<SpriteRenderer>();
                if (oldSr != null) Object.DestroyImmediate(oldSr); // no placeholder slabs in Unity
                if (earth != null && L(5)) {
                    float ew = h * 0.94f * (earth.bounds.size.x / earth.bounds.size.y);
                    for (float x = child.position.x - w / 2f; x < child.position.x + w / 2f; x += ew)
                        SpriteGo("Earth", earth, new Vector3(x, top - h / 2f, 0), ew, -2, child);
                }
                if (strip != null && L(5)) {
                    float sw = strip.bounds.size.x * (0.62f / strip.bounds.size.y);
                    for (float x = child.position.x - w / 2f; x < child.position.x + w / 2f - 0.05f; x += sw)
                        SpriteGo("GrassTop", strip, new Vector3(x, top - 0.28f, 0), sw, -1, child);
                }
            }

            // ---- CAM TREES + trail cam art ----
            var camTree = Art("art_camtree.png");
            var trailcamArt = Art("art_trailcam.png");
            foreach (Transform child in map.transform) {
                if (!child.name.StartsWith("TrailCam")) continue;
                if (camTree != null && L(7)) {
                    float cth = 3.1f; float ctw = cth * (camTree.bounds.size.x / camTree.bounds.size.y);
                    SpriteGo("CamTreeArt", camTree, new Vector3(child.position.x, GY - 0.55f + cth / 2f, 0), ctw, -6, map.transform);
                }
                if (trailcamArt != null && L(8)) ChildSprite(child.gameObject, "TrailCamArt", trailcamArt, 0.52f, 6);
            }

            // ---- FOREGROUND DEPTH PROPS (Bude Depth Doctrine): small dark fern/grass
            // silhouettes passing IN FRONT of the play plane, movie-like depth ----
            var fore = Art("art_fore_props.png");
            if (fore != null && L(9)) {
                float fh2 = 1.15f;
                float fw2 = fh2 * (fore.bounds.size.x / fore.bounds.size.y);
                float fx = -6f; int fi = 0;
                while (fx < 104f) {
                    var fg = SpriteGo("ForeProp", fore, new Vector3(fx, GY - 0.1f + fh2 / 2f, 0), fw2, 30, map.transform);
                    var fsr = fg.GetComponent<SpriteRenderer>();
                    if (fi % 2 == 1) fsr.flipX = true;   // alternate so the row doesn't visibly repeat
                    fx += fw2 * 0.62f; fi++;
                }
            }

            // ---- ENEMY ART (child sprites — hitboxes untouched) ----
            var hound = Art("art_hound.png");
            var drone = Art("art_drone.png");
            foreach (Transform child in map.transform) {
                if (child.name.StartsWith("Hound") && hound != null && L(8)) {
                    bool flip = child.GetComponent<HoundController>().dir < 0;
                    ChildSprite(child.gameObject, "HoundArt", hound, 0.62f, 6, flip);
                }
                if (child.name.StartsWith("Drone") && drone != null && L(8))
                    ChildSprite(child.gameObject, "DroneArt", drone, 0.55f, 6);
            }

            // ---- TOKENS (footprint Big Token) + secret heart ----
            var token = Art("art_token.png");
            foreach (Transform child in map.transform) {
                if (!child.name.StartsWith("Token_")) continue;
                if (token != null && L(8)) ChildSprite(child.gameObject, "TokenArt", token, 0.66f, 5);
            }
            var heartArt = Art("art_heart.png");
            var sh = data.ContainsKey("secretHeart") ? data["secretHeart"] as Dictionary<string, object> : null;
            if (sh != null && heartArt != null && L(8)) {
                var hb = new GameObject("SecretHeart");
                hb.transform.SetParent(map.transform);
                hb.transform.position = new Vector3(F(sh["x"]) / 100f, F(sh["y"]) / 100f, 0);
                var hsr = hb.AddComponent<SpriteRenderer>(); hsr.sprite = heartArt; hsr.sortingOrder = 5;
                float hf = 0.55f / heartArt.bounds.size.y; hb.transform.localScale = new Vector3(hf, hf, 1f);
                var hc = hb.AddComponent<CircleCollider2D>(); hc.isTrigger = true; hc.radius = 0.5f;
                hb.AddComponent<SecretHeartPickup>();
            }

            // ---- FINISH: flagpole gate + portal (real props) ----
            var gate = GameObject.Find("Gate");
            if (gate != null && L(8)) {
                var fgArt = Art("art_flaggate.png");
                if (fgArt != null) SpriteGo("FlagGateArt", fgArt, new Vector3(gate.transform.position.x, GY + 1.2f, 0), 2.2f, 4, map.transform);
                var portal = Art("art_flagportal.png");
                if (portal != null) SpriteGo("PortalArt", portal, new Vector3(gate.transform.position.x - 1.4f, GY + 1.6f, 0), 3.2f, 3, map.transform);
            }

            // ---- PLAYER: Lily, real art on a child sprite (capsule collider untouched) ----
            var lily = GameObject.Find("Lily");
            if (lily != null && L(8) && Art("whole_lily.png") != null)
                ChildSprite(lily, "LilyArt", Art("whole_lily.png"), 0.82f, 10);

            // ---- HUD: hearts row + wooden panel token counter (camera-pinned) ----
            if (cam != null && heartArt != null) {
                for (int i = 0; i < 3; i++)
                    SpriteGo("HUDHeart" + i, heartArt, Vector3.zero, 0.62f, 100, cam.transform)
                        .transform.localPosition = new Vector3(-5.9f + i * 0.75f, 3.2f, 10f);
                SpriteGo("HUDPanel", Art("art_panel.png"), Vector3.zero, 1.7f, 98, cam.transform)
                    .transform.localPosition = new Vector3(-4.35f, 3.2f, 10f);
                var tm = new GameObject("HUDCount").AddComponent<TextMesh>();
                tm.transform.SetParent(cam.transform, false);
                tm.transform.localPosition = new Vector3(-4.35f, 3.2f, 10f);
                tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                tm.fontSize = 48; tm.characterSize = 0.16f; tm.anchor = TextAnchor.MiddleCenter;
                tm.color = new Color(0.10f, 0.06f, 0.02f);
                tm.text = "0 / 18";
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[ArtPass] Art pass complete: real art only, per the Unity Art Law.");
        }

        static float F(object o) { return System.Convert.ToSingle(o); }

        /// <summary>
        /// MOBILE CONTROL DECK (Sept 18 playability fix): native uGUI Canvas + EventSystem + three
        /// on-screen buttons (LEFT / RIGHT / JUMP) using the canon button art, wired to the TouchDeck
        /// statics via TouchDeckButton. Without this the game only answers a keyboard — unplayable
        /// on Bude's phone and mouse-only on the web preview.
        /// </summary>
        static void BuildTouchDeck() {
            // EventSystem — uGUI pointer events need it
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            // Canvas — screen-space overlay, mobile-scaled
            var canvasGo = new GameObject("TouchDeckCanvas");
            var canvas = canvasGo.AddComponent<UnityEngine.Canvas>(); // Canvas is in UnityEngine, not UnityEngine.UI
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1334f, 750f);
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var btnL = Art("art_btnL.png");
            var btnJ = Art("art_btnJ.png");
            if (btnL != null && btnJ != null) {
                MakeDeckButton(canvasGo.transform, "BtnLeft",  btnL, false, TouchDeckButton.Kind.Left,
                    new Vector2(120f, 90f), new Vector2(150f, 150f));
                MakeDeckButton(canvasGo.transform, "BtnRight", btnL, true,  TouchDeckButton.Kind.Right,
                    new Vector2(300f, 90f), new Vector2(150f, 150f));
                MakeDeckButton(canvasGo.transform, "BtnJump",  btnJ, false, TouchDeckButton.Kind.Jump,
                    new Vector2(1214f, 90f), new Vector2(170f, 170f));
            }
            Debug.Log("[ArtPass] Touch deck built: uGUI LEFT/RIGHT/JUMP wired to TouchDeck.");
        }

        static void MakeDeckButton(Transform parent, string name, Sprite art, bool flip,
                                   TouchDeckButton.Kind kind, Vector2 anchoredPos, Vector2 size) {
            var go = new GameObject(name, typeof(UnityEngine.RectTransform));
            var rt = (UnityEngine.RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.sprite = art;
            img.preserveAspect = true;
            var c = img.color; c.a = 0.88f; img.color = c;
            if (flip) rt.localScale = new Vector3(-1f, 1f, 1f); // mirror for the right arrow
            var tb = go.AddComponent<TouchDeckButton>();
            tb.kind = kind;
        }

        /// <summary>CI entry: build map 001, dress with art, save scene, render QC shots, exit.</summary>
        public static void BuildAndShoot() {
            BuildAndShootCore();
            EditorApplication.Exit(0);
        }

        /// <summary>Full scene pipeline without exiting — also used by the APK build runner.</summary>
        public static void BuildAndShootCore() {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            LilFootsLevelBuilder.Build();
            BuildArt();
            BuildTouchDeck(); // MOBILE CONTROL DECK — the Sept 18 playability fix (Bude: "this isn't playable")
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Scenes/Map001.unity");
            var outDir = System.Environment.GetEnvironmentVariable("QC_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = "QCShots";
            Directory.CreateDirectory(outDir);

            var cam = GameObject.Find("MainCamera").GetComponent<Camera>();
            var cf = cam.GetComponent<CameraFollow>();
            if (cf != null) cf.enabled = false; // frame shots manually
            var lily = GameObject.Find("Lily");
            var hound = GameObject.Find("Hound");
            var gate = GameObject.Find("Gate");

            var rt = new RenderTexture(1334, 750, 24);
            cam.targetTexture = rt;
            // shot 1: start area, Lily on the grass
            cam.transform.position = new Vector3(lily.transform.position.x + 2.5f, 4.2f, -10f);
            Snap(rt, System.IO.Path.Combine(outDir, "unity_start.png"));
            // shot 2: mid-map, hound + cam tree
            float hx = hound != null ? hound.transform.position.x : 45f;
            cam.transform.position = new Vector3(hx + 2.2f, 4.2f, -10f);
            Snap(rt, System.IO.Path.Combine(outDir, "unity_mid.png"));
            // shot 3: finish gate + flag + portal
            float gx = gate != null ? gate.transform.position.x : 86f;
            cam.transform.position = new Vector3(gx - 2.5f, 4.2f, -10f);
            Snap(rt, System.IO.Path.Combine(outDir, "unity_gate.png"));

            Debug.Log("[ArtPass] QC shots done.");
        }

        static void Snap(RenderTexture rt, string path) {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            Camera.main.Render();
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = prev;
        }
    }
}
#endif
