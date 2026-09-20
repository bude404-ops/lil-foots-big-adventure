using UnityEngine;
using System.Collections.Generic;

namespace LilFoots {
/// <summary>Static registry so cams/drones can alert hounds without scene coupling.</summary>
public static class HoundManager {
    static readonly List<HoundController> hounds = new List<HoundController>();
    public static void Register(HoundController h) { if (!hounds.Contains(h)) hounds.Add(h); }
    public static void AlertAllInRange(float x, float range, float alertT = 5f) {
        foreach (var h in hounds)
            if (!h.dead && Mathf.Abs(h.transform.position.x - x) < range) h.Alert(alertT);
    }
    public static void AlertAll(float alertT = 4f) { foreach (var h in hounds) if (!h.dead) h.Alert(alertT); }
}
}
