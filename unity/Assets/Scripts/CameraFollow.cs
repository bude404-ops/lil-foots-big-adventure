using UnityEngine;

namespace LilFoots {
/// <summary>Camera follow — locks to the side-scroll plane, no rotation, smooth window.</summary>
public class CameraFollow : MonoBehaviour {
    public Transform target;
    public float lookAhead = 2.4f;
    public float smoothTime = 0.08f;
    public float minX = 6.7f, maxX = 93f; // level bounds (px/100, half-screen margin)
    Vector3 vel;

    void LateUpdate() {
        if (!target) return;
        Vector3 want = new Vector3(target.position.x + lookAhead, transform.position.y, transform.position.z);
        want.x = Mathf.Clamp(want.x, minX, maxX);
        transform.position = Vector3.SmoothDamp(transform.position, want, ref vel, smoothTime);
    }
}
}
