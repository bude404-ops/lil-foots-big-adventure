using UnityEngine;

namespace LilFoots {
/// <summary>
/// Trail camera — Carl's tree-mounted snitch. Detection cone flashes, alerts hounds in range
/// and calls a hound pack toward the camera. Stompable mid-air (smash = gone).
/// Mounted on a REAL tree (per playtest fix): tree art belongs to the builder, cam sits on the trunk.
/// </summary>
public class TrailCamController : MonoBehaviour {
    public float coneRange = 4.2f;
    public float sweep = 0.5f;      // cone angle oscillation amplitude
    public float flashTime = 1.5f;
    public float alertHoundRange = 8f;
    [HideInInspector] public bool dead;
    [HideInInspector] public float flash;

    float ang;
    Transform conePivot; // visual cone child, optional

    void Update() {
        if (dead) return;
        ang = Mathf.Sin(Time.time * 1.5f) * sweep;
        if (flash > 0) flash -= Time.deltaTime;
        var p = PlayerController.Instance;
        if (p == null) return;
        float dx = p.transform.position.x - transform.position.x;
        if (dx > 0 && dx < coneRange && flash <= 0) {
            float dy = (p.transform.position.y - 0.4f) - transform.position.y;
            if (dy > 1.2f && dy < 3.4f && Mathf.Abs(ang) < 0.45f) {
                flash = flashTime;
                HoundManager.AlertAllInRange(transform.position.x, alertHoundRange);
                Sfx.Play(Sfx.Clip.Snitch);
            }
        }
        // SMASH — airborne contact destroys the snitch
        if (!p.onGround && !dead) {
            float d = Vector2.Distance(p.transform.position + Vector3.up * 0.6f, transform.position);
            if (d < 0.8f) {
                dead = true; gameObject.SetActive(false);
                Sfx.Play(Sfx.Clip.CamSmash);
                Vfx.Poof(transform.position);
            }
        }
    }
}
}
