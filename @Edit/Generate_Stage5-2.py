# Historical random generator. Codex (GPT-6), 2026-10-16: current fixed repair is in MapDesign_Stage5-2.json.
# Do not regenerate the current approved repair with this older search procedure; see Stage52_RepairedImplementation_20261016.md.
# 5-2 설계 데이터 생성/검증 전용. Unity 씬과 프리팹은 변경하지 않는다.
# 실행: python Generate_Stage5-2.py  -> MapDesign_Stage5-2.json, MapDesign_Stage5-2_Tables.md
# 고정 시드(52)로 이동형 발판 후보를 탐색하므로 결과는 재현 가능하다.
import math, json, random, os
import numpy as np

G = 29.43      # 중력 (9.81 x gravityScale 3)
V = 8.0        # 이동 속도
JV = 9.0       # 점프 상승 속도
T = 0.5        # 발판 두께 (Tiled 기준)
PW = 0.98      # 플레이어 충돌 폭
UNIT = 8.0     # 거리 1 = 8유닛
SEG_LEN = 120.0   # 구간 이동거리 15
HERE = os.path.dirname(os.path.abspath(__file__))
rng = random.Random(52)


def dist(a, b):
    return math.hypot(a[0] - b[0], a[1] - b[1])


def t_air(dy):
    d = JV * JV - 2 * G * dy
    return (JV + math.sqrt(d)) / G if d >= 0 else 0.0


def jump_reach(dy):            # 점프 성능 100%의 수평 도달(중심 이동)
    return V * t_air(dy)


def walk_reach(drop):          # 걸어서 가장자리를 벗어나 떨어질 때 닿는 최대 간격
    return V * math.sqrt(2 * max(drop, T) / G) + PW


def gap_max(dy):               # MapEdit 14: 점프 성능 80%로도 닿는 간격
    return 0.8 * jump_reach(dy)


def gap_min(dy):               # 걸어서는 닿지 못하는 간격
    return 1.5 if dy >= -0.5 else walk_reach(-dy) + 0.2


# ---------------------------------------------------------------- 발판 모델
def static(pid, kind, cx, top, w, **kw):
    d = dict(id=pid, kind=kind, move=None, cx=cx, top=top, w=w)
    d.update(kw)
    return d


def mover(pid, kind, wp, w, speed, delay=0.0, mode='pingpong', wait=0.0, **kw):
    d = dict(id=pid, kind=kind, move=dict(mode=mode, wp=[list(p) for p in wp], speed=speed, delay=delay, wait=wait),
             cx=sum(p[0] for p in wp) / len(wp), top=sum(p[1] for p in wp) / len(wp), w=w)
    d.update(kw)
    return d


def pos(p, t):
    m = p['move']
    if not m:
        return (p['cx'], p['top'])
    wp, sp, tt = m['wp'], m['speed'], t - m['delay']
    if tt <= 0:
        return tuple(wp[0])
    if m['mode'] == 'loop':
        pts = wp + [wp[0]]
        segs = [dist(pts[i], pts[i + 1]) for i in range(len(wp))]
        s = (tt * sp) % sum(segs)
        for i, L in enumerate(segs):
            if s <= L:
                f = s / L
                return (pts[i][0] + (pts[i + 1][0] - pts[i][0]) * f, pts[i][1] + (pts[i + 1][1] - pts[i][1]) * f)
            s -= L
    a, b = wp[0], wp[1]
    L = dist(a, b)
    one = L / sp + m['wait']
    u = tt % (2 * one)
    if u < L / sp:
        f = u * sp / L
    elif u < one:
        f = 1
    elif u < one + L / sp:
        f = 1 - (u - one) * sp / L
    else:
        f = 0
    return (a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f)


def period(p):
    m = p['move']
    if not m:
        return 0.0
    if m['mode'] == 'loop':
        pts = m['wp'] + [m['wp'][0]]
        return sum(dist(pts[i], pts[i + 1]) for i in range(len(m['wp']))) / m['speed']
    return 2 * (dist(m['wp'][0], m['wp'][1]) / m['speed'] + m['wait'])


def rail_len(p):
    m = p['move']
    if not m:
        return 0.0
    if m['mode'] == 'loop':
        pts = m['wp'] + [m['wp'][0]]
        return sum(dist(pts[i], pts[i + 1]) for i in range(len(m['wp'])))
    return dist(m['wp'][0], m['wp'][1])


TS = np.arange(0, 60.0 + 1e-9, 0.1)


def traj(p, ts=TS):
    m = p['move']
    if not m:
        return np.full_like(ts, p['cx']), np.full_like(ts, p['top'])
    wp, sp = np.array(m['wp'], dtype=float), m['speed']
    tt = np.maximum(ts - m['delay'], 0.0)
    if m['mode'] == 'loop':
        pts = np.vstack([wp, wp[:1]])
        segs = np.hypot(*(pts[1:] - pts[:-1]).T)
        per = segs.sum()
        s_ = (tt * sp) % per
        cum = np.concatenate([[0], np.cumsum(segs)])
        idx = np.clip(np.searchsorted(cum, s_, side='right') - 1, 0, len(segs) - 1)
        f = (s_ - cum[idx]) / segs[idx]
        x = pts[idx, 0] + (pts[idx + 1, 0] - pts[idx, 0]) * f
        y = pts[idx, 1] + (pts[idx + 1, 1] - pts[idx, 1]) * f
        return x, y
    a, b = wp[0], wp[1]
    L = float(np.hypot(*(b - a)))
    one = L / sp + m['wait']
    u = tt % (2 * one)
    f = np.where(u < L / sp, u * sp / L, np.where(u < one, 1.0, np.where(u < one + L / sp, 1 - (u - one) * sp / L, 0.0)))
    f = np.where(tt <= 0, 0.0, f)
    return a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f


def jump_mask(a, b, ts=TS):
    ax, ay = traj(a, ts)
    bx, by = traj(b, ts)
    gx = np.abs(bx - ax) - (a['w'] + b['w']) / 2
    dy = by - ay
    disc = np.maximum(JV * JV - 2 * G * dy, 0.0)
    gmax = 0.8 * V * (JV + np.sqrt(disc)) / G
    return (dy <= 1.0) & (gx > 0.5) & (gx <= gmax)


def jumpable(a, b):
    gx = abs(b[0] - a[0]) - (a[2] + b[2]) / 2
    dy = b[1] - a[1]
    if dy > 1.0:
        return False
    return 0.5 < gx <= gap_max(dy)


def runs(mask, dt=0.1):
    out, cur = [], None
    for i, v in enumerate(mask):
        if v and cur is None:
            cur = i
        if (not v) and cur is not None:
            out.append((cur * dt, i * dt))
            cur = None
    if cur is not None:
        out.append((cur * dt, len(mask) * dt))
    return out


def windows(a, b):
    return runs(jump_mask(a, b))


def pair_stats(a, b, start=None):
    """점프 가능 윈도우의 최소 길이와 최대 대기(윈도우 사이 간격)를 계산한다."""
    if start is None:
        start = max(period(a), period(b), 8.0)
    mask = jump_mask(a, b)
    mask = mask[int(start / 0.1):]
    if mask.all():
        return dict(ok=True, count=1, minlen=round(len(mask) * 0.1, 2), maxwait=0.0)
    w = runs(mask)
    if len(w) < 2:
        return dict(ok=False, count=len(w), minlen=0, maxwait=999)
    minlen = min(e - s_ for s_, e in w[:-1] if s_ > 0) if any(s_ > 0 for s_, e in w[:-1]) else min(e - s_ for s_, e in w)
    maxwait = max(w[i + 1][0] - w[i][1] for i in range(len(w) - 1))
    return dict(ok=True, count=len(w), minlen=round(minlen, 2), maxwait=round(maxwait, 2))


def collides(a, b, margin=0.3):
    if not a['move'] and not b['move']:
        return False
    ax, ay = traj(a)
    bx, by = traj(b)
    return bool(np.any((np.abs(ax - bx) < (a['w'] + b['w']) / 2 + margin) & (np.abs(ay - by) < T + margin)))


