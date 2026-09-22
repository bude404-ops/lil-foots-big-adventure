#!/usr/bin/env python3
"""Skin pass v2 for r1_depth_test — all-new ONE-painting lore build.

Doctrine (BudE, Sept 21 'lets skin it... all as one from our lore'):
  - Geometry unchanged (audited GREEN).
  - The painting is BORN from the layout: per-column vertical warp binds the
    painted terrain line onto the collider height profile.
  - Sky comes from the tall full-frame lore gen (real painted sky, 1:1 rows);
    terrain comes from the letterboxed lore strips (2x horizontal detail vs
    the old 1024 band; 2048 source columns for 8000 canvas).
  - Float-anchor doctrine: every raised plat gets a world-anchored chunk sliced
    from the warped painting at its own act position, so it reads as the lit
    front extension of the structure (rock spine / cedar) painted behind it.
  - Locked gameplay pieces composite at collider-exact positions.
  - Numeric QC before delivery.
"""
import json
import numpy as np
from PIL import Image, ImageFilter, ImageDraw

W, H = 8000, 1560

d = json.load(open('levels/map_r1_depth_test.json'))
plats, tokens = d['plats'], d['tokens']

# ---------------- sources ----------------
L1 = np.array(Image.open('art/lore_left.png').convert('RGB')).astype(np.uint8)    # 1024x1024 sky left
R1 = np.array(Image.open('art/lore_right.png').convert('RGB')).astype(np.uint8)  # sky right
L2 = np.array(Image.open('art/lore_left_v2.png').convert('RGB'))[389:634].astype(np.uint8)   # band 245
R2 = np.array(Image.open('art/lore_right_v2.png').convert('RGB'))[384:633].astype(np.uint8)  # band 249

BH_L, BH_R = L2.shape[0], R2.shape[0]
HOR_L, HOR_R = 600, 430        # painted horizon rows in the v1 frames

# ---------------- collider ground profile c(x) ----------------
ANCHORS_C = [   # (canvas_x, canvas_row)  geometry-authored
    (-400, 620), (1200, 620), (1600, 740), (1950, 620), (3580, 620),
    (4550, 620), (4675, 480), (5060, 200), (5560, 200), (6040, 200),
    (6135, 850), (6230, 200), (6460, 200), (6880, 320), (7130, 460),
    (7480, 620), (8400, 620),
]
def c_at(x):
    xs = [a[0] for a in ANCHORS_C]
    if x <= xs[0]:  return ANCHORS_C[0][1]
    if x >= xs[-1]: return ANCHORS_C[-1][1]
    for i in range(len(ANCHORS_C) - 1):
        if xs[i] <= x <= xs[i+1]:
            t = (x - xs[i]) / (xs[i+1] - xs[i])
            return ANCHORS_C[i][1] + (ANCHORS_C[i+1][1] - ANCHORS_C[i][1]) * t
    return ANCHORS_C[-1][1]

# ---------------- painted terrain line s2(x) in each strip ----------------
def sky_mask(band):
    lum = band.astype(int).mean(axis=2)
    r, g = band[:,:,0].astype(int), band[:,:,1].astype(int)
    return (lum > 168) | ((r > g + 15) & (lum > 140))

def terrain_line(band):
    h, w, _ = band.shape
    sk = sky_mask(band)
    out = np.zeros(w)
    for x in range(w):
        col = sk[:, x].astype(float)
        y = h - 1
        # top of the bottom-continuous non-sky region (allow 5% sky flecks)
        run = 0
        for yy in range(h - 1, 20, -1):
            if col[yy] < 0.5:
                run += 1
            else:
                if run >= 50:  # long ground run ends here
                    y = yy + 1
                    break
                run = 0
        out[x] = max(24, min(h - 80, y))
    # smooth (median over 25 cols)
    sm = np.copy(out)
    for i in range(w):
        lo, hi = max(0, i-12), min(w, i+13)
        sm[i] = np.median(out[lo:hi])
    return sm

S_L = terrain_line(L2)
S_R = terrain_line(R2)
print('s2 left  (min/med/max):', int(S_L.min()), int(np.median(S_L)), int(S_L.max()))
print('s2 right (min/med/max):', int(S_R.min()), int(np.median(S_R)), int(S_R.max()))

