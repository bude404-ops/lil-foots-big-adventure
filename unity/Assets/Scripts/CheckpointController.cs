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
            lit = true; // PIECE 5 totem: brighten from dim moss to full color + warm glow
            var sr = GetComponentInChildren<SpriteRenderer>(); // totem art rides the TotemArt child (standing fix)
            if (sr != null) sr.color = new Color(1f, 1f, 0.96f, 1f); // lit: full color, faint warm cast
            if (AudioManager.Instance != null) AudioManager.Instance.Play("checkpoint");
        }
    }
}
}