def seg_cross(p1, p2, p3, p4):
    def ccw(a, b, c):
        return (c[1] - a[1]) * (b[0] - a[0]) > (b[1] - a[1]) * (c[0] - a[0])
    return ccw(p1, p3, p4) != ccw(p2, p3, p4) and ccw(p1, p2, p3) != ccw(p1, p2, p4)


def rails_cross(a, b):
    if not a['move'] or not b['move']:
        return False
    A, B = a['move']['wp'], b['move']['wp']
    for i in range(len(A) if a['move']['mode'] == 'loop' else 1):
        a1, a2 = A[i], A[(i + 1) % len(A)] if a['move']['mode'] == 'loop' else A[1]
        for j in range(len(B) if b['move']['mode'] == 'loop' else 1):
            b1, b2 = B[j], B[(j + 1) % len(B)] if b['move']['mode'] == 'loop' else B[1]
            if seg_cross(a1, a2, b1, b2):
                return True
    return False


MAX_WAIT = 4.0
MIN_WIN = 0.5


def good(st):
    return st['ok'] and st['minlen'] >= MIN_WIN and st['maxwait'] <= MAX_WAIT


def tune_speed_delay(m, prev, nxt, others, speeds, tries=4000, waits=(0.0, 0.0, 0.5, 1.0)):
    """고정 레일에서 속도/출발 지연/시작 끝을 탐색한다. prev/nxt는 확정된 발판(없으면 None)."""
    best = None
    for _ in range(tries):
        mm = json.loads(json.dumps(m))
        mm['move']['speed'] = rng.choice(speeds)
        mm['move']['delay'] = round(rng.uniform(0, 8), 2)
        mm['move']['wait'] = rng.choice(waits)
        if rng.random() < 0.5:
            mm['move']['wp'].reverse()
        if prev is not None and prev['move'] and abs(prev['move']['speed'] - mm['move']['speed']) < 0.01:
            continue        # MapEdit 23
        if prev is not None and prev['move'] and abs(prev['move']['delay'] - mm['move']['delay']) < 0.01:
            continue        # MapEdit 18
        if any(collides(mm, o) for o in others):
            continue
        s1 = pair_stats(prev, mm) if prev is not None else dict(ok=True, minlen=9, maxwait=0)
        s2 = pair_stats(mm, nxt) if nxt is not None else dict(ok=True, minlen=9, maxwait=0)
        if not (good(s1) and good(s2)):
            continue
        score = s1['maxwait'] + s2['maxwait'] - 0.2 * (s1['minlen'] + s2['minlen'])
        if best is None or score < best[0]:
            best = (score, mm, s1, s2)
    return best


def tune_chain_mover(pid, kind, prev, others, center, orients, lengths, speeds, w, tries=6000, need_cross=True):
    """이동 경로가 이전 이동 발판과 교차하고 점프 가능 시점이 있는 이동 발판을 탐색한다(MapEdit 20)."""
    best = None
    for _ in range(tries):
        ang = math.radians(rng.choice(orients))
        L = rng.choice(lengths)
        cx = center[0] + rng.uniform(-2.0, 2.0)
        cy = center[1] + rng.uniform(-2.0, 2.0)
        a = (cx + math.cos(ang) * L / 2, cy + math.sin(ang) * L / 2)
        b = (cx - math.cos(ang) * L / 2, cy - math.sin(ang) * L / 2)
        sp = rng.choice(speeds)
        if prev['move'] and abs(prev['move']['speed'] - sp) < 0.01:
            continue
        m = mover(pid, kind, [a, b], w, sp, round(rng.uniform(0, 8), 2))
        if prev['move'] and abs(prev['move']['delay'] - m['move']['delay']) < 0.01:
            continue
        if need_cross and prev['move'] and not rails_cross(prev, m):
            continue
        if any(collides(m, o) for o in others):
            continue
        st = pair_stats(prev, m)
        if not good(st):
            continue
        score = st['maxwait'] - 0.3 * st['minlen']
        if best is None or score < best[0]:
            best = (score, m, st)
    return best


def finalize_move(p):
    if p['move']:
        for q in p['move']['wp']:
            q[0], q[1] = round(q[0], 3), round(q[1], 3)
        p['cx'] = round(sum(q[0] for q in p['move']['wp']) / len(p['move']['wp']), 3)
        p['top'] = round(sum(q[1] for q in p['move']['wp']) / len(p['move']['wp']), 3)
    return p


# ---------------------------------------------------------------- 경로 길이
def ref(p):
    """기준 경로 점: 고정 발판은 윗면 중앙, 이동 발판은 이동 경로 중심(윗면)."""
    return (p['cx'], p['top'])


def path_length(seq):
    return sum(dist(ref(seq[i]), ref(seq[i + 1])) for i in range(len(seq) - 1))


def shift(plats, dx, dy):
    for p in plats:
        p['cx'] += dx
        p['top'] += dy
        if p['move']:
            for q in p['move']['wp']:
                q[0] += dx
                q[1] += dy
    return plats


def fit_last(seq, target):
    """마지막 연결 발판을 진행 방향으로 이동해 기준 경로를 정확히 target으로 맞춘다."""
    last, prev = seq[-1], seq[-2]
    base = path_length(seq[:-1])
    need = target - base
    d = (last['cx'] - prev['cx'], last['top'] - prev['top'])
    n = math.hypot(*d)
    ux, uy = d[0] / n, d[1] / n
    last['cx'] = prev['cx'] + ux * need
    last['top'] = prev['top'] + uy * need
    return need - n


def check_static_pairs(seq, label):
    issues = []
    for i in range(len(seq) - 1):
        a, b = seq[i], seq[i + 1]
        if a['move'] or b['move'] or a['kind'] == 'damage_floor' or b['kind'] == 'damage_floor':
            continue
        gx = abs(b['cx'] - a['cx']) - (a['w'] + b['w']) / 2
        dy = b['top'] - a['top']
        lo, hi = gap_min(dy), gap_max(dy)
        if not (lo - 1e-6 <= gx <= hi + 1e-6) and b.get('exempt_gap') is None:
            issues.append(f"{label}:{a['id']}->{b['id']} gap {gx:.2f} not in [{lo:.2f},{hi:.2f}] dy {dy:.2f}")
    return issues


def seq_stats(seq, label, extra_mover_pairs=True):
    out = []
    for i in range(len(seq) - 1):
        a, b = seq[i], seq[i + 1]
        if a['move'] or b['move']:
            st = pair_stats(a, b)
            out.append(dict(pair=f"{a['id']}->{b['id']}", **st))
    return out


def overlap_sweeps(seq, label):
    bad = []
    for i in range(len(seq)):
        for j in range(i + 1, len(seq)):
            if collides(seq[i], seq[j], margin=0.2):
                bad.append(f"{label}:{seq[i]['id']}~{seq[j]['id']}")
    return bad


def bbox(seq):
    xs, ys = [], []
    for p in seq:
        pts = p['move']['wp'] if p['move'] else [(p['cx'], p['top'])]
        for q in pts:
            xs += [q[0] - p['w'] / 2, q[0] + p['w'] / 2]
            ys += [q[1] - T, q[1]]
    return [min(xs), min(ys), max(xs), max(ys)]


# ================================================================ 구간 빌더
DEFAULT_STEPS = [(-5.0, -4.2), (-5.2, -4.6), (-4.8, -4.4), (-5.1, -4.3), (-5.0, -4.5), (-4.9, -4.4)]
STEEP_STEPS = [(-4.5, -8.0), (-5.0, -8.5), (-4.6, -8.2), (-5.0, -8.4), (-4.8, -8.0), (-4.7, -8.2)]


