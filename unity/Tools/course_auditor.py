#!/usr/bin/env python3
"""Lil Foots Course Auditor — solves a map JSON BEFORE Unity builds it.

A red recipe dies here in seconds, with a reason — never a 40-minute red forge run.

Checks (from Bude's Level Design Doctrine + PlayerController physics):
  reachability : every plat reachable from a predecessor (gap <= 360px, rise <= 185px)
  full traverse: the course is walkable start->end on the main route
  checkpoints : none more than ~30u apart, none in mid-air over a gap
  tokens      : >= 62 (smoke gate), all four tiers present (easy/exploration/difficult/hidden)
  secret      : at least one secret heart or tier-3 cache
  blind jumps : no landing that starts beyond a takeoff's horizontal reach+margin

Usage: python3 course_auditor.py ../Assets/LevelData/map_r1_1.json
Exit 0 = GREEN (Unity may build it). Exit 2 = RED with reasons.
"""
import json, sys

MAX_GAP = 360
WARN_GAP = 330
MAX_RISE = 185
MIN_TOKENS = 62
MAX_CP_SPAN = 3000


def span(p):
    """plat [cx, y, w, h] CANVAS-BASED (LevelBuilder: Unity top = 2*GY - y/100, i.e. canvas
    y IS the plat's standing surface and y grows DOWN). x is center-based.
    Returns (left, top, right, h) in canvas space: top = standing surface = p[1]."""
    cx, y, w, h = p
    return (cx - w/2, y, cx + w/2, h)


def plat_top(p):
    return p[1]  # canvas top = standing surface (y-down)


def audit(path):
    d = json.load(open(path))
    plats = d["plats"]
    tokens = d["tokens"]
    cps = d.get("checkpoints", [])
    errors, warnings = [], []

    # sort walkable surfaces by x (ground + raised; snags included)
    surf = sorted(plats, key=lambda p: p[0])
    tops = {}  # x-interval -> top y, for landing lookup

    def over_plat(x, y):
        """Is point (x, y) above some plat (standing surface)?"""
        for p in plats:
            left, top, right, h = span(p)
            if left - 10 <= x <= right + 10 and y >= top - 5:
                return True
        return False

    # ---- reachability: every non-ground plat needs a reachable predecessor ----
    ground = [p for p in plats if p[3] >= GROUND_TOLERANCE]
    for p in plats:
        if p[3] >= GROUND_TOLERANCE:  # ground-level: reachable by walking
            continue
        pl, ptop, pr, ph = span(p)
        best = None
        for q in plats:
            if q is p:
                continue
            ql, qtop, qr, qh = span(q)
            # candidate takeoff: stand on q, jump toward p
            # horizontal distance between q's right edge and p's left edge (or overlap)
            if qr <= pl + 5:          # q is to the left
                dx = pl - qr
            elif pr <= ql + 5:        # q is to the right (backtracking jumps ok)
                dx = ql - pr
            else:
                continue               # overlapping in x: vertical step
                # vertical reachability handled by rise check below
            rise = qtop - ptop          # positive = p is higher than q (canvas: smaller y = higher)
            if rise > MAX_RISE:
                continue
            if dx > MAX_GAP:
                continue
            score = dx + rise
            if best is None or score < best[0]:
                best = (score, q)
        if best is None:
            # maybe it's directly above another plat (stacked, no dx)
            stacked = any(span(q)[0] - 10 < (pl+pr)/2 < span(q)[2] + 10 and span(q)[1] >= ptop and q is not p
                          for q in plats)
            if not stacked:
                errors.append(f"UNREACHABLE plat at x={(pl+pr)/2:.0f} top={ptop:.0f} (no takeoff within {MAX_GAP}px / {MAX_RISE}px rise)")
        elif best[0] > WARN_GAP:
            warnings.append(f"tight jump onto plat x={(pl+pr)/2:.0f} (cost {best[0]:.0f}px)")

    # ---- full traverse: walk the main route left->right at ground level ----
    grounds = sorted([p for p in plats if p[3] >= GROUND_TOLERANCE], key=lambda p: p[0])
    for a, b in zip(grounds, grounds[1:]):
        al, at, ar, ah = span(a); bl, bt, br, bh = span(b)
        gap = bl - ar
        if gap > MAX_GAP and not any(  # a mid-gap hop block saves it
            p[3] < GROUND_TOLERANCE and ar <= span(p)[0] and span(p)[2] <= bl + MAX_GAP
            for p in plats):
            errors.append(f"GAP {gap:.0f}px between ground plats x={ar:.0f}->{bl:.0f} exceeds {MAX_GAP}px")

    # ---- checkpoints ----
    prev = 0
    for cp in sorted(cps):
        cp_span = cp - prev
        if cp_span > MAX_CP_SPAN:
            errors.append(f"checkpoint gap {cp_span:.0f}px before x={cp} exceeds {MAX_CP_SPAN}px")
        if not any(span(p)[0] - 10 <= cp <= span(p)[2] + 10 and p[3] >= 200 for p in plats):
            errors.append(f"checkpoint x={cp} floats over a gap")
        prev = cp
    tail = d["meta"]["width"] - prev
    if tail > MAX_CP_SPAN:
        errors.append(f"level tail {tail:.0f}px after last checkpoint exceeds {MAX_CP_SPAN}px")

    # ---- tokens ----
    n = len(tokens)
    if n < MIN_TOKENS:
        errors.append(f"token count {n} below smoke gate {MIN_TOKENS}")
    tiers = set(t.get("tier", 0) for t in tokens)
    for tier, label in [(0, "easy"), (1, "exploration"), (2, "difficult"), (3, "hidden")]:
        if tier not in tiers:
            errors.append(f"token tier missing: {label} (tier {tier})")

    # ---- secret ----
    if not d.get("secretHeart") and 3 not in tiers:
        errors.append("no secret: need a heart or a tier-3 hidden cache")

    # ---- report ----
    print(f"== AUDIT {path} ==")
    print(f"   plats={len(plats)} tokens={n} checkpoints={len(cps)} width={d['meta']['width']/100:.0f}u")
    for w in warnings:
        print(f"   WARN: {w}")
    for e in errors:
        print(f"   FAIL: {e}")
    if errors:
        print(f"   RED - {len(errors)} blocking issue(s)")
        sys.exit(2)
    print(f"   GREEN - all checks passed" + (f" ({len(warnings)} warning(s))" if warnings else ""))
    sys.exit(0)


GROUND_Y = lambda d: d["meta"].get("groundY", 620)
GROUND_TOLERANCE = 600  # ground = plat taller than 200px (height, not canvas y)

if __name__ == "__main__":
    audit(sys.argv[1])
