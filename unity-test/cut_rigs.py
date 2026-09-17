"""Pixel-accurate rig cutter v3: fills are OUTLINE-TRACED (never cross the art's
own dark outlines), L/R split by simultaneous 2-source BFS, outline ring
re-attached to each foot by 1px dilation."""
from PIL import Image
import numpy as np
from collections import deque
from scipy import ndimage as _nd
import json, os

NB = [(1,0),(-1,0),(0,1),(0,-1),(1,1),(1,-1),(-1,1),(-1,-1)]

def cut_character(path, name, outdir):
    im = Image.open(path).convert("RGBA")
    a = np.asarray(im).astype(int)
    h, w, _ = a.shape
    alpha = a[..., 3] > 8
    rgb = a[..., :3]
    mx = rgb.max(axis=2); mn = rgb.min(axis=2)

    # ---- seeds: low-sat light pixels in bottom 14% ----
    y0 = int(h * 0.86)
    inband = (np.arange(h)[:, None] >= y0)
    footish = alpha & inband & (mn > 130) & ((mx - mn) < 60) & (mx > 150)
    ys, xs = np.where(footish)
    assert len(ys) > 100, f"{name}: no foot seeds"
    cx = (xs.min() + xs.max()) // 2
    L = [(y, x) for y, x in zip(ys, xs) if x < cx]
    R = [(y, x) for y, x in zip(ys, xs) if x >= cx]
    def pick(cluster):
        Y = np.array([c[0] for c in cluster]); X = np.array([c[1] for c in cluster])
        med_c = np.median(rgb[Y, X], axis=0)
        y_lo, y_hi = np.percentile(Y, 35), np.percentile(Y, 65)
        best, bd = None, 1e9
        for y, x in cluster:
            if y_lo <= y <= y_hi:
                d = np.linalg.norm(rgb[y, x] - med_c)
                if d < bd: bd, best = d, (int(y), int(x))
        return best, med_c
    (sl, mlc), (sr, mrc) = pick(L), pick(R)

    # ---- simultaneous 2-source BFS, STOPS at outlines (no darks) ----
    ymin = int(h * 0.84)
    lab = np.zeros((h, w), dtype=np.int8)
    def ok(y, x, m):
        if not alpha[y, x] or y < ymin: return False
        c = rgb[y, x]
        if c.max() < 95: return False          # outline = boundary
        return np.linalg.norm(c - m) < 42
    q = deque()
    if ok(*sl, mlc): lab[sl] = 1; q.append((*sl, 1))
    if ok(*sr, mrc): lab[sr] = 2; q.append((*sr, 2))
    while q:
        y, x, l = q.popleft()
        for dy, dx in NB:
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and lab[ny, nx] == 0:
                m = mlc if l == 1 else mrc
                if ok(ny, nx, m):
                    lab[ny, nx] = l
                    q.append((ny, nx, l))
    fcore = lab > 0
    assert fcore.sum() > 300, f"{name}: fill too small ({fcore.sum()})"

    # ---- re-attach outline ring: 1px dilation into dark/close pixels ----
    def dilate1(m, allow):
        d = m.copy()
        for dy, dx in NB:
            d |= np.roll(m, (dy, dx), axis=(0, 1)) & allow
        return d
    allow = alpha & ((rgb.max(axis=2) < 120) | (np.linalg.norm(rgb - (mlc if True else mrc), axis=2) < 55))
    # per-foot allow color
    fl_c = dilate1(fcore & (lab == 1), alpha & ((rgb.max(axis=2) < 120) | (np.linalg.norm(rgb - mlc, axis=2) < 55)))
    fr_c = dilate1(fcore & (lab == 2), alpha & ((rgb.max(axis=2) < 120) | (np.linalg.norm(rgb - mrc, axis=2) < 55)))
    fl = fl_c & ~fr_c
    fr = fr_c & ~fl_c
    tan  = (rgb[..., 0] - rgb[..., 2] > 35) & (rgb[..., 0] > 140) & (rgb[..., 2] < 170)
    teal = (rgb[..., 1] - rgb[..., 0] > 60) & (rgb[..., 1] > 120) & (rgb[..., 2] > rgb[..., 0])
    low_sat = (rgb.max(axis=2) - rgb.min(axis=2)) < 32
    # ---- outline shell: dark pixels within 3px of a foot fill, EXCLUDING
    # pixels near patch colors (ovals/stipple) so the shell never eats a patch.
    # Shell is added to the feet AND kept in the body (duplicated boundary) so
    # both pieces carry a closed cartoon outline.
    dark = rgb.max(axis=2) < 120
    # patch colors for shell exclusion: tan/teal ovals + mid-value grey stipple;
    # very dark pixels (real outlines) must NOT be excluded
    patchish = tan | teal | (low_sat & (rgb.min(axis=2) > 60))
    patchnear = _nd.binary_dilation(patchish, structure=np.ones((5, 5), dtype=bool))
    from scipy import ndimage as _nd2
    def shell(m):
        grown = m.copy()
        for _ in range(3):
            grown = _nd.binary_dilation(grown) & alpha
        return dark & grown & ~patchnear & ~patchish
    shl, shr = shell(fl), shell(fr)
    fl_core, fr_core = fl, fr          # fill only, no shell
    fl = fl | shl
    fr = fr | shr
    # main-component per foot: drops stipple grains absorbed by the fill
    def maincomp(m):
        lm, nm = _nd.label(m)
        if nm <= 1: return m
        sz = _nd.sum(m, lm, range(1, nm + 1))
        return lm == (int(np.argmax(sz)) + 1)
    fl = maincomp(fl); fr = maincomp(fr)
    feet = fl | fr

    # ---- body: alpha minus feet minus below-feet minus patch colors ----
    clear = np.zeros((h, w), dtype=bool)
    fb = np.zeros(w, dtype=int) - 1
    for x in range(w):
        col = np.where(feet[:, x])[0]
        if len(col): fb[x] = col.max()
    for x in range(w):
        if fb[x] >= 0: clear[fb[x] + 3:, x] = True
        else: clear[int(h * 0.90):, x] = True
    # stray/patch zone anchored at the measured foot line (protects hands)
    foot_top = int(np.where((fl | fr).any(axis=1))[0].min())
    below = np.arange(h)[:, None] >= (foot_top - 8)
    stray = below & (tan | teal) & ~feet
    # buddy's grainy ground ring: low-sat pixels hugging the feet (within 12px)
    near_feet = _nd.binary_dilation(feet, structure=np.ones((25, 25), dtype=bool))
    stipple = near_feet & _nd.binary_dilation(feet, structure=np.ones((25, 25), dtype=bool)) & low_sat & ~feet
    # body excludes only the foot FILLS; it keeps the shared outline shell
    # (duplicated boundary) so legs keep their outline when feet swing
    body = alpha & ~fl_core & ~fr_core & ~clear & ~stray & ~stipple
    # keep only the main connected blob (drops buddy's grainy ground speckles
    # and any stray disconnected fragments)
    lbl2, n2 = _nd.label(body)
    if n2 > 1:
        sizes = _nd.sum(body, lbl2, range(1, n2 + 1))
        body = lbl2 == (int(np.argmax(sizes)) + 1)

    # ---- export ----
    os.makedirs(outdir, exist_ok=True)
    def save(m, fn):
        out = a.copy()
        out[..., 3] = np.where(m, a[..., 3], 0).astype(np.uint8)
        sub = out.astype(np.uint8)
        ys2, xs2 = np.where(m)
        y0b, y1b, x0b, x1b = ys2.min(), ys2.max() + 1, xs2.min(), xs2.max() + 1
        Image.fromarray(sub[y0b:y1b, x0b:x1b]).save(f"{outdir}/{fn}")
        return [int(x0b), int(y0b), int(x1b), int(y1b)]
    bb = save(body, f"rig_{name}_body.png")
    bl = save(fl, f"rig_{name}_footL.png")
    br = save(fr, f"rig_{name}_footR.png")

    gy = int(np.where(feet.any(axis=1))[0].max())
    json.dump({"name": name, "w": w, "h": h, "ground_img_y": gy,
               "parts": {"body": {"rect": bb}, "footL": {"rect": bl}, "footR": {"rect": br}}},
              open(f"{outdir}/{name}-rig.json", "w"), indent=1)

    # composite preview on checker to spot seams
    canvas = np.zeros_like(a)
    for m, tag in ((body, None), (fl, None), (fr, None)):
        p = a.copy(); p[..., 3] = np.where(m, a[..., 3], 0)
        al = p[..., 3:4] / 255.0
        canvas = (p * al + canvas * (1 - al)).astype(int)
        if tag:  # tint edges for identification
            edge = m & ~np.roll(m, 1, axis=1)
            canvas[edge] = list(tag) + [255]
    Image.fromarray(canvas.astype(np.uint8)).save(f"{outdir}/{name}-assembled.png")
    print(f"{name}: feetL={int(fl.sum())} feetR={int(fr.sum())} body={int(body.sum())} ground_y={gy}")

for nm, p in [("lily", "sprites/lily-sprite.png"),
              ("buddy", "sprites/buddy-sprite.png"),
              ("emma", "sprites/pink-sprite.png")]:
    cut_character(p, nm, "unity-test/rig-v2")