def _try_zroute(prefix, nA, nB, nC, mw=2.4, kw=6.0, start_w=3.2, a_gap=3.1, c_gap=3.1,
                speeds=(3.0, 3.5, 4.0, 4.5, 5.0), tries=3000, start_kind='normal', steps=None, c1=None,
                lengths=(8.0, 10.0, 12.0)):
    """1-2 방식: 오른쪽 점프 -> 왼쪽 아래 이동형 발판 -> 오른쪽 점프. 마지막 발판은 연결 발판 K."""
    ya = [0.0, 0.3, -0.2, 0.3, -0.2, 0.3, -0.2, 0.3]
    seq = []
    cursor, top = 0.0, 0.0
    for i in range(nA):
        w = start_w if i == 0 else (3.2 if i < nA - 1 else 2.0)
        cx = 0.0 if i == 0 else cursor + a_gap + w / 2
        top = ya[i % len(ya)] if i else 0.0
        kind = start_kind if i == 0 else 'normal'
        pid = f"{prefix}_K0" if (i == 0 and start_kind == 'start') else f"{prefix}_A{i}"
        seq.append(static(pid, kind, cx, top, w))
        cursor = cx + w / 2
    last = seq[-1]
    steps = steps or DEFAULT_STEPS
    prev = last
    for k in range(nB):
        c = (prev['cx'] + (steps[k][0] if k else steps[0][0] + 1.0), prev['top'] + (steps[k][1] if k else steps[0][1] + 0.2))
        orients = [225, 180, 135, 200, 160, 270, 90, 0]
        res = tune_chain_mover(f"{prefix}_B{k + 1}", 'moving', prev, seq, c, orients, list(lengths),
                               list(speeds), mw, tries=tries, need_cross=(k > 0))
        if res is None:
            raise RuntimeError(f"{prefix}: no mover for B{k + 1}")
        seq.append(finalize_move(res[1]))
        prev = seq[-1]
    best = None
    pc = (prev['cx'], prev['top'])
    for _ in range(2000):
        dxr, dyr = c1 or ((5.0, 11.0), (-0.8, 0.5))
        cx = pc[0] + rng.uniform(*dxr)
        top = pc[1] + rng.uniform(*dyr)
        cand = static(f"{prefix}_C1", 'normal', cx, top, 3.2)
        st = pair_stats(prev, cand)
        if not good(st) or any(collides(cand, o) for o in seq):
            continue
        sc = st['maxwait'] - 0.3 * st['minlen']
        if best is None or sc < best[0]:
            best = (sc, cand, st)
    if best is None:
        raise RuntimeError(f"{prefix}: no C1")
    seq.append(best[1])
    yc = [0.0, 0.3, -0.2, 0.3, -0.2]
    for i in range(2, nC):
        last_c = seq[-1]
        if i == nC - 1:
            cx = last_c['cx'] + last_c['w'] / 2 + c_gap + kw / 2
            seq.append(static(f"{prefix}_K", 'connector', cx, last_c['top'], kw))
        else:
            cx = last_c['cx'] + last_c['w'] / 2 + c_gap + 1.6
            seq.append(static(f"{prefix}_C{i}", 'normal', cx, last_c['top'] + yc[i % len(yc)] - yc[(i - 1) % len(yc)], 3.2))
    return seq


def stretch_zroute(seq, nA, nC, target):
    """A·C 구간 고정 발판 간격을 균등하게 벌려 기준 경로를 target에 맞춘다(이동형 쌍의 상대 위치는 유지)."""
    length = path_length(seq)
    nB = sum(1 for p in seq if p['move'])
    adj = (nA - 1) + (nC - 2)
    d = (target - length) / adj
    idx_b0 = nA
    idx_c1 = nA + nB
    for j, p in enumerate(seq):
        if j < nA:
            sh = j * d
            p['cx'] += sh
        elif j < idx_c1:
            sh = (nA - 1) * d
            p['cx'] += sh
            if p['move']:
                for q in p['move']['wp']:
                    q[0] += sh
        else:
            sh = (nA - 1) * d + (j - idx_c1) * d
            p['cx'] += sh
    return d


def retrying(fn, *a, attempts=40, **kw):
    last = None
    for _ in range(attempts):
        try:
            return fn(*a, **kw)
        except RuntimeError as e:
            last = e
    raise last


def build_zroute(*a, **kw):
    return retrying(_try_zroute, *a, **kw)


def _skeleton_jungle(prefix, spec, mw=2.4):
    """spec: ('N',w,gap,dy,kind) | ('M',kind,rail) 순서 목록으로 2-2 방식 발판열을 만든다."""
    seq, cursor, top = [], 0.0, 0.0
    first = True
    pending = None   # 직전 이동형 레일 끝 위치
    cnt = {}
    for item in spec:
        if item[0] == 'N':
            _, w, gap, dy, kind = item
            cnt[kind] = cnt.get(kind, 0) + 1
            nid = {'normal': 'N', 'disappearing': 'D', 'connector': 'K', 'start': 'K0', 'damage': 'X', 'blink': 'B'}[kind]
            nid = f"{prefix}_{nid}{cnt[kind] if kind not in ('connector', 'start') else ''}"
            if first:
                cx = 0.0
                first = False
            elif pending is not None:
                cx = pending[0] + mw / 2 + 2.2 + w / 2
                top = pending[1]
                pending = None
            else:
                cx = cursor + gap + w / 2
                top += dy
            seq.append(static(nid, kind, cx, top, w))
            cursor = cx + w / 2
        else:
            _, kind, rail = item
            cnt['M'] = cnt.get('M', 0) + 1
            x0, y0 = cursor + 2.2 + mw / 2, top
            if rail[0] == 'line':
                _, dx, dy = rail
                wp = [(x0, y0), (x0 + dx, y0 + dy)]
                m = mover(f"{prefix}_M{cnt['M']}", 'moving', wp, mw, 3.0, 0.0)
                pending = (x0 + dx, y0 + dy)
            else:   # ('loop', [(dx,dy),...], exit_index)
                _, pts, ex = rail
                wp = [(x0 + p[0], y0 + p[1]) for p in pts]
                m = mover(f"{prefix}_M{cnt['M']}", 'moving', wp, mw, 3.0, 0.0, mode='loop')
                pending = wp[ex]
            seq.append(m)
    return seq


def _try_jungle(prefix, spec, speeds=(2.5, 3.0, 3.5, 4.0, 4.5, 5.0), tries=3000):
    seq = _skeleton_jungle(prefix, spec)
    for i, p in enumerate(seq):
        if not p['move']:
            continue
        prev = seq[i - 1]
        nxt = seq[i + 1] if i + 1 < len(seq) and not seq[i + 1]['move'] else None
        res = tune_speed_delay(p, prev, nxt, [q for q in seq[:i]], list(speeds), tries=tries)
        if res is None:
            raise RuntimeError(f"{prefix}: no timing for {p['id']}")
        seq[i] = finalize_move(res[1])
    return seq


def build_jungle(prefix, spec, **kw):
    return retrying(_try_jungle, prefix, spec, **kw)


JUNGLE_FULL = [
    ('N', 6.0, 0, 0, 'start'), ('N', 1.6, 3.5, 0, 'normal'), ('N', 2.4, 3.5, 0, 'normal'),
    ('M', 'horizontal', ('line', 6.0, 0.0)),
    ('N', 2.0, 0, 0, 'normal'),
    ('M', 'vertical', ('line', 0.0, 3.2)),
    ('N', 2.0, 0, 0, 'normal'),
    ('M', 'diagonal', ('line', 6.0, -2.5)),
    ('N', 2.0, 0, 0, 'normal'),
    ('M', 'L-loop', ('loop', [(0, 0), (4.0, 0), (4.0, 2.5)], 2)),
    ('N', 2.0, 0, 0, 'normal'),
    ('M', 'long-diagonal', ('line', 7.0, -1.7)),
    ('N', 2.4, 0, 0, 'normal'),
    ('N', 1.0, 1.6, 0, 'disappearing'), ('N', 1.0, 1.6, 0, 'disappearing'),
    ('N', 1.0, 1.6, 0, 'disappearing'), ('N', 1.0, 1.6, 0, 'disappearing'),
    ('N', 2.4, 1.6, 0, 'normal'),
    ('M', 'square-loop', ('loop', [(0, 0), (3.5, 0), (3.5, 2.5), (0, 2.5)], 1)),
    ('N', 2.0, 0, 0, 'normal'),
    ('N', 2.4, 3.5, 0, 'normal'),
    ('N', 6.0, 3.5, 0, 'connector'),
]


