// Lil Foots: Big Adventure — RIG PASS 2, NATIVE (Bude doctrine, Sept 18 2026: "UNITY MUST PERFORM THE WORK")
// Unity 2D Animation is the rigging system. This Editor script only AUTOMATES Unity:
//   - Unity creates the bone hierarchy (native Transforms, anatomical parent chain)
//   - Unity gets the native SpriteSkin component (UnityEngine.U2D.Animation) — rootBone + boneTransforms
//   - Unity creates a native AnimatorController + walk-cycle AnimationClip (UnityEditor.Animations)
//   - Unity attaches the native Animator and drives the walk clip
// No custom skeleton system, no external animation engine. Big automates; Unity performs.
// QC: bind-pose render + native Animator pose render (the clip poses the rig = validated),
// plus a bone overlay pass for review shots. Validation = Unity must report zero errors.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using U2D = UnityEngine.U2D.Animation;

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
            if (s == null) Debug.LogError("[RigPass] MISSING ART: " + path);
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

        class CharRig {
            public GameObject Root;
            public Dictionary<string, Transform> Bones = new Dictionary<string, Transform>();
            public Dictionary<string, string> AnimPaths = new Dictionary<string, string>();
            public float WorldH;
        }

        // Build the NATIVE rig: SpriteRenderer + Unity bone hierarchy + SpriteSkin + Animator + walk clip.
        static CharRig BuildRig(string name, string artPath, float worldH, Vector3 pos) {
            var rig = new CharRig { WorldH = worldH };
            var s = Art(artPath);

            var charGo = new GameObject(name);
            charGo.transform.position = pos;
            rig.Root = charGo;

            var sr = charGo.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = 10;
            float f = (s != null) ? worldH / s.bounds.size.y : 1f;
            charGo.transform.localScale = new Vector3(f, f, 1f);

            // world-space art rect (pivot-centered)
            float wW = s.bounds.size.x * f, hW = s.bounds.size.y * f;

            // ---- Unity creates the native bone hierarchy ----
            var boneRootGo = new GameObject("root");
            boneRootGo.transform.SetParent(charGo.transform, false);
            foreach (var kv in Pose) {
                var bone = new GameObject(kv.Key);
                bone.transform.SetParent(boneRootGo.transform, false);
                // bone positions in art-fraction space, scaled by art rect, centered on the character
                bone.transform.localPosition = new Vector3(
                    (kv.Value.x - 0.5f) * wW / f,
                    (kv.Value.y - 0.5f) * hW / f, 0f);
                rig.Bones[kv.Key] = bone.transform;
            }
            foreach (var kv in Parent) rig.Bones[kv.Key].SetParent(rig.Bones[kv.Value], true);
            // full animation paths AFTER reparenting: walk each bone up to the character root
            foreach (var kv in rig.Bones) {
                var names = new List<string>();
                var t = kv.Value;
                while (t != null && t != charGo.transform) { names.Add(t.name); t = t.parent; }
                names.Reverse();
                rig.AnimPaths[kv.Key] = string.Join("/", names.ToArray());
            }

            // ---- native SpriteSkin: Unity 2D Animation performs the deformation ----
            var skin = charGo.AddComponent<U2D.SpriteSkin>();
            skin.rootBone = rig.Bones["hip"];
            var bt = new List<Transform>();
            foreach (var kv in rig.Bones) bt.Add(kv.Value);
            skin.boneTransforms = bt.ToArray();

            // ---- Unity creates a native AnimatorController + walk AnimationClip ----
            Directory.CreateDirectory("Assets/Animation");
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath("Assets/Animation/" + name + "_WalkController.controller");
            var walkState = ctrl.layers[0].stateMachine.AddState("walk");

            var clip = new AnimationClip { frameRate = 12f };
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            foreach (var kv in Walk) {
                var eulers = kv.Value;
                var keys = new Keyframe[5];
                for (int i = 0; i < 4; i++) keys[i] = new Keyframe(i / 12f, eulers[i]);
                keys[4] = new Keyframe(4 / 12f, eulers[0]); // loop wrap
                var binding = EditorCurveBinding.FloatCurve(rig.AnimPaths[kv.Key], typeof(Transform), "localEulerAnglesRaw.z");
                AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys));
            }
            AssetDatabase.CreateAsset(clip, "Assets/Animation/" + name + "_Walk.anim");
            var walkMotion = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animation/" + name + "_Walk.anim");
            walkState.motion = walkMotion;

            var animator = charGo.AddComponent<Animator>();
            animator.runtimeAnimatorController = ctrl;
            return rig;
        }

        static Texture2D _dot;
        static Texture2D Dot() {
            if (_dot != null) return _dot;
            int r = 24;
            var t = new Texture2D(2 * r, 2 * r, TextureFormat.RGBA32, false);
            for (int y = 0; y < 2 * r; y++)
                for (int x = 0; x < 2 * r; x++) {
                    float d = Mathf.Sqrt((x - r + 0.5f) * (x - r + 0.5f) + (y - r + 0.5f) * (y - r + 0.5f));
                    Color c = d <= r * 0.62f ? Color.white : Color.clear;
                    if (d > r * 0.62f && d <= r * 0.95f) c = new Color(0.05f, 0.05f, 0.05f, 1f);
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return _dot = t;
        }

        /// <summary>QC review overlay: joint dots on the native bone transforms. Visual only — the rig is Unity's.</summary>
        static void OverlayBones(CharRig rig) {
            foreach (var kv in rig.Bones) {
                var dot = new GameObject("dot_" + kv.Key);
                dot.transform.SetParent(kv.Value, false);
                var dsr = dot.AddComponent<SpriteRenderer>();
                dsr.sprite = Sprite.Create(Dot(), new Rect(0, 0, 48, 48), new Vector2(0.5f, 0.5f), 100f);
                dsr.sortingOrder = 40;
                dot.transform.localScale = new Vector3(0.10f / rig.Root.transform.localScale.x, 0.10f / rig.Root.transform.localScale.y, 1f);
            }
        }

        static void ClearOverlay(CharRig rig) {
            var doomed = new List<GameObject>();
            foreach (var kv in rig.Bones) {
                for (int i = 0; i < kv.Value.childCount; i++) {
                    var ch = kv.Value.GetChild(i);
                    if (ch.name.StartsWith("dot_")) doomed.Add(ch.gameObject);
                }
            }
            foreach (var d in doomed) Object.DestroyImmediate(d);
        }

        public static void BuildAndShoot() {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("RigCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 3.2f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.10f, 0.13f, 1f);
            cam.transform.position = new Vector3(0f, 2.3f, -10f);
            cam.tag = "MainCamera";

            var outDir = System.Environment.GetEnvironmentVariable("QC_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = "QCShots";
            Directory.CreateDirectory(outDir);

            // rig all three approved T-poses with the NATIVE stack (Unity 2D Animation + Animator)
            var chars = new[] {
                new { name = "Lily",  file = "lily-rig-ready-v1-cut.png",  x = -3.2f },
                new { name = "Buddy", file = "buddy-rig-ready-v1-cut.png", x =  0.0f },
                new { name = "Emma",  file = "emma-rig-ready-v1-cut.png",  x =  3.2f },
            };
            var rigs = new List<CharRig>();
            foreach (var c in chars)
                rigs.Add(BuildRig(c.name, "Assets/Art/Characters/" + c.file, 2.6f, new Vector3(c.x, 2.0f, 0f)));

            // ---- QC 1: bind pose (native SpriteSkin, rest pose) ----
            cam.Render();
            Snap(cam, outDir + "/rig_bindpose.png");

            // ---- QC 2: Unity's native Animator plays the walk clip, sampled mid-cycle ----
            foreach (var r in rigs) {
                var anim = r.Root.GetComponent<Animator>();
                anim.Play("walk", 0, 2f / 12f); // "pass" keyframe, mid-stride
                anim.Update(0f);
            }
            cam.Render();
            Snap(cam, outDir + "/rig_walk_native.png");

            // ---- QC 3: bone overlay on the posed frame (review visual) ----
            foreach (var r in rigs) OverlayBones(r);
            cam.Render();
            Snap(cam, outDir + "/rig_walk_bones.png");
            foreach (var r in rigs) ClearOverlay(r);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Scenes/RigSheet.unity");
            AssetDatabase.SaveAssets();

            Debug.Log("[RigPass] NATIVE rig pass complete: Unity created bone hierarchies, SpriteSkin components, "
                      + "AnimatorControllers + walk clips. QC: bindpose / walk_native / walk_bones. Unity owns the rig.");
        }

        static void Snap(Camera cam, string path) {
            int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = prev;
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Debug.Log("[RigPass] QC shot: " + path);
        }
    }
}
#endif
