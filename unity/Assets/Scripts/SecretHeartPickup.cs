using UnityEngine;

/// <summary>
/// Lil Foots: Big Adventure — secret extra-life heart pickup (stream gully, map 001).
/// Engine behavior: touching the glowing heart grants +1 life, poofs, stays collected for the run.
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
public class SecretHeartPickup : MonoBehaviour {
    public float bobHz = 3f, bobAmp = 0.06f;
    Vector3 basePos;

    void Start() { basePos = transform.position; }
    void Update() {
        transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * bobHz * Mathf.PI) * bobAmp);
    }

    void OnTriggerEnter2D(Collider2D c) {
        var p = c.GetComponentInParent<PlayerController>();
        if (p == null) return;
        p.lives++;
        Destroy(gameObject);
    }
}
