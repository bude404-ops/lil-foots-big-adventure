// Paper-doll rig from Bude's exact character art + procedural cartoon animation
// (idle breathe / run cycle with oversized-feet slaps / jump squash-stretch).
using System.Collections.Generic;
using UnityEngine;

[System.Serializable] public class RigPart { public string name, file; public float x0,y0,x1,y1,z; public float[] pivot; }
[System.Serializable] public class RigData { public string sprite; public float w,h,ground_img_y; public List<RigPart> parts; }

public class PlayerRig : MonoBehaviour
{
    public const float PPU = 100f, CX = 173f, GY = 460f;
    class Part { public Transform t; public SpriteRenderer sr; public Vector3 rest; public float restZ; public RigPart def; }
    List<Part> parts = new List<Part>();
    Dictionary<string,Part> byName = new Dictionary<string, Part>();

    public Transform head, torso, armL, armR, legL, legR, footL, footR;
    public SpriteRenderer shadow;
    [HideInInspector] public float groundY = 0f;

    public float phase = 0f;          // animation clock
    public float runSpeed01 = 0f;     // 0..1 smoothed
    public bool airborne = false;
    public float vy = 0f;
    public float landSquash = 0f;     // 0..1 decaying
    public float launchStretch = 0f;

    public static PlayerRig Build(string charName, string prefix)
    {
        var data = Resources.Load<TextAsset>(charName + "/rig");
        var rig = JsonUtility.FromJson<RigData>(data.text);
        var root = new GameObject(charName);
        var pr = root.AddComponent<PlayerRig>();

        foreach (var p in rig.parts)
        {
            var tex = Resources.Load<Texture2D>(charName + "/" + prefix + "-" + p.name);
            var cw = p.x1 - p.x0; var ch = p.y1 - p.y0;
            // pivot: image coords -> crop-local 0..1 (Unity pivot y=0 at bottom)
            var piv = new Vector2((p.pivot[0] - p.x0) / cw, 1f - (p.pivot[1] - p.y0) / ch);
            var spr = Sprite.Create(tex, new Rect(0, 0, cw, ch), piv, PPU);
            var go = new GameObject(p.name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spr;
            sr.sortingOrder = 100 + (int)p.z;
            var world = new Vector2((p.pivot[0] - CX) / PPU, (GY - p.pivot[1]) / PPU);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(world.x, world.y, 0);
            var part = new Part { t = go.transform, sr = sr, rest = go.transform.localPosition, restZ = 0, def = p };
            pr.parts.Add(part); pr.byName[p.name] = part;
        }
        pr.head = pr.byName["head"].t; pr.torso = pr.byName["torso"].t;
        pr.armL = pr.byName["armL"].t; pr.armR = pr.byName["armR"].t;
        pr.legL = pr.byName["legL"].t; pr.legR = pr.byName["legR"].t;
        pr.footL = pr.byName["footL"].t; pr.footR = pr.byName["footR"].t;

        // contact shadow (clean contact shadows per art bible)
        var sh = new GameObject("shadow");
        var ssr = sh.AddComponent<SpriteRenderer>();
        ssr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0,0,4,4), new Vector2(0.5f,0.5f), 100f);
        ssr.color = new Color(0.07f, 0.15f, 0.11f, 0.30f);
        ssr.sortingOrder = 50;
        ssr.transform.localScale = new Vector3(2.4f, 0.28f, 1);
        ssr.transform.SetParent(root.transform, false);
        pr.shadow = ssr;
        return pr;
    }

    public void ResetPose()
    {
        foreach (var p in parts) { p.t.localPosition = p.rest; p.t.localRotation = Quaternion.identity; p.t.localScale = Vector3.one; }
        transform.localScale = Vector3.one;
    }

    void LateUpdate()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        Tick(dt);
        ApplyPose();
    }

    public void Tick(float dt)
    {
        phase += dt * (3f + runSpeed01 * 5f);
        if (landSquash > 0) landSquash = Mathf.Max(0, landSquash - dt * 4.5f);
        if (launchStretch > 0) launchStretch = Mathf.Max(0, launchStretch - dt * 4.5f);
    }

    public void ApplyPose()
    {
        float run = runSpeed01;
        float s = Mathf.Sin(phase * 2f), c = Mathf.Cos(phase * 2f);
        float s1 = Mathf.Sin(phase * 2f), s2 = Mathf.Sin(phase * 2f + Mathf.PI);

        ResetPose();
        transform.localScale = Vector3.one;

        if (airborne)
        {
            // JUMP: stretch by vertical speed, arms up, legs tucked back, feet pointed
            float k = Mathf.Clamp01(Mathf.Abs(vy) / 9f);
            float st = 0.10f + 0.10f * k;
            transform.localScale = new Vector3(1f - st * 0.55f, 1f + st, 1f);
            armL.Rotate(0, 0,  130f * Mathf.Sign(vy) * -0f + 118f);  // arms up
            armR.Rotate(0, 0, -118f);
            legL.Rotate(0, 0,  34f);   // tuck
            legR.Rotate(0, 0,  34f);
            footL.Rotate(0, 0, -28f);  // toes down
            footR.Rotate(0, 0, -28f);
            head.Rotate(0, 0, 3f);
        }
        else if (run > 0.03f)
        {
            // RUN: bounce, arm counter-swing, leg stride, foot slap (oversized feet!)
            float bob = Mathf.Abs(s1) * 0.09f * run;
            transform.localScale = new Vector3(1f + bob * 0.6f, 1f - bob, 1f);
            torso.Rotate(0, 0, 4f * run);
            head.Rotate(0, 0, -2f * run + Mathf.Sin(phase * 2f) * 3f);
            armL.Rotate(0, 0, -34f * run * s1);
            armR.Rotate(0, 0, -34f * run * s2);
            legL.Rotate(0, 0,  40f * run * s1);
            legR.Rotate(0, 0,  40f * run * s2);
            footL.Rotate(0, 0, -18f * run * s1 + 6f * run);
            footR.Rotate(0, 0, -18f * run * s2 + 6f * run);
            // lean into the run
            transform.Rotate(0, 0, 0);
        }
        else
        {
            // IDLE: breathing bob, gentle sway, occasional look-around
            float b = Mathf.Sin(phase * 0.8f);
            transform.localScale = new Vector3(1f + b * 0.008f, 1f + b * 0.012f, 1f);
            head.Rotate(0, 0, b * 1.2f);
            armL.Rotate(0, 0, b * 1.5f);
            armR.Rotate(0, 0, b * -1.5f);
            torso.Rotate(0, 0, b * 0.4f);
        }

        if (landSquash > 0)
        {
            float q = landSquash;
            transform.localScale = new Vector3(transform.localScale.x * (1f + 0.30f * q),
                                                transform.localScale.y * (1f - 0.30f * q), 1f);
        }
        if (launchStretch > 0)
        {
            float q = launchStretch;
            transform.localScale = new Vector3(transform.localScale.x * (1f - 0.14f * q),
                                                transform.localScale.y * (1f + 0.18f * q), 1f);
        }

        // contact shadow follows under the body, anchored to local ground
        if (shadow != null)
        {
            float gy = groundY;
            var p = transform.position;
            float h = Mathf.Max(0f, p.y - gy);
            float sk = Mathf.Max(0.35f, 1f - h / 4f);
            shadow.transform.position = new Vector3(p.x, gy + 0.04f, 0);
            shadow.transform.localScale = new Vector3(2.4f * sk + 0.6f, 0.28f * sk + 0.06f, 1);
            var cc = shadow.color; cc.a = 0.30f * sk; shadow.color = cc;
        }
    }

    // pose API for QC shots
    public void PoseIdle(float t) { phase = t; runSpeed01 = 0; airborne = false; }
    public void PoseRun(float t) { phase = t; runSpeed01 = 1; airborne = false; }
    public void PoseJump(float t) { phase = t; runSpeed01 = 0.6f; airborne = true; vy = 8f; }
    public void PoseLand(float t) { phase = t; runSpeed01 = 0.5f; airborne = false; landSquash = 1f; }
}
