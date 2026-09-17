using UnityEditor;
using UnityEngine;
using System.IO;

public static class BuildScript
{
    // scene is generated (not hand-authored): Camera + Boot
    public static void EnsureScene()
    {
        if (File.Exists("Assets/Scenes/Main.unity")) return;
        Directory.CreateDirectory("Assets/Scenes");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
            UnityEditor.SceneManagement.NewSceneMode.Single);
        var camGo = new GameObject("Cam");
        var cam = camGo.AddComponent<Camera>();
        camGo.tag = "MainCamera";
        cam.orthographic = true;
        cam.orthographicSize = 7.2f;
        cam.transform.position = new Vector3(0, 6.4f, -10);
        var bootGo = new GameObject("Boot");
        bootGo.AddComponent<Boot>();
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");
    }

    public static void BuildWebGL()
    {
        EnsureScene();
        var scenes = new[] { "Assets/Scenes/Main.unity" };
        var dist = "dist";
        if (Directory.Exists(dist)) Directory.Delete(dist, true);
        Directory.CreateDirectory(dist);
        var bo = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = dist,
            targetGroup = BuildTargetGroup.WebGL,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        var res = BuildPipeline.BuildPlayer(bo);
        Debug.Log("BUILD RESULT: " + res.summary.result + " size=" + res.summary.totalSize);
        if (res.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            foreach (var step in res.steps)
                foreach (var msg in step.messages) Debug.Log(step.name + ": " + msg.message);
            EditorApplication.Exit(1);
        }
        EditorApplication.Exit(0);
    }
}
