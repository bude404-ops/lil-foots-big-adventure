// LIL FOOTS: Big Adventure — Unity 2D TEST BUILD (v1)
// Bude's art: Lily rigged from his EXACT ref art (paper-doll cut, no redraws).
// Runtime-constructed scene: rig + procedural animation + clean map + effects.
using UnityEngine;

public static class RigMath
{
    public const float PPU = 100f;
    public const float IMG_CENTER_X = 173f;
    public const float IMG_GROUND_Y = 460f;

    public static Vector2 ImgToWorld(float imgX, float imgY)
    {
        return new Vector2((imgX - IMG_CENTER_X) / PPU, (IMG_GROUND_Y - imgY) / PPU);
    }
}

public class Boot : MonoBehaviour
{
    public static Boot I;
    [HideInInspector] public PlayerRig player;
    [HideInInspector] public PlayerController controller;
    [HideInInspector] public FX fx;
    [HideInInspector] public WorldBuilder world;
    [HideInInspector] public Camera cam;

    public static bool built = false;

    void Awake()
    {
        if (!built) BuildAll();
    }

    public static void BuildAll()
    {
        if (built) return;
        built = true;
        var go = new GameObject("BOOT");
        var b = go.AddComponent<Boot>();
        I = b;

        b.cam = Camera.main;
        if (b.cam == null)
        {
            var cgo = new GameObject("Cam");
            b.cam = cgo.AddComponent<Camera>();
            cgo.tag = "MainCamera";
        }
        b.cam.orthographic = true;
        b.cam.orthographicSize = 7.2f;
        b.cam.clearFlags = CameraClearFlags.SolidColor;
        b.cam.backgroundColor = new Color(0.83f, 0.80f, 0.68f);
        b.cam.transform.position = new Vector3(0, 6.4f, -10);

        b.world = WorldBuilder.Build();
        b.fx = FX.Build();
        b.player = PlayerRig.Build("Lily", "lily");
        b.controller = b.player.gameObject.AddComponent<PlayerController>();
        b.controller.Init(b.player, b.fx);

        b.BuildHUD();
    }

    void BuildHUD()
    {
        var cvgo = new GameObject("Canvas", typeof(UnityEngine.RectTransform));
        var cv = cvgo.AddComponent<UnityEngine.Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = cvgo.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(780, 1690);
        scaler.matchWidthOrHeight = 1f;

        var t = MakeText(cvgo.transform, "LIL FOOTS  -  UNITY TEST v1", 46,
            new Vector2(390, 90), new Vector2(0.5f, 1f), new Color(0.09f, 0.25f, 0.18f));
        t.fontStyle = FontStyle.Bold;
        MakeText(cvgo.transform, "A / D to run  -  SPACE to jump\n(phone: hold lower left/right, tap JUMP)",
            28, new Vector2(390, 1690 - 70), new Vector2(0.5f, 0f), new Color(0.09f, 0.25f, 0.18f, 0.9f));

        var jump = MakeTouchButton(cvgo.transform, "JUMP", new Vector2(600, 150), new Vector2(150, 150));
        if (controller != null) controller.jumpButtonRect = jump;
    }

    UnityEngine.UI.Text MakeText(Transform parent, string txt, int size, Vector2 pos, Vector2 anchor, Color c)
    {
        var go = new GameObject("txt", typeof(UnityEngine.RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<UnityEngine.RectTransform>();
        rt.sizeDelta = new Vector2(780, 60);
        rt.anchoredPosition = pos; rt.anchorMin = anchor; rt.anchorMax = anchor;
        var t = go.AddComponent<UnityEngine.UI.Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = txt; t.fontSize = size; t.color = c; t.alignment = TextAnchor.MiddleCenter;
        return t;
    }

    UnityEngine.RectTransform MakeTouchButton(Transform parent, string label, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("btn-" + label, typeof(UnityEngine.RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<UnityEngine.RectTransform>();
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f);
        var img = go.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.06f, 0.18f, 0.13f, 0.65f);
        var t = go.AddComponent<UnityEngine.UI.Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = label; t.fontSize = 34; t.color = new Color(0.95f, 0.98f, 0.85f);
        t.alignment = TextAnchor.MiddleCenter;
        return rt;
    }

    void LateUpdate()
    {
        if (player != null)
        {
            var px = player.transform.position.x;
            var py = player.transform.position.y;
            cam.transform.position = new Vector3(px, py + 5.6f, -10);
        }
    }
}
