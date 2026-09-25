using UnityEngine;
using UnityEngine.Tilemaps;

namespace LilFoots {
/// <summary>
/// BUILD MODULE KEEPER (BudE, Sept 25: "the map still doesn't look right with the lines and
/// ground"): the shipped builds logged "'Tilemap' is not supported because the module Tilemap
/// is disabled in the build" — the entire tile terrain was STRIPPED from the player, so the
/// course rendered as bare fringe lines over sky. The module is now in the manifest; this
/// runtime reference plus Assets/link.xml guarantees the Tilemap classes survive engine-code
/// stripping in every future build. Never delete this class.
/// </summary>
public static class TilemapModuleKeeper {
    static readonly Tilemap[] _keep;
    static TilemapModuleKeeper() { _keep = new Tilemap[0]; }
}
}
