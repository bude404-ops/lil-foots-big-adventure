#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEditor.Animations;

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

        /// <summary>Re-skin a builder placeholder: swap in the real art, keep the name so the smoke
        /// gate finds the same object in greybox AND art builds.</summary>
        static void Reskin(string name, Sprite s, Vector3 pos, float width, int order, Transform parent) {
            var existing = GameObject.Find(name);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
            SpriteGo(name, s, pos, width, order, parent);
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

        /// <summary>Hound run waddle: a native AnimationClip (rock +/-5deg + slight squash,
        /// 10fps loop) on the HoundArt child, driven by a native AnimatorController on the hound.
        /// Unity performs the animation; this only authors the assets.</summary>
        static void HoundWaddle(GameObject hound) {
            Directory.CreateDirectory("Assets/Animation");
            var art = hound.transform.Find("HoundArt");
            if (art == null) return;
            var clipPath = "Assets/Animation/Hound_Waddle.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null) {
                clip = new AnimationClip { frameRate = 10 };
                var st = AnimationUtility.GetAnimationClipSettings(clip);
                st.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, st);
                float[] tilt = { 4f, -5f, 4f, -5f };
                var keys = new Keyframe[tilt.Length + 1];
                for (int i = 0; i < tilt.Length; i++) keys[i] = new Keyframe(i / 10f, tilt[i]);
                keys[tilt.Length] = new Keyframe(tilt.Length / 10f, tilt[0]);
                var b = EditorCurveBinding.FloatCurve("HoundArt", typeof(Transform), "localEulerAnglesRaw.z");
                AnimationUtility.SetEditorCurve(clip, b, new AnimationCurve(keys));
                AssetDatabase.CreateAsset(clip, clipPath);
                clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            }
            var ctrlPath = "Assets/Animation/Hound_Controller.controller";
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null) {
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
                var st = ctrl.layers[0].stateMachine.AddState("waddle");
                st.motion = clip;
                ctrl.layers[0].stateMachine.defaultState = st;
            }
            var anim = hound.GetComponent<Animator>();
            if (anim == null) anim = hound.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
        }

        /// <summary>Art child on a gameplay object — parent's colliders/rigidbody stay unscaled.</summary>
        static GameObject ChildSprite(GameObject parent, string name, Sprite s, float height, int order,
                                      bool flipX = false, float feetFrac = 0.5f) {
            var old = parent.GetComponent<SpriteRenderer>();
            if (old != null) Object.DestroyImmediate(old); // drop any placeholder renderer on the body
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.sortingOrder = order; sr.flipX = flipX;
            if (s != null) {
                float f = height / s.bounds.size.y;
                go.transform.localScale = new Vector3(f, f, 1f);
                // FEET-ANCHOR (Bude, Sept 19: "the terrain hides the character when actually running"):
                // the art's content is NOT centered in its frame - the character's feet sit
                // feetFrac above the sprite's bottom edge. Anchor the FEET to the parent origin
                // (the ground line) instead of the sprite center, so the character stands ON the
                // terrain instead of sinking into it behind the grass strip.
                go.transform.localPosition = new Vector3(0f, (0.5f - feetFrac) * s.bounds.size.y * f, 0f);
            }
            return go;
        }

        public static void BuildArt() {
            var map = GameObject.Find("MAP001");
            if (map == null) { Debug.LogError("[ArtPass] MAP001 not found — run Tools > Lil Foots > Build Map 001 first."); return; }
            var data = MiniJson.Deserialize(File.ReadAllText(MapDataPath())) as Dictionary<string, object>;
            float GY = GameManager.GroundY;

            // LAYER-BY-LAYER BUILD LAW (Bude, Sept 18 2026: "start over completely on the map in unity
            // and add one layer at a time... send me a image with one layer at a time as well and wait
            // for approved"): LILFOOTS_LAYERS=N builds only the first N layers; default 99 = all layers.
            int LN = 99;
            try { LN = System.Convert.ToInt32(System.Environment.GetEnvironmentVariable("LILFOOTS_LAYERS") ?? "99"); } catch { }
            bool L(int n) => LN >= n;
            Debug.Log("[ArtPass] LILFOOTS_LAYERS=" + LN + " (layer-by-layer review mode)");

            // ---- LAYER 1: NEW SKY BACKDROP (BudE, Sept 20: 'why are you using that old world
            // skin art I thought we were creating new ones with the new building system') - the
            // reference-painting world skin and the whole old stamp stack are RETIRED. Fresh
            // world skin art built for the block system: one clean overcast sky plate with a
            // distant ridge wash, camera-pinned (static backdrop law - never scrolls), mirrored
            // x2 so any aspect stays covered. Minimal layers per his verdict: sky + gameplay
            // plane. No middle-ground bands, no foreground props over the playfield. ----
            var camGo = GameObject.Find("MainCamera");
            var cam = camGo != null ? camGo.GetComponent<UnityEngine.Camera>() : null;
            if (cam != null && L(1)) {
                var skySpr = Art("art_sky_new.png");
                if (skySpr != null) {
                    float skyH = 12f;                 // frame is 7.5 tall - generous bleed top and bottom
                    float skyW = 12f;                  // square art, mirrored x2 = 24 wide for ultrawide
                    float sy = skyH / skySpr.bounds.size.y;
                    for (int i = 0; i < 2; i++) {
                        var sk = SpriteGo("SkyBackdrop", skySpr, Vector3.zero, skyW, -100, cam.transform);
                        sk.transform.localScale = new Vector3(sk.transform.localScale.x, sy, 1f);
                        sk.transform.localPosition = new Vector3(-6f + i * skyW, 1.0f, 10f);
                        if (i % 2 == 1) sk.GetComponent<SpriteRenderer>().flipX = true;
                    }
                } else {
                    Debug.LogError("[ArtPass] art_sky_new.png missing - fresh world skin art required");
                }
            }

            // MIST + WASH REMOVED (Bude, Sept 19 2026: 'remove the clouds and mist that layer 2
            // adds'). Layer 2 no longer spawns mist banks or the below-ground teal wash - the
            // vista depth base carries the below-ground atmosphere instead.

            // ---- STREAM WATER (PIECE 4, new PNW stream art pending BudE verdict - old
            // art_water.png deleted in the purge; block no-ops until the new art lands) ----
            var water = Art("art_stream.png");
            var plats = (List<object>)data["plats"];
            var sorted = plats.Cast<List<object>>()
                .Select(p => new float[] { F(p[0]), F(p[1]), F(p[2]), F(p[3]) })
                .OrderBy(a => a[0]).ToList();
            if (water != null && L(2)) {
                // MAP v2 (Bude, Sept 20 course redo): hop blocks now float OVER ground, so the
                // naive consecutive-plat gap can span solid earth. Subtract every ground-level
                // plat interval from the candidate gap and draw water only in the true pits.
                var grounds = sorted.Where(a => a[1] >= 600f)
                    .Select(a => new float[] { (a[0] - a[2] / 2f) / 100f, (a[0] + a[2] / 2f) / 100f })
                    .OrderBy(g => g[0]).ToList();
                for (int i = 0; i < sorted.Count - 1; i++) {
                    var a = sorted[i]; var b = sorted[i + 1];
                    float gapL = (a[0] + a[2] / 2f) / 100f, gapR = (b[0] - b[2] / 2f) / 100f;
                    if (gapR - gapL < 0.05f) continue;
                    var runs = new System.Collections.Generic.List<float[]> { new float[] { gapL, gapR } };
                    foreach (var g in grounds) {
                        var next = new System.Collections.Generic.List<float[]>();
                        foreach (var seg in runs) {
                            if (g[1] <= seg[0] || g[0] >= seg[1]) { next.Add(seg); continue; }
                            if (g[0] > seg[0]) next.Add(new float[] { seg[0], g[0] });
                            if (g[1] < seg[1]) next.Add(new float[] { g[1], seg[1] });
                        }
                        runs = next;
                    }
                    foreach (var seg in runs) {
                        float w = seg[1] - seg[0];
                        if (w < 0.3f || w > 7f) continue;
                        SpriteGo("Stream", water, new Vector3((seg[0] + seg[1]) / 2f, GY - 0.55f, 0), w + 0.6f, -60, map.transform);
                    }
                }
            }

            // ---- PLATFORM SKINS: real earth body + ground strip top (procedural slabs retired) ----
            var earth = Art("art_earth_new.png"); // NEW world skin (BudE: fresh art with the new building system)
            var strip = Art("art_grass_new.png"); // NEW world skin (BudE: fresh art with the new building system)
            foreach (Transform child in map.transform) {
                if (!child.name.StartsWith("Plat_")) continue;
                var bc = child.GetComponent<BoxCollider2D>();
                if (bc == null) continue;
                float w = bc.size.x, h = bc.size.y;
                float top = child.position.y + h / 2f;
                var oldSr = child.GetComponent<SpriteRenderer>();
                if (oldSr != null) Object.DestroyImmediate(oldSr); // no placeholder slabs in Unity

                // HOP BLOCKS (PIECE 2, BudE 'Keep' Sept 20): thin floaters (h < 2u) are NOT
                // ground - they wear the approved hop-block slab instead of earth + grass.
                var hopArt = Art("art_hopblock.png");
                if (hopArt != null && h < 2f) {
                    float hw = hopArt.bounds.size.x * (h / hopArt.bounds.size.y);
                    int hi = 0;
                    for (float x = child.position.x - w / 2f; x < child.position.x + w / 2f; x += hw) {
                        var hb = SpriteGo("HopBlockArt", hopArt, new Vector3(x, child.position.y, 0), hw, -2, child);
                        if (hi % 2 == 1) hb.GetComponent<SpriteRenderer>().flipX = true; // break the repeat
                        hi++;
                    }
                    continue;
                }

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

            }

            // ---- CAM TREES + TRAIL CAMS + HOUNDS + DRONES: PURGED (BudE, Sept 20: 'remove any
            // and delete all old work art skins' + enemy doctrine: v1 gadget/hound enemies retired,
            // Roster B creatures land at the M3 life pass; new art files deleted from the repo).

            // ---- LAYER 3 FOREGROUND PROPS REMOVED (Sept 19: Bude's world-skin reference
            // carries the near-field treatment itself - a continuous near-black forest floor
            // at the bottom edge, not discrete prop blobs. Fore props stay out until Bude
            // asks for garnish; the painting's own bottom band is the foreground depth). ----

            // ---- ENEMY ART: PURGED (see above) ----

            // ---- CHECKPOINT TOTEMS (PIECE 5, BudE 'Keep piece 5' Sept 21): approved mossy cedar
            // trail totem with glowing footprint emblem. Unlit = dim moss tint; the controller
            // brightens it to full color when the player claims it.
            var totem = Art("art_checkpoint.png");
            if (totem != null) {
                for (int ci = 0; ci < 64; ci++) {
                    var cp = GameObject.Find("Checkpoint_" + ci);
                    if (cp == null) break;
                    var sr = cp.AddComponent<SpriteRenderer>();
                    sr.sprite = totem; sr.sortingOrder = 1;
                    float cf = 1.7f / totem.bounds.size.y;
                    cp.transform.localScale = new Vector3(cf, cf, 1f);
                    sr.color = new Color(0.72f, 0.82f, 0.74f, 1f); // dim moss until claimed
                }
            }

            // ---- TOKENS (footprint Big Token) + secret heart ----
            // COINS BACK ON (Bude, Sept 20: "there are no tokens to collect"): the trail was
            // re-placed along the actual platform path (surface lines + arc bridges over the
            // gaps - no floaters), so the art goes back on at the map-data positions.
            var token = Art("art_token.png");
            foreach (Transform child in map.transform) {
                if (!child.name.StartsWith("Token_")) continue;
                var oldTa = child.transform.Find("TokenArt");
                if (oldTa != null) Object.DestroyImmediate(oldTa.gameObject); // no stale floaters
                if (token != null) ChildSprite(child.gameObject, "TokenArt", token, 0.46f, 5, false, 0.5f);
            }
            var heartArt = Art("art_heart.png");
            var sh = data.ContainsKey("secretHeart") ? data["secretHeart"] as Dictionary<string, object> : null;
            if (sh != null && heartArt != null && L(2)) {
                var hb = new GameObject("SecretHeart");
                hb.transform.SetParent(map.transform);
                hb.transform.position = new Vector3(F(sh["x"]) / 100f, 12.4f - F(sh["y"]) / 100f, 0); // canvas-y flip (2*GY - y)
                var hsr = hb.AddComponent<SpriteRenderer>(); hsr.sprite = heartArt; hsr.sortingOrder = 5;
                float hf = 0.55f / heartArt.bounds.size.y; hb.transform.localScale = new Vector3(hf, hf, 1f);
                var hc = hb.AddComponent<CircleCollider2D>(); hc.isTrigger = true; hc.radius = 0.5f;
                hb.AddComponent<SecretHeartPickup>();
            }

            // ---- FINISH: flagpole gate + portal (real props) ----
            var gate = GameObject.Find("Gate");
            if (gate != null && L(2)) {
                var fgArt = Art("art_flaggate.png");
                if (fgArt != null) Reskin("FlagGateArt", fgArt, new Vector3(gate.transform.position.x, GY + 1.2f, 0), 2.2f, 4, map.transform);
                var portal = Art("art_flagportal.png");
                if (portal != null) Reskin("PortalArt", portal, new Vector3(gate.transform.position.x - 1.4f, GY + 1.6f, 0), 3.2f, 3, map.transform);
            }

            // ---- PLAYER: selected Lil Foot, real art on a child sprite (capsule collider untouched) ----
            var lily = GameObject.Find("Lily");
            if (lily != null && L(2)) {
                // RIGGED PLAYER (Bude, Sept 20: "the characters pose still is the t pose and no
                // animations"): all three Lil Foots spawn as FULL NATIVE RIGS (SpriteSkin + bones
                // + Animator with idle/walk/jump) - the T-pose art never renders bare; the baked
                // arms-down idle stance is the rest pose, PlayerAnimBridge drives live animation.
                // Static whole-sprite is only the fallback if rigging fails.
                bool rigged = false;
                try {
                    var activeRig = RigPass.BuildPlayerRigs(lily, LilFoots.CharacterMenuController.Current());
                    rigged = activeRig != null;
                } catch (System.Exception e) { Debug.LogWarning("[ArtPass] player rig failed, static art: " + e.Message); }
                if (!rigged) {
                    string selChar = LilFoots.CharacterMenuController.Current();
                    string selFile = selChar == "buddy" ? "whole_buddy.png"
                                   : selChar == "emma" ? "whole_emma.png"
                                   : "whole_lily.png";
                    var selArt = Art(selFile);
                    float feetFrac = selChar == "buddy" ? 0.165f : selChar == "emma" ? 0.079f : 0.071f;
                    if (selArt != null) ChildSprite(lily, "PlayerArt", selArt, 0.82f, 10, false, feetFrac);
                }
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
            var btnR = Art("art_btnR.png"); // dedicated right-button art (BudE, Sept 20: 'missing right button in the art') - the runtime localScale mirror never showed on device
            var btnJ = Art("art_btnJ.png");
            if (btnL != null && btnR != null && btnJ != null) {
                MakeDeckButton(canvasGo.transform, "BtnLeft",  btnL, false, TouchDeckButton.Kind.Left,
                    new Vector2(120f, 90f), new Vector2(150f, 150f));
                MakeDeckButton(canvasGo.transform, "BtnRight", btnR, false, TouchDeckButton.Kind.Right,
                    new Vector2(300f, 90f), new Vector2(150f, 150f));
                // JUMP bigger + pulled inward (Sept 19: "jumping doesn't work" on the phone) -
                // taps at the extreme screen edge can land on browser chrome, not the canvas.
                MakeDeckButton(canvasGo.transform, "BtnJump",  btnJ, false, TouchDeckButton.Kind.Jump,
                    new Vector2(1120f, 100f), new Vector2(230f, 230f));
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
            // 2.0 DOCTRINE: every forge pass play-tests the scene it just saved. Red = Exit(2)
            // (the runners already gate their own builds; this covers QC/layer passes).
            if (!LilFootsSmokeTest.Run("smoke-report.json")) {
                Debug.Log("[ArtPass] SMOKE RED - pass fails, scene withheld.");
                EditorApplication.Exit(2); return;
            }
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
                // QC-only pin: overlay canvases (menu included since the Sept 19 rewrite) don't
                // render into a camera RT - pin them all for the UI-pass shots. Runtime builds
                // (map pass) never call this: there the menu stays ScreenSpaceOverlay.
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

        /// <summary>Character select (Bude, Sept 19: "It doesn't let me actually pick a character on the
        /// select" + "I thought we were going to use their idle pose in the character select").
        /// REWRITE - pick bug + idle law in one pass:
        /// 1) EventSystem guaranteed BEFORE anything is clickable (uGUI is dead input without it).
        /// 2) ScreenSpaceOverlay canvas at sortingOrder 100 - the menu is modal and renders above the
        ///    touch deck; no camera-plane math (the old ScreenSpaceCamera canvas sat at the exact
        ///    depth plane as the gameplay art - removed from the equation entirely).
        /// 3) Cards pick on POINTER DOWN (CharacterCard) - instant response on touch AND mouse, no
        ///    click-release dependency; Button kept with targetGraphic for visible tint feedback.
        /// 4) Cards show the LIVE IDLE RIGS: each character fully rigged (SpriteSkin + Animator,
        ///    idle clip default) on an off-map stage rendered into one shared RenderTexture - the
        ///    select law is ANIMATED idle, not a static portrait. Static-sprite fallback if the
        ///    rig stage can't build.</summary>
        static void BuildCharacterMenu(UnityEngine.Camera cam) {
            // 1) EventSystem first - but check the SCENE, not EventSystem.current (current is
            // null in edit mode even when one exists; a duplicate EventSystem breaks uGUI input
            // stability at runtime - the Sept 19 jump-button suspect).
            if (UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null) {
                var esGo = new GameObject("CharMenuEventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            var go = new GameObject("CharMenuCanvas");
            var canvas = go.AddComponent<UnityEngine.Canvas>();
            canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // modal: above the touch deck, above everything
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

            // ---- IDLE RIG STAGE (select law): 3 rigged characters on an off-map stage,
            // one shared RenderTexture; each card shows its third. Fails soft to static art. ----
            RenderTexture idleRt = null;
            try {
                idleRt = BuildIdleStage(out var stageRoot);
                ctl.idleStageTexture = idleRt;
                ctl.idleStageRoot = stageRoot; // SCENE-ROOT stage: world objects parented under an
                // overlay canvas inherit its UI transform (scale/position), which put the stage
                // camera in empty space - the cards rendered bind pose (T-pose), Sept 19.
            }
            catch (System.Exception e) { Debug.LogWarning("[ArtPass] Idle rig stage failed, static cards: " + e.Message); }

            // three cards
            string[] names = { "LILY", "BUDDY", "EMMA" };
            // fallback = RECREATED standing art (Sept 20: whole_* is now the T-pose rig art -
            // a failed rig stage must never show T-pose on the cards)
            string[] files = { "recreated_lily.png", "recreated_buddy.png", "recreated_emma.png" };
            var panelArt = Art("art_panel.png");
            for (int i = 0; i < 3; i++) {
                float x = (i - 1) * 360f;
                var card = MakeUi(go.transform, "Card" + names[i]);
                card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
                card.anchoredPosition = new Vector2(x, 10f);
                card.sizeDelta = new Vector2(320f, 320f);

                // cedar card backing (visual frame only, raycast-passthrough)
                if (panelArt != null) {
                    var back = MakeUi(card, "Backing");
                    back.anchorMin = Vector2.zero; back.anchorMax = Vector2.one;
                    back.offsetMin = back.offsetMax = Vector2.zero;
                    var bImg = back.gameObject.AddComponent<UnityEngine.UI.Image>();
                    bImg.sprite = panelArt; bImg.preserveAspect = true;
                    bImg.raycastTarget = false;
                }

                // portrait: live idle rig (RenderTexture third) or static art fallback
                var portrait = MakeUi(card, "Portrait");
                portrait.anchorMin = portrait.anchorMax = new Vector2(0.5f, 0.5f);
                portrait.sizeDelta = new Vector2(280f, 280f);
                UnityEngine.UI.Graphic face;
                if (idleRt != null) {
                    var raw = portrait.gameObject.AddComponent<UnityEngine.UI.RawImage>();
                    raw.texture = idleRt;
                    raw.uvRect = new UnityEngine.Rect(i / 3f, 0f, 1f / 3f, 1f);
                    face = raw;
                } else {
                    var img = portrait.gameObject.AddComponent<UnityEngine.UI.Image>();
                    img.sprite = Art(files[i]); img.preserveAspect = true;
                    face = img;
                }

                // pick: POINTER DOWN for instant touch/mouse response + Button for visible feedback
                var cc = card.gameObject.AddComponent<LilFoots.CharacterCard>();
                cc.menu = ctl; cc.character = names[i].ToLower();
                var btn = card.gameObject.AddComponent<UnityEngine.UI.Button>();
                btn.targetGraphic = face;
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
            Debug.Log("[ArtPass] Character menu built: LILY/BUDDY/EMMA as live idle rigs ("
                + (idleRt != null ? "RenderTexture stage" : "STATIC FALLBACK")
                + "), overlay canvas order 100, pick on pointer-down.");
        }

        /// <summary>Off-map stage holding the three fully-rigged characters (idle default state)
        /// plus one ortho camera rendering them into a shared 1536x512 RenderTexture (transparent
        /// background). Parented under the menu so it dies with the menu. Renders once immediately
        /// so the cards have content on frame one (and in editor QC); at runtime the Animator
        /// drives the idle clip and the camera re-renders every frame = animated select cards.</summary>
        static RenderTexture BuildIdleStage(out GameObject stageRoot) {
            stageRoot = null;
            // STAGE NAMING LAW (run 35469967904 red): the stage clones must NOT share names with
            // gameplay objects. GameObject.Find("Lily") is ambiguous with a stage copy present -
            // the smoke gate grabbed the stage copy (no rb/capsule/PlayerController) and went red.
            string[] names = { "LilyStage", "BuddyStage", "EmmaStage" };
            string[] files = { "whole_lily.png", "whole_buddy.png", "whole_emma.png" };
            for (int i = 0; i < 3; i++)
                if (!System.IO.File.Exists(System.IO.Path.Combine("Assets/Art", files[i])))
                    return null; // art missing -> caller falls back to static cards

            // SCENE-ROOT stage, NOT under the menu canvas: an overlay canvas drives its own
            // transform and world objects parented under it inherit UI scale - the stage camera
            // then renders empty space and the cards show bind pose (the Sept 19 T-pose bug).
            // The menu controller destroys this root + the RT when the pick happens.
            var stage = new GameObject("CharIdleStage");
            stageRoot = stage;
            for (int i = 0; i < 3; i++) {
                var root = RigPass.BuildStageRig(names[i], "Assets/Art/" + files[i],
                    new Vector3((i - 1) * 3f, -60f, 0f)); // off-map band, far below any gameplay
                root.transform.SetParent(stage.transform, true);
            }

            var rt = new RenderTexture(1536, 512, 24, RenderTextureFormat.ARGB32);
            var camGo = new GameObject("IdleStageCam");
            camGo.transform.SetParent(stage.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 1.6f; // stage band -61.6..-58.4; rigs 2.4 tall centered at -60
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // transparent over the dim
            cam.transform.position = new Vector3(0f, -60f, -10f);
            cam.targetTexture = rt;
            cam.Render(); // immediate content for frame one + editor QC

            // BUILD-TIME CONTENT CHECK: verify the stage camera actually frames the characters.
            // Read the RT and measure alpha coverage in each card third - if a third is empty the
            // camera is pointed wrong and the cards would ship blank: fall back to static art.
            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var probe = new Texture2D(1536, 512, TextureFormat.RGBA32, false);
            probe.ReadPixels(new Rect(0, 0, 1536, 512), 0, 0);
            probe.Apply();
            bool coverageOk = true;
            var px = probe.GetPixels32();
            for (int c = 0; c < 3; c++) {
                int hit = 0, n = 0;
                for (int y = 32; y < 480; y += 8)
                    for (int x = c * 512 + 64; x < (c + 1) * 512 - 64; x += 8) {
                        n++;
                        if (px[y * 1536 + x].a > 32) hit++;
                    }
                float cov = 100f * hit / Mathf.Max(1, n);
                Debug.Log("[ArtPass] Idle stage card " + names[c] + " coverage: " + cov.ToString("F1") + "%");
                if (cov < 3f) coverageOk = false;
            }
            RenderTexture.active = prevActive;
            Object.DestroyImmediate(probe);
            if (!coverageOk) {
                Debug.LogWarning("[ArtPass] Idle stage camera framed nothing - static card fallback.");
                Object.DestroyImmediate(stage);
                Object.DestroyImmediate(rt);
                stageRoot = null;
                return null;
            }
            Debug.Log("[ArtPass] Idle rig stage built + coverage verified -> shared RT.");
            return rt;
        }

        static UnityEngine.RectTransform MakeUi(Transform parent, string name) {
            var go = new GameObject(name, typeof(UnityEngine.RectTransform));
            var rt = (UnityEngine.RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        /// <summary>Full scene pipeline without exiting — also used by the APK build runner.</summary>
        // MAP_DATA accepts a bare filename or a full path - same law as LevelBuilder.DataPath.
        static string MapDataPath() {
            var v = System.Environment.GetEnvironmentVariable("MAP_DATA");
            if (string.IsNullOrEmpty(v)) v = "map001.json";
            if (!v.Contains("/")) v = "Assets/LevelData/" + v;
            return v;
        }

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
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Scenes/" + (System.Environment.GetEnvironmentVariable("MAP_SCENE") ?? "Map001") + ".unity");
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
