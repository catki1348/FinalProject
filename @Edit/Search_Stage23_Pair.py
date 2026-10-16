# AI work record: Codex; model unknown; 2026-10-16. Discrete MovingPlatform pair search.
import numpy as np, math, json
from pathlib import Path
DT=.02
P6=np.array([[0,0],[3,1],[6,0],[9,-1],[12,0]],float)
P7=np.array([[9,1],[12,0],[15,1],[18,0],[21,1]],float)
def track(p,s,wait,delay,frames=12000):
    pos=p[0].copy();target=1;direction=1;rest=round(delay/DT);out=[]
    for k in range(frames):
        if rest>0: rest-=1
        else:
            d=p[target]-pos;ln=np.linalg.norm(d)
            if ln<=s*DT+1e-10:
                pos=p[target].copy()
                if target in (0,len(p)-1):
                    direction=1 if target==0 else -1;rest=round(wait/DT)
                target+=direction
            else:pos+=d/ln*s*DT
        out.append(pos.copy())
    return np.array(out)
def cycle_frames(p,s):return 2*sum(math.ceil(np.linalg.norm(b-a)/(s*DT)-1e-10) for a,b in zip(p,p[1:]))
def gaps(mask):
    idx=np.flatnonzero(mask)
    if len(idx)<2:return 999,0
    waits=np.diff(np.r_[-1,idx,len(mask)])*DT
    dif=np.diff(np.r_[False,mask,False].astype(int));runs=(np.flatnonzero(dif==-1)-np.flatnonzero(dif==1))*DT
    return waits.max(),runs.min() if len(runs) else 0
P6[:,1]*=.3;P7[:,1]*=.3
best=None
for s6,s7 in [(4,4.5),(4.5,5),(5,5.5),(5.8,6.2),(6,6.5),(6.5,7),(7,7.5),(7.5,8)]:
    n6,n7=cycle_frames(P6,s6),cycle_frames(P7,s7)
    # Equal discrete period avoids long-term phase drift while retaining unequal speeds.
    period=max(n6,n7)+36
    w6=(period-n6)/2*DT;w7=(period-n7)/2*DT
    a=track(P6,s6,w6,.2,1500)
    for delay in np.arange(.4,3.5,.1):
        b=track(P7,s7,w7,delay,1500)
        collision=(abs(a[:,0]-b[:,0])<3.05)&(abs(a[:,1]-b[:,1])<.55)
        if collision.any():continue
        # Directional transfer: M7 to right, gap 0.5..3, rise 0..0.5 or small descent.
        dx=b[:,0]-a[:,0];dy=b[:,1]-a[:,1]
        ok=(dx>3.5)&(dx<6)&(dy>=-.5)&(dy<=.5)
        wait,window=gaps(ok[200:])
                # Require moving-target 80%-height ballistic landings, not just close snapshots.
        valid=np.zeros(1400,bool); prev=np.zeros(1400); blocked=np.zeros(1400,bool)
        for step in range(1,34):
            tt=step*DT;footx=a[:1400,0]+1+8*tt;footy=a[:1400,1]+.02+math.sqrt(.8)*9*tt-29.43*tt*tt/2
            target=b[step:1400+step];delta=footy-target[:,1]
            inside=abs(footx-target[:,0])<1.4
            if step>1:valid |= (~blocked)&(prev>0)&(delta<=0)&inside
            blocked |= inside&(delta<-.04)&(delta+1.28>-.5)&(~valid)
            prev=delta
        actualWait,actualWindow=gaps(valid[200:])
        if valid.sum()<20:continue
        score=(max(0,.5-window)*100+max(0,actualWait-4)*100+actualWait,-actualWindow)
        if best is None or score<best['score']:
            best=dict(score=score,s6=s6,s7=s7,w6=w6,w7=w7,delay=float(delay),period=period*DT)
if best is None:raise RuntimeError('No safe crossing pair')
a=track(P6,best['s6'],best['w6'],.2)
b=track(P7,best['s7'],best['w7'],best['delay'])
assert not np.any((abs(a[:,0]-b[:,0])<3.05)&(abs(a[:,1]-b[:,1])<.55))
print(best)
Path(__file__).with_name('Stage23_PairSearch.json').write_text(json.dumps(best,indent=2),encoding='utf-8')


