"""Codex (GPT-6), 2026-10-15. Collider snapshot audit; does not modify Unity assets."""
import json
from pathlib import Path
import numpy as np

HERE = Path(__file__).resolve().parent
ORDER = 'N1 N2 N3 MP1 N4 MP2 N5 MP3 N6 MP4 N7 MP5 N8 D1 D2 D3 N9 N10'.split()


def positions(p, times):
    """Sample the configured loop including the L path's diagonal closing segment."""
    if not p['points']:
        return np.full_like(times, p['x']), np.full_like(times, p['top'])
    points = np.array([[v['x'], v['y']] for v in p['points']])
    if p['loop'] == 0:
        points = np.vstack([points, points[:1]])
    else:
        points = np.vstack([points, points[-2::-1]])
    lengths = np.linalg.norm(np.diff(points, axis=0), axis=1)
    elapsed = np.maximum(times-p['delay'], 0)*p['speed'] % lengths.sum()
    cumulative = np.r_[0, lengths.cumsum()]
    flat = elapsed.ravel()
    x = np.interp(flat, cumulative, points[:, 0]).reshape(times.shape)
    y = np.interp(flat, cumulative, points[:, 1]).reshape(times.shape)
    return x, y


def false_runs(mask):
    largest = current = 0
    for value in mask:
        current = 0 if value else current+1
        largest = max(largest, current)
    return round(largest*.1, 2)


def audit():
    snapshot = json.loads((HERE/'Stage22_Audit_20261015.json').read_text(encoding='utf-8'))
    by_id = {p['id']: p for p in snapshot['platforms']}
    starts = np.arange(0, 60, .1)[:, None]
    flight = np.arange(0, 2.01, .01)[None, :]
    checks = []
    for a_id, b_id in zip(ORDER, ORDER[1:]):
        a, b = by_id[a_id], by_id[b_id]
        ax, ay = positions(a, starts)
        bx, by = positions(b, starts+flight)
        # Launch with the foot center still just inside the departing platform's edge.
        py = ay+snapshot['jumpForce']*flight-.5*snapshot['gravity']*flight**2
        difference = py-by
        mask = np.zeros(starts.shape[0], dtype=bool)
        for speed in np.linspace(.8, snapshot['moveSpeed']*.8, 15):
            px = ax+a['width']/2-.03+speed*flight
            horizontal = (px+snapshot['playerWidth']/2 >= bx-b['width']/2) & (px-snapshot['playerWidth']/2 <= bx+b['width']/2)
            cross = (difference[:, :-1] >= 0) & (difference[:, 1:] <= 0) & horizontal[:, 1:] & (flight[:, 1:] > .1)
            mask |= cross.any(axis=1)
        checks.append(dict(pair=f'{a_id}->{b_id}', modelJump80=bool(mask.any()), possibleSamples=int(mask.sum()),
                           sampledStartTimes=600, maximumUnavailableSeconds=false_runs(mask)))
    result = dict(scope='read_only; flight model is not a full playthrough',
                  normalCount=sum(p['kind']=='normal' for p in by_id.values()),
                  moverCount=sum(p['kind']=='moving' for p in by_id.values()),
                  disappearingCount=sum(p['kind']=='disappearing' for p in by_id.values()),
                  simpleRenderers=[p['id'] for p in by_id.values() if p['drawMode']!='Tiled'],
                  stretchedTransforms=[p['id'] for p in by_id.values() if abs(p['scaleX']-1)>.001 or abs(p['scaleY']-1)>.001],
                  sceneAddedSpecialBehaviors=[p['id'] for p in by_id.values() if p['behaviorAddedInScene']],
                  missingPaths=[p['id'] for p in by_id.values() if p['kind']=='moving' and not p['pathConnected']],
                  jumpPairs=checks, missingScripts=snapshot['missingScripts'],
                  documentedDistanceUnits=88.9, correctedDistanceScale=88.9/8)
    (HERE/'Stage22_AuditResults_20261015.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print('COUNTS',result['normalCount'],result['moverCount'],result['disappearingCount'])
    print('NON_TILED',len(result['simpleRenderers']),'STRETCHED',len(result['stretchedTransforms']),'SCENE_BEHAVIORS',len(result['sceneAddedSpecialBehaviors']))
    print('JUMP_MODEL',sum(x['modelJump80'] for x in checks),'/',len(checks))
    print('LONGEST_WINDOW_GAP',max(x['maximumUnavailableSeconds'] for x in checks))
    print('FAILED', [x['pair'] for x in checks if not x['modelJump80']])


if __name__ == '__main__':
    audit()
