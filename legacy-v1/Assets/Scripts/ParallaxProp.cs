using UnityEngine;

// Lil Foots: Big Adventure — Layer 3 foreground parallax (Bude Depth Doctrine).
// Near-field props sweep past the camera slightly faster than the gameplay plane,
// the classic 2D platformer depth cue. Unity transform work only.
public class ParallaxProp : MonoBehaviour {
    [Tooltip("1 = world-locked like gameplay; >1 = nearer to camera (sweeps faster).")]
    public float factor = 1.3f;
    Transform camT;
    float baseX, anchorX;
    void Start() {
        var cam = Camera.main;
        camT = cam != null ? cam.transform : null;
        baseX = transform.position.x;
        if (camT != null) anchorX = camT.position.x;
    }
    void LateUpdate() {
        if (camT == null) return;
        var p = transform.position;
        p.x = baseX + (camT.position.x - anchorX) * (factor - 1f);
        transform.position = p;
    }
}
