from Stage24_sim import *
import json
T=1.8; J=2.15
def build(T=T,J=J):
    out=[];prev=[None]
    def left_for(): return 0.0 if prev[0] is None else prev[0]+J
    def pp(n,dx,y0,y1,w,par,left=None):
        left=left_for() if left is None else left
        p0=(left,y0);p1=(left+dx,y1);Lg=math.dist(p0,p1)
        out.append(Pl(n,[p0,p1],round(Lg/(T-w),2),'pp',w,par*T));prev[0]=left+dx
    S=[('L1',1.4,0,0,0.5,0),('L2',1.4,0,0,0.7,1),
       ('V1',0,0,2.4,0.5,0),('V2',0,2.4,4.8,0.7,1),('V3',0,4.8,7.2,0.5,0),
       ('D1',1.2,7.2,5.0,0.6,1),('D2',1.2,5.0,3.0,0.4,0),
       ('Z1',0,3.0,4.4,0.5,1),('Z2',0,4.4,5.8,0.7,0),('Z3',0,5.8,7.2,0.5,1),('Z4',0,7.2,9.0,0.7,0)]
    for a in S: pp(*a)
    left=left_for()
    for n,y,w,dl in [('P1',9.8,0.4,1.95),('P2',8.6,0.7,1.8),('P3',7.4,0.5,1.65)]:
        out.append(Pl(n,[(left,y),(left+1.0,y)],round(1.0/(T-w),2),'pp',w,dl))
    prev[0]=left+1.0
    def loop(n,sp,y0,h,delay):
        left=left_for()
        out.append(Pl(n,[(left,y0),(left+0.6,y0),(left+0.6,y0+h),(left,y0+h)],sp,'loop',0,delay));prev[0]=left+0.6
    loop('C1',2.2,7.4,2.2,0.0);loop('C2',2.6,8.2,1.2,0.45);loop('C3',2.2,8.2,1.2,0.9);loop('C4',2.6,8.2,1.2,1.35)
    ys=[8.8,6.8,4.8,2.8,1.0];osp=[2.0,2.3,1.9,2.2]
    for i in range(4):
        left=left_for();out.append(Pl('O%d'%(i+1),[(left,ys[i]),(left+1.0,ys[i+1])],osp[i],'once'));prev[0]=left+1.0
    for n,y0,y1,w,par in [('Fi1',1.0,0.8,0.5,0),('Fi2',0.8,0.5,0.7,1),('Fi3',0.5,0.0,0.5,0)]:
        pp(n,1.2,y0,y1,w,par)
    return out,prev[0]
chain=['L1','L2','V1','V2','V3','D1','D2','Z1','Z2','Z3','Z4','PX','C1','C2','C3','C4','O1','O2','O3','O4','Fi1','Fi2','Fi3']
def ev(d,a,b):
    A=d[a];B=d[b]
    if A.mode=='once': A=Pl(A.name,A.pts,A.speed,'pp',static='end')
    if B.mode=='once': B=Pl(B.name,B.pts,B.speed,'pp',static='start')
    return stat(A,B)
def overlaps(pl):
    ids=[]
    states={}
    for p in pl:
        if p.mode=='once':
            states[p.name]=[(np.array([p.pts[0][0]]),np.array([p.pts[0][1]])),(np.array([p.pts[-1][0]]),np.array([p.pts[-1][1]]))]
            # include intermediate ride positions
            f=np.linspace(0,1,12);states[p.name]=[(np.array([p.pts[0][0]+(p.pts[-1][0]-p.pts[0][0])*a]),np.array([p.pts[0][1]+(p.pts[-1][1]-p.pts[0][1])*a])) for a in f]
        else:
            x,y=p.pos(tt);states[p.name]=[(x,y)]
    bad=[]
    names=list(states)
    for a,b in itertools.combinations(names,2):
        for xa,ya in states[a]:
            for xb,yb in states[b]:
                ox=W-np.abs(xa[:,None]-xb[None,:]) if False else None
        # compute min separation using broadcasting on combos
    return bad
def overlap2(pl):
    import itertools
    res={}
    cand={}
    for p in pl:
        if p.mode=='once':
            f=np.linspace(0,1,24)
            cand[p.name]=(p.pts[0][0]+(p.pts[-1][0]-p.pts[0][0])*f,p.pts[0][1]+(p.pts[-1][1]-p.pts[0][1])*f)
        else: cand[p.name]=p.pos(tt)
    for a,b in itertools.combinations(cand,2):
        xa,ya=cand[a];xb,yb=cand[b]
        if len(xa)==len(xb):
            ox=W-np.abs(xa-xb);oy=TH-np.abs(ya-yb)
            m=(ox>0)&(oy>0)
        else:
            ox=W-np.abs(xa[:,None]-xb[None,:]);oy=TH-np.abs(ya[:,None]-yb[None,:]);m=(ox>0)&(oy>0)
        if m.any(): res[(a,b)]=1
    return res
OFFSET=-24.6   # Section_4 local X = design X' + OFFSET
def export(path_json,path_md=None):
    pl,r=build(); d={p.name:p for p in pl}
    rows=[]
    for p in pl:
        mode={'pp':'PingPong','loop':'Loop','once':'Once'}[p.mode]
        pts=[(round(a+OFFSET,2),round(b,2)) for a,b in p.pts]
        rows.append(dict(name=p.name,mode=mode,points=pts,speed=p.speed,endpointWait=p.wait,startDelay=round(p.delay,2),startOnFirstBoard=(p.mode=='once'),travel=round(p.L,2)))
    pairs=[]
    for a,b in zip(chain,chain[1:]):
        if 'PX' in (a,b):
            for ln in ['P1','P2','P3']:
                pairs.append((ln if a=='PX' else a, b if a=='PX' else ln, ev(d,ln,b) if a=='PX' else ev(d,a,ln)))
        else: pairs.append((a,b,ev(d,a,b)))
    json.dump(dict(offset=OFFSET,platforms=rows,pairs=[dict(a=a,b=b,**{k:(float(v) if hasattr(v,'item') or isinstance(v,float) else v) for k,v in s.items()}) for a,b,s in pairs]),open(path_json,'w',encoding='utf-8'),ensure_ascii=False,indent=1)
    return rows,pairs

if __name__=='__main__':
    import sys
    out=sys.argv[1] if len(sys.argv)>1 else 'Stage24_revision.json'
    rows,pairs=export(out,None)
    print('platforms',len(rows),'width',round(build()[1]+W,2))
    for a,b,s_ in pairs: print(a,'->',b,s_)
    print('sweep overlaps',overlap2(build()[0]))