# ---------------------------------------------------------------- 3-2 방식 하강
GAP_E = 4.5      # 승강기와 고정 발판의 간격
DROP_IN = 1.5    # 이전 발판에서 이동형으로 내려가는 낙차
DROP_OUT = 2.0   # 승강기 하단에서 다음 발판으로 내려가는 낙차
GAP_S = 4.2      # 하강 고정 발판 사이 간격(낙차 1.5)


def _skeleton_descent(prefix, items, mw=2.4, lift_len=14.0):
    """items: ('S',w) | ('N',w,kind) | ('H',dx) | ('V',dx,dy) | ('LIFT',) | ('K',w)"""
    seq, d, cursor, top, pending = [], 1, 0.0, 0.0, None
    cnt = {}
    tags = {'damage': 'X', 'blink': 'B', 'disappearing': 'D', 'normal': 'N'}

    def nid(tag):
        cnt[tag] = cnt.get(tag, 0) + 1
        return f"{prefix}_{tag}{cnt[tag]}"

    def place(it, after_lift):
        nonlocal cursor, top, pending
        k, w = it[0], it[1]
        kind = {'S': 'start', 'K': 'connector'}.get(k) or it[2]
        if k == 'S':
            cx, top = 0.0, 0.0
            pid = f"{prefix}_K0"
        else:
            if after_lift:
                cx = after_lift[0] + d * (mw / 2 + GAP_E + w / 2)
                top = after_lift[1] - DROP_OUT
            elif pending is not None:
                cx = pending[0] + d * (mw / 2 + 2.2 + w / 2)
                top = pending[1] - DROP_IN
                pending = None
            else:
                cx = cursor + d * (GAP_S + w / 2)
                top -= 1.5
            pid = f"{prefix}_K" if k == 'K' else nid(tags[kind])
        seq.append(static(pid, kind, cx, top, w))
        cursor = cx + d * w / 2

    skip_next_lift = None
    for it in items:
        k = it[0]
        if k in ('S', 'N', 'K'):
            place(it, skip_next_lift)
            skip_next_lift = None
        elif k in ('H', 'V'):
            dx = it[1]
            dyv = 0.0 if k == 'H' else it[2]
            x0, y0 = cursor + d * (2.2 + mw / 2), top - DROP_IN
            seq.append(mover(nid(k), 'moving', [(x0, y0), (x0 + d * dx, y0 + dyv)], mw, 3.0, 0.0))
            pending = (x0 + d * dx, y0 + dyv)
            top = y0 + dyv
        elif k == 'LIFT':
            x = cursor + d * (GAP_E + mw / 2)
            y0 = top - DROP_IN
            seq.append(mover(nid('E'), 'moving', [(x, y0), (x, y0 - lift_len)], mw, 3.0, 0.0))
            d = -d
            skip_next_lift = (x, y0 - lift_len)
    return seq


def _try_descent(prefix, items, lift_len, speeds=(3.0, 3.5, 4.0, 4.5, 5.0), tries=2500):
    seq = _skeleton_descent(prefix, items, lift_len=lift_len)
    for i, p in enumerate(seq):
        if not p['move']:
            continue
        prev = seq[i - 1]
        nxt = seq[i + 1] if i + 1 < len(seq) and not seq[i + 1]['move'] else None
        res = tune_speed_delay(p, prev, nxt, seq[:i], list(speeds), tries=tries)
        if res is None:
            raise RuntimeError(f"{prefix}: no timing for {p['id']}")
        seq[i] = finalize_move(res[1])
    return seq


def build_descent(prefix, items, target=None, lift_len=14.0, **kw):
    if target is not None:   # 승강기 길이를 이분 탐색해 기준 경로를 target에 맞춘다
        lo, hi = 2.0, 40.0
        for _ in range(40):
            mid = (lo + hi) / 2
            L = path_length(_skeleton_descent(prefix, items, lift_len=mid))
            if L < target:
                lo = mid
            else:
                hi = mid
        lift_len = round((lo + hi) / 2, 3)
    return retrying(_try_descent, prefix, items, lift_len, **kw), lift_len


DESCENT_FULL = [
    ('S', 6.0), ('N', 2.0, 'damage'), ('N', 1.6, 'blink'), ('LIFT',),
    ('N', 2.0, 'damage'), ('N', 1.6, 'blink'), ('V', 8.0, -6.0), ('N', 1.6, 'blink'), ('LIFT',),
    ('N', 2.0, 'damage'), ('N', 1.6, 'blink'), ('H', 8.0), ('K', 6.0),
]


F2_SPEC = [
    ('N', 4.0, 0, 0, 'start'),
    ('M', 'horizontal', ('line', 4.0, 0.0)), ('N', 2.0, 0, 0, 'damage'),
    ('M', 'vertical', ('line', 0.0, 3.2)), ('N', 1.2, 0, 0, 'disappearing'),
    ('N', 1.2, 1.6, 0, 'disappearing'), ('N', 4.0, 3.5, 0, 'connector'),
]
F3_ITEMS = [
    ('S', 4.0), ('N', 2.0, 'damage'), ('LIFT',), ('N', 1.2, 'disappearing'), ('K', 4.0),
]
# 2번째 이후 연결 발판 폭은 6, 5구간 내부 하위 구간 연결은 4


# ================================================================ 조립
def world(seq, origin):
    dx, dy = origin[0] - seq[0]['cx'], origin[1] - seq[0]['top']
    return shift(seq, dx, dy)


def runway(pid, x0, x1, top, kind='damage_floor'):
    return static(pid, kind, (x0 + x1) / 2, top, x1 - x0)


def flyer_clear(pt, plats, pad=1.6):
    for p in plats:
        pts = p['move']['wp'] if p['move'] else [(p['cx'], p['top'])]
        xs = [q[0] for q in pts]
        ys = [q[1] for q in pts]
        if (min(xs) - p['w'] / 2 - pad <= pt[0] <= max(xs) + p['w'] / 2 + pad and
                min(ys) - T - pad <= pt[1] <= max(ys) + pad):
            return False
    return True


def place_flyer(near, plats, rngl, dxs=(0.0,), prefer_up=True):
    for n in range(4000):
        spread = 3 + n / 400
        pt = (near[0] + rngl.uniform(-spread, spread), near[1] + rngl.uniform(-spread + 2, spread + 4))
        if flyer_clear(pt, plats, pad=2.0):
            return (round(pt[0], 2), round(pt[1], 2))
    raise RuntimeError('no flyer spot')


def stretch_to(seq, nA, nC, target):
    for _ in range(8):
        if abs(path_length(seq) - target) < 1e-4:
            break
        stretch_zroute(seq, nA, nC, target)


def monster(mid, pattern, x, y, group=None, **kw):
    d = dict(id=mid, pattern=pattern, x=round(x, 2), y=round(y, 2), group=group)
    d.update(kw)
    return d


