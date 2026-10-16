"""Codex (GPT-6), 2026-10-15. Design-only repair of the four Stage2-2 audit findings."""
import copy
import importlib.util
import json
import math
from pathlib import Path
import numpy as np

HERE = Path(__file__).resolve().parent
ORDER = 'N1 N2 N3 MP1 N4 MP2 N5 MP3 N6 MP4 N7 MP5 N8 D1 D2 D3 N9 N10'.split()
PREFABS = {k: f'Assets/@ExternalAssets/@Prefab/Stage2_2/P_Stage22_{name}.prefab'
           for k, name in [('normal', 'Normal'), ('moving', 'Moving'), ('disappearing', 'Disappearing')]}


def jump_pairs(plats, player, position_fn):
    """Look for adjacent landing windows with a controllable speed capped at 80%."""
    starts = np.arange(0, 60, .1)[:, None]
    air = np.arange(0, 2.01, .01)[None, :]
    result = []
    by_id = {p['id']: p for p in plats}
    for a_id, b_id in zip(ORDER, ORDER[1:]):
        a, b = by_id[a_id], by_id[b_id]
        ax, ay = position_fn(a, starts)
        bx, by = position_fn(b, starts+air)
        y = ay+player['jumpForce']*air-.5*player['gravity']*air**2
        delta = y-by
        mask = np.zeros(starts.shape[0], dtype=bool)
        for speed in np.linspace(.8, player['moveSpeed']*.8, 29):
            x = ax+a['width']/2-.03+speed*air
            overlap = (x+player['playerWidth']/2 >= bx-b['width']/2) & (x-player['playerWidth']/2 <= bx+b['width']/2)
            cross = (delta[:, :-1] >= 0) & (delta[:, 1:] <= 0) & overlap[:, 1:] & (air[:, 1:] > .1)
            mask |= cross.any(axis=1)
        longest = current = 0
        for valid in mask:
            current = 0 if valid else current+1
            longest = max(longest, current)
        result.append(dict(pair=f'{a_id}->{b_id}', reachable80=bool(mask.any()),
                           sampledStartTimes=600, availableSamples=int(mask.sum()), maximumWaitSeconds=round(longest*.1, 2)))
    return result


def dash_reach_upper(dy, player):
    """Upper bound for a single zero-vertical-velocity dash during a ballistic jump.

    With alpha=J/g, maximum non-dash air time is alpha+sqrt(2*(alpha²-2*dy/g)).
    The maximum occurs on descent, so merely adding dash distance to a normal jump is unsafe.
    """
    j, g = player['jumpForce'], player['gravity']
    if dy > j*j/(2*g):
        return 0.0
    alpha = j/g
    free = alpha+math.sqrt(max(0, 2*(alpha*alpha-2*dy/g)))
    return player['moveSpeed']*1.25*free + .9*1.25 + player['playerWidth']


def canonical_route(by_id, start_x, goal_x):
    """Count walks, gap chords and the outbound ride once, excluding waiting and jump arcs."""
    points = [dict(x=start_x, y=by_id[ORDER[0]]['top'], kind='start', platform=ORDER[0])]
    totals = dict(walk=0.0, jumpChord=0.0, ride=0.0)

    def append(x, y, kind, name):
        previous = points[-1]
        totals[kind] += math.hypot(x-previous['x'], y-previous['y'])
        points.append(dict(x=x, y=y, kind=kind, platform=name))

    first = by_id[ORDER[0]]
    append(first['x']+first['width']/2, first['top'], 'walk', first['id'])
    for name in ORDER[1:]:
        p = by_id[name]
        if p['points']:
            append(p['points'][0]['x'], p['points'][0]['y'], 'jumpChord', name)
            for point in p['points'][1:]:
                append(point['x'], point['y'], 'ride', name)
        else:
            append(p['x']-p['width']/2, p['top'], 'jumpChord', name)
            append(goal_x if name == ORDER[-1] else p['x']+p['width']/2, p['top'], 'walk', name)
    return points, {k: round(v, 6) for k, v in totals.items()}