# ---------------- canvas sources per column ----------------
# halves: left covers x in [-400, 4100), right covers [4100, 8400)
# seam feather: blend across [4050, 4150]
def sources(x):
    """Return (sky_ref, band_ref, band_col, band_h, feather) for canvas column x."""
    if x < 4050:
        f = max(0.0, (x - 3950) / 200.0) if x > 3950 else 0.0
        bx = (x + 400) / 4500.0 * 1024
        return 'L', 'L', min(1023, int(bx)), BH_L, f
    elif x > 4150:
        bx = (x - 4100) / 4300.0 * 1024
        return 'R', 'R', min(1023, int(bx)), BH_R, 0.0
    else:
        f = (x - 4050) / 100.0
        bxL = (x + 400) / 4500.0 * 1024
        bxR = (x - 4100) / 4300.0 * 1024
        return 'L', ('L', 'R'), (min(1023, int(bxL)), min(1023, int(bxR))), (BH_L, BH_R), f

# ---------------- build canvas ----------------
canvas = np.zeros((H, W, 3), dtype=np.uint8)
sky_rows = np.arange(H)

for x in range(W):
    cx = x if x >= 0 else 0
    sk, bd, bcol, bh, f = sources(x)
    c = c_at(x)
    ci = int(min(H - 2, max(2, round(c))))

    # ---- sky (v1 frames, horizon-mapped, no stretch beyond ~1.5x) ----
    def sky_col(side, csrc):
        frame = L1 if side == 'L' else R1
        hor = HOR_L if side == 'L' else HOR_R
        # map canvas rows 0..ci -> v1 rows 0..hor
        m = (sky_rows[:ci] / max(ci, 1)) * hor
        return frame[m.astype(int), min(1023, int((cx + (400 if side == 'L' else -4100)) / (4500 if side == 'L' else 4300) * 1024)), :]
    if isinstance(f, float) and f > 0 and isinstance(bd, tuple):
        colsky = (sky_col('L', 0).astype(int) * (1 - f) + sky_col('R', 0).astype(int) * f).astype(np.uint8)
    else:
        colsky = sky_col(sk, 0)

    # ---- terrain (v2 strips, per-column warp) ----
    def terr_col(side, bcol_i, bh_i):
        band = L2 if side == 'L' else R2
        s = (S_L if side == 'L' else S_R)[bcol_i]
        col = band[:, bcol_i, :]
        below = H - ci
        m = s + (np.arange(below) / max(below, 1)) * (bh_i - s)
        m = np.clip(m, 0, bh_i - 1)
        return col[m.astype(int)], s
    if isinstance(bd, tuple):
        tA, sA = terr_col('L', bcol[0], bh[0])
        tB, sB = terr_col('R', bcol[1], bh[1])
        colterr = (tA.astype(int) * (1 - f) + tB.astype(int) * f).astype(np.uint8)
    else:
        colterr, _ = terr_col(bd, bcol, bh)

    col = np.zeros((H, 3), dtype=np.uint8)
    col[:ci] = colsky
    col[ci:] = colterr
    canvas[:, x, :] = col

print('warp complete')
img = Image.fromarray(canvas)

# ---------------- valley water tint (living-water zone) ----------------
a = np.array(img).astype(int)
for x in range(1150, 2050):
    c = c_at(x)
    if c > 700:
        depth = np.arange(H - int(c))
        tint = np.clip((depth - 30) / 180, 0, 0.45)[:, None]
        a[int(c):, x, :] = (a[int(c):, x, :] * (1 - tint) + np.array([24, 48, 56]) * tint)
img = Image.fromarray(a.astype(np.uint8))

# ---------------- world-anchored chunks on raised plats (float-anchor doctrine) ----------------
CEDAR_X = 3430      # world x of the painted cedar trunk in the warped canvas
RIDGE_X = 6300      # world x of ridge rock material

