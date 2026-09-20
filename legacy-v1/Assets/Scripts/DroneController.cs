using UnityEngine;

namespace LilFoots {
/// <summary>
/// Carl's spy drone — low-flying spotter with a sweeping spotlight. Free-flying (no tether, per playtest fix).
/// Spotlight detection alerts the pack. Stompable / punchable mid-air; falling wreck tumbles.
/// </summary>
public class DroneController : MonoBehaviour {
    public float spotHalfWidth = 0.7f;
    [HideInInspector] public bool dead, crashed;
    float fallT; Vector2 fallPos;
    public float alarm = 3.5f;

    void Update() {
        var p = PlayerController.Instance;
        if (dead) {
            if (crashed) return;
            fallT += Time.deltaTime;
            fallPos += new Vector2(0.3f * fallT * 60f, -9f * fallT * fallT) * Time.deltaTime; // wreck arc
            transform.position = fallPos;
            transform.Rotate(0, 0, 7f * Time.deltaTime * 60f);
            if (fallPos.y <= GameManager.GroundY - 0.16f) {
                crashed = true; Vfx.Poof(fallPos); Sfx.Play(Sfx.Clip.DroneDie);
            }
            return;
        }
        // low patrol bob
        float bob = Mathf.Sin(Time.time * 2f) * 0.04f;
        transform.position = new Vector3(transform.position.x, transform.position.y + bob * Time.deltaTime * 60f, transform.position.z);
        if (p == null) return;
        // spotlight hit
        float dx = p.transform.position.x - transform.position.x;
        if (Mathf.Abs(dx) < spotHalfWidth && p.onGround && Mathf.Abs(p.transform.position.y - GameManager.GroundY) < 0.3f) {
            HoundManager.AlertAll(4f);
            Sfx.Play(Sfx.Clip.Snitch);
        }
        // stomp / mid-air contact
        float d = Vector2.Distance(p.transform.position + Vector3.up * 0.5f, transform.position);
        if (d < 0.7f) {
            dead = true; fallPos = transform.position; fallT = 0;
            if (p.rb.velocity.y > 0) p.rb.velocity = new Vector2(p.rb.velocity.x, 5.2f); // punch through
            else p.StompBounce();
        }
    }
}
}