def assemble():
    parts = {}
    # ---- P1 (1-2 방식) : ★ = S1_A0
    s1 = build_zroute('S1', 6, 5, 5, start_kind='start', start_w=3.2, steps=STEEP_STEPS,
                      c1=((11.5, 14.0), (-12.0, -7.0)), lengths=(10.0, 12.0, 14.0), tries=6000)
    stretch_to(s1, 6, 5, SEG_LEN)
    s1[0]['id'] = 'S1_Start'
    parts['P1'] = dict(title='1-2 방식 - Z 점프 이동', plats=s1, monsters=[])
    # ---- P2 (2-2 방식)
    s2 = build_jungle('S2', JUNGLE_FULL)
    fit_last(s2, SEG_LEN)
    parts['P2'] = dict(title='2-2 방식 - 정글 짐', plats=world(s2, ref(s1[-1])), monsters=[])
    # ---- P3 (3-2 방식)
    s3, lift = build_descent('S3', DESCENT_FULL, target=SEG_LEN)
    fit_last(s3, SEG_LEN)
    parts['P3'] = dict(title='3-2 방식 - 용암 갱도 하강', plats=world(s3, ref(parts['P2']['plats'][-1])), monsters=[], lift=lift)
    # ---- P4 (4-2 방식) : 일직선 대미지 질주
    k3 = parts['P3']['plats'][-1]
    k4 = static('S4_K', 'connector', k3['cx'] + SEG_LEN, k3['top'], 6.0)
    run = runway('S4_Runway', k3['cx'] + 3.0, k4['cx'] - 3.0, k3['top'])
    run['id'] = 'S4_Runway'
    mons4 = []
    for i in range(15):
        mons4.append(monster(f"Monster_5-2_S4_{i + 1:02d}", 'FlyingChasePattern', k3['cx'] + 8 + 7 * i,
                             k3['top'] + (2.2 if i % 2 == 0 else 3.0), group=1 if i < 8 else 2,
                             drops='heal+speed 100%'))
    parts['P4'] = dict(title='4-2 방식 - 대미지 질주', plats=[k3, run, k4], monsters=mons4,
                       groups=[dict(group=1, startX=0, count=8), dict(group=2, startX=60, count=7)])
    # ---- P5 (융합) : F1 Z 점프 / F2 정글+용암 / F3 하강+소멸 / F4 반환 질주
    f1 = build_zroute('S5F1', 2, 2, 3, start_w=6.0, kw=4.0, start_kind='start')
    f1 = world(f1, ref(k4))
    f2 = build_jungle('S5F2', F2_SPEC)
    f2 = world(f2, ref(f1[-1]))
    f3, _ = build_descent('S5F3', F3_ITEMS, lift_len=6.0)
    f3 = world(f3, ref(f2[-1]))
    # F4: F3 끝(왼쪽 방향)에서 왼쪽으로 달리는 질주. 총 기준 경로 120에 맞게 길이를 계산
    used = path_length(f1) + path_length(f2) + path_length(f3)
    r4 = SEG_LEN - used - 3.0 - 3.0     # K(4) 반폭 2 + 종료 K(6) 반폭 3 -> 아래에서 보정
    k_f3 = f3[-1]
    # 시작 K(폭 4) 중심 -> 종료 K(폭 6) 중심 거리 = 2 + R + 3
    R = SEG_LEN - used - 5.0
    f4_run = runway('S5F4_Runway', k_f3['cx'] - 2.0 - R, k_f3['cx'] - 2.0, k_f3['top'])
    k5 = static('S5_K', 'goal', k_f3['cx'] - 2.0 - R - 3.0, k_f3['top'], 6.0)
    parts['P5'] = dict(title='5-2 융합 구간', plats=f1 + f2[1:] + f3[1:] + [f4_run, k5], monsters=[],
                       phases=dict(F1=f1, F2=f2, F3=f3), runwayF4=R, k4=k4)
    return parts


def all_platforms(parts):
    seen, out = set(), []
    for key in ('P1', 'P2', 'P3', 'P4', 'P5'):
        for p in parts[key]['plats']:
            if id(p) in seen:
                continue
            seen.add(id(p))
            out.append((key, p))
    return out


def polyline_point(seq, frac):
    pts = [ref(p) for p in seq]
    segs = [dist(pts[i], pts[i + 1]) for i in range(len(pts) - 1)]
    s_ = sum(segs) * frac
    for i, L in enumerate(segs):
        if s_ <= L:
            f = s_ / L
            return (pts[i][0] + (pts[i + 1][0] - pts[i][0]) * f, pts[i][1] + (pts[i + 1][1] - pts[i][1]) * f)
        s_ -= L
    return pts[-1]


def place_monsters(parts):
    rm = random.Random(5252)
    all_pl = [p for _, p in all_platforms(parts)]
    # P3: 3-2 방식 비행 몬스터 5체
    pats = [('FlyingPatrolPattern', dict(areaSize=(4.2, 1.6))), ('FlyingRandomWanderPattern', dict(areaSize=(4.5, 3.5))),
            ('FlyingPathPatrolPattern', dict(pathLength=3.0)), ('FlyingChasePattern', dict(orbit=2.0, detect=(8, 4))),
            ('FlyingChasePattern', dict(orbit=2.0, detect=(8, 4)))]
    seq3 = parts['P3']['plats']
    for i, f in enumerate([0.10, 0.30, 0.50, 0.70, 0.90]):
        near = polyline_point(seq3, f)
        pt = place_flyer(near, all_pl, rm)
        parts['P3']['monsters'].append(monster(f"Monster_5-2_S3_F{i + 1}", pats[i][0], pt[0], pt[1], drops='기존 비행 몬스터 기본 드롭', **pats[i][1]))
    # P5
    m5 = []
    f1 = parts['P5']['phases']['F1']
    for i, f in enumerate([0.35, 0.60, 0.85]):
        near = polyline_point(f1, f)
        pt = place_flyer(near, all_pl, rm)
        m5.append(monster(f"Monster_5-2_S5F1_{i + 1}", 'FlyingChasePattern', pt[0], pt[1], group=1, drops='heal+speed 100%',
                          orbit=0.35, detect=(24, 10)))
    f3 = parts['P5']['phases']['F3']
    pats5 = [('FlyingPatrolPattern', dict(areaSize=(4.2, 1.6))), ('FlyingRandomWanderPattern', dict(areaSize=(4.5, 3.5))),
             ('FlyingPathPatrolPattern', dict(pathLength=3.0)), ('FlyingChasePattern', dict(orbit=2.0, detect=(8, 4)))]
    for i, f in enumerate([0.15, 0.40, 0.65, 0.88]):
        near = polyline_point(f3, f)
        pt = place_flyer(near, all_pl, rm)
        m5.append(monster(f"Monster_5-2_S5F3_{i + 1}", pats5[i][0], pt[0], pt[1], group=2, drops='기존 비행 몬스터 기본 드롭', **pats5[i][1]))
    run = parts['P5']['plats'][-2]
    x_right = run['cx'] + run['w'] / 2
    for i in range(4):
        x = x_right - run['w'] * (i + 0.5) / 4
        m5.append(monster(f"Monster_5-2_S5F4_{i + 1}", 'FlyingChasePattern', x, run['top'] + (2.2 if i % 2 == 0 else 3.0), group=3,
                          drops='heal+speed 100%', orbit=0.35, detect=(24, 10)))
    parts['P5']['monsters'] = m5


def validate(parts):
    res = dict(lengths={}, static_issues=[], mover_pairs=[], overlaps=[], monsters_in_platform=[])
    for k in ('P1', 'P2', 'P3', 'P4', 'P5'):
        pl = parts[k]['plats']
        res['lengths'][k] = round(path_length(pl), 3)
        res['static_issues'] += check_static_pairs(pl, k)
        for r in seq_stats(pl, k):
            res['mover_pairs'].append(dict(part=k, **r))
    allp = [(k, p) for k, p in all_platforms(parts) if not p['id'].endswith('_K0')]
    for i in range(len(allp)):
        for j in range(i + 1, len(allp)):
            a, b = allp[i][1], allp[j][1]
            ba, bb = bbox([a]), bbox([b])
            if ba[2] < bb[0] - 0.2 or bb[2] < ba[0] - 0.2 or ba[3] < bb[1] - 0.2 or bb[3] < ba[1] - 0.2:
                continue
            if a['move'] or b['move']:
                if collides(a, b, margin=0.2):
                    res['overlaps'].append([a['id'], b['id']])
            elif ba[2] > bb[0] + 0.01 and bb[2] > ba[0] + 0.01 and ba[3] > bb[1] + 0.01 and bb[3] > ba[1] + 0.01:
                res['overlaps'].append([a['id'], b['id']])
    for k in ('P3', 'P5'):
        for m in parts[k]['monsters']:
            if m['id'].startswith('Monster_5-2_S5F4'):
                continue
            if not flyer_clear((m['x'], m['y']), [p for _, p in all_platforms(parts)], pad=1.0):
                res['monsters_in_platform'].append(m['id'])
    res['moverRules'] = []
    for _, p in all_platforms(parts):
        if p['move']:
            res['moverRules'].append(dict(id=p['id'], rail=round(rail_len(p), 2), maxRail=56.0, ok=rail_len(p) <= 56.0))
    strips = make_strips(parts)
    res['fallTriggers'] = strips
    stats, shortcuts, leaks = fall_check(parts, strips, skip=4)
    res['fallSim'] = dict(trajectories=stats['traj'], landed=stats['landed'], killed=stats['killed'],
                          skip4plus=len(shortcuts), leaks=len(leaks))
    res['routeJumpMissing'] = route_check(parts, strips)
    res['stripOverlap'] = []
    for st in strips:
        for _, p in all_platforms(parts):
            e = rail_extent(p)
            if e[0] < st['x1'] and e[1] > st['x0'] and e[3] > st['y'] - 0.5 and e[2] < st['y'] + 0.5:
                res['stripOverlap'].append([st['id'], p['id']])
    res['ok'] = bool(all(abs(v - SEG_LEN) < 1e-3 for v in res['lengths'].values())
                     and not shortcuts and not leaks and not res['routeJumpMissing'] and not res['stripOverlap'] and not res['static_issues'] and not res['overlaps']
                     and all(good(m) for m in res['mover_pairs']) and not res['monsters_in_platform']
                     and all(r['ok'] for r in res['moverRules']))
    return res


