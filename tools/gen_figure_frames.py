#!/usr/bin/env python3
"""gen_figure_frames.py - [FIGURE ANIMATION Sept 27 PM, BudE: "animations aren't like they
should be with more figures to make it look like they are animated"] + [SAME-CHARACTER LAW
Sept 27: "it isn't even the same character while moving around"]

Generates multi-figure animation frame sets (walk 8 / jump 4 / idle 4) by POSE-WARPING the
character's ACTUAL upright art (whole_*.png). Every frame is literally his character's own
pixels, smoothly warped into a distinct pose - identity is guaranteed by construction, no
AI redraw, no cut lines (all warps use gaussian falloff weights, never hard region splits).
Frames land on the character's exact canvas so scale/baseline match the rig sprite.
"""
import numpy as np
from PIL import Image
import os

SRC = 'unity/Assets/Art'
OUT = 'unity/Assets/Art/Frames'

def vweight(y, y0, y1, s):
    c, hw = (y0 + y1) / 2, (y1 - y0) / 2
    d = (y - c) / max(1.0, hw)
    return np.exp(-(d * d) / (s * s))

def rot(px, py, ang, pivx, pivy):
    a = np.deg2rad(ang)
    dx, dy = px - pivx, py - pivy
    return (pivx + dx * np.cos(a) + dy * np.sin(a),
            pivy - dx * np.sin(a) + dy * np.cos(a))

def _bilinear(ch, sx, sy, W, H):
    x0 = np.clip(np.floor(sx).astype(np.int32), 0, W - 2)
    y0 = np.clip(np.floor(sy).astype(np.int32), 0, H - 2)
    fx, fy = sx - x0, sy - y0
    return (ch[y0, x0] * (1 - fx) * (1 - fy) + ch[y0, x0 + 1] * fx * (1 - fy) +
            ch[y0 + 1, x0] * (1 - fx) * fy + ch[y0 + 1, x0 + 1] * fx * fy)

def warp(ref, H, W, cx, hip, legA_deg, legB_deg, armA_deg, lean_deg, vscale, chest=1.0, tuck=0.0, headbob=0):
    a = np.asarray(ref.convert('RGBA')).astype(np.float32)
    ys = np.linspace(0, H - 1, H)[:, None] * np.ones((1, W))
    xs = np.ones((H, 1)) * np.linspace(0, W - 1, W)[None, :]
    srcx, srcy = xs.copy(), ys.copy()

    # --- leg scissor: two gaussian-weighted rotations about the hip pivot, split at cx ---
    wl = vweight(ys, hip, H, 0.85) * np.exp(-((xs - (cx - W * 0.06)) ** 2) / (2 * (W * 0.13) ** 2))
    wr = vweight(ys, hip, H, 0.85) * np.exp(-((xs - (cx + W * 0.06)) ** 2) / (2 * (W * 0.13) ** 2))
    wl, wr = wl / max(wl.max(), 1e-6), wr / max(wr.max(), 1e-6)
    for wmat, ang in ((wl, legA_deg), (wr, legB_deg)):
        if abs(ang) < 0.01: continue
        rx, ry = rot(xs, ys, ang * wmat, cx, hip)
        srcx = srcx * (1 - wmat) + rx * wmat
        srcy = srcy * (1 - wmat) + ry * wmat

    # --- arm sway: outer band of upper body counter-rotates about the shoulder line ---
    wband = vweight(ys, int(H * 0.18), int(H * 0.62), 0.55) * \
            np.exp(-((np.abs(xs - cx) - W * 0.24) ** 2) / (2 * (W * 0.10) ** 2))
    wband = wband / max(wband.max(), 1e-6)
    if abs(armA_deg) >= 0.01:
        rx, ry = rot(xs, ys, armA_deg * wband, cx, int(H * 0.35))
        srcx = srcx * (1 - wband) + rx * wband
        srcy = srcy * (1 - wband) + ry * wband

    # --- whole-body lean about the feet + vertical bob ---
    wbody = vweight(ys, 0, H, 1.6)
    if abs(lean_deg) >= 0.01:
        rx, ry = rot(xs, ys, lean_deg * wbody, cx, H * 0.97)
        srcx = srcx * (1 - wbody) + rx * wbody
        srcy = srcy * (1 - wbody) + ry * wbody
    cy = (H - 1) / 2
    srcy = (srcy - cy) / vscale + cy
    if tuck > 0:   # apex knee tuck: compress lower body toward hip
        wt = vweight(ys, hip, H, 0.9)
        srcy = srcy * (1 - wt) + (hip + (srcy - hip) * (1 - tuck)) * wt

    # --- breath: chest expands outward from the sternum with a tiny head bob ---
    if abs(chest - 1) >= 0.001:
        wc = vweight(ys, int(H * 0.22), int(H * 0.58), 0.38) * \
             np.exp(-((xs - cx) ** 2) / (2 * (W * 0.30) ** 2))
        wc = wc / max(wc.max(), 1e-6) * (chest - 1)
        srcx = srcx - (xs - cx) * wc
        srcy = srcy - (ys - int(H * 0.40)) * wc * 0.4
    if headbob:
        wh = vweight(ys, 0, int(H * 0.22), 0.6)
        srcy = srcy + headbob * wh * 0.9

    sx = np.clip(srcx, 0, W - 1).astype(np.float32)
    sy = np.clip(srcy, 0, H - 1).astype(np.float32)
    out = np.empty_like(a)
    for c in range(4):
        out[:, :, c] = _bilinear(a[:, :, c], sx, sy, W, H)
    return out

