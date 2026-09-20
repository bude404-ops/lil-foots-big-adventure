using UnityEngine;

namespace LilFoots {
/// <summary>
/// Lives / death / checkpoints — the classic loop from the engine:
/// death = lose a life + respawn at last checkpoint + enemies reset (stomped hounds STAY dead per run? 
/// No — engine rule: death resets enemies but stomped state also resets; run progress (tokens) keeps).
/// Out of lives = full restart.
/// </summary>
public class LivesManager : MonoBehaviour {
    public static LivesManager Instance { get; private set; }
    public Transform player;
    public Transform[] checkpoints; // filled by the level builder from map001.json

    int lastCp = 0;
    [HideInInspector] public int gateNeed = 18;
    [HideInInspector] public bool won;

    void Awake() { Instance = this; }

    public void Die() {
        var p = PlayerController.Instance;
        p.lives--;
        Sfx.Play(Sfx.Clip.Die);
        if (p.lives <= 0) { Restart(); return; }
        // last checkpoint passed
        for (int i = 0; i < checkpoints.Length; i++)
            if (p.maxX >= checkpoints[i].position.x - 0.1f) lastCp = i;
        var hounds = FindObjectsOfType<HoundController>();
        foreach (var h in hounds) { h.gameObject.SetActive(true); h.dead = false; }
        var cams = FindObjectsOfType<TrailCamController>();
        foreach (var c in cams) c.dead = false;
        p.transform.position = checkpoints[lastCp].position + Vector3.up * 0.5f;
        p.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
        p.invuln = 2f;
    }

    public void Restart() {
        var p = PlayerController.Instance;
        p.lives = p.maxLives;
        p.tokens = 0;
        p.maxX = 0;
        p.transform.position = checkpoints[0].position + Vector3.up * 0.5f;
        var hounds = FindObjectsOfType<HoundController>();
        foreach (var h in hounds) { h.gameObject.SetActive(true); h.dead = false; }
        foreach (var t in FindObjectsOfType<TokenCollectible>()) t.Reset();
        foreach (var c in FindObjectsOfType<TrailCamController>()) c.dead = false;
        // (secret heart stays collected once earned — engine keeps heart.got per run)
    }
}
}
