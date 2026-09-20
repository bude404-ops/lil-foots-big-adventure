#!/usr/bin/env python3
"""Lil Foots Course Composer — recipe -> audited map JSON.

A recipe is ~10 lines: which blocks, what order, difficulty dial, lore name.
Emits the same schema the game already reads (map011.json style) so the Unity
level builder consumes it without modification.

Usage: python3 course_composer.py recipes/r1_1_1.json [out.json]
"""
import json, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from course_blocks import BLOCKS

GROUND = 620
STITCH = 320


def scale_gap(dial):
    return {0: 0.85, 1: 1.0, 2: 1.15}.get(dial, 1.0)


def place_plats(b, bx, dial, block_span):
    """Emit a block's plats at origin bx.
    - Terminal plats [x, 620, 0] auto-width to the next block origin.
    - Gap-edge plats slide outward: only the GAP before them scales by the dial,
      so base gaps <= 330 stay <= 360 even at hard (1.15x).
    """
    g = scale_gap(dial)
    edges = set(b.get("gap_edges", []))
    out, prev_right = [], None
    for i, (px, py, pw, ph) in enumerate(b["plats"]):
        if pw == 0:
            pw = block_span - px          # extends to the next block origin
        if i in edges and prev_right is not None and g != 1.0:
            gap = px - prev_right
            px = px + int(gap * (g - 1))
        out.append([bx + px, py, pw, ph])
        prev_right = px + pw
    for s in b.get("snags", []):
        out.append([bx + s[0], s[1], s[2], s[3]])
    return out


def tokens_for(b, plats_in_block, dial):
    """Tokens derive from the EMITTED plats (adjusted by the dial), so they ride
    the real geometry. Raised plats = top < 620 (excluding ground terminals)."""
    raised = [p for p in plats_in_block if p[1] != 620]  # off-ground: up OR sunken
    grounds = [p for p in plats_in_block if p[1] >= 620]
    out = []
    t = b.get("tokens")

    def cx(p): return p[0] + p[2] / 2

    if t == "ground_trail":
        n = b.get("tokens_n", 5)
        g0, g1 = grounds[0], grounds[-1]
        return [{"x": int(g0[0] + (g1[0] + g1[2] - g0[0]) * (i + 0.5) / n), "y": 580, "tier": 0}
                for i in range(n)]
    if t == "hop_arc":
        for p in raised:
            out += [{"x": int(cx(p)), "y": p[1] - 90, "tier": 0},
                    {"x": int(cx(p)), "y": p[1] - 220, "tier": 1}]
        return out + [{"x": grounds[0][0] + 150, "y": 580, "tier": 0},
                      {"x": grounds[0][0] + 300, "y": 580, "tier": 0}]
    if t == "stair_arc":
        for i, p in enumerate(raised):
            out.append({"x": int(cx(p)), "y": p[1] - 80, "tier": 0 if i < 2 else 1})
        return out + [{"x": grounds[0][0] + 150, "y": 580, "tier": 0}]
    if t == "log_arc":
        for p in raised:
            out += [{"x": int(cx(p)), "y": p[1] - 70, "tier": 0},
                    {"x": int(cx(p)), "y": p[1] - 200, "tier": 1}]
        return out + [{"x": grounds[0][0] + 200, "y": 580, "tier": 0}]
    if t == "fog_arc":
        for p in raised:
            out.append({"x": int(cx(p)), "y": p[1] - 80, "tier": 1})
        mid = (grounds[0][0] + grounds[0][2] + grounds[-1][0]) / 2
        return out + [{"x": int(mid), "y": 580, "tier": 2},
                      {"x": int(mid + 300), "y": 580, "tier": 2},
                      {"x": grounds[0][0] + 150, "y": 580, "tier": 0}]
    if t == "climb_arc":
        for i, p in enumerate(raised):
            out.append({"x": int(cx(p)), "y": p[1] - 80, "tier": 1 if i < 2 else 2})
        return out + [{"x": grounds[0][0] + 150, "y": 580, "tier": 0}]
    if t == "gauntlet_arc":
        for p in raised:
            out.append({"x": int(cx(p)), "y": p[1] - 120, "tier": 2})
        return out + [{"x": grounds[0][0] + 150, "y": 580, "tier": 0}]
    if t == "tower_cache":
        out += [{"x": int(cx(raised[0])), "y": raised[0][1] - 80, "tier": 1},
                {"x": int(cx(raised[1])), "y": raised[1][1] - 80, "tier": 1}]
        top = raised[2]
        for i in range(3):   # tier3 exploration cache at the tower top
            out.append({"x": int(top[0] + 60 + i * 80), "y": top[1] - 70, "tier": 3})
        return out
    if t == "runway_arc":
        r = raised[0] if raised else grounds[0]
        for i in range(6):
            out.append({"x": int(r[0] + 60 + i * 140), "y": r[1] - 90, "tier": 0})
        return out
    if t == "gully_cache":
        gully = raised[0]
        for i in range(3):
            out.append({"x": int(gully[0] + 80 + i * 120), "y": gully[1] - 50, "tier": 3})
        return out
    if t == "cave_cache":
        under = grounds[1]
        for i in range(3):
            out.append({"x": int(under[0] - 90 + i * 60), "y": 560, "tier": 3})
        return out
    return out


