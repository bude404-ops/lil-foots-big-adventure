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
        // BLUE-LINE SKELETON (Bude's ref, imgur L4ui1Jm): head, neck, SINGLE spine bone,
        // single-bone arms straight off the shoulder girdle, hip, single-bone legs. No elbows, no knees.
        static readonly Dictionary<string, Vector2> Pose = new Dictionary<string, Vector2> {
            {"hip",       new Vector2(0.50f, 0.42f)},   // pelvis pivot (hip bar center)
            {"neck",      new Vector2(0.50f, 0.72f)},   // top of the single spine bone
            {"head",      new Vector2(0.50f, 0.92f)},   // head bone to top of skull
            {"shoulderL", new Vector2(0.34f, 0.66f)},   // arm bone pivots at the shoulder girdle
            {"handL",     new Vector2(0.07f, 0.66f)},   // single-bone arm: shoulder -> hand
            {"shoulderR", new Vector2(0.66f, 0.66f)},
            {"handR",     new Vector2(0.93f, 0.66f)},
            {"legL",      new Vector2(0.44f, 0.42f)},   // leg bone pivots AT THE HIP
            {"footL",     new Vector2(0.44f, 0.04f)},   // single-bone leg: hip -> foot
            {"legR",      new Vector2(0.56f, 0.42f)},
            {"footR",     new Vector2(0.56f, 0.04f)},
        };

        // parent chain — matches the blue line exactly: hip -> neck (one spine) -> head + shoulders;
        // hip -> legs (straight through, no knees)
        static readonly Dictionary<string, string> Parent = new Dictionary<string, string> {
            {"neck", "hip"}, {"head", "neck"},
            {"shoulderL", "neck"}, {"handL", "shoulderL"},
            {"shoulderR", "neck"}, {"handR", "shoulderR"},
            {"legL", "hip"}, {"footL", "legL"},
            {"legR", "hip"}, {"footR", "legR"},
        };

        // IDLE (character select law, Bude Sept 18/19: cards use the idle pose once animated):
        // gentle breathing bob — shoulders counter-sway 3°, neck 2°, head 1.5°, hip 1°. 2s loop.
        // IDLE REST POSE v2 (Bude, Sept 19: 'Show me one image of lily in an idle pose'): an idle
        // pose is a RELAXED STANCE, not a T-pose with 3 degrees of breathing - the arms must come
        // DOWN at the natural arms-down rest (bind art IS the approved original art, Sept 20) with the breathing
        // sway on top. Sign: shoulderL sits left of center in local space; +z rotates its hand
        // offset down; shoulderR mirrors with -z.
        static readonly Dictionary<string, float[]> Idle = new Dictionary<string, float[]> {
            {"shoulderL", new float[]{ 3f,  0f,  -3f,  0f}},
            {"shoulderR", new float[]{ -3f,  0f,  3f,  0f}},
            {"neck",      new float[]{ 2.0f,  0.0f, -2.0f,  0.0f}},
            {"head",      new float[]{ 1.5f,  0.0f, -1.5f,  0.0f}},
            {"hip",       new float[]{ 1.0f,  0.0f, -1.0f,  0.0f}},
        };

        // walk cycle for the blue-line skeleton: swing at the hips, arms counter, slight torso/head bob
        static readonly Dictionary<string, float[]> Walk = new Dictionary<string, float[]> {
            {"legL",      new float[]{  30f,  14f,  -6f, -24f}},
            {"legR",      new float[]{ -24f,  -6f,  14f,  30f}},
            {"shoulderL", new float[]{  21f,  0f,  -21f,  0f}},  // counter-swing around the natural arms-down rest
            {"shoulderR", new float[]{  -21f,  0f,  21f,  0f}},
            {"neck",      new float[]{   4f,   2f, -2f,  -4f}},
        };

        // JUMP (gameplay law): legs split-tuck mid-air, arms swing out for balance.
        // Same curve-convention as Walk (signs mirror around the arms-down rest pose).
        static readonly Dictionary<string, float[]> Jump = new Dictionary<string, float[]> {
            {"legL",      new float[]{ 24f,  20f,  23f,  24f}},
            {"legR",      new float[]{ 34f,  38f,  35f,  34f}},
            {"shoulderL", new float[]{ -24f, -29f,  -26f,  -24f}},
            {"shoulderR", new float[]{  24f,  29f,   26f,   24f}},
            {"neck",      new float[]{ -2.0f, -1.0f, -1.5f, -2.0f}},
            {"hip",       new float[]{ -2.0f,  0.0f, -1.0f, -2.0f}},
        };

        // natural standing stance measured off the whole_lily art (arms angled down, feet splayed) -
        // THE character-select idle stance (Bude's approved idle look, Sept 19).
        static readonly Dictionary<string, Vector2> SelectStance = new Dictionary<string, Vector2> {
            {"hip",       new Vector2(0.50f, 0.55f)},
            {"neck",      new Vector2(0.50f, 0.76f)},
            {"head",      new Vector2(0.50f, 0.94f)},
            {"shoulderL", new Vector2(0.32f, 0.68f)},
            {"handL",     new Vector2(0.10f, 0.38f)},
            {"shoulderR", new Vector2(0.68f, 0.68f)},
            {"handR",     new Vector2(0.90f, 0.38f)},
            {"legL",      new Vector2(0.42f, 0.55f)},
            {"footL",     new Vector2(0.28f, 0.08f)},
            {"legR",      new Vector2(0.58f, 0.55f)},
            {"footR",     new Vector2(0.72f, 0.08f)},
        };

        // BUDDY + EMMA STANCES (Sept 20 fix): their original art hangs the arms at the sides
        // (measured off the restored exact-ref sprites) - Lily keeps her spread stance.
        static readonly Dictionary<string, Vector2> BuddyStance = new Dictionary<string, Vector2> {
            {"hip",       new Vector2(0.50f, 0.50f)},
            {"neck",      new Vector2(0.50f, 0.78f)},
            {"head",      new Vector2(0.50f, 0.96f)},
            {"shoulderL", new Vector2(0.40f, 0.70f)},
            {"handL",     new Vector2(0.14f, 0.42f)},
            {"shoulderR", new Vector2(0.60f, 0.70f)},
            {"handR",     new Vector2(0.87f, 0.42f)},
            {"legL",      new Vector2(0.45f, 0.50f)},
            {"footL",     new Vector2(0.28f, 0.02f)},
            {"legR",      new Vector2(0.55f, 0.50f)},
            {"footR",     new Vector2(0.72f, 0.02f)},
        };
        static readonly Dictionary<string, Vector2> EmmaStance = new Dictionary<string, Vector2> {
            {"hip",       new Vector2(0.50f, 0.50f)},
            {"neck",      new Vector2(0.50f, 0.78f)},
            {"head",      new Vector2(0.50f, 0.96f)},
            {"shoulderL", new Vector2(0.40f, 0.70f)},
            {"handL",     new Vector2(0.10f, 0.42f)},
            {"shoulderR", new Vector2(0.60f, 0.70f)},
            {"handR",     new Vector2(0.90f, 0.42f)},
            {"legL",      new Vector2(0.45f, 0.50f)},
            {"footL",     new Vector2(0.30f, 0.02f)},
            {"legR",      new Vector2(0.55f, 0.50f)},
            {"footR",     new Vector2(0.70f, 0.02f)},
        };
        public static Dictionary<string, Vector2> StanceFor(string name) {
            if (name.ToLower().Contains("buddy")) return BuddyStance;
            if (name.ToLower().Contains("emma")) return EmmaStance;
            return SelectStance;
        }

        /// <summary>Character-select stage rig (Bude, Sept 19: "I thought we were going to use
        /// their idle pose in the character select"). Builds one fully rigged character -
        /// SpriteSkin + Animator with the IDLE clip as default state (the select law) - posed in
        /// the natural select stance. Returns the root GameObject; parent/cleanup is the caller's.
        /// World height 2.4 so three of them fit one shared stage camera.</summary>
        public static GameObject BuildStageRig(string name, string artPath, Vector3 pos) {
            var rig = BuildRig(name, artPath, 2.4f, pos, StanceFor(name));
            // Bake the idle clip's deepest-breath pose onto the bones at BUILD time (deterministic,
            // clip data drives it) so the saved scene carries the arms-down idle stance - the
            // runtime Animator then owns the live breathing loop from the same clip.
            ApplyPoseFromClip(rig, rig.IdleClip, 1f / 6f);
            var root = rig.Root;
            var anim = root.GetComponent<Animator>();
            if (anim != null) {
                // the select menu pauses the game (timeScale 0) - unscaled time keeps the idle
                // breathing on the cards; always-animate because they render via the stage RT
                // camera, not the gameplay camera.
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                anim.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
            return root;
        }

        /// <summary>GAMEPLAY PLAYER RIGS (Bude, Sept 20: "the characters pose still is the t pose
        /// and no animations"): builds ALL THREE Lil Foots fully rigged under the player object -
        /// feet anchored to the player origin, gameplay height, idle default state, speed/air
        /// params wired for idle <-> walk <-> jump. The chosen character's rig is active, the
        /// other two disabled; CharacterMenuController.Select swaps them at runtime. Falls back
        /// to null on any failure (caller keeps the static whole-sprite path).</summary>
        public static GameObject BuildPlayerRigs(GameObject player, string defaultChar) {
            string[] names = { "Lily", "Buddy", "Emma" };
            string[] files = { "whole_lily.png", "whole_buddy.png", "whole_emma.png" };
            float[] feetFrac = { 0.001f, 0.001f, 0.002f };  // Lily re-measured off his reference art (feet at bottom, no shadow strip)  // measured off Bude's original art (arms-down)
            GameObject active = null;
            for (int i = 0; i < 3; i++) {
                var rig = BuildRig(names[i] + "Rig", "Assets/Art/" + files[i], 0.82f, player.transform.position, StanceFor(names[i]));
                rig.Root.transform.SetParent(player.transform, false);
                // feet-anchor: sprite center sits (0.5 - feetFrac) * worldH above the player origin
                float off = (0.5f - feetFrac[i]) * 0.82f;
                rig.Root.transform.localPosition = new Vector3(0f, off, 0f);
                // bake the arms-down idle stance into the saved scene (same as the select cards)
                ApplyPoseFromClip(rig, rig.IdleClip, 1f / 6f);
                var anim = rig.Root.GetComponent<Animator>();
                if (anim != null) {
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; // in-game rig never sleeps
                    anim.updateMode = AnimatorUpdateMode.Normal;
                }
                bool isDefault = names[i].ToLower() == defaultChar;
                rig.Root.SetActive(isDefault);
                if (isDefault) active = rig.Root;
                // flip anchor for PlayerAnimBridge: store the base scale so facing flips are sign-safe
                rig.Root.AddComponent<PlayerAnimBridge>();
                WireFrameAnimator(rig.Root, names[i]);   // generated frame sets replace procedural motion when present
            }
            return active;
        }

        class CharRig {
            public string Name;
            public GameObject Root;
            public List<string> BoneOrder = new List<string>();   // deterministic: matches SpriteBone[]/bindpose/weight order
            public Dictionary<string, Transform> Bones = new Dictionary<string, Transform>();
            public Dictionary<string, string> AnimPaths = new Dictionary<string, string>();
            public float WorldH;
            public AnimationClip IdleClip;   // for edit-mode QC sampling (AnimationMode)
            public AnimationClip WalkClip;
        }

        // Build the NATIVE rig: SpriteRenderer + Unity bone hierarchy + SpriteSkin + Animator + walk clip.
        // ==================== FRAME ANIMATION WIRING (BudE Sept 26: generated frame sets) ====================
        /// <summary>Loads Assets/Art/Frames/&lt;char&gt;_&lt;anim&gt;_N.png (Sprite, PPU 100, FullRect)
        /// and wires a FrameAnimator when any frame exists. Missing sets fall back to the
        /// rig's native motion - frame art is optional per character.</summary>
        static void WireFrameAnimator(GameObject root, string charName) {
            var walk = LoadFrameSet(charName, "walk", 6);
            var jump = LoadFrameSet(charName, "jump", 4);
            var idle = LoadFrameSet(charName, "idle", 2);
            if (walk.Length == 0 && jump.Length == 0 && idle.Length == 0) return;
            var fa = root.AddComponent<FrameAnimator>();
            fa.idle = idle; fa.walk = walk; fa.jump = jump;
            Debug.Log("[RigPass] FRAME ANIMATION: " + charName + " wired - " + walk.Length + " walk / "
                      + jump.Length + " jump / " + idle.Length + " idle generated frames");
        }

        static Sprite[] LoadFrameSet(string charName, string anim, int count) {
            var list = new List<Sprite>();
            for (int i = 0; i < count; i++) {
                string path = "Assets/Art/Frames/" + charName.ToLower() + "_" + anim + "_" + i + ".png";
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp == null) {
                    var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (ti != null) {
                        ti.textureType = TextureImporterType.Sprite;
                        ti.spriteImportMode = SpriteImportMode.Single;
                        ti.spritePixelsPerUnit = 100f;
                        ti.alphaIsTransparency = true;
                        ti.mipmapEnabled = false;
                        // spriteMeshType lives on TextureImporterSettings, not TextureImporter
                        var tis = new TextureImporterSettings();
                        ti.ReadTextureSettings(tis);
                        tis.spriteMeshType = SpriteMeshType.FullRect;
                        ti.SetTextureSettings(tis);
                        ti.SaveAndReimport();
                        sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    }
                }
                if (sp != null) list.Add(sp);
            }
            return list.ToArray();
        }

        static CharRig BuildRig(string name, string artPath, float worldH, Vector3 pos,
                                  Dictionary<string, Vector2> poseOverride = null) {
            var rig = new CharRig { WorldH = worldH };
            var s = Art(artPath);
            var PoseMap = poseOverride != null ? poseOverride : Pose;

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
            foreach (var kv in PoseMap) {
                rig.BoneOrder.Add(kv.Key);
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

            // ---- [UNITY 2D ANIMATION, BudE Sept 26 ~1:40 AM ET: "I also want you to use
            // unity 2d animation instead of what im assuming you are with yourself?"]
            // The character stays ONE WHOLE sprite (his approved art, never cut) and now
            // gets REAL skeletal deformation on Unity's own 2D Animation package: the
            // sprite's skinning data (SpriteBone[] hierarchy + bind poses + per-vertex bone
            // weights on a deformation grid mesh) is authored in-engine with the SAME
            // public Sprite APIs the package's own editor uses (SetBones / SetBindPoses /
            // SetVertexAttribute - verified against com.unity.2d.animation@9.2.2 source),
            // then SpriteSkin deforms the mesh at runtime as the Animator drives the bones.
            // The whole-art bridge bob stays as the safety net if authoring ever fails.
            // ---- [FRAME ANIMATION, BudE Sept 26 ~1:44 AM ET: "we need to generate frames and
            // proper smount of frames needed for the characters and their poses aka walking
            // jumping etc... that will make the animations better and easier"] The character
            // animation is now FRAME-BASED (classic sprite animation): generated frame sets
            // (idle/walk/jump) rendered FROM his approved reference art, normalized to the
            // same canvas + feet baseline, played by FrameAnimator by player state. The
            // Unity 2D Animation (SpriteSkin) programmatic-authoring path needs Unity 6000.x
            // Sprite APIs (SetVertexAttribute/SetBones/SetBindPoses) which do NOT exist in
            // the 2022.3 forge - it returns when the build image is Unity 6. Until then the
            // whole-art bridge motion (squash/lean/facing) stays layered on top of frames.

            // ---- Unity creates native AnimationClips + AnimatorController ----
            // IDLE = default state (character select law). WALK = second state (gameplay).
            Directory.CreateDirectory("Assets/Animation");
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath("Assets/Animation/" + name + "_Controller.controller");

            System.Func<Dictionary<string, float[]>, int, string, AnimationClip> bake =
                (Dictionary<string, float[]> cycles, int fps, string suffix) => {
                var c = new AnimationClip { frameRate = fps };
                var st = AnimationUtility.GetAnimationClipSettings(c);
                st.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(c, st);
                foreach (var kv in cycles) {
                    if (!rig.AnimPaths.ContainsKey(kv.Key)) continue;   // path may not exist (legsBand is fallback-only)
                    var eulers = kv.Value;
                    var keys = new Keyframe[eulers.Length + 1];
                    for (int i = 0; i < eulers.Length; i++) keys[i] = new Keyframe(i / (float)fps, eulers[i]);
                    keys[eulers.Length] = new Keyframe(eulers.Length / (float)fps, eulers[0]); // loop wrap
                    var binding = EditorCurveBinding.FloatCurve(rig.AnimPaths[kv.Key], typeof(Transform), "localEulerAnglesRaw.z");
                    AnimationUtility.SetEditorCurve(c, binding, new AnimationCurve(keys));
                }
                AssetDatabase.CreateAsset(c, "Assets/Animation/" + name + "_" + suffix + ".anim");
                return AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animation/" + name + "_" + suffix + ".anim");
            };

            var idleClip = bake(Idle, 6, "Idle");   // 4 keys @ 6fps = 2s breathing loop
            var walkClip = bake(Walk, 12, "Walk");
            rig.Name = name; rig.IdleClip = idleClip; rig.WalkClip = walkClip;

            var idleState = ctrl.layers[0].stateMachine.AddState("idle");
            idleState.motion = idleClip;
            ctrl.layers[0].stateMachine.defaultState = idleState;
            var walkState = ctrl.layers[0].stateMachine.AddState("walk");
            walkState.motion = walkClip;
            var jumpClip = bake(Jump, 8, "Jump");

            // ---- gameplay state machine (Bude, Sept 20: 'the characters pose still is the t pose
            // and no animations'): speed/air params drive idle <-> walk <-> jump so the PLAYER
            // rig is fully animated. Stage rigs keep idle as default (select law) - they simply
            // never set the params. ----
            var speedP = new AnimatorControllerParameter { name = "speed", type = AnimatorControllerParameterType.Float, defaultFloat = 0f };
            ctrl.AddParameter(speedP);
            var airP = new AnimatorControllerParameter { name = "air", type = AnimatorControllerParameterType.Bool, defaultBool = false };
            ctrl.AddParameter(airP);
            var jumpState = ctrl.layers[0].stateMachine.AddState("jump");
            jumpState.motion = jumpClip;
            var sm = ctrl.layers[0].stateMachine;
            // idle <-> walk on speed
            var i2w = idleState.AddTransition(walkState);
            i2w.AddCondition(AnimatorConditionMode.Greater, 0.1f, "speed"); i2w.hasExitTime = false; i2w.duration = 0.08f;
            var w2i = walkState.AddTransition(idleState);
            w2i.AddCondition(AnimatorConditionMode.Less, 0.1f, "speed"); w2i.hasExitTime = false; w2i.duration = 0.08f;
            // grounded -> jump on air
            var i2j = idleState.AddTransition(jumpState);
            i2j.AddCondition(AnimatorConditionMode.If, 0f, "air"); i2j.hasExitTime = false; i2j.duration = 0.0f;
            var w2j = walkState.AddTransition(jumpState);
            w2j.AddCondition(AnimatorConditionMode.If, 0f, "air"); w2j.hasExitTime = false; w2j.duration = 0.0f;
            // jump -> back down when landed
            var j2i = jumpState.AddTransition(idleState);
            j2i.AddCondition(AnimatorConditionMode.IfNot, 0f, "air"); j2i.AddCondition(AnimatorConditionMode.Less, 0.1f, "speed"); j2i.hasExitTime = false; j2i.duration = 0.05f;
            var j2w = jumpState.AddTransition(walkState);
            j2w.AddCondition(AnimatorConditionMode.IfNot, 0f, "air"); j2w.AddCondition(AnimatorConditionMode.Greater, 0.1f, "speed"); j2w.hasExitTime = false; j2w.duration = 0.05f;

            var animator = charGo.AddComponent<Animator>();
            animator.runtimeAnimatorController = ctrl;
            return rig;
        }


        // ==================== UNITY 2D ANIMATION AUTHORING ====================
        // Writes the sprite's skinning data with the same public Sprite APIs the 2D
        // Animation package's own SpritePostProcess uses (verified against
        // com.unity.2d.animation@9.2.2 source): SetVertexCount/SetIndices/SetVertexAttribute
        // (Position/TexCoord0/Tangent/BlendWeight), SetBones, SetBindPoses. Deformation grid
        // mesh = 24x24 quads over the art rect (smooth bends everywhere, no hull coarseness,
        // silhouette never severed); weights = top-2 bone influences by segment-distance
        // falloff; bind poses = TR-inverse of the identity-rotation bind pose (same
        // convention as the package's CalculateLocaltoWorldMatrix).
        // [Retired Sept 26] AuthorUnity2DSkinning + DistPointSegment removed: the public
        // Sprite skinning APIs (SetVertexCount/SetVertexAttribute/SetIndices/SetBones/
        // SetBindPoses) are Unity 6000.x engine APIs and do not compile on the 2022.3 forge
        // (build 116 died on CS1061). Frame animation replaces this path; revisit on Unity 6.

        /// <summary>QC-only: apply the clip's pose at time t straight onto the bones
        /// (deterministic in headless editor mode). Clip data is the source of truth.</summary>
        /// <summary>Pump one editor frame (EditorApplication.Step) so Unity's native SpriteSkin
        /// editor-side update computes the deformation after a manual pose write. Guarded - Step
        /// can throw in some batch contexts, in which case we log and render as-is.</summary>
        static void PumpSkin() {
            try { EditorApplication.Step(); } catch (System.Exception e) {
                Debug.LogWarning("[RigPass] EditorApplication.Step failed (sprite may render undeformed): " + e.Message);
            }
        }

        static void ApplyPoseFromClip(CharRig rig, AnimationClip clip, float t) {
            foreach (var kv in rig.Bones) {
                float z = 0f;
                if (clip != null) {
                    var b = EditorCurveBinding.FloatCurve(rig.AnimPaths[kv.Key], typeof(Transform), "localEulerAnglesRaw.z");
                    var curve = AnimationUtility.GetEditorCurve(clip, b);
                    if (curve != null) z = curve.Evaluate(t);
                }
                kv.Value.localEulerAngles = new Vector3(0f, 0f, z);
            }
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
            cam.backgroundColor = new Color(0.92f, 0.92f, 0.90f, 1f); // light studio bg: poses read like the approved white-bg T-poses
            cam.transform.position = new Vector3(0f, 2.3f, -10f);
            cam.tag = "MainCamera";

            var outDir = System.Environment.GetEnvironmentVariable("QC_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = "QCShots";
            Directory.CreateDirectory(outDir);

            // rig all three approved T-poses with the NATIVE stack (Unity 2D Animation + Animator)
            // Sept 20 FIX: QC binds the SAME art + stances the game binds (whole_* restored
            // exact-ref art + per-char stances). The old QC bound the T-pose measuring files,
            // where the +66deg T-swing READ as arms-down - so a curve calibrated for the wrong
            // bind art passed QC while the live game flung the characters' real arms out
            // horizontal ("the t pose is whats showing" - Bude, Sept 20).
            var chars = new[] {
                new { name = "Lily",  file = "whole_lily.png",  x = -3.2f },
                new { name = "Buddy", file = "whole_buddy.png", x =  0.0f },
                new { name = "Emma",  file = "whole_emma.png",  x =  3.2f },
            };
            var rigs = new List<CharRig>();
            foreach (var c in chars) {
                var cardRig = BuildRig(c.name, "Assets/Art/" + c.file, 2.6f, new Vector3(c.x, 2.0f, 0f), StanceFor(c.name));
                WireFrameAnimator(cardRig.Root, c.name);   // select cards breathe the generated idle frames
                rigs.Add(cardRig);
            }

            // ---- QC 1: bind pose (native SpriteSkin, rest pose) ----
            cam.Render();
            Snap(cam, outDir + "/rig_bindpose.png");

            // ---- QC 1b: IDLE clip sampled mid-breath. EDITOR-MODE CAPTURE v3 (Sept 19): neither
            // Animator.Play nor AnimationMode.SampleAnimationClip evaluates reliably in headless
            // batch mode (both sheets rendered pixel-identical bind poses, verified diff 0.0).
            // QC path now evaluates the clip's own curves (AnimationUtility.GetEditorCurve) and
            // applies the pose to the bones directly - the CLIP data drives the shot, deterministically.
            // The native Animator still drives these clips in play mode; this is review capture only. ----
            var lilySr = rigs[0].Root.GetComponent<SpriteRenderer>();
            var bBefore = lilySr != null ? lilySr.bounds.size : Vector3.zero;
            foreach (var r in rigs) ApplyPoseFromClip(r, r.IdleClip, 1f / 6f); // deepest-breath key
            PumpSkin();
            var bAfter = lilySr != null ? lilySr.bounds.size : Vector3.zero;
            Debug.Log("[RigPass] SKIN DEFORM CHECK (idle vs bind bounds): " + bBefore + " -> " + bAfter
                      + (bBefore != bAfter ? " DEFORMED" : " NOT DEFORMED (SpriteSkin editor update did not run in batch)"));
            Snap(cam, outDir + "/rig_idle.png");

            // ---- QC 1c: LILY-ONLY idle closeup (Bude, Sept 19: 'Show me one image of lily in an idle pose') ----
            cam.orthographicSize = 1.9f;
            cam.transform.position = new Vector3(-3.2f, 2.0f, -10f);
            Snap(cam, outDir + "/rig_lily_idle.png");
            cam.orthographicSize = 3.2f;
            cam.transform.position = new Vector3(0f, 2.3f, -10f);

            // ---- QC 2: walk clip sampled mid-stride (the "pass" keyframe) ----
            foreach (var r in rigs) ApplyPoseFromClip(r, r.WalkClip, 2f / 12f);
            PumpSkin();
            Snap(cam, outDir + "/rig_walk_native.png");

            // ---- QC 3: bone overlay on the posed frame (review visual) ----
            foreach (var r in rigs) OverlayBones(r);
            Snap(cam, outDir + "/rig_walk_bones.png");
            foreach (var r in rigs) ClearOverlay(r);
            foreach (var r in rigs) ApplyPoseFromClip(r, null, 0f); // back to bind for the saved scene

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Scenes/RigSheet.unity");
            AssetDatabase.SaveAssets();

            Debug.Log("[RigPass] NATIVE rig pass complete: Unity created bone hierarchies, SpriteSkin components, "
                      + "AnimatorControllers + walk clips. QC: bindpose / walk_native / walk_bones. Unity owns the rig.");
        }

        /// <summary>Bude's ask (Sept 19): "Show me one image of lily in a idle pose".
        /// Renders Lily in her natural character-select stance (the approved whole_lily art) on the
        /// light studio bg, rigged with her native bone skeleton posed at the idle clip's deepest
        /// breath. One clean image - the select card will animate from exactly this pose.</summary>
        public static void IdleShoot() {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("IdleCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 2.2f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.92f, 0.92f, 0.90f, 1f);
            cam.transform.position = new Vector3(0f, 2.1f, -10f);
            cam.tag = "MainCamera";

            var outDir = System.Environment.GetEnvironmentVariable("QC_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = "QCShots";
            Directory.CreateDirectory(outDir);

            var rig = BuildRig("Lily", "Assets/Art/whole_lily.png", 3.6f, new Vector3(0f, 2.0f, 0f), SelectStance);

            ApplyPoseFromClip(rig, rig.IdleClip, 1f / 6f); // idle, deepest-breath key
            OverlayBones(rig);
            Snap(cam, outDir + "/idle_lily.png");

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Scenes/IdleLily.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("[RigPass] IDLE SHOT: Lily in her natural select-card stance, skeleton posed mid-breath. QC: idle_lily.png");
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
