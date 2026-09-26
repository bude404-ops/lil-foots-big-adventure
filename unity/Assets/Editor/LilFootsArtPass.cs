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

        static Sprite ArtCrop(string file, float worldW, float worldH, float worldX0) {
            // EXACT-FIT + FLOW (BudE, Sept 21 'properly sized' + 'landscape meshing and flowing'):
            // art is sampled at a CONSISTENT world scale (th px per worldH units) and the crop
            // window is keyed to the plat's world x, so neighboring platforms sample adjoining
            // texture - the whole map reads as one continuous surface instead of random patches.
            var full = Art(file);
            if (full == null) return null;
            int tw = full.texture.width, th = full.texture.height;
            float pxPerUnit = th / worldH;                     // consistent world scale
            int cw = Mathf.RoundToInt(worldW * pxPerUnit);
            if (cw > tw) cw = tw;                              // very wide plat: full art width
            int maxX = Mathf.Max(0, tw - cw);
            int ox = maxX == 0 ? 0 : (int)(worldX0 * pxPerUnit) % maxX;  // world-x flow
            var rect = new Rect(ox, 0, cw, th);
            return Sprite.Create(full.texture, rect, new Vector2(0.5f, 0.5f), pxPerUnit);
        }

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
            var smeta = data.ContainsKey("meta") ? data["meta"] as Dictionary<string, object> : null;
            string mapId = smeta != null && smeta.ContainsKey("map") ? (smeta["map"] as string) : "";
            bool isStory = (mapId == "r1_story");
            bool isFull = (mapId == "r1_full");   // ONE COMPLETE MAP (BudE Sept 21 ~2:30 PM ET)
            bool isLong = (mapId == "r1_long");   // FULL-LENGTH 128u variant (Sept 21 verdict: length + depth)
            bool isEpic = (mapId == "r1_epic");   // 256u FULL JOURNEY (Sept 21 ~4:40 PM ET: 5-minute map + ground-fix verdict)
            bool isDt = (mapId == "r1_depth_test");   // GEOMETRY-DEPTH TEST map: the 8000x1560 painting IS the world (v12 skin)
            // [TILEMAP PIVOT Sept 25, take 2 - Bude: "build the geometry side in Unity and skin it
            // in Unity"]. The one-painting canvas bind is RETIRED for r1_depth_test: the map runs the
            // STANDARD native stack (real colliders, Unity Tilemap terrain skin via
            // LilFootsTilemapSkin, bound props/tokens/character) like every other course. LILFOOTS_TILEMAP=0
            // restores the retired v12 painting bind.
            bool isTile = isDt && (System.Environment.GetEnvironmentVariable("LILFOOTS_TILEMAP") ?? "1") != "0";
            if (isTile) isDt = false;
            var camGo = GameObject.Find("MainCamera");
            var cam = camGo != null ? camGo.GetComponent<UnityEngine.Camera>() : null;
            if (cam != null && L(1) && !isFull && !isLong && !isEpic && !isDt) {   // [FULL MAP] the painting is the sky
                // [UNITY-BUILT SKIN Sept 25 ~11:45 PM ET: "remove ant art work and just have
                // unity build it for the maps"] tilemap maps wear the UNITY-GENERATED sky
                // (LilFootsProcTiles: sage overcast gradient + forest ridge silhouettes) -
                // painted sky PNGs are retired from the map build.
                var skySpr = isTile ? LilFootsProcTiles.EnsureSky() : Art("art_sky_new.png");
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


            // ---- LAYER 1.1: ZONE BACKDROPS (BudE, Sept 21 ~1:30 PM ET verdict: 'it just all
            // so cut and pasted in and as a long reel instead of a real map feel'): the map
            // reads as FOUR PAINTED SCENES, not tiled strips - Mossveil Meadow, Cedar Rise,
            // Fern Hollow, Old Growth Gate. Each zone backdrop is ONE large painting covering
            // its zone (world-anchored, no parallax so seams stay fixed), stretched to the
            // full 14u frame height. Supersedes the mirrored-tile backdrop approach. ----
            // ---- LAYER 1.15: STORY PAINTING (BudE, Sept 21 ~2:15 PM ET: 'build a new one
            // that [is] all one solid map and painting but having the flow of a story as the
            // character runs and jumps around for that map'): for the story course the ENTIRE
            // backdrop is ONE continuous painting with the narrative arc baked in left-to-right
            // (meadow awakening -> fern hollow -> the climb -> old-growth finale). ----
            if (isFull) {
                // ---- ONE COMPLETE FULL NEW MAP (BudE, Sept 21 ~2:30 PM ET: 'stop adding the
                // blocks and pieces in, its not turning out like im wanting i need to to generate
                // one comlete full new map'): the ENTIRE level is ONE generated painting
                // (art_fullmap.png) - terrain, ledges, stream, cedar, gate and portal are all
                // PAINTED IN. Colliders are invisible and follow the painted terrain. No band
                // slices, no cap/edge tiles, no hop-block art, no zone stamps, no god rays, no
                // props, no gate/portal sprites pasted on top - nothing assembled from pieces.
                // Calibration (painted row -> world y): meadow surface row 400 -> 6.2, stream
                // water row 504 -> 3.5, summit surface row 195 -> 11.5. Vertical scale
                // 0.02585 u/px, painting bottom edge anchored at world y 3.2. ----
                var fullArt = Art("art_fullmap.png");
                if (fullArt != null) {
                    float fw = F(smeta["width"]) / 100f;
                    float ph = 516f * 0.02585f;                    // 13.34u of painted world
                    var sg = SpriteGo("FullMapPainting", fullArt, new Vector3(fw / 2f, 3.2f + ph / 2f, 0), fw, -95, map.transform);
                    var ssr2 = sg.GetComponent<SpriteRenderer>();
                    ssr2.color = Color.white;                       // the painting is the world: no suppression tint
                    float fx = sg.transform.localScale.x;
                    sg.transform.localScale = new Vector3(fx, ph / (fullArt.bounds.size.y), 1f); // independent vertical fit
                } else {
                    Debug.LogError("[ArtPass] art_fullmap.png missing - the full map IS the art");
                }
                if (isLong) {
                    // [LONG MAP] two continuous halves of ONE world: A (meadow -> canyon -> dark forest,
                    // full-frame 14u) and B (cliffs -> cedar climb -> boughs -> gorge -> gate, 0.01569
                    // u/row, summit row 134 -> 11.5). Seam hides dark-forest-on-dark-cliff.
                    var artA = Art("art_long_a.png");
                    var artB = Art("art_long_b.png");
                    if (artA != null) {
                        var gA = SpriteGo("LongMap_A", artA, new Vector3(32f, 7f, 0), 64f, -95, map.transform);
                        float ax = gA.transform.localScale.x;
                        gA.transform.localScale = new Vector3(ax, 14f / artA.bounds.size.y, 1f);
                    }
                    if (artB != null) {
                        float bh = 1024f * 0.01569f;
                        var gB = SpriteGo("LongMap_B", artB, new Vector3(96f, 13.6f - bh / 2f, 0), 64f, -95, map.transform);
                        float bx = gB.transform.localScale.x;
                        gB.transform.localScale = new Vector3(bx, bh / artB.bounds.size.y, 1f);
                    }
                    var wArt = Art("art_stream.png");
                    if (wArt != null) {
                        var s1 = SpriteGo("Stream", wArt, new Vector3(39.8f, 3.8f, 0), 3.0f, -60, map.transform);
                        s1.AddComponent<StreamFlow>().flowLeft = true;
                        var s2 = SpriteGo("Stream", wArt, new Vector3(99.0f, 3.5f, 0), 3.6f, -60, map.transform);
                        s2.AddComponent<StreamFlow>().flowLeft = false;
                    }
                }
                if (isEpic) {
                    // [EPIC MAP] four continuous strips of ONE 256u world.
                    // A: baked full-frame, meadow surface -> world 6.2 (asset row 620 = canvas).
                    // B2/C: full-frame 12.4u (v 0.01211 u/row). D: 639 rows at 0.0229 u/row (60 sky rows
                    // added on top, 150 mass rows at bottom -> covers world -2.2..12.4).
                    var eA = Art("art_epic_a.png");
                    var eB = Art("art_epic_b.png");
                    var eC = Art("art_epic_c.png");
                    var eD = Art("art_epic_d.png");
                    if (eA != null) {
                        var gA = SpriteGo("Epic_A", eA, new Vector3(32f, 6.2f, 0), 64f, -95, map.transform);
                        gA.transform.localScale = new Vector3(64f / eA.bounds.size.x, 12.4f / eA.bounds.size.y, 1f);
                    }
                    if (eB != null) {
                        var gB = SpriteGo("Epic_B", eB, new Vector3(96f, 6.2f, 0), 64f, -95, map.transform);
                        gB.transform.localScale = new Vector3(64f / eB.bounds.size.x, 12.4f / eB.bounds.size.y, 1f);
                    }
                    if (eC != null) {
                        var gC = SpriteGo("Epic_C", eC, new Vector3(160f, 6.2f, 0), 64f, -95, map.transform);
                        gC.transform.localScale = new Vector3(64f / eC.bounds.size.x, 12.4f / eC.bounds.size.y, 1f);
                    }
                    if (eD != null) {
                        float dH = 639f * 0.0229f;                                  // 14.63u
                        var gD = SpriteGo("Epic_D", eD, new Vector3(224f, 12.4f - dH / 2f, 0), 64f, -95, map.transform);
                        gD.transform.localScale = new Vector3(64f / eD.bounds.size.x, dH / eD.bounds.size.y, 1f);
                    }
                    var wArt2 = Art("art_stream.png");
                    if (wArt2 != null) {
                        var st1 = SpriteGo("Stream", wArt2, new Vector3(39.8f, 2.22f, 0), 3.0f, -60, map.transform);
                        st1.AddComponent<StreamFlow>().flowLeft = true;    // canyon of the stream
                        var st2 = SpriteGo("Stream", wArt2, new Vector3(94.5f, 4.02f, 0), 3.5f, -60, map.transform);
                        st2.AddComponent<StreamFlow>().flowLeft = false;  // rapids high
                        var st3 = SpriteGo("Stream", wArt2, new Vector3(99.0f, 2.1f, 0), 3.5f, -60, map.transform);
                        st3.AddComponent<StreamFlow>().flowLeft = false; // rapids low
                        var st4 = SpriteGo("Stream", wArt2, new Vector3(142.5f, 1.5f, 0), 4.5f, -60, map.transform);
                        st4.AddComponent<StreamFlow>().flowLeft = true;    // riverbend stones
                        var st5 = SpriteGo("Stream", wArt2, new Vector3(157.5f, 1.2f, 0), 3.0f, -60, map.transform);
                        st5.AddComponent<StreamFlow>().flowLeft = false; // second crossing
                    }
                }
                // deep-earth fill below the painting's bottom edge (world 0..3.2): one solid,
                // not pieces - reads as the shadowed earth under the painted terrain mass.
                if (!isLong) {                 // [LONG MAP] A stretches the full 14u frame - no underfill
                var fillTex = new Texture2D(2, 2);
                var fillPx = fillTex.GetPixels();
                for (int fi = 0; fi < fillPx.Length; fi++) fillPx[fi] = new Color(0.10f, 0.12f, 0.07f, 1f);
                fillTex.SetPixels(fillPx); fillTex.Apply();
                var fillSpr = Sprite.Create(fillTex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
                float fwfill = F(smeta["width"]) / 100f;
                SpriteGo("FullMapUnderfill", fillSpr, new Vector3(fwfill / 2f, 1.6f, 0), fwfill, -94, map.transform)
                    .transform.localScale = new Vector3(fwfill / 2f, 3.2f / 2f, 1f);
                }
            }
            else if (isDt) {
                // ---- DEPTH TEST (r1_depth_test, BudE Sept 21-22): the ONE painting is the world.
                // Canvas = world x100 (groundY 620 -> 6.2u, 1560 rows -> 15.6u, bottom row = y 0).
                // Only Lily, tokens, secret heart and invisible triggers render on top.
                var dtArt = Art("art_r1dt_skin_render.png");
                if (dtArt != null) {
                    float fw = F(smeta["width"]) / 100f;
                    float ph = 1560f / 100f;
                    var sg = SpriteGo("DepthTestPainting", dtArt, new Vector3(fw / 2f, ph / 2f, 0), fw, -95, map.transform);
                    var ssr = sg.GetComponent<SpriteRenderer>();
                    ssr.color = Color.white;                 // the painting is the world: no suppression tint
                    float fx = sg.transform.localScale.x;
                    sg.transform.localScale = new Vector3(fx, ph / dtArt.bounds.size.y, 1f);
                } else {
                    Debug.LogError("[ArtPass] art_r1dt_skin_render.png missing - the depth test map IS the art");
                }
            }
            else if (isStory) {
                var storyArt = Art("art_story_r1.png");
                if (storyArt != null) {
                    float sw = F(smeta["width"]) / 100f;
                    var sg = SpriteGo("StoryPainting", storyArt, new Vector3(sw / 2f, 7.0f, 0), sw, -95, map.transform);
                    var ssr = sg.GetComponent<SpriteRenderer>();
                    ssr.color = new Color(0.86f, 0.89f, 0.92f, 1f);  // suppressed backdrop doctrine
                    sg.transform.localScale = new Vector3(sg.transform.localScale.x, (2.22f * 14f) / sw, 1f); // 2.22:1 art fit to the full 14u frame
                }
            } else {
            // [COURSE-SCENE Sept 21] one painting per COURSE (BudE: 'paint each entire
            // map as it should be to fit lore'): the 225u reel layout is retired - each
            // course picks ITS OWN full-width zone painting from its map id.
            // Lore: 1-1 Mossveil=z1 | 1-2 Fern Hollow=z3 | 1-3 Cedar Run=z2 | 1-4 Old Growth=z4
            var zmeta = data.ContainsKey("meta") ? data["meta"] as Dictionary<string, object> : null;
            float zmapW = zmeta != null && zmeta.ContainsKey("width") ? F(zmeta["width"]) / 100f : 62f;
            string zmid = zmeta != null && zmeta.ContainsKey("map") ? zmeta["map"].ToString() : "r1_1";
            int zcourse = 0; int zlast = 0;
            foreach (char zch in zmid) if (char.IsDigit(zch)) zlast = zch - '0';
            zcourse = zlast;
            string zoneArt = zcourse == 2 ? "art_zone_3.png" : zcourse == 3 ? "art_zone_2.png" : "art_zone_" + zcourse + ".png";
            var zoneDefs = new (string art, float cx, float w)[] {
                (zoneArt, zmapW / 2f, zmapW),   // ONE painting, full course width, full frame height
            };
            foreach (var zd in zoneDefs) {
                var zArt = Art(zd.art);
                if (zArt == null) continue;
                var zg = SpriteGo("Zone_" + zd.art, zArt, new Vector3(zd.cx, 7.0f, 0), zd.w, -95, map.transform);
                var zsr = zg.GetComponent<SpriteRenderer>();
                // 2:1 art scaled to full frame height 14u: localScale y = 14 / (w/2) = 28/w
                zg.transform.localScale = new Vector3(zg.transform.localScale.x, 28f / zd.w, 1f);
                // suppressed-backdrop doctrine: zones sit slightly dim/cool behind the play plane
                zsr.color = new Color(0.82f, 0.86f, 0.90f, 1f);
            }
            } // end non-story zone branch

            // ---- LAYER 1.5: MIDDLE GROUND (BudE, Sept 21 render verdict: 'the landscape
            // isn't meshing and flowing together... its all just floating in the sky instead
            // of a proper middle ground background and foreground'): misty PNW ridgeline band
            // between the sky and the gameplay plane, drifting at half camera speed. This
            // supersedes the Sept 20 'no middle-ground bands' minimal-layer rule as stated -
            // the stripped world read as floating blocks; depth comes back as ONE continuous
            // lore-native band (not the old stamp clutter). Art: art_midground.png, mirrored
            // double = seamless 16u tile. ----
            // [ZONE REBUILD Sept 21] midground band DISABLED - the four painted zone
            // backdrops carry their own atmospheric depth; a mirrored band tiled over them
            // was a main source of the 'cut and pasted reel' look.
            var midArt = (Sprite)null;
            if (midArt != null) {   // [ZONE REBUILD] band disabled - guarded so no null-sprite objects spawn
                int mt = 0;
                for (float bx = -48f; bx <= 360f; bx += 48f, mt++) {
                    var mg = SpriteGo("Midground_" + mt, midArt, new Vector3(bx, 5.5f, 0), 16f, -70, map.transform);
                    var sr = mg.GetComponent<SpriteRenderer>();
                    // DEPTH LIGHT (BudE 'looks like crap' verdict fix): background bands get
                    // dimmed + cooled so the play plane owns the frame (DKC suppressed-bg
                    // doctrine). Band height stays 8u: localScale y = 16*3/8*2... keep 2:1 art
                    // at HALF height so a 48u-wide tile keeps the same silhouette height.
                    sr.flipX = (mt % 2 == 1);
                    sr.color = new Color(0.52f, 0.58f, 0.62f, 1f);
                    var tr = mg.transform;
                    tr.localScale = new Vector3(tr.localScale.x, tr.localScale.y * 0.5f, 1f);
                    mg.AddComponent<ParallaxProp>().factor = 0.5f;
                }
            } // end midground guard

            // ---- LAYER 4: FOREGROUND (same verdict): near-black forest floor fringe along
            // the bottom edge, sweeping 1.3x so the near-field reads as close. Band top sits
            // below the streams (y=4.5) so it frames the bottom of the frame without covering
            // standing gameplay. ----
            var fgArt = Art("art_foreground.png");
            if (fgArt != null && L(4) && !isFull && !isLong && !isEpic && !isDt) {   // [FULL MAP] the painting carries the foreground
                int ft = 0;
                for (float bx = -16f; bx <= 200f; bx += 16f, ft++) {
                    var fg = SpriteGo("Foreground_" + ft, fgArt, new Vector3(bx, 3.0f, 0), 16f, 30, map.transform);
                    fg.transform.localScale = new Vector3(fg.transform.localScale.x, fg.transform.localScale.x * 3f / 8f, 1f);
                    if (ft % 2 == 1) fg.GetComponent<SpriteRenderer>().flipX = true;
                    fg.AddComponent<ParallaxProp>().factor = 1.3f;
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
            if (water != null && L(2) && !isLong && !isEpic && !isDt) {   // [LONG MAP] streams placed manually at the painted waterlines
                // MAP v2 (Bude, Sept 20 course redo): hop blocks now float OVER ground, so the
                // naive consecutive-plat gap can span solid earth. Subtract every ground-level
                // plat interval from the candidate gap and draw water only in the true pits.
                var grounds = sorted.Where(a => a[1] >= 600f || a[3] >= 400f) // [MAP OVERHAUL Sept 21] tall ground bodies at any height (plateau/hollow)
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
                        float waterY = isFull ? 3.5f : GY - 0.55f;   // [FULL MAP] stream drawn at the painted water line
                        var st = SpriteGo("Stream", water, new Vector3((seg[0] + seg[1]) / 2f, waterY, 0), w + 0.6f, -60, map.transform);
                        st.AddComponent<StreamFlow>().flowLeft = (seg[1] < 31f); // alive water: scrolls + bobs (BudE Sept 21 'flowing water... bring to life')
                    }
                }
            }

            // [MAP OVERHAUL Sept 21] BudE: 'generate and keep what you need to get a full
            // map going... ill judge when its all finished' — set pieces + light + surface life + pit
            // framing, DKC/Rayman/Ori doctrine. All lore-native Region 1, nothing covers gameplay.
            {
                var cpsList = new System.Collections.Generic.List<float>();
                if (data.ContainsKey("checkpoints"))
                    foreach (var c in (List<object>)data["checkpoints"]) cpsList.Add(F(c) / 100f);

                // ---- SET PIECE: Old Growth cedar giant (BudE 'Keep' msg 8870) — the Region 1
                // signature landmark, standing ON the raised plateau (x=94, surface 8.2). ----
                var cedar = Art("art_cedar_giant.png");
                if (cedar != null && L(2) && !isFull && !isLong && !isEpic && !isDt) {   // [FULL MAP] the cedar is painted into the map
                    float cw = 14f;
                    // [COMPACT COURSE FIX] the 62u Mossveil Meadow course has no plateau -
                    // the cedar stands as the terminus landmark behind the flag-gate approach.
                    // Long maps keep the plateau placement (94f, 8.2).
                    var meta = data.ContainsKey("meta") ? data["meta"] as Dictionary<string, object> : null;
                    float mapW = meta != null && meta.ContainsKey("width") ? F(meta["width"]) / 100f : 225f;
                    if (mapId == "r1_story") SpriteGo("CedarGiant", cedar, new Vector3(mapW - 2.5f, 11.8f + cw / 2f, 0), cw, -50, map.transform); // STORY MAP: cedar crowns the summit (surface 11.8) at the finale
                    else {
                        // [COURSE-SCENE] snap the cedar base to the real ground surface at its x
                        // (composed courses have no plateau - a hardcoded 8.2 base would float it)
                        float cedarX = mapW < 70f ? mapW - 8f : 94f;
                        float cedarBase = GY;
                        foreach (Transform child in map.transform) {
                            if (!child.name.StartsWith("Plat_")) continue;
                            var bcx = child.GetComponent<BoxCollider2D>();
                            if (bcx == null) continue;
                            float pl = child.position.x - bcx.size.x / 2f, pr = child.position.x + bcx.size.x / 2f;
                            if (cedarX >= pl && cedarX <= pr && bcx.size.y >= 400f) { cedarBase = child.position.y + bcx.size.y / 2f; break; }
                        }
                        SpriteGo("CedarGiant", cedar, new Vector3(cedarX, cedarBase + cw / 2f, 0), cw, -50, map.transform);
                    }
                }

                // ---- LIGHT PASS: golden god rays between sky and midground, slow drift. ----
                var rays = Art("art_godrays.png");
                if (rays != null && L(1) && !isFull && !isLong && !isEpic && !isDt) {   // [FULL MAP] light is baked into the painting
                    // [COMPACT COURSE FIX] rays distribute across the actual map width
                    var rmeta = data.ContainsKey("meta") ? data["meta"] as Dictionary<string, object> : null;
                    float mw = rmeta != null && rmeta.ContainsKey("width") ? F(rmeta["width"]) / 100f : 225f;
                    float[] rxs = { mw * 0.22f, mw * 0.48f, mw * 0.72f, mw * 0.92f };
                    for (int ri = 0; ri < rxs.Length; ri++) {
                        var ray = SpriteGo("GodRay_" + ri, rays, new Vector3(rxs[ri], 6.5f, 0), 11f, -80, map.transform);
                        var rsr = ray.GetComponent<SpriteRenderer>();
                        rsr.color = new Color(1f, 0.96f, 0.82f, 0.35f);
                        ray.AddComponent<ParallaxProp>().factor = 0.3f;
                        if (ri % 2 == 1) rsr.flipX = true;
                    }
                }

                // ---- SURFACE LIFE: sparse fern/tuft/stone props ON grass tops, world-x keyed,
                // never over gaps, edges, checkpoints or hop blocks (BudE: nothing covering gameplay). ----
                if (L(2) && !isFull && !isLong && !isEpic && !isDt) {   // [FULL MAP] the painted terrain already lives
                    var hopXs = new System.Collections.Generic.List<float>();
                    foreach (var a in sorted) if (a[3] < 400f) hopXs.Add(a[0] / 100f);
                    int pi = 0;
                    foreach (var g in sorted) {
                        if (!(g[1] >= 600f || g[3] >= 400f)) continue;   // grounds only
                        float l = (g[0] - g[2] / 2f) / 100f, r = (g[0] + g[2] / 2f) / 100f;
                        float surf = 2f * GY - g[1] / 100f;               // standing surface (canvas flip)
                        for (float x = l + 1.3f; x < r - 1.3f; x += 7f + (pi % 5), pi++) {
                            bool clear = true;
                            foreach (var cp in cpsList) if (Mathf.Abs(cp - x) < 2.0f) { clear = false; break; }
                            if (clear) foreach (var hx in hopXs) if (Mathf.Abs(hx - x) < 1.6f) { clear = false; break; }
                            if (!clear) continue;
                            var ps = Art("art_prop_" + (pi % 5) + ".png");
                            if (ps == null) continue;
                            float pw = 0.9f;
                            float ph = pw * ps.bounds.size.y / ps.bounds.size.x;
                            var pgo = SpriteGo("SurfaceProp_" + pi, ps, new Vector3(x, surf + ph / 2f - 0.05f, 0), pw, 1, map.transform);
                            if (pi % 2 == 1) pgo.GetComponent<SpriteRenderer>().flipX = true;
                        }
                    }
                }

                // ---- PIT FRAMING: root/rock lips hanging from both edges of every true pit. ----
                if (L(2) && !isFull && !isLong && !isEpic && !isDt) {   // [FULL MAP] the painted banks frame the pit
                    var gs2 = sorted.Where(a => a[1] >= 600f || a[3] >= 400f).OrderBy(a => a[0]).ToList();
                    int li = 0;
                    for (int i = 0; i < gs2.Count - 1; i++) {
                        var a = gs2[i]; var b = gs2[i + 1];
                        float gapL = (a[0] + a[2] / 2f) / 100f, gapR = (b[0] - b[2] / 2f) / 100f;
                        if (gapR - gapL < 0.9f || gapR - gapL > 5f) continue;   // frame real pits only
                        float sA = 2f * GY - a[1] / 100f, sB = 2f * GY - b[1] / 100f;
                        foreach (var side in new int[] { 0, 1 }) {
                            var lip = Art("art_pitlip_" + (li % 3) + ".png"); li++;
                            if (lip == null) continue;
                            float lw = 2.2f;
                            float lh = lw * lip.bounds.size.y / lip.bounds.size.x;
                            float lx = side == 0 ? gapL - 0.35f : gapR + 0.35f;
                            float ls = side == 0 ? sA : sB;
                            var lgo = SpriteGo("PitLip_" + li, lip, new Vector3(lx, ls - lh / 2f + 0.18f, 0), lw, -3, map.transform);
                            if (side == 1) lgo.GetComponent<SpriteRenderer>().flipX = true;
                        }
                    }
                }
            }

            // ---- PLATFORM SKINS: real earth body + ground strip top (procedural slabs retired) ----
            var earth = Art("art_earth_new.png"); // NEW world skin (BudE: fresh art with the new building system)
            // [ZONE REBUILD] old flat grass strip art RETIRED (BudE Sept 21: remove the old blocks and arts) - surface is the organic cap + edge system
            foreach (Transform child in map.transform) {
                if (!child.name.StartsWith("Plat_")) continue;
                var bc = child.GetComponent<BoxCollider2D>();
                if (bc == null) continue;
                float w = bc.size.x, h = bc.size.y;
                float top = child.position.y + h / 2f;
                var oldSr = child.GetComponent<SpriteRenderer>();
                if (oldSr != null) Object.DestroyImmediate(oldSr); // no placeholder slabs in Unity
                if (isFull || isLong || isEpic || isDt) continue;   // [FULL MAP] colliders are INVISIBLE - the painting is the terrain

                // HOP BLOCKS (PIECE 2, BudE 'Keep' Sept 20): thin floaters (h < 2u) are NOT
                // ground - they wear the approved hop-block slab instead of earth + grass.
                var hopArt = isTile ? null : Art("art_hopblock.png");   // [UNITY-BUILT Sept 25] tilemap hops wear the Unity slab, not painted art
                if (hopArt != null && h < 2f) {
                    var crop = ArtCrop("art_hopblock.png", w, h, child.position.x - w / 2f);
                    if (crop != null) SpriteGo("HopBlockArt", crop, new Vector3(child.position.x, child.position.y, 0), w, -2, child);
                    continue;
                }

                // ---- STORY TERRAIN (BudE, Sept 21 'one solid map and painting'): on the
                // story map every ground is a world-anchored slice of ONE continuous painted
                // band (art_terrain_r1: living moss fringe + rich earth in the same stroke)
                // - neighboring grounds sample adjoining texture, so the whole course reads
                // as one solid vein of earth. Cap/edge tiles are retired for this map. ----
                if (isStory && h >= 2f && L(2)) {
                    var band = ArtCrop("art_terrain_r1.png", w, h, child.position.x - w / 2f);
                    if (band != null) {
                        SpriteGo("StoryTerrain", band, new Vector3(child.position.x, top - h / 2f, 0), w, -2, child);
                        continue;                                   // story grounds: band ONLY, no cap/edge tiles
                    }
                }
                // [TILEMAP PIVOT] grounds wear the REAL Unity Tilemap skin (grass-cap row +
                // dirt fill at 1u cells, green canon) - the stretched band crops retire for
                // tile-skinned grounds. The grass-edge fringe below still binds above the
                // walking line, absorbing the 0.2u plateau tile offset organically.
                bool tiled = (h >= 2f && !isStory);
                if (earth != null && L(2) && !tiled) {
                    var crop = ArtCrop("art_earth_new.png", w, h, child.position.x - w / 2f);
                    if (crop != null) SpriteGo("Earth", crop, new Vector3(child.position.x, top - h / 2f, 0), w, -2, child);
                }
                // [ORGANIC GROUND Sept 21 - 'cut and pasted' fix] surface = ground cap band
                // (art_ground_cap, organic painted grass) + hanging edge silhouettes rising
                // above the walking line (art_grass_edge, flipped so the dense mass roots AT
                // the surface and blade tips taper upward). Both tile world-x phased at natural
                // scale so the pattern flows continuously across the map.
                var cap = Art("art_ground_cap.png");
                if (cap != null && L(2) && !tiled) {
                    float capH = 0.45f;
                    float cw2 = capH * (cap.bounds.size.x / cap.bounds.size.y);
                    float left = child.position.x - w / 2f;
                    float x = left - (left % cw2);
                    int ci = 0;
                    for (; x < child.position.x + w / 2f - 0.02f; x += cw2) {
                        var ct = SpriteGo("GroundCap", cap, new Vector3(x + cw2 / 2f, top - capH / 2f, 0), cw2, -1, child);
                        if (ci % 2 == 1) ct.GetComponent<SpriteRenderer>().flipX = true;
                        ci++;
                    }
                }
                var edge = Art("art_grass_edge.png");
                if (edge != null && L(2)) {
                    float edgeH = 0.35f;
                    float ew = edgeH * (edge.bounds.size.x / edge.bounds.size.y);
                    float left = child.position.x - w / 2f;
                    float x = left - (left % ew);
                    int ei = 0;
                    for (; x < child.position.x + w / 2f - 0.02f; x += ew) {
                        var et = SpriteGo("GrassEdge", edge, new Vector3(x + ew / 2f, top + 0.02f + edgeH / 2f, 0), ew, 0, child);
                        var esr = et.GetComponent<SpriteRenderer>();
                        esr.flipY = true;                       // dense mass roots at the walking line
                        if (ei % 2 == 1) esr.flipX = true;
                        ei++;
                    }
                }

            }

            // [TILEMAP PIVOT] paint the real Unity Tilemap terrain once - every ground's
            // footprint as 1u green-canon tiles (grass caps + dirt fill + exposed lips).
            if (isTile && LilFootsTilemapSkin.Ready()) LilFootsTilemapSkin.PaintGrounds(map);

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
            var totem = isTile ? LilFootsProcTiles.EnsureSprite("unity_totem") : Art("art_checkpoint.png");   // [CLEANSE Sept 26 ~1:36 AM ET] tilemap maps wear the clean Unity-built totem
            if (totem != null && !isDt) {
                for (int ci = 0; ci < 64; ci++) {
                    var cp = GameObject.Find("Checkpoint_" + ci);
                    if (cp == null) break;
                    // STANDING FIX (BudE, Sept 21: 'character inside the blocks and not on top
                    // of the grass'): the totem was centered on the cp trigger at the ground
                    // LINE, sinking it half into the earth. The art rides a child raised so its
                    // FEET sit ON the grass (top = GroundY), trigger stays put.
                    float cf = 3.4f / totem.bounds.size.y;   // [SCALE LAW Sept 21] totem 3.4u tall - world dwarfs the character (BudE: 'objects way bigger than the lil foots')
                    var to = new GameObject("TotemArt");
                    to.transform.SetParent(cp.transform, false);
                    to.transform.localPosition = new Vector3(0f, 1.7f, 0f); // feet on the grass
                    to.transform.localScale = new Vector3(cf, cf, 1f);
                    var sr = to.AddComponent<SpriteRenderer>();
                    sr.sprite = totem; sr.sortingOrder = 1;
                    sr.color = new Color(0.72f, 0.82f, 0.74f, 1f); // dim moss until claimed
                }
            }

            // ---- TOKENS (footprint Big Token) + secret heart ----
            // COINS BACK ON (Bude, Sept 20: "there are no tokens to collect"): the trail was
            // re-placed along the actual platform path (surface lines + arc bridges over the
            // gaps - no floaters), so the art goes back on at the map-data positions.
            var token = isTile ? LilFootsProcTiles.EnsureSprite("unity_coin") : Art("art_token.png");   // [CLEANSE Sept 26] tilemap tokens wear the clean Unity-built coin
            foreach (Transform child in map.transform) {
                if (!child.name.StartsWith("Token_")) continue;
                var oldTa = child.transform.Find("TokenArt");
                if (oldTa != null) Object.DestroyImmediate(oldTa.gameObject); // no stale floaters
                if (token != null) ChildSprite(child.gameObject, "TokenArt", token, 0.28f, 5, false, 0.5f); // [SCALE LAW Sept 21] 0.28u - Mario-coin size vs 0.82u Lily
            }
            var heartArt = isTile ? LilFootsProcTiles.EnsureSprite("unity_heart") : Art("art_heart.png");   // [CLEANSE Sept 26] tilemap heart is Unity-built
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
            if (gate != null && L(2) && !isFull && !isLong && !isEpic && !isDt) {   // [FULL MAP] flag + portal are painted into the terminus (trigger stays)
                var gateArt = Art("art_flaggate.png");   // renamed from fgArt - CS0136 collision with the Layer-4 foreground fgArt (same method scope)
                if (gateArt != null) Reskin("FlagGateArt", gateArt, new Vector3(gate.transform.position.x, GY + 3.0f, 0), 5.5f, 4, map.transform); // [SCALE LAW Sept 21] 6u-tall monumental flagpole (was 2.2) - base on the grass, towers over 0.82u Lily
                var portal = Art("art_flagportal.png");
                if (portal != null) Reskin("PortalArt", portal, new Vector3(gate.transform.position.x + 2.8f, GY + 3.25f, 0), 6.5f, 3, map.transform); // [SCALE LAW Sept 21] 6.5u tall, placed PAST the flag (flag-then-portal flow, was behind the flag at -1.4)
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
                // [DECK FIX Sept 25 ~11:45 PM ET: BudE "the left and right motion button they
                // are too close... the character doesnt always go that direction you want"]
                // (a) LEFT/RIGHT pushed apart + enlarged (centers 230px apart at the 1334 ref,
                //     ~68pt on a phone - real thumb separation, no accidental cross-taps).
                // (b) TouchDeckRoot owns the flags now: FRAME-POLLED hit zones (padded,
                //     bottom-extended, multi-touch + drag-through aware) - uGUI pointer events
                //     lose drags between buttons (OnPointerExit releases, nothing re-captures),
                //     which is exactly why the direction died mid-slide. The root never misses.
                var rL = MakeDeckButton(canvasGo.transform, "BtnLeft",  btnL, false, TouchDeckButton.Kind.Left,
                    new Vector2(115f, 90f), new Vector2(175f, 175f));
                var rR = MakeDeckButton(canvasGo.transform, "BtnRight", btnR, false, TouchDeckButton.Kind.Right,
                    new Vector2(345f, 90f), new Vector2(175f, 175f));
                // JUMP bigger + pulled inward (Sept 19: "jumping doesn't work" on the phone) -
                // taps at the extreme screen edge can land on browser chrome, not the canvas.
                var rJ = MakeDeckButton(canvasGo.transform, "BtnJump",  btnJ, false, TouchDeckButton.Kind.Jump,
                    new Vector2(1120f, 100f), new Vector2(230f, 230f));
                var root = canvasGo.AddComponent<TouchDeckRoot>();
                root.canvasRect = canvasGo.transform as UnityEngine.RectTransform;
                root.leftRect = rL; root.rightRect = rR; root.jumpRect = rJ;
            }
            Debug.Log("[ArtPass] Touch deck built: frame-polled LEFT/RIGHT/JUMP (TouchDeckRoot owns flags).");
        }

        static UnityEngine.RectTransform MakeDeckButton(Transform parent, string name, Sprite art, bool flip,
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
            return rt;
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

            // ---- STORY-WORLD LOADING/SELECT (BudE, Sept 21: 'change the character
            // loading screen so it matches what we are changing and doing'): the select
            // screen sits INSIDE the story painting - art_story_r1 fills the frame, dimmed
            // + cooled so the cards own the light; a light black veil keeps text contrast. ----
            var storyBg = Art("art_story_r1.png");
            if (storyBg != null) {
                var sbg = MakeUi(go.transform, "StoryBackdrop");
                sbg.anchorMin = Vector2.zero; sbg.anchorMax = Vector2.one;
                sbg.sizeDelta = Vector2.zero;
                var sbgImg = sbg.gameObject.AddComponent<UnityEngine.UI.Image>();
                sbgImg.sprite = storyBg; sbgImg.preserveAspect = false;
                sbgImg.color = new Color(0.55f, 0.60f, 0.55f, 1f); // dimmed + cooled story world
            }
            // dim veil (lighter now - the painting carries the depth)
            var dim = MakeUi(go.transform, "Dim");
            dim.anchorMin = Vector2.zero; dim.anchorMax = Vector2.one;
            dim.sizeDelta = Vector2.zero;
            var dimImg = dim.gameObject.AddComponent<UnityEngine.UI.Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.30f);

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
            var panelArt = Art("art_panel_story.png") ?? Art("art_panel.png");   // story-matched painted cedar panel
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
            // TILEMAP PIVOT (BudE green light Sept 25 2026: geometry-only into a new map,
            // skinned IN UNITY with Tilemap terrain): LILFOOTS_TILEMAP=1 routes the build to
            // the native-terrain builder and skips the old stretched-canvas painting pass.
            // [TAKE 2 Sept 25] the standard LevelBuilder + ArtPass stack builds the FULL art
            // (props, tokens, totems, gate, portal, character rig) - and BuildArt routes
            // r1_depth_test terrain to LilFootsTilemapSkin (isTile). The standalone placeholder
            // TilemapBuilder stays menu-only (Tools > Lil Foots > Build Tilemap Level).
            LilFootsLevelBuilder.Build();
            BuildArt();
            BuildTouchDeck(); // MOBILE CONTROL DECK — the Sept 18 playability fix (Bude: "this isn't playable")
            // RUN TIMER (BudE Sept 26: "lets go your recommendation on the incentives") — clock
            // + medals + best-time panel; GameManager.Win() freezes it at the flag.
            if (UnityEngine.Object.FindObjectOfType<RunTimer>() == null) new GameObject("RunTimer").AddComponent<RunTimer>();
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
