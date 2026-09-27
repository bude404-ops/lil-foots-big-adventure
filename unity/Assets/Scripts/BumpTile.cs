using System.Collections;
using UnityEngine;

namespace LilFoots {
    /// <summary>
    /// Lil Foots: Big Adventure — Mario-style bump tile (BudE Sept 27 PM: "can we make
    /// certain tiles hittable if you [hit] below them like in mario? For where hidden
    /// stuff can be?"). The player jumps into the UNDERSIDE of the block: the block
    /// pops, its content bursts out and is collected instantly, and the block dims to a
    /// "used" state. Hidden blocks are invisible until first bumped (classic secret
    /// blocks). Contents: "token" (+1), "big" (+5), "heart" (+1 life).
    /// All visuals are runtime-generated sprites (no AssetDatabase — build-safe).
    /// </summary>
    public class BumpTile : MonoBehaviour {
        public string content = "token";   // token | big | heart
        public bool hidden = false;       // invisible until bumped
        public bool used = false;
        Transform art;

        void Start() {
            art = transform.Find("BumpArt");
            if (hidden && art != null) art.gameObject.SetActive(false);
        }

        /// Called by PlayerController when the head hits the block's underside.
        public void Bump() {
            if (used) return;
            used = true;
            var p = PlayerController.Instance;
            if (p != null) {
                if (content == "heart") {
                    // [SPIRIT CHARM Sept 27 PM - BudE approved] extra life = forest-spirit
                    // wisp (lore-native heart replacement), restored on the trail.
                    p.lives++;
                    if (AudioManager.Instance != null) AudioManager.Instance.Play("heart");
                    StartCoroutine(PopOut(MakeSprite(CharmTex(), 0.34f), 0f, 0f));
                } else if (content == "bark") {
                    // [CEDAR BARK HIDE Sept 27 PM - BudE approved power-up] pops out and
                    // waits on the ground below: touch it to gain ONE FREE HIT (gold rim
                    // glow while held; the glow pops instead of a footprint when you take it).
                    StartCoroutine(BarkOut());
                } else {
                    int n = content == "big" ? 5 : 1;
                    for (int i = 0; i < n; i++)
                        StartCoroutine(PopOut(MakeSprite(CoinTex(), 0.28f), i * 0.06f, (i - (n - 1) / 2f) * 0.30f));
                    p.tokens += n;
                    Sfx.Play(Sfx.Clip.Coin);
                }
            }
            if (art != null) { art.gameObject.SetActive(true); StartCoroutine(BumpAnim()); }
        }

        IEnumerator BumpAnim() {
            // pop the whole block up then settle; dim to the used state
            Vector3 baseP = art.localPosition;
            float t = 0;
            while (t < 0.22f) {
                t += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(t / 0.22f) * Mathf.PI);
                art.localPosition = baseP + Vector3.up * 0.16f * k;
                yield return null;
            }
            art.localPosition = baseP;
            var sr = art.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = new Color(0.60f, 0.56f, 0.52f, 1f);   // used: dimmed, no glow read
        }

