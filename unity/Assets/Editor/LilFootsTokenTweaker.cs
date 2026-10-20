using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LilFoots.EditorTools
{
    /// <summary>Token Tweaker - BudE (Oct 20: "I need to be able to edit this stuff easier").
    /// Open Tools > Lil Foots > Token Tweaker, then in the Scene view:
    ///   - DRAG any coin to move it (snaps to 10px grid; Ctrl+Z works)
    ///   - RIGHT-CLICK a coin to delete it
    ///   - turn on ADD MODE and click empty space to drop a new coin
    /// Every change is written back into the course's spine JSON (the same file the
    /// Build Course menu builds from), so a rebuild keeps your edits. No play-mode edits.
    /// Tokens map to spine entries by sibling order (the builder creates them in spine order).</summary>
    public class LilFootsTokenTweaker : EditorWindow
    {
        const float GY = 6.2f;              // matches LilFootsLevelBuilder (px/100)
        const float SNAP = 0.10f;           // 10px grid
        const float PICK_R = 16f;           // GUI px pick radius

        static LilFootsTokenTweaker win;
        List<TokenCollectible> toks;
        int dragIdx = -1, selIdx = -1;
        Vector2 dragOff;
        bool addMode;
        string spinePath;
        string lastMsg = "Open the course scene, then drag coins in the Scene view.";
        Vector2 scroll;

        [MenuItem("Tools/Lil Foots/Token Tweaker")]
        static void OpenWin()
        {
            win = GetWindow<LilFootsTokenTweaker>("Token Tweaker");
            win.minSize = new Vector2(260, 160);
        }

        void OnEnable()
        {
            win = this;
            SceneView.duringSceneGui += OnScene;
        }
        void OnDisable()
        {
            SceneView.duringSceneGui -= OnScene;
            win = null;
        }

        void GuessSpine()
        {
            if (!string.IsNullOrEmpty(spinePath) && File.Exists(spinePath)) return;
            spinePath = EditorPrefs.GetString("LilFoots_TweakSpine", "");
            if (string.IsNullOrEmpty(spinePath) || !File.Exists(spinePath)) {
                string scene = EditorSceneManagerSafe();
                if (scene.Contains("Map002")) spinePath = "Assets/LevelData/map_region1_2_spine.json";
                else if (scene.Contains("Map001")) spinePath = "Assets/LevelData/map_region1_spine.json";
            }
        }
        static string EditorSceneManagerSafe()
        {
            var s = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            return s == null ? "" : s.path;
        }

        void RefreshTokens()
        {
            toks = new List<TokenCollectible>(
                Object.FindObjectsByType<TokenCollectible>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            toks.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            // sibling order only means something if they share a parent; fall back to x order
            bool sameParent = toks.Count < 2 || toks[0].transform.parent == toks[1].transform.parent;
            if (!sameParent) toks.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        }

        // ---------- spine write-back ----------
        static Dictionary<string, object> ReadSpine(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            return MiniJson2.Deserialize(File.ReadAllText(path)) as Dictionary<string, object>;
        }
        void WriteSpine()
        {
            GuessSpine();
            var d = ReadSpine(spinePath);
            if (d == null || !(d["tokens"] is List<object> list)) { lastMsg = "No spine file - changes saved to scene only."; return; }
            for (int i = 0; i < toks.Count && i < list.Count; i++) {
                var t = toks[i];
                var e = list[i] as Dictionary<string, object>;
                if (e == null) continue;
                e["x"] = (double)Mathf.Round(t.transform.position.x * 100f);
                e["y"] = (double)Mathf.Round((2f * GY - t.transform.position.y) * 100f);
            }
            File.WriteAllText(spinePath, MiniJson2.Serialize(d));
            lastMsg = "Saved " + toks.Count + " coins -> " + Path.GetFileName(spinePath);
        }
        void DeleteFromSpine(int idx)
        {
            GuessSpine();
            var d = ReadSpine(spinePath);
            if (d != null && d["tokens"] is List<object> list && idx >= 0 && idx < list.Count) {
                list.RemoveAt(idx);
                File.WriteAllText(spinePath, MiniJson2.Serialize(d));
            }
            lastMsg = "Deleted coin #" + idx;
        }
        void AppendToSpine(float wx, float wy)
        {
            GuessSpine();
            var d = ReadSpine(spinePath);
            if (d == null || !(d["tokens"] is List<object> list)) { lastMsg = "No spine file - coin added to scene only."; return; }
            var e = new Dictionary<string, object> {
                { "x", (double)Mathf.Round(wx * 100f) }, { "y", (double)Mathf.Round((2f * GY - wy) * 100f) }, { "tier", (double)0 }
            };
            list.Add(e);
            File.WriteAllText(spinePath, MiniJson2.Serialize(d));
            lastMsg = "Added coin at " + Mathf.Round(wx * 100f) + "," + Mathf.Round((2f * GY - wy) * 100f);
        }

        // ---------- scene handles ----------
        void OnScene(SceneView sv)
        {
            if (Application.isPlaying) return;
            RefreshTokens();
            Event ev = Event.current;
            int hot = GUIUtility.hotControl;

            for (int i = 0; i < toks.Count; i++) {
                var t = toks[i];
                if (t == null) continue;
                Vector3 wp = t.transform.position;
                int id = GUIUtility.GetControlID("LilFootsTok".GetHashCode() + i, FocusType.Passive);

                if (ev.type == EventType.Repaint) {
                    Handles.color = i == selIdx ? new Color(1f, 0.55f, 0.1f)
                                 : i == dragIdx ? new Color(0.2f, 1f, 1f)
                                 : new Color(1f, 0.85f, 0.2f);
                    float camDist = (sv.camera != null) ? Mathf.Abs(sv.camera.transform.position.z - wp.z) : 10f;
                    Handles.SphereHandleCap(id, wp, Quaternion.identity, 0.14f * camDist * 0.15f, EventType.Repaint);
                    Handles.Label(wp + new Vector3(0f, 0.35f, 0f), "" + i);
                }

                if (ev.type == EventType.MouseDown) {
                    Vector2 gui = HandleUtility.WorldToGUIPoint(wp);
                    if ((gui - ev.mousePosition).magnitude <= PICK_R) {
                        ev.Use();
                        GUIUtility.hotControl = id;
                        if (ev.button == 1) {                       // right-click = delete
                            Undo.DestroyObjectImmediate(t.gameObject);
                            toks.RemoveAt(i);
                            DeleteFromSpine(i);
                            SceneView.RepaintAll();
                            return;
                        }
                        dragIdx = i; selIdx = i;
                        Undo.RecordObject(t.transform, "Move Token");
                        Vector3 m = GUIToWorld(ev.mousePosition, sv);
                        dragOff = wp - m;
                        return;
                    }
                }
            }

            if (ev.type == EventType.MouseDrag && hot != 0 && dragIdx >= 0 && dragIdx < toks.Count) {
                var t = toks[dragIdx];
                if (t == null) { dragIdx = -1; return; }
                Vector3 m = GUIToWorld(ev.mousePosition, sv) + (Vector3)dragOff;
                m.x = Mathf.Round(m.x / SNAP) * SNAP;
                m.y = Mathf.Round(m.y / SNAP) * SNAP;
                t.transform.position = new Vector3(m.x, m.y, 0f);
                SceneView.RepaintAll();
                ev.Use();
            }
            if (ev.type == EventType.MouseUp && hot != 0) {
                bool wasDrag = dragIdx >= 0;
                dragIdx = -1;
                GUIUtility.hotControl = 0;
                if (wasDrag) { WriteSpine(); SceneView.RepaintAll(); ev.Use(); }
            }

            // add mode: click empty space to drop a coin on the click point
            if (addMode && ev.type == EventType.MouseDown && ev.button == 0 && hot == 0) {
                foreach (var t in toks) {
                    Vector2 gui = HandleUtility.WorldToGUIPoint(t.transform.position);
                    if ((gui - ev.mousePosition).magnitude <= PICK_R) return;   // clicked a coin, don't stack
                }
                Vector3 w = GUIToWorld(ev.mousePosition, sv);
                w.x = Mathf.Round(w.x / SNAP) * SNAP; w.y = Mathf.Round(w.y / SNAP) * SNAP;
                var go = new GameObject("Token_" + Mathf.RoundToInt(w.x * 100f));
                Transform parent = toks.Count > 0 && toks[0] != null ? toks[0].transform.parent : null;
                if (parent != null) go.transform.SetParent(parent, false);
                go.transform.position = w;
                var cc = go.AddComponent<CircleCollider2D>(); cc.isTrigger = true; cc.radius = 0.34f;
                go.AddComponent<TokenCollectible>().tier = 0;
                Undo.RegisterCreatedObjectUndo(go, "Add Token");
                AppendToSpine(w.x, w.y);
                ev.Use();
                SceneView.RepaintAll();
            }
        }

        static Vector3 GUIToWorld(Vector2 gui, SceneView sv)
        {
            Ray r = HandleUtility.GUIPointToWorldRay(gui);
            if (Mathf.Abs(r.direction.z) > 1e-5f) return r.origin + r.direction * (-r.origin.z / r.direction.z);
            return r.origin;
        }

        // ---------- window UI ----------
        void OnGUI()
        {
            GuessSpine();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox("Drag coins in the Scene view. Right-click deletes. Add mode drops new ones. " +
                                    "Edits save straight into the course spine, so rebuilds keep them.", MessageType.None);
            addMode = EditorGUILayout.Toggle("Add Coin Mode", addMode);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(addMode ? "CLICK SCENE TO DROP" : "Add mode: OFF")) addMode = !addMode;
            if (GUILayout.Button("Save Now")) { RefreshTokens(); WriteSpine(); }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Spine file:");
            spinePath = EditorGUILayout.TextField(spinePath);
            RefreshTokens();
            EditorGUILayout.LabelField("Coins in scene:", toks.Count.ToString());
            EditorGUILayout.LabelField("Selected:", selIdx >= 0 ? ("#" + selIdx) : "none (drag to select)");
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(lastMsg, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
        }
    }

    /// <summary>Self-contained JSON read/write for the tweaker (the builder's MiniJson is read-only).</summary>
    public static class MiniJson2
    {
        public static object Deserialize(string json)
        {
            int pos = 0;
            object v = ParseValue(json, ref pos);
            return v;
        }
        static void Skip(string s, ref int i) { while (i < s.Length && (s[i]==' '||s[i]=='\n'||s[i]=='\r'||s[i]=='\t')) i++; }
        static object ParseValue(string s, ref int i)
        {
            Skip(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{') { i++; var d = new Dictionary<string, object>(); Skip(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; return d; }
                while (i < s.Length) { Skip(s, ref i);
                    string k = ParseString(s, ref i); Skip(s, ref i); i++; // colon
                    d[k] = ParseValue(s, ref i); Skip(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == '}') { i++; return d; }
                    break; }
                return d; }
            if (c == '[') { i++; var l = new List<object>(); Skip(s, ref i);
                if (i < s.Length && s[i] == ']') { i++; return l; }
                while (i < s.Length) { l.Add(ParseValue(s, ref i)); Skip(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == ']') { i++; return l; }
                    break; }
                return l; }
            if (c == '"') return ParseString(s, ref i);
            if (s.StartsWith("true", i)) { i += 4; return true; }
            if (s.StartsWith("false", i)) { i += 5; return false; }
            if (s.StartsWith("null", i)) { i += 4; return null; }
            int j = i;
            while (j < s.Length && (char.IsDigit(s[j]) || s[j]=='-' || s[j]=='+' || s[j]=='.' || s[j]=='e' || s[j]=='E')) j++;
            double num; double.TryParse(s.Substring(i, j - i), System.Globalization.NumberStyles.Float,
                                         System.Globalization.CultureInfo.InvariantCulture, out num);
            i = j; return num;
        }
        static string ParseString(string s, ref int i)
        {
            i++; var b = new StringBuilder();
            while (i < s.Length) {
                char ch = s[i++];
                if (ch == '"') break;
                if (ch == '\\' && i < s.Length) {
                    char e = s[i++];
                    if (e == 'n') b.Append('\n'); else if (e == 't') b.Append('\t');
                    else if (e == 'u') { if (i + 4 <= s.Length) { b.Append((char)System.Convert.ToInt32(s.Substring(i,4),16)); i += 4; } }
                    else b.Append(e);
                } else b.Append(ch);
            }
            return b.ToString();
        }

        public static string Serialize(object o)
        {
            var sb = new StringBuilder();
            Write(o, sb, 0);
            sb.AppendLine();
            return sb.ToString();
        }
        static void Write(object o, StringBuilder sb, int ind)
        {
            string pad = new string(' ', ind);
            var d = o as Dictionary<string, object>;
            if (d != null) {
                sb.Append("{\n"); int n = 0;
                foreach (var kv in d) {
                    sb.Append(pad); sb.Append("  \""); sb.Append(kv.Key); sb.Append("\": ");
                    Write(kv.Value, sb, ind + 2);
                    if (++n < d.Count) sb.Append(',');
                    sb.Append('\n');
                }
                sb.Append(pad); sb.Append('}'); return;
            }
            var l = o as List<object>;
            if (l != null) {
                sb.Append("[\n"); int n2 = 0;
                foreach (var v in l) {
                    sb.Append(pad); sb.Append("  ");
                    Write(v, sb, ind + 2);
                    if (++n2 < l.Count) sb.Append(',');
                    sb.Append('\n');
                }
                sb.Append(pad); sb.Append(']'); return;
            }
            if (o is string) { sb.Append('"'); sb.Append(((string)o).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n")); sb.Append('"'); return; }
            if (o is bool) { sb.Append(((bool)o) ? "true" : "false"); return; }
            if (o is double) {
                double dv = (double)o;
                if (dv == System.Math.Floor(dv) && System.Math.Abs(dv) < 1e15) sb.Append(System.Math.Floor(dv).ToString());
                else sb.Append(dv.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return;
            }
            if (o == null) sb.Append("null"); else sb.Append('"').Append(o).Append('"');
        }
    }
}
