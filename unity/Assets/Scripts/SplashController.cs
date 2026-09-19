using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LilFoots
{
    /// <summary>Studio splash sequence (Bude, Sept 19: "big entertainment art and bude vision
    /// images after the unity loading screen... bith having their own seperate moment then you
    /// enter the game menu"). Runs AFTER Unity's own loading splash: each logo gets its own
    /// beat (fade in -> hold -> fade out), then the game menu (Map001) loads. Tap/click/any
    /// key skips to the next logo - standard studio-splash behavior.</summary>
    public class SplashController : MonoBehaviour
    {
        [Header("Splash logos, in order. Sprite null -> typographic fallback card.")]
        public Sprite[] logos = new Sprite[0];
        public string[] labels = new string[0];

        [Header("Per-logo cinematic sting (Bude, Sept 19: 'a cool cinematic sound like other games have'). Null -> silent.")]
        public AudioClip[] stings = new AudioClip[0];

        [Header("Timing per logo (seconds)")]
        public float fadeIn = 0.5f;
        public float hold = 1.6f;
        public float fadeOut = 0.5f;
        public float gap = 0.35f;

        Image img;
        Text txt;
        AudioSource stingSrc;
        bool skip;

        void Start()
        {
            Application.targetFrameRate = 60;
            stingSrc = gameObject.AddComponent<AudioSource>();
            stingSrc.playOnAwake = false;
            // Full-screen overlay canvas, black stage
            var canvasGo = new GameObject("SplashCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1334, 750);
            canvasGo.AddComponent<GraphicRaycaster>(); // keeps the scene's EventSystem honest

            var black = new GameObject("Black", typeof(Image));
            black.transform.SetParent(canvasGo.transform, false);
            var bi = black.GetComponent<Image>();
            bi.color = Color.black;
            RectTransformExtensions.Stretch(bi.rectTransform);

            var imgGo = new GameObject("LogoImage", typeof(Image));
            imgGo.transform.SetParent(canvasGo.transform, false);
            img = imgGo.GetComponent<Image>();
            img.preserveAspect = true;
            RectTransformExtensions.Center(img.rectTransform, 640);

            var txtGo = new GameObject("LogoText", typeof(Text));
            txtGo.transform.SetParent(canvasGo.transform, false);
            txt = txtGo.GetComponent<Text>();
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontSize = 72;
            txt.color = new Color(0.98f, 0.82f, 0.30f); // BIG gold
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            RectTransformExtensions.Center(txt.rectTransform, 900);

            img.gameObject.SetActive(false);
            txt.gameObject.SetActive(false);
            StartCoroutine(Sequence());
        }

        void Update()
        {
            if (Input.GetMouseButtonDown(0) || Input.touchCount > 0 || Input.anyKeyDown)
                skip = true;
        }

        IEnumerator Sequence()
        {
            int n = Mathf.Max(logos.Length, labels.Length);
            for (int i = 0; i < n; i++)
            {
                skip = false;
                var sprite = i < logos.Length ? logos[i] : null;
                var label = i < labels.Length ? labels[i] : "";
                img.gameObject.SetActive(sprite != null);
                txt.gameObject.SetActive(sprite == null);
                if (sprite != null) { img.sprite = sprite; img.SetNativeSize(); }
                else txt.text = label;

                var graphic = sprite != null ? (Graphic)img : txt;
                var sting = i < stings.Length ? stings[i] : null;
                if (sting != null) stingSrc.PlayOneShot(sting);
                yield return Fade(graphic, 0f, 1f, fadeIn);
                yield return Wait(hold);
                yield return Fade(graphic, 1f, 0f, fadeOut);
                yield return new WaitForSeconds(gap);
            }
            // Unity splash -> our studio splashes -> the game menu (Map001 = build index 1)
            SceneManager.LoadScene(SceneManager.sceneCountInBuildSettings > 1 ? 1 : 0);
        }

        IEnumerator Fade(Graphic g, float from, float to, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = dur <= 0f ? 1f : Mathf.Clamp01(t / dur);
                g.color = new Color(g.color.r, g.color.g, g.color.b, Mathf.Lerp(from, to, k));
                yield return null;
            }
            g.color = new Color(g.color.r, g.color.g, g.color.b, to);
        }

        IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds && !skip) { t += Time.unscaledDeltaTime; yield return null; }
        }
    }

    /// <summary>RectTransform layout helpers for the runtime-built splash UI.</summary>
    public static class RectTransformExtensions
    {
        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
        public static void Center(RectTransform rt, float width)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, 300f);
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
