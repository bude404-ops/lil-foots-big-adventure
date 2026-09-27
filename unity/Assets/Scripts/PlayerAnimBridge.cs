using UnityEngine;

namespace LilFoots {
/// <summary>
/// GAMEPLAY ANIMATION BRIDGE (BudE, Sept 20: "the characters pose still is the t pose and no
/// animations"; Sept 25: "we need to make character animations" + "jumping lags behind").
/// Sits on each gameplay rig root and keeps the rig's NATIVE Animator honest: feeds it the
/// player's real state (speed / airborne) so idle <-> walk <-> jump actually play, flips the
/// rig to face the run direction, and layers the 2.5D PAPER MOTION on top (Sept 25 punch-up):
/// take-off stretch, landing squash, and a run lean into the travel direction - the character
/// visibly answers every input, on top of the bone animation.
/// [RUN CYCLE Sept 26 ~1:36 AM ET - BudE: "the character running... doesnt look like running"
/// + "the face cuts off like Canadians in South Park"] The art NEVER splits: the approved
/// sprite stays one whole piece (no hip slice, no cut face). The run reads through motion
/// ON the whole art: a step-synced vertical bob (a little hop per stride) plus a light
/// stride wobble, composed with the squash/lean. Paper-cutout life, zero amputation.
/// </summary>
public class PlayerAnimBridge : MonoBehaviour {
    Animator anim;
    PlayerController pc;
    float baseScaleX, baseScaleY;

    // 2.5D paper motion state
    float squash = 1f;   // 1 = neutral; >1 stretches tall, <1 squashes flat
    float squashVel;
    bool wasAir;
    [HideInInspector] public bool runBob = true;   // FrameAnimator sets false: generated frames carry the stride
    float runPhase;              // stride cycle clock
    Vector3 baseLocalPos;        // feet-anchor offset baked by RigPass - bob rides ON TOP of it
    int prevFacing;              // [MOTION-FEEL Sept 27 PM] turn snap: an overshoot spring on direction change
    float turnSnap, turnVel;

    void Awake() {
        baseScaleX = Mathf.Abs(transform.localScale.x);
        baseScaleY = transform.localScale.y;
        baseLocalPos = transform.localPosition;
    }

    void Update() {
        if (pc == null) pc = GetComponentInParent<PlayerController>();
        if (pc == null) return;
        if (anim == null) {
            anim = GetComponent<Animator>();
            if (anim == null) return;
        }
        // state -> native Animator parameters (the controller drives idle/walk/jump)
        anim.SetFloat("speed", Mathf.Abs(pc.rb.velocity.x));
        anim.SetBool("air", !pc.onGround);

        // ---- 2.5D PAPER MOTION (Sept 25: take-off must FEEL instant, landing must SELL) ----
        bool air = !pc.onGround;
        if (wasAir && !air) {
            squash = 0.76f; squashVel = 0f;              // LANDING: flat squash pop
        } else if (!wasAir && air && pc.rb.velocity.y > 0.5f) {
            squash = 1.16f; squashVel = 0f;               // TAKE-OFF: tall stretch snap
        }
        wasAir = air;
        // [MOTION-FEEL Sept 27 PM - BudE: "character animations are still off"] AIR POSE
        // PHASES: the whole flight now reads - rising stretches tall (neutral 1.10), the
        // apex floats neutral, falling flattens (0.94) like bracing for the plant. The
        // spring chases the PHASE neutral, not a flat 1.0, so the arc has shape.
        float vy = pc.rb.velocity.y;
        float neutral = 1f;
        if (air) neutral = vy > 1.5f ? 1.10f : (vy < -2f ? 0.94f : 1.02f);
        // spring back to neutral (stiff spring, heavy damping - snappy, no wobble)
        float k = 170f, d = 15f;
        squashVel += (k * (neutral - squash) - d * squashVel) * Time.deltaTime;
        squash += squashVel * Time.deltaTime;
        float sq = Mathf.Clamp(squash, 0.72f, 1.24f);
        // turn snap: a quick overshoot rotation when the direction flips - the character
        // visibly ANSWERS the input instead of gluing flat.
        if (prevFacing != 0 && pc.facing != prevFacing) { turnSnap = pc.facing * 7f; turnVel = 0f; }
        prevFacing = pc.facing;
        turnVel += (220f * (0f - turnSnap) - 18f * turnVel) * Time.deltaTime;
        turnSnap += turnVel * Time.deltaTime;

        // ---- facing + paper scale + run lean, composed (flip law unchanged) ----
        // ART FACES LEFT NATIVELY (same law as the JS engine): moving RIGHT (facing=+1)
        // needs the MIRRORED scale and moving LEFT needs the baked scale.
        float speedFrac = Mathf.Clamp01(Mathf.Abs(pc.rb.velocity.x) / pc.runSpeed);
        var sc = transform.localScale;
        sc.x = baseScaleX * -pc.facing / Mathf.Sqrt(sq);  // tall+thin when stretched, wide when squashed
        sc.y = baseScaleY * sq;
        transform.localScale = sc;
        // ---- RUN CYCLE (whole-art motion, no cuts): step-synced bob + stride wobble ----
        // [READABILITY CRANK Sept 27 PM - BudE: "the character animations are still off"]
        // the old values were calibrated on the QC stills, not at game zoom: a 0.06u bob
        // on a 1.2u character is sub-pixel. Paper motion now reads at a glance:
        //   bob 0.06 -> 0.17u (a real hop per stride), sway 2.2 -> 5deg, lean 6 -> 11deg,
        //   idle gets a gentle breathing scale so standing is alive too.
        float wobble = 0f, idleSway = 0f;
        if (runBob && !air && speedFrac > 0.05f) {
            runPhase += Time.deltaTime * (8f + 10f * speedFrac);        // stride cadence scales with speed
            // ASYMMETRIC HOP: sharp plant, floaty top (pow 0.7 shapes the |sin| wave) -
            // a symmetric bob reads as a sewing machine; this reads as steps.
            float bob = Mathf.Pow(Mathf.Abs(Mathf.Sin(runPhase)), 0.7f) * 0.17f * speedFrac;
            wobble = Mathf.Sin(runPhase * 2f) * 5f * speedFrac;                // pronounced stride sway
            transform.localPosition = baseLocalPos + new Vector3(0f, bob, 0f);
        } else {
            runPhase = 0f;
            transform.localPosition = baseLocalPos;                      // settle back on the anchor
            if (!air) {                                                  // idle breathing (scale, no cut)
                sq = 1f + Mathf.Sin(Time.time * 2.1f) * 0.018f;          // gentle life while standing
                var sb = transform.localScale;
                sb.x = baseScaleX * -pc.facing / Mathf.Sqrt(sq);
                sb.y = baseScaleY * sq;
                transform.localScale = sb;
                idleSway = Mathf.Sin(Time.time * 1.7f) * 1.4f;           // slow sway so standing feels alive
            }
        }
        // lean into the run (momentum) + stride sway + turn snap + idle sway, composed
        transform.localRotation = Quaternion.Euler(0f, 0f,
            -pc.facing * 9f * speedFrac * (air ? 0.35f : 1f) + wobble + turnSnap + idleSway);
    }
}
}
