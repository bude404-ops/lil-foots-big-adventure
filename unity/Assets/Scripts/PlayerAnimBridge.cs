using UnityEngine;

namespace LilFoots {
/// <summary>
/// GAMEPLAY ANIMATION BRIDGE (Bude, Sept 20: "the characters pose still is the t pose and no
/// animations"). Sits on each gameplay rig root and keeps the rig's NATIVE Animator honest:
/// feeds it the player's real state (speed / airborne) so idle <-> walk <-> jump actually play,
/// and flips the rig to face the run direction. Native Animator + parameters, no custom
/// skeleton - Unity performs the animation, this only reports the state.
/// </summary>
public class PlayerAnimBridge : MonoBehaviour {
    Animator anim;
    PlayerController pc;
    float baseScaleX;

    void Awake() { baseScaleX = Mathf.Abs(transform.localScale.x); }

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
        // face the run direction (rig flip, sign-safe off the baked base scale)
        if (pc.facing != 0) {
            var sc = transform.localScale;
            float want = baseScaleX * pc.facing;
            if (!Mathf.Approximately(sc.x, want)) { sc.x = want; transform.localScale = sc; }
        }
    }
}
}
