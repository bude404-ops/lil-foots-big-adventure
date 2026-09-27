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

    /// <summary>[DETAIL PASS Sept 27 - BudE: "maps are just basic colors of brown and green
    /// so we need to actually start getting real details nd art in there"] The grass cap
    /// keeps the clean readable silhouette but gains REAL detail, all Unity-generated:
    /// dappled light (large value-noise), an undulating crisp top edge, scattered grass
    /// blades, tiny leaf specks and the occasional two-petal forest flower.</summary>
    static Texture2D CapTex(int variant, bool lipL, bool lipR) {
        var pal = Palette(Region);
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        int seed = 100 + Region * 10 + variant * 7;
        // deterministic daisy centers (4-6 per tile, away from the edges)
        int flowerN = 4 + (int)(Hash(0, 0, seed + 91) * 3f);
        var flowerCenters = new Vector2[flowerN];
        for (int i = 0; i < flowerN; i++)
            flowerCenters[i] = new Vector2(
                30f + Hash(i, 1, seed + 92) * (N - 60f),
                26f + Hash(i, 2, seed + 93) * (N - 70f));
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) {
            // 1) dappled light over the flat body (two-tone, no brushwork)
            // [BOLD PASS Sept 27 PM - BudE: "the map was the same"] the AM detail was
            // 6-14px on a 256px tile that renders ~81px on screen - invisible at game
            // zoom. Everything below is 3-4x larger so it READS while playing.
            float dapple = VNoise(x / 110f, y / 110f, seed);
            Color body = Color.Lerp(pal.capBody * 0.86f, pal.capBody * 1.14f, dapple);
            // 2) undulating crisp top edge (6-14px, follows slow noise)
            float edgeWave = VNoise(x / 26f, 0.5f, seed + 5);
            int edgeH = 6 + (int)(edgeWave * 8f);
            Color px = body;
            if (y >= N - edgeH) px = pal.capEdge;
            // 3) grass blades under the edge line - TALL clumps (18-44px), two tones
            float bladeCol = Hash(x, 0, seed + 11);
            if (bladeCol > 0.34f && (bladeCol < 0.44f || bladeCol < 0.52f && Hash(x, 1, seed + 12) > 0.5f)) {
                int bladeH = 18 + (int)(Hash(x, 2, seed + 13) * 26f);   // 18-44px tall
                int bladeTop = N - edgeH - bladeH;
                if (y >= bladeTop && y < N - edgeH - 1)
                    px = (Hash(x, 3, seed + 14) > 0.5f) ? pal.capBody * 1.30f : pal.capBody * 0.68f;
            }
            // 4) leaf specks + REAL daisies (petal discs ~13px, 4-6 per tile)
            float speck = Hash(x, y, seed + 21);
            if (speck > 0.992f && y < N - edgeH - 10) px = pal.capBody * 1.32f;      // light fleck
            else if (speck < 0.006f && y < N - edgeH - 10) px = pal.capBody * 0.68f; // dark fleck
            for (int fi = 0; fi < flowerCenters.Length; fi++) {
                float fdx = x - flowerCenters[fi].x, fdy = y - flowerCenters[fi].y;
                float fd = Mathf.Sqrt(fdx * fdx + fdy * fdy);
                if (fd <= 6.5f && fdy > -6f) {                       // warm face
                    px = new Color(0.95f, 0.83f, 0.42f);
                    if (fd > 4.6f) px = new Color(0.97f, 0.96f, 0.92f);   // white petal ring
                    if (fd > 5.6f) px *= 0.92f;                            // soft petal edge
                }
            }
            if (lipL && x < 16)  px *= (x < 3) ? 1.06f : 0.80f;
            if (lipR && x > N - 17) px *= (x > N - 4) ? 1.06f : 0.80f;
            tex.SetPixel(x, y, px);
        }
        tex.Apply();
        return tex;
    }

    /// <summary>[DETAIL PASS Sept 27] The dirt keeps the earthy two-stop base but gains
    /// REAL underground detail, all Unity-generated: soft wavy strata bands, scattered
    /// pebbles with highlights, a thin wandering root strand, faint moisture streaks.</summary>
    static Texture2D DirtTex(int variant, bool lipL, bool lipR) {
        var pal = Palette(Region);
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        int seed = 300 + Region * 10 + variant * 7;
        // deterministic pebbles (6-10 per tile)
        int pebN = 6 + (int)(Hash(0, 0, seed + 61) * 5f);
        var pebbleCenters = new Vector2[pebN];
        var pebbleRadii = new float[pebN];
        for (int i = 0; i < pebN; i++) {
            pebbleCenters[i] = new Vector2(
                22f + Hash(i, 1, seed + 62) * (N - 44f),
                20f + Hash(i, 2, seed + 63) * (N - 40f));
            pebbleRadii[i] = 5f + Hash(i, 3, seed + 64) * 4.5f;
        }
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) {
            float fy = y / (float)N;
            // 1) wavy strata: the base gradient warped by slow horizontal noise
            float warp = VNoise(x / 30f, y / 30f, seed) * 0.22f;
            Color px = Color.Lerp(pal.dirtTop, pal.dirtDeep, Mathf.Clamp01(fy + warp - 0.11f));
            // 2) BOLD strata bands (lighter/darker seams) - [BOLD PASS Sept 27 PM]
            float band = VNoise(x / 48f, (y + warp * 60f) / 28f, seed + 3);
            px *= 0.84f + 0.30f * band;
            // 3) pebbles: REAL stones (radius 5-9px, 6-10 per tile) with highlight + rim
            for (int pi = 0; pi < pebbleCenters.Length; pi++) {
                float pdx = x - pebbleCenters[pi].x, pdy = y - pebbleCenters[pi].y;
                float pd = Mathf.Sqrt(pdx * pdx + pdy * pdy);
                float pr = pebbleRadii[pi];
                if (pd <= pr) {
                    px = px * 1.06f + new Color(0.05f, 0.04f, 0.03f, 0f);           // stone body
                    if (pdy < -pr * 0.25f && pdx < pr * 0.3f) px = px * 1.35f + new Color(0.09f, 0.08f, 0.06f, 0f);  // lit top-left
                    if (pd > pr - 2.2f) px *= 0.58f;                                // dark rim
                }
            }
            // 4) one wandering root strand per tile (thick dark sine curve, deterministic)
            float rootPhase = VNoise(0.3f, fy * 3f, seed + 41) * 26f;
            float rootX = N * 0.5f + Mathf.Sin(fy * 7f + rootPhase) * 40f + (variant - 1.5f) * 30f;
            if (Mathf.Abs(x - rootX) < 3.4f && fy > 0.06f && fy < 0.9f) {
                px *= 0.45f;
                if (Mathf.Abs(x - rootX) > 2.0f) px *= 1.5f;   // lit edge beside the root
            }
            // 5) faint vertical moisture streaks
            px *= 1f - 0.05f * VNoise(x / 8f, y / 40f, seed + 55);
            if (lipL && x < 16)  px *= (x < 3) ? 1.05f : 0.80f;
            if (lipR && x > N - 17) px *= (x > N - 4) ? 1.05f : 0.80f;
            tex.SetPixel(x, y, px);
        }
        tex.Apply();
        return tex;
    }

    /// <summary>[DETAIL PASS Sept 27 - BudE: "blank sky"] The sky gains REAL atmosphere,
    /// all Unity-generated: a three-stop mist gradient, soft drifting clouds (two noise
    /// octaves, alpha-stretched horizontally), a warm morning sun glow high right, and a
    /// low horizon mist band. Still painterly-quiet, never busy.</summary>
    public static Texture2D SkyTex() {
        const int W = 1024, H = 512;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var top = new Color(0.55f, 0.68f, 0.66f);
        var mid = new Color(0.74f, 0.80f, 0.76f);
        var mist = new Color(0.88f, 0.92f, 0.89f);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) {
            float fy = y / (float)H;
            Color c = fy < 0.5f ? Color.Lerp(top, mid, fy / 0.5f) : Color.Lerp(mid, mist, (fy - 0.5f) / 0.5f);
            // clouds: two stretched noise octaves -> soft white bodies with lit tops
            float n1 = VNoise(x / 130f, y / 52f, 71);
            float n2 = VNoise(x / 46f, y / 30f, 72);
            // [BOLD PASS Sept 27 PM] lower threshold + higher alpha: clouds actually
            // register at phone zoom (were subpixel-faint before)
            float cloud = Mathf.SmoothStep(0.46f, 0.72f, n1 * 0.65f + n2 * 0.35f);
            if (cloud > 0f) {
                float lit = Mathf.Clamp01(n2 * 1.2f);
                Color cloudC = Color.Lerp(new Color(0.92f, 0.95f, 0.94f), new Color(1.00f, 0.99f, 0.97f), lit);
                c = Color.Lerp(c, cloudC, cloud * 0.88f);
            }
            // warm sun glow, high right
            float dx = (x - W * 0.78f) / (W * 0.30f), dy = (y - H * 0.24f) / (H * 0.30f);
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            c = Color.Lerp(c, new Color(1.00f, 0.94f, 0.76f), 0.52f * Mathf.Exp(-d * d * 2.2f));
            // low horizon mist band
            c = Color.Lerp(c, mist, Mathf.Pow(Mathf.Clamp01((fy - 0.82f) / 0.18f), 2f) * 0.6f);
            tex.SetPixel(x, y, c);
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
        // [FRESH-RUNNER FIX 2 Sept 27] same crash family as EnsureSprite/EnsureSky:
        // a tile PNG written this session is INVISIBLE to AssetImporter until the
        // database refreshes - GetAtPath returned null and NRE'd run 36291315199's
        // WebGL job at the ti.textureType bind (fresh runner, first tile load).
        // Refresh FIRST and never dereference a null importer.
        AssetDatabase.Refresh();
        var ti = (TextureImporter)AssetImporter.GetAtPath(png);
        if (ti == null) {
            Debug.LogError("[ProcTiles] tile importer missing for " + png + " - refresh failed");
            return null;   // LoadSet tolerates nulls; LoadTiles reports the failure
        }
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 256f;
        ti.filterMode = FilterMode.Bilinear;
        ti.mipmapEnabled = false;
        ti.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png);
        if (sprite == null) {
            Debug.LogError("[ProcTiles] tile sprite failed to import for " + png);
            return null;   // never bind a null sprite into a persisted tile asset
        }
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
        // [FRESH-RUNNER FIX Sept 26 PM] a file written this session is INVISIBLE to
        // AssetImporter until the database refreshes - GetAtPath returned null and killed
        // the whole forge run (36283818694, NRE at the sky bind). Refresh FIRST, and
        // never dereference a null importer.
        AssetDatabase.Refresh();
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null) { Debug.LogError("[ProcTiles] importer missing for " + path + " - refresh failed"); return null; }
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100f;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ==================== [DETAIL PASS Sept 27] CHARACTER-SELECT BACKDROP + CARD PANEL
    // (BudE Sept 27: "the character select menu is still the same messed up background")
    // The old painted story file (art_story_r1.png) and cedar panels are RETIRED from the
    // select screen per the full-cleanse law - the menu gets its own clean Unity-built
    // forest backdrop: deep pine gradient, layered cedar silhouettes, soft light shafts.

    /// <summary>Select-screen backdrop: deep pine top, mist glow floor, three layers of
    /// cedar silhouettes (classic stacked-frond triangles), diagonal light shafts.</summary>
    public static Texture2D MenuBackdropTex() {
        const int W = 1334, H = 750;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var deep = new Color(0.10f, 0.20f, 0.16f);
        var mid = new Color(0.22f, 0.36f, 0.28f);
        var mist = new Color(0.62f, 0.72f, 0.64f);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) {
            float fy = y / (float)H;
            Color c = fy < 0.55f ? Color.Lerp(deep, mid, fy / 0.55f) : Color.Lerp(mid, mist, (fy - 0.55f) / 0.45f);
            // layered cedar silhouettes: far (light haze), mid, near (darkest)
            for (int layer = 0; layer < 3; layer++) {
                float baseY = 0.38f + layer * 0.13f;                    // silhouette bases
                int period = 90 + layer * 46;                          // tree spacing tightens near
                int tx = (x + layer * 37) % period;
                float th = 0.16f + layer * 0.07f;                       // tree height fraction
                float treeFrac = (fy - (baseY - th)) / th;
                if (treeFrac < 0f || treeFrac > 1f) continue;
                // stacked fronds: triangle wave narrows with height
                float frond = Mathf.Abs(((tx / (float)period) * 2f - 1f)) * (1f - treeFrac * 0.55f);
                if (frond < 0.22f - treeFrac * 0.10f) {
                    Color tone = layer == 0 ? new Color(0.40f, 0.52f, 0.44f)
                               : layer == 1 ? new Color(0.26f, 0.40f, 0.31f)
                               : new Color(0.14f, 0.26f, 0.20f);
                    c = Color.Lerp(c, tone, 0.9f);
                }
            }
            // soft diagonal light shafts (two, top-left to mid-right)
            float shaft = Mathf.Sin((x + y * 1.45f) / 260f);
            if (shaft > 0.82f) c = Color.Lerp(c, new Color(0.98f, 0.95f, 0.82f), (shaft - 0.82f) * 1.2f);
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    /// <summary>Clean Unity-built card backing: rounded moss-dark panel with a lighter
    /// cedar rim - replaces the painted art_panel*.png on the select cards.</summary>
    public static Texture2D PanelTex() {
        const int S = 256, R = 34;
        var body = new Color(0.16f, 0.26f, 0.19f);
        var rim = new Color(0.55f, 0.44f, 0.28f);
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) {
            // rounded-rect distance
            float dx = Mathf.Max(0f, Mathf.Abs(x - S / 2f) - (S / 2f - R));
            float dy = Mathf.Max(0f, Mathf.Abs(y - S / 2f) - (S / 2f - R));
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            Color c = Color.clear;
            if (d <= R) {
                c = body;
                float edge = Mathf.Clamp01((R - d) / 9f);          // inner rim band
                if (edge < 1f) c = Color.Lerp(rim, body, edge);
                float soft = 0.9f + 0.1f * VNoise(x / 36f, y / 36f, 91);   // faint texture
                c *= soft;
            }
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    /// <summary>Menu backdrop as a loadable Sprite (fresh-runner-safe: refresh + guard).</summary>
    public static Sprite EnsureMenuBackdrop() {
        const string path = "Assets/Art/Generated/unity_menu_backdrop.png";
        Directory.CreateDirectory("Assets/Art/Generated");
        if (!File.Exists(path)) File.WriteAllBytes(path, MenuBackdropTex().EncodeToPNG());
        AssetDatabase.Refresh();
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null) { Debug.LogError("[ProcTiles] menu backdrop importer missing for " + path); return null; }
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100f;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>Card panel as a loadable Sprite (fresh-runner-safe: refresh + guard).</summary>
    public static Sprite EnsurePanel() {
        const string path = "Assets/Art/Generated/unity_panel.png";
        Directory.CreateDirectory("Assets/Art/Generated");
        if (!File.Exists(path)) File.WriteAllBytes(path, PanelTex().EncodeToPNG());
        AssetDatabase.Refresh();
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null) { Debug.LogError("[ProcTiles] panel importer missing for " + path); return null; }
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
        // [FRESH-RUNNER FIX Sept 26 PM] see EnsureSprite: refresh before GetAtPath,
        // null-guard the importer. This exact line NRE'd run 36283818694's WebGL job.
        AssetDatabase.Refresh();
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null) { Debug.LogError("[ProcTiles] sky importer missing for " + path); return null; }
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100f;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
}
#endif
