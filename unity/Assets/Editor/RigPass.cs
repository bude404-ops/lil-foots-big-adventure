// Lil Foots: Big Adventure — RIG PASS 1 (Bude, Sept 18 2026: "can we begin rigging the characters as well?")
// Builds the skeleton for each approved rig-ready T-pose (Lily, Buddy, Emma):
//   - full Unity bone hierarchy (root/hip/spine/chest/head + arms + legs) mapped onto the uncut sprite
//   - overlay render: bones + joints drawn ON the T-pose art (whole-sprite law: art stays uncut)
//   - walk-cycle pose sheet: 4 articulated skeletons (contact/down/pass/up) proving the rig moves
// Relays QC shots to Bude's DM. Rigging only — no skinning/weights yet (that's pass 2 on his verdict).
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LilFoots.EditorTools
{
    public static class RigPass
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        static Sprite Art(string path) {
            if (Cache.TryGetValue(path, out var s)) return s;
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null) {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 100f;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
            s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Cache[path] = s;
            if (s == null) Debug.LogWarning("[RigPass] MISSING ART: " + path);
            return s;
        }

        // bone endpoint fractions of the T-pose art rect (x: 0..1 left->right, y: 0..1 bottom->top)
        // T-pose: arms straight out at shoulder line (~0.66 height), legs slightly apart.
        static readonly Dictionary<string, Vector2> Pose = new Dictionary<string, Vector2> {
            {"hip",       new Vector2(0.50f, 0.42f)},
            {"spine",     new Vector2(0.50f, 0.52f)},
            {"chest",     new Vector2(0.50f, 0.63f)},
            {"neck",      new Vector2(0.50f, 0.72f)},
            {"headTop",   new Vector2(0.50f, 0.92f)},
            {"shoulderL", new Vector2(0.34f, 0.66f)},
            {"elbowL",    new Vector2(0.20f, 0.66f)},
            {"handL",     new Vector2(0.07f, 0.66f)},
            {"shoulderR", new Vector2(0.66f, 0.66f)},
            {"elbowR",    new Vector2(0.80f, 0.66f)},
            {"handR",     new Vector2(0.93f, 0.66f)},
            {"thighL",    new Vector2(0.44f, 0.30f)},
            {"kneeL",     new Vector2(0.44f, 0.17f)},
            {"footL",     new Vector2(0.44f, 0.04f)},
            {"thighR",    new Vector2(0.56f, 0.30f)},
            {"kneeR",     new Vector2(0.56f, 0.17f)},
            {"footR",     new Vector2(0.56f, 0.04f)},
        };

        // parent chain for the bone hierarchy
        static readonly Dictionary<string, string> Parent = new Dictionary<string, string> {
            {"spine", "hip"}, {"chest", "spine"}, {"neck", "chest"}, {"headTop", "neck"},
            {"shoulderL", "chest"}, {"elbowL", "shoulderL"}, {"handL", "elbowL"},
            {"shoulderR", "chest"}, {"elbowR", "shoulderR"}, {"handR", "elbowR"},
            {"thighL", "hip"}, {"kneeL", "thighL"}, {"footL", "kneeL"},
            {"thighR", "hip"}, {"kneeR", "thighR"}, {"footR", "kneeR"},
        };

        // walk-cycle articulation: local euler Z (deg) per bone per keyframe (contact/down/pass/up)
        // arms swing opposite legs; front leg extends, back leg lifts.
        static readonly Dictionary<string, float[]> Walk = new Dictionary<string, float[]> {
            {"thighL",  new float[]{  32f,  18f,  -5f, -22f}},
            {"kneeL",   new float[]{  -4f, -18f,  -28f, -10f}},
            {"thighR",  new float[]{ -22f,  -5f,  18f,  32f}},
            {"kneeR",   new float[]{ -10f, -28f, -18f,  -4f}},
            {"shoulderL", new float[]{ -28f, -12f, 10f, 24f}},
            {"elbowL",    new float[]{  -8f,  -4f,  -6f, -10f}},
            {"shoulderR", new float[]{  24f,  10f, -12f, -28f}},
            {"elbowR",    new float[]{ -10f,  -6f,  -4f,  -8f}},
            {"chest",   new float[]{  4f,  2f, -2f, -4f}},
        };

        static Texture2D _dot;
        static Texture2D Dot() {
            if (_dot != null) return _dot;
            int r = 24;
            var t = new Texture2D(2 * r, 2 * r, TextureFormat.RGBA32, false);
            for (int y = 0; y < 2 * r; y++)
                for (int x = 0; x < 2 * r; x++) {
                    float d = Mathf.Sqrt((x - r + 0.5f) * (x - r + 0.5f) + (y - r + 0.5f) * (y - r + 0.5f));
                    Color c = d <= r * 0.62f ? Color.white : Color.clear;
                    if (d > r * 0.62f && d <= r * 0.95f) c = new Color(0.05f, 0.05f, 0.05f, 1f); // ink ring
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return _dot = t;
        }

        static Material _lineMat;
        static Material LineMat() {
            if (_lineMat != null) return _lineMat;
            _lineMat = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
            return _lineMat;
        }

        static GameObject BoneLine(Vector3 a, Vector3 b, Color c, int order, Transform parent) {
            var go = new GameObject("bone_line");
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.startWidth = 0.055f; lr.endWidth = 0.055f;
            lr.material = LineMat();
            lr.sortingOrder = order;
            lr.startColor = c; lr.endColor = c;
            return go;
        }

        static GameObject JointDot(Vector3 p, float size, Color c, int order, Transform parent) {
            var go = new GameObject("joint");
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(Dot(), new Rect(0, 0, 48, 48), new Vector2(0.5f, 0.5f), 100f);
            sr.sortingOrder = order;
            sr.color = c;
            go.transform.position = p;
            go.transform.localScale = new Vector3(size, size, 1f);
            return go;
        }

        class CharRig {
            public GameObject Root;
            public Dictionary<string, Transform> Bones = new Dictionary<string, Transform>();
            public Sprite Sprite;
            public float WorldH;
            public Vector2 ArtSize;
        }

        static CharRig BuildRig(string name, string artPath, float worldH, Vector3 pos, float artAlpha) {
            var rig = new CharRig { WorldH = worldH };
            var s = Art(artPath);
            rig.Sprite = s;
            if (s != null) rig.ArtSize = s.bounds.size;

            var rootGo = new GameObject(name + "Rig");
            rootGo.transform.position = pos;
            rig.Root = rootGo;

            // uncut whole sprite, drawn faint under the bones
            var artGo = new GameObject(name + "Tpose");
            artGo.transform.SetParent(rootGo.transform, false);
            var asr = artGo.AddComponent<SpriteRenderer>();
            asr.sprite = s;
            asr.sortingOrder = 10;
            float f = worldH / (s != null ? s.bounds.size.y : 1f);
            artGo.transform.localScale = new Vector3(f, f, 1f);
            asr.color = new Color(1f, 1f, 1f, artAlpha);

            // bone transforms in a FLAT hierarchy under root, each positioned in world space
            // (hierarchy parent chain is set via Transform parenting AFTER positioning, so local
            //  offsets derive from the anatomical points — no double-transform issues)
            foreach (var kv in Pose) {
                var b = new GameObject("bone_" + kv.Key);
                b.transform.SetParent(rootGo.transform, false);
                Vector3 w = new Vector3(
                    pos.x + (kv.Value.x - 0.5f) * worldH * (s != null ? rig.ArtSize.x / rig.ArtSize.y : 1f),
                    pos.y + (kv.Value.y - 0.5f) * worldH, 0f);
                b.transform.position = w;
                rig.Bones[kv.Key] = b.transform;
            }
            foreach (var kv in Parent)
                rig.Bones[kv.Key].SetParent(rig.Bones[kv.Value], true); // worldPositionStays: keep anatomy
            return rig;
        }

        public static void BuildAndShoot() {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // camera
            var camGo = new GameObject("RigCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 3.4f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.10f, 0.13f, 1f);
            cam.transform.position = new Vector3(0f, 2.6f, -10f);
            cam.tag = "MainCamera";

            Directory.CreateDirectory("QCShots");

            var chars = new (string name, string file, Color bone, Color joint)[] {
                ("lily",  "lily-rig-ready-v1.png",  new Color(0.60f, 0.98f, 0.25f), new Color(1f, 1f, 1f)),
                ("buddy", "buddy-rig-ready-v1.png", new Color(1.00f, 0.72f, 0.16f), new Color(1f, 1f, 1f)),
                ("emma",  "emma-rig-ready-v1.png",  new Color(1.00f, 0.42f, 0.75f), new Color(1f, 1f, 1f)),
            };

            var rigs = new Dictionary<string, CharRig>();
            int i = 0;
            foreach (var c in chars) {
                var rig = BuildRig(c.name, "Assets/Art/Characters/" + c.file, 5.2f, new Vector3(i * 12f, 2.6f, 0f), 0.85f);
                rigs[c.name] = rig;
                // overlay: bone lines + joint dots on top of the art
                foreach (var kv in Parent) {
                    Vector3 a = rig.Bones[kv.Key].position;
                    Vector3 b = rig.Bones[kv.Value].position;
                    BoneLine(a, b, c.bone, 150, rig.Root.transform);
                }
                foreach (var kv in Pose)
                    JointDot(rig.Bones[kv.Key].position, 0.30f, c.joint, 200, rig.Root.transform);
                i++;
            }

            var rt = new RenderTexture(1334, 750, 24);
            cam.targetTexture = rt;

            // one rig sheet per character
            foreach (var c in chars) {
                var rig = rigs[c.name];
                cam.transform.position = new Vector3(rig.Root.transform.position.x, 2.6f, -10f);
                Snap(rt, "QCShots/rig_" + c.name + ".png");
            }

            // walk-cycle pose sheet (Lily): 4 articulated skeletons over ghosted art
            var walkRoot = new GameObject("WalkCycle");
            walkRoot.transform.position = new Vector3(0f, 2.6f, 0f);
            for (int k = 0; k < 4; k++) {
                var ghostRig = BuildRig("walk" + k, "Assets/Art/Characters/lily-rig-ready-v1.png", 5.2f, new Vector3((k - 1.5f) * 6.5f, 2.6f, 0f), 0.30f);
                // articulate: rotate bones per walk keyframe (Z euler), arms/legs swing
                foreach (var w in Walk) {
                    if (ghostRig.Bones.TryGetValue(w.Key, out var bone)) {
                        float deg = w.Value[k];
                        bone.localRotation = Quaternion.Euler(0f, 0f, deg);
                    }
                }
                foreach (var kv in Parent) {
                    Vector3 a = ghostRig.Bones[kv.Key].position;
                    Vector3 b = ghostRig.Bones[kv.Value].position;
                    BoneLine(a, b, new Color(0.60f, 0.98f, 0.25f), 150, walkRoot.transform);
                }
                foreach (var kv in Pose)
                    JointDot(ghostRig.Bones[kv.Key].position, 0.30f, Color.white, 200, walkRoot.transform);
            }
            cam.transform.position = new Vector3(0f, 2.6f, -10f);
            Snap(rt, "QCShots/rig_walkcycle.png");

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Scenes/RigSheet.unity");

            Debug.Log("[RigPass] rig QC shots done.");
            EditorApplication.Exit(0);
        }

        static void Snap(RenderTexture rt, string path) {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            rt.DiscardContents();
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
