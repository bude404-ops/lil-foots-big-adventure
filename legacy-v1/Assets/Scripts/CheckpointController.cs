using UnityEngine;

namespace LilFoots {
/// <summary>Checkpoint post — reaching it sets your respawn point (1-2 min apart, per doctrine).</summary>
public class CheckpointController : MonoBehaviour {
    public int index;
    bool lit;
    void OnTriggerStay2D(Collider2D c) {
        var p = c.GetComponentInParent<PlayerController>();
        if (p == null) return;
        if (p.maxX >= transform.position.x - 0.1f && !lit) {
            lit = true; // lights up — visual in the art pass
        }
    }
}
}
