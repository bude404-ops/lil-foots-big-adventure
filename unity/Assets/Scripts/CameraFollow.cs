using UnityEngine;

namespace LilFoots {
/// <summary>Camera follow — locks to the side-scroll plane, no rotation, smooth window.
/// CENTERED FRAME (Bude, Sept 20: 'The camera also isnt centered on the character'):
/// x tracks the player with only a small forward bias (was +2.4 look-ahead that shoved the
/// character to the screen's left edge), and y now FOLLOWS the player with a soft damp so
/// climbing the step platforms keeps the character framed instead of pinned low while
/// higher ground scrolls out of view. Unity-native SmoothDamp does the easing.</summary>
public class CameraFollow : MonoBehaviour {
    public Transform target;
    public float lookAhead = 0.8f;   // small forward bias so there's run room ahead
    public float smoothTime = 0.08f;
    public float yFollow = 0.8f;     // camera rides this above the player (ground framing unchanged)
    public float minX = 6.7f, maxX = 93f;   // level bounds (px/100, half-screen margin)
    public float minY = 7.0f, maxY = 8.8f;  // vertical bounds: never below the approved ground
                                            // framing, never past the highest platform + margin
    Vector3 vel;

    void LateUpdate() {
        if (!target) return;
        Vector3 want = new Vector3(target.position.x + lookAhead,
                                   target.position.y + yFollow,
                                   transform.position.z);
        want.x = Mathf.Clamp(want.x, minX, maxX);
        want.y = Mathf.Clamp(want.y, minY, maxY);
        transform.position = Vector3.SmoothDamp(transform.position, want, ref vel, smoothTime);
    }
}
}
