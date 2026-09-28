using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LilFoots {
/// <summary>
/// Lil Foots: Big Adventure — Unity port of the custom canvas engine's player controller.
/// All constants come from the shipped JS build (map 001, v0.3), converted at 100 px = 1 Unity unit.
/// Physics: NATIVE Unity Physics2D (Rigidbody2D + gravityScale). Feel constants from the JS engine @ PPU 100.
/// </summary>
public class PlayerController : MonoBehaviour {
    [Header("Tuned feel (converted from the JS engine @ PPU 100)")]
    public float runSpeed     = 4.6f;   // RUNSPD 460 px/s
    public float jumpVelocity = 9.0f;  // JUMPVEL -900 px/s (up)
    public float gravityScale = 2.446f; // GRAV 2400 px/s^2 / Physics2D's 9.81 — native gravity
    public float jumpHoldTime = 0.28f; // variable-jump hold window
    public float jumpHoldFactor = 0.9f;// gravity scale while holding jump
    public float coyoteTime   = 0.12f;
    public float jumpBuffer   = 0.14f;
    public float accelGround  = 34.0f;  // 3400 px/s^2
    public float accelAir     = 30.0f; // 3000 px/s^2 (BudE Sept 25: 'jumping lags behind' - mid-air control was 2100, felt sluggish; 3000 answers the stick the moment you push it)
    public int   maxLives     = 3;

    [Header("Frame bounds (BudE Sept 26: 'the character can disappear if runs to the left')")]
    public float boundMinX = -9999f, boundMaxX = 9999f;   // set by the level builder: player stays inside the visible screen at the level edges

    [Header("Refs")]
    public Rigidbody2D rb;
    public Collider2D feet;
    public SpriteRenderer art;

    public static PlayerController Instance { get; private set; }

    [HideInInspector] public int tokens;
    [Header("CEDAR BARK HIDE (BudE approved Sept 27 PM): one free hit")]
    [HideInInspector] public int bark = 0;   // 1 = shield held (gold rim glow); enemy pass calls TakeHit()
    Transform barkGlow;
    [HideInInspector] public int lives = 3;
    [HideInInspector] public float prevY;      // for swept stomp checks
    [HideInInspector] public bool onGround;
    [HideInInspector] public float maxX;        // checkpoint progress
    [HideInInspector] public float invuln;      // respawn safety window

    float coyote, buffer, jumpHold;
    bool jumpWas;
    public int facing = 1;

    void Awake() { Instance = this; if (!rb) rb = GetComponent<Rigidbody2D>(); rb.gravityScale = gravityScale; }

    void Update() {
        // ---- input (keyboard + touch) ----
        // RAW TOUCH SAFETY NET (Bude, Sept 20: "the jump button isnt working" on the live phone
        // build): phone browsers can swallow uGUI pointer events (no touch-action on the WebGL
        // canvas -> the browser eats the gesture), which kills the whole touch deck. Raw
        // UnityEngine.Touch still arrives, so screen zones keep the game playable no matter
        // what happens to uGUI: left 28% = LEFT, next 32% = RIGHT, right 40% = JUMP.
        // The zones only activate while the deck reports NOTHING, so when the buttons work
        // they stay the only controls - no double input.
        bool rawJump = false, rawLeft = false, rawRight = false;
        if (!TouchDeck.LeftHeld && !TouchDeck.RightHeld && !TouchDeck.JumpHeld && Input.touchCount > 0) {
            for (int i = 0; i < Input.touchCount; i++) {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) continue;
                float u = t.position.x / (float)Screen.width;
                if (u < 0.28f) rawLeft = true;
                else if (u < 0.60f) rawRight = true;
                else rawJump = true;
            }
        }
        bool jump = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)
                    || TouchDeck.JumpHeld || rawJump;
        bool left = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) || TouchDeck.LeftHeld || rawLeft;
        bool right = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) || TouchDeck.RightHeld || rawRight;
        if (jump && !jumpWas) buffer = jumpBuffer;
        if (!jump) buffer = 0;
        jumpWas = jump;

        // ---- coyote + buffered jump ----
        if (onGround) coyote = coyoteTime; else coyote -= Time.deltaTime;
        if (buffer > 0 && (onGround || coyote > 0)) {
            rb.velocity = new Vector2(rb.velocity.x, jumpVelocity);
            onGround = false; coyote = 0; buffer = 0; jumpHold = jumpHoldTime;
            Sfx.Play(Sfx.Clip.Jump);
        }
        if (jump && jumpHold > 0) { jumpHold -= Time.deltaTime; }
        else jumpHold = 0;
        if (!jump) jumpHold = 0;
        // variable jump height via NATIVE gravity scaling (Unity Physics2D applies the force)
        rb.gravityScale = (jumpHold > 0 && rb.velocity.y > 0) ? gravityScale * jumpHoldFactor : gravityScale;

        // ---- horizontal accel/decel (smooth, flows instead of snapping) ----
        float target = (right ? runSpeed : 0) - (left ? runSpeed : 0);
        if (target != 0) facing = (int)Mathf.Sign(target);
        float accel = onGround ? accelGround : accelAir;
        float vx = rb.velocity.x;
        if (target > vx) vx = Mathf.Min(target, vx + accel * Time.deltaTime);
        else if (target < vx) vx = Mathf.Max(target, vx - accel * Time.deltaTime);
        rb.velocity = new Vector2(vx, rb.velocity.y);

        // ---- FRAME BOUNDS (Sept 26): the camera clamps at the level edges, so the player
        // clamps with it - running off the left/right edge holds you AT the screen edge
        // instead of letting you vanish out of frame. ----
        if (boundMinX > -9998f || boundMaxX < 9998f) {
            var pos = rb.position;
            if (pos.x < boundMinX) { pos.x = boundMinX; if (rb.velocity.x < 0f) rb.velocity = new Vector2(0f, rb.velocity.y); }
            if (pos.x > boundMaxX) { pos.x = boundMaxX; if (rb.velocity.x > 0f) rb.velocity = new Vector2(0f, rb.velocity.y); }
            if (pos != rb.position) rb.position = pos;
        }

        if (invuln > 0) invuln -= Time.deltaTime;
        maxX = Mathf.Max(maxX, transform.position.x);

        // ---- FALLING POINTS (Bude, Sept 20: 'no falling points') ----
        // The stream gaps are real pits: fall below the kill line and it costs a life +
        // respawns at the last checkpoint (classic). Before this, falling into a pit was an
        // infinite fall with no consequence - a softlock.
        if (transform.position.y < 1.0f && LivesManager.Instance != null) LivesManager.Instance.Die();
    }

    /// <summary>Damage entry point (enemy pass wires this to contact damage): the Cedar
    /// Bark Hide pops FIRST - glow shatters, brief invuln, no footprint lost. No hide
    /// held = classic death (lose a footprint from the trail, respawn at checkpoint).</summary>
    public void TakeHit() {
        if (bark > 0) {
            bark = 0;
            invuln = 1.2f;
            Sfx.Play(Sfx.Clip.Coin);   // acquired-item pop until the enemy pass gives it its own shatter SFX
        } else if (LivesManager.Instance != null) {
            LivesManager.Instance.Die();
        }
    }

    void LateUpdate() {
        // gold rim glow while the bark hide is held
        if (bark > 0 && barkGlow == null) {
            var g = new GameObject("BarkGlow");
            g.transform.SetParent(transform, false);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = BarkRingSprite();
            sr.sortingOrder = 2;   // behind the character art: reads as a rim halo
            if (sr.sprite != null) {
                float f = 1.15f / sr.sprite.bounds.size.y;
                g.transform.localScale = new Vector3(f, f, 1f);
            }
            barkGlow = g.transform;
        } else if (bark <= 0 && barkGlow != null) {
            Destroy(barkGlow.gameObject);
            barkGlow = null;
        }
    }

    static Sprite BarkRingSprite() {
        const int S = 48;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) {
            float nx = (x - S / 2f + 0.5f) / (S / 2f), ny = (y - S / 2f + 0.5f) / (S / 2f);
            float d = Mathf.Sqrt(nx * nx + ny * ny);
            Color c = Color.clear;
            if (d > 0.72f && d < 1f) {           // soft gold ring band
                float band = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.14f);
                c = new Color(1f, 0.88f, 0.45f, 0.55f * band);
            } else if (d <= 0.72f) {             // faint interior wash
                c = new Color(1f, 0.95f, 0.65f, 0.06f * (1f - d / 0.72f));
            }
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S / 1.15f);
    }

    void FixedUpdate() {
        prevY = transform.position.y;
        // gravity is applied NATIVELY by Unity Physics2D (gravityScale), not manually
        if (rb.velocity.y < -30f) rb.velocity = new Vector2(rb.velocity.x, -30f); // terminal velocity clamp
    }

    void OnCollisionStay2D(Collision2D c) {
        if (c.GetContact(0).normal.y > 0.5f) onGround = true;
        // [BUMP TILES Sept 27 PM - BudE: "can we make certain tiles hittable if you [hit]
        // below them like in mario? For where hidden stuff can be?"] head hits a tile's
        // underside (contact normal points down) -> the tile bumps + pops its content.
        // [BUMP REGISTRATION FIX Sept 27 PM - BudE: "map isnt registering invisible blocks"]
        // GetContact(0) is just the FIRST contact point - on a head bonk the first contact is
        // often a shoulder/side touch with a sideways normal, so the bump never fired. Scan
        // ALL contacts: if ANY of them points down, the head hit the underside.
        for (int ci = 0; ci < c.contactCount; ci++) {
            if (c.GetContact(ci).normal.y < -0.5f) {
                var bt = c.gameObject.GetComponentInParent<LilFoots.BumpTile>();
                if (bt != null) { bt.Bump(); break; }
            }
        }
    }
    void OnCollisionExit2D(Collision2D c) { onGround = false; }

}
}
