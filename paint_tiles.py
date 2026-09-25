#!/usr/bin/env python3
"""
LIL FOOTS PAINTED TILE ENGINE v1 (BudE, Sept 25: 'how can we have unity build the map
but use OUR suggestive art style and quality instead of trying to use the reference art')

Authors the terrain tile set 100% from OUR painterly palette (sampled from the locked
world-skin art - art_grass_new / art_earth_new / art_hopblock) - zero reference crops.

Style laws (the locked-piece doctrine, applied to terrain):
  - sage greens + gold light on grass, warm dark soil with moss flecks
  - painterly: soft multi-scale light dapple, blade strokes, painted shadows
  - organic: wavy blade silhouette on top, hanging fringe into the dirt
  - seamless: every tile is mirror-doubled (self-tiling), all variants share the
    canonical border ring, brightness-matched - no visible seams at any adjacency

Output: lf_grass_a-d, lf_dirt_a-d, lips, legacy 128px names in unity/Assets/Art/Tiles/
"""
import math, random, os
from PIL import Image, ImageFilter

random.seed(404)
D = 'unity/Assets/Art/Tiles/'
T = 256

# ---- OUR PALETTE (opaque-pixel samples of the locked world-skin art) ----
GRASS_LIT   = (128, 148, 64)   # art_grass_new lit zone
GRASS_MID   = (100, 120, 40)   # art_grass_new body
GRASS_DEEP  = (72, 92, 32)
GRASS_SHADOW= (48, 64, 24)
GOLD_LIGHT  = (160, 160, 60)   # art_grass_new highlight fleck
MOSS_FLECK  = (120, 140, 48)
EARTH_MID   = (56, 56, 30)     # art_earth_new body
EARTH_DEEP  = (40, 40, 20)
EARTH_WARM  = (72, 60, 36)     # hopblock warm soil
EARTH_LIGHT = (88, 84, 44)
STONE       = (48, 44, 36)
STONE_LIT   = (92, 88, 68)

def lerp(a, b, t): return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))
def add(im, px_list):
    pass

