#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.IO;

namespace LilFoots {
/// <summary>
/// UNITY-BUILT MAP SKIN (BudE, Sept 26 2026 ~1:36 AM ET: "few things big we need to cleanse
/// the unity map of the artwork and have it start a new"). FULL CLEANSE: every painted PNG
/// is gone AND the first procedural "textured" pass is retired too - the map starts ANEW
/// with CLEAN, FLAT, UNITY-NATIVE visuals: solid sage grass caps with a crisp darker top
/// edge, flat two-stop earth fill, clean flat gradient sky, and flat Unity-built props
/// (coin / totem / heart). No noise, no fringe, no brushwork, no painted files anywhere -
/// the map is pure readable geometry until BudE art-directs the next pass himself.
/// 256px @ 256ppu = 1u = one tilemap cell (the quad-mirror law stays).
/// </summary>
public static class LilFootsProcTiles {

    // ---------------- REGION PALETTES (BudE, Sept 26 ~1:40 AM ET: "we need to make sure the
    // art is created for the actual maps aswell for their region") ----------------
    // Every region's terrain is generated in ITS OWN biome palette. LILFOOTS_REGION=N
    // (forge input / env) selects the palette; region > 1 tiles are named r<N>_<tile>
    // (Region 1 keeps the canon lf_* names). Hand-art reskin hooks work per region the
    // same way (drop PNGs at the r<N>_ paths and they win).
    //   1 Old Growth (PNW)  - clean flat sage + crisp edge
    //   2 The High White     - snowfield white-blue caps + blue-grey glacier rock
    //   3 The Red Dust       - sun-bleached ochre caps + red-earth fill
    //   4 The Still Water    - murky swamp green + wet dark muck
    //   5 The Deep Green     - vivid jungle greens + loam
    struct Pal { public Color capBody, capEdge, dirtTop, dirtDeep; }
    static Pal Palette(int r) {
        switch (r) {
            case 2: return new Pal {
                capBody = new Color(0.93f, 0.97f, 1.00f), capEdge = new Color(0.70f, 0.82f, 0.96f),
                dirtTop = new Color(0.46f, 0.51f, 0.62f),  dirtDeep = new Color(0.22f, 0.26f, 0.36f) };
            case 3: return new Pal {
                capBody = new Color(0.86f, 0.62f, 0.38f), capEdge = new Color(0.62f, 0.36f, 0.18f),
                dirtTop = new Color(0.56f, 0.35f, 0.21f),  dirtDeep = new Color(0.30f, 0.16f, 0.09f) };
            case 4: return new Pal {
                capBody = new Color(0.42f, 0.60f, 0.42f), capEdge = new Color(0.22f, 0.38f, 0.24f),
                dirtTop = new Color(0.31f, 0.27f, 0.17f), dirtDeep = new Color(0.13f, 0.12f, 0.07f) };
            case 5: return new Pal {
                capBody = new Color(0.33f, 0.72f, 0.30f), capEdge = new Color(0.14f, 0.42f, 0.16f),
                dirtTop = new Color(0.38f, 0.31f, 0.16f), dirtDeep = new Color(0.16f, 0.13f, 0.07f) };
            default: return new Pal {
                capBody = new Color(0.45f, 0.58f, 0.39f), capEdge = new Color(0.33f, 0.44f, 0.30f),
                dirtTop = new Color(0.36f, 0.32f, 0.23f), dirtDeep = new Color(0.27f, 0.25f, 0.17f) };
        }
    }
    public static int Region {
        get {
            int r;
            int.TryParse(System.Environment.GetEnvironmentVariable("LILFOOTS_REGION") ?? "1", out r);
            return (r >= 1 && r <= 5) ? r : 1;
        }
    }
    static string RegionName(string tile) { return Region == 1 ? tile : "r" + Region + "_" + tile; }
    const int N = 256;   // 256px @ 256ppu = 1u per tile
    const string DIR = "Assets/Art/Tiles";

