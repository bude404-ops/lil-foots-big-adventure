using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>SPLASH SCENE BUILDER (Bude, Sept 19: studio launch sequence AFTER the Unity
/// loading screen: BIG Entertainment logo -> Bude Vision image -> game menu, each with its
/// own moment). Unity performs the work: this pass builds the Splash scene natively; the
/// runtime SplashController plays the sequence and hands off to Map001 (the game menu).
/// Art slots: Assets/Art/art_splash_big.png + art_splash_bude.png. Missing art falls back to
/// clean typographic cards (BIG ENTERTAINMENT / BUDE VISION) so the sequence ships complete
/// and swaps to real art by dropping in the two files - no code change needed.</summary>
public static class LilFootsSplashPass
{
    public static string ScenePath { get { return "Assets/Scenes/Splash.unity"; } }

    public static void BuildSplashScene()
    {
        Directory.CreateDirectory("Assets/Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Black stage camera (the scene behind the overlay canvas)
        var camGo = new GameObject("SplashCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.tag = "Untagged"; // Map001 owns the MainCamera; this one never follows anything

        var go = new GameObject("Splash");
        var ctl = go.AddComponent<LilFoots.SplashController>();
        ctl.logos = new[] { Art("art_splash_big.png"), Art("art_splash_bude.png") };
        ctl.labels = new[] { "BIG ENTERTAINMENT", "BUDE VISION" };

        // the sequence needs an EventSystem-free path (raw Input), but if Map001's EventSystem
        // is loaded later it stays owned by the menu - nothing to dedupe here.

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[SplashPass] Splash scene built -> " + ScenePath +
                  " (big: " + (ctl.logos[0] != null ? "art" : "typographic fallback") +
                  ", bude: " + (ctl.logos[1] != null ? "art" : "typographic fallback") + ")");
    }

    static Sprite Art(string file)
    {
        var path = Path.Combine("Assets/Art", file);
        if (!File.Exists(path)) return null;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null) return null;
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