def compose(recipe):
    dial = int(recipe.get("dial", 1))
    plats, tokens, checkpoints, hearts = [], [], [], []
    cursor = 0
    for entry in recipe["blocks"]:
        name = entry["block"] if isinstance(entry, dict) else entry
        b = BLOCKS[name]
        bx = cursor
        block_span = b["length"] + STITCH
        b_plats = place_plats(b, bx, dial, block_span)
        plats.extend(b_plats)
        tokens.extend(tokens_for(b, b_plats, dial))
        if b.get("checkpoint"):
            cpx = b.get("checkpoint_x", b["length"] - 200)
            checkpoints.append(bx + cpx)
        if b.get("heart"):
            hearts.append({"x": bx + b["heart"]["dx"], "y": b["heart"]["y"]})
        cursor += block_span
    checkpoints = ensure_cadence(checkpoints, plats, cursor)
    return plats, tokens, checkpoints, hearts, cursor

def ground_point_near(plats, target, hardmax, hardmin):
    """Best standing point on a ground plat: closest to target, within [hardmin, hardmax]."""
    best, bd = None, 1e18
    for px, py, pw, ph in plats:
        if py < GROUND:
            continue
        lo = max(px + 40, hardmin)
        hi = min(px + pw - 40, hardmax)
        if lo > hi:
            continue
        cand = min(hi, max(lo, target))
        d = abs(cand - target)
        if d < bd:
            bd, best = d, cand
    return best if best is not None else hardmax

def ensure_cadence(checkpoints, plats, width):
    """Doctrine: checkpoints at most ~30u apart, always on solid ground."""
    cps = sorted(set([0] + checkpoints))
    out = []
    prev = 0
    for cp in cps:
        while cp - prev > 3000:
            x = ground_point_near(plats, prev + 2800, prev + 2900, prev + 200)
            out.append(x)
            prev = x
        out.append(cp)
        prev = cp
    while width - prev > 3000:
        x = ground_point_near(plats, prev + 2800, prev + 2900, prev + 200)
        out.append(x)
        prev = x
    return sorted(set(out))


def main():
    recipe_path = sys.argv[1]
    recipe = json.load(open(recipe_path))
    plats, tokens, checkpoints, hearts, width = compose(recipe)
    map_data = {
        "meta": {
            "map": recipe.get("map_id", "r1_1"),
            "name": recipe["name"],
            "width": width,
            "groundY": GROUND,
            "gateNeed": recipe.get("gateNeed", 15),
            "ppu": 100,
            "region": recipe.get("region", "pnw"),
            "role": recipe.get("role", "teach"),
            "tokenCount": len(tokens),
            "composed": True,
        },
        "physics": {"gravity": 2400, "runSpeed": 460, "jumpVelocity": -900,
                     "jumpHold": 0.28, "jumpHoldGravityFactor": 0.9,
                     "coyote": 0.12, "buffer": 0.14},
        "plats": plats,
        "hounds": [],
        "cams": [],
        "drone": None,
        "checkpoints": sorted(set(checkpoints)),
        "tokens": tokens,
        "secretHeart": hearts[0] if hearts else None,
    }
    default_out = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                                "..", "Assets", "LevelData",
                                                f"map_{recipe.get('map_id', 'r1_1')}.json"))
    out = sys.argv[2] if len(sys.argv) > 2 else default_out
    with open(out, "w") as f:
        json.dump(map_data, f, indent=1)
    print(f"composed {recipe['name']} -> {out}")
    print(f"  plats={len(plats)} tokens={len(tokens)} checkpoints={len(map_data['checkpoints'])} width={width/100:.0f}u")


if __name__ == "__main__":
    main()
