#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.IO;

namespace LilFoots {
/// <summary>
/// UNITY TILEMAP SKIN (BudE, Sept 25 2026: "build the geometry side in Unity and skin it in
/// Unity"). The terrain where the player runs is a REAL Unity Tilemap, not a stretched
/// painting: each audited ground collider gets its footprint painted as 1u tiles - a
/// grass-cap row (green canon: BudE world reference - sage moss caps, deep forest darks)
/// plus dirt-fill rows down to bedrock, with lip tiles on exposed side columns.
///
/// LAW: the tilemap is the SKIN ONLY. Physics stays on the audited BoxCollider2D Plat
/// objects - zero gameplay drift, the geometry he judged GREEN is exactly what ships.
/// Tile cells sit at the 1u Unity grid, tilemap offset y=+0.2 so the tile top edge lands
/// exactly on the 6.2u ground surface; the 0.2u plateau difference is absorbed by the
/// grass-edge fringe band (ArtPass binds it above every walking line).
/// 1 tile = 1 Unity unit: the layout stays editable/reskinnable with the Unity Tile
/// Palette - drop hand-painted art over the same lf_*.png files and the world reskins.
/// </summary>
public static class LilFootsTilemapSkin {
    static Tilemap _tm;
    static TileBase[] _caps, _capsL, _capsR, _dirts, _dirtsL, _dirtsR;
    static bool _ready;

    public static bool Ready() { return _ready || LoadTiles(); }

    static Tilemap EnsureTilemap(Transform root) {
        if (_tm != null) return _tm;
        var gridGo = new GameObject("TerrainGrid");
        gridGo.transform.SetParent(root, false);
        gridGo.AddComponent<Grid>();
        var tmGo = new GameObject("TerrainTiles");
        tmGo.transform.SetParent(gridGo.transform, false);
        var tm = tmGo.AddComponent<Tilemap>();
        var tr = tmGo.AddComponent<TilemapRenderer>();
        tr.sortingOrder = -2;                          // same plane the old earth bands drew on
        tr.mode = TilemapRenderer.Mode.Chunk;
        tmGo.transform.localPosition = new Vector3(0f, 0.2f, 0f);  // tile top edge = 6.2 surface
        _tm = tm;
        return tm;
    }

    /// <summary>Persisted Tile asset (sprite sub-asset) so scenes reload clean across
    /// sessions and shipped builds. PPU = texture width: a square lf_*.png = one 1u tile.</summary>
    static TileBase MakeTile(string name, string texPath) {
        var assetPath = "Assets/Art/Tiles/Generated/tile_" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(assetPath);
        if (existing != null) return existing;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null) return null;
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                   new Vector2(0.5f, 0.5f), tex.width, 0, SpriteMeshType.FullRect);
        sprite.name = name;
        var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        tile.sprite = sprite;
        tile.name = name;
        Directory.CreateDirectory("Assets/Art/Tiles/Generated");
        AssetDatabase.CreateAsset(tile, assetPath);
        AssetDatabase.AddObjectToAsset(sprite, assetPath);
        AssetDatabase.SaveAssets();
        return tile;
    }

    static bool LoadTiles() {
        string d = "Assets/Art/Tiles/";
        _caps   = LoadSet(d, "lf_grass_a", "lf_grass_b", "lf_grass_c", "lf_grass_d");
        _capsL  = LoadSet(d, "lf_grass_l");
        _capsR  = LoadSet(d, "lf_grass_r");
        _dirts  = LoadSet(d, "lf_dirt_a", "lf_dirt_b", "lf_dirt_c", "lf_dirt_d");
        _dirtsL = LoadSet(d, "lf_dirt_l");
        _dirtsR = LoadSet(d, "lf_dirt_r");
        _ready = _caps.Length > 0 && _dirts.Length > 0;
        if (_ready) Debug.Log("[TilemapSkin] green canon tiles ready: " + _caps.Length + " caps + "
                              + _dirts.Length + " dirt (+lip variants: " + (_capsL.Length + _capsR.Length + _dirtsL.Length + _dirtsR.Length) + ")");
        else Debug.LogWarning("[TilemapSkin] lf_* tile art missing - falling back to band skins");
        return _ready;
    }

    static TileBase[] LoadSet(string d, params string[] names) {
        var list = new List<TileBase>();
        foreach (var n in names) { var t = MakeTile(n, d + n + ".png"); if (t != null) list.Add(t); }
        return list.ToArray();
    }

    static int Hash(int c, int r) { return ((c * 73856093) ^ (r * 19349663)) & 0x7fffffff; }

    /// <summary>Pre-pass: the union of every ground's tile cells, so lips only paint on
    /// TRULY exposed sides (adjacent grounds don't get seam lips where they meet).</summary>
    static HashSet<long> CollectCells(System.Collections.Generic.IEnumerable<GameObject> grounds) {
        var cells = new HashSet<long>();
        foreach (var plat in grounds) {
            var bc = plat.GetComponent<BoxCollider2D>();
            if (bc == null || bc.size.y < 2f) continue;             // hops wear slab art, not tiles
            float top = plat.transform.position.y + bc.size.y / 2f;
            float x0 = plat.transform.position.x - bc.size.x / 2f;
            float x1 = plat.transform.position.x + bc.size.x / 2f;
            int c0 = Mathf.FloorToInt(x0 + 0.001f);
            int c1 = Mathf.CeilToInt(x1 - 0.001f) - 1;
            int rTop = Mathf.RoundToInt(top - 1.2f);                 // tile top edge at the surface
            for (int c = c0; c <= c1; c++)
                for (int r = rTop; r >= -6; r--)                     // bedrock: solid to below frame
                    cells.Add(((long)c << 32) ^ (uint)r);
        }
        return cells;
    }

    /// <summary>Paint every audited ground collider's footprint as tiles (skin only).</summary>
    public static void PaintGrounds(GameObject mapRoot) {
        if (!Ready()) return;
        var grounds = new List<GameObject>();
        foreach (Transform child in mapRoot.transform) {
            var bc = child.GetComponent<BoxCollider2D>();
            if (bc != null && bc.size.y >= 2f) grounds.Add(child.gameObject);
        }
        var cells = CollectCells(grounds);
        bool Has(int c, int r) => cells.Contains(((long)c << 32) ^ (uint)r);

        var tm = EnsureTilemap(mapRoot.transform);
        int painted = 0, caps = 0, lips = 0;
        foreach (var key in cells) {
            int c = (int)(key >> 32); int r = (int)(key & 0xffffffffL);
            bool top = !Has(c, r + 1);
            bool openL = !Has(c - 1, r);
            bool openR = !Has(c + 1, r);
            TileBase t;
            if (top) {
                if (openL && _capsL.Length > 0) { t = _capsL[0]; lips++; }
                else if (openR && _capsR.Length > 0) { t = _capsR[0]; lips++; }
                else { t = _caps[Hash(c, r) % _caps.Length]; caps++; }
            } else {
                if (openL && _dirtsL.Length > 0) t = _dirtsL[0];
                else if (openR && _dirtsR.Length > 0) t = _dirtsR[0];
                else t = _dirts[Hash(c, r) % _dirts.Length];
            }
            tm.SetTile(new Vector3Int(c, r, 0), t);
            painted++;
        }
        tm.CompressBounds();
        Debug.Log("[TilemapSkin] painted " + painted + " tiles (" + caps + " grass caps, " + lips + " exposed lips) across " + grounds.Count + " grounds");
    }
}
}
#endif
