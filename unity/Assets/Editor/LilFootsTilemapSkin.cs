#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LilFoots {
/// <summary>
/// UNITY TILEMAP SKIN (Bude, Sept 25 2026: "green light - build the geometry in Unity and skin
/// it with Unity"): the terrain where the player runs is a REAL Unity Tilemap, not a stretched
/// painting. Each audited ground collider gets its footprint painted as 1u tiles - a grass-cap
/// row (green canon: bude world reference - sage moss, deep forest darks) plus dirt-fill rows
/// down to bedrock, with collar tiles on exposed side columns.
///
/// LAW: the tilemap is the SKIN ONLY. Physics stays on the audited BoxCollider2D Plat objects
/// (zero gameplay drift - the geometry he judged GREEN is exactly what ships). Tile cells sit at
/// the 1u Unity grid, offset y=+0.2 so the tile top edge lands exactly on the 6.2u ground
/// surface; the 0.2u plateau difference is absorbed by the grass-edge fringe band at the
/// walking line (ArtPass keeps binding it above every surface).
/// 1 tile = 1 Unity unit, so the same layout can be re-skinned or hand-edited with the Unity
/// Tile Palette - everything stays native, editable Unity geometry.
/// </summary>
public static class LilFootsTilemapSkin {
    static Tilemap _tm;
    static TileBase[] _caps, _dirts, _collars;
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
        tr.sortingOrder = -2;                        // same plane the old earth bands drew on
        tmGo.transform.localPosition = new Vector3(0f, 0.2f, 0f);   // tile top edge = 6.2 surface
        _tm = tm;
        return tm;
    }

    static TileBase MakeTile(string name, string texPath, int px, int py) {
        // persisted Tile asset (sprite sub-asset) so scenes reload clean across sessions/builds
        var assetPath = "Assets/Art/Tiles/Generated/tile_" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.Tile>(assetPath);
        if (existing != null) return existing;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null) return null;
        var rect = new Rect(px, py, 100, 100);
        var pivot = new Vector2(0.5f, 0.5f);
        var sprite = Sprite.Create(tex, rect, pivot, 100f, 0, SpriteMeshType.FullRect);
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
        var capList = new List<TileBase>();
        var dirtList = new List<TileBase>();
        var collarList = new List<TileBase>();
        for (int i = 1; i <= 6; i++) {
            var c = MakeTile("grass_top_" + i, "Assets/Art/Tiles/grass_top_" + i + ".png", 0, 0);
            if (c != null) capList.Add(c);
            var dd = MakeTile("dirt_" + i, "Assets/Art/Tiles/dirt_" + i + ".png", 0, 0);
            if (dd != null) dirtList.Add(dd);
        }
        for (int i = 1; i <= 2; i++) {
            var cc = MakeTile("collar_" + i, "Assets/Art/Tiles/collar_" + i + ".png", 0, 0);
            if (cc != null) collarList.Add(cc);
        }
        _caps = capList.ToArray(); _dirts = dirtList.ToArray(); _collars = collarList.ToArray();
        _ready = _caps.Length > 0 && _dirts.Length > 0;
        if (_ready) Debug.Log("[TilemapSkin] tiles ready: " + _caps.Length + " caps, " + _dirts.Length + " dirt, " + _collars.Length + " collar");
        else Debug.LogWarning("[TilemapSkin] tile art missing - falling back to band skins");
        return _ready;
    }

    static int Hash(int c, int r) { return ((c * 73856093) ^ (r * 19349663)) & 0x7fffffff; }

    /// <summary>Paint one audited ground collider's footprint as tiles (skin only).</summary>
    public static void PaintGround(GameObject plat) {
        if (!Ready()) return;
        var bc = plat.GetComponent<BoxCollider2D>();
        if (bc == null) return;
        var tm = EnsureTilemap(plat.transform.root == null ? plat.transform : plat.transform.root);

        float top = plat.transform.position.y + bc.size.y / 2f;   // walking surface
        float x0 = plat.transform.position.x - bc.size.x / 2f;
        float x1 = plat.transform.position.x + bc.size.x / 2f;

        // cells: col covers world x [c, c+1]; row r covers world y [r+0.2, r+1.2] (tm offset)
        int c0 = Mathf.FloorToInt(x0 + 0.001f);
        int c1 = Mathf.CeilToInt(x1 - 0.001f) - 1;
        int rTop = Mathf.RoundToInt(top - 1.2f);                  // tile top edge at the surface
        int rBottom = -6;                                         // bedrock: solid earth to below the frame

        // collect the union of cells this pass paints (for cap/side decisions)
        var cells = new HashSet<int>();
        for (int c = c0; c <= c1; c++) for (int r = rTop; r >= rBottom; r--) cells.Add(c * 100000 + r);

        for (int c = c0; c <= c1; c++) {
            for (int r = rTop; r >= rBottom; r--) {
                TileBase t;
                bool solidAbove = cells.Contains(c * 100000 + (r + 1));
                bool solidLeft = cells.Contains((c - 1) * 100000 + r);
                bool solidRight = cells.Contains((c + 1) * 100000 + r);
                if (!solidAbove) {
                    // exposed surface row: cap tile, collar at exposed sides (organic lip)
                    if (!solidLeft || !solidRight) {
                        var col = _collars[Hash(c, r) % _collars.Length];
                        t = col != null ? col : _caps[Hash(c, r) % _caps.Length];
                    } else {
                        t = _caps[Hash(c, r) % _caps.Length];
                    }
                } else {
                    t = _dirts[Hash(c, r) % _dirts.Length];
                }
                tm.SetTile(new Vector3Int(c, r, 0), t);
            }
        }
    }
}
}
#endif
