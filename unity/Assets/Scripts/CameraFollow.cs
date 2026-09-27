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
    public float lookAhead = 0.15f;  // Bude Sept 20: "camera also needs to move over to the left some more" - nearly centered, tiny lead
    public float smoothTime = 0.08f;
    public float yFollow = 0.8f;     // camera rides this above the player (ground framing unchanged)
    public float minX = 6.7f, maxX = 93f;   // level bounds (px/100, half-screen margin)
    public float minY = 7.0f, maxY = 9.6f;  // vertical bounds: never below the approved ground (maxY 8.8->9.6 Sept 25: the jump apex hit the clamp, so the camera stopped following mid-jump and the jump READ as laggy)
                                            // framing, never past the highest platform + margin
    Vector3 vel;
    PlayerController pc;
    Camera cam;

    void Awake() {
        cam = GetComponent<Camera>();
    }

    void LateUpdate() {
        if (!target) return;
        // [BUD-E Sept 27: "if you run to the left you cant see the character"] the builder's
        // fixed boundMinX assumed the editor aspect; on a phone the camera's visible left
        // edge sits further right and the player walked out of frame at the level start.
        // Bind the player's left wall to the CAMERA's actual visible edge every frame.
        if (cam == null) cam = GetComponent<Camera>();
        if (pc == null) pc = target.GetComponentInParent<PlayerController>();
        if (pc != null && cam != null) {
            float halfW = cam.orthographicSize * cam.aspect;
            pc.boundMinX = transform.position.x - halfW + 0.55f;
        }
        Vector3 want = new Vector3(target.position.x + lookAhead,
                                   target.position.y + yFollow,
                                   transform.position.z);
        want.x = Mathf.Clamp(want.x, minX, maxX);
        want.y = Mathf.Clamp(want.y, minY, maxY);
        transform.position = Vector3.SmoothDamp(transform.position, want, ref vel, smoothTime);
    }
}
}
