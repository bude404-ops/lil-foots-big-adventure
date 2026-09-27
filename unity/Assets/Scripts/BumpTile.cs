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
                    p.lives++;
                    if (AudioManager.Instance != null) AudioManager.Instance.Play("heart");
                    StartCoroutine(PopOut(MakeSprite(HeartTex(), 0.34f), 0f, 0f));
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
