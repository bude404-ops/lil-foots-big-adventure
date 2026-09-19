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

            // ---- WORLD BACKDROP = BUDE'S EXACT REFERENCE ART (Sept 19 2026, imgur JAnDjxQ:
            // "I'll send a file that is suppose to be the reference art for the world skin").
            // THE REFERENCE IS THE WORLD SKIN: his 1024x1024 painting (pale sage sky wash ->
            // deepening forest -> near-black floor edge, one continuous gradient, no hard seams)
            // is used AS the backdrop, pixel for pixel - the map looks like the reference by
            // construction. Coverage: two mirrored tiles, each 8.1u wide x 7.5u tall (frame
            // height), seamless by mirror construction, <10% stretch. Camera-pinned (static
            // vista law). Mid band + foreground depth come from the painting itself. ----
            var camGo = GameObject.Find("MainCamera");
            var cam = camGo != null ? camGo.GetComponent<UnityEngine.Camera>() : null;
            if (cam != null && L(1)) {
                var skinSpr = Art("art_worldskin_ref.png");
                if (skinSpr != null) {
                    float skinH = 7.5f;   // exactly the ortho frame height
                    float skinW = 8.1f;   // two mirrored tiles cover the 16.2u view width
                    for (int i = 0; i < 2; i++) {
                        var sk = SpriteGo("WorldSkin", skinSpr, Vector3.zero, skinW, -100, cam.transform);
                        float sy = skinH / skinSpr.bounds.size.y;
                        sk.transform.localScale = new Vector3(sk.transform.localScale.x, sy, 1f);
                        sk.transform.localPosition = new Vector3(-skinW / 2f + i * skinW, 0f, 10f);
                        if (i == 1) sk.GetComponent<SpriteRenderer>().flipX = true;
                    }
                } else {
                    Debug.LogError("[ArtPass] art_worldskin_ref.png missing - copy Bude's reference into Assets/Art");
                }
            }

            // MIST + WASH REMOVED (Bude, Sept 19 2026: 'remove the clouds and mist that layer 2
            // adds'). Layer 2 no longer spawns mist banks or the below-ground teal wash - the
            // vista depth base carries the below-ground atmosphere instead.

            // ---- STREAM WATER in the gaps ----
            var water = Art("art_water.png");
            var plats = (List<object>)data["plats"];
            var sorted = plats.Cast<List<object>>()
                .Select(p => new float[] { F(p[0]), F(p[1]), F(p[2]), F(p[3]) })
                .OrderBy(a => a[0]).ToList();
            if (water != null && L(2)) {
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
                if (earth != null && L(2)) {
                    float ew = h * 0.94f * (earth.bounds.size.x / earth.bounds.size.y);
                    int ei = 0;
                    for (float x = child.position.x - w / 2f; x < child.position.x + w / 2f; x += ew) {
                        var et = SpriteGo("Earth", earth, new Vector3(x, top - h / 2f, 0), ew, -2, child);
                        if (ei % 2 == 1) et.GetComponent<SpriteRenderer>().flipX = true; // break the repeat
                        ei++;
                    }
                }
                if (strip != null && L(2)) {
                    float sw = strip.bounds.size.x * (0.62f / strip.bounds.size.y);
                    int si = 0;
                    for (float x = child.position.x - w / 2f; x < child.position.x + w / 2f - 0.05f; x += sw) {
                        var st = SpriteGo("GrassTop", strip, new Vector3(x, top - 0.28f, 0), sw, -1, child);
                        if (si % 2 == 1) st.GetComponent<SpriteRenderer>().flipX = true;
                        si++;
                    }
                }

                // ---- EDGE BREAKERS (Bude Sept 19 diagnosis: no ruler-straight layer seams).
                // Tufts rise through the grass line and moss fringe hangs under the lip so
                // the ground-to-forest seam goes organic, like the approved concept plate. ----
                var tufts = Art("art_tufts.png");
                if (tufts != null && L(2)) {
                    float tw2 = tufts.bounds.size.x * (0.30f / tufts.bounds.size.y);
                    int ti = 0;
                    for (float x = child.position.x - w / 2f + 0.4f; x < child.position.x + w / 2f; x += tw2 * 0.78f) {
                        var tt = SpriteGo("Tuft", tufts, new Vector3(x, top + 0.17f, 0), tw2, 3, child);
                        if (ti % 2 == 1) tt.GetComponent<SpriteRenderer>().flipX = true;
                        ti++;
                    }
                }
                var fringe = Art("art_fringe.png");
                if (fringe != null && L(2)) {
                    float fw3 = fringe.bounds.size.x * (0.42f / fringe.bounds.size.y);
                    int fi2 = 0;
                    for (float x = child.position.x - w / 2f; x < child.position.x + w / 2f; x += fw3 * 0.82f) {
                        var ft = SpriteGo("LipFringe", fringe, new Vector3(x, top - 0.73f, 0), fw3, 4, child);
                        if (fi2 % 2 == 1) ft.GetComponent<SpriteRenderer>().flipX = true;
                        fi2++;
                    }
                }
            }

            // ---- CAM TREES + trail cam art ----
            var camTree = Art("art_camtree.png");
            var trailcamArt = Art("art_trailcam.png");
            foreach (Transform child in map.transform) {
                if (!child.name.StartsWith("TrailCam")) continue;
                if (camTree != null && L(2)) {
                    // TREE HEIGHT (Bude, Sept 19 'layer 2 is too high'): 3.1u trees poked above
                    // the vista treeline; 2.3u keeps the canopy under the L1 backdrop line.
                    float cth = 2.3f; float ctw = cth * (camTree.bounds.size.x / camTree.bounds.size.y);
                    SpriteGo("CamTreeArt", camTree, new Vector3(child.position.x, GY - 0.55f + cth / 2f, 0), ctw, -6, map.transform);
                }
                if (trailcamArt != null && L(2)) ChildSprite(child.gameObject, "TrailCamArt", trailcamArt, 0.52f, 6);
            }

            // ---- LAYER 3 FOREGROUND PROPS REMOVED (Sept 19: Bude's world-skin reference
            // carries the near-field treatment itself - a continuous near-black forest floor
            // at the bottom edge, not discrete prop blobs. Fore props stay out until Bude
            // asks for garnish; the painting's own bottom band is the foreground depth). ----

            // ---- ENEMY ART (child sprites — hitboxes untouched) ----
            var hound = Art("art_hound.png");
            var drone = Art("art_drone.png");
            foreach (Transform child in map.transform) {
                if (child.name.StartsWith("Hound") && hound != null && L(2)) {
                    bool flip = child.GetComponent<HoundController>().dir < 0;
                    ChildSprite(child.gameObject, "HoundArt", hound, 0.62f, 6, flip);
                }
                if (child.name.StartsWith("Drone") && drone != null && L(2))
                    ChildSprite(child.gameObject, "DroneArt", drone, 0.55f, 6);
            }

            // ---- TOKENS (footprint Big Token) + secret heart ----
            // COINS OFF (Bude, Sept 19 2026: 'remove the coins from it because they dont make
            // sense in their positions'): token art stays OUT until the trail is re-placed
            // along the actual platform path (proper curve arcs over jumps, no floaters over
            // gaps). Token_ logic objects remain so collection still works when art returns.
            var token = Art("art_token.png");
            foreach (Transform child in map.transform) {
                if (!child.name.StartsWith("Token_")) continue;
                var oldTa = child.transform.Find("TokenArt");
                if (oldTa != null) Object.DestroyImmediate(oldTa.gameObject); // no stale floaters
            }
            var heartArt = Art("art_heart.png");
            var sh = data.ContainsKey("secretHeart") ? data["secretHeart"] as Dictionary<string, object> : null;
            if (sh != null && heartArt != null && L(2)) {
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
            if (gate != null && L(2)) {
                var fgArt = Art("art_flaggate.png");
                if (fgArt != null) SpriteGo("FlagGateArt", fgArt, new Vector3(gate.transform.position.x, GY + 1.2f, 0), 2.2f, 4, map.transform);
                var portal = Art("art_flagportal.png");
                if (portal != null) SpriteGo("PortalArt", portal, new Vector3(gate.transform.position.x - 1.4f, GY + 1.6f, 0), 3.2f, 3, map.transform);
            }

            // ---- PLAYER: selected Lil Foot, real art on a child sprite (capsule collider untouched) ----
            var lily = GameObject.Find("Lily");
            if (lily != null && L(2)) {
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
            go.tag = "MainCamera"; // Snap() renders via Camera.main - without the tag it's null (UI-pass NRE fix)
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
            // TOKEN BAR (Bude: 'the bar need to be moved over to the right not behind the hearts'):
            // top-right corner, mirroring the hearts row at top-left. Hearts stay top-left.
            var panelArt = Art("art_panel.png");
            if (panelArt != null)
                SpriteGo("HUDPanel", panelArt, Vector3.zero, 1.7f, 98, cam.transform)
                    .transform.localPosition = new Vector3(4.55f, 3.2f, 10f);
            var tm = new GameObject("HUDCount").AddComponent<TextMesh>();
            tm.transform.SetParent(cam.transform, false);
            tm.transform.localPosition = new Vector3(4.55f, 3.2f, 10f);
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // Bude verdicts: 'the numbers are behind the wood panel and too large' ->
            // MeshRenderer sorts 0 by default (panel = 98) so the count rendered BEHIND the
            // cedar panel; characterSize 0.16 made it ~0.77u tall on a ~0.5u panel.
            tm.fontSize = 48; tm.characterSize = 0.06f; tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(0.10f, 0.06f, 0.02f);
            tm.text = "0 / 18";
            tm.GetComponent<MeshRenderer>().sortingOrder = 101;  // in front of the panel (98)
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
                btn.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
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
            // DEPTH DOCTRINE FRAMING (Bude, Sept 19 'layer 2 is still too high on screen'):
            // camera rides 7.0 so the vista (sky/ridges/treeline/fir wall) owns the top ~60%
            // of the frame with the treeline at the middle, and the gameplay strip reads as
            // a crisp band in the lower ~40% - matching the game camera y (builder: 7.0).
            cam.transform.position = new Vector3(lily.transform.position.x + 2.5f, 7.0f, -10f);
            Snap(rt, System.IO.Path.Combine(outDir, "unity_start.png"));
            // shot 2: mid-map, hound + cam tree
            float hx = hound != null ? hound.transform.position.x : 45f;
            cam.transform.position = new Vector3(hx + 2.2f, 7.0f, -10f);
            Snap(rt, System.IO.Path.Combine(outDir, "unity_mid.png"));
            // shot 3: finish gate + flag + portal
            float gx = gate != null ? gate.transform.position.x : 86f;
            cam.transform.position = new Vector3(gx - 2.5f, 7.0f, -10f);
            Snap(rt, System.IO.Path.Combine(outDir, "unity_gate.png"));

            Debug.Log("[ArtPass] QC shots done.");
        }

        static void Snap(RenderTexture rt, string path) {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var c = Camera.main != null ? Camera.main : UnityEngine.Object.FindObjectOfType<UnityEngine.Camera>();
            if (c == null) { Debug.LogError("[ArtPass] No camera to render QC shot - skipping " + path); return; }
            c.Render();
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = prev;
        }
    }
}
#endif