def generate():
    snapshot = json.loads((HERE/'Stage22_Audit_20261015.json').read_text(encoding='utf-8'))
    spec = importlib.util.spec_from_file_location('stage22_audit', HERE/'Audit_Stage2-2.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    by_id = {p['id']: copy.deepcopy(p) for p in snapshot['platforms']}
    for p in by_id.values():
        for key in ('x', 'top', 'width', 'height'):
            p[key] = round(p[key], 3)
        for q in p['points']:
            q['x'], q['y'] = round(q['x'], 3), round(q['y'], 3)
        p.update(scaleX=1, scaleY=1, drawMode='Tiled', tileMode='Continuous', autoTiling=False,
                 prefab=PREFABS[p['kind']], behaviorAddedInScene=False)
    old9, old10 = copy.deepcopy(by_id['N9']), copy.deepcopy(by_id['N10'])
    right = by_id['N8']['x']+by_id['N8']['width']/2
    for index, name in enumerate(('D1', 'D2', 'D3'), 1):
        p = by_id[name]
        p['width'] = 1.6
        p['x'] = round(right+3.8+p['width']/2, 3)
        p['top'] = round(by_id['N8']['top']+.4*index, 3)
        right = p['x']+p['width']/2
    by_id['N9']['x'] = round(right+3.8+by_id['N9']['width']/2, 3)
    by_id['N9']['top'] = round(by_id['N8']['top']+1.6, 3)
    shift = by_id['N9']['x']-old9['x']
    by_id['N10']['x'] = round(old10['x']+shift, 3)
    by_id['N10']['top'] = by_id['N9']['top']
    start = dict(x=round(snapshot['start']['x'], 3), y=by_id['N1']['top']+.03)
    goal = dict(x=round(snapshot['end']['x']+shift, 3), y=round(snapshot['end']['y']+1.6, 3))
    platforms = [by_id[name] for name in ORDER]
    pairs = jump_pairs(platforms, snapshot, module.positions)
    chain = ['N8', 'D1', 'D2', 'D3', 'N9']
    skips = []
    for i in range(len(chain)):
        for j in range(i+2, len(chain)):
            a, b = by_id[chain[i]], by_id[chain[j]]
            gap = b['x']-b['width']/2-(a['x']+a['width']/2)
            dy = b['top']-a['top']
            reach = dash_reach_upper(dy, snapshot)
            # Additional 0.08 seconds and 0.05 units reserve discrete-update/input/skin error.
            conservative = reach+snapshot['moveSpeed']*1.25*.08+.05 if reach else 0
            skips.append(dict(pair=f'{a["id"]}->{b["id"]}', gap=round(gap, 6), rise=round(dy, 6),
                              maxBuffJumpDashEdgeReach=round(reach, 6), conservativeReach=round(conservative, 6),
                              clearance=round(gap-conservative, 6), blocked=gap>conservative))
    # Even from the highest point, the next dash cannot be ready before crossing a +0.8 surface.
    height = snapshot['jumpForce']**2/(2*snapshot['gravity'])
    post_dash_fall = math.sqrt(2*(height-.8)/snapshot['gravity'])
    no_second_dash = .2+post_dash_fall < .8*.75
    route, distances = canonical_route(by_id, start['x'], goal['x'])
    total = sum(distances.values())
    for p in platforms:
        p['center'] = dict(x=p['x'], y=round(p['top']-p['height']/2, 6))
        p['waypointCenters'] = [dict(x=q['x'], y=round(q['y']-p['height']/2, 6)) for q in p['points']]
        p['rendererSize'] = [p['width'], p['height']]
        p['colliderSize'] = p['rendererSize']
    runtime_path = HERE/'Stage22_Revision_Implementation_20261015.json'
    applied = runtime_path.exists() and json.loads(runtime_path.read_text(encoding='utf-8')).get('status') == 'verified'
    output = dict(status='implemented_verified' if applied else 'design_only_unity_not_modified', date='2026-10-15',
                  sourceSnapshot='Stage22_Audit_20261015.json', start=start, goal=goal,
                  unitsPerScale=8, camera=dict(x0=50, x1=172, y0=-8, y1=8),
                  downstreamShiftX=12, bossCenter=dict(x=236.025, y=-60),
                  platforms=platforms, canonicalRoute=route,
                  distance=dict(**distances, totalUnits=round(total, 6), scale=round(total/8, 6)),
                  validation=dict(adjacentPairs=pairs, skipPairs=skips, noSecondDashBeforeLanding=no_second_dash,
                                  minimumSkipClearance=round(min(s['clearance'] for s in skips), 6),
                                  counts=dict(normal=10, moving=5, disappearing=3),
                                  modelOnly=True, unityPlaytested=applied,
                                  unityReport='Stage22_Revision_Implementation_20261015.md' if applied else None))
    assert all(p['reachable80'] for p in pairs)
    assert all(s['blocked'] for s in skips) and no_second_dash
    assert len(platforms) == 18 and sum(p['kind']=='normal' for p in platforms) == 10
    assert all(p['x']-p['width']/2 >= 50 and p['x']+p['width']/2 <= 172 for p in platforms)
    assert all(p['top']-p['height'] >= -8 and p['top'] <= 8 for p in platforms)
    (HERE/'MapDesign_Stage2-2_Revision.json').write_text(json.dumps(output, ensure_ascii=False, indent=2), encoding='utf-8')
    write_preview(output)
    print('PASS: adjacent pairs', len(pairs), '/ skip pairs blocked', len(skips), '/ min conservative skip clearance', output['validation']['minimumSkipClearance'])
    print('DISTANCE', output['distance'])
    print('NEW CHAIN', [(p['id'], p['x'], p['top'], p['width']) for p in platforms if p['id'] in chain or p['id']=='N10'])


def write_preview(data):
    """Produce a code-native SVG plan with a complete map and a readable sequence inset."""
    colors = dict(normal='#80c99c', moving='#78b9ff', disappearing='#f4b65f')
    out = ['<svg xmlns="http://www.w3.org/2000/svg" width="1440" height="720" viewBox="0 0 1440 720">',
           '<!-- Codex (GPT-6), 2026-10-15; design preview, not a Unity screenshot. -->',
           '<rect width="1440" height="720" fill="#111b26"/>',
           '<style>text{font-family:Segoe UI,Malgun Gothic,sans-serif;fill:#e8f0f8} .small{font-size:15px} .label{font-size:16px;font-weight:600}</style>',
           '<text x="40" y="48" font-size="28" font-weight="700">2-2 수정 설계 · 정글 짐</text>',
           '<text x="40" y="80" font-size="17">일반 10 · 이동형 5 · 소멸형 3 | 설계안 / Unity 미적용</text>']
    for inset in (False, True):
        sx, x0, base = (12, 68, 260) if not inset else (34, 132, 600)
        subset = data['platforms'] if not inset else [p for p in data['platforms'] if p['id'] in ['N8','D1','D2','D3','N9','N10']]
        if inset:
            out += ['<line x1="40" y1="355" x2="1400" y2="355" stroke="#314359"/>',
                    '<text x="40" y="396" font-size="22" font-weight="700">소멸 구간 확대 · 간격 3.8 / 상승 0.4 / 착지 폭 1.6</text>']
        for p in subset:
            x = 40+(p['x']-x0)*sx
            y = base-p['top']*22
            width = p['width']*sx
            if p['points']:
                pts = ' '.join(f'{40+(q["x"]-x0)*sx:.2f},{base-q["y"]*22:.2f}' for q in p['points'])
                if p['loop']==0:
                    q=p['points'][0];pts+=f' {40+(q["x"]-x0)*sx:.2f},{base-q["y"]*22:.2f}'
                out.append(f'<polyline points="{pts}" fill="none" stroke="#78b9ff" stroke-width="2" stroke-dasharray="5 4"/>')
            out.append(f'<rect x="{x-width/2:.2f}" y="{y:.2f}" width="{width:.2f}" height="{p["height"]*12:.2f}" rx="2" fill="{colors[p["kind"]]}"/>')
            out.append(f'<text x="{x:.2f}" y="{y-9:.2f}" text-anchor="middle" class="label">{p["id"]}</text>')
        if inset:
            chain = {p['id']:p for p in subset}
            for a,b in zip(['N8','D1','D2','D3'],['D1','D2','D3','N9']):
                pa,pb=chain[a],chain[b]
                left=40+(pa['x']+pa['width']/2-x0)*sx
                right=40+(pb['x']-pb['width']/2-x0)*sx
                y=base-min(pa['top'],pb['top'])*22+35
                out.append(f'<path d="M {left:.2f} {y:.2f} H {right:.2f}" stroke="#b4c4d7"/>')
                out.append(f'<text x="{(left+right)/2:.2f}" y="{y+22:.2f}" text-anchor="middle" class="small">3.8</text>')
    out += [f'<text x="40" y="670" font-size="18">기준 동선 {data["distance"]["totalUnits"]:.3f}유닛 = 척도 {data["distance"]["scale"]:.3f} · 인접 17쌍 도달 / 소멸 우회 6쌍 차단 모델 통과</text>',
            '<text x="40" y="699" class="small">색상: 초록 일반 · 파랑 이동 · 주황 소멸 | 가속 5중첩·대시 포함 검토 · 실제 물리 및 연속 완주는 적용 단계에서 검증</text>', '</svg>']
    preview='\n'.join(out)
    if data['status']=='implemented_verified':
        preview=preview.replace('설계안 / Unity 미적용','Unity 반영 완료 / 배치 도식')
        preview=preview.replace('실제 물리 및 연속 완주는 적용 단계에서 검증','실제 물리·연속 소멸 구간·2-3 전환 검증 완료')
    (HERE/'MapPreview_Stage2-2_Revision.svg').write_text(preview, encoding='utf-8')


if __name__ == '__main__':
    generate()
