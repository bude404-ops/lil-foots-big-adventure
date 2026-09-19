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
                // SKY = pixel-faithful slice of Bude's approved plate (map-pure-locked).
                // The approved plate has NO sun disc (its sun is the horizon glow) - no sun sprite
                // until Bude approves one.
                var skySpr = Art("art_sky_pnw.png");
                float skyW = 16.2f;
                float skyH = skyW * (skySpr.bounds.size.y / skySpr.bounds.size.x);
                // pin so the slice's top edge sits at the top of the 7.5u view
                SpriteGo("SkyPlate", skySpr, Vector3.zero, skyW, -100, cam.transform)
                    .transform.localPosition = new Vector3(0f, (7.5f - skyH) / 2f, 10f);
            }
            // Cascade ridges + snow-capped volcano = plate band (rows 200-480 of the approved
            // plate, dissolved edges). Frame mapping: plate x-scale 16.2u/1024px, y 7.5u/1024px.
            // Band top row 200 -> world y 6.78; center row 340 -> world 5.76 -> local 1.26.
            var ridges = Art("art_ridges.png");
            if (ridges != null && cam != null && L(2)) {
                // LAYER 2 v2 (Bude: 'The line at the middle should be there'): ridge band runs from
                // the haze tops down THROUGH the fir treeline, so the plate's mid line lands at the
                // frame middle. Plate frame mapping is non-uniform on purpose (wide engine view):
                // x = 16.2u/1024px, y = 7.5u/1024px. Slice = plate rows 200-520 (320px) -> 2.34u tall,
                // top edge at plate row 200 -> view-top minus 200*7.5/1024 -> local 2.29.
                // STATIC VISTA LAW (Bude): pinned to the camera - only the gameplay plane scrolls.
                float px2uY = 7.5f / 1024f;          // plate vertical scale (composition-true)
                float bandPx = 320f;                 // slice height in plate rows
                float ridgeW = 16.2f;                // spans the view like the sky
                float ridgeH = bandPx * px2uY;       // 2.34u - NOT aspect-derived (uniform sizing made
                                                      // the band 2.16x too tall and pushed the treeline
                                                      // off-frame; that was the missing mid-line bug)
                float topLocal = 3.75f - (200f * px2uY); // plate row 200 -> world 6.79
                var rgo = SpriteGo("Ridges", ridges, Vector3.zero, ridgeW, -90, cam.transform);
                var rsr = rgo.GetComponent<SpriteRenderer>();
                // explicit non-uniform scale: full width, plate-true height
                rgo.transform.localScale = new Vector3(
                    rgo.transform.localScale.x,               // width already set by SpriteGo
                    ridgeH / rsr.bounds.size.y, 1f);
                rgo.transform.localPosition = new Vector3(0f, topLocal - (ridgeH / 2f), 10f);
            }
            // dense fir wall — BAND ~1.5u tall at the ground line (old 12u-wide tile was 10.4u
            // tall — 1.4x the whole screen height; the map read as one zoomed wall texture)
            var firs = Art("art_firwall.png");
            if (firs != null && cam != null && L(3)) {
                // STATIC VISTA LAW (Bude): fir wall rides with the camera too.
                float fh = 1.5f;
                float fw = fh * (firs.bounds.size.x / firs.bounds.size.y); // L3 treeline ~1.64:1
                for (int k = -4; k <= 4; k++) {
                    var fwg = SpriteGo("FirWall", firs, Vector3.zero, fw - 0.02f, -80, cam.transform);
                    fwg.transform.localPosition = new Vector3(k * (fw - 0.02f), (GY - 0.25f + fh / 2f) - 4.5f, 10f);
                }
            }
            // drifting PNW mist banks (soft sprites, ~1.4u tall, upper sky band)
            var mist = Art("art_mist.png");
            if (mist != null && L(4)) {
                // size by HEIGHT so the HQ mist plate (~square) reads as a 1.3u bank, never a sky wall
                // STATIC VISTA LAW (Bude): mist banks ride with the camera, spread across the view.
                float mh = 1.0f; // L4 mist strip ~6.9:1 -> banks ~6.9u wide
                float mw = mh * (mist.bounds.size.x / mist.bounds.size.y);
                float[] lx = { -5.2f, -1.6f, 2.2f, 5.4f };
                float[] ly = { (GY + 0.75f) - 4.5f, (GY + 1.35f) - 4.5f, (GY + 1.0f) - 4.5f, (GY + 1.5f) - 4.5f };
                for (int i = 0; i < 4; i++) {
                    var mb = SpriteGo("MistBank", mist, Vector3.zero, mw, -70, cam != null ? cam.transform : map.transform);
                    mb.transform.localPosition = new Vector3(lx[i], ly[i], 10f);
                }
            }

            // ---- BELOW-GROUND DEPTH WASH (approved plate lower third): tinted mist banks
            // filling the zone under the ground line so it reads as teal mist-washed depth,
            // never raw sky. World-space: scrolls with the gameplay plane. ----
            if (mist != null && L(4)) {
                float wh = 2.2f;
                float ww = wh * (mist.bounds.size.x / mist.bounds.size.y);
                float[] wy = { GY - 1.4f, GY - 3.2f, GY - 4.9f };
                float[] wa = { 0.88f, 0.92f, 0.96f };
                for (int r = 0; r < wy.Length; r++) {
                    for (float x = -6f; x < 104f; x += ww * 0.92f) {
                        var wsh = SpriteGo("DepthWash", mist, new Vector3(x, wy[r], 0), ww, -58 + r, map.transform);
                        var wsr = wsh.GetComponent<SpriteRenderer>();
                        wsr.color = new Color(0.32f, 0.52f, 0.45f, wa[r]); // teal wash per plate bands
                    }
                }
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
                // dark near-field silhouette base across the bottom of the frame (plate band 9)
                float bh = 2.0f;
                float bw = bh * (fore.bounds.size.x / fore.bounds.size.y);
                float bx = -8f; int bi = 0;
                while (bx < 106f) {
                    var bg2 = SpriteGo("ForeBase", fore, new Vector3(bx, bh / 2f - 0.2f, 0), bw, 40, map.transform);
                    var bsr = bg2.GetComponent<SpriteRenderer>();
                    if (bi % 2 == 1) bsr.flipX = true;
                    bsr.color = new Color(0.30f, 0.36f, 0.28f, 0.97f); // near-black silhouettes per plate
                    bx += bw * 0.58f; bi++;
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

            // ---- PLAYER: selected Lil Foot, real art on a child sprite (capsule collider untouched) ----
            var lily = GameObject.Find("Lily");
            if (lily != null && L(8)) {
                string selChar = LilFoots.CharacterMenuController.Current();
                string selFile = selChar == "buddy" ? "whole_buddy.png"
                               : selChar == "emma" ? "whole_emma.png"
                               : "whole_lily.png";
                var selArt = Art(selFile);
                if (selArt != null) ChildSprite(lily, "PlayerArt", selArt, 0.82f, 10);
            }

            // ---- HUD: hearts row + wooden panel token counter (shared builder) ----
            BuildHud(cam);

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

        // ==================== UI PASS (character menu / controls / hearts) ====================
        static UnityEngine.Camera UiCam() {
            var go = new GameObject("MainCamera");
            var cam = go.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true; cam.orthographicSize = 3.75f; cam.farClipPlane = 60f;
            go.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.13f, 0.18f, 0.14f); // deep PNW forest tone so UI pops
            cam.transform.position = new Vector3(0f, 3.75f, -10f);
            return cam;
        }

        static void BuildUiOnly() {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = UiCam();

            BuildHud(cam);
            BuildTouchDeck(); // also creates the EventSystem
            PinCanvasesToCam(cam);
            BuildCharacterMenu(cam);

            var outDir = System.Environment.GetEnvironmentVariable("QC_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = "QCShots";
            Directory.CreateDirectory(outDir);

            var rt = new RenderTexture(1334, 750, 24);
            cam.targetTexture = rt;
            // shot 1: character menu
            Snap(rt, System.IO.Path.Combine(outDir, "ui_menu.png"));
            // shot 2: HUD + control deck (menu dismissed)
            var menu = GameObject.Find("CharMenuCanvas");
            if (menu != null) Object.DestroyImmediate(menu);
            Snap(rt, System.IO.Path.Combine(outDir, "ui_hud.png"));
            Debug.Log("[ArtPass] UI pass QC shots done.");
        }

        /// <summary>Overlay canvases don't render into a camera texture — pin them to the cam.</summary>
        static void PinCanvasesToCam(UnityEngine.Camera cam) {
            foreach (var canvas in Object.FindObjectsOfType<UnityEngine.Canvas>()) {
                if (canvas.name == "CharMenuCanvas") continue; // menu builds its own camera-space canvas
                canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 10f;
            }
        }

        /// <summary>HUD: hearts row + wooden panel token counter, camera-pinned (world + UI paths share it).</summary>
        static void BuildHud(UnityEngine.Camera cam) {
            var heartArt = Art("art_heart.png");
            if (heartArt != null) {
                for (int i = 0; i < 3; i++)
                    SpriteGo("HUDHeart" + i, heartArt, Vector3.zero, 0.62f, 100, cam.transform)
                        .transform.localPosition = new Vector3(-5.9f + i * 0.75f, 3.2f, 10f);
            }
            var panelArt = Art("art_panel.png");
            if (panelArt != null)
                SpriteGo("HUDPanel", panelArt, Vector3.zero, 1.7f, 98, cam.transform)
                    .transform.localPosition = new Vector3(-4.35f, 3.2f, 10f);
            var tm = new GameObject("HUDCount").AddComponent<TextMesh>();
            tm.transform.SetParent(cam.transform, false);
            tm.transform.localPosition = new Vector3(-4.35f, 3.2f, 10f);
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.fontSize = 48; tm.characterSize = 0.16f; tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(0.10f, 0.06f, 0.02f);
            tm.text = "0 / 18";
        }

        /// <summary>Character select: native uGUI screen — three Lil Foot cards, tap to pick + start.</summary>
        static void BuildCharacterMenu(UnityEngine.Camera cam) {
            var go = new GameObject("CharMenuCanvas");
            var canvas = go.AddComponent<UnityEngine.Canvas>();
            canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam; canvas.planeDistance = 10f;
            var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1334f, 750f);
            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var ctl = go.AddComponent<LilFoots.CharacterMenuController>();

            // dim backdrop
            var dim = MakeUi(go.transform, "Dim");
            dim.anchorMin = Vector2.zero; dim.anchorMax = Vector2.one;
            dim.sizeDelta = Vector2.zero;
            var dimImg = dim.gameObject.AddComponent<UnityEngine.UI.Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.55f);

            // title
            var title = MakeUi(go.transform, "Title");
            title.anchorMin = title.anchorMax = new Vector2(0.5f, 1f);
            title.pivot = new Vector2(0.5f, 1f); title.anchoredPosition = new Vector2(0f, -60f);
            title.sizeDelta = new Vector2(700f, 90f);
            var tt = title.gameObject.AddComponent<UnityEngine.UI.Text>();
            tt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tt.fontSize = 64; tt.alignment = TextAnchor.MiddleCenter; tt.color = new Color(1f, 0.92f, 0.55f);
            tt.text = "CHOOSE YOUR LIL FOOT";

            // three cards
            string[] names = { "LILY", "BUDDY", "EMMA" };
            string[] files = { "whole_lily.png", "whole_buddy.png", "whole_emma.png" };
            for (int i = 0; i < 3; i++) {
                float x = (i - 1) * 360f;
                var card = MakeUi(go.transform, "Card" + names[i]);
                card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
                card.anchoredPosition = new Vector2(x, 10f);
                card.sizeDelta = new Vector2(320f, 320f);
                var img = card.gameObject.AddComponent<UnityEngine.UI.Image>();
                var sprite = Art(files[i]);
                img.sprite = sprite; img.preserveAspect = true;
                var btn = card.gameObject.AddComponent<UnityEngine.UI.Button>();
                btn.transition = UnityEngine.UI.Selectable.Transition.Scale;
                string picked = names[i].ToLower();
                btn.onClick.AddListener(() => ctl.Select(picked));

                var label = MakeUi(go.transform, "Label" + names[i]);
                label.anchorMin = label.anchorMax = new Vector2(0.5f, 0.5f);
                label.anchoredPosition = new Vector2(x, -170f);
                label.sizeDelta = new Vector2(200f, 50f);
                var lt = label.gameObject.AddComponent<UnityEngine.UI.Text>();
                lt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                lt.fontSize = 44; lt.alignment = TextAnchor.MiddleCenter; lt.color = Color.white;
                lt.text = names[i];
            }
            Debug.Log("[ArtPass] Character menu built: LILY / BUDDY / EMMA, tap to select.");
        }

        static UnityEngine.RectTransform MakeUi(Transform parent, string name) {
            var go = new GameObject(name, typeof(UnityEngine.RectTransform));
            var rt = (UnityEngine.RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        /// <summary>Full scene pipeline without exiting — also used by the APK build runner.</summary>
        public static void BuildAndShootCore() {
            // UI PASS MODE (Bude: character menu + control buttons + hearts must match the HQ art):
            // builds ONLY the UI over a neutral backdrop — no world layers, so it never collides
            // with the layer-by-layer map review.
            if (System.Environment.GetEnvironmentVariable("LILFOOTS_UI") == "1") { BuildUiOnly(); return; }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            LilFootsLevelBuilder.Build();
            BuildArt();
            BuildTouchDeck(); // MOBILE CONTROL DECK — the Sept 18 playability fix (Bude: "this isn't playable")
            BuildCharacterMenu(GameObject.Find("MainCamera").GetComponent<UnityEngine.Camera>()); // UI PASS: character select at start
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

            // gameplay QC shots: dismiss the character menu (it's reviewed in UI-pass builds)
            var menu = GameObject.Find("CharMenuCanvas");
            if (menu != null) Object.DestroyImmediate(menu);
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