def gen_walk(img, H, W, cx):
    hip = int(H * 0.60)
    frames = []
    for i in range(8):
        ph = 2 * np.pi * i / 8
        legA = 11 * np.sin(ph)
        lean = 3.0 + 2.0 * np.sin(ph - 0.6)
        bob = 1 + 0.025 * np.cos(2 * ph)
        armA = -7 * np.sin(ph)
        frames.append(warp(img, H, W, cx, hip,
                           legA_deg=legA, legB_deg=-legA, armA_deg=armA,
                           lean_deg=lean, vscale=bob))
    return frames

def gen_jump(img, H, W, cx):
    hip = int(H * 0.60)
    return [
        warp(img, H, W, cx, hip, legA_deg=-3, legB_deg=3, armA_deg=-4, lean_deg=-6, vscale=0.93),
        warp(img, H, W, cx, hip, legA_deg=13, legB_deg=-10, armA_deg=10, lean_deg=7, vscale=1.06),
        warp(img, H, W, cx, hip, legA_deg=8, legB_deg=-6, armA_deg=6, lean_deg=3, vscale=1.0, tuck=0.10),
        warp(img, H, W, cx, hip, legA_deg=-8, legB_deg=8, armA_deg=-12, lean_deg=-3, vscale=1.04),
    ]

def gen_idle(img, H, W, cx):
    frames = []
    for ch, hb in [(1.000, 0), (1.018, 1), (1.032, 2), (1.018, 1)]:
        frames.append(warp(img, H, W, cx, int(H * 0.60), legA_deg=0, legB_deg=0,
                           armA_deg=0, lean_deg=0, vscale=1.0, chest=ch, headbob=hb))
    return frames

def main():
    os.makedirs(OUT, exist_ok=True)
    names = {'lily': 'whole_lily.png', 'buddy': 'whole_buddy.png', 'emma': 'whole_emma.png'}
    for char, fname in names.items():
        path = f'{SRC}/{fname}'
        if not os.path.exists(path):
            print('missing', path); continue
        img = Image.open(path)
        W, H = img.size
        alpha = np.asarray(img.convert('RGBA'))[:, :, 3]
        ys, xs = np.where(alpha > 10)
        cx = (xs.min() + xs.max()) // 2
        sets = {'walk': gen_walk(img, H, W, cx),
                'jump': gen_jump(img, H, W, cx),
                'idle': gen_idle(img, H, W, cx)}
        for anim, frames in sets.items():
            for i, fr in enumerate(frames):
                Image.fromarray(fr.astype(np.uint8), 'RGBA').save(f'{OUT}/{char}_{anim}_{i}.png')
            arrs = [np.asarray(Image.open(f'{OUT}/{char}_{anim}_{i}.png')) for i in range(len(frames))]
            base = arrs[0].astype(np.int32)
            diffs = [np.abs(arrs[i].astype(np.int32) - base).mean() for i in range(1, len(arrs))]
            print(f'{char}/{anim}: {len(frames)} frames, mean L1 vs frame0 =', [round(d, 2) for d in diffs])

if __name__ == '__main__':
    main()
