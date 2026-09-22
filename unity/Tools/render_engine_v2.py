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

# ---------------- gameplay sprites at collider-exact positions ----------------
def sprite(path, target_h, cx, base_y, dy=0):
    s = Image.open(path).convert('RGBA')
    scale = target_h / s.size[1]
    s = s.resize((max(1, int(s.size[0] * scale)), int(target_h)), Image.LANCZOS)
    img.paste(s, (int(cx - s.size[0] / 2), int(base_y - s.size[1] + dy)), s)

sprite('art/whole_lily.png', 82, -200, 620)
cp_plats = {2100: 620, 4200: 620, 5600: 200, 6460: 200, 7600: 620}
for cp, y in cp_plats.items():
    sprite('art/art_checkpoint.png', 340, cp, y)
sprite('art/art_flaggate.png', 600, 7800, 620)
sprite('art/art_flagportal.png', 650, 7940, 620)
sprite('art/art_heart.png', 64, 3450, 240, dy=-90)

tok = Image.open('art/art_token.png').convert('RGBA').resize((28, 28), Image.LANCZOS)
for t in tokens:
    img.paste(tok, (int(t['x']) - 14, int(t['y']) - 14), tok)

img.save('art/render_depth_test_v2.png')
print('saved art/render_depth_test_v2.png', img.size)

# ---------------- numeric QC ----------------
a = np.array(img).astype(int)
pw = int((a.min(axis=2) >= 235).sum())
print(f'pure-white px: {pw} ({pw / (W * H) * 100:.4f}%)')
lum = a.mean(axis=2)
print('edge luminance L/R:', round(lum[:, 0].mean(), 1), round(lum[:, -1].mean(), 1))
seam = lum[:, 4048:4152].mean(axis=1)
print('seam-zone mean luminance:', round(seam.mean(), 1))
for x in [400, 2300, 3500, 7600]:
    px = a[635, x] if c_at(x) <= 640 else a[int(c_at(x)) + 15, x]
    print(f'ground sample x={x}: [{px[0]} {px[1]} {px[2]}] (want green/earth dominant)')
buried = 0
for t in tokens:
    surf = None
    for cx2, y2, w2, h2 in plats:
        if cx2 - w2 / 2 - 10 <= t['x'] <= cx2 + w2 / 2 + 10 and y2 >= t['y']:
            surf = y2 if surf is None else min(surf, y2)
    if surf is not None and surf - t['y'] < 40:
        buried += 1
print('buried/near-surface tokens (<40px above a surface):', buried)
