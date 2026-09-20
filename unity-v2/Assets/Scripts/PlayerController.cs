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
    public float accelAir     = 21.0f; // 2100 px/s^2
    public float stompBounce  = 5.2f;  // -520 px/s
    public int   maxLives     = 3;

    [Header("Refs")]
    public Rigidbody2D rb;
    public Collider2D feet;
    public SpriteRenderer art;

    public static PlayerController Instance { get; private set; }

    [HideInInspector] public int tokens;
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

        if (invuln > 0) invuln -= Time.deltaTime;
        maxX = Mathf.Max(maxX, transform.position.x);

        // ---- FALLING POINTS (Bude, Sept 20: 'no falling points') ----
        // The stream gaps are real pits: fall below the kill line and it costs a life +
        // respawns at the last checkpoint (classic). Before this, falling into a pit was an
        // infinite fall with no consequence - a softlock.
        if (transform.position.y < 1.0f && LivesManager.Instance != null) LivesManager.Instance.Die();
    }

    void FixedUpdate() {
        prevY = transform.position.y;
        // gravity is applied NATIVELY by Unity Physics2D (gravityScale), not manually
        if (rb.velocity.y < -30f) rb.velocity = new Vector2(rb.velocity.x, -30f); // terminal velocity clamp
    }

    void OnCollisionStay2D(Collision2D c) {
        if (c.GetContact(0).normal.y > 0.5f) onGround = true;
    }
    void OnCollisionExit2D(Collision2D c) { onGround = false; }

    /// Bounce from a stomp + kill the enemy — called by HoundController/DroneController.
    public void StompBounce() {
        rb.velocity = new Vector2(rb.velocity.x, stompBounce);
        invuln = Mathf.Max(invuln, 0.1f);
    }
}
}