KIND_KO = {'start': '★ 시작', 'normal': '일반', 'connector': '연결', 'goal': '○ 종료', 'damage': '피해', 'blink': '점멸',
           'disappearing': '소멸', 'moving': '이동형', 'damage_floor': '피해 바닥'}


def move_desc(p):
    m = p['move']
    if m['mode'] == 'loop':
        return '순환 ' + ' → '.join(f"({q[0]:.1f}, {q[1]:.1f})" for q in m['wp']) + ' → 처음'
    return f"({m['wp'][0][0]:.1f}, {m['wp'][0][1]:.1f}) ↔ ({m['wp'][1][0]:.1f}, {m['wp'][1][1]:.1f})"


def write_outputs(parts, res):
    origin = ref(parts['P1']['plats'][0])

    def rel(v):
        return round(v, 3)

    j = dict(status='design_only', date='2026-10-15', segmentLength=15, totalLength=75, unitsPerScale=UNIT,
             start=[0, 0], validation=res, parts=[])
    for k in ('P1', 'P2', 'P3', 'P4', 'P5'):
        pt = parts[k]
        plats = []
        for p in pt['plats']:
            mv = None
            if p['move']:
                mv = dict(p['move'])
                mv['wp'] = [[rel(q[0] - origin[0]), rel(q[1] - origin[1])] for q in p['move']['wp']]
            plats.append(dict(id=p['id'], kind=p['kind'], cx=rel(p['cx'] - origin[0]), top=rel(p['top'] - origin[1]),
                              width=round(p['w'], 3), move=mv))
        j['parts'].append(dict(id=k, title=pt['title'], pathLength=round(path_length(pt['plats']), 3), platforms=plats,
                               monsters=[dict(m, x=rel(m['x'] - origin[0]), y=rel(m['y'] - origin[1])) for m in pt['monsters']]))
    with open(os.path.join(HERE, 'MapDesign_Stage5-2.json'), 'w', encoding='utf-8') as f:
        json.dump(j, f, ensure_ascii=False, indent=2)
    L = ['# 5-2 발판 좌표표', '',
         '`Generate_Stage5-2.py`가 생성한 파일이다. 좌표 기준은 ★ 시작 발판 윗면 중앙 (0, 0)이며 단위는 Unity 유닛이다.',
         '이동형은 경로 중심을 발판 위치로 표기하고 경로 점·속도·출발 지연·끝점 대기를 함께 적는다. 두께는 모두 0.5(Tiled 기준)다.', '']
    for k in ('P1', 'P2', 'P3', 'P4', 'P5'):
        pt = parts[k]
        L += [f"## {k} — {pt['title']}", '', f"기준 경로 {path_length(pt['plats']):.3f}", '',
              '| # | 이름 | 종류 | X | 윗면 Y | 폭 | 이동 경로 / 설정 |', '|---:|---|---|---:|---:|---:|---|']
        for i, p in enumerate(pt['plats']):
            mv = ''
            if p['move']:
                m = p['move']
                mv = (f"{move_desc_rel(p, origin)} · 속도 {m['speed']} · 지연 {m['delay']}s · 끝점 대기 {m['wait']}s · 경로 {rail_len(p):.1f}")
            kind_ko = KIND_KO.get(p['kind'], p['kind'])
            if p['id'].endswith('_K0') and k != 'P1':
                kind_ko = '연결(앞 구간 종료 발판 공유)'
            L.append(f"| {i} | `{p['id']}` | {kind_ko} | {p['cx'] - origin[0]:.2f} | {p['top'] - origin[1]:.2f} | {p['w']:.2f} | {mv} |")
        if pt['monsters']:
            L += ['', '| 몬스터 | 패턴 | X | Y | 비고 |', '|---|---|---:|---:|---|']
            for m in pt['monsters']:
                extra = ', '.join(f"{a}={m[a]}" for a in ('areaSize', 'pathLength', 'orbit', 'detect') if a in m)
                L.append(f"| `{m['id']}` | {m['pattern']} | {m['x'] - origin[0]:.2f} | {m['y'] - origin[1]:.2f} | {m.get('drops', '')}{' · ' + extra if extra else ''} |")
        L.append('')
    L += ['## 추락 판정 제안', '', '가로 띠(두께 1) 형태의 판정이다. 좌표는 ★ 기준이다.', '',
          '| 이름 | X 시작 | X 끝 | Y | 목적 |', '|---|---:|---:|---:|---|']
    for st in res['fallTriggers']:
        L.append(f"| `{st['id']}` | {st['x0'] - origin[0]:.1f} | {st['x1'] - origin[0]:.1f} | {st['y'] - origin[1]:.1f} | {st['note']} |")
    L.append('')
    with open(os.path.join(HERE, 'MapDesign_Stage5-2_Tables.md'), 'w', encoding='utf-8') as f:
        f.write('\n'.join(L))
    return j


def move_desc_rel(p, origin):
    m = p['move']
    pts = [(q[0] - origin[0], q[1] - origin[1]) for q in m['wp']]
    if m['mode'] == 'loop':
        return '순환 ' + ' → '.join(f"({q[0]:.1f}, {q[1]:.1f})" for q in pts) + ' → 처음'
    return f"({pts[0][0]:.1f}, {pts[0][1]:.1f}) ↔ ({pts[1][0]:.1f}, {pts[1][1]:.1f})"


# ================================================================ 추락 판정 제안
def rail_extent(p):
    pts = p['move']['wp'] if p['move'] else [(p['cx'], p['top'])]
    return (min(q[0] for q in pts) - p['w'] / 2, max(q[0] for q in pts) + p['w'] / 2,
            min(q[1] for q in pts) - T, max(q[1] for q in pts))


