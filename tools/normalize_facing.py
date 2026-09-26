#!/usr/bin/env python3
"""FACING NORMALIZATION (BudE Sept 26: "the jump is only to the right for animations
there isnt a left animation it auto turns right even tho you go left").

The game's flip law (PlayerAnimBridge): art must face LEFT natively; moving right
mirrors it (scale.x = -facing). So every frame PNG must natively face LEFT.

Detection: head-region asymmetry (top 32% of the character bbox). The face side
carries more edge energy (eyes/nose/mouth) and typically more mass than the back of
the head. Score > +THRESH = faces right = mirror it. Validated against:
- the 3 reference arts (all read LEFT, matching the game law)
- build-120 walk frames BudE explicitly liked (read LEFT)
- lily_jump_0 from the build he complained about (reads +0.97 RIGHT - the bug)

Usage: python3 tools/normalize_facing.py [--dry-run] [--thresh 0.10]
Mirrors any Assets/Art/Frames/*.png scoring above the threshold, in place.
"""
import sys, glob, os
from PIL import Image
import numpy as np

ROOT = os.path.join(os.path.dirname(__file__), '..', 'unity', 'Assets', 'Art')
DRY = '--dry-run' in sys.argv
THRESH = 0.10
for i, a in enumerate(sys.argv):
    if a == '--thresh' and i + 1 < len(sys.argv):
        THRESH = float(sys.argv[i + 1])

def facing_score(path, head_frac=0.32):
    im = np.array(Image.open(path).convert('RGBA'))
    a = im[:, :, 3] > 40
    ys, xs = np.where(a)
    if len(ys) < 50: return None
    y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()
    hy1 = y0 + int((y1 - y0 + 1) * head_frac)
    head = im[y0:hy1 + 1, x0:x1 + 1]
    ha = head[:, :, 3] > 40
    if ha.sum() < 20: return None
    rgb = head[:, :, :3].astype(float)
    e = (np.abs(np.gradient(rgb, axis=1)).sum(axis=2)
         + np.abs(np.gradient(rgb, axis=0)).sum(axis=2)) * ha
    W = ha.shape[1]
    l, r = e[:, :W // 2].sum(), e[:, W // 2:].sum()
    ml, mr = ha[:, :W // 2].sum(), ha[:, W // 2:].sum()
    return ((r - l) / (r + l) + (mr - ml) / (mr + ml)) / 2   # + = faces RIGHT

mirrored = []
for f in sorted(glob.glob(os.path.join(ROOT, 'Frames', '*.png'))):
    s = facing_score(f)
    if s is None:
        print(f'  SKIP (too small): {os.path.basename(f)}')
        continue
    if s > THRESH:
        print(f'  MIRROR {os.path.basename(f):24s} score {s:+.3f}')
        if not DRY:
            im = Image.open(f)
            im.transpose(Image.FLIP_LEFT_RIGHT).save(f)
        mirrored.append(f)
    else:
        print(f'  ok     {os.path.basename(f):24s} score {s:+.3f}')

print(f'\n{len(mirrored)} file(s) mirrored' + (' (dry run)' if DRY else ''))
