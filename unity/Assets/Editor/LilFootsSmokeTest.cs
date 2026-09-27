#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace LilFoots.EditorTools {
/// <summary>
/// GATE 2 — MACHINE PLAY-TEST (LIL-FOOTS-2.0-PLAN.md, Bude Sept 19: "so we wont have a bunch of fixes to go over").
/// Every shipped build passes this before Bude ever sees it. Red test = run fails = nothing is relayed.
/// v1 asserts the runtime wiring that actually broke this week: duplicate EventSystems (dead jump), feet
/// anchoring (character sinking behind terrain), sorting at contact points, the touch deck, the character
/// menu cards, and the course content (tokens / checkpoints / gate / flag / portal / hounds).
/// v1.5 (planned) upgrades to UTF play-mode with simulated input via -runTests.
/// </summary>
public static class LilFootsSmokeTest {

    public static bool Run(string reportPath) {
        if (string.IsNullOrEmpty(reportPath)) reportPath = "smoke-report.json";
        var fail = new List<string>();
        int pass = 0;
        Action<bool, string> C = (ok, name) => {
            if (ok) { pass++; Debug.Log("[SMOKE] PASS " + name); }
            else    { fail.Add(name); Debug.Log("[SMOKE] **FAIL** " + name); }
        };

        // 0) test the EXACT scene that ships — reload from disk (the QC pass mutates the session scene)
        // MULTI-MAP LAW: gate the scene this run is actually shipping (MAP_SCENE, default Map001).
        var sceneName = System.Environment.GetEnvironmentVariable("MAP_SCENE") ?? "Map001";
        EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
        C(SceneManager.GetActiveScene().IsValid() && SceneManager.GetActiveScene().name == sceneName,
          "scene: " + sceneName + ".unity loads");

        // 1) INPUT SPINE — the Sept 19 dead-jump root cause was a DUPLICATE EventSystem
        var es = UnityEngine.Object.FindObjectsOfType<EventSystem>();
        C(es.Length == 1, "input: exactly ONE EventSystem (found " + es.Length + ")");

        bool m1 = (System.Environment.GetEnvironmentVariable("MAP_DATA") == "map_m1.json") || sceneName.StartsWith("MapM1");
        var cams = UnityEngine.Object.FindObjectsOfType<Camera>();
        var mains = cams.Where(c => c.CompareTag("MainCamera")).ToList();
        C(mains.Count == 1, "camera: exactly one MainCamera (found " + mains.Count + ")");
        C(mains.Count == 1 && mains[0].GetComponent<LilFoots.CameraFollow>() != null, "camera: CameraFollow attached");
        // CAMERA BOUNDS vs COURSE (BudE, Sept 20: the old M1 maxX=93 clamped the 225u course
        // and the player walked off screen at 40% of the level. The clamp must reach the gate.)
        if (!m1) {
            var cf = mains.Count == 1 ? mains[0].GetComponent<LilFoots.CameraFollow>() : null;
            var gateGo = GameObject.Find("Gate");
            C(cf != null && gateGo != null && cf.maxX >= gateGo.transform.position.x - 7.5f,
              "camera: bounds reach the gate (maxX=" + (cf != null ? cf.maxX : 0).ToString("0") +
              ", gate=" + (gateGo != null ? gateGo.transform.position.x : 0).ToString("0") + ")");
        }

        // 2) PLAYER + FEET (Sept 19: "the terrain hides the character")
        // PLAYER RESOLUTION LAW (run 35469967904 red): the idle-rig stage builds clones that
        // can share the name "Lily" - Find is ambiguous. The PLAYER is the "Lily"-named object
        // carrying a PlayerController; every other Lily is a select-stage copy and must sit
        // in the off-map band (y < -50) so it can never touch gameplay.
        var lily = UnityEngine.Object.FindObjectsOfType<LilFoots.PlayerController>()
            .Select(x => x.gameObject)
            .FirstOrDefault(g => g.name == "Lily");
        C(lily != null, "player: Lily exists (with PlayerController)");
        var stageCopies = UnityEngine.Object.FindObjectsOfType<Transform>()
            .Where(t => t.name == "Lily" && t != (lily != null ? lily.transform : null))
            .ToList();
        C(stageCopies.All(t => t.position.y < -50f),
          "select: stage copies off-map (" + stageCopies.Count + " found" +
          (stageCopies.Count > 0 ? ", worst y=" + stageCopies.Min(t => t.position.y).ToString("F1") : "") + ")");
        var pc = lily != null ? lily.GetComponent<LilFoots.PlayerController>() : null;
        C(pc != null && pc.rb != null, "player: Rigidbody2D wired");
        // NOTE: pc.feet is a vestigial field (ground detection is OnCollisionStay2D contact-normal based) —
        // the real check is: a capsule collider exists and the Ground layer is under the player.
        C(lily != null && lily.GetComponent<CapsuleCollider2D>() != null, "player: capsule collider present");
        var art = lily != null ? lily.GetComponentInChildren<SpriteRenderer>() : null;
        C(art != null, "player: sprite art present");
        // [DEPTH BANDS Sept 27 PM - BudE "is it properly layering the maps like background
        // middle etc?"] the course must own its depth stack: far/mid ridges + bottom fringe.
        C(UnityEngine.GameObject.Find("DepthRidgeFar_0") != null, "depth: far ridge band present");
        C(UnityEngine.GameObject.Find("DepthRidgeMid_0") != null, "depth: mid ridge band present");
        C(UnityEngine.GameObject.Find("DepthFringe_0") != null, "depth: foreground fringe present");
        // [BUMP TILES Sept 27 PM - BudE: "hittable if you [hit] below them like in mario"]
        C(UnityEngine.GameObject.Find("BumpBlock_0") != null, "bump: hittable bump block present (mario-style)");
        // [IDENTITY GATE Sept 27 PM - BudE: 'only sends the fixed updated versions'] the
        // shipping scene must PROVE it is the requested map from the requested commit.
        {
            var id = UnityEngine.Object.FindObjectOfType<LilFoots.MapIdentity>();
            string want = System.Environment.GetEnvironmentVariable("MAP_DATA");
            if (string.IsNullOrEmpty(want)) want = "map001.json";
            string wantFile = want.Contains("/") ? want.Substring(want.LastIndexOf('/') + 1) : want;
            string sha = System.Environment.GetEnvironmentVariable("BUILD_SHA");
            bool ok = id != null && id.dataFile == wantFile && (string.IsNullOrEmpty(sha) || id.buildSha == sha);
            C(ok, "identity: shipping the REQUESTED map + commit (" + (id != null ? id.Describe() : "NO MapIdentity - STALE SCENE") + ", asked for " + wantFile + ")");
        }
        // [FOOTPRINT TRAIL Sept 27 PM - BudE approved: lives = glowing footprint trail, not hearts]
        C(UnityEngine.GameObject.Find("HUDPrint0") != null, "hud: footprint trail present (lore-native lives)");
        bool anyBark = false;
        foreach (var bt in UnityEngine.Object.FindObjectsOfType<LilFoots.BumpTile>()) if (bt.content == "bark") { anyBark = true; break; }
        C(anyBark, "power: Cedar Bark Hide block placed (one free hit)");

        // [JUMP FACING Sept 26 PM: BudE "the jump is still one directional"] the flip law is
        // now GATED IN CI: the bridge must mirror the rig for right (scale.x < 0) and show the
        // left-native art for left (scale.x > 0). The frames are canonical left-native
        // (tools/normalize_facing.py), the bridge is the single flip authority.
        if (lily != null) {
            // [FIGURE ANIMATION Sept 27 PM - BudE: 'more figures to make it look like they
            // are animated'] multi-figure frame sets are REQUIRED on the player rig again.
            // FrameAnimator absent = regression (motion fell back to single-sprite bob).
            var fa = lily.GetComponentInChildren<LilFoots.FrameAnimator>();
            C(fa != null, "anim: figure frame set wired on the player rig");
            if (fa != null)
                C(fa.walk != null && fa.walk.Length >= 6 && fa.jump != null && fa.jump.Length >= 4
                  && fa.idle != null && fa.idle.Length >= 2,
                  "anim: figure set complete (walk>=6, jump>=4, idle>=2)");
            // edit-time sprite state is NOT a valid witness (the skinning bridge assigns the
            // deformed copy at runtime; QC shots prove the character renders) - the structural
            // witness is the rig itself: BuildPlayerRigs builds LilyRig from whole_lily.png.
            C(lily.transform.Find("LilyRig") != null,
              "anim: player rig built from the actual upright art (LilyRig present)");
            var bridge = lily.GetComponentInChildren<LilFoots.PlayerAnimBridge>();
            if (bridge != null && pc != null) {
                var rigRoot = bridge.transform;
                float baseX = System.Math.Abs(rigRoot.localScale.x) > 0.001f
                              ? System.Math.Abs(rigRoot.localScale.x) : 0.82f;
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                try {
                    bridge.GetType().GetMethod("Awake", flags)?.Invoke(bridge, null);
                    int savedFacing = pc.facing;
                    pc.facing = 1; bridge.GetType().GetMethod("Update", flags)?.Invoke(bridge, null);
                    bool rightMirrors = rigRoot.localScale.x < 0f;
                    pc.facing = -1; bridge.GetType().GetMethod("Update", flags)?.Invoke(bridge, null);
                    bool leftNative = rigRoot.localScale.x > 0f;
                    pc.facing = savedFacing;
                    bridge.GetType().GetMethod("Update", flags)?.Invoke(bridge, null);   // restore pose
                    C(rightMirrors && leftNative,
                      "facing: rig mirrors BOTH directions (right " + (rightMirrors ? "mirrored" : "BROKEN") +
                      ", left " + (leftNative ? "left-native" : "BROKEN") + ")");
                } catch (System.Exception e) {
                    C(false, "facing: flip check failed to run [" + e.Message + "]");
                }
            } else {
                C(bridge != null, "facing: PlayerAnimBridge present on the player rig");
            }
        }

        // FROZEN CORE GATE — the feel constants Bude play-tested. Drift here = the core was touched.
        C(pc != null
          && Math.Abs(pc.runSpeed - 4.6f) < 0.01f
          && Math.Abs(pc.jumpVelocity - 9.0f) < 0.01f
          && Math.Abs(pc.gravityScale - 2.446f) < 0.01f
          && Math.Abs(pc.coyoteTime - 0.12f) < 0.01f
          && Math.Abs(pc.jumpBuffer - 0.14f) < 0.01f,
          "core: feel constants frozen (run 4.6 / jump 9.0 / grav 2.446 / coyote .12 / buffer .14)");

        if (lily != null) {
            // raycast on the GROUND LAYER ONLY (a bare ray hits the player's own capsule first)
            var gmask = LayerMask.GetMask("Ground");
            var hit = Physics2D.Raycast(lily.transform.position + Vector3.up * 0.5f, Vector2.down, 6f, gmask);
            float gap = hit.collider != null ? lily.transform.position.y - hit.point.y : -99f;
            C(hit.collider != null && hit.point.y > 5.5f && hit.point.y < 6.9f && gap >= -0.1f && gap <= 0.9f,
              "feet: player stands ON the ground (ground y=" + (hit.collider != null ? hit.point.y.ToString("F2") : "none") +
              ", gap " + gap.ToString("F2") + ")");
        }
        // terrain-hide regression: every collider sharing the player's feet point must sort BELOW the player art
        if (lily != null && art != null) {
            var contact = Physics2D.OverlapPointAll((Vector2)lily.transform.position + Vector2.down * 0.05f);
            bool ok = true; string worst = "";
            foreach (var col in contact) {
                var r = col.GetComponent<SpriteRenderer>();
                if (r == null) r = col.GetComponentInParent<SpriteRenderer>();
                if (r != null && r != art && r.sortingOrder >= art.sortingOrder) { ok = false; worst = col.name + " order " + r.sortingOrder + " >= player " + art.sortingOrder; }
            }
            C(ok, "sorting: player renders ABOVE terrain at contact" + (ok ? "" : " [" + worst + "]"));
        }

        // 3) TOUCH DECK (Sept 19: "jumping doesn't work" on the phone)
        foreach (var n in new[] { "BtnLeft", "BtnRight", "BtnJump" }) {
            var b = GameObject.Find(n);
            C(b != null && b.GetComponent<LilFoots.TouchDeckButton>() != null, "deck: " + n + " wired");
        }
        C(Physics2D.gravity.x == 0f && Math.Abs(Physics2D.gravity.y - -9.81f) < 0.01f, "physics: native gravity default (0,-9.81)");

        // 4) CHARACTER SELECT (Sept 19: "characters are still the t pose for select")
        var cards = UnityEngine.Object.FindObjectsOfType<LilFoots.CharacterCard>();
        C(cards.Length == 3, "select: 3 character cards (found " + cards.Length + ")");
        var menu = GameObject.Find("CharMenuCanvas");
        var mc = menu != null ? menu.GetComponent<Canvas>() : null;
        C(mc != null && mc.renderMode == RenderMode.ScreenSpaceOverlay && mc.sortingOrder >= 100,
          "select: menu is a ScreenSpaceOverlay modal >= 100");
        C(menu != null && menu.GetComponent<GraphicRaycaster>() != null, "select: menu has a raycaster");
        bool cardsHaveArt = cards.Length == 3;
        foreach (var card in cards) {
            if (card.GetComponentInChildren<Graphic>(true) == null) cardsHaveArt = false;
        }
        C(cardsHaveArt, "select: every card has a visible graphic");

        // 5) COURSE CONTENT — the shipped loop must be present.
        // v2 M1 (RESTART-V2.md): the clean-floor milestone ships character + run/jump/touch +
        // camera + meadow + flag ONLY. Course content checks swap to scope-purity checks.
        var tokens = UnityEngine.Object.FindObjectsOfType<LilFoots.TokenCollectible>();
        // Sept 20: 3-tier doctrine (gate needs 15) - 30+ tokens means the course carries
            // double the gate cost across easy/exploration/difficult tiers. The old >=60 was
            // calibrated to the retired 73-token map, not the redesigned courses.
        if (m1) {
            // M1 SCOPE PURITY: the floor must be clean - no course content rides along early.
            C(tokens.Length == 0, "M1: no tokens on the floor (found " + tokens.Length + ")");
            var cps0 = UnityEngine.Object.FindObjectsOfType<LilFoots.CheckpointController>();
            C(cps0.Length == 0, "M1: no checkpoints (found " + cps0.Length + ")");
            var h0 = UnityEngine.Object.FindObjectsOfType<LilFoots.HoundController>();
            C(h0.Length == 0, "M1: no hounds (found " + h0.Length + ")");
            C(UnityEngine.Object.FindObjectsOfType<LilFoots.DroneController>().Length == 0, "M1: no drone");
        } else {
            C(tokens.Length >= 30, "course: >=30 Big Tokens across tiers (found " + tokens.Length + ")");
            var cps = UnityEngine.Object.FindObjectsOfType<LilFoots.CheckpointController>();
            C(cps.Length >= 4, "course: >=4 checkpoints (found " + cps.Length + ")");
        }
        var gate = GameObject.Find("Gate");
        C(gate != null && gate.GetComponent<LilFoots.GateController>() != null, "course: gate + GateController wired");
        // TERMINUS CHECK (width-relative, run 36162649214 red): the terminus law places the gate
        // at width-4, so the floor must scale with THIS course - the 80u depth-test course
        // gates at x=76 (=80-4, correct) and failed the old hardcoded >80 gate.
        float termMin = m1 ? 30f : 80f;
        if (!m1) {
            try {
                var md = System.Environment.GetEnvironmentVariable("MAP_DATA");
                if (!string.IsNullOrEmpty(md)) {
                    var path = md.Contains("/") ? md : "Assets/LevelData/" + md;
                    if (System.IO.File.Exists(path)) {
                        var meta = MiniJson.Deserialize(System.IO.File.ReadAllText(path)) as System.Collections.Generic.Dictionary<string, object>;
                        var m = meta != null && meta.ContainsKey("meta")
                            ? (System.Collections.Generic.Dictionary<string, object>)meta["meta"] : null;
                        if (m != null && m.ContainsKey("width")) {
                            float w = (float)System.Convert.ToDouble(m["width"]) / 100f;
                            termMin = w - 8f;   // gate must land in the final 8u of its own course
                        }
                    }
                }
            } catch (Exception) { /* keep the 80u default */ }
        }
        C(gate != null && gate.transform.position.x > termMin,
          "course: gate at the terminus (x=" + (gate != null ? gate.transform.position.x.ToString("F1") : "none") +
          ", termMin=" + termMin.ToString("0") + ")");
        C(GameObject.Find("FlagGateArt") != null, "course: flag art at the finish");
        C(GameObject.Find("PortalArt") != null, "course: portal art at the finish");

        // 5b) GEOMETRY GATE (BudE Sept 27 PM: "do a check on the actual geometric of the
        // map to make sure they are playable i kept hitting invisible walls and walking on
        // invisible grounds"). Three audits against the EXACT shipping scene:
        //   (a) every ground collider's TOP edge must be covered by painted Tilemap art —
        //       a collider with no art over it IS the invisible-ground bug;
        //   (a2) every EXPOSED vertical face of a ground must be painted — a bare face IS
        //       the invisible-wall bug;
        //   (b) reachability BFS with the REAL jump physics (runSpeed 4.6 u/s, jumpV 9.0,
        //       gravity 9.81*2.446, 0.9 hold factor -> max rise ~1.9u, gap ~3.7u): the flag
        //       gate and every Big Token must be reachable from spawn;
        //   (c) a hop slab with no SpriteRenderer under it reads as an invisible block.
        // A map failing any of these can never ship again.
        try {
            var plats = UnityEngine.Object.FindObjectsOfType<Transform>()
                .Where(t => t.name.StartsWith("Plat_") && t.GetComponent<BoxCollider2D>() != null)
                .Select(t => { var b = t.GetComponent<BoxCollider2D>();
                              return new { go = t.gameObject, x0 = b.bounds.min.x, x1 = b.bounds.max.x,
                                           y0 = b.bounds.min.y, top = b.bounds.max.y, h = b.bounds.size.y }; })
                .OrderBy(p => p.x0).ToList();
            C(plats.Count >= 10, "geometry: platform colliders present (" + plats.Count + ")");

            // (a) ONE-PIECE ART COVERAGE (Sept 27 PM: 'one large piece should be one large
            // art piece' + the invisible-ground bug): every ground collider must wear a
            // continuous GroundArt canvas whose bounds match the collider — the canvas
            // paints the top AND the side faces, so a matching art piece covers BOTH
            // invisible ground and invisible walls in one assertion.
            {
                var arts = UnityEngine.Object.FindObjectsOfType<SpriteRenderer>()
                    .Where(s => s.gameObject.name.StartsWith("GroundArt") && s.sprite != null).ToList();
                var bare = new List<string>(); float worst = 0f;
                foreach (var p in plats) {
                    if (p.h < 2f) continue;
                    var ctr = new Vector3((p.x0 + p.x1) / 2f, p.top - p.h / 2f, 0f);
                    var sr = arts.OrderBy(a => Vector3.Distance(a.bounds.center, ctr)).FirstOrDefault();
                    if (sr == null || Vector3.Distance(sr.bounds.center, ctr) > 0.08f) {
                        bare.Add(p.go.name + "@" + p.x0.ToString("F0") + "u"); continue;
                    }
                    worst = Mathf.Max(worst,
                        Mathf.Max(Mathf.Abs(sr.bounds.size.x - (p.x1 - p.x0)),
                                  Mathf.Abs(sr.bounds.size.y - p.h)));
                }
                C(bare.Count == 0, "geometry: no invisible ground/walls (" + bare.Count + " bare colliders" +
                  (bare.Count > 0 ? " e.g. " + string.Join(", ", bare.Take(3)) : "") + ")");
                C(worst < 0.06f, "geometry: ground art sized to collider (worst delta " + worst.ToString("F3") + ")");
            }
                // (b) REACHABILITY — BFS across platform tops with the real jump arc.
                float spawnX = lily != null ? lily.transform.position.x : 2.2f;
                int si = -1; float bestTop = -9999f;
                for (int i = 0; i < plats.Count; i++) {
                    var p = plats[i];
                    if (spawnX >= p.x0 - 0.6f && spawnX <= p.x1 + 0.6f && p.top > bestTop) { bestTop = p.top; si = i; }
                }
                C(si >= 0, "geometry: spawn stands on a platform");
                if (si >= 0) {
                    bool[] reach = new bool[plats.Count]; reach[si] = true;
                    var q2 = new Queue<int>(); q2.Enqueue(si);
                    while (q2.Count > 0) {
                        int ai = q2.Dequeue(); var a = plats[ai];
                        for (int bi = 0; bi < plats.Count; bi++) {
                            if (reach[bi]) continue;
                            var b = plats[bi];
                            float rise = b.top - a.top;
                            if (rise > 1.95f) continue;                    // above the jump arc
                            float gap = b.x0 > a.x1 ? b.x0 - a.x1 : (a.x0 > b.x1 ? a.x0 - b.x1 : 0f);
                            if (gap <= 3.7f - Mathf.Max(0f, rise) * 0.9f) { reach[bi] = true; q2.Enqueue(bi); }
                        }
                    }
                    bool gateOk = false;
                    if (gate != null) {
                        for (int i = 0; i < plats.Count; i++)
                            if (reach[i] && gate.transform.position.x >= plats[i].x0 - 1f && gate.transform.position.x <= plats[i].x1 + 1f) { gateOk = true; break; }
                    }
                    C(gateOk, "geometry: flag gate reachable with real jump physics");
                    int orphan = 0; var orphanWhere = new List<string>();
                    for (int t2 = 0; t2 < tokens.Length; t2++) {
                        var tp = tokens[t2].transform.position; bool ok2 = false;
                        for (int i = 0; i < plats.Count && !ok2; i++) {
                            if (!reach[i]) continue;
                            var p = plats[i];
                            if (tp.x >= p.x0 - 1.5f && tp.x <= p.x1 + 1.5f && tp.y - p.top > -1.2f && tp.y - p.top < 3.6f) ok2 = true;
                        }
                        if (!ok2) {   // straddling a gap between two reachable plats is also fine
                            for (int i = 0; i < plats.Count && !ok2; i++) {
                                if (!reach[i]) continue;
                                var p = plats[i];
                                if (tp.x > p.x1 && tp.x - p.x1 <= 2.2f && tp.y - p.top > -1.2f && tp.y - p.top < 3.2f) {
                                    for (int j = 0; j < plats.Count && !ok2; j++) {
                                        if (!reach[j] || j == i) continue;
                                        var r2 = plats[j];
                                        if (r2.x0 > tp.x && r2.x0 - tp.x <= 2.2f) ok2 = true;
                                    }
                                }
                            }
                        }
                        if (!ok2) { orphan++; if (orphanWhere.Count < 3) orphanWhere.Add("tok@" + tp.x.ToString("F0") + "," + tp.y.ToString("F1")); }
                    }
                    C(orphan == 0, "geometry: all tokens reachable (" + orphan + " orphans" +
                      (orphan > 0 ? " e.g. " + string.Join(", ", orphanWhere) : "") + ")");
                }
            }

            // (c) HOP SLABS — bare colliders read as invisible blocks mid-air.
            int bareHops = 0;
            foreach (var p in plats) {
                if (p.h >= 2f) continue;
                if (!p.go.GetComponentsInChildren<SpriteRenderer>(true).Any(sr => sr.sprite != null)) bareHops++;
            }
            C(bareHops == 0, "geometry: no invisible hop blocks (" + bareHops + " bare slabs)");
        } catch (Exception e) { C(false, "geometry: audit ran without crashing (" + e.Message + ")"); }

