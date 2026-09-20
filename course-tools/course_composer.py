#!/usr/bin/env python3
"""Lil Foots Course Composer + Auditor (Region block system, Bude-approved direction).

A level is a RECIPE: an ordered list of course blocks with difficulty dials.
The composer stamps blocks into a LevelBuilder-compatible map JSON (plats use
LEFT-EDGE x convention, same as map_m1/map011). The auditor then PROVES the
assembled map against the live jump physics before Unity ever sees it.
Red audit = named reason in seconds; red maps cannot be built.

Usage:
  python3 course_composer.py compose recipes/1-1.json -o map_1_1.json
  python3 course_composer.py audit   map_1_1.json
"""
import json, sys, math

PPU = 100
WORLD_END = 8600          # builder places the gate at x=86u; courses end before it
RUN_SPEED = 460.0
JUMP_V = 900.0
GRAV = 2400.0
HOLD_T, HOLD_F = 0.28, 0.9

def _jump(hold):
    if hold:
        v1 = JUMP_V - GRAV * HOLD_F * HOLD_T
        h1 = JUMP_V * HOLD_T - 0.5 * GRAV * HOLD_F * HOLD_T ** 2
        t1, t2 = HOLD_T, v1 / GRAV
        h2 = v1 ** 2 / (2 * GRAV)
    else:
        t1, h1, t2, h2 = 0.0, 0.0, JUMP_V / GRAV, JUMP_V ** 2 / (2 * GRAV)
    apex = h1 + h2
    air = t1 + t2 + math.sqrt(2 * apex / GRAV)
    return RUN_SPEED * air, apex

MAX_RANGE, MAX_APEX = _jump(True)    # full hold: ~366px, ~185px
TAP_RANGE, TAP_APEX = _jump(False)   # panic tap: ~345px, ~169px

HARD_GAP = 0.985 * TAP_RANGE         # tap-safe ceiling (~340px)
MIN_LANDING = 120

DIAL = {0: 0.62, 1: 0.78, 2: 0.88, 3: 0.96}   # stretches toward the ceiling

def gap_w(base, dial):
    return round(base * DIAL[dial])

def rise_h(base, dial):
    return round(base * DIAL[dial])

GY = 620

def ground(x, w):
    """Ground slab: [left, y(center), w, h] — same convention as map_m1."""
    return [x, GY, w, 400]

def hop(x, y_top, w):
    """Hop block: thin floater stored by CENTER y (canvas), top = y-50."""
    return [x, y_top + 50, w, 100]

def tok(x, y, tier=0):
    return {"x": int(x), "y": int(y), "tier": tier}

def line_toks(o, w, y, step=180, tier=0):
    return [tok(x, y, tier) for x in range(int(o + 160), int(o + w - 100), step)]

# ================= BLOCK LIBRARY (Region 1, PNW) =================
# fn(origin_x, dial, tokens, plats) -> block width. Gaps are streams (water
# renders in true pits automatically); rises respect the 1.6u standard law.

def b_fern_gap(o, d, t, p):
    """T1 Fern Gap: first jump over a stream. Dial 0 = 2.2u gap, wide landing."""
    g = gap_w(220, d)
    p.append(ground(o, 300))
    p.append(ground(o + 300 + g, 380))
    t.append(tok(o + 300 + g / 2, GY - 120))
    t.append(tok(o + 120, GY - 60))
    t.append(tok(o + 300 + g + 150, GY - 60))
    t.append(tok(o + 300 + g + 300, GY - 60))
    return 680 + g

def b_moss_steps(o, d, t, p):
    """T2 Moss Steps: two low rises teaching climb."""
    r = rise_h(100, d)
    p.append(ground(o, 260))
    p.append(hop(o + 260, GY - r, 240))
    p.append(hop(o + 520, GY - r - 70, 240))
    p.append(ground(o + 780, 260))
    t.append(tok(o + 380, GY - r - 50))
    t.append(tok(o + 640, GY - r - 120))
    return 1040

def b_log_hop(o, d, t, p):
    """P1 Log Hop: fallen-cedar chain over a brook, 3 hops."""
    g = gap_w(240, d)
    p.append(ground(o, 260))
    x = o + 260
    for i in range(3):
        p.append(hop(x + g / 2 - 90, GY - 60 - (i % 2) * 35, 180))
        t.append(tok(x + g / 2, GY - 140 - (i % 2) * 35, 1))
        x += g
    p.append(ground(x, 260))
    t.append(tok(o + 120, GY - 60))
    t.append(tok(x + 120, GY - 60))
    return 520 + 3 * g

def b_cedar_stairs(o, d, t, p):
    """P2 Cedar Stairs: up 3 steps, arc over the top, down the far side."""
    r = rise_h(95, d)
    sw = 220
    p.append(ground(o, 240))
    x = o + 240
    for i in range(1, 4):
        p.append(hop(x, GY - i * r, sw))
        x += sw + 30
    t.append(tok(o + 240 + sw * 1.5, GY - 3 * r - 60, 1))
    t.append(tok(o + 240 + sw * 1.5 + 130, GY - 3 * r - 120, 1))
    t.append(tok(o + 240 + sw * 1.5 + 260, GY - 3 * r - 60, 1))
    p.append(ground(x + 40, 240))
    return x + 280 - o

