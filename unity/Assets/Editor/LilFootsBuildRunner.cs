#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;

namespace LilFoots.EditorTools {
/// <summary>
/// THE BUILD RUNNER (per UNITY-SOURCE-OF-TRUTH.md): Big operates Unity, UNITY BUILDS THE GAME.
/// CI entry — builds map 001 from data, runs the art pass, saves the real scene, renders QC shots,
/// then makes Unity BUILD the actual game (Android APK). A pass is complete only when Unity builds it.
/// </summary>
public static class LilFootsBuildRunner {
    [MenuItem("Tools/Lil Foots/Build Map 001 + APK")]
    public static void BuildAndShipApk() {
        // scene + art + saved Map001.unity + QC shots (no exit — we keep the session for the build)
        LilFootsArtPass.BuildAndShootCore();
        // GATE 2: machine play-test on the EXACT saved scene that ships. Red = build withheld.
        if (!LilFootsSmokeTest.Run("smoke-report.json")) {
            Debug.Log("[BuildRunner] SMOKE RED - refusing to build or ship.");
            EditorApplication.Exit(2); return;
        }

        // player identity
        PlayerSettings.companyName = "Bigfoot404 LLC";
        PlayerSettings.productName = "Lil Foots Big Adventure";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.bigfoot404.lilfootsbigadventure");

        var scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Map001.unity", true) };
        EditorBuildSettings.scenes = scenes;

        var apk = System.Environment.GetEnvironmentVariable("APK_OUT");
        if (string.IsNullOrEmpty(apk)) apk = "Builds/LilFoots.apk";
        Directory.CreateDirectory(Path.GetDirectoryName(apk));

        Debug.Log("[BuildRunner] Unity building the game: " + apk);
        var report = BuildPipeline.BuildPlayer(scenes, apk, BuildTarget.Android, BuildOptions.None);
        var sum = report.summary;
        Debug.Log($"[BuildRunner] BUILD RESULT={sum.result} size={sum.totalSize} errors={sum.totalErrors} warnings={sum.totalWarnings}");

        EditorApplication.Exit(sum.result == BuildResult.Succeeded ? 0 : 1);
    }

    [MenuItem("Tools/Lil Foots/Build Map 001 + WebGL (live web preview)")]
    public static void BuildAndShipWebGl() {
        // same doctrine: Unity builds the scene from data + art pass + QC, then BUILDS the game
        LilFootsArtPass.BuildAndShootCore();
        // GATE 2: machine play-test on the EXACT saved scene that ships. Red = build withheld.
        if (!LilFootsSmokeTest.Run("smoke-report.json")) {
            Debug.Log("[BuildRunner] SMOKE RED - refusing to build or ship.");
            EditorApplication.Exit(2); return;
        }

        PlayerSettings.companyName = "Bigfoot404 LLC";
        PlayerSettings.productName = "Lil Foots Big Adventure";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.WebGL, "com.bigfoot404.lilfootsbigadventure");

        var scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Map001.unity", true) };
        EditorBuildSettings.scenes = scenes;

        var outDir = System.Environment.GetEnvironmentVariable("WEBGL_OUT");
        if (string.IsNullOrEmpty(outDir)) outDir = "Builds/WebGL";
        Directory.CreateDirectory(outDir);

        // GitHub Pages serves raw files without Content-Encoding headers, so a gzip
        // build can't be parsed by the loader ("Unable to parse WebGL.framework.js.gz").
        // The exact API differs across 2022.3 point releases -> use reflection:
        // prefer Disabled compression; fall back to gzip + decompressionFallback
        // (loader inflates client-side, also Pages-safe).
        {
            const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            var prop = typeof(EditorUserBuildSettings).GetProperty("webGLCompressionFormat", F);
            if (prop != null && prop.CanWrite) {
                prop.SetValue(null, System.Enum.Parse(prop.PropertyType, "Disabled"), null);
                Debug.Log("[BuildRunner] WebGL compression format -> Disabled (Pages-safe, raw files).");
            } else {
                var wgl = typeof(PlayerSettings).GetNestedType("WebGL", F);
                var df = wgl != null ? wgl.GetProperty("decompressionFallback", F) : null;
                if (df != null && df.CanWrite) {
                    df.SetValue(null, true, null);
                    Debug.Log("[BuildRunner] WebGL decompressionFallback -> true (loader inflates gzip client-side).");
                } else {
                    Debug.LogWarning("[BuildRunner] No WebGL compression API found via reflection; post-build gzip strip in CI covers Pages.");
                }
            }
        }
        Debug.Log("[BuildRunner] Unity building the game (WebGL): " + outDir);
        var report = BuildPipeline.BuildPlayer(scenes, outDir, BuildTarget.WebGL, BuildOptions.None);
        var sum = report.summary;
        Debug.Log($"[BuildRunner] WEBGL BUILD RESULT={sum.result} size={sum.totalSize} errors={sum.totalErrors}");

        EditorApplication.Exit(sum.result == BuildResult.Succeeded ? 0 : 1);
    }
}
}
#endif
