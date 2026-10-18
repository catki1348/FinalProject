import math, numpy as np, itertools, json, sys
g=29.43; V0=9.0; MS=8.0; W=1.1; BODY=0.983; TH=1.05
DT=0.02; TMAX=90.0
tt=np.arange(0,TMAX,DT)
def reach(dy,v):
    d=v*v-2*g*dy
    out=np.full_like(dy,np.nan,dtype=float)
    m=d>=0
    out[m]=MS*(v+np.sqrt(d[m]))/g
    return out
class Pl:
    def __init__(s,name,pts,speed,mode='pp',wait=0.0,delay=0.0,static=None):
        s.name=name;s.pts=[tuple(p) for p in pts];s.speed=speed;s.mode=mode;s.wait=wait;s.delay=delay;s.static=static
        q=s.pts+([s.pts[0]] if mode=='loop' else [])
        s.seg=[math.dist(q[i],q[i+1]) for i in range(len(q)-1)];s.q=q;s.L=sum(s.seg)
    def T(s): return s.L/s.speed+s.wait   # half period (pingpong)
    def pos(s,t):
        t=np.asarray(t,dtype=float)
        if s.static=='start': return np.full(t.shape,s.pts[0][0]),np.full(t.shape,s.pts[0][1])
        if s.static=='end': return np.full(t.shape,s.pts[-1][0]),np.full(t.shape,s.pts[-1][1])
        if s.mode=='loop':
            d=((np.maximum(t-s.delay,0)*s.speed)%s.L)
        else:
            u=np.maximum(t-s.delay,0); leg=s.L/s.speed; cyc=2*(leg+s.wait)
            ph=u%cyc
            d=np.where(ph<leg,ph*s.speed,np.where(ph<leg+s.wait,s.L,np.where(ph<2*leg+s.wait,(2*leg+s.wait-ph)*s.speed,0.0)))
        # arclength -> point
        x=np.zeros(t.shape);y=np.zeros(t.shape);acc=0.0;rem=d.copy()
        cum=np.cumsum([0]+s.seg)
        for i,l in enumerate(s.seg):
            a,b=s.q[i],s.q[i+1]
            m=(d>=cum[i])&((d<cum[i+1])|(i==len(s.seg)-1))
            f=np.clip((d[m]-cum[i])/l,0,1) if l>0 else 0
            x[m]=a[0]+(b[0]-a[0])*f;y[m]=a[1]+(b[1]-a[1])*f
        return x,y
def nojump_limit(dy):
    # horizontal gap up to which player can reach B without jumping (walk off / straddle)
    lim=np.full_like(dy,BODY)
    m=dy<-0.05
    lim[m]=MS*np.sqrt(2*(-dy[m])/g)+BODY
    return lim
def pair(A,B,t=tt):
    xa,ya=A.pos(t);xb,yb=B.pos(t)
    gap=np.abs(xa-xb)-W; dy=(yb-ya)
    rm=reach(dy,V0); rv=reach(dy,0.8*V0)
    need=gap+BODY+0.2
    jump_ok=(gap>0)&(need<=np.minimum(0.8*np.nan_to_num(rm,nan=-1),np.nan_to_num(rv,nan=-1)))
    walk=(gap<nojump_limit(dy))&(dy<=0.15)&(gap>-W)  # reachable without jump
    # vertical overlap (gap<=0): jumpable if player can hop up/down onto it
    H=(0.8*V0)**2/(2*g)
    lat=(gap<=0)&(dy>0.05)&(dy<=H*0.95)
    return jump_ok&~walk, walk&(gap<nojump_limit(dy)) , lat, gap, dy
def windows(mask):
    idx=np.flatnonzero(np.diff(np.concatenate([[0],mask.astype(int),[0]])))
    st=idx[0::2];en=idx[1::2]
    return [(a*DT,(b-a)*DT) for a,b in zip(st,en)]
def stat(A,B,skip=10):
    jmp,walk,lat,gap,dy=pair(A,B)
    k=int(skip/DT)
    ok=(jmp|lat)[k:]; wl=(walk&~(jmp|lat))[k:]
    ws=windows(ok); wk=windows(wl)
    if not ws: return dict(ok=False,n=0)
    interior=[w for w in ws if w[0]>0 and w[0]+w[1]<(TMAX-skip)-0.05]
    mw=max([ws[i+1][0]-(ws[i][0]+ws[i][1]) for i in range(len(ws)-1)] or [0])
    first=ws[0][0]
    # leading gap (from arbitrary time) is covered by maxwait; add edge waits
    edge=max(first, (TMAX-skip)-(ws[-1][0]+ws[-1][1]))
    mw=max(mw,0)
    return dict(ok=True,n=len(ws),minwin=round(float(min(w[1] for w in (interior or ws))),2),maxwait=round(float(mw),2),frac=round(float(ok.mean()),2),walkonly=round(float(wl.mean()),2))