def add_chunk(img, cx, y, w, h, material_x, material_y):
    left = int(cx - w / 2)
    ch = int(min(h + 48, 250))
    src_x = int(min(max(material_x, 0), W - int(w) - 1))
    src = img.crop((src_x, int(material_y), src_x + int(w), int(material_y) + ch))
    if src.size[0] <= 0 or src.size[1] <= 0:
        return
    chunk = src.copy()
    cwid, chei = chunk.size
    yy = np.arange(chei)[:, None]
    xx = np.arange(cwid)[None, :]
    mask = np.clip(yy / 7, 0, 1) * np.clip(np.minimum(xx, cwid - 1 - xx) / 16, 0, 1)
    mask *= np.clip((chei - 8 - yy) / (chei * 0.45), 0, 1)
    m = Image.fromarray((mask * 255).astype(np.uint8), 'L').filter(ImageFilter.GaussianBlur(2.5))
    chunk.putalpha(m)
    sh = Image.new('RGBA', (cwid * 2, 64), (0, 0, 0, 0))
    dr = ImageDraw.Draw(sh)
    dr.ellipse([cwid // 2, 22, cwid + cwid // 2, 58], fill=(8, 12, 6, 110))
    sh = sh.filter(ImageFilter.GaussianBlur(8))
    img.paste(Image.alpha_composite(Image.new('RGBA', sh.size, (0, 0, 0, 0)), sh).convert('RGB'),
              (left - cwid // 2, int(y) + 4), sh)
    img.paste(chunk, (left, int(y)), chunk)

for px, py, pw, ph in plats:
    if ph >= 400:      # tall blocks (ridge, valley walls) keep the warped painting
        continue
    if px < -300:     # spawn meadow slab
        continue
    # material: cedar branches sample the trunk; pit float samples ridge rock;
    # everything else samples the ground at its own act x (world-anchored)
    if 3300 <= px <= 4200 and py < 520:
        mx, my = CEDAR_X, 760          # cedar trunk material
    elif 6000 <= px <= 6300:
        mx, my = RIDGE_X, 260          # ridge rock material
    else:
        mx, my = px, int(c_at(px)) + 8
    add_chunk(img, px, py, pw, ph, mx, my)

# ---------------- native world props (painted, not sprites - old object art deleted) ----------------
rng = np.random.default_rng(404)
pa = np.array(img).astype(float)

def sample(cx, cy, r=80):
    x0, x1 = max(0, int(cx) - r), min(W, int(cx) + r)
    y0, y1 = max(0, int(cy) - r), min(H, int(cy) + r)
    return pa[y0:y1, x0:x1].reshape(-1, 3).mean(axis=0)

def grass(cx, cy):
    return sample(cx, cy, 60)

def painted_shadow(cx, base_y, w, h=52):
    yy, xx = np.mgrid[0:h, 0:w * 2]
    e = (((xx - w) / w) ** 2 + ((yy - h / 2) / (h / 2)) ** 2 <= 1) * 120
    a_s = e * np.clip(np.arange(h)[:, None] / (h * 0.5), 0.35, 1)
    ys, xs = int(base_y - h * 0.35), int(cx - w)
    h2, w2 = min(H - ys, h), min(W - xs, w * 2)
    if h2 > 0 and w2 > 0:
        reg = pa[ys:ys + h2, xs:xs + w2]
        reg *= (1 - a_s[:h2, :w2, None] / 255 * 0.55)
        pa[ys:ys + h2, xs:xs + w2] = reg

def paint_rect(x0, y0, x1, y1, color, grain=14, edge=2):
    x0, y0, x1, y1 = int(x0), int(y0), int(min(x1, W)), int(min(y1, H))
    h, w = y1 - y0, x1 - x0
    if h <= 0 or w <= 0:
        return
    tex = color[None, None, :] + rng.normal(0, grain, (h, w, 3))
    tex = np.clip(tex, 0, 255)
    fade = np.ones((h, w))
    if edge:
        fade *= np.clip(np.arange(w)[None, :] / edge, 0, 1) * np.clip(np.arange(w)[None, ::-1] / edge, 0, 1)
        fade *= np.clip(np.arange(h)[:, None] / max(1, edge // 2), 0, 1)
    pa[y0:y1, x0:x1] = pa[y0:y1, x0:x1] * (1 - fade[:, :, None] * 0.92) + tex * (fade[:, :, None] * 0.92)

def _soft_blob(cx, cy, rx, ry, color, alpha):
    color = np.asarray(color, dtype=float)
    for yy in range(int(cy - ry), int(cy + ry) + 1):
        if not (0 <= yy < H):
            continue
        for xx in range(int(cx - rx), int(cx + rx) + 1):
            if not (0 <= xx < W):
                continue
            d = ((xx - cx) / max(1.0, rx)) ** 2 + ((yy - cy) / max(1.0, ry)) ** 2
            if d <= 1:
                aq = alpha * (1 - d) ** 0.6
                pa[yy, xx] = pa[yy, xx] * (1 - aq) + color * aq

def paint_bark_pole(cx, base_y, height, half_w, ground_col):
    # base value shape: warm lit left face, deep shadow right face - smooth painted bands, zero pixel noise
    core = np.clip(ground_col * np.array([0.56, 0.50, 0.46]) + np.array([-12, -4, 6]), 10, 108)
    for i, yy in enumerate(range(int(base_y - height), int(base_y))):
        t = i / max(1, height)
        hw = half_w * (1.0 + 0.6 * (1 - t) ** 7)
        x0, x1 = int(cx - hw), int(min(cx + hw, W))
        if x1 - x0 <= 0:
            continue
        xs = np.arange(x1 - x0)
        fr = np.clip((xs - hw) / (2 * hw) + 0.5, 0, 1)
        band = 1.34 - 0.78 * fr ** 1.2
        tone = 1.0 + 0.05 * np.sin(yy / 9.0)
        pa[yy, x0:x1] = np.clip(core[None, :] * (band * tone)[:, None], 0, 255)
    # chunky painted bark strokes - long soft vertical strips
    for _ in range(int(height / 8)):
        sy = base_y - rng.uniform(6, max(10, height - 4))
        sl = rng.uniform(24, 70)
        sx = cx + rng.uniform(-half_w * 0.7, half_w * 0.7)
        sw = rng.uniform(2.5, 6.5)
        if rng.random() < 0.5:
            col = np.clip(ground_col * np.array([0.68, 0.60, 0.55]) + np.array([10, 2, 2]), 16, 130)
        else:
            col = np.clip(ground_col * np.array([0.40, 0.36, 0.34]), 6, 84)
        _soft_blob(sx, sy, sw, sl / 2, col, rng.uniform(0.22, 0.45))
    # warm rim light on the lit left edge
    rim = np.clip(ground_col * np.array([1.18, 1.08, 0.82]), 60, 195)
    for yy in range(int(base_y - height), int(base_y)):
        t = (base_y - yy) / max(1, height)
        hw = half_w * (1.0 + 0.6 * (1 - (1 - t)) ** 0) if False else half_w
        xx = int(cx - hw * 0.78)
        if 0 <= yy < H and 0 <= xx < W - 3:
            pa[yy, xx:xx + 3] = pa[yy, xx:xx + 3] * 0.45 + rim * 0.55
    # moss collar - bold painted blobs
    moss = np.clip(ground_col * np.array([0.80, 1.15, 0.70]), 30, 150)
    for _ in range(10):
        _soft_blob(cx + rng.uniform(-half_w, half_w), base_y - rng.uniform(4, 22), rng.uniform(4, 9), rng.uniform(3, 7), moss, rng.uniform(0.5, 0.8))

def paint_base_rocks(cx, base_y, ground_col, spread=40, n=5):
    stone = np.clip(ground_col * np.array([0.66, 0.74, 0.68]) - 22, 12, 92)
    for _ in range(n):
        rxc = cx + rng.normal(0, spread)
        rx, ry = rng.uniform(9, 20), rng.uniform(6, 12)
        _soft_blob(rxc, base_y - ry * 0.2, rx, ry, stone, 0.95)
        _soft_blob(rxc - rx * 0.35, base_y - ry * 0.55, rx * 0.42, ry * 0.38, np.clip(stone * 1.45 + 10, 20, 145), 0.5)

def paint_flag(cx, top_y, ground_col):
    gw, gh = 118, 74
    x0, y0 = int(cx + 10), int(top_y)
    xs, ys = np.meshgrid(np.arange(gw), np.arange(gh))
    wave = np.sin(xs / 12.0 + ys / 9.0) * 3
    green = np.clip(ground_col * np.array([0.55, 0.95, 0.55]), 20, 120)
    tex = green[None, None, :] * (1 - 0.22 * (ys / gh))[:, :, None] + rng.normal(0, 6, (gh, gw, 3))
    fade = np.clip(np.minimum(xs, gw - 1 - xs) / 3, 0, 1) * np.clip((gh - ys) / 6, 0, 1) * np.clip((ys + 2) / 4, 0, 1)
    yy = y0 + np.clip(ys + wave, 0, 200).astype(int)
    # vectorized paste
    for c in range(gw):
        for r in range(gh):
            if fade[r, c] > 0.15:
                yq = int(y0 + r + wave[r, c])
                pa[yq, x0 + c] = pa[yq, x0 + c] * (1 - fade[r, c] * 0.95) + np.clip(tex[r, c], 0, 255) * (fade[r, c] * 0.95)
    # gold footprint emblem
    ex, ey = x0 + gw // 2, y0 + gh // 2
    gold = np.array([212, 168, 74])
    for r in range(-20, 21):
        for c in range(-13, 14):
            if (r * r) / 400 + (c * c) / 169 <= 1:
                pa[ey + r, ex + c] = pa[ey + r, ex + c] * 0.15 + gold * 0.85 + rng.normal(0, 8, 3)
    return gw

def paint_gate(cx, base_y):
    g = grass(cx, base_y + 20)
    painted_shadow(cx, base_y + 6, 64, h=56)
    paint_bark_pole(cx, base_y, 470, 13, g)
    paint_base_rocks(cx, base_y - 6, g, spread=34, n=5)
    # topper knob + pennant line
    top = base_y - 470
    pa[int(top) - 8:int(top), int(cx) - 15:int(cx) + 15] = pa[int(top) - 8:int(top), int(cx) - 15:int(cx) + 15] * 0.2 + np.array([196, 152, 62]) * 0.8
    paint_flag(cx, top + 22, g)

def paint_portal(cx, base_y):
    g = grass(cx, base_y + 30)
    stone = np.clip(g * np.array([0.70, 0.80, 0.73]) - 28, 16, 104)
    painted_shadow(cx, base_y + 6, 92, h=58)
    ph = 520
    moss = np.clip(g * np.array([0.82, 1.20, 0.72]), 30, 160)
    for side in (-1, 1):
        px = cx + side * 52
        for yy in range(int(base_y - ph), int(base_y)):
            t = (base_y - yy) / ph
            hw = 26 * (1 + 0.38 * (1 - t) ** 3)
            hw = hw if t < 0.93 else hw * 0.82
            xs0, xs1 = int(px - hw), int(min(px + hw, W))
            if xs1 - xs0 <= 0:
                continue
            xs = np.arange(xs1 - xs0)
            fr = np.clip((xs - hw) / (2 * hw) + 0.5, 0, 1)
            light = 1.30 - 0.72 * fr ** 1.15
            pa[yy, xs0:xs1] = np.clip(stone[None, :] * (1 - 0.16 * t) * light[:, None], 0, 255)
        # strata: chunky soft horizontal stroke bands
        for _ in range(9):
            sy = base_y - rng.uniform(24, ph - 16)
            _soft_blob(px + rng.uniform(-10, 10), sy, rng.uniform(16, 26), rng.uniform(2.5, 5), stone * rng.uniform(0.55, 0.8), rng.uniform(0.3, 0.5))
        # bold moss patches on the shadow side
        for _ in range(6):
            _soft_blob(px + rng.uniform(8, 22) * side, base_y - rng.uniform(40, ph - 60), rng.uniform(6, 12), rng.uniform(4, 10), moss, rng.uniform(0.4, 0.65))
        # lichen: few soft flecks on the lit side
        lich = np.clip(stone * 1.5 + np.array([18, 14, -6]), 40, 210)
        for _ in range(9):
            _soft_blob(px - rng.uniform(6, 18), base_y - rng.uniform(30, ph - 30), rng.uniform(2, 4), rng.uniform(1.5, 3), lich, 0.55)
        paint_base_rocks(px, base_y - 2, g, spread=30, n=3)
        # moss cap - painted blobs across the top
        for _ in range(7):
            _soft_blob(px + rng.uniform(-20, 20), base_y - ph + rng.uniform(0, 30), rng.uniform(6, 12), rng.uniform(3, 7), moss, rng.uniform(0.5, 0.8))
    # lintel
    ly = base_y - ph
    for yy in range(int(ly - 30), int(ly)):
        xs0, xs1 = int(cx - 90), int(min(cx + 90, W))
        if xs1 - xs0 <= 0:
            continue
        xs = np.arange(xs1 - xs0)
        light = 1 - 0.35 * (xs / max(1, xs1 - xs0))
        pa[yy, xs0:xs1] = np.clip(stone[None, :] * light[:, None], 0, 255)
    for _ in range(6):
        _soft_blob(cx + rng.uniform(-80, 80), ly - rng.uniform(4, 24), rng.uniform(4, 9), rng.uniform(3, 8), moss, rng.uniform(0.4, 0.7))
    # emerald veil + ground light pool (smooth washes)
    em = np.array([52, 196, 134])
    xs = np.arange(int(cx - 30), int(min(cx + 30, W)))
    for yy in range(int(base_y - ph + 44), int(base_y - 12)):
        t = (base_y - yy) / ph
        alpha = 0.30 * np.exp(-((yy - (base_y - ph * 0.42)) / (ph * 0.38)) ** 2) * (1 - 0.25 * t)
        shimmer = 0.85 + 0.15 * np.sin(xs / 8.0 + yy / 16.0) * np.sin(yy / 23.0)
        for xi, xq in enumerate(xs):
            aq = alpha * shimmer[xi]
            pa[yy, xq] = pa[yy, xq] * (1 - aq) + em * aq
    for r in range(0, 26):
        yq = int(base_y - 4 + r)
        if not (0 <= yq < H):
            continue
        for xx in range(int(cx - 44), int(min(cx + 44, W))):
            aq = 0.10 * (1 - r / 26) * np.cos((xx - cx) / 44.0 * 1.57)
            if aq > 0:
                pa[yq, xx] = pa[yq, xx] * (1 - aq) + em * aq

def paint_totem(cx, base_y):
    g = grass(cx, base_y + 14)
    painted_shadow(cx, base_y + 5, 34, h=44)
    th = int(min(340, max(140, base_y - 50)))
    paint_bark_pole(cx, base_y, th, 10, g)
    paint_base_rocks(cx, base_y - 4, g, spread=26, n=4)
    # carved ring grooves - two chunky painted bands
    for frac in (0.38, 0.62):
        gy = base_y - th * frac
        _soft_blob(cx, gy, 11, 4.5, np.clip(g * 0.28, 4, 60), 0.85)
        _soft_blob(cx, gy + 6, 10, 3, np.clip(g * np.array([0.85, 0.75, 0.65]), 20, 130), 0.45)
    # moss cap - bold blobs
    moss = np.clip(g * np.array([0.85, 1.25, 0.75]), 30, 168)
    for _ in range(12):
        _soft_blob(cx + rng.uniform(-12, 12), base_y - th + rng.uniform(0, 26), rng.uniform(5, 11), rng.uniform(3, 8), moss, rng.uniform(0.5, 0.85))
    # glowing footprint emblem - smooth gradient + halo
    ey = int(base_y - max(110, th * 0.55))
    gold = np.array([228, 188, 96])
    for r in range(-26, 27):
        for c in range(-16, 17):
            yy, xx = ey + r, int(cx) + c
            if not (0 <= yy < H and 0 <= xx < W):
                continue
            d = (r * r) / 400 + (c * c) / 156
            if d <= 1:
                wgt = 0.78 * (1 - 0.35 * d)
                pa[yy, xx] = pa[yy, xx] * (1 - wgt) + gold * wgt
            elif d <= 3.4:
                halo = 0.20 * (3.4 - d) / 2.4
                pa[yy, xx] = pa[yy, xx] * (1 - halo) + gold * halo
    # grass tufts - smooth curved strokes
    tuft = np.clip(g * np.array([0.9, 1.3, 0.8]), 40, 185)
    for _ in range(7):
        tx = cx + rng.normal(0, 20)
        for k in range(int(rng.uniform(9, 18))):
            xx = int(tx + np.sin(k / 3.5) * 2.5)
            yy = int(base_y - k)
            if 0 <= yy < H and 0 <= xx < W:
                pa[yy, xx] = pa[yy, xx] * 0.3 + tuft * 0.7

cp_plats = {2100: 620, 4200: 620, 5600: 200, 6460: 200, 7600: 620}
paint_gate(7800, 620)
paint_portal(7940, 620)
for cpx, cpy in cp_plats.items():
    paint_totem(cpx, cpy)
img = Image.fromarray(np.clip(pa, 0, 255).astype(np.uint8))
img = img.filter(ImageFilter.GaussianBlur(0.4))

# ---------------- living sprites only: character, tokens, heart ----------------
def sprite(path, target_h, cx, base_y, dy=0):
    s = Image.open(path).convert('RGBA')
    scale = target_h / s.size[1]
    s = s.resize((max(1, int(s.size[0] * scale)), int(target_h)), Image.LANCZOS)
    img.paste(s, (int(cx - s.size[0] / 2), int(base_y - s.size[1] + dy)), s)

sprite('art/whole_lily.png', 82, -200, 620)
sprite('art/art_heart.png', 64, 3450, 240, dy=-90)

tok = Image.open('art/art_token.png').convert('RGBA').resize((28, 28), Image.LANCZOS)
for t in tokens:
    img.paste(tok, (int(t['x']) - 14, int(t['y']) - 14), tok)

img.save('art/render_depth_test_v6.png')
print('saved art/render_depth_test_v4.png', img.size)
