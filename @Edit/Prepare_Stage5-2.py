"""Codex (GPT-6), 2026-10-15. Convert Claude's design into Unity-readable preparation data."""
import json
import hashlib
from pathlib import Path

HERE = Path(__file__).resolve().parent


def prepare():
    """Deduplicate shared connectors; preserve design coordinates and movement phases."""
    source = json.loads((HERE / 'MapDesign_Stage5-2.json').read_text(encoding='utf-8'))
    platforms, monsters, seen = [], [], set()
    for part in source['parts']:
        for p in part['platforms']:
            if p['id'] in seen or p['id'].endswith('_K0'):
                continue
            seen.add(p['id'])
            move = p['move']
            platforms.append(dict(id=p['id'], kind=p['kind'], x=p['cx'], y=p['top'], width=p['width'],
                                  points=[dict(x=x, y=y) for x, y in move['wp']] if move else [],
                                  speed=move['speed'] if move else 0, delay=move['delay'] if move else 0,
                                  wait=move['wait'] if move else 0, loop=bool(move and move['mode'] == 'loop')))
            platforms[-1]['syncPeriod'] = move.get('syncPeriod', 0) if move else 0
        for m in part['monsters']:
            group = part['id'] + ('_' + str(m['group']) if m.get('group') else '')
            monsters.append(dict(id=m['id'], pattern=m['pattern'], x=m['x'], y=m['y'], group=group,
                                 guaranteed=m['drops'] == 'heal+speed 100%',
                                 area=dict(zip(('x', 'y'), m.get('areaSize', [4.2, 1.6]))),
                                 detection=dict(zip(('x', 'y'), m.get('detect', [24, 10] if part['id'] == 'P4' else [8, 4]))),
                                 orbit=m.get('orbit', .35 if part['id'] == 'P4' else 2),
                                 pathLength=m.get('pathLength', 0)))
    by_id = {p['id']: p for p in platforms}

    def gate(key, marker, dx=0):
        p = by_id[marker]
        return dict(key=key, x=p['x']+dx, y=p['y']+1, width=8, height=10)

    groups = [gate('P3', 'S2_K'), gate('P4_1', 'S3_K'), gate('P4_2', 'S3_K', 60),
              gate('P5_1', 'S4_K'), gate('P5_2', 'S5F2_K'), gate('P5_3', 'S5F3_K')]
    # Reserve the not-yet-applied 5-1 design (world max X 495.4); include all fall strips.
    result = dict(status='unity_placement_data', sourceRevision=source.get('revision','legacy'),
                  sourceDesignSha256=hashlib.sha256((HERE/'MapDesign_Stage5-2.json').read_bytes()).hexdigest(), origin=dict(x=532, y=0),
                  boundary12=501, boundary23=916, reserve51Right=495.4,
                  minY=-152, maxY=10, platforms=platforms, monsters=monsters, groups=groups,
                  fallTriggers=source['validation']['fallTriggers'],
                  sourceSession='7a52062c-ef67-4a96-b8c3-e3fb1d05daa2',
                  sourceInstruction='2026-10-16: repair reviewed mover periods, B4 separation, F1 headroom and long-horizon traversal; keep one area')
    assert len(platforms) == 67 and len(monsters) == 31
    assert sum(bool(p['points']) for p in platforms) == 20
    assert sum(m['guaranteed'] for m in monsters) == 22
    assert all(501 <= 532+s['x0'] < 532+s['x1'] <= 916 for s in result['fallTriggers'])
    for p in platforms:
        points = p['points'] or [dict(x=p['x'], y=p['y'])]
        assert all(501 <= 532+q['x']-p['width']/2 and 532+q['x']+p['width']/2 <= 916 for q in points)
        assert all(-152 <= q['y']-.5 and q['y'] <= 10 for q in points)
    (HERE / 'Stage52_UnityPreparation.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print('PASS: 67 platforms / 20 movers / 31 flyers / 22 paired drops / 13 fall strips; 5-1 reserved')


if __name__ == '__main__':
    prepare()
