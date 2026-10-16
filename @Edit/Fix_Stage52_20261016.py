"""Codex (GPT-6), 2026-10-16. Explicit coordinate patch and bounded validation; no random generation/search.

Start from the saved Unity preparation data, not the interrupted generator's divergent JSON.
"""
import copy
import hashlib
import json
import math
from pathlib import Path
import sys
import numpy as np

HERE=Path(__file__).resolve().parent
ROOT=HERE.parent
BACKUP=ROOT/'@Backup/Stage52_DeterministicFix_20261016'
G=29.43; BW=.9831; BH=1.287; J=9*math.sqrt(.8); DT=.02


def backup():
    """Preserve the pre-edit scene and all design inputs once, without touching earlier backups."""
    BACKUP.mkdir(parents=True,exist_ok=True)
    for relative in ['Assets/Scenes/Stage5.unity','Assets/@Scripts/Platform/MovingPlatform.cs',
                     '@Edit/MapDesign_Stage5-2.json','@Edit/MapDesign_Stage5-2.md','@Edit/MapDesign_Stage5-2_Tables.md',
                     '@Edit/MapPreview_Stage5-2.svg','@Edit/Stage52_UnityPreparation.json']:
        p=ROOT/relative;target=BACKUP/p.name
        if not target.exists():target.write_bytes(p.read_bytes())


def load_base():
    """Reconcile all positions/paths/monsters/fall strips with the actual deployed version."""
    j=json.loads((BACKUP/'MapDesign_Stage5-2.json').read_text(encoding='utf-8'))
    u=json.loads((BACKUP/'Stage52_UnityPreparation.json').read_text(encoding='utf-8'))
    lookup={p['id']:p for p in u['platforms']};monsters={m['id']:m for m in u['monsters']};last=None
    for part in j['parts']:
        for p in part['platforms']:
            if p['id'].endswith('_K0'):
                p.update(cx=last['cx'],top=last['top'],width=last['width'],move=None)
                continue
            q=lookup[p['id']]
            m=dict(mode='loop' if q['loop'] else 'pingpong',wp=[[v['x'],v['y']] for v in q['points']],speed=q['speed'],delay=q['delay'],wait=q['wait']) if q['points'] else None
            p.update(cx=q['x'],top=q['y'],width=q['width'],kind=q['kind'],move=m)
        last=part['platforms'][-1]
        for m in part['monsters']:
            q=monsters[m['id']];m['x'],m['y']=q['x'],q['y']
    j['validation']={'fallTriggers':copy.deepcopy(u['fallTriggers'])}
    j.update(date='2026-10-16',status='revision_pending_Unity',revision='stage52_deterministic_fix_20261016')
    return j


def flatten(j):
    by={};orders=[]
    for part in j['parts']:
        seq=[]
        for p in part['platforms']:
            if p['id'].endswith('_K0'):
                previous=orders[-1][-1] if part['id']!='P5' else 'S4_K'
                seq.append(previous);continue
            by.setdefault(p['id'],p);seq.append(p['id'])
        orders.append(seq)
    return by,orders


def rail(p):
    m=p['move'];wp=m['wp']+([m['wp'][0]] if m['mode']=='loop' else [])
    return sum(math.dist(a,b) for a,b in zip(wp,wp[1:]))


def patch(j):
    """The changes below are hand-chosen constants; no candidate generation or stochastic selection."""
    by,_=flatten(j)
    # Keep intersecting vertical/diagonal rails but align motion phases, so crossings happen at different times.
    for i in range(1,6):
        p=by[f'S1_B{i}'];m=p['move']
        if i==1:
            m['wp']=[[x,y-1.7] for x,y in m['wp']]
        if i==4:
            m['wp']=[[x+1.5,y] for x,y in m['wp']]
        m.update(speed=rail(p)/2,delay=(i-1)*.16,wait=0,syncPeriod=4.0)
        p['cx']=sum(q[0] for q in m['wp'])/2;p['top']=sum(q[1] for q in m['wp'])/2
    # F1 retains its original mean point (and route distance) but has a shorter B1 rail with headroom.
    p=by['S5F1_B1'];p['move'].update(wp=[[p['cx'],p['top']-2.6],[p['cx'],p['top']+2.6]],speed=2.6,delay=1.8,wait=0,syncPeriod=4.0)
    p=by['S5F1_B2'];p['move'].update(speed=rail(p)/2,delay=2.6,wait=0,syncPeriod=4.0)
    # Remove startup-only excessive waits; faster fixed routes reduce steady-state waits, too.
    speeds={'S2_M1':5,'S2_M2':5,'S2_M3':5,'S2_M4':5,'S2_M5':5.5,'S2_M6':6,
            'S3_E1':8,'S3_E2':8,'S3_V1':5.5,'S3_H1':5,
            'S5F2_M1':5,'S5F2_M2':5,'S5F3_E1':4}
    for name,speed in speeds.items():by[name]['move'].update(speed=speed,delay=0,wait=0)
    # Correct the small change in the P1 reference-polyline length using one normal landing height.
    # This is a bounded scalar length equation, not a layout search; all connectors stay fixed.
    seq=j['parts'][0]['platforms'];c1=by['S1_C1'];base=c1['top'];lo,hi=base-1,base+1
    def length():return sum(math.dist([a['cx'],a['top']],[b['cx'],b['top']]) for a,b in zip(seq,seq[1:]))
    c1['top']=lo;flo=length()-120
    c1['top']=hi;fhi=length()-120
    assert flo*fhi<=0
    for _ in range(45):
        mid=(lo+hi)/2;c1['top']=mid;f=length()-120
        if f*flo<=0:hi=mid
        else:lo=mid;flo=f
    c1['top']=round((lo+hi)/2,6)
    for part in j['parts']:
        part['pathLength']=round(sum(math.dist([a['cx'],a['top']],[b['cx'],b['top']]) for a,b in zip(part['platforms'],part['platforms'][1:])),6)
        assert abs(part['pathLength']-120)<.01,(part['id'],part['pathLength'])
    return j


