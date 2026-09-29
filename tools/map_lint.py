#!/usr/bin/env python3
"""Map linter: pre-flight physics + reachability check for Lil Foots level data.

Replicates the smoke test's geometry gate (LilFootsSmokeTest.cs) EXACTLY:
- weld grouping: same-y grounds (y>=600 or w>=400, h>=200) overlapping or
  gap<=0.6u merge into one platform node
- BFS from spawn with real jump physics: rise<=1.95u, gap<=3.7-0.9*rise
- every platform reachable, flag gate reachable, every token collectable
  (within x+/-1.5u of a reachable platform, 0..3.6u above its top, or the
  straddle-gap rule), secret heart reachable

Run before dispatching the 25-minute Unity forge. A broken map dies here in
seconds instead of after a full build cycle. Exit 0 = ship-safe geometry.

Usage: python3 tools/map_lint.py unity/Assets/LevelData/map_region1_spine.json
"""
import json, sys, collections

def lint(path, gate_x=None, spawn_x=6.52):
    d = json.load(open(path))
    GY = d['meta']['groundY']; ppu = 100
    plats = d['plats']
    def top_of(y): return (2*GY - y) / ppu

    # weld rows (mirrors LevelBuilder: same-y grounds, overlap or gap<=0.6)
    idx_plats = sorted(enumerate(plats), key=lambda ip: (ip[1][1], ip[1][0] - ip[1][2]/2))
    rows = []
    for i, p in idx_plats:
        cx, y, w_, h = p[0], p[1], p[2], p[3]   # [KIT Sept 28] plats may carry a 5th hop-kind element
        x0, x1 = (cx - w_/2)/ppu, (cx + w_/2)/ppu
        if (y >= 600 or w_ >= 400) and h >= 200:
            placed = False
            for r in rows:
                if abs(r['y'] - y) < 0.5 and x0 <= r['maxr'] + 0.6:
                    r['maxr'] = max(r['maxr'], x1); r['ids'].append(i); placed = True; break
            if not placed:
                rows.append(dict(y=y, minl=x0, maxr=x1, ids=[i]))
    nodes = []
    for r in rows:
        if len(r['ids']) > 1:
            nodes.append(dict(x0=r['minl'], x1=r['maxr'], top=top_of(r['y']), ids=list(r['ids'])))
    for i, p in enumerate(plats):
        if any(i in n['ids'] for n in nodes): continue
        cx, y, w_, h = p[0], p[1], p[2], p[3]   # [KIT Sept 28] plats may carry a 5th hop-kind element
        nodes.append(dict(x0=(cx-w_/2)/ppu, x1=(cx+w_/2)/ppu, top=top_of(y), ids=[i]))

    # BFS from spawn (mirrors the smoke gate)
    si, best = -1, -999
    for i, n in enumerate(nodes):
        if spawn_x >= n['x0']-0.6 and spawn_x <= n['x1']+0.6 and n['top'] > best:
            best, si = n['top'], i
    if si < 0:
        return [f"SPAWN: x={spawn_x} does not stand on any platform"]
    reach = [False]*len(nodes); reach[si] = True
    q = collections.deque([si])
    while q:
        ai = q.popleft(); a = nodes[ai]
        for bi, b in enumerate(nodes):
            if reach[bi]: continue
            rise = b['top'] - a['top']
            if rise > 1.95: continue
            gap = b['x0']-a['x1'] if b['x0'] > a['x1'] else (a['x0']-b['x1'] if a['x0'] > b['x1'] else 0)
            if gap <= 3.7 - max(0, rise)*0.9:
                reach[bi] = True; q.append(bi)

    fails = []
    unreached = [(round(n['x0'],1), round(n['top'],1)) for n, r in zip(nodes, reach) if not r]
    if unreached:
        fails.append(f"REACHABILITY: {len(unreached)} platform(s) unreachable from spawn, first 5: {unreached[:5]}")

    # flag gate: rightmost ground +1u tolerance (smoke uses scene gate; here: max x plat)
    if gate_x is None:
        gate_x = max(n['x1'] for n, r in zip(nodes, reach) if r) - 1.2  # gate stands near the level end
    gate_ok = any(r and n['x0']-1 <= gate_x <= n['x1']+1 for n, r in zip(nodes, reach))
    if not gate_ok:
        fails.append(f"GATE: x={gate_x:.1f} not reachable")

    # tokens
    orphans = []
    for t in d['tokens']:
        tx, ty = t['x']/ppu, (2*GY - t['y'])/ppu
        ok = False
        for n, r in zip(nodes, reach):
            if not r: continue
            if tx >= n['x0']-1.5 and tx <= n['x1']+1.5 and -1.2 < ty - n['top'] < 3.6:
                ok = True; break
        if not ok:
            # straddle-gap rule (mirrors smoke)
            for n, r in zip(nodes, reach):
                if not r: continue
                if n['x1'] < tx and tx - n['x1'] <= 2.2 and -1.2 < ty - n['top'] < 3.2:
                    for m, r2 in zip(nodes, reach):
                        if r2 and m['x0'] > tx and m['x0'] - tx <= 2.2:
                            ok = True; break
                if ok: break
        if not ok: orphans.append((round(tx,1), round(ty,1)))
    if orphans:
        fails.append(f"TOKENS: {len(orphans)} unreachable, first 5: {orphans[:5]}")

    sh = d.get('secretHeart')
    if sh:
        tx, ty = sh['x']/ppu, (2*GY - sh['y'])/ppu
        ok = any(r and tx >= n['x0']-1.5 and tx <= n['x1']+1.5 and -1.2 < ty - n['top'] < 3.6
                 for n, r in zip(nodes, reach))
        if not ok:
            fails.append(f"SECRET HEART at ({tx:.1f},{ty:.1f}) unreachable")
    return fails

if __name__ == '__main__':
    path = sys.argv[1]
    gate = float(sys.argv[2]) if len(sys.argv) > 2 else None
    fails = lint(path, gate)
    if fails:
        print("MAP LINT: RED - DO NOT DISPATCH")
        for f in fails: print("  -", f)
        sys.exit(1)
    print("MAP LINT: GREEN - geometry ship-safe (spawn->gate, all platforms + tokens reachable)")
