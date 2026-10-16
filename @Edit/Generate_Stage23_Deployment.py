# AI work record: Codex; model unknown; 2026-10-16. Reproducible Stage2-3 deployment geometry.
import json,math,numpy as np
from pathlib import Path
from Search_Stage23_Pair import track,P6,P7
HERE=Path(__file__).parent
cfg=json.loads((HERE/'Stage23_PairSearch.json').read_text())
a=track(P6,cfg['s6'],cfg['w6'],.2,6500);b=track(P7,cfg['s7'],cfg['w7'],cfg['delay'],6500)
# Moving-target ballistic landing probes; use jump height 80% and horizontal speed 8.
def probe(k):
    start=a[k]+[1.,.02];prev=start[1]-b[k,1]
    for n in range(1,40):
        t=n*.02;foot=start+[8*t,math.sqrt(.8)*9*t-29.43*t*t/2];target=b[k+n]
        delta=foot[1]-target[1]
        if prev>0 and delta<=0 and abs(foot[0]-target[0])<1.4:
            return n,target
        # A side impact before reaching the top invalidates the straight jump.
        if abs(foot[0]-target[0])<1.45 and foot[1]<target[1]-.04 and foot[1]+1.28>target[1]-.5:return None
        prev=delta
    return None
hits=[]
for k in range(100,6100):
    hit=probe(k)
    if hit:hits.append((k,hit[0]))
assert hits,'No actual moving-target transfer'
# Representative transfer on M6 outbound half, with M7 path-coordinate accessible.
def arc(p,q):
    best=None;total=0
    for x,y in zip(p,p[1:]):
        v=y-x;l=np.linalg.norm(v);u=np.clip(np.dot(q-x,v)/(l*l),0,1);pt=x+u*v;err=np.linalg.norm(pt-q)
        if best is None or err<best[0]:best=(err,total+u*l)
        total+=l
    return best[1]
chosen=None
for k,n in hits:
    if a[k+1,0]>a[k,0] and a[k,0]>6:
        chosen=(k,n);break
assert chosen
k,n=chosen;handoffA=a[k];handoffB=b[k+n]
def geom(L):
    plats=[];nodes=[];route=0.;last=np.array([0.,0.]);w=4.
    def fixed(pid,width):
        nonlocal route,last,w
        pos=last+[w/2+width/2+2.4,0];route+=np.linalg.norm(pos-last);last=pos;w=width
        plats.append(dict(id=pid,width=width,kind='Normal',points=[pos.tolist()]));return pos
    def move(i):
        nonlocal route,last,w
        start=last+[w/2+1.5+2.4,0];pts=np.array([[0,0],[L*.25,.6],[L*.5,-.6],[L*.75,.6],[L,0]])+start
        route+=np.linalg.norm(start-last)+sum(np.linalg.norm(y-x) for x,y in zip(pts,pts[1:]));last=pts[-1];w=3.
        plats.append(dict(id='M'+str(i),width=3.,kind='Moving',points=pts.tolist(),speed=[4.8,5.2,5.6,5,5.4][i-1],delay=[0,.35,.7,1.05,1.4][i-1],wait=.35));return pts
    plats.append(dict(id='Start',width=4.,kind='Normal',points=[[0.,0.]]))
    groups=[['N1','N2','N3'],['N4','N5','Rest1'],['N6','N7'],['N8','N9','Rest2'],['N10']]
    for i,group in enumerate(groups,1):
        move(i)
        for name in group:fixed(name,4. if 'Rest' in name else 3.)
    origin=last+[w/2+1.5+2.4,0]
    pts6=P6+origin;pts7=P7+origin
    route+=np.linalg.norm(origin-last)+arc(P6,handoffA)+np.linalg.norm(handoffB-handoffA)+sum(np.linalg.norm(y-x) for x,y in zip(P7,P7[1:]))-arc(P7,handoffB)
    plats.append(dict(id='M6',width=3.,kind='Moving',points=pts6.tolist(),speed=cfg['s6'],delay=.2,wait=cfg['w6']))
    plats.append(dict(id='M7',width=3.,kind='Moving',points=pts7.tolist(),speed=cfg['s7'],delay=cfg['delay'],wait=cfg['w7']))
    last=pts7[-1];w=3.;fixed('Goal',4.)
    return plats,route,origin
