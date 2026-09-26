using UnityEngine;

namespace LilFoots {
/// <summary>
/// FRAME ANIMATION (BudE, Sept 26 ~1:44 AM ET: "we need to generate frames and proper
/// smount of frames needed for the characters and their poses aka walking jumping etc
/// for all characters and that will make the animations better and easier"). Classic
/// frame-based sprite animation: every character gets a GENERATED frame set
/// (idle / walk / jump) rendered from his approved reference art, key-out + normalized
/// to the same canvas and feet baseline as the original sprite, and this component plays
/// them by player state. Walk = forward-looped stride cycle, jump = indexed by vertical
/// velocity (launch / apex / fall), idle = gentle breathing pair. The 2.5D paper motion
/// (facing flip, squash/stretch, run lean) still layers on top via PlayerAnimBridge;
/// the bridge's step bob is disabled when frames are present because the frames
/// themselves carry the stride. Frames live at Assets/Art/Frames/&lt;char&gt;_&lt;anim&gt;_N.png.
/// </summary>
public class FrameAnimator : MonoBehaviour {
    public Sprite[] idle, walk, jump;
    public float idleFps = 2.5f;   // gentle breathing
    public float walkFps = 12f;    // full stride cadence at run speed

    SpriteRenderer sr;
    PlayerController pc;
    float tWalk, tIdle;

    void Start() {
        sr = GetComponent<SpriteRenderer>();
        pc = GetComponentInParent<PlayerController>();
        var bridge = GetComponent<PlayerAnimBridge>();
        if (bridge != null) bridge.runBob = false;   // the frames carry the stride now
    }

    void Update() {
        if (sr == null) return;
        // SELECT-CARD MODE (character pick screen): no PlayerController in this context,
        // so the card plays the generated idle breathing - the pick screen shows EXACTLY
        // the art and motion you'll play. (BudE, Sept 26: "character pick screen that also
        // getting reworked correct?")
        if (pc == null) {
            if (idle != null && idle.Length > 0) {
                tIdle += Time.deltaTime * idleFps;
                int n = idle.Length;
                int cycle = (n > 2) ? (2 * n - 2) : n;
                int idx = ((int)tIdle) % cycle;
                if (n > 2 && idx >= n) idx = cycle - idx;
                sr.sprite = idle[idx];
            }
            return;
        }
        float speed = Mathf.Abs(pc.rb.velocity.x);
        bool air = !pc.onGround;

        // ---- JUMP: frame indexed by vertical velocity (launch -> apex -> fall) ----
        if (air && jump != null && jump.Length > 0) {
            float vy = pc.rb.velocity.y;
            int i;
            if (vy > 1.5f)      i = Mathf.Min(1, jump.Length - 1);   // springing up
            else if (vy < -1.5f) i = jump.Length - 1;               // descending
            else                i = Mathf.Min(2, jump.Length - 1);  // apex tuck
            sr.sprite = jump[i];
            return;
        }

        // ---- WALK: forward-looped stride cycle, cadence scales with speed ----
        if (speed > 0.4f && walk != null && walk.Length > 0) {
            float fps = Mathf.Max(5f, walkFps * (speed / Mathf.Max(0.1f, pc.runSpeed)));
            tWalk += Time.deltaTime * fps;
            int i = ((int)tWalk) % walk.Length;
            sr.sprite = walk[i];
            return;
        }

        // ---- IDLE: gentle breathing, PING-PONG (BudE Sept 26: "we need to have better
        // idle animation") - a longer breathing cycle reads in-and-out; with 2 frames it
        // behaves exactly like the old alternation, with 3+ it breathes without snapping.
        if (idle != null && idle.Length > 0) {
            tIdle += Time.deltaTime * idleFps;
            int n = idle.Length;
            int cycle = (n > 2) ? (2 * n - 2) : n;   // 4 frames -> 0,1,2,3,2,1 ... 2 frames -> 0,1
            int idx = ((int)tIdle) % cycle;
            if (n > 2 && idx >= n) idx = cycle - idx;
            sr.sprite = idle[idx];
        }
    }
}
}
