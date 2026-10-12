using UnityEngine;

// Lil Foots v3 touch input bridge (BIGagent404).
// Static state that gameplay scripts read; keyboard input still works everywhere.
public static class TouchInput
{
    public static float moveX;      // -1..1, 0 = no touch input
    public static bool jumpPressed; // true for one frame when the touch JUMP button goes down
    public static bool jumpHeld;
    public static bool downHeld;    // drop-through / crouch hold
    public static bool touchUI;     // true once a touch has been seen (UI becomes visible)
}

// On-screen mobile controls: LEFT/RIGHT buttons bottom-left, JUMP + DROP buttons bottom-right.
// Auto-attaches itself to every scene at runtime — no scene wiring needed.
// Visible on mobile devices, or on desktop after the first touch; hidden otherwise.
public class TouchControls : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindObjectOfType<TouchControls>() == null)
        {
            var go = new GameObject("LilFoots TouchControls");
            go.AddComponent<TouchControls>();
            DontDestroyOnLoad(go);
        }
    }

    private Rect rLeft, rRight, rJump, rDown;
    private GUIStyle btnStyle;

    void Update()
    {
        // clear last frame's state
        TouchInput.moveX = 0f;
        TouchInput.jumpPressed = false;
        TouchInput.jumpHeld = false;
        TouchInput.downHeld = false;

        if (Input.touchCount > 0) TouchInput.touchUI = true;
        bool visible = TouchInput.touchUI || Application.isMobilePlatform;
        if (!visible) return;

        ComputeRects();
        bool left = false, right = false, jump = false, down = false;
        bool jumpBegan = false;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) continue;
            Vector2 p = t.position; // GUI coords: bottom-left origin, matches Contains()
            if (rLeft.Contains(p)) left = true;
            else if (rRight.Contains(p)) right = true;
            else if (rJump.Contains(p)) { jump = true; if (t.phase == TouchPhase.Began) jumpBegan = true; }
            else if (rDown.Contains(p)) down = true;
        }

        TouchInput.moveX = (right ? 1f : 0f) - (left ? 1f : 0f);
        TouchInput.jumpHeld = jump;
        TouchInput.jumpPressed = jumpBegan;
        TouchInput.downHeld = down;
    }

    void ComputeRects()
    {
        float h = Mathf.Max(84f, Screen.height * 0.17f);   // button size
        float m = h * 0.35f;                                // margin
        float gap = h * 0.18f;
        float downH = h * 0.62f;
        rLeft  = new Rect(m,             Screen.height - m - h, h, h);
        rRight = new Rect(m + h + gap,   Screen.height - m - h, h, h);
        rJump  = new Rect(Screen.width - m - h,     Screen.height - m - h, h, h);
        rDown  = new Rect(Screen.width - m - h - downH - gap, Screen.height - m - h, downH, h);
    }

    void OnGUI()
    {
        bool visible = TouchInput.touchUI || Application.isMobilePlatform;
        if (!visible) return;
        ComputeRects();

        if (btnStyle == null)
        {
            btnStyle = new GUIStyle(GUI.skin.box);
            btnStyle.alignment = TextAnchor.MiddleCenter;
            btnStyle.fontSize = Mathf.Max(14, (int)(Screen.height * 0.030f));
            btnStyle.fontStyle = FontStyle.Bold;
            btnStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
        }

        Color prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.40f); // translucent so the art shows through
        GUI.Box(rLeft, "\u25C0", btnStyle);
        GUI.Box(rRight, "\u25B6", btnStyle);
        GUI.Box(rJump, "JUMP", btnStyle);
        GUI.Box(rDown, "\u25BC", btnStyle);
        GUI.color = prev;
    }
}
