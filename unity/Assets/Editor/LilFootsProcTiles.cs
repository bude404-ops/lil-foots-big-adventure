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

    /// <summary>[STYLE MATCH Sept 27 PM] cuts a 256px tile out of the painterly terrain
    /// art that matches the character style. Caps crop from the TOP band of paint_grass.png
    /// (grass line + soil transition, horizontally seamless so wrapped crops tile); dirt
    /// crops quadrants of paint_dirt.png (seamless both axes). Variants get a tiny per-tile
    /// brightness jitter so treads never read as a photocopied grid. Null when absent.</summary>
    static Texture2D _paintGrass, _paintDirt;
    static int _capTopRow = -1;   // detected grass-cap edge in paint_grass.png
    static Texture2D PaintedTileTex(bool dirt, int variant, bool lipL = false, bool lipR = false) {
        try {
            if (dirt && _paintDirt == null && System.IO.File.Exists("Assets/Art/paint_dirt.png"))
                { _paintDirt = new Texture2D(2, 2); _paintDirt.LoadImage(System.IO.File.ReadAllBytes("Assets/Art/paint_dirt.png")); }
            if (!dirt && _paintGrass == null && System.IO.File.Exists("Assets/Art/paint_grass.png"))
                { _paintGrass = new Texture2D(2, 2); _paintGrass.LoadImage(System.IO.File.ReadAllBytes("Assets/Art/paint_grass.png")); }
            var srcTex = dirt ? _paintDirt : _paintGrass;
            if (srcTex == null) return null;
            int W = srcTex.width, H = srcTex.height, T = 256;
            var src = srcTex.GetPixels32();
            var outc = new Color32[T * T];
            int v = ((variant % 4) + 4) % 4;
            int y0;
            if (dirt) {
                // dirt: two clean soil windows, skipping the washed-out top quarter
                y0 = 128 + (v / 2) * 128;
            } else {
                // cap: crop from the DETECTED grass line (the painted source can carry a
                // light wash above the cap - the generated art puts sky glow there), so
                // find the first strongly green-dominant row and put the organic edge at
                // the top of the tile: blade tips peek in, grass cap, soil below.
                int capTop = _capTopRow;
                if (capTop < 0) {
                    var cpx = srcTex.GetPixels32();
                    int best = H / 3;
                    for (int r = 8; r < H - 8; r++) {
                        float gd = 0f;
                        for (int xx = 0; xx < W; xx += 8) { var c = cpx[r * W + xx]; gd += c.g - c.r; }
                        if (gd / (W / 8) > 10f) { best = r; break; }
                    }
                    _capTopRow = capTop = best;
                }
                y0 = Mathf.Clamp(capTop - 24, 0, H - T);
            }
            for (int y = 0; y < T; y++) {
                int sy = y0 + y;
                for (int x = 0; x < T; x++) {
                    int sx = (v * (W / 4) + x * (W / 4) / T) % W;             // 4 windows, wrapping seam
                    outc[y * T + x] = src[sy * W + sx];
                }
            }
            // [STORYBOOK Sept 27 PM] exposed-edge caps: the open side of a ground gets a
            // soft mossy curl highlight at the top corner and a gentle vertical shade
            // down the edge - the lip reads as a rounded storybook earth edge, not a cut.
            if (lipL || lipR) {
                int E = 30;
                for (int x = 0; x < E; x++) {
                    float u = x / (float)(E - 1);
                    float f = lipL ? (1f - u) : u;                     // 1 at the exposed column
                    float mul = 1f - 0.20f * f;
                    for (int y = 0; y < T; y++) {
                        int xi = lipL ? x : (T - 1 - x);
                        var cc = outc[y * T + xi];
                        cc.r = (byte)(cc.r * mul); cc.g = (byte)(cc.g * mul); cc.b = (byte)(cc.b * mul);
                        if (y < 8) {                                     // mossy curl light at the lip crest
                            float hi = (1f - y / 8f) * f * 0.18f;
                            cc.r = (byte)Mathf.Min(255, cc.r + 255 * hi * 0.6f);
                            cc.g = (byte)Mathf.Min(255, cc.g + 255 * hi);
                            cc.b = (byte)Mathf.Min(255, cc.b + 255 * hi * 0.4f);
                        }
                        outc[y * T + xi] = cc;
                    }
                }
            }
            // [STORYBOOK Sept 27 PM] dirt variants are DEPTH SHADES: soil gets warmer-
            // darker the deeper the tile sits (storybook cross-section feel). Caps keep a
            // subtle random jitter so the grass never photocopies.
            float jit = dirt ? 1f - 0.055f * variant
                             : 0.96f + 0.08f * (((variant * 37) % 7) / 6f);
            var tex = new Texture2D(T, T, TextureFormat.RGBA32, false);
            tex.SetPixels32(outc);
            var px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++) px[i] = new Color(px[i].r * jit, px[i].g * jit, px[i].b * jit, 1f);
            tex.SetPixels(px); tex.Apply();
            return tex;
        } catch (System.Exception e) {
            Debug.LogWarning("[ProcTiles] painted tile source unavailable, using Unity painter: " + e.Message);
            return null;
        }
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
            // [STYLE MATCH Sept 27 PM - BudE: "art graphics arent quality or match the art of
            // our characters"] PAINTERLY SOURCE: if the generated painterly terrain art
            // (painted to match the character art, committed at Assets/Art/paint_*.png) is
            // present, tiles are cut FROM IT instead of the flat cleanse painters. The world
            // now shares the characters' brush. Falls back to the Unity-built painters if
            // the painted files are absent (never a hard dependency).
            Texture2D tex = PaintedTileTex(dirt, VariantOf(name), lipL, lipR);
            if (tex == null) tex = dirt ? DirtTex(VariantOf(name), lipL, lipR)
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
            else if (name == "unity_print") tex = FootprintTex();
            else if (name == "unity_charm") tex = CharmTex();
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

    /// <summary>[FOOTPRINT TRAIL Sept 27 PM - BudE approved] HUD lives icon: Bigfoot's
    /// glowing gold footprint on a small dark mossy disc - the same emblem language as
    /// the Big Token coin and the bump blocks.</summary>
    public static Texture2D FootprintTex() {
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) {
            Color c = Color.clear;
            float nx = (x - S / 2f + 0.5f), ny = (y - S / 2f + 0.5f);
            float d = Mathf.Sqrt(nx * nx + ny * ny);
            // dark mossy disc
            if (d < 29f) {
                float moss = 0.5f + 0.5f * VNoise(x / 7f, y / 7f, 780 + Region);
                c = Color.Lerp(new Color(0.16f, 0.20f, 0.13f), new Color(0.24f, 0.30f, 0.19f), moss);
                // soft rim
                c = Color.Lerp(c, new Color(0.30f, 0.24f, 0.15f), Mathf.Clamp01((d - 23f) / 6f));
                // gold footprint: sole ellipse + 5 toes (Big Token emblem math)
                float ex = nx / 6.6f, ey = (y - 28f) / 8.0f;
                bool toe = false;
                for (int t = 0; t < 5; t++) {
                    float ang = (t - 2f) * 0.42f;
                    float tx = x - (S / 2f + Mathf.Sin(ang) * 8.4f), ty = y - (13f + (2f - Mathf.Abs(t - 2f)) * 2.8f);
                    toe = toe || (tx * tx + ty * ty * 1.3f < 2.4f * 2.4f);
                }
                if (ex * ex + ey * ey < 1f || toe) {
                    c = Color.Lerp(new Color(1f, 0.86f, 0.38f), c, 0.10f);
                    c = Color.Lerp(c, new Color(1f, 1f, 0.88f), 0.30f * Mathf.Clamp01(1f - ex * ex - ey * ey));
                } else {
                    float halo = Mathf.Exp(-d / 9f) * 0.22f;
                    c = Color.Lerp(c, new Color(1f, 0.90f, 0.55f), halo);
                }
            }
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    /// <summary>[SPIRIT CHARM Sept 27 PM] the lore-native extra-life pickup (replaces the
    /// classic heart): a small forest-spirit wisp - pale gold-green glow orb with a soft
    /// core, reads as a living spark of the woods.</summary>
    public static Texture2D CharmTex() {
        const int S = 48;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) {
            float nx = (x - S / 2f + 0.5f) / (S / 2f), ny = (y - S / 2f + 0.5f) / (S / 2f);
            float d = Mathf.Sqrt(nx * nx + ny * ny);
            Color c = Color.clear;
            if (d < 1f) {
                // wisp tail: slightly pear-shaped glow (wider up top, tapering below)
                float shape = d * (1f - 0.25f * Mathf.Clamp01(ny));
                float a = Mathf.Clamp01(1f - shape);
                a = a * a * 1.6f;
                Color core = new Color(1f, 0.98f, 0.85f);
                Color edge = new Color(0.62f, 0.85f, 0.52f);
                c = Color.Lerp(edge, core, Mathf.Clamp01(1f - shape * 1.15f));
                c.a = Mathf.Clamp01(a);
            }
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    // ==================== [DETAIL PASS Sept 27] CHARACTER-SELECT BACKDROP + CARD PANEL
    // (BudE Sept 27 PM: "the character select background needs to look like the map - a
    // cinematic look of what the region is per map") CINEMATIC REGION VISTA: the select
    // screen paints the SAME biome the course lives in - game sky palette + drifting
    // clouds + warm sun glow, then four atmospheric ridge layers with mist bands, an
    // organic meadow floor, and dark framing cedar silhouettes. Per-region via the same
    // LILFOOTS_REGION env the terrain uses, so every region's select screen shows ITS
    // world. All Unity-generated (cleanse law) and matching the in-game sky math.

    class VistaSpec {
        public Color skyTop, skyMid, skyMist, sunC, mistC, treeC, floorTop, floorDeep;
        public Color[] ridge;   // far -> near
        public string caption;
    }
    static VistaSpec Vista(int region) {
        switch (region) {
            case 2: return new VistaSpec {   // The High White (Himalaya)
                skyTop = new Color(0.60f,0.70f,0.82f), skyMid = new Color(0.76f,0.83f,0.90f),
                skyMist = new Color(0.90f,0.93f,0.97f), sunC = new Color(1.00f,0.97f,0.88f),
                mistC = new Color(0.92f,0.95f,0.98f), treeC = new Color(0.18f,0.28f,0.38f),
                floorTop = new Color(0.84f,0.88f,0.93f), floorDeep = new Color(0.62f,0.70f,0.80f),
                ridge = new Color[] { new Color(0.72f,0.78f,0.86f), new Color(0.58f,0.66f,0.76f),
                    new Color(0.44f,0.53f,0.65f), new Color(0.30f,0.39f,0.52f) },
                caption = "REGION 2 - THE HIGH WHITE" };
            case 3: return new VistaSpec {   // The Red Dust (Outback)
                skyTop = new Color(0.72f,0.60f,0.46f), skyMid = new Color(0.86f,0.72f,0.55f),
                skyMist = new Color(0.95f,0.86f,0.70f), sunC = new Color(1.00f,0.90f,0.70f),
                mistC = new Color(0.94f,0.86f,0.72f), treeC = new Color(0.22f,0.16f,0.10f),
                floorTop = new Color(0.76f,0.55f,0.34f), floorDeep = new Color(0.52f,0.34f,0.20f),
                ridge = new Color[] { new Color(0.82f,0.68f,0.52f), new Color(0.68f,0.50f,0.36f),
                    new Color(0.52f,0.37f,0.26f), new Color(0.36f,0.25f,0.17f) },
                caption = "REGION 3 - THE RED DUST" };
            case 4: return new VistaSpec {   // The Still Water (swamp)
                skyTop = new Color(0.48f,0.60f,0.55f), skyMid = new Color(0.66f,0.74f,0.64f),
                skyMist = new Color(0.84f,0.88f,0.78f), sunC = new Color(0.98f,0.93f,0.72f),
                mistC = new Color(0.80f,0.86f,0.74f), treeC = new Color(0.08f,0.16f,0.11f),
                floorTop = new Color(0.24f,0.34f,0.20f), floorDeep = new Color(0.10f,0.16f,0.09f),
                ridge = new Color[] { new Color(0.60f,0.68f,0.58f), new Color(0.44f,0.54f,0.42f),
                    new Color(0.30f,0.40f,0.29f), new Color(0.18f,0.27f,0.18f) },
                caption = "REGION 4 - THE STILL WATER" };
            case 5: return new VistaSpec {   // The Deep Green (jungle)
                skyTop = new Color(0.52f,0.70f,0.60f), skyMid = new Color(0.72f,0.84f,0.70f),
                skyMist = new Color(0.86f,0.92f,0.80f), sunC = new Color(1.00f,0.96f,0.78f),
                mistC = new Color(0.84f,0.90f,0.78f), treeC = new Color(0.05f,0.20f,0.10f),
                floorTop = new Color(0.22f,0.52f,0.22f), floorDeep = new Color(0.10f,0.30f,0.12f),
                ridge = new Color[] { new Color(0.62f,0.76f,0.58f), new Color(0.44f,0.60f,0.40f),
                    new Color(0.28f,0.44f,0.26f), new Color(0.14f,0.30f,0.15f) },
                caption = "REGION 5 - THE DEEP GREEN" };
            default: return new VistaSpec {   // Region 1 - Pacific Northwest (Old Growth)
                skyTop = new Color(0.55f,0.68f,0.66f), skyMid = new Color(0.74f,0.80f,0.76f),
                skyMist = new Color(0.88f,0.92f,0.89f), sunC = new Color(1.00f,0.94f,0.76f),
                mistC = new Color(0.88f,0.92f,0.89f), treeC = new Color(0.08f,0.17f,0.12f),
                floorTop = new Color(0.45f,0.58f,0.39f), floorDeep = new Color(0.25f,0.33f,0.22f),
                ridge = new Color[] { new Color(0.70f,0.77f,0.72f), new Color(0.52f,0.62f,0.54f),
                    new Color(0.34f,0.45f,0.36f), new Color(0.18f,0.29f,0.22f) },
                caption = "REGION 1 - PACIFIC NORTHWEST" };
        }
    }
    /// <summary>Region label under the select title ("a cinematic look of what the
    /// region is per map") - matches the terrain palette table names.</summary>
    public static string RegionCaption() { return Vista(Region).caption; }

    /// <summary>CINEMATIC REGION VISTA (BudE Sept 27 PM: "the background needs to look
    /// like the map should - maybe a cinematic look of what the region is per map").
    /// Composited like a movie shot of the course's biome: the game's own sky math
    /// (gradient + two-octave drifting clouds + warm sun glow and disc), then FOUR
    /// ridged-noise mountain layers fading toward the sky with mist bands at their
    /// bases, an undulating meadow floor with dappled light pools and a lit rim,
    /// three diagonal god-ray shafts, and two huge dark cedar silhouettes framing the
    /// left/right thirds. Gentle vignette for the film feel. Region palette from the
    /// same table the terrain uses - the select screen shows the world you're entering.</summary>
    public static Texture2D MenuBackdropTex(int region) {
        const int W = 1334, H = 750;
        var v = Vista(region);
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        // precompute ridge profiles (per layer, per column): ridged noise = crests
        float[][] ridgeY = new float[4][];
        float[] ridgeBase = { 0.40f, 0.48f, 0.56f, 0.63f };
        float[] ridgeAmp = { 0.10f, 0.085f, 0.065f, 0.05f };
        for (int l = 0; l < 4; l++) {
            ridgeY[l] = new float[W];
            for (int x = 0; x < W; x++) {
                float n = 0.62f * VNoise(x / 170f, l * 13.7f, 500 + region * 7 + l)
                        + 0.38f * VNoise(x / 55f, l * 31.3f, 600 + region * 7 + l);
                float crest = Mathf.Pow(1f - Mathf.Abs(2f * n - 1f), 1.25f);   // sharp peaks
                ridgeY[l][x] = (ridgeBase[l] - ridgeAmp[l] * crest) * H;
            }
        }
        // precompute floor profile
        float[] floorY = new float[W];
        for (int x = 0; x < W; x++)
            floorY[x] = (0.80f - 0.022f * VNoise(x / 140f, 901, region)) * H;
        // sun position (matches the in-game sky: warm glow high right)
        float sx = W * 0.78f, sy = H * 0.24f;
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) {
            float fy = y / (float)H;
            // 1) SKY - the game's own three-stop gradient
            Color c = fy < 0.5f ? Color.Lerp(v.skyTop, v.skyMid, fy / 0.5f)
                                : Color.Lerp(v.skyMid, v.skyMist, (fy - 0.5f) / 0.5f);
            // clouds: same two stretched octaves as the course sky
            float n1 = VNoise(x / 150f, y / 60f, 71);
            float n2 = VNoise(x / 52f, y / 34f, 72);
            float cloud = Mathf.SmoothStep(0.46f, 0.72f, n1 * 0.65f + n2 * 0.35f);
            if (cloud > 0f && fy < 0.72f) {
                float lit = Mathf.Clamp01(n2 * 1.2f);
                c = Color.Lerp(c, Color.Lerp(new Color(0.92f,0.95f,0.94f), new Color(1f,0.99f,0.97f), lit), cloud * 0.85f);
            }
            // warm sun glow + disc
            float dx = (x - sx) / (W * 0.30f), dy = (y - sy) / (H * 0.30f);
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            c = Color.Lerp(c, v.sunC, 0.55f * Mathf.Exp(-d * d * 2.0f));
            if (d < 0.055f) c = Color.Lerp(c, new Color(1f, 0.98f, 0.88f), 0.92f);
            // 2) RIDGES far -> near, each washed toward the sky by atmospheric haze
            for (int l = 0; l < 4; l++) {
                if (y >= ridgeY[l][x]) {
                    float haze = 0.55f - l * 0.15f;   // far layers melt into sky
                    Color tone = Color.Lerp(v.ridge[l], v.skyMist, haze * Mathf.Clamp01((y - ridgeY[l][x]) / (0.18f * H)));
                    // snow caps on the highest crests (regions 1-2 only feel right with them; keep for all, subtle)
                    float crestFrac = Mathf.Clamp01(1f - (ridgeY[l][x] / H - 0.30f) / 0.12f);
                    if (crestFrac > 0.5f && (y - ridgeY[l][x]) < 10f + 14f * crestFrac)
                        tone = Color.Lerp(tone, new Color(0.93f, 0.95f, 0.93f), 0.5f * crestFrac);
                    c = Color.Lerp(c, tone, 0.94f);
                    // mist band at the ridge base: long stretched noise wisps
                    float band = Mathf.Exp(-Mathf.Pow((y - (ridgeY[l][x] + 0.05f * H)) / (0.028f * H), 2f));
                    if (band > 0.08f) {
                        float wisps = VNoise(x / 90f, y / 16f, 730 + l);
                        c = Color.Lerp(c, v.mistC, band * 0.42f * (0.55f + 0.45f * wisps));
                    }
                }
            }
            // 3) MEADOW FLOOR - undulating top edge with a lit rim, dappled light pools
            if (y >= floorY[x]) {
                float t = Mathf.Clamp01((y - floorY[x]) / (H - floorY[x] + 1f));
                Color fc = Color.Lerp(v.floorTop, v.floorDeep, Mathf.Pow(t, 0.75f));
                float pool = VNoise(x / 120f, y / 70f, 810 + region);
                fc = Color.Lerp(fc, v.floorTop * 1.25f, 0.30f * Mathf.SmoothStep(0.55f, 0.8f, pool));
                float rim = Mathf.Clamp01(1f - (y - floorY[x]) / 7f);
                if (rim > 0f) fc = Color.Lerp(fc, v.floorTop * 1.35f, rim * 0.8f);
                c = fc;
            }
            // 4) GOD RAYS - three soft diagonal shafts from the sun down-left
            float px = (x - sx) / (float)W, py = (y - sy) / (float)H;
            float along = px * -0.35f + py * 1f;
            float perp = px * 1f + py * 0.35f;
            for (int k = 0; k < 3; k++) {
                float center = -0.10f - k * 0.13f;
                float wdt = 0.028f + k * 0.008f;
                float sDist = Mathf.Abs(perp - center) / wdt;
                if (sDist < 1f && along > 0.05f && along < 1.15f && y < floorY[x]) {
                    float fade = Mathf.Sin(Mathf.Clamp01(along / 1.15f) * Mathf.PI) * (1f - sDist);
                    c = Color.Lerp(c, v.sunC, 0.13f * fade);
                }
            }
            // 5) VIGNETTE - filmic corner falloff
            float vx = x / (float)W - 0.5f, vy2 = y / (float)H - 0.5f;
            float vig = Mathf.Clamp01((vx * vx * 1.35f + vy2 * vy2 * 1.1f) / 0.32f);
            c = Color.Lerp(c, c * 0.78f, vig);
            tex.SetPixel(x, y, c);
        }
        // 6) FRAMING CEDARS - two huge dark silhouettes left/right thirds (regional tone:
        // conifer shape everywhere, biome-colored; the region reads as ITS forest)
        PaintVistaTree(tex, W, H, (int)(W * 0.065f), 0.74f, 0.66f, v.treeC, 991, 0.96f);
        PaintVistaTree(tex, W, H, (int)(W * 0.94f), 0.76f, 0.56f, v.treeC, 992, 0.96f);
        PaintVistaTree(tex, W, H, (int)(W * 0.27f), 0.66f, 0.30f, Color.Lerp(v.treeC, v.ridge[3], 0.35f), 993, 0.8f);
        PaintVistaTree(tex, W, H, (int)(W * 0.71f), 0.64f, 0.26f, Color.Lerp(v.treeC, v.ridge[3], 0.35f), 994, 0.8f);
        tex.Apply();
        return tex;
    }
    /// <summary>One painterly conifer silhouette: trunk + stacked noise-jittered frond
    /// canopy, blended onto the vista. Organic (wind-shaped), never a uniform triangle.</summary>
    static void PaintVistaTree(Texture2D tex, int W, int H, int cx, float baseF, float hFrac, Color tone, int seed, float strength) {
        int baseY = (int)(baseF * H), h = (int)(hFrac * H);
        int top = baseY - h, maxW = (int)(h * 0.34f);
        for (int y = top; y < baseY && y < H; y++) {
            float tf = (y - top) / (float)h;                    // 0 at tip, 1 at base
            // trunk
            int tw = Mathf.Max(1, (int)(2f + 4f * tf));
            for (int x = cx - tw; x <= cx + tw; x++)
                SetVistaPixel(tex, x, y, tone, strength * 0.9f);
            // canopy: frond stacks, half-width grows down, edges wobble with noise
            float halfW = maxW * Mathf.Pow(tf, 0.72f);
            int x0 = cx - (int)halfW - 6, x1 = cx + (int)halfW + 6;
            for (int x = x0; x <= x1; x++) {
                float ax = Mathf.Abs(x - cx) / (halfW + 0.001f);
                if (ax > 1.15f) continue;
                // frond stacking: 5 bands, each band's edge lobe-noised
                float band = (y * 0.06f) % 1f;
                float lobe = 0.62f + 0.38f * VNoise(x / 22f, y / 26f, seed);
                if (ax < lobe * (0.85f + 0.15f * Mathf.Sin(band * Mathf.PI * 2f)))
                    SetVistaPixel(tex, x, y, Color.Lerp(tone, tone * 1.3f, 0.25f * VNoise(x / 14f, y / 18f, seed + 5)), strength);
            }
        }
    }
    static void SetVistaPixel(Texture2D tex, int x, int y, Color c, float a) {
        if (x < 0 || y < 0 || x >= tex.width || y >= tex.height) return;
        tex.SetPixel(x, y, Color.Lerp(tex.GetPixel(x, y), c, a));
    }
    public static Texture2D MenuBackdropTex() { return MenuBackdropTex(Region); }

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
        string path = "Assets/Art/Generated/unity_menu_backdrop_cinematic_r" + Region + ".png";
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

    // ---------------- DEPTH BANDS (BudE Sept 27 PM: "is it properly layering the maps
    // like background middle etc?") The tile cleanse left the course as TWO planes (sky +
    // gameplay). Depth returns as Unity-generated bands in the SAME cinematic language as
    // the select vista, region-toned via Vista(): a mist-washed FAR ridge band drifting at
    // 0.35x camera speed, a nearer MID ridge band with cedar silhouettes at 0.55x, and a
    // dark FOREST FRINGE sweeping 1.3x along the bottom. Mirror-flip tiling = seamless.
    // No painted PNGs (cleanse law) - the ridges ARE the biome, not stickers.

    /// <summary>Far ridge band: sky-washed mountain crests with tiny crest cedars.</summary>
    public static Texture2D RidgeBandTex(int layer) {   // layer 0 = far (mistiest), 1 = mid
        const int W = 1024, H = 256;
        var v = Vista(Region);
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        float haze = layer == 0 ? 0.52f : 0.28f;
        Color tone = Color.Lerp(v.ridge[layer == 0 ? 0 : 2], v.ridge[layer == 0 ? 1 : 3], 0.55f);
        for (int x = 0; x < W; x++) {
            // ridge profile: same ridged-noise crests as the select vista
            float n = 0.62f * VNoise(x / 130f, layer * 17.3f, 420 + Region * 7 + layer)
                    + 0.38f * VNoise(x / 46f, layer * 41.1f, 430 + Region * 7 + layer);
            float crest = Mathf.Pow(1f - Mathf.Abs(2f * n - 1f), 1.2f);
            float ry = H * (0.72f - 0.40f * crest * (layer == 0 ? 1f : 0.8f));
            for (int y = 0; y < H; y++) {
                Color c;
                if (y >= ry) {
                    c = Color.Lerp(tone, v.skyMist, haze * Mathf.Clamp01((y - ry) / (0.35f * H)));
                    // snow caps on the highest crests
                    float crestFrac = Mathf.Clamp01(1f - (ry / H - 0.30f) / 0.14f);
                    if (crestFrac > 0.45f && (y - ry) < 4f + 7f * crestFrac)
                        c = Color.Lerp(c, new Color(0.93f, 0.95f, 0.93f), 0.45f * crestFrac);
                    // tiny cedars riding the mid band crest line
                    if (layer == 1) {
                        float cn = VNoise(x / 9f, 77f, 480 + Region);
                        if (cn > 0.86f && (y - ry) > 4f && (y - ry) < 16f + 14f * (cn - 0.86f))
                            c = Color.Lerp(c, Color.Lerp(v.treeC, tone, 0.3f), 0.85f);
                    }
                } else {
                    c = Color.clear;   // transparent sky above the ridge
                }
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return tex;
    }

    /// <summary>Foreground fringe: dark scalloped forest-floor foliage along the bottom
    /// edge, transparent above - frames the near-field without covering standing gameplay.</summary>
    public static Texture2D FringeTex() {
        const int W = 1024, H = 192;
        var v = Vista(Region);
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        for (int x = 0; x < W; x++) {
            // scalloped silhouette: two noise octaves, taller clumps + dips
            float n1 = VNoise(x / 120f, 5f, 910 + Region);
            float n2 = VNoise(x / 34f, 9f, 911 + Region);
            float edge = H * (0.62f - 0.38f * n1 - 0.14f * n2);
            for (int y = 0; y < H; y++) {
                if (y >= edge) {
                    Color c = Color.Lerp(v.treeC, v.treeC * 1.35f, 0.22f * VNoise(x / 26f, y / 22f, 912));
                    c.a = 0.94f;
                    tex.SetPixel(x, y, c);
                } else tex.SetPixel(x, y, Color.clear);
            }
        }
        tex.Apply();
        return tex;
    }

    /// <summary>Mario-style bump block (BudE Sept 27 PM): carved cedar block, moss cap
    /// (region palette), soft inner rim, and the glowing gold FOOTPRINT emblem - the
    /// Big Token language on a hittable tile. After a bump the runtime dims it to "used".</summary>
    public static Texture2D BumpBlockTex() {
        const int S = 128;
        var pal = Palette(Region);
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) {
            Color c = Color.clear;
            // rounded-square body
            float dx = Mathf.Max(0f, Mathf.Abs(x - S / 2f + 0.5f) - (S / 2f - 12f));
            float dy = Mathf.Max(0f, Mathf.Abs(y - S / 2f + 0.5f) - (S / 2f - 12f));
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d <= 12f) {
                float fy = y / (float)S;
                // cedar body, darker toward the base, faint grain
                c = Color.Lerp(new Color(0.44f, 0.34f, 0.21f), new Color(0.30f, 0.23f, 0.14f), fy);
                float grain = 0.5f + 0.5f * VNoise(x / 9f, y / 5f, 770 + Region);
                c *= 0.92f + 0.10f * grain;
                // moss cap: top 18% wears the region's cap green w/ a crisp edge
                float mossEdge = 0.82f + 0.05f * VNoise(x / 16f, 0f, 771 + Region);
                if (fy > mossEdge) c = Color.Lerp(pal.capEdge, pal.capBody, (fy - mossEdge) / 0.18f);
                // dark outline
                float edge = Mathf.Clamp01((12f - d) / 5f);
                if (edge < 1f) c = Color.Lerp(new Color(0.16f, 0.12f, 0.08f), c, edge);
                // GLOWING GOLD FOOTPRINT EMBLEM: sole ellipse + 5 toes, gold with halo
                float ex = (x - S / 2f) / 14f, ey = (y - 62f) / 17f;
                float sole = ex * ex + ey * ey;
                bool toe = false;
                for (int t = 0; t < 5; t++) {
                    float ang = (t - 2f) * 0.42f;
                    float tx = x - (S / 2f + Mathf.Sin(ang) * 17f), ty = y - (30f + (2f - Mathf.Abs(t - 2f)) * 5.5f);
                    toe = toe || (tx * tx + ty * ty * 1.3f < 4.5f * 4.5f);
                }
                float halo = Mathf.Exp(-Mathf.Min(Mathf.Abs(x - S / 2f) / 26f, 1f) * 1.4f)
                           * Mathf.Exp(-Mathf.Abs(y - 48f) / 24f);
                if (sole < 1f || toe) {
                    c = Color.Lerp(new Color(0.99f, 0.82f, 0.34f), c, 0.10f);           // solid gold fill
                    c = Color.Lerp(c, new Color(1f, 1f, 0.85f), 0.35f * Mathf.Clamp01(1f - sole / 1f)); // lit core
                } else if (halo > 0.10f) {
                    c = Color.Lerp(c, new Color(1f, 0.88f, 0.50f), 0.30f * halo);        // soft glow ring
                }
            }
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }
    public static Sprite EnsureBumpBlock() {
        return EnsureGeneratedTex("unity_bump_r" + Region, BumpBlockTex);
    }

    static Sprite EnsureGeneratedTex(string name, System.Func<Texture2D> paint) {
        string path = "Assets/Art/Generated/" + name + ".png";
        Directory.CreateDirectory("Assets/Art/Generated");
        if (!File.Exists(path)) File.WriteAllBytes(path, paint().EncodeToPNG());
        AssetDatabase.Refresh();
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null) { Debug.LogError("[ProcTiles] " + name + " importer missing"); return null; }
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100f;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    public static Sprite EnsureRidgeBand(int layer) {
        return EnsureGeneratedTex("unity_ridge_" + (layer == 0 ? "far" : "mid") + "_r" + Region,
                                  () => RidgeBandTex(layer));
    }
    public static Sprite EnsureFringe() { return EnsureGeneratedTex("unity_fringe_r" + Region, FringeTex); }

    /// <summary>The Unity-generated sky as a loadable Sprite (camera-pinned backdrop).</summary>
    public static Sprite EnsureSky() {
        // [STYLE MATCH Sept 27 PM] the painterly sky (painted to match the character art)
        // takes precedence; the flat cleanse sky is only the fallback.
        const string painted = "Assets/Art/paint_sky.png";
        const string path = "Assets/Art/Generated/unity_sky_painted.png";
        Directory.CreateDirectory("Assets/Art/Generated");
        if (System.IO.File.Exists(painted)) System.IO.File.Copy(painted, path, true);
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
