using UnityEngine;

namespace LilFoots {
/// <summary>Big Token (footprint coin) — 4 tiers: easy / exploration / difficult / hidden.</summary>
public class TokenCollectible : MonoBehaviour {
    public int tier;
    bool got;
    void OnTriggerEnter2D(Collider2D c) {
        if (got) return;
        var p = c.GetComponentInParent<PlayerController>();
        if (p == null) return;
        got = true; gameObject.SetActive(false);
        PlayerController.Instance.tokens++;
        Sfx.Play(Sfx.Clip.Coin);
    }
    public void Reset() { got = false; gameObject.SetActive(true); }
}
}