lo,hi=1.,30.
for _ in range(70):
    mid=(lo+hi)/2;p,r,o=geom(mid)
    if r<240:lo=mid
    else:hi=mid
plats,route,origin=geom((lo+hi)/2);assert abs(route-240)<1e-6
# Monsters deliberately occupy fixed resting-ledge airspace, outside any moving sweep.
mon=[]
hosts=[p for p in plats if p['kind']=='Normal' and p['id'] not in ('Start','Goal')]
for i,p in enumerate(hosts):
    x,y=p['points'][0];mon.append(dict(id=i+1,kind='Ground',host=p['id'],points=[[x-.35,y+.34],[x+.35,y+.34]],width=.49,height=.645,scale=.5,speed=1.2))
# 18 flying patrols distributed across 12 fixed ledges; vertical lanes do not overlap.
slots=[('N1',0,1.8),('N2',0,1.8),('N3',0,1.8),('N4',0,1.8),('N5',0,1.8),('Rest1',0,1.8),('N6',0,1.8),('N7',0,1.8),('N8',0,1.8),('N9',0,1.8),('Rest2',-.55,1.8),('Rest2',.55,1.8),('N10',-.8,1.8),('N10',0,1.8),('N10',.8,1.8),('N10',-.8,2.9),('N10',0,2.9),('N10',.8,2.9)]
for j,(hid,offset,height) in enumerate(slots):
    host=next(h for h in hosts if h['id']==hid);x,y=host['points'][0];x+=offset
    mon.append(dict(id=j+13,kind='Flying',host=hid,points=[[x-.1,y+height],[x+.1,y+height]],width=.49,height=.645,scale=.5,speed=.4))
# Conservative swept rectangles are exact enough here: monsters move horizontally,
# and fixed ledges separate their X interval from every platform path's X interval.
minclear=999.
for m in mon:
    pts=np.array(m['points']);mn=pts.min(0)-[m['width']/2,m['height']/2];mx=pts.max(0)+[m['width']/2,m['height']/2]
    for p in plats:
        if p['kind']!='Moving':continue
        pts=np.array(p['points']);pn=pts.min(0)-[1.5,.5];px=pts.max(0)+[1.5,0]
        sep=np.maximum(0,np.maximum(mn-px,pn-mx));clear=np.linalg.norm(sep);minclear=min(minclear,clear)
        assert clear>=.5,(m['id'],p['id'],clear)
# Stronger corpse test: all living/corpse X footprints remain outside moving sweeps.
for m in mon:
    if m['kind']!='Flying':continue
    mn=min(q[0] for q in m['points'])-.245;mx=max(q[0] for q in m['points'])+.245
    for p in plats:
        if p['kind']!='Moving':continue
        left=min(q[0] for q in p['points'])-1.5;right=max(q[0] for q in p['points'])+1.5
        assert mx+.5<left or mn-.5>right
hitmask=np.zeros(6000,bool)
for kk,nn in hits:hitmask[kk-100]=True
idx=np.flatnonzero(hitmask);maxgap=float(np.diff(np.r_[0,idx,len(hitmask)-1]).max()*.02)
result=dict(status='validated_geometry',originWorld=[192.5,0],distance=30,routeLength=route,platforms=plats,monsters=mon,transfer=dict(launchTime=k*.02,flightTime=n*.02,fromPoint=(handoffA+origin).tolist(),toPoint=(handoffB+origin).tolist(),coordinateOnly=True),validation=dict(platformCount=21,movingCount=7,monsterCount=30,monsterPlatformPairs=210,minSweptClearance=float(minclear),corpseVerticalCorridorsClear=True,transferBallisticSuccesses=len(hits),transferMaxLaunchGapSeconds=maxgap,pairCollisionSampleSeconds=240,pairGeometryMaxWait=float(cfg['score'][0]),pairGeometryMinWindow=-float(cfg['score'][1])))
(HERE/'Stage23_Deployment.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print('PASS route=',route,'L=',(lo+hi)/2,'goal=',plats[-1]['points'],'minClearance=',minclear,'transferProbes=',len(hits),'maxGap=',maxgap)




bridge=json.loads(json.dumps(result))
for key in ('platforms','monsters'):
    for obj in bridge[key]: obj['points']=[dict(x=q[0],y=q[1]) for q in obj['points']]
(HERE/'Stage23_Unity.json').write_text(json.dumps(bridge,indent=2),encoding='utf-8')