        /// Content burst: rises out of the block, hangs, shrinks away (collected on bump).
        IEnumerator PopOut(Sprite s, float delay, float side) {
            if (s == null) yield break;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            var go = new GameObject("BumpDrop");
            go.transform.position = transform.position + Vector3.up * 0.55f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.sortingOrder = 12;
            Vector3 p0 = go.transform.position + new Vector3(side, 0f, 0f);
            float t = 0;
            while (t < 0.45f) {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.45f);
                float rise = 1.25f * k - 1.6f * k * k;       // up, then drifts back down
                go.transform.position = p0 + Vector3.up * rise;
                if (k > 0.55f) {                              // fade-out shrink on the way down
                    float sc = Mathf.Max(0f, 1f - (k - 0.55f) / 0.45f);
                    go.transform.localScale = Vector3.one * sc;
                }
                yield return null;
            }
            Destroy(go);
        }

        // ---- runtime sprites (pure Texture2D + Sprite.Create - no editor APIs) ----
        static Sprite MakeSprite(Texture2D tex, float size) {
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width / size);
        }
        static Texture2D CoinTex() {
            const int S = 24;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) {
                float d = Mathf.Sqrt((x - S / 2f + 0.5f) * (x - S / 2f + 0.5f) + (y - S / 2f + 0.5f) * (y - S / 2f + 0.5f));
                Color c = Color.clear;
                if (d < S / 2f) c = Color.Lerp(new Color(1f, 0.85f, 0.35f), new Color(0.85f, 0.60f, 0.18f), d / (S / 2f));
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }
        /// [SPIRIT CHARM Sept 27 PM] runtime wisp (lore-native heart), same look as the edit-time unity_charm.
        static Texture2D CharmTex() {
            const int S = 26;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) {
                float nx = (x - S / 2f + 0.5f) / (S / 2f), ny = (y - S / 2f + 0.5f) / (S / 2f);
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                Color c = Color.clear;
                if (d < 1f) {
                    float shape = d * (1f - 0.25f * Mathf.Clamp01(ny));
                    float a = Mathf.Clamp01(1f - shape); a = a * a * 1.6f;
                    c = Color.Lerp(new Color(0.62f, 0.85f, 0.52f), new Color(1f, 0.98f, 0.85f), Mathf.Clamp01(1f - shape * 1.15f));
                    c.a = Mathf.Clamp01(a);
                }
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }

        /// [CEDAR BARK HIDE Sept 27 PM] pickup chip: cedar bark with a glowing gold rim.
        static Texture2D BarkTex() {
            const int S = 24;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) {
                float dx = Mathf.Max(0f, Mathf.Abs(x - S / 2f + 0.5f) - (S / 2f - 3f));
                float dy = Mathf.Max(0f, Mathf.Abs(y - S / 2f + 0.5f) - (S / 2f - 3f));
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                Color c = Color.clear;
                if (d <= 3f) {
                    float fy = y / (float)S;
                    c = Color.Lerp(new Color(0.47f, 0.36f, 0.22f), new Color(0.33f, 0.25f, 0.15f), fy);
                    float grain = 0.5f + 0.5f * Mathf.Sin(y * 1.7f + Mathf.Sin(x * 0.9f) * 2f);
                    c *= 0.90f + 0.12f * grain;
                    if (d > 1.2f) c = Color.Lerp(c, new Color(1f, 0.87f, 0.45f), 0.85f);   // glowing gold rim
                }
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }

        /// Bark pickup: pops out of the block, drifts to the ground below, bobs until touched.</summary>
        IEnumerator BarkOut() {
            var go = new GameObject("BarkPickup");
            var spr = MakeSprite(BarkTex(), 0.42f);
            var sr = go.AddComponent<SpriteRenderer>();
            if (spr != null) { sr.sprite = spr; sr.sortingOrder = 11; go.transform.localScale = Vector3.one; }
            var cc = go.AddComponent<CircleCollider2D>(); cc.isTrigger = true; cc.radius = 0.40f;
            var bp = go.AddComponent<BarkPickup>();
            // pop out of the block, then fall until just above the ground below
            Vector3 from = transform.position + Vector3.up * 0.4f;
            float t = 0;
            while (t < 0.35f) {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.35f);
                go.transform.position = Vector3.Lerp(from, from + Vector3.up * 0.45f, k)
                                      + Vector3.up * (0.22f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            int mask = LayerMask.GetMask("Ground");
            var hit = Physics2D.Raycast(go.transform.position, Vector2.down, 6f, mask);
            float restY = hit ? hit.point.y + 0.45f : 6.65f;
            bp.basePos = new Vector3(go.transform.position.x, restY, 0f);
            bp.settle = true;
        }

        /// Bark pickup body: bobs at rest; player touch -> one free hit.</summary>
        class BarkPickup : MonoBehaviour {
            public Vector3 basePos;
            public bool settle;
            void Update() {
                if (!settle) return;
                transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 3f) * 0.08f);
            }
            void OnTriggerEnter2D(Collider2D c) {
                var p = c.GetComponentInParent<PlayerController>();
                if (p == null || p.bark > 0) return;   // one hide at a time
                p.bark = 1;
                Sfx.Play(Sfx.Clip.Coin);
                Destroy(gameObject);
            }
        }

        static Texture2D HeartTex() {
            const int S = 26;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) {
                float nx = (x - S / 2f + 0.5f) / (S / 2f), ny = (y - S / 2f + 0.5f) / (S / 2f);
                float lobes = Mathf.Sqrt((nx + 0.42f) * (nx + 0.42f) + (ny - 0.25f) * (ny - 0.25f) * 1.7f);
                float lobes2 = Mathf.Sqrt((nx - 0.42f) * (nx - 0.42f) + (ny - 0.25f) * (ny - 0.25f) * 1.7f);
                float tip = (ny + 0.85f) - Mathf.Abs(nx) * 1.35f;
                Color c = Color.clear;
                if (lobes < 0.52f || lobes2 < 0.52f || (tip < 0f && Mathf.Abs(nx) < 0.62f && ny < 0.25f && ny > -0.95f))
                    c = new Color(0.86f, 0.18f, 0.20f);
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }
    }
}
