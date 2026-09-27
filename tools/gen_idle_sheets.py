#!/usr/bin/env python3
"""CHARACTER-SPECIFIC IDLE SHEETS (BudE Sept 26: "the idle needs work too and
should be character specific for each character").

Doctrine: the approved art is NEVER cut or redrawn - each idle panel is the whole
original art with a character-specific breathing motion (vertical stretch about
the feet pivot, tiny tilt, gentle bob), rendered on the exact frame canvas the
walk/jump sets use (Lily 548x920, Buddy 551x943, Emma 511x921) so FrameAnimator
swaps them with zero scaling seams.

Per-character personality (played by FrameAnimator ping-pong 0,1,2,3,2,1...):
  Lily  - graceful slow breath, gentle head sway  (idleFps 1.8)
  Buddy - energetic squash/bounce overshoot       (idleFps 3.0)
  Emma  - calm deep breath, subtle rise + drift   (idleFps 1.5)
"""
import math
import os
from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), '..', 'unity', 'Assets', 'Art')

IDLE = {
    'lily': [
        (1.000, 0.0, 0, 0),    # rest
        (1.006, 0.6, 0, 0),    # mid inhale, slight tilt
        (1.012, 1.0, 0, 0),    # full inhale, gentle sway
        (1.004, -0.4, 0, 0),   # exhale, head settles the other way
    ],
    'buddy': [
        (1.000, 0.0, 0, 0),    # rest
        (0.995, 0.0, 0, 2),    # anticipation squash (press down)
        (1.018, 0.0, 0, -4),   # bounce lift (whole body rises)
        (1.004, -1.2, 0, 0),   # overshoot sway settling
    ],
    'emma': [
        (1.000, 0.0, 0, 0),    # rest
        (1.010, 0.0, 0, -1),   # deep inhale, body rises
        (1.022, 0.0, 0, -3),   # full deep breath, calm lift
        (1.010, 0.6, 0, -1),   # exhale drift
    ],
}


def panel(im, sy, rot, dx, dy):
    """Whole-art transform: vertical scale about the bottom-center pivot (feet stay
    planted), a tiny tilt about the same pivot, then a whole-body bob. Output canvas
    is identical to the input so frames swap 1:1 with the walk/jump sets."""
    w, h = im.size
    cx, py = w / 2.0, float(h)          # pivot: bottom center (feet baseline)
    th = math.radians(rot)
    cos, sin = math.cos(th), math.sin(th)
    # inverse map (output -> input):
    #   in = pivot + R(-theta) * diag(1, 1/sy) * (out - pivot - d)
    # PIL AFFINE tuple (a b c d e f): in = (a*xo + b*yo + c, d*xo + e*yo + f)
    a = cos
    b = sin / sy
    d = -sin
    e = cos / sy
    c = cx - cos * (cx + dx) - b * (py + dy)
    f = py - d * (cx + dx) - e * (py + dy)
    return im.transform((w, h), Image.AFFINE, (a, b, c, d, e, f),
                        resample=Image.BICUBIC, fillcolor=(0, 0, 0, 0))


for char, params in IDLE.items():
    src = os.path.join(ROOT, f'whole_{char}.png')
    im = Image.open(src).convert('RGBA')
    for i, (sy, rot, dx, dy) in enumerate(params):
        if sy == 1.0 and rot == 0.0 and dx == 0 and dy == 0:
            out = im    # rest panel ships his EXACT approved art, zero resampling
        else:
            out = panel(im, sy, rot, dx, dy)
        path = os.path.join(ROOT, 'Frames', f'{char}_idle_{i}.png')
        out.save(path)
        print(f'{char}_idle_{i}.png  sy={sy} rot={rot:+} dx={dx} dy={dy}')
print('done')
