// Headless QC renders: rest / run / jump / land poses from the live rig.
using UnityEditor;
using UnityEngine;
using System.IO;

public static class QCShots
{
    public static void Shoot()
    {
        BuildScript.EnsureScene();
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        Boot.BuildAll();
        var b = Boot.I;
        var cam = b.cam;

        Directory.CreateDirectory("QCShots");
        int[] sizes = { 390, 844 };
        RenderTexture rt = new RenderTexture(sizes[0] * 2, sizes[1] * 2, 24);
        cam.targetTexture = rt;
        cam.Render();

        // idle
        b.player.PoseIdle(1.3f); b.player.ApplyPose();
        b.player.transform.position = new Vector3(0, 0, 0);
        Snap(rt, "QCShots/idle.png");
        // run (mid-stride)
        b.player.PoseRun(0.7f); b.player.ApplyPose();
        Snap(rt, "QCShots/run.png");
        // jump (apex stretch)
        b.player.PoseJump(0.4f); b.player.ApplyPose();
        b.player.transform.position = new Vector3(4, 3.4f, 0);
        Snap(rt, "QCShots/jump.png");
        // landing squash + dust
        b.player.PoseLand(0.2f); b.player.ApplyPose();
        b.player.transform.position = new Vector3(-3, 0, 0);
        Snap(rt, "QCShots/land.png");

        cam.targetTexture = null;
        Debug.Log("QC SHOTS DONE");
        EditorApplication.Exit(0);
    }

    static void Snap(RenderTexture rt, string path)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        Camera.main.Render();
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        RenderTexture.active = prev;
    }
}