def b_fern_run(o, d, t, p):
    """P3 Fern Run: flat sprint with a token line."""
    w = 700
    p.append(ground(o, w))
    t += line_toks(o, w, GY - 60, step=150)
    return w

def b_brook_crossing(o, d, t, p):
    """D1 Brook Crossing: mixed gaps, one mid-gap hop is required."""
    g = gap_w(300, d)
    p.append(ground(o, 260))
    p.append(hop(o + 260 + g / 2 - 90, GY - 80, 180))
    p.append(ground(o + 260 + g, 260))
    t.append(tok(o + 120, GY - 60))
    t.append(tok(o + 260 + g + 120, GY - 60))
    p.append(hop(o + 520 + g + g / 2 - 90, GY - 110, 180))
    p.append(ground(o + 520 + 2 * g, 260))
    t.append(tok(o + 260 + g / 2, GY - 200, 1))
    t.append(tok(o + 520 + g + g / 2, GY - 230, 1))
    return 780 + 2 * g

def b_canopy_shelf(o, d, t, p):
    """D2 Canopy Shelf: high road / low road, risk = reward, no blind jumps."""
    w = 1000
    p.append(ground(o, w))
    p.append(hop(o + 200, GY - rise_h(130, d), 700))
    t.append(tok(o + 380, GY - rise_h(130, d) - 70, 1))
    t.append(tok(o + 560, GY - rise_h(130, d) - 70, 1))
    t.append(tok(o + 740, GY - rise_h(130, d) - 70, 1))
    return w

def b_fog_bank(o, d, t, p):
    """D3 Fog Bank: known-safe pattern under gameplay fog (pattern first)."""
    g = gap_w(260, d)
    p.append(ground(o, 300))
    p.append(ground(o + 300 + g, 300))
    t.append(tok(o + 300 + g / 2, GY - 130, 1))
    return 600 + g

def b_gauntlet_hollow(o, d, t, p):
    """C1 Gauntlet Hollow: gap-rise-gap chain, checkpoint lands before it."""
    g = gap_w(280, d)
    r = rise_h(110, d)
    p.append(ground(o, 260))
    p.append(hop(o + 260 + g / 2 - 90, GY - r, 180))
    p.append(hop(o + 260 + g + 130, GY - r - rise_h(90, d), 180))
    p.append(ground(o + 260 + 2 * g + 260, 260))
    t.append(tok(o + 260 + g / 2, GY - r - 60, 2))
    t.append(tok(o + 260 + g + 220, GY - 2 * r - 60, 2))
    return 780 + 2 * g

def b_snag_ridge(o, d, t, p):
    """C2 Snag Ridge: staggered platforms over a long pit."""
    g = gap_w(260, d)
    p.append(ground(o, 260))
    p.append(hop(o + 260 + g / 2 - 80, GY - 80, 160))
    p.append(hop(o + 260 + g * 1.5 - 80, GY - 170, 160))
    p.append(ground(o + 260 + 2 * g, 300))
    t.append(tok(o + 260 + g / 2, GY - 160, 2))
    t.append(tok(o + 260 + g * 1.5, GY - 250, 2))
    return 560 + 2 * g

def b_wendigo_approach(o, d, t, p):
    """F1 Wendigo Approach: finale runway, token arc, boss foreshadow."""
    w = 700
    p.append(ground(o, w))
    ax = o + w / 2
    t.append(tok(ax - 160, GY - 80))
    t.append(tok(ax, GY - 170))
    t.append(tok(ax + 160, GY - 80))
    t += line_toks(o, w, GY - 60)
    return w

def b_meadow_gate(o, d, t, p):
    """F2 Meadow Gate: clear runway into flag + portal (gate at x=86u)."""
    p.append(ground(o, 600))
    t += line_toks(o, 600, GY - 60, step=170)
    return 600

BLOCKS = {
    "T1": ("teach", b_fern_gap), "T2": ("teach", b_moss_steps),
    "P1": ("play", b_log_hop), "P2": ("play", b_cedar_stairs), "P3": ("play", b_fern_run),
    "D1": ("develop", b_brook_crossing), "D2": ("develop", b_canopy_shelf),
    "D3": ("develop", b_fog_bank),
    "C1": ("challenge", b_gauntlet_hollow), "C2": ("challenge", b_snag_ridge),
    "F1": ("finale", b_wendigo_approach), "F2": ("finale", b_meadow_gate),
}

def secret_gully(p, t, o, w):
    """S1 Gully Secret: alcove under the far edge of the host block."""
    x = o + w - 160
    p.append(hop(x, GY + 30, 220))
    t.append(tok(x - 40, GY + 80, 3))
    t.append(tok(x + 60, GY + 80, 3))

def secret_treetop(p, t, o, w):
    """S2 Treetop Cache: exploration tier above the main route."""
    x = o + w / 2
    p.append(hop(x, GY - 300, 200))
    t.append(tok(x, GY - 360, 3))
    t.append(tok(x + 80, GY - 360, 3))

