using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LilFoots.EditorTools
{
    /// <summary>Prop Placer - BudE (Oct 20: Fallout-4-settlement ease of use). The studio's
    /// placement tool: open the palette, click a piece, click the world - it drops in
    /// auto-sized, auto-layered, base snapped to the ground underneath. Click-drag moves,
    /// R rotates 15 degrees, Delete removes, Ctrl+Z undoes. Everything placed is written to
    /// a sidecar file (map_*_props.json) the builder reads on rebuild, so decorations survive
    /// course rebuilds. Depth menu: Background sits in the far plane like setpieces, Mid is
    /// in-scene dressing, Front plays in front of the action.</summary>
    public class LilFootsPropPlacer : EditorWindow
    {
        class Piece { public string file; public Sprite sprite; public float height; public Texture2D tex; }

        static LilFootsPropPlacer win;
        List<Piece> palette;
        int selPiece = -1;
        int depth = 0;                 // 0 Background, 1 Mid, 2 Front
        bool eraseMode;
        LilFootsProp selProp;          // selected placed prop
        Vector2 scroll;
        string lastMsg = "Pick a piece, then click the ground in the Scene view.";
        const float SNAP = 0.10f;

        [MenuItem("Tools/Lil Foots/Prop Placer")]
        static void OpenWin()
        {
            win = GetWindow<LilFootsPropPlacer>("Prop Placer");
            win.minSize = new Vector2(320, 240);
        }

        void OnEnable()
        {
            win = this;
            SceneView.duringSceneGui += OnScene;
            BuildPalette();
        }
        void OnDisable()
        {
            SceneView.duringSceneGui -= OnScene;
            win = null;
        }

        // ---------- catalog ----------
        static readonly (string, float)[] HeightTable = {
            ("cedar_giant", 6.5f), ("rock_spine", 4.0f), ("fern_giant", 5.5f),
            ("fork_snag", 6.0f), ("log_hollow", 2.2f), ("shore_bank", 3.0f),
            ("cliff_loam", 4.5f), ("totem", 1.7f), ("floor_ferns", 1.2f),
            ("water_stream", 1.6f), ("slope_grass_gentle", 3.0f),
            ("slope_grass_steep", 3.0f), ("slope_loam", 3.0f), ("slope_stone_mossy", 3.0f),
            ("floor_forest", 1.5f)
        };
        static float HeightFor(string file)
        {
            foreach (var (k, h) in HeightTable)
                if (file.Contains(k)) return h;
            return 3.0f;
        }
        static readonly string[] Excluded = { "t_branch_L", "t_branch_R", "t_flag_gate", "_style_reference" };

        void BuildPalette()
        {
            palette = new List<Piece>();
            foreach (string dir in new[] { "Assets/Art/Props", "Assets/Art/MossveilKit", "Assets/Art/FernHollowKit" }) {
                if (!Directory.Exists(dir)) continue;
                foreach (string path in Directory.GetFiles(dir, "*.png").OrderBy(p => p)) {
                    string rel = path.Substring("Assets/Art/".Length).Replace("\\", "/");
                    if (Excluded.Any(e => rel.Contains(e))) continue;
                    var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (s == null) continue;
                    palette.Add(new Piece { file = rel, sprite = s, tex = s.texture, height = HeightFor(rel) });
                }
            }
            lastMsg = palette.Count + " pieces loaded.";
        }

        int OrderForDepth() { return depth == 0 ? -58 : depth == 1 ? -20 : 6; }

        // ---------- scene ----------
        void OnScene(SceneView sv)
        {
            if (Application.isPlaying) return;
            Event ev = Event.current;

            var props = new List<LilFootsProp>(Object.FindObjectsByType<LilFootsProp>(FindObjectsInactive.Include, FindObjectsSortMode.None));

            // selection highlight for the selected placed prop
            if (selProp != null && ev.type == EventType.Repaint) {
                var r = selProp.GetComponent<SpriteRenderer>();
                if (r != null) {
                    Handles.color = new Color(1f, 0.6f, 0.1f, 0.9f);
                    var b = r.bounds;
                    Handles.DrawWireDisc(new Vector3(b.center.x, b.min.y, 0f), Vector3.forward, 0.3f);
                }
            }

            if (ev.type == EventType.MouseDown && ev.button == 0) {
                LilFootsProp hit = HitProp(ev.mousePosition, props);
                if (hit != null && eraseMode) {
                    Undo.DestroyObjectImmediate(hit.gameObject);
                    selProp = null;
                    SaveSidecar();
                    ev.Use(); SceneView.RepaintAll(); return;
                }
                if (hit != null) {
                    selProp = hit; ev.Use();
                    GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
                    Undo.RecordObject(hit.transform, "Move Prop");
                    return;
                }
                if (!eraseMode && selPiece >= 0) { Place(ev.mousePosition); ev.Use(); return; }
            }

            if (ev.type == EventType.MouseDrag && GUIUtility.hotControl != 0 && selProp != null) {
                Vector3 w = GUIToWorld(ev.mousePosition);
                w.x = Mathf.Round(w.x / SNAP) * SNAP;
                selProp.transform.position = new Vector3(w.x, w.y, 0f);
                SceneView.RepaintAll(); ev.Use();
            }
            if (ev.type == EventType.MouseUp && GUIUtility.hotControl != 0 && selProp != null) {
                Reground(selProp);
                GUIUtility.hotControl = 0;
                SaveSidecar();
                SceneView.RepaintAll(); ev.Use();
            }

            if (selProp != null && ev.type == EventType.KeyDown) {
                if (ev.keyCode == KeyCode.R) {
                    Undo.RecordObject(selProp.transform, "Rotate Prop");
                    selProp.rot = selProp.transform.eulerAngles.z + 15f;
                    selProp.transform.rotation = Quaternion.Euler(0f, 0f, selProp.rot);
                    SaveSidecar(); ev.Use(); SceneView.RepaintAll();
                } else if (ev.keyCode == KeyCode.Delete || ev.keyCode == KeyCode.Backspace) {
                    Undo.DestroyObjectImmediate(selProp.gameObject);
                    selProp = null;
                    SaveSidecar(); ev.Use(); SceneView.RepaintAll();
                }
            }
        }

        LilFootsProp HitProp(Vector2 mouse, List<LilFootsProp> props)
        {
            foreach (var p in props) {
                var r = p.GetComponent<SpriteRenderer>();
                if (r == null) continue;
                Bounds b = r.bounds;
                Vector3 c0 = HandleUtility.WorldToGUIPoint(new Vector3(b.min.x, b.min.y, 0f));
                Vector3 c1 = HandleUtility.WorldToGUIPoint(new Vector3(b.max.x, b.max.y, 0f));
                Rect gui = Rect.MinMaxRect(Mathf.Min(c0.x, c1.x), Mathf.Min(c0.y, c1.y),
                                           Mathf.Max(c0.x, c1.x), Mathf.Max(c0.y, c1.y));
                if (gui.Contains(mouse)) return p;
            }
            return null;
        }

        void Place(Vector2 mouse)
        {
            Piece pc = palette[selPiece];
            Vector3 w = GUIToWorld(mouse);
            var go = new GameObject("Prop_" + Path.GetFileNameWithoutExtension(pc.file));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = pc.sprite;
            float f = pc.height / pc.sprite.bounds.size.y;
            go.transform.localScale = new Vector3(f, f, 1f);
            go.transform.position = new Vector3(w.x, w.y + pc.height / 2f, 0f);
            sr.sortingOrder = OrderForDepth();
            bool dimmed = depth == 0;
            sr.color = dimmed ? new Color(0.72f, 0.78f, 0.75f, 1f) : Color.white;
            var mp = go.AddComponent<LilFootsProp>();
            mp.file = pc.file; mp.order = sr.sortingOrder; mp.rot = 0f; mp.dim = dimmed ? 1f : 0f;
            Reground(mp);
            Undo.RegisterCreatedObjectUndo(go, "Place Prop");
            selProp = mp;
            lastMsg = "Placed " + pc.file;
            SaveSidecar();
            SceneView.RepaintAll();
        }

        static void Reground(LilFootsProp p)
        {
            var sr = p.GetComponent<SpriteRenderer>();
            if (sr == null) return;
            float h = sr.bounds.size.y;
            Vector3 pos = p.transform.position;
            Physics2D.SyncTransforms();
            var hit = Physics2D.Raycast(new Vector2(pos.x, pos.y + 0.25f), Vector2.down, 60f);
            if (hit.collider != null)
                p.transform.position = new Vector3(pos.x, hit.point.y + h / 2f, 0f);
        }

        static Vector3 GUIToWorld(Vector2 gui)
        {
            Ray r = HandleUtility.GUIPointToWorldRay(gui);
            if (Mathf.Abs(r.direction.z) > 1e-5f) return r.origin + r.direction * (-r.origin.z / r.direction.z);
            return r.origin;
        }

        // ---------- sidecar ----------
        string SidecarPath()
        {
            string spine = EditorPrefs.GetString("LilFoots_TweakSpine", "");
            if (string.IsNullOrEmpty(spine)) {
                string scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
                if (scene.Contains("Map002")) spine = "Assets/LevelData/map_region1_2_spine.json";
                else if (scene.Contains("Map001")) spine = "Assets/LevelData/map_region1_spine.json";
            }
            if (string.IsNullOrEmpty(spine)) return null;
            return spine.Replace("_spine.json", "_props.json");
        }
        void SaveSidecar()
        {
            string path = SidecarPath();
            if (string.IsNullOrEmpty(path)) { lastMsg = "No course file known - prop saved in scene only."; return; }
            var props = Object.FindObjectsByType<LilFootsProp>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var list = new List<object>();
            foreach (var p in props) {
                list.Add(new Dictionary<string, object> {
                    { "file", p.file },
                    { "x", (double)p.transform.position.x },
                    { "y", (double)p.transform.position.y },
                    { "order", (double)p.order },
                    { "rot", (double)p.rot },
                    { "dim", (double)p.dim }
                });
            }
            var root = new Dictionary<string, object> { { "props", list } };
            Directory.CreateDirectory("Assets/LevelData");
            File.WriteAllText(path, MiniJson2.Serialize(root));
            lastMsg = "Saved " + list.Count + " props -> " + Path.GetFileName(path);
        }

        // ---------- window ----------
        void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Pick a piece, click the world to place it (drops in sized + grounded).\n" +
                "Click-drag a placed piece to move, R rotates, Delete removes.\n" +
                "Every placement is saved to the course, so rebuilds keep your decor.",
                MessageType.None);

            EditorGUILayout.BeginHorizontal();
            depth = GUILayout.Toolbar(depth, new[] { "Background", "Mid", "Front" });
            eraseMode = GUILayout.Toggle(eraseMode, "Erase", "Button");
            EditorGUILayout.EndHorizontal();

            if (selPiece >= 0 && GUILayout.Button("Piece: " + Path.GetFileNameWithoutExtension(palette[selPiece].file) + "  (click to deselect)"))
                selPiece = -1;

            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (palette == null || palette.Count == 0) {
                if (GUILayout.Button("Reload palette")) BuildPalette();
            } else {
                const float cell = 84f, pad = 4f;
                int cols = Mathf.Max(1, (int)((position.width - 24f) / (cell + pad)));
                int i = 0;
                while (i < palette.Count) {
                    EditorGUILayout.BeginHorizontal();
                    for (int c = 0; c < cols && i < palette.Count; c++, i++) {
                        int idx = i;
                        GUIStyle st = GUI.skin.button;
                        Rect r = GUILayoutUtility.GetRect(cell, cell, st);
                        bool on = GUI.Toggle(r, selPiece == idx, GUIContent.none, st);
                        if (on && selPiece != idx) { selPiece = idx; SceneView.RepaintAll(); }
                        if (!on && selPiece == idx) selPiece = -1;
                        if (palette[idx].tex != null)
                            GUI.DrawTexture(new Rect(r.x + 6, r.y + 6, r.width - 12, r.height - 18),
                                            palette[idx].tex, ScaleMode.ScaleToFit, true);
                        GUI.Label(new Rect(r.x + 4, r.y + r.height - 16, r.width - 8, 14),
                                  Path.GetFileNameWithoutExtension(palette[idx].file),
                                  EditorStyles.miniLabel);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(lastMsg, EditorStyles.wordWrappedMiniLabel);
        }
    }
}