def trajectory(p,t):
    """Analytic constant-speed paths; synchronized two-point movers use a common 4-second clock."""
    t=np.asarray(t);m=p['move']
    if not m:return np.full_like(t,p['cx'],dtype=float),np.full_like(t,p['top'],dtype=float)
    pts=np.array(m['wp']+([m['wp'][0]] if m['mode']=='loop' else m['wp'][-2::-1]),float)
    lengths=np.linalg.norm(np.diff(pts,axis=0),axis=1);cum=np.r_[0,lengths.cumsum()]
    elapsed=np.maximum(t-m['delay'],0)*m['speed'] % cum[-1]
    return np.interp(elapsed,cum,pts[:,0]),np.interp(elapsed,cum,pts[:,1])


def clearance(j):
    """400-second, 0.02-second all-platform overlap and rider head-space check."""
    by,_=flatten(j);ts=np.arange(0,400.0001,.02);poses={n:trajectory(p,ts) for n,p in by.items()};issues=[]
    for i,(a,p) in enumerate(by.items()):
        for b,q in list(by.items())[i+1:]:
            if not p['move'] and not q['move']:continue
            ax,ay=poses[a];bx,byy=poses[b]
            overlap=(np.abs(ax-bx)<(p['width']+q['width'])/2+.30)&(np.abs(ay-byy)<.5+BH+.1)
            if overlap.any():issues.append(dict(pair=a+' / '+b,seconds=round(float(overlap.sum())*.02,3),first=round(float(ts[np.flatnonzero(overlap)[0]]),3)))
    return issues


