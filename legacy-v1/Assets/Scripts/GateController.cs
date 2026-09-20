using UnityEngine;

namespace LilFoots {
/// <summary>
/// BIG TOKEN GATE — needs 18 tokens to pass. Playtest-fix behavior: arriving short bounces you
/// back with a transient notice. NEVER a soft-lock (v0.3 fix, kept in the port).
/// </summary>
public class GateController : MonoBehaviour {
    public int need = 18;      // GATE_NEED
    public float noticeTime = 2.4f;
    float gateMsg;

    void OnTriggerStay2D(Collider2D c) {
        var p = c.GetComponentInParent<PlayerController>();
        if (p == null) return;
        if (p.tokens < need) {
            p.transform.position = new Vector3(85.45f, p.transform.position.y, p.transform.position.z);
            if (gateMsg <= 0) Sfx.Play(Sfx.Clip.Gate);
            gateMsg = noticeTime; // bounce back + notice — game keeps running
        }
        // tokens >= need: trigger is disabled (no blocking collider beyond this check)
    }

    void Update() { if (gateMsg > 0) gateMsg -= Time.deltaTime; }

    void OnGUI() {
        if (gateMsg > 0) {
            GUI.Label(new Rect(Screen.width * 0.3f, Screen.height * 0.28f, Screen.width * 0.4f, 40f),
                "BIG TOKEN GATE — need " + need + " tokens to pass (you have " + PlayerController.Instance.tokens + ")");
        }
    }
}
}
