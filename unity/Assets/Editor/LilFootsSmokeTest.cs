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

        var cams = UnityEngine.Object.FindObjectsOfType<Camera>();
        var mains = cams.Where(c => c.CompareTag("MainCamera")).ToList();
        C(mains.Count == 1, "camera: exactly one MainCamera (found " + mains.Count + ")");
        C(mains.Count == 1 && mains[0].GetComponent<LilFoots.CameraFollow>() != null, "camera: CameraFollow attached");

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
        bool m1 = (System.Environment.GetEnvironmentVariable("MAP_DATA") == "map_m1.json") || sceneName.StartsWith("MapM1");
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
        C(gate != null && gate.transform.position.x > (m1 ? 30f : 80f),
          "course: gate at the terminus (x=" + (gate != null ? gate.transform.position.x.ToString("F1") : "none") + ")");
        C(GameObject.Find("FlagGateArt") != null, "course: flag art at the finish");
        C(GameObject.Find("PortalArt") != null, "course: portal art at the finish");
        if (!m1) {
            var hounds = UnityEngine.Object.FindObjectsOfType<LilFoots.HoundController>();
            C(hounds.Length >= 1, "course: hounds present (" + hounds.Length + ")");
        }

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

    /// Standalone CI entry: -executeMethod LilFoots.EditorTools.LilFootsSmokeTest.RunAndExit
    public static void RunAndExit() {
        var p = System.Environment.GetEnvironmentVariable("SMOKE_OUT");
        if (string.IsNullOrEmpty(p)) p = "smoke-report.json";
        bool ok = Run(p);
        EditorApplication.Exit(ok ? 0 : 1);
    }
}
}
#endif
