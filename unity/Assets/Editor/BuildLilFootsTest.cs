#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LilFoots.EditorTools
{
    public static class BuildLilFootsTest
    {
        const string ART = "Assets/Art/";

        static Sprite Load(string file, float px, float py)
        {
            string path = ART + file;
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 100f;
                ti.filterMode = FilterMode.Bilinear;
                ti.mipmapEnabled = false;
                ti.spritePivot = new Vector2(px, py);
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static GameObject SpriteGo(string name, Sprite s, Vector3 pos, float width, int order, Color tint)
        {
            GameObject go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.sortingOrder = order; sr.color = tint;
            float f = width / s.bounds.size.x;
            go.transform.localScale = new Vector3(f, f, 1f);
            go.transform.position = pos;
            return go;
        }

        static Material ParticleMat(string name, Sprite s)
        {
            string path = "Assets/Art/mat_" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Sprites/Default"));
                m.mainTexture = s.texture;
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }

        static ParticleSystemRenderer SetupParticles(GameObject go, Sprite s, string matName, int order)
        {
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = ParticleMat(matName, s);
            psr.sortingOrder = order;
            return psr;
        }

        static ParticleSystem MakeDust(Transform parent, Sprite puff)
        {
            GameObject go = new GameObject("DustPuff");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 5f; main.loop = true; main.playOnAwake = false;
            main.startLifetime = 0.5f; main.startSpeed = new ParticleSystem.MinMaxCurve(2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.gravityModifier = 0.12f; main.maxParticles = 500; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.94f, 0.96f, 0.87f, 0.95f));
            var em = ps.emission; em.rateOverTime = 0f; em.enabled = true;
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.18f;
            var sol = ps.sizeOverLifetime; sol.enabled = true;
            AnimationCurve cu = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.35f, 1.3f), new Keyframe(1f, 0.05f));
            sol.size = new ParticleSystem.MinMaxCurve(1f, cu);
            var rol = ps.rotationOverLifetime; rol.enabled = true; rol.z = new ParticleSystem.MinMaxCurve(200f);
            SetupParticles(go, puff, "puff", 40);
            return ps;
        }

        [MenuItem("LilFoots/Build WebGL Test")]
        public static void Build()
        {
            // ---- sprites ----
            Sprite bgplate = Load("art_bgplate.jpg", 0.5f, 0.5f);
            Sprite plat    = Load("art_plat.png", 0.5f, 0.5f);
            Sprite cedar   = Load("art_cedar.png", 0.5f, 0f);
            Sprite fg      = Load("art_fg.png", 0.5f, 0.5f);
            Sprite token   = Load("art_token.png", 0.5f, 0.5f);
            Sprite leaf    = Load("art_leaf.png", 0.5f, 0.5f);
            Sprite spark   = Load("art_spark.png", 0.5f, 0.5f);
            Sprite puff    = Load("art_puff.png", 0.5f, 0.5f);

            Sprite[] bodies = { Load("rig_lily_body.png", 0.5f, 0f), Load("rig_buddy_body.png", 0.5f, 0f), Load("rig_emma_body.png", 0.5f, 0f) };
            Sprite[] feetL  = { Load("rig_lily_footL.png", 0.5f, 1f), Load("rig_buddy_footL.png", 0.5f, 1f), Load("rig_emma_footL.png", 0.5f, 1f) };
            Sprite[] feetR  = { Load("rig_lily_footR.png", 0.5f, 1f), Load("rig_buddy_footR.png", 0.5f, 1f), Load("rig_emma_footR.png", 0.5f, 1f) };

            Scene sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- camera ----
            GameObject camGo = new GameObject("Main Camera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 5.1f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.96f, 0.92f, 0.84f, 1f);
            cam.transform.position = new Vector3(6f, 3.9f, -10f);
            camGo.tag = "MainCamera";
            camGo.AddComponent<AudioListener>();

            GameObject light = new GameObject("Light");
            light.AddComponent<Light>().type = LightType.Directional;

            // ---- LAYER 1-2: far plate, two copies, seamless wrap ----
            GameObject bgBand = new GameObject("BGPlate");
            for (int c = 0; c < 2; c++)
            {
                var g = SpriteGo("bg" + c, bgplate, new Vector3(17f + c * 34f, 14f, 50f), 34f, -50, Color.white);
                g.transform.SetParent(bgBand.transform, true);
            }
            var bp = bgBand.AddComponent<LilFoots.Parallax>(); bp.factor = 0.05f; bp.spanW = 34f;

            // ---- LAYER 3: midground cedars (parallax 0.35), two copies ----
            GameObject midBand = new GameObject("MidTrees");
            for (int c = 0; c < 2; c++)
            {
                for (int i = 0; i < 10; i++)
                {
                    Vector3 pos = new Vector3(2f + i * 3.4f + c * 34f, 3.1f, 30f);
                    Color tint = (i % 2 == 0) ? new Color(0.62f, 0.82f, 0.64f) : new Color(0.55f, 0.74f, 0.58f);
                    var t = SpriteGo("midCedar" + c + "_" + i, cedar, pos, 1.6f + (i % 3) * 0.25f, -30, tint);
                    t.transform.SetParent(midBand.transform, true);
                }
            }
            var mbp = midBand.AddComponent<LilFoots.Parallax>(); mbp.factor = 0.35f; mbp.spanW = 34f;

            // ---- gameplay: ground + platforms ----
            List<BoxCollider2D> plats = new List<BoxCollider2D>();
            float gW = plat.bounds.size.x, gH = plat.bounds.size.y;

            GameObject ground = new GameObject("Ground");
            for (int i = 0; i < 5; i++)
            {
                GameObject seg = new GameObject("gseg" + i);
                var sr = seg.AddComponent<SpriteRenderer>();
                sr.sprite = plat; sr.sortingOrder = 0;
                seg.transform.position = new Vector3(4f + i * (gW - 0.02f), -gH / 2f, 0f);
                seg.transform.SetParent(ground.transform, true);
            }
            GameObject gCol = new GameObject("GroundCol");
            var gc = gCol.AddComponent<BoxCollider2D>();
            gc.offset = new Vector2(21.58f, -gH / 2f);
            gc.size = new Vector2(48f, gH);
            plats.Add(gc);

            Vector3[] pp = { new Vector3(8f, 3.1f, 0f), new Vector3(14.5f, 4.6f, 0f), new Vector3(21f, 3.1f, 0f), new Vector3(27f, 5.6f, 0f) };
            foreach (var p in pp)
            {
                GameObject po = new GameObject("plat");
                var sr = po.AddComponent<SpriteRenderer>();
                sr.sprite = plat; sr.sortingOrder = 5;
                po.transform.position = p;
                po.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
                var bc = po.AddComponent<BoxCollider2D>();
                bc.size = new Vector2(gW * 0.92f, 0.24f);
                bc.offset = new Vector2(0f, gH / 2f - 0.12f);
                plats.Add(bc);
            }

            SpriteGo("cedarA", cedar, new Vector3(3f, 0.02f, 2f), 2.1f, 10, Color.white);
            SpriteGo("cedarB", cedar, new Vector3(25.5f, 0.02f, 2f), 2.4f, 10, Color.white);
            SpriteGo("cedarC", cedar, new Vector3(12.7f, 0.02f, 2f), 1.7f, 10, new Color(0.92f, 1f, 0.92f));

            // ---- player rig ----
            float footH = 0.24f; // ankle line (rig-info.json: lily 0.24, buddy/emma 0.23)
            GameObject player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = new Vector3(4f, 0f, 0f);

            GameObject body = new GameObject("body");
            body.transform.SetParent(player.transform, false);
            var bsr = body.AddComponent<SpriteRenderer>();
            bsr.sprite = bodies[0]; bsr.sortingOrder = 20;
            body.transform.localPosition = new Vector3(0f, footH, 0f);

            GameObject fl = new GameObject("footL");
            fl.transform.SetParent(player.transform, false);
            var flsr = fl.AddComponent<SpriteRenderer>();
            flsr.sprite = feetL[0]; flsr.sortingOrder = 19;
            fl.transform.localPosition = new Vector3(-0.27f, footH, 0f);

            GameObject fr = new GameObject("footR");
            fr.transform.SetParent(player.transform, false);
            var frsr = fr.AddComponent<SpriteRenderer>();
            frsr.sprite = feetR[0]; frsr.sortingOrder = 18;
            fr.transform.localPosition = new Vector3(0.295f, footH, 0f);

            GameObject rigAssetsGo = new GameObject("RigAssets");
            var ra = rigAssetsGo.AddComponent<LilFoots.RigAssets>();
            ra.bodies = bodies; ra.feetL = feetL; ra.feetR = feetR;

            var rig = player.AddComponent<LilFoots.PlayerRig>();
            rig.bodyT = body.transform; rig.footLT = fl.transform; rig.footRT = fr.transform;
            rig.bodyR = bsr; rig.footLR = flsr; rig.footRR = frsr;

            var pc = player.AddComponent<BoxCollider2D>();
            pc.size = new Vector2(1.3f, 2.0f); pc.isTrigger = true;

            ParticleSystem dust = MakeDust(player.transform, puff);

            var ctl = player.AddComponent<LilFoots.PlayerController>();
            ctl.platforms = plats; ctl.rig = rig; ctl.dust = dust; ctl.cam = cam;
            ctl.minX = 1f; ctl.maxX = 31f;

            // ---- tokens ----
            Vector3[] tp = { new Vector3(6.5f, 1.6f, 0f), new Vector3(8f, 4.6f, 0f), new Vector3(14.5f, 6.1f, 0f),
                             new Vector3(18f, 1.6f, 0f), new Vector3(21f, 4.6f, 0f), new Vector3(27f, 7.1f, 0f) };
            foreach (var t in tp)
            {
                GameObject to = SpriteGo("token", token, t, 0.55f, 25, Color.white);
                var bc = to.AddComponent<BoxCollider2D>(); bc.isTrigger = true; bc.size = new Vector2(20f, 20f);
                var bt = to.AddComponent<LilFoots.BigToken>();
                GameObject sp = new GameObject("sparkle");
                sp.transform.SetParent(to.transform, false);
                var ps = sp.AddComponent<ParticleSystem>();
                var main = ps.main; main.playOnAwake = false; main.startLifetime = 0.5f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f); main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
                main.maxParticles = 200; main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.79f, 0.3f, 1f));
                var em = ps.emission; em.rateOverTime = 0f;
                SetupParticles(sp, spark, "spark", 30);
                bt.sparkle = ps;
            }

            // ---- ambient falling leaves ----
            GameObject lv = new GameObject("Leaves");
            var lps = lv.AddComponent<ParticleSystem>();
            var lmain = lps.main;
            lmain.duration = 8f; lmain.loop = true; lmain.playOnAwake = true;
            lmain.startLifetime = 7f; lmain.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            lmain.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.28f);
            lmain.gravityModifier = 0.04f; lmain.maxParticles = 80;
            var lem = lps.emission; lem.rateOverTime = 3f;
            var lsh = lps.shape; lsh.shapeType = ParticleSystemShapeType.Box; lsh.scale = new Vector3(40f, 2f, 1f);
            lv.transform.position = new Vector3(15f, 11f, 5f);
            SetupParticles(lv, leaf, "leaf", 8);
            var lrot = lps.rotationOverLifetime; lrot.enabled = true; lrot.z = new ParticleSystem.MinMaxCurve(180f);

            // ---- LAYER 5: fg undergrowth (parallax 1.3), two copies of a 5-tile band ----
            GameObject fgBand = new GameObject("FGBand");
            for (int c = 0; c < 2; c++)
            {
                for (int i = 0; i < 5; i++)
                {
                    var g = SpriteGo("fg" + c + "_" + i, fg, new Vector3(5.12f + i * 10.24f + c * 51.2f, 0.1f, 8f), 10.24f, 50, Color.white);
                    g.transform.SetParent(fgBand.transform, true);
                }
            }
            var fgp = fgBand.AddComponent<LilFoots.Parallax>(); fgp.factor = 1.3f; fgp.spanW = 51.2f;

            // ---- HUD ----
            GameObject hud = new GameObject("HUD");
            hud.AddComponent<LilFoots.TestHUD>();

            // ---- save + build ----
            string scenePath = "Assets/Scenes/lilfoots-test.unity";
            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(sc, scenePath);

            EditorBuildSettingsScene[] ebss = { new EditorBuildSettingsScene(scenePath, true) };
            EditorBuildSettings.scenes = ebss;

            PlayerSettings.companyName = "Bigfoot404 LLC";
            PlayerSettings.productName = "Lil Foots Big Adventure - Unity Test";

            // compression disabled (reflection-safe): GitHub Pages serves no Content-Encoding
            PropertyInfo prop = typeof(PlayerSettings.WebGL).GetProperty("compressionFormat");
            if (prop != null)
            {
                object val = System.Enum.Parse(prop.PropertyType, "Disabled");
                prop.SetValue(null, val, null);
            }

            string outDir = "Build";
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            var report = BuildPipeline.BuildPlayer(new string[] { scenePath }, outDir, BuildTarget.WebGL, BuildOptions.None);
            bool ok = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
            File.WriteAllText("build_result.txt", ok ? "BUILD OK size=" + report.summary.totalSize : "BUILD FAILED");
            Debug.Log(ok ? "WEBGL BUILD OK size=" + report.summary.totalSize : "WEBGL BUILD FAILED");
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
#endif
