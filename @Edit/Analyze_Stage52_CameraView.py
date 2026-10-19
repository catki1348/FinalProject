# Stage 5-2: how much of the time is the NEXT platform inside the main camera view while the player rides the current one?
# Usage: python Analyze_Stage52_CameraView.py [orthographicSize]
import json, math, sys

ASPECT = 16 / 9
MARGIN = 0.8                      # keep the target this far inside the screen edge
d = json.load(open('MapDesign_Stage5-2.json', encoding='utf-8'))
size = float(sys.argv[1]) if len(sys.argv) > 1 else 5.0


def pos(p, t):
    mv = p.get('move')
    if not mv:
        return p['cx'], p['top']
    wp = mv['wp']
    seg = [math.hypot(wp[i + 1][0] - wp[i][0], wp[i + 1][1] - wp[i][1]) for i in range(len(wp) - 1)]
    length = sum(seg)
    period = mv.get('syncPeriod') or 0
    if period <= 0:
        period = 2 * length / mv['speed'] + 2 * mv.get('wait', 0) * (len(wp) - 1)
    ph = ((t - mv.get('delay', 0)) % period) / period
    dist = (1 - abs(2 * ph - 1)) * length                 # ping-pong along the polyline
    for i, sl in enumerate(seg):
        if dist <= sl or i == len(seg) - 1:
            f = 0 if sl == 0 else min(1, dist / sl)
            return wp[i][0] + (wp[i + 1][0] - wp[i][0]) * f, wp[i][1] + (wp[i + 1][1] - wp[i][1]) * f
        dist -= sl


rows = []
for part in d['parts']:
    pl = part['platforms']
    for a, b in zip(pl, pl[1:]):
        vis = 0; need_best = 1e9; need_worst = 0; n = 0
        for i in range(0, 3200):
            t = i * 0.0125                                   # 40 s, covers the 4 s sync period
            ax, ay = pos(a, t); bx, by = pos(b, t)
            # nearest edge of the next platform (platform width counts), player centre = camera centre
            ddx = max(0.0, abs(bx - ax) - b['width'] / 2)
            need = max(abs(by - ay) + MARGIN, ddx / ASPECT + MARGIN)
            need_best = min(need_best, need); need_worst = max(need_worst, need)
            if need <= size: vis += 1
            n += 1
        rows.append((part['id'], a['id'], b['id'], vis / n, need_best, need_worst))

bad = [r for r in rows if r[3] < 0.999]
print('orthographicSize', size, 'pairs', len(rows), 'not always visible', len(bad))
print('worst required size over all pairs: %.2f' % max(r[5] for r in rows))
print('%-4s %-10s %-10s %8s %8s %8s' % ('part', 'from', 'to', 'visible', 'bestS', 'worstS'))
for r in sorted(rows, key=lambda r: -r[5])[:40]:
    print('%-4s %-10s %-10s %7.0f%% %8.2f %8.2f' % (r[0], r[1], r[2], 100 * r[3], r[4], r[5]))

# how many pairs are fully visible (100% of the time) for a range of zoom sizes
print()
for sz in (5, 6, 7, 8, 9, 10, 11, 12, 14):
    full = sum(1 for r in rows if r[5] <= sz)
    print('size %4.1f: always visible %2d / %d pairs' % (sz, full, len(rows)))

# ---- approach-window analysis: B must be visible from LEAD seconds before a jump becomes possible until it ends ----
LEAD = 1.0
REACH_X = 4.4                      # 80% jump, level
print()
print('approach-window requirement (lead %.1fs):' % LEAD)
req = []
for part in d['parts']:
    pl = part['platforms']
    for a, b in zip(pl, pl[1:]):
        dt = 0.0125; ts = [i * dt for i in range(0, 3200)]
        win = []
        for t in ts:
            ax, ay = pos(a, t); bx, by = pos(b, t)
            gap = max(0.0, abs(bx - ax) - a['width'] / 2 - b['width'] / 2)
            reach = gap <= REACH_X and (by - ay) <= 0.8 and (by - ay) >= -6.0
            win.append(reach)
        need = 0.0
        n = len(ts); lead_n = int(LEAD / dt)
        for i in range(n):
            if win[i] or any(win[(i + k) % n] for k in range(0, lead_n, 8)):
                t = ts[i]
                ax, ay = pos(a, t); bx, by = pos(b, t)
                ddx = max(0.0, abs(bx - ax) - b['width'] / 2)
                need = max(need, abs(by - ay) + MARGIN, ddx / ASPECT + MARGIN)
        req.append((part['id'], a['id'], b['id'], need, any(win)))
for r in sorted(req, key=lambda r: -r[3])[:28]:
    print('%-4s %-10s -> %-10s needs S>= %5.2f %s' % (r[0], r[1], r[2], r[3], '' if r[4] else '(no jump window!)'))
print('max over all: %.2f' % max(r[3] for r in req))
for sz in (5, 6, 7, 8, 9, 10, 11, 12):
    print('size %2d: %2d / %d pairs OK' % (sz, sum(1 for r in req if r[3] <= sz), len(req)))