    static float Hash(int x, int y, int seed) {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2246822519);
        h = (h ^ (h >> 13)) * 1274126177;
        return ((h ^ (h >> 16)) & 0xffffff) / 16777215f;
    }
    static float VNoise(float x, float y, int seed) {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float xf = x - xi, yf = y - yi;
        float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
        float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed);
        float c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    /// <summary>CLEAN grass cap: flat sage body + crisp darker top edge line. No noise.</summary>
    static Texture2D CapTex(int variant, bool lipL, bool lipR) {
        var pal = Palette(Region);
        var body = pal.capBody;   // region's flat cap color
        var edge = pal.capEdge;   // crisp top edge line
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) {
            var px = (y >= N - 4) ? edge : body;
            if (lipL && x < 16)  px *= (x < 3) ? 1.06f : 0.80f;
            if (lipR && x > N - 17) px *= (x > N - 4) ? 1.06f : 0.80f;
            tex.SetPixel(x, y, px);
        }
        tex.Apply();
        return tex;
    }

    /// <summary>CLEAN dirt fill: flat two-stop earth gradient. No strata, no speckles.</summary>
    static Texture2D DirtTex(int variant, bool lipL, bool lipR) {
        var pal = Palette(Region);
        var top  = pal.dirtTop;
        var deep = pal.dirtDeep;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) {
            var px = Color.Lerp(top, deep, y / (float)N);
            if (lipL && x < 16)  px *= (x < 3) ? 1.05f : 0.80f;
            if (lipR && x > N - 17) px *= (x > N - 4) ? 1.05f : 0.80f;
            tex.SetPixel(x, y, px);
        }
        tex.Apply();
        return tex;
    }

    /// <summary>CLEAN sky: soft flat sage-to-mist gradient, nothing else.</summary>
    public static Texture2D SkyTex() {
        const int W = 512, H = 256;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        for (int y = 0; y < H; y++) {
            float fy = y / (float)H;
            var c = Color.Lerp(new Color(0.74f, 0.80f, 0.76f), new Color(0.88f, 0.92f, 0.89f), Mathf.Pow(fy, 1.3f));
            for (int x = 0; x < W; x++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    // ==================== CLEAN UNITY-BUILT PROPS (no painted files on the map) ====================
    public static Texture2D CoinTex() {
        const int S = 128, C = 64, R = 54;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) {
            float d = Mathf.Sqrt((x - C + 0.5f) * (x - C + 0.5f) + (y - C + 0.5f) * (y - C + 0.5f));
            Color c = Color.clear;
            if (d <= R)      c = new Color(0.87f, 0.70f, 0.22f);       // flat gold
            if (d > R - 7) c = new Color(0.66f, 0.49f, 0.13f);         // darker rim ring
            if (d <= R - 18) c = new Color(0.93f, 0.79f, 0.34f);       // light inner face
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    public static Texture2D TotemTex() {
        const int W = 128, H = 256;
        var post = new Color(0.42f, 0.29f, 0.18f);   // cedar brown
        var cap  = new Color(0.29f, 0.42f, 0.27f);   // moss green cap
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) {
            bool capBand = y >= H - 26 && x >= 42 && x < 86;   // moss cap on top of the post
            bool body = x >= 50 && x < 78;                     // post body
            Color c = Color.clear;
            if (capBand) c = cap;
            else if (body) c = post;
            if (body && y < 6) c *= 0.7f;                      // darker foot line
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    public static Texture2D HeartTex() {
        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) {
            float px = (x + 0.5f) / S * 2.3f - 1.15f;
            float py = (y + 0.5f) / S * 2.3f - 1.15f - 0.25f;
            float v = (px * px + py * py - 1f);
            bool inside = v * v * v - px * px * py * py * py <= 0f;   // classic heart curve
            tex.SetPixel(x, y, inside ? new Color(0.76f, 0.23f, 0.31f) : Color.clear);
        }
        tex.Apply();
        return tex;
    }

    static int VariantOf(string name) {
        char c = name.Length > 0 ? name[name.Length - 1] : 'a';
        return char.IsDigit(c) ? (c - '0') : (char.ToLower(c) - 'a');
    }

    /// <summary>Generates the PNG if missing (Unity builds it), imports at 256ppu, and
    /// returns a persistent Tile asset. No painted art is loaded - the file at
    /// Assets/Art/Tiles/<name>.png IS the Unity-generated skin.</summary>
    public static TileBase EnsureTile(string tileName) {
        string name = RegionName(tileName);   // per-region art sets (r2_* snowfield, r3_* red dust, ...)
        Directory.CreateDirectory(DIR);
        string png = DIR + "/" + name + ".png";
        string asset = DIR + "/" + name + ".asset";
        if (!File.Exists(png)) {
            bool dirt = name.Contains("dirt");
            bool lipL = name.EndsWith("_l");
            bool lipR = name.EndsWith("_r");
            Texture2D tex = dirt ? DirtTex(VariantOf(name), lipL, lipR)
                                 : CapTex(VariantOf(name), lipL, lipR);
            File.WriteAllBytes(png, tex.EncodeToPNG());
        }
        var ti = (TextureImporter)AssetImporter.GetAtPath(png);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 256f;
        ti.filterMode = FilterMode.Bilinear;
        ti.mipmapEnabled = false;
        ti.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png);
        var t = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(asset);
        if (t == null) {
            t = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            AssetDatabase.CreateAsset(t, asset);
        }
        // REBIND LAW (Sept 25): the tile binds whatever the PNG says TODAY.
        t.sprite = sprite;
        t.name = name;
        EditorUtility.SetDirty(t);
        return t;
    }

    /// <summary>Clean Unity-built PROP sprite (coin / totem / heart) - no painted files.</summary>
    public static Sprite EnsureSprite(string name) {
        const string dir = "Assets/Art/Generated";
        Directory.CreateDirectory(dir);
        string path = dir + "/" + name + ".png";
        if (!File.Exists(path)) {
            Texture2D tex;
            if (name == "unity_coin") tex = CoinTex();
            else if (name == "unity_totem") tex = TotemTex();
            else if (name == "unity_quad") {
                // [TILEMAP CLEANSE Sept 26] flat white quad: the Unity-native building block for
                // hop slabs, gate/portal arches - tinted per use, zero painted files.
                tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
                var wp = new Color32[64];
                for (int i = 0; i < 64; i++) wp[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(wp); tex.Apply();
            }
            else tex = HeartTex();
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100f;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>The Unity-generated sky as a loadable Sprite (camera-pinned backdrop).</summary>
    public static Sprite EnsureSky() {
        const string path = "Assets/Art/Generated/unity_sky_v2.png";   // v2 = clean flat cleanse (no stale textured sky)
        Directory.CreateDirectory("Assets/Art/Generated");
        if (!File.Exists(path)) File.WriteAllBytes(path, SkyTex().EncodeToPNG());
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100f;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
}
#endif