def make_strips(parts):
    """층/구간 사이 추락 판정(가로 띠). 정상 점프 궤적과 이동형 탑승 구간을 피하도록 위치를 계산한다."""
    def pid(part, name):
        return next(p for p in parts[part]['plats'] if p['id'] == name)
    strips = []
    # P1: A 구간 밑
    p1 = parts['P1']['plats']
    A = [p for p in p1 if p['id'] == 'S1_Start' or p['id'].startswith('S1_A')]
    B = [p for p in p1 if p['move']]
    c_top = max(p['top'] for p in p1 if p['id'].startswith('S1_C') or p['id'] == 'S1_K')
    a_bottom = min(p['top'] for p in A) - T
    b1_left = rail_extent(B[0])[0]
    strips.append(dict(id='P1_A_under', x0=round(min(p['cx'] - p['w'] / 2 for p in A) - 18.0, 1), x1=round(b1_left - 0.8, 1),
                       y=round(a_bottom - 3.5, 1), note='A 구간 아래로 떨어져 이동형 발판 중간으로 끼어드는 우회 방지'))
    # P1: 이동형 계단 띠 - 각 이동형 발판 아래, 그보다 아래 이동형 발판들의 오른쪽 끝 바깥
    ext = [rail_extent(b) for b in B]
    for k in range(len(B) - 1):
        r = max(e[1] for e in ext[k:])
        strips.append(dict(id=f'P1_step_B{k + 1}', x0=round(r + 0.3, 1), x1=round(parts['P1']['plats'][-1]['cx'] + 4.0, 1),
                           y=round(ext[k][2] - 2.0, 1), note=f'B{k + 1} 아래 오른쪽으로 떨어져 아래 구간으로 건너뛰는 우회 방지'))
    k1 = parts['P1']['plats'][-1]
    strips.append(dict(id='P1_floor', x0=-26.0, x1=round(k1['cx'] + 4.0, 1), y=round(c_top - 6.0, 1), note='1-2 방식 구간 바닥'))
    # P2 바닥
    p2 = [p for p in parts['P2']['plats'] if not p['id'].endswith('_K0')]
    p3 = parts['P3']['plats']
    e1 = next(p for p in p3 if p['id'] == 'S3_E1')
    e2 = next(p for p in p3 if p['id'] == 'S3_E2')
    ex1, ex2 = rail_extent(e1), rail_extent(e2)
    floor2 = [p for p in p3 if p['id'] in ('S3_X2', 'S3_B2', 'S3_V1', 'S3_B3')]
    floor3 = [p for p in p3 if p['id'] in ('S3_X3', 'S3_B4', 'S3_H1')]
    d_x0 = round(ex2[1] + 1.6, 1)
    strips.append(dict(id='P2_floor', x0=-26.0, x1=round(d_x0 + 1.0, 1), y=round(min(c_top, min(p['top'] for p in p2)) - 6.0, 1),
                       note='2-2 방식 구간 바닥(3-2 방식 구간 앞까지)'))
    f1_low = ex1[3]
    f2_high = max(p['top'] for p in floor2)
    strips.append(dict(id='P3_D', x0=d_x0, x1=round(ex1[0] - 1.2, 1), y=round((f1_low + f2_high) / 2, 1),
                       note='1층→2층 사이. E1 승강기가 아래로 내려간 뒤에만 내릴 수 있게 함'))
    f2_low = min(rail_extent(p)[2] for p in floor2)
    f3_high = max(p['top'] for p in floor3)
    strips.append(dict(id='P3_E', x0=d_x0, x1=round(ex1[1] + 17.0, 1), y=round(f2_low - 4.0, 1),
                       note='2층→3층 사이. E1 아래 3층·질주 구간으로의 낙하 우회도 함께 차단'))
    run = next(p for p in parts['P4']['plats'] if p['id'] == 'S4_Runway')
    k4 = parts['P4']['plats'][-1]
    strips.append(dict(id='P4_floor', x0=round(ex2[0] - 16.0, 1), x1=round(k4['cx'] - 3.0 + 2.0, 1), y=round(run['top'] - 4.7, 1),
                       note='3-2 방식 3층 / 4-2 방식 질주 구간 바닥'))
    f = parts['P5']['phases']
    f2k = f['F2'][-1]
    f2_low_top = min(p['top'] for p in f['F2'])
    f3x1 = next(p for p in f['F3'] if p['id'] == 'S5F3_X1')
    f1k = f['F1'][-1]
    strips.append(dict(id='P5_G', x0=round(f1k['cx'] + 2.0, 1), x1=round(f3x1['cx'] - 1.0, 1), y=round(f2_low_top - 4.6, 1),
                       note='융합 F2(정글)에서 F4 질주로 곧장 떨어지는 우회 방지'))
    f3e = next(p for p in f['F3'] if p['id'] == 'S5F3_E1')
    lowest = min(rail_extent(p)[2] for p in parts['P5']['plats'])
    a1 = next(p for p in f['F1'] if p['id'] == 'S5F1_A1')
    b1 = next(p for p in f['F1'] if p['id'] == 'S5F1_B1')
    m1 = next(p for p in f['F2'] if p['id'] == 'S5F2_M1')
    strips.append(dict(id='P5_F1_under', x0=round(rail_extent(b1)[1] + 1.1, 1), x1=round(rail_extent(m1)[1] + 1.2, 1),
                       y=round(a1['top'] - 4.5, 1), note='융합 F1의 오른쪽 점프 발판 아래 낙하 우회 방지'))
    strips.append(dict(id='P5_floor', x0=round(min(rail_extent(p)[0] for p in f['F1']) - 20.0, 1),
                       x1=round(rail_extent(f3e)[1] + 20.0, 1), y=round(lowest - 8.0, 1), note='융합 구간 바닥'))
    return strips


# ================================================================ 추락 우회 검사
BODY_W, BODY_H = 0.98, 1.29


def ordered_platforms(parts):
    return [p for _, p in all_platforms(parts) if not p['id'].endswith('_K0')]


def fall_check(parts, strips, skip=3, dt=0.02, tmax=5.0):
    """모든 발판 끝에서 걸어 떨어지기/점프(좌우 속도 다양)했을 때 착지 발판을 시뮬레이션한다.
    착지 발판이 기준 순서상 skip개 이상 앞서면 우회, 추락 판정 없이 무한 낙하하면 누락으로 센다."""
    plats = ordered_platforms(parts)
    n = len(plats)
    steps = int(tmax / dt)
    tt = np.arange(steps + 1) * dt
    shortcuts, leaks, stats = [], [], dict(traj=0, landed=0, killed=0)
    for si, src in enumerate(plats):
        t0s = [0.0] if not src['move'] else list(np.linspace(10.0, 10.0 + max(period(src), 1.0), 5, endpoint=False))
        for t0 in t0s:
            X = np.zeros((n, steps + 1))
            Y = np.zeros((n, steps + 1))
            for pi, p in enumerate(plats):
                x, y = traj(p, t0 + tt)
                X[pi], Y[pi] = x, y
            W = np.array([p['w'] for p in plats])[:, None] / 2
            sx, sy = X[si, 0], Y[si, 0]
            for edge in (-1, 1):
                for vx in (-8.0, -4.0, 0.0, 4.0, 8.0):
                    for jump in (False, True):
                        bx0 = sx + edge * src['w'] / 2
                        bx = bx0 + vx * tt
                        vy0 = JV if jump else 0.0
                        feet = sy + vy0 * tt - 0.5 * G * tt * tt
                        vy = vy0 - G * tt
                        # 착지 판정
                        top = Y
                        cross = (feet[:-1] >= top[:, :-1] - 1e-6) & (feet[1:] <= top[:, 1:]) & (vy[1:] < 0)
                        over = (bx[1:] + BODY_W / 2 > X[:, 1:] - W) & (bx[1:] - BODY_W / 2 < X[:, 1:] + W)
                        hit = cross & over
                        hit[si, :] &= (tt[1:] > 0.15)
                        land_k, land_i = steps + 1, None
                        if hit.any():
                            ks = np.where(hit.any(axis=0))[0]
                            land_k = ks[0] + 1
                            land_i = int(np.where(hit[:, ks[0]])[0][0])
                        kill_k = steps + 1
                        for st in strips:
                            ov = ((bx + BODY_W / 2 > st['x0']) & (bx - BODY_W / 2 < st['x1']) &
                                  (feet + BODY_H > st['y'] - 0.5) & (feet < st['y'] + 0.5))
                            if ov.any():
                                kill_k = min(kill_k, int(np.argmax(ov)))
                        stats['traj'] += 1
                        if kill_k <= land_k and kill_k <= steps:
                            stats['killed'] += 1
                        elif land_i is not None:
                            stats['landed'] += 1
                            if land_i - si >= skip:
                                shortcuts.append((src['id'], plats[land_i]['id'], round(t0, 1), edge, vx, jump))
                        else:
                            leaks.append((src['id'], round(t0, 1), edge, vx, jump))
    return stats, shortcuts, leaks


