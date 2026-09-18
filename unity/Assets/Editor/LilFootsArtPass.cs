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

            // ---- WORLD BACKDROP (Region 1: PNW) ----
            var cam = GameObject.Find("MainCamera");
            // pinned sky + THE one sun ride with the camera (never scroll, never duplicate)
            if (cam != null) {
                SpriteGo("SkyPlate", Art("art_sky_pnw.png"), Vector3.zero, 16.2f, -100, cam.transform)
                    .transform.localPosition = new Vector3(0f, 0f, 10f);
                SpriteGo("TheSun", Art("art_sun.png"), Vector3.zero, 1.7f, -95, cam.transform)
                    .transform.localPosition = new Vector3(4.4f, 2.2f, 10f);
            }
            // Cascade ridges + snow-capped volcano strip along the whole level
            var ridges = Art("art_ridges.png");
            if (ridges != null)
                for (float x = -8f; x < 98f; x += 19f)
                    SpriteGo("Ridges", ridges, new Vector3(x, 4.6f, 0), 19f, -90, map.transform);
            // dense fir wall midground
            var firs = Art("art_firwall.png");
            if (firs != null)
                for (float x = -8f; x < 100f; x += 12f)
                    SpriteGo("FirWall", firs, new Vector3(x, GY - 1.9f, 0), 12f, -80, map.transform);
            // drifting PNW mist banks (soft sprites)
            var mist = Art("art_mist.png");
            if (mist != null) {
                float[] mx = { 6f, 30f, 62f, 88f };
                float[] my = { GY + 1.6f, GY + 2.6f, GY + 1.9f, GY + 2.8f };
                for (int i = 0; i < 4; i++) SpriteGo("MistBank", mist, new Vector3(mx[i], my[i], 0), 9f + (i % 2) * 3f, -70, map.transform);
            }

            // ---- STREAM WATER in the gaps ----
            var water = Art("art_water.png");
            var plats = (List<object>)data["plats"];
            var sorted = plats.Cast<List<object>>()
                .Select(p => new float[] { F(p[0]), F(p[1]), F(p[2]), F(p[3]) })
                .OrderBy(a => a[0]).ToList();
            if (water != null) {
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
                if (earth != null) {
                    float ew = h * 0.94f * (earth.bounds.size.x / earth.bounds.size.y);
                    for (float x = child.position.x - w / 2f; x < child.position.x + w / 2f; x += ew)
                        SpriteGo("Earth", earth, new Vector3(x, top - h / 2f, 0), ew, -2, child);
                }
                if (strip != null) {
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
                if (camTree != null)
                    SpriteGo("CamTreeArt", camTree, new Vector3(child.position.x, GY - 2.35f, 0), 5.6f, -6, map.transform);
                if (trailcamArt != null) ChildSprite(child.gameObject, "TrailCamArt", trailcamArt, 0.52f, 6);
            }

            // ---- ENEMY ART (child sprites — hitboxes untouched) ----
            var hound = Art("art_hound.png");
            var drone = Art("art_drone.png");
            foreach (Transform child in map.transform) {
                if (child.name.StartsWith("Hound") && hound != null) {
                    bool flip = child.GetComponent<HoundController>().dir < 0;
                    ChildSprite(child.gameObject, "HoundArt", hound, 0.62f, 6, flip);
                }
                if (child.name.StartsWith("Drone") && drone != null)
                    ChildSprite(child.gameObject, "DroneArt", drone, 0.55f, 6);
            }

            // ---- TOKENS (footprint Big Token) + secret heart ----
            var token = Art("art_token.png");
            foreach (Transform child in map.transform) {
                if (!child.name.StartsWith("Token_")) continue;
                if (token != null) ChildSprite(child.gameObject, "TokenArt", token, 0.66f, 5);
            }
            var heartArt = Art("art_heart.png");
            var sh = data.ContainsKey("secretHeart") ? data["secretHeart"] as Dictionary<string, object> : null;
            if (sh != null && heartArt != null) {
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
            if (gate != null) {
                var fgArt = Art("art_flaggate.png");
                if (fgArt != null) SpriteGo("FlagGateArt", fgArt, new Vector3(gate.transform.position.x, GY + 1.2f, 0), 2.2f, 4, map.transform);
                var portal = Art("art_flagportal.png");
                if (portal != null) SpriteGo("PortalArt", portal, new Vector3(gate.transform.position.x - 1.4f, GY + 1.6f, 0), 3.2f, 3, map.transform);
            }

            // ---- PLAYER: Lily, real art on a child sprite (capsule collider untouched) ----
            var lily = GameObject.Find("Lily");
            if (lily != null && Art("whole_lily.png") != null)
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

        /// <summary>CI entry: build map 001, dress with art, save scene, render QC shots, exit.</summary>
        public static void BuildAndShoot() {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            LilFootsLevelBuilder.Build();
            BuildArt();
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Scenes/Map001.unity");
            Directory.CreateDirectory("QCShots");

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
            Snap(rt, "QCShots/unity_start.png");
            // shot 2: mid-map, hound + cam tree
            float hx = hound != null ? hound.transform.position.x : 45f;
            cam.transform.position = new Vector3(hx + 2.2f, 4.2f, -10f);
            Snap(rt, "QCShots/unity_mid.png");
            // shot 3: finish gate + flag + portal
            float gx = gate != null ? gate.transform.position.x : 86f;
            cam.transform.position = new Vector3(gx - 2.5f, 4.2f, -10f);
            Snap(rt, "QCShots/unity_gate.png");

            Debug.Log("[ArtPass] QC shots done.");
            EditorApplication.Exit(0);
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
