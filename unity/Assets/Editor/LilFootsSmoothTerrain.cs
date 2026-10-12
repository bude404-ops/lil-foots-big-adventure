// LilFootsSmoothTerrain.cs - SMOOTH FLOWING TERRAIN (BudE Oct 12: "everything is jagged, doesn't flow smooth")
// Replaces the straight-rectangle ground canvases with one continuous world-anchored silhouette:
// soft undulating grassline (never dips below the walk line), rounded flowing shoulders on exposed
// lips, antialiased alpha edges, seamless across strips and welded bodies (noise sampled in WORLD x),
// no horizontal stretching (strips capped at 40u, import max 4096).
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace LilFoots {
public static class LilFootsSmoothTerrain {
    const int PXU = 96;
    const float BLEED = 0.30f;    // max silhouette height above the walk line (u)

    public static void Apply(GameObject mapRoot) {
        if (mapRoot == null) { Debug.LogError("[SmoothTerrain] map root not found"); return; }
        for (int i = mapRoot.transform.childCount - 1; i >= 0; i--) {
            var ch = mapRoot.transform.GetChild(i);
            if (ch.name.StartsWith("GroundArt") || ch.name.StartsWith("SmoothGround")) Object.DestroyImmediate(ch.gameObject);
        }
        var bodies = new List<BoxCollider2D>();
        foreach (Transform child in mapRoot.transform) {
            var bc = child.GetComponent<BoxCollider2D>();
            if (bc != null && bc.size.y >= 2f) bodies.Add(bc);
        }
        var boxes = new List<Rect>();
        foreach (var bc in bodies) boxes.Add(new Rect(bc.bounds.min.x, bc.bounds.min.y, bc.size.x, bc.size.y));
        int made = 0;
        for (int i = 0; i < bodies.Count; i++) {
            var bc = bodies[i];
            float wU = bc.size.x, hU = bc.size.y;
            float top = bc.bounds.max.y, x0 = bc.bounds.min.x, y0 = bc.bounds.min.y;
            bool lipL = !SideCovered(boxes, i, true, top);
            bool lipR = !SideCovered(boxes, i, false, top);
            int strips = Mathf.Max(1, Mathf.CeilToInt(wU / 40f));
            float sw = wU / strips;
            for (int s = 0; s < strips; s++) {
                float sx0 = x0 + s * sw;
                bool eL = lipL && s == 0, eR = lipR && s == strips - 1;
                var sp = EnsureSprite(sw, hU, eL, eR, sx0, i);
                if (sp == null) continue;
                var go = new GameObject("SmoothGround_" + i + "_" + s);
                go.transform.SetParent(mapRoot.transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sp;
                sr.sortingOrder = -2;
                go.transform.position = new Vector3(sx0 + sw / 2f, y0 + (hU + BLEED) / 2f, 0f);
                made++;
            }
        }
        Debug.Log("[SmoothTerrain] " + made + " smooth canvases over " + bodies.Count + " ground bodies");
    }

    static bool SideCovered(List<Rect> b, int i, bool left, float top) {
        float myEdge = left ? b[i].xMin : b[i].xMax;
        for (int j = 0; j < b.Count; j++) {
            if (j == i) continue;
            float vOv = Mathf.Min(b[i].yMax, b[j].yMax) - Mathf.Max(b[i].yMin, b[j].yMin);
            if (vOv < 0.5f) continue;
            float theirFace = left ? b[j].xMax : b[j].xMin;
            if (Mathf.Abs(theirFace - myEdge) < 0.4f) return true;
        }
        return false;
    }

    static Sprite EnsureSprite(float sw, float hU, bool eL, bool eR, float sx0, int idx) {
        string key = "smooth_" + idx + "_" + Mathf.Round(sx0 * 8) + "_" + Mathf.Round(sw * 8) + "x" + Mathf.Round(hU * 8) +
                     (eL ? "L" : "") + (eR ? "R" : "");
        string dir = "Assets/Art/Generated";
        Directory.CreateDirectory(dir);
        string path = dir + "/" + key + ".png";
        if (!File.Exists(path))
            File.WriteAllBytes(path, GroundTex(Mathf.RoundToInt(sw * PXU), Mathf.RoundToInt((hU + BLEED) * PXU), eL, eR, sx0, idx).EncodeToPNG());
        AssetDatabase.Refresh();
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null) return null;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = PXU;
        ti.mipmapEnabled = false;
        ti.maxTextureSize = 4096;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static float Hsh(int x, int y, int s) {
        int h = x * 374761393 + y * 668265263 + s * unchecked((int)2246822519u);
        h = (h ^ (h >> 13)) * 1274126177;
        return ((h ^ (h >> 16)) & 0x7fffffff) / (float)0x7fffffff;
    }
    static float Vals(float x, float y, int s) {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = x - xi, fy = y - yi;
        float a = Hsh(xi, yi, s), b = Hsh(xi + 1, yi, s), c = Hsh(xi, yi + 1, s), d = Hsh(xi + 1, yi + 1, s);
        float u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }
    // smooth flowing silhouette height (0..1) sampled in WORLD units - seamless across strips/bodies
    static float Flow(float xw, int seed) {
        float v = 0.54f * Vals(xw / 6.5f, 3.1f, seed + 11)
                + 0.30f * Vals(xw / 2.8f, 7.7f, seed + 23)
                + 0.16f * Vals(xw / 1.3f, 9.2f, seed + 37);
        return Mathf.Clamp01(v);
    }

    static Texture2D GroundTex(int W, int H, bool eL, bool eR, float sx0, int seed) {
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var px = new Color32[W * H];
        var soilTop = new Color32(76, 54, 34, 255);
        var soilDeep = new Color32(41, 27, 16, 255);
        var capLight = new Color32(96, 120, 66, 255);
        var capMid = new Color32(70, 93, 57, 255);
        var capDeep = new Color32(52, 72, 43, 255);
        var mossLite = new Color32(118, 140, 82, 255);
        int yLine = H - Mathf.RoundToInt(BLEED * PXU);
        int capH = Mathf.Clamp(Mathf.RoundToInt(H * 0.14f), 30, 84);
        int E = Mathf.RoundToInt(0.30f * PXU);
        var topY = new float[W];
        for (int x = 0; x < W; x++) {
            float xw = sx0 + x / (float)PXU;
            float h = Flow(xw, seed);
            if (eL && x < E) h *= Mathf.SmoothStep(0f, 1f, x / (float)E);
            if (eR && x >= W - E) h *= Mathf.SmoothStep(0f, 1f, (W - 1 - x) / (float)E);
            topY[x] = yLine + h * (BLEED * PXU);
        }
        for (int x = 0; x < W; x++) {
            float xw = sx0 + x / (float)PXU;
            for (int y = 0; y < H; y++) {
                float a = 1f;
                float t = topY[x];
                if (y > t + 1.2f) a = 0f;
                else if (y > t - 0.8f) a = Mathf.Clamp01((t + 1.2f - y) / 2f);
                Color32 c = new Color32(0, 0, 0, 0);
                if (a > 0f) {
                    if (y >= yLine) {
                        float tip = Mathf.Clamp01((y - yLine) / (BLEED * PXU));
                        c = Color32.Lerp(capMid, capLight, 0.35f + 0.5f * (1f - tip));
                        float tuft = Vals(xw / 2.6f, y / 5f, seed + 21);
                        if (tuft > 0.80f) c = Color32.Lerp(c, mossLite, 0.35f * (tuft - 0.80f) / 0.2f);
                    } else if (y >= yLine - capH) {
                        float g = Mathf.Clamp01((float)(yLine - y) / capH);
                        c = Color32.Lerp(capLight, capDeep, g * g);
                        float tuft = Vals(xw / 2.6f, y / 5f, seed + 21);
                        if (tuft > 0.82f && y < yLine - 3) c = Color32.Lerp(c, mossLite, 0.4f * (tuft - 0.82f) / 0.18f);
                        if (y > yLine - 7) c = Color32.Lerp(c, capDeep, 0.45f);
                    } else {
                        float d = Mathf.Clamp01(y / (float)yLine);
                        c = Color32.Lerp(soilDeep, soilTop, d * d * 0.9f + 0.1f);
                        float strata = 0.5f + 0.5f * Mathf.Sin(y * 0.11f + Vals(xw / 26f, y / 26f, seed + 3) * 6f);
                        if (strata > 0.78f) c = Color32.Lerp(c, soilDeep, 0.25f * (strata - 0.78f) / 0.22f);
                        float grain = Vals(xw / 5f, y / 5f, seed + 31);
                        if (grain > 0.88f) c = Color32.Lerp(c, soilTop, 0.35f);
                        float pxn = Vals(xw / 18f, y / 18f, seed + 41);
                        if (pxn > 0.93f && Vals(xw / 4f, y / 4f, seed + 43) > 0.35f)
                            c = Color32.Lerp(c, new Color32(55, 40, 27, 255), 0.6f);
                        float sp = Hsh(x, y, seed + 55);
                        if (sp > 0.5f) c = Color32.Lerp(c, soilDeep, 0.06f); else c = Color32.Lerp(c, soilTop, 0.05f);
                    }
                    if (eL && x < 26) { float f = 1f - x / 26f; c = Color32.Lerp(c, soilDeep, 0.20f * f); if (y > yLine - 12) c = Color32.Lerp(c, mossLite, 0.25f * f); }
                    if (eR && x >= W - 26) { float f = (x - (W - 26)) / 26f; c = Color32.Lerp(c, soilDeep, 0.20f * f); if (y > yLine - 12) c = Color32.Lerp(c, mossLite, 0.25f * f); }
                }
                c.a = (byte)(a * 255f);
                px[y * W + x] = c;
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
}
#endif
