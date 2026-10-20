using UnityEngine;

namespace LilFoots {
/// <summary>Marker on props placed with the LilFootsPropPlacer (editor tool). Carries the info
/// the builder needs to re-place them on a course rebuild, so hand-decorating survives rebuilds.</summary>
public class LilFootsProp : MonoBehaviour {
    public string file;   // art file relative to Assets/Art (e.g. "Props/fern_giant.png")
    public int order;     // sorting order at placement
    public float rot;     // z rotation in degrees
    public float dim;     // 1 = dimmed background tint, 0 = full color
}
}