def pair_mask(a,b,by,strips,horizon=400,step=.2):
    """Bounded vector collision check; source edge/center departures, selectable horizontal velocities.

    Includes other platform bodies and fall strips, not only the destination gap.
    Blink/disappearing supports are treated as visible during these geometric landing probes.
    """
    t0=np.arange(0,horizon,step);flight=np.arange(0,2.401,DT);times=t0[:,None]+flight[None,:]
    ax,ay=trajectory(a,t0);bx,byy=trajectory(b,times)
    foot=ay[:,None]+.02+J*flight-.5*G*flight*(flight+DT)
    allmax=foot.max();allmin=foot.min()
    near=[]
    for p in by.values():
        pts=p['move']['wp'] if p['move'] else [[p['cx'],p['top']]]
        if max(q[1] for q in pts)<allmin-1 or min(q[1] for q in pts)>allmax+BH+1:continue
        if max(q[0] for q in pts)+p['width']/2 < ax.min()-21 or min(q[0] for q in pts)-p['width']/2 > ax.max()+21:continue
        x,y=trajectory(p,times);near.append((p,x,y))
    good=np.zeros(len(t0),bool);witness=None;witnesses=[]
    for ox in (-1,0,1):
        startx=ax+ox*(a['width']/2-.08)
        for vx in (-8,-6,-4,-2,0,2,4,6,8):
            active=np.flatnonzero(~good)
            if not len(active):break
            x=startx[active,None]+vx*flight
            f=foot[active];hit_any=np.zeros(x.shape,bool);valid_target=np.zeros(x.shape,bool)
            for p,px,py in near:
                px,py=px[active],py[active]
                overlap=(x+BW/2>px-p['width']/2+.03)&(x-BW/2<px+p['width']/2-.03)&(f+BH>py-.5)&(f<py)
                if p['id']==a['id']:overlap[:,flight<.14]=False
                if p['id']==b['id']:
                    above=np.zeros(x.shape,bool);above[:,1:]=f[:,:-1]>=py[:,:-1]-.03
                    valid_target=overlap&above&(x>px-p['width']/2+.15)&(x<px+p['width']/2-.15)
                hit_any|=overlap
            for s in strips:
                if s['y']+.5 < allmin or s['y']-.5 > allmax+BH:continue
                killed=(x+BW/2>s['x0'])&(x-BW/2<s['x1'])&(f+BH>s['y']-.5)&(f<s['y']+.5)
                hit_any|=killed;valid_target&=~killed
            hit_any[:,0]=False;first=hit_any.argmax(axis=1);ok=hit_any.any(axis=1)&valid_target[np.arange(len(active)),first]
            # A simultaneous non-target collision cannot count as landing on the target.
            for p,px,py in near:
                if p['id']==b['id']:continue
                k=first;rows=active
                other=(x[np.arange(len(active)),k]+BW/2>px[rows,k]-p['width']/2+.03)&(x[np.arange(len(active)),k]-BW/2<px[rows,k]+p['width']/2-.03)&(f[np.arange(len(active)),k]+BH>py[rows,k]-.5)&(f[np.arange(len(active)),k]<py[rows,k])
                if p['id']==a['id']:other&=flight[k]>=.14
                ok&=~other
            if witness is None and ok.any():
                k=int(np.flatnonzero(ok)[0]);row=int(active[k]);witness=dict(t0=round(float(t0[row]),3),offset=ox*(a['width']/2-.08),vx=vx,jump=J,flight=round(float(flight[first[k]]),3))
            if ok.any() and len(witnesses)<24:
                choices=np.flatnonzero(ok);early=choices[t0[active[choices]]<10]
                if len(early):choices=early
                k=int(choices[len(choices)//2]);row=int(active[k]);witnesses.append(dict(t0=round(float(t0[row]),3),offset=ox*(a['width']/2-.08),vx=vx,jump=J,flight=round(float(flight[first[k]]),3)))
            good[active[ok]]=True
    longest=cur=0
    for v in good:cur=0 if v else cur+1;longest=max(longest,cur)
    return dict(pair=a['id']+' -> '+b['id'],available=int(good.sum()),samples=len(t0),maxWait=round(longest*step,3),witness=witness,witnesses=witnesses)


def run():
    backup();j=patch(load_base());by,orders=flatten(j)
    issues=clearance(j);print('CABIN',issues,flush=True)
    (HERE/'Stage52_Deterministic_Proposal.json').write_text(json.dumps(j,ensure_ascii=False,indent=2),encoding='utf-8')
    checks=[]
    for part,seq in zip(j['parts'],orders):
        for a,b in zip(seq,seq[1:]):
            if not by[a]['move'] and not by[b]['move']:continue
            result=pair_mask(by[a],by[b],by,j['validation']['fallTriggers'])
            checks.append(result);print(result['pair'],result['available'],result['maxWait'],flush=True)
        (HERE/'Stage52_Deterministic_Model_20261016.json').write_text(json.dumps(dict(clearanceIssues=issues,pairs=checks),ensure_ascii=False,indent=2),encoding='utf-8')
    print('DONE',flush=True)


def accept():
    """Publish a validated fixed proposal and matching coordinate tables/preview, without rerunning checks."""
    j=json.loads((HERE/'Stage52_Deterministic_Proposal.json').read_text(encoding='utf-8'))
    report=json.loads((HERE/'Stage52_Deterministic_Model_20261016.json').read_text(encoding='utf-8'))
    assert not report['clearanceIssues'] and len(report['pairs'])==35
    assert all(p['available'] and p['maxWait']<=4 for p in report['pairs'])
    by,orders=flatten(j)
    static=[]
    for seq in orders:
        for a,b in zip(seq,seq[1:]):
            if by[a]['move'] or by[b]['move']:continue
            # Adjacent shared connectors on continuous runways have no required jump.
            if abs(by[a]['top']-by[b]['top'])<.001 and abs(by[a]['cx']-by[b]['cx'])<=(by[a]['width']+by[b]['width'])/2+.02:continue
            p=pair_mask(by[a],by[b],by,j['validation']['fallTriggers'],horizon=.4,step=.2)
            static.append(p);assert p['available'],p
    report['staticJumpPairs']=static
    report['lengths']={p['id']:p['pathLength'] for p in j['parts']}
    report['method']='Explicit fixed edits; 400s at .02s cabin sampling, .2s departures with .02s collision trajectories; 80% jump height.'
    report['inputDesignHash']=hashlib.sha256((HERE/'Stage52_Deterministic_Proposal.json').read_bytes()).hexdigest()
    j['validation'].update(report)
    j['internalAttribution']=dict(agent='Codex',model='GPT-6',date='2026-10-16',task='fixed 5-2 repair, no random generator')
    (HERE/'MapDesign_Stage5-2.json').write_text(json.dumps(j,ensure_ascii=False,indent=2),encoding='utf-8')
    (HERE/'Stage52_Deterministic_Model_20261016.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    lines=['# 5-2 수정 좌표표 (2026-10-16)','<!-- Codex (GPT-6), 2026-10-16. Fixed edited coordinates; no stochastic generator. -->','']
    for part in j['parts']:
        lines += [f'## {part["id"]} · {part["pathLength"]:.6f}유닛','','| ID | 유형 | 중심 X | 표면 Y | 폭 | 속도 / 지연 / 공통주기 |','|---|---|---:|---:|---:|---|']
        for p in part['platforms']:
            m=p['move'];motion=f'{m["speed"]:.6f} / {m["delay"]} / {m.get("syncPeriod",0)}' if m else '—'
            lines.append(f'| {p["id"]} | {p["kind"]} | {p["cx"]:.6f} | {p["top"]:.6f} | {p["width"]} | {motion} |')
            if m:lines.append(f'<!-- {p["id"]} waypoints: {json.dumps(m["wp"])} -->')
        lines.append('')
    (HERE/'MapDesign_Stage5-2_Tables.md').write_text('\n'.join(lines),encoding='utf-8')
    colors={'moving':'#65b7ff','damage_floor':'#ff805d','damage':'#e7ad5d','blink':'#bb92ed','disappearing':'#66cec0'}
    out=['<svg xmlns="http://www.w3.org/2000/svg" width="1440" height="720" viewBox="0 0 1440 720">',
         '<!-- Codex (GPT-6), 2026-10-16. Native diagram of the validated fixed 5-2 coordinates. -->',
         '<rect width="1440" height="720" fill="#111b26"/>','<style>text{font-family:Segoe UI,Malgun Gothic,sans-serif;fill:#e8f0f8}</style>',
         '<text x="25" y="38" font-size="25">5-2 수정 배치 · 5구간 × 15 = 75</text>',
         '<text x="25" y="68" font-size="17">P1·F1 공통주기 4초 / B4 경로 분리 / F1 머리 공간 확보 / 400초 충돌 모델</text>']
    def xy(x,y):return 35+(x+28)*3.35,105+(7-y)*3.35
    for s in j['validation']['fallTriggers']:
        x,y=xy(s['x0'],s['y']);x2,_=xy(s['x1'],s['y']);out.append(f'<path d="M {x:.2f} {y:.2f} H {x2:.2f}" stroke="#d67273" stroke-dasharray="4 4"/>')
    for p in by.values():
        m=p['move'];x,y=xy(p['cx'],p['top']);color=colors.get(p['kind'],'#80c99c')
        if m:
            pts=' '.join(f'{xy(a,b)[0]:.2f},{xy(a,b)[1]:.2f}' for a,b in m['wp']+([m['wp'][0]] if m['mode']=='loop' else []))
            out.append(f'<polyline points="{pts}" fill="none" stroke="{color}" stroke-width="2"/>')
        out.append(f'<rect x="{x-p["width"]*3.35/2:.2f}" y="{y:.2f}" width="{p["width"]*3.35:.2f}" height="3" fill="{color}"/>')
        if p['id'] in ['S1_B4','S1_B5','S5F1_B1']:out.append(f'<text x="{x+8:.2f}" y="{y:.2f}" font-size="12">{p["id"]}</text>')
    for part in j['parts']:
        p=part['platforms'][0];x,y=xy(p['cx'],p['top']);out.append(f'<text x="{x:.2f}" y="{y-14:.2f}" font-size="17">{part["id"]}</text>')
        for m in part['monsters']:
            x,y=xy(m['x'],m['y']);out.append(f'<circle cx="{x:.2f}" cy="{y:.2f}" r="2" fill="#ef7e88"/>')
    out += ['<text x="25" y="680" font-size="17">도식 미리보기 · 이동형 20개 / 발판 67개 / 몬스터 31마리 · 실제 Unity 검증은 적용 보고서 참조</text>','</svg>']
    (HERE/'MapPreview_Stage5-2.svg').write_text('\n'.join(out),encoding='utf-8')
    print('ACCEPTED: fixed patch, 35 moving links +',len(static),'static jump links, all part lengths 120')


if __name__=='__main__':
    accept() if '--accept' in sys.argv else run()