def route_check(parts, strips, dt=0.02, tmax=3.0):
    """정상 진행 쌍(앞 발판 -> 다음 발판)마다 다음 발판에 처음 착지하는 점프 궤적이 존재하는지 확인한다.
    추락 판정이 정상 점프를 막지 않는지와 물리 도달 가능성을 함께 본다(이동형은 여러 시점에서 표본)."""
    plats = ordered_platforms(parts)
    steps = int(tmax / dt)
    tt = np.arange(steps + 1) * dt
    bad = []
    seqs = []
    for key in ('P1', 'P2', 'P3', 'P4', 'P5'):
        sq = [p for p in parts[key]['plats']]
        seqs.append(sq)
    pairs = []
    for sq in seqs:
        for i in range(len(sq) - 1):
            a, b = sq[i], sq[i + 1]
            if a['kind'] == 'damage_floor' or b['kind'] == 'damage_floor':
                continue
            pairs.append((a, b))
    n = len(plats)
    for a, b in pairs:
        def find(q):
            for i, p in enumerate(plats):
                if p['id'] == q['id'] or (abs(p['cx'] - q['cx']) < 1e-6 and abs(p['top'] - q['top']) < 1e-6 and p['w'] == q['w']):
                    return i
            return None
        ai, bi = find(a), find(b)
        if ai is None or bi is None:
            continue
        t0s = [0.0] if not (a['move'] or b['move']) else list(np.linspace(10.0, 10.0 + max(period(a), period(b), 1.0), 40, endpoint=False))
        ok = False
        for t0 in t0s:
            X = np.zeros((n, steps + 1))
            Y = np.zeros((n, steps + 1))
            for pi, p in enumerate(plats):
                X[pi], Y[pi] = traj(p, t0 + tt)
            W = np.array([p['w'] for p in plats])[:, None] / 2
            sx, sy = X[ai, 0], Y[ai, 0]
            for ox in (-0.8, -0.4, 0.0, 0.4, 0.8):
                bx0 = sx + ox * a['w'] / 2 * 1.0
                for vx in np.arange(-8.0, 8.01, 1.0):
                    bx = bx0 + vx * tt
                    feet = sy + JV * tt - 0.5 * G * tt * tt
                    vy = JV - G * tt
                    cross = (feet[:-1] >= Y[:, :-1] - 1e-6) & (feet[1:] <= Y[:, 1:]) & (vy[1:] < 0)
                    over = (bx[1:] + BODY_W / 2 > X[:, 1:] - W) & (bx[1:] - BODY_W / 2 < X[:, 1:] + W)
                    hit = cross & over
                    hit[ai, :] &= (tt[1:] > 0.15)
                    if not hit.any():
                        continue
                    ks = np.where(hit.any(axis=0))[0]
                    k = ks[0] + 1
                    li = int(np.where(hit[:, ks[0]])[0][0])
                    if li != bi:
                        continue
                    killed = False
                    for st in strips:
                        ov = ((bx[:k + 1] + BODY_W / 2 > st['x0']) & (bx[:k + 1] - BODY_W / 2 < st['x1']) &
                              (feet[:k + 1] + BODY_H > st['y'] - 0.5) & (feet[:k + 1] < st['y'] + 0.5))
                        if ov.any():
                            killed = True
                            break
                    if not killed:
                        ok = True
                        break
                if ok:
                    break
            if ok:
                break
        if not ok:
            bad.append(f"{a['id']}->{b['id']}")
    return bad


def write_preview(j):
    """MapPreview_Stage5-2.svg: 발판(종류별 색), 이동형 경로, 몬스터, 추락 판정을 한 장에 그린다."""
    sc, mx, my = 3.0, 30.0, 14.0
    xs0, xs1, ys0, ys1 = -30.0, 380.0, -156.0, 12.0
    W, H = (xs1 - xs0) * sc + 2 * mx, (ys1 - ys0) * sc + 2 * my + 60

    def X(x):
        return mx + (x - xs0) * sc

    def Y(y):
        return my + 40 + (ys1 - y) * sc
    col = {'start': '#4c9f50', 'normal': '#6b7a8f', 'connector': '#3d6fb4', 'goal': '#c0392b', 'damage': '#e67e22',
           'blink': '#8e44ad', 'disappearing': '#16a085', 'moving': '#2c3e50', 'damage_floor': '#d35400'}
    out = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W:.0f} {H:.0f}" width="{W:.0f}" height="{H:.0f}" font-family="sans-serif" font-size="11">',
           f'<rect width="{W:.0f}" height="{H:.0f}" fill="#fafafa"/>',
           f'<text x="{mx}" y="22" font-size="15" font-weight="bold">5-2 설계 미리보기 (5구간 x 15 = 75, 120유닛/구간)</text>']
    for st in j['validation']['fallTriggers']:
        out.append(f'<line x1="{X(st["x0"]):.1f}" y1="{Y(st["y"]):.1f}" x2="{X(st["x1"]):.1f}" y2="{Y(st["y"]):.1f}" stroke="#e74c3c" stroke-width="1.2" stroke-dasharray="5 3" opacity="0.7"/>')
    for part in j['parts']:
        for p in part['platforms']:
            if p['move']:
                m = p['move']
                pts = ' '.join(f'{X(a):.1f},{Y(b):.1f}' for a, b in (m['wp'] + ([m['wp'][0]] if m['mode'] == 'loop' else [])))
                out.append(f'<polyline points="{pts}" fill="none" stroke="#2c3e50" stroke-width="1" stroke-dasharray="3 2" opacity="0.6"/>')
                for a, b in m['wp']:
                    out.append(f'<rect x="{X(a - p["width"] / 2):.1f}" y="{Y(b):.1f}" width="{p["width"] * sc:.1f}" height="{T * sc:.1f}" fill="#2c3e50" opacity="0.18"/>')
            else:
                if p['id'].endswith('_K0') and part['id'] != 'P1':
                    continue
                out.append(f'<rect x="{X(p["cx"] - p["width"] / 2):.1f}" y="{Y(p["top"]):.1f}" width="{max(p["width"] * sc, 1.5):.1f}" height="{max(T * sc, 2):.1f}" fill="{col.get(p["kind"], "#888")}"/>')
    for part in j['parts']:
        for m in part['monsters']:
            c = '#c0392b' if m['pattern'] == 'FlyingChasePattern' else '#8e44ad'
            out.append(f'<circle cx="{X(m["x"]):.1f}" cy="{Y(m["y"]):.1f}" r="2.4" fill="{c}"/>')
    for part in j['parts']:
        a, b = part['platforms'][0], part['platforms'][-1]
        out.append(f'<text x="{X(a["cx"]):.1f}" y="{Y(a["top"]) - 12:.1f}" fill="#222">{part["id"]}</text>')
    st = j['parts'][0]['platforms'][0]
    out.append(f'<text x="{X(st["cx"]) - 4:.1f}" y="{Y(st["top"]) - 4:.1f}" fill="#4c9f50" font-weight="bold">★</text>')
    gl = j['parts'][4]['platforms'][-1]
    out.append(f'<text x="{X(gl["cx"]) - 4:.1f}" y="{Y(gl["top"]) + 16:.1f}" fill="#c0392b" font-weight="bold">○</text>')
    legend = [('일반', 'normal'), ('연결', 'connector'), ('피해', 'damage'), ('점멸', 'blink'), ('소멸', 'disappearing'), ('이동형(경로)', 'moving'), ('피해 바닥', 'damage_floor')]
    lx = mx
    for name, k in legend:
        out.append(f'<rect x="{lx}" y="{H - 24:.0f}" width="14" height="6" fill="{col[k]}"/><text x="{lx + 18}" y="{H - 17:.0f}">{name}</text>')
        lx += 18 + len(name) * 12 + 14
    out.append(f'<text x="{lx}" y="{H - 17:.0f}" fill="#e74c3c">- - 추락 판정</text>')
    out.append('</svg>')
    with open(os.path.join(HERE, 'MapPreview_Stage5-2.svg'), 'w', encoding='utf-8') as f:
        f.write(chr(10).join(out))


if __name__ == '__main__':
    parts = assemble()
    place_monsters(parts)
    res = validate(parts)
    jj = write_outputs(parts, res)
    write_preview(jj)
    print('lengths', res['lengths'])
    print('static_issues', res['static_issues'])
    print('overlaps', res['overlaps'])
    print('bad mover pairs', [m for m in res['mover_pairs'] if not good(m)])
    print('monsters_in_platform', res['monsters_in_platform'])
    print('movers', len(res['moverRules']), 'OK' if res['ok'] else 'FAIL')
    print('max wait', max(m['maxwait'] for m in res['mover_pairs']), 'pairs', len(res['mover_pairs']))
    print('fallSim', res['fallSim'], 'routeMissing', res['routeJumpMissing'], 'stripOverlap', res['stripOverlap'])
    for k in ('P1', 'P2', 'P3', 'P4', 'P5'):
        print(k, [round(v, 1) for v in bbox(parts[k]['plats'])], len(parts[k]['monsters']))