SECRETS = {"S1": secret_gully, "S2": secret_treetop}

def compose(recipe_path, out_path):
    r = json.load(open(recipe_path))
    plats, tokens, checkpoints = [], [], []
    x = 0
    secret = r.get("secret") or {}
    for b in r["blocks"]:
        bid, dial = b["id"], b.get("dial", 0)
        phase, fn = BLOCKS[bid]
        if phase == "challenge":
            checkpoints.append(int(x - 150))
        w = fn(x, dial, tokens, plats)
        if secret.get("block") == bid:
            SECRETS[secret["id"]](plats, tokens, x, w)
        x += w
    if x < WORLD_END:
        plats.append(ground(x, WORLD_END - x))
        x = WORLD_END
    for c in [1800, 4000, 6200]:
        if 800 < c < x - 800:
            checkpoints.append(c)
    checkpoints = sorted(set(checkpoints))

    m = {
        "meta": {"map": r.get("map", "1_1"), "name": r["name"], "width": int(x),
                 "groundY": GY, "gateNeed": 15, "ppu": PPU, "region": "pnw",
                 "role": r.get("role", "teach"), "tokenCount": len(tokens)},
        "physics": {"gravity": 2400, "runSpeed": 460, "jumpVelocity": -900,
                    "jumpHold": 0.28, "jumpHoldGravityFactor": 0.9, "coyote": 0.12,
                    "buffer": 0.14},
        "plats": plats, "hounds": [], "cams": [], "tokens": tokens,
        "checkpoints": checkpoints, "drone": None,
        "secretHeart": {"x": int(x * 0.55), "y": 295} if secret else None,
    }
    json.dump(m, open(out_path, "w"), indent=1)
    print(f"composed '{r['name']}': {len(plats)} plats, {len(tokens)} tokens, "
          f"{len(checkpoints)} checkpoints, {x/PPU:.1f}u -> {out_path}")
    return out_path

def _rect(pl):
    left, y, w, h = pl
    return (left, left + w, y - h / 2)   # (x0, x1, top) canvas: smaller top = higher

def audit(map_path):
    m = json.load(open(map_path))
    fails, warns = [], []

    # 0) course length law
    if m["meta"]["width"] > WORLD_END:
        fails.append(f"COURSE {m['meta']['width']}px overruns the gate line {WORLD_END}px")

    # 1) landing width law
    for pl in m["plats"]:
        if pl[2] < MIN_LANDING:
            fails.append(f"LANDING {pl[2]}px too thin at x={pl[0]:.0f}")

    # 2) traversal law: walk every standable surface left->right; each next
    #    surface must be jump-reachable from the previous one (rise-adjusted).
    rects = sorted((_rect(pl) for pl in m["plats"]), key=lambda r: (r[0], r[2]))
    grounds = [r for r in rects if r[2] <= 430]     # ground tops (canvas 420)
    grounds.sort(key=lambda r: r[0])
    path = grounds + [r for r in rects if r[2] > 430]
    path.sort(key=lambda r: r[0])
    cur = path[0]
    for nxt in path[1:]:
        gap = nxt[0] - cur[1]
        rise = cur[2] - nxt[2]          # canvas: positive = next is HIGHER
        if gap > 20:                     # adjacency check across a gap
            allowed = HARD_GAP * (1.0 - max(0.0, rise) / TAP_APEX * 0.75)
            if gap > allowed:
                fails.append(
                    f"GAP {gap:.0f}px at x={cur[1]:.0f} exceeds {allowed:.0f} "
                    f"(rise {rise:.0f}px)")
            elif gap > 0.9 * allowed:
                warns.append(f"gap {gap:.0f}px near limit at x={cur[1]:.0f}")
        cur = nxt

    # 3) token law
    tiers = [t["tier"] for t in m["tokens"]]
    if len(tiers) < 30:
        fails.append(f"TOKENS {len(tiers)} < 30 smoke gate")
    if tiers and tiers.count(0) / len(tiers) < 0.5:
        warns.append("tier-0 under half; keep the easy line rewarding")

    # 4) secret law
    if not m.get("secretHeart") and tiers.count(3) < 2:
        fails.append("SECRET missing: no heart and no tier-3 cache")

    # 5) checkpoint spacing
    cps = sorted(m["checkpoints"]) + [m["meta"]["width"]]
    prev = 0
    for c in cps:
        if c - prev > 2600:
            warns.append(f"checkpoint gap {c-prev:.0f}px before x={c:.0f}")
        prev = c

    print(f"AUDIT {map_path}: {'RED' if fails else 'GREEN'} "
          f"({len(fails)} fails, {len(warns)} warns)")
    for f in fails: print("  FAIL:", f)
    for w in warns: print("  warn:", w)
    return not fails

if __name__ == "__main__":
    cmd = sys.argv[1]
    if cmd == "compose":
        compose(sys.argv[2], sys.argv[sys.argv.index("-o") + 1])
    elif cmd == "audit":
        sys.exit(0 if audit(sys.argv[2]) else 1)
