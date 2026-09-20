using UnityEngine;

namespace LilFoots {
/// <summary>
/// Hound dog — ground patrol enemy. Carries over the v0.3 playtest fixes:
///  1) LEDGE GUARD: probes the ground ahead; turns at pit edges. Never runs on nothing.
///  2) Chase only when solid ground exists toward the player (no chasing across gaps).
///  3) STOMP HITBOX: falling contact from above the back line kills the hound;
///     side contact is lethal to the player (forgiving from-above stomp, per Bude's playtest).
/// All tuned values match the JS engine build (converted @ PPU 100).
/// </summary>
public class HoundController : MonoBehaviour {
    [Header("Patrol (world-logic units, px/100)")]
    public float minX = 24.5f, maxX = 33.2f;
    public float speed = 1.10f;
    public float chaseRange = 2.6f;
    public float alertSpeedMul = 2.2f;   // 1 + 1.2 alert multiplier from the engine
    public float alertTime = 5f;

    [Header("Stomp geometry")]
    public float contactXRange = 0.52f; // forgiving 52px window
    public float backLineOffset = 0.24f; // 24px above the hound's feet = back line
    public float sideKillDepth = 0.46f;

    [HideInInspector] public bool alerted;
    [HideInInspector] public bool dead;

    [HideInInspector] public float dir = -1;
    float alert;
    Rigidbody2D rb;
    Vector2 size;

    void Start() { rb = GetComponent<Rigidbody2D>(); dir = (transform.position.x > (minX+maxX)/2) ? -1 : 1; }

    // ALIVE ON SCREEN (Bude, Sept 20: 'the enemies are still just stale models'): the art
    // child faces the patrol/chase direction and only waddles while the hound actually moves.
    Transform art;
    void Update() {
        if (dead) return;
        if (art == null) { var a = transform.Find("HoundArt"); if (a != null) art = a; else return; }
        float moving = Mathf.Abs(rb.velocity.x);
        var an = art.GetComponent<Animator>();
        if (an != null) an.speed = moving > 0.05f ? 1f : 0f;   // waddle only when walking
        float sx = Mathf.Abs(art.localScale.x) * (dir >= 0 ? 1f : -1f);
        var sc = art.localScale; sc.x = sx; art.localScale = sc;
    }

    void FixedUpdate() {
        if (dead) return;
        var p = PlayerController.Instance;
        if (p == null) return;
        if (alert > 0) alert -= Time.fixedDeltaTime;
        alerted = alert > 0;

        float spd = speed * (alert > 0 ? alertSpeedMul : 1f);
        float px = transform.position.x;

        // chase — only when ground exists toward the player (no pit-chasing)
        bool inLeash = px >= minX - 2f && px <= maxX + 2f;
        float toward = Mathf.Sign(p.transform.position.x - px);
        bool groundToPlayer = ProbeGround(px + toward * 0.42f);
        if (inLeash && Mathf.Abs(px - p.transform.position.x) < chaseRange && p.onGround && groundToPlayer)
            dir = toward;

        // LEDGE GUARD — probe ahead; turn at edges, never walk on nothing
        if (ProbeGround(px + dir * 0.42f)) rb.velocity = new Vector2(dir * spd, rb.velocity.y);
        else { dir *= -1; rb.velocity = Vector2.zero; }

        if (px > maxX) dir = -1;
        if (px < minX) dir = 1;

        // FACE THE PATROL DIRECTION (Bude, Sept 20: "the enemies are still just stale models"):
        // the art child flips live when the hound turns - a hound running backwards reads dead.
        var art = transform.Find("HoundArt");
        if (art != null) {
            float want = Mathf.Abs(art.localScale.x) * ((dir < 0) ? -1f : 1f);
            if (!Mathf.Approximately(art.localScale.x, want))
                art.localScale = new Vector3(want, art.localScale.y, art.localScale.z);
        }
    }

    bool ProbeGround(float x) {
        // probe straddles the ground line (slab tops sit at y=GroundY) — catches edges reliably
        var hit = Physics2D.OverlapBox(new Vector2(x, transform.position.y + 0.15f), new Vector2(0.1f, 0.6f), 0,
                                       LayerMask.GetMask("Ground"));
        return hit != null;
    }

    public void Alert(float t = 5f) { alert = Mathf.Max(alert, t); }

    void OnTriggerStay2D(Collider2D c) {
        if (dead) return;
        var p = c.GetComponentInParent<PlayerController>();
        if (p == null) return;
        float feetNow = p.transform.position.y - 0.10f;
        float feetPrev = p.prevY - 0.10f;
        float backLine = transform.position.y - backLineOffset;
        bool xHit = Mathf.Abs(transform.position.x - p.transform.position.x) < contactXRange;
        if (!xHit) return;
        if (p.rb.velocity.y > 1.0f && (feetNow <= backLine || feetPrev <= backLine)) {
            // STOMP — from above (forgiving, swept) = hound dies, player bounces
            dead = true; gameObject.SetActive(false);
            p.StompBounce();
            Sfx.Play(Sfx.Clip.Stomp);
            Vfx.Poof(transform.position);
        } else if (feetNow > backLine && Mathf.Abs(transform.position.y - feetNow) < sideKillDepth && p.invuln <= 0) {
            LivesManager.Instance.Die(); // side contact = player loses a life
        }
    }
}
}
