using UnityEngine;

/// <summary>
/// STREAM FLOW (BudE, Sept 21 ~1:48 PM ET: "objects... that look like flowing water or
/// whatever for hazard environments we can bring to life"): hazard water is ALIVE.
/// Unity-native animation - the sprite's material texture scrolls continuously and the
/// surface bobs on a slow sine, so pit streams read as moving water instead of a pasted
/// picture. No custom engine, no external assets: a SpriteRenderer, a Repeat-wrap
/// texture clone, and an animated material offset.
/// </summary>
public class StreamFlow : MonoBehaviour {
    [Range(0f, 1f)] public float flowSpeed = 0.06f;   // texture loops per second (gentle current)
    public float bobAmplitude = 0.03f;                // u; subtle surface breathing
    public float bobFrequency = 0.7f;                 // Hz
    public bool flowLeft = false;

    Material mat;
    Vector3 basePos;
    Texture2D clone;   // only the runtime Repeat-wrap clone is destroyed - never the source sprite texture

    void Awake() {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) { enabled = false; return; }
        var tex = sr.sprite.texture;
        // clone with Repeat wrap so the offset can scroll seamlessly (source texture stays untouched)
        var rt = new Texture2D(tex.width, tex.height, tex.format, false);
        rt.wrapMode = TextureWrapMode.Repeat;
        rt.filterMode = FilterMode.Bilinear;
        rt.SetPixels32(tex.GetPixels32());
        rt.Apply();
        clone = rt;
        // material instance on the Sprites/Default shader - sprites stay crisp, UVs scroll
        mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = rt;
        sr.sharedMaterial = mat;
        basePos = transform.localPosition;
    }

    void Update() {
        if (mat == null) return;
        float d = Time.time * flowSpeed * (flowLeft ? -1f : 1f);
        mat.mainTextureOffset = new Vector2(d, 0f);
        transform.localPosition = basePos + Vector3.up * (Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f) * bobAmplitude);
    }

    void OnDestroy() {
        if (mat != null) Destroy(mat);
        if (clone != null) Destroy(clone);
    }
}