def soft_noise(w, h, scale, seed):
    """Smooth value-noise field 0..1 via blurred random grid."""
    random.seed(seed)
    gw, gh = max(2, w // scale), max(2, h // scale)
    g = [[random.random() for _ in range(gw)] for _ in range(gh)]
    nim = Image.new('L', (gw, gh))
    np_ = nim.load()
    for y in range(gh):
        for x in range(gw):
            np_[x, y] = int(255 * g[y][x])
    return nim.resize((w, h), Image.BILINEAR).filter(ImageFilter.GaussianBlur(scale * 0.6))

def field_at(nim, x, y):
    return nim.load()[x, y] / 255.0

def paint_grass(seed):
    """Painterly grass body: gradient + dapple + blade strokes."""
    random.seed(seed)
    w = h = T
    im = Image.new('RGB', (w, h))
    px = im.load()
    n1 = soft_noise(w, h, 46, seed * 7 + 1)      # broad light dapple
    n2 = soft_noise(w, h, 14, seed * 7 + 2)     # medium patchiness
    for y in range(h):
        # vertical gradient: lit at top, deep at bottom
        t = y / (h - 1)
        base = lerp(GRASS_LIT, GRASS_SHADOW, t ** 0.9)
        for x in range(w):
            d1 = field_at(n1, x, y) - 0.5
            d2 = field_at(n2, x, y) - 0.5
            c = base
            c = lerp(c, GRASS_MID, 0.35 * (0.5 - t))                 # mid-tone wash up top
            c = tuple(int(v * (1 + 0.16 * d1 + 0.10 * d2)) for v in c)  # dapple
            if d1 > 0.22 and d2 > 0.3:
                c = lerp(c, GOLD_LIGHT, 0.5 * (d1 - 0.22))            # gold sunlit fleck
            elif d1 < -0.25:
                c = lerp(c, GRASS_DEEP, 0.4 * (-d1 - 0.25))          # shadow pocket
            px[x, y] = c
    # blade strokes: short vertical dashes, lighter above, darker below
    for _ in range(2600):
        x = random.randint(0, w - 2); y = random.randint(0, h - 6)
        ln = random.randint(3, 7)
        lit = random.random() < 0.5 - (y / h) * 0.35
        c = lerp(GRASS_LIT, GRASS_SHADOW, random.uniform(0.1, 0.5)) if lit else lerp(GRASS_DEEP, GRASS_SHADOW, random.uniform(0.2, 0.7))
        for dy in range(ln):
            yy = y + dy
            if 0 <= yy < h and 0 <= x < w:
                o = px[x, yy]
                a = random.uniform(0.35, 0.75)
                px[x, yy] = tuple(int(o[i] + (c[i] - o[i]) * a) for i in range(3))
                if random.random() < 0.3 and x + 1 < w:
                    o2 = px[x + 1, yy]
                    px[x + 1, yy] = tuple(int(o2[i] + (c[i] - o2[i]) * a * 0.5) for i in range(3))
    return im

def paint_dirt(seed):
    """Painterly soil: warm earth gradient + stones + root threads + moss flecks."""
    random.seed(seed)
    w = h = T
    im = Image.new('RGB', (w, h))
    px = im.load()
    n1 = soft_noise(w, h, 52, seed * 11 + 3)
    n2 = soft_noise(w, h, 16, seed * 11 + 4)
    for y in range(h):
        t = y / (h - 1)
        base = lerp(EARTH_MID, EARTH_DEEP, t ** 0.8)
        for x in range(w):
            d1 = field_at(n1, x, y) - 0.5
            d2 = field_at(n2, x, y) - 0.5
            c = tuple(int(v * (1 + 0.22 * d1 + 0.16 * d2)) for v in base)
            if d2 > 0.30:
                c = lerp(c, EARTH_WARM, 0.4 * (d2 - 0.30))            # warm patch
            elif d2 < -0.30:
                c = lerp(c, (28, 30, 18), 0.45 * (-d2 - 0.30))        # cold dark pocket
            px[x, y] = c
    # vertical soil grain streaks
    for _ in range(70):
        x = random.randint(0, w - 1)
        c = lerp(EARTH_DEEP, EARTH_LIGHT, random.uniform(0.0, 0.5))
        a = random.uniform(0.10, 0.22)
        for y in range(h):
            o = px[x, y]
            px[x, y] = tuple(int(o[i] + (c[i] - o[i]) * a) for i in range(3))
            if random.random() < 0.15: x = min(w - 1, x + random.choice([-1, 1]))
    # painted stones: soft blobs with a lit top rim
    for _ in range(random.randint(7, 10)):
        cx, cy = random.randint(18, w - 18), random.randint(24, h - 24)
        rx, ry = random.randint(9, 20), random.randint(6, 13)
        for dx in range(-rx - 2, rx + 3):
            for dy in range(-ry - 2, ry + 3):
                d = math.hypot(dx / rx, dy / ry)
                if d < 1.0:
                    x, y = cx + dx, cy + dy
                    if 0 <= x < w and 0 <= y < h:
                        t = (1 - d)
                        base = lerp(STONE, STONE_LIT, max(0.0, 0.55 - dy / ry * 0.9))
                        o = px[x, y]
                        px[x, y] = tuple(int(o[i] + (base[i] - o[i]) * t * 0.85) for i in range(3))
    # root threads: dark meanders
    for _ in range(5):
        x, y = random.randint(0, w - 1), random.randint(0, h - 1)
        for _ in range(random.randint(30, 70)):
            x = max(0, min(w - 1, x + random.randint(-2, 2)))
            y = max(0, min(h - 1, y + random.randint(-1, 2)))
            o = px[x, y]
            c = (24, 22, 14)
            px[x, y] = tuple(int(o[i] + (c[i] - o[i]) * 0.5) for i in range(3))
    # moss flecks: our sage green sprinkled through the soil
    for _ in range(700):
        x, y = random.randint(0, w - 1), random.randint(0, h - 1)
        if field_at(n2, x, y) > 0.45:
            o = px[x, y]
            c = MOSS_FLECK
            a = random.uniform(0.30, 0.60)
            px[x, y] = tuple(int(o[i] + (c[i] - o[i]) * a) for i in range(3))
    return im

# ---------- seamless machinery (same proven treatment) ----------
def mirror_x(im):
    w, h = im.size
    out = Image.new('RGB', (w * 2, h))
    out.paste(im, (0, 0)); out.paste(im.transpose(Image.FLIP_LEFT_RIGHT), (w, 0))
    return out

def mirror_y(im):
    w, h = im.size
    out = Image.new('RGB', (w, h * 2))
    out.paste(im, (0, 0)); out.paste(im.transpose(Image.FLIP_TOP_BOTTOM), (0, h))
    return out

def gain_match(src, target, blend=0.6):
    def means(im):
        px = im.load(); w, h = im.size
        return [sum(px[x, y][ch] for x in range(0, w, 8) for y in range(0, h, 8))
                / len(range(0, w, 8)) / len(range(0, h, 8)) for ch in range(3)]
    ms, mt = means(src), means(target)
    gains = [1 + (mt[ch] / max(ms[ch], 1) - 1) * blend for ch in range(3)]
    out = Image.new('RGB', src.size); s, op = src.load(), out.load()
    for x in range(src.size[0]):
        for y in range(src.size[1]):
            p = s[x, y]
            op[x, y] = tuple(min(255, int(p[ch] * gains[ch])) for ch in range(3))
    return out

def apply_ring(im, canon):
    w, h = im.size; W = 20
    cl = canon.crop((0, 0, W, h)).load()
    cr = canon.crop((w - W, 0, w, h)).load()
    op = im.load()
    for y in range(h):
        for i in range(W):
            a = (W - i) / W
            l = cl[i, y]; q = op[i, y]
            op[i, y] = tuple(int(l[c] * a + q[c] * (1 - a)) for c in range(3))
            a2 = (i + 1) / W
            r = cr[i, y]; q2 = op[w - W + i, y]
            op[w - W + i, y] = tuple(int(r[c] * a2 + q2[c] * (1 - a2)) for c in range(3))
    return im

def finish_cap(im, seed):
    """Organic blade silhouette top + sunlit lip + hanging fringe bottom."""
    random.seed(seed); w, h = im.size
    im = im.convert('RGBA'); px = im.load()
    for x in range(w):
        base = 12 + 6 * math.sin(x * 0.22) + 4 * math.sin(x * 0.09 + 2.1) + random.uniform(-1.5, 1.5)
        tooth = 6 if ((x // 13) % 3 == 0 and random.random() < 0.8) else 0
        cut = max(2, int(base + tooth))
        for y in range(cut):
            px[x, y] = (px[x, y][0], px[x, y][1], px[x, y][2], 0)
        for y in range(cut, min(cut + 8, h)):
            p = px[x, y]; f = 1.12 - (y - cut) * 0.016
            px[x, y] = (min(255, int(p[0] * f)), min(255, int(p[1] * f)), min(255, int(p[2] * f)), p[3])
        fd = 14 + 8 * math.sin(x * 0.17 + 0.8) + random.uniform(-2.5, 2.5)
        f0 = h - int(fd)
        for y in range(f0, h):
            t = (y - f0) / max(1, (h - f0))
            a = int(255 * (1 - t) ** 1.4)
            p = px[x, y]; sh = 0.70 + 0.30 * (1 - t)
            px[x, y] = (int(p[0] * sh), int(p[1] * sh), int(p[2] * sh), a)
    return im

def finish_dirt(im, seed):
    """Shadow band right under the grass cap."""
    random.seed(seed); w, h = im.size
    im = im.convert('RGBA'); px = im.load()
    for x in range(w):
        band = int(18 + 6 * math.sin(x * 0.11 + random.random() * 0.2))
        for y in range(band):
            p = px[x, y]; f = 0.74 + 0.16 * (y / max(1, band))
            px[x, y] = (int(p[0] * f), int(p[1] * f), int(p[2] * f), p[3])
    return im

def make_lip(base, side, seed):
    im = base.copy(); w, h = im.size; px = im.load()
    random.seed(seed)
    if side == 'l':
        for y in range(h):
            cx = 24 + 16 * math.sin(y * 0.07) + 10 * math.sin(y * 0.031 + 1.3) + random.uniform(-2, 2)
            for x in range(int(cx) + 4):
                t = max(0.0, 1 - (cx - x) / 4)
                p = px[x, y]
                px[x, y] = (p[0], p[1], p[2], int(255 * t))
    else:
        for y in range(h):
            cx = w - 24 - 16 * math.sin(y * 0.07) - 10 * math.sin(y * 0.031 + 1.3) - random.uniform(-2, 2)
            for x in range(max(0, int(cx) - 4), w):
                t = max(0.0, 1 - (x - cx) / 4)
                p = px[x, y]
                px[x, y] = (p[0], p[1], p[2], int(255 * t))
    return im

# ---------- build ----------
caps = []
for i in range(4):
    c = paint_grass(100 + i)                    # 256x256 painted
    c = c.crop((0, 0, T // 2, T))               # take half, mirror-double -> self-tiling
    caps.append(mirror_x(c))
canon = caps[0].copy()
caps = [apply_ring(gain_match(c, canon), canon) for c in caps]
caps = [finish_cap(c, 404 + i) for i, c in enumerate(caps)]
for i, c in enumerate(caps):
    c.save(D + f'lf_grass_{"abcd"[i]}.png')

dirts = []
for i in range(4):
    c = paint_dirt(200 + i)
    c = c.crop((0, 0, T // 2, T // 2))
    dirts.append(mirror_y(mirror_x(c)))
dcanon = dirts[0].copy()
dirts = [apply_ring(gain_match(c, dcanon), dcanon) for c in dirts]
dirts = [finish_dirt(c, 904 + i) for i, c in enumerate(dirts)]
for i, c in enumerate(dirts):
    c.save(D + f'lf_dirt_{"abcd"[i]}.png')

make_lip(caps[0], 'l', 501).save(D + 'lf_grass_l.png')
make_lip(caps[0], 'r', 502).save(D + 'lf_grass_r.png')
make_lip(dirts[0], 'l', 503).save(D + 'lf_dirt_l.png')
make_lip(dirts[0], 'r', 504).save(D + 'lf_dirt_r.png')

caps[0].resize((128, 128)).save(D + 'lf_grass_top.png')
caps[1].resize((128, 128)).save(D + 'lf_grass_top_2.png')
dirts[0].resize((128, 128)).save(D + 'lf_dirt.png')
dirts[1].resize((128, 128)).save(D + 'lf_dirt_2.png')
caps[0].resize((128, 128)).save(D + 'lf_grass_top_l.png')
caps[0].resize((128, 128)).save(D + 'lf_grass_top_r.png')
print('painted tile set written to', D)
