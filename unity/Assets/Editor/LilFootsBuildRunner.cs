#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
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

        // player identity
        PlayerSettings.companyName = "Bigfoot404 LLC";
        PlayerSettings.productName = "Lil Foots Big Adventure";
        PlayerSettings.SetApplicationIdentifier("com.bigfoot404.lilfootsbigadventure");

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

        PlayerSettings.companyName = "Bigfoot404 LLC";
        PlayerSettings.productName = "Lil Foots Big Adventure";
        PlayerSettings.SetApplicationIdentifier("com.bigfoot404.lilfootsbigadventure");

        var scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Map001.unity", true) };
        EditorBuildSettings.scenes = scenes;

        var outDir = System.Environment.GetEnvironmentVariable("WEBGL_OUT");
        if (string.IsNullOrEmpty(outDir)) outDir = "Builds/WebGL";
        Directory.CreateDirectory(outDir);

        Debug.Log("[BuildRunner] Unity building the game (WebGL): " + outDir);
        var report = BuildPipeline.BuildPlayer(scenes, outDir, BuildTarget.WebGL, BuildOptions.None);
        var sum = report.summary;
        Debug.Log($"[BuildRunner] WEBGL BUILD RESULT={sum.result} size={sum.totalSize} errors={sum.totalErrors}");

        EditorApplication.Exit(sum.result == BuildResult.Succeeded ? 0 : 1);
    }
}
}
#endif