        // 6) REPORT
        bool allPass = fail.Count == 0;
        var sb = new StringBuilder();
        sb.AppendLine("{ \"passed\": " + pass + ", \"failed\": " + fail.Count + ", \"result\": \"" + (allPass ? "GREEN" : "RED") + "\",");
        sb.AppendLine("  \"failures\": [" + string.Join(", ", fail.Select(f => "\"" + f.Replace("\"", "'") + "\"")) + "],");
        sb.AppendLine("  \"checked_at\": \"" + DateTime.UtcNow.ToString("o") + "\" }");
        try { System.IO.File.WriteAllText(reportPath, sb.ToString()); } catch (Exception e) { Debug.LogWarning("[SMOKE] report write failed: " + e.Message); }
        Debug.Log("[SMOKE] RESULT: " + (allPass ? "GREEN - cleared to ship" : "RED - DO NOT SHIP") +
                  " (" + pass + " pass / " + fail.Count + " fail)");
        return allPass;
    }

    

    /// Standalone CI entry: -executeMethod LilFoots.EditorTools.LilFootsSmokeTest.RunAndExit: -executeMethod LilFoots.EditorTools.LilFootsSmokeTest.RunAndExit
    public static void RunAndExit() {
        var p = System.Environment.GetEnvironmentVariable("SMOKE_OUT");
        if (string.IsNullOrEmpty(p)) p = "smoke-report.json";
        bool ok = Run(p);
        EditorApplication.Exit(ok ? 0 : 1);
    }
}
}
#endif
