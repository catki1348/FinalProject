# Stage 5-2 camera framing plan: for every platform A, how big must the main camera be so the NEXT platform B is visible while the player is on A?
# Output: Stage52_CameraZoom.json (zoom volumes in world coordinates) + a report. Run from @Edit:  python Plan_Stage52_CameraZoom.py
import json, math

ASPECT = 16 / 9
MARGIN = 0.8                  # keep B this far inside the screen edge
LEAD = 1.0                    # seconds of warning before a jump becomes possible
REACH_X = 4.4                 # 80% jump, level
ALWAYS_CAP = 11.5             # "next platform visible all the time" is only demanded up to this size
ZONE_SIZE = 5.0               # current CameraBoundsZone size of 5-2
ORIGIN_X = 532.0              # world X of the part origin (Stage52_UnityPreparation.json)
d = json.load(open('MapDesign_Stage5-2.json', encoding='utf-8'))


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
    dist = (1 - abs(2 * ph - 1)) * length
    for i, sl in enumerate(seg):
        if dist <= sl or i == len(seg) - 1:
            f = 0 if sl == 0 else min(1, dist / sl)
            return wp[i][0] + (wp[i + 1][0] - wp[i][0]) * f, wp[i][1] + (wp[i + 1][1] - wp[i][1]) * f
        dist -= sl


def stand_x(a, bx):
    """x where the player waits on A: the point of A nearest to B (long platforms: near their edge)"""
    half = a['width'] / 2 - 0.4
    axc, _ = a['_pos']
    return min(max(bx, axc - half), axc + half)


def pair_need(a, b, dt=0.0125, n=3200):
    always = 0.0; approach = 0.0
    win = []; need_t = []
    for i in range(n):
        t = i * dt
        a['_pos'] = pos(a, t)
        axc, ay = a['_pos']; bx, by = pos(b, t)
        ax = stand_x(a, bx)
        gap = max(0.0, abs(bx - ax) - b['width'] / 2)
        need = max(abs(by - ay) + MARGIN, gap / ASPECT + MARGIN)
        need_t.append(need)
        win.append(gap <= REACH_X and -6.0 <= (by - ay) <= 0.8)
        always = max(always, need)
    lead_n = int(LEAD / dt)
    for i in range(n):
        if win[i] or any(win[(i + k) % n] for k in range(0, lead_n, 8)):
            approach = max(approach, need_t[i])
    return always, approach


plat = {}
rows = []
for part in d['parts']:
    pl = part['platforms']
    for idx, q in enumerate(pl):
        plat[q['id']] = q
    for a, b in zip(pl, pl[1:]):
        always, approach = pair_need(a, b)
        need = max(approach, min(always, ALWAYS_CAP))
        rows.append({'part': part['id'], 'from': a['id'], 'to': b['id'], 'always': round(always, 2), 'approach': round(approach, 2), 'size': round(need, 2)})


def extent(q):
    if q.get('move'):
        xs = []; ys = []
        for x, y in q['move']['wp']:
            xs += [x - q['width'] / 2, x + q['width'] / 2]; ys.append(y)
        return min(xs), max(xs), min(ys), max(ys)
    return q['cx'] - q['width'] / 2, q['cx'] + q['width'] / 2, q['top'], q['top']


volumes = []
for r in rows:
    if r['size'] <= ZONE_SIZE + 0.2:
        continue
    a = plat[r['from']]
    x0, x1, y0, y1 = extent(a)
    padx = 2.5; pady_lo = 3.0; pady_hi = 3.0
    size = math.ceil(r['size'] * 2) / 2                    # round up to 0.5
    volumes.append({'name': 'Zoom_' + r['from'], 'from': r['from'], 'to': r['to'], 'orthographicSize': size,
                    'minX': round(ORIGIN_X + x0 - padx, 2), 'maxX': round(ORIGIN_X + x1 + padx, 2),
                    'minY': round(y0 - pady_lo, 2), 'maxY': round(y1 + pady_hi, 2)})

json.dump({'zoneSize': ZONE_SIZE, 'alwaysCap': ALWAYS_CAP, 'lead': LEAD, 'margin': MARGIN, 'volumes': volumes, 'pairs': rows}, open('Stage52_CameraZoom.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=1)

print('pairs %d, volumes needed %d, max size %.1f' % (len(rows), len(volumes), max([v['orthographicSize'] for v in volumes] + [0])))
for v in volumes:
    print('%-14s -> %-12s size %4.1f   x %.1f..%.1f  y %.1f..%.1f' % (v['from'], v['to'], v['orthographicSize'], v['minX'], v['maxX'], v['minY'], v['maxY']))


# ---- verify: with the zoom volumes, is the next platform visible while the player rides A? ----
def size_at(x, y):
    s = ZONE_SIZE
    for v in volumes:
        if v['minX'] <= x <= v['maxX'] and v['minY'] <= y <= v['maxY']:
            s = max(s, v['orthographicSize'])
    return s


bad_always = []; bad_window = []
for part in d['parts']:
    pl = part['platforms']
    for a, b in zip(pl, pl[1:]):
        dt = 0.0125; n = 3200
        vis = 0; win_t = []; vis_t = []
        for i in range(n):
            t = i * dt
            a['_pos'] = pos(a, t)
            axc, ay = a['_pos']; bx, by = pos(b, t)
            ax = stand_x(a, bx)
            S = size_at(ORIGIN_X + ax, ay + 0.65)
            gap = max(0.0, abs(bx - ax) - b['width'] / 2)
            ok = abs(by - ay) + MARGIN <= S and gap / ASPECT + MARGIN <= S
            vis += ok
            win_t.append(gap <= REACH_X and -6.0 <= (by - ay) <= 0.8); vis_t.append(ok)
        lead_n = int(LEAD / dt)
        miss = sum(1 for i in range(n) if (win_t[i] or any(win_t[(i + k) % n] for k in range(0, lead_n, 8))) and not vis_t[i])
        if miss: bad_window.append((a['id'], b['id'], miss))
        if vis / n < 0.99: bad_always.append((a['id'], b['id'], round(100 * vis / n)))
print()
print('with volumes: pairs whose jump window (+%.1fs lead) is ever outside the view: %d' % (LEAD, len(bad_window)), bad_window)
print('pairs not visible 100%% of the time: %d' % len(bad_always), bad_always)
