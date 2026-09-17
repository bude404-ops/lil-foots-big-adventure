// Clean Bright Woodland screen: flat depth plate parallax, grass slab ground, undergrowth fg.
using UnityEngine;

public class WorldBuilder : MonoBehaviour
{
    public Transform bg, mid, play, fg;

    public static WorldBuilder Build()
    {
        var go = new GameObject("World");
        var w = go.AddComponent<WorldBuilder>();
        w.bg = new GameObject("L1-bg").transform;
        w.mid = new GameObject("L2-mid").transform;
        w.play = new GameObject("L3-play").transform;
        w.fg = new GameObject("L4-fg").transform;

        // L1: sky + distant hills plate (slow parallax, cover-fit, world-drawn wide)
        var plate = SpriteFromArt("bgplate");
        var bgGo = NewSprite("plate", plate, -20, new Vector3(0, 0, 30f), 40f);
        bgGo.transform.SetParent(w.bg, false);
        bgGo.GetComponent<SpriteRenderer>().drawMode = SpriteDrawMode.Sliced;
        bgGo.GetComponent<SpriteRenderer>().size = new Vector2(120f, 22f);

        // L2: cedars on the horizon
        var cedar = SpriteFromArt("cedar");
        for (int i = 0; i < 7; i++)
        {
            var cg = NewSprite("cedar" + i, cedar, -10, new Vector3(-26f + i * 8.6f, 1.2f, 0), 6.5f);
            cg.transform.SetParent(w.mid, false);
        }

        // L3: gameplay — grass slab ground (art bible: physically connected, no floating rects)
        var slab = SpriteFromArt("plat");
        float slabW = slab.bounds.size.x * 0.30f; // drawn at 30px/unit-ish scale below
        float scale = 0.30f; float sw = slab.bounds.size.x * scale; float sh = slab.bounds.size.y * scale;
        // ground: 3 slabs left-to-right, tops at y=0
        for (int i = 0; i < 3; i++)
        {
            var g = NewSprite("slab" + i, slab, 0, new Vector3(-sw + i * sw, -sh / 2f + 0.25f, 0), scale);
            g.transform.SetParent(w.play, false);
        }
        // one floating platform (reachable from the ground)
        var fp = NewSprite("float-slab", slab, 0, new Vector3(7.2f, 2.6f + 0.25f, -0.2f), 0.22f);
        fp.transform.SetParent(w.play, false);
        // a few gold BIG tokens floating over the playfield
        var tok = SpriteFromArt("token");
        for (int i = 0; i < 5; i++)
        {
            var t = NewSprite("token" + i, tok, 5, new Vector3(-8f + i * 4.4f, 2.2f + (i % 2) * 1.1f, 0), 0.75f);
            t.transform.SetParent(w.play, false);
        }

        // L4: foreground undergrowth strip
        var fgs = SpriteFromArt("fg");
        var fgo = NewSprite("fg-strip", fgs, 10, new Vector3(0, -1.15f, 0), 0.40f);
        fgo.transform.SetParent(w.fg, false);

        return w;
    }

    static Sprite SpriteFromArt(string n)
    {
        var tex = Resources.Load<Texture2D>("Art/" + n);
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    static GameObject NewSprite(string n, Sprite spr, int order, Vector3 pos, float scale)
    {
        var go = new GameObject(n);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = spr; sr.sortingOrder = order;
        go.transform.position = pos;
        go.transform.localScale = new Vector3(scale, scale, 1);
        return go;
    }

    void Update()
    {
        // parallax: camera is global; layers drift by depth factor
        if (Boot.I == null) return;
        var c = Boot.I.cam.transform.position;
        bg.position = new Vector3(c.x * 0.92f, 0f, 30f);
        mid.position = new Vector3(c.x * 0.75f, 0f, 0f);
        fg.position = new Vector3(c.x * 1.06f, -1.15f, 0f);
    }
}
