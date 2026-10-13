"""Extract the two generated attack poses, key green, align feet, and build playback."""
from pathlib import Path
import json
from collections import deque
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent
PATTERNS = [('ClawSlash', '발톱 베기', .35, .7), ('LowSweep', '하단 휩쓸기', .4, .8),
            ('SonicRoar', '음파 포효', .5, 1.9), ('CaveCollapse', '천장 낙석', .8, 1.4),
            ('Jump', '점프', .3, .8), ('Charge', '돌진', .6, 1.5)]
# Generated single images use slightly different body scales; these visual corrections
# reduce torso-size jumps without stretching any body part independently.
# CaveCollapse raises its arms high, so it may use more of the canvas height than other attacks.
MAX_H = {'CaveCollapse': 1150, 'ClawSlash': 1170}
# LowSweep's extended arm needs a wider canvas so its strike pose can keep the same body scale.
CANVAS_W = {'LowSweep': 2200, 'Jump': 2200, 'Charge': 2200}
POSE_SCALE = {'ClawSlash': [1.28,1.42], 'LowSweep': [1,1.16], 'SonicRoar': [1,1.15], 'CaveCollapse': [1.26,1.26], 'Jump': [.88,.95], 'Charge': [.9,.9]}

def extract(source, column=None, drop_shadow=False):
    """Split equal cells and remove the requested green matte without changing body colors."""
    im = Image.open(source).convert('RGBA')
    w, h = im.size
    cell = im if column is None else im.crop((column*w//2, 0, (column+1)*w//2, h))
    rgba = np.array(cell)
    rgb = rgba[:, :, :3].astype(float)
    green = (rgb[:, :, 1] > 5) & (rgb[:, :, 1] > rgb[:, :, 0]*1.3) & (rgb[:, :, 1] > rgb[:, :, 2]*1.3)
    rgba[green, 3] = 0
    rgba[green, :3] = 0
    if drop_shadow:
        # Remove the dark-green ground shadow and dust the generator draws under the feet.
        # Only the lowest 14% of the figure is touched so body outlines stay intact.
        ys=np.where(rgba[:,:,3]>0)[0]
        low=ys.min()+round((ys.max()-ys.min())*.86)
        r,g,b=(rgba[:,:,k].astype(int) for k in range(3))
        dark=(g>r+8)&(g>=b-4)&(g<140)
        dark[:low]=False
        rgba[dark,3]=0;rgba[dark,:3]=0
    # Keep the connected character and discard detached slash trails or matte specks.
    # A coarse mask joins only pixels within four pixels; original RGB is never repainted.
    hh,ww=rgba.shape[:2]
    padded=np.pad(rgba[:,:,3]>0,((0,(-hh)%4),(0,(-ww)%4)))
    coarse=padded.reshape(padded.shape[0]//4,4,padded.shape[1]//4,4).any(axis=(1,3))
    seen=np.zeros(coarse.shape,dtype=bool)
    largest=[]
    for sy,sx in zip(*np.where(coarse)):
        if seen[sy,sx]:
            continue
        queue=deque([(sy,sx)]);seen[sy,sx]=True;component=[]
        while queue:
            y,x=queue.popleft();component.append((y,x))
            for yy,xx in ((y-1,x),(y+1,x),(y,x-1),(y,x+1)):
                if 0<=yy<coarse.shape[0] and 0<=xx<coarse.shape[1] and coarse[yy,xx] and not seen[yy,xx]:
                    seen[yy,xx]=True;queue.append((yy,xx))
        if len(component)>len(largest):
            largest=component
    main=np.zeros(coarse.shape,dtype=bool)
    for y,x in largest:
        main[y,x]=True
    keep=main.repeat(4,axis=0).repeat(4,axis=1)[:hh,:ww]
    rgba[~keep,3]=0
    cell = Image.fromarray(rgba)
    box = cell.getbbox()
    if box is None:
        raise ValueError('Empty frame')
    if box[0] <= 0 or box[2] >= cell.width or box[1] <= 0 or box[3] >= cell.height:
        raise ValueError(f'Clipped sprite: {source.name}, {box}')
    return cell, box, float(green.mean())

def build():
    """Create eight PNGs, four looping GIF studies, and a timing manifest."""
    results = []
    for ident, label, hit, duration in PATTERNS:
        source = ROOT / ('sheet_'+ident+'.png')
        poses=[]
        for i in range(2):
            # Movement poses use named sources (windup/air) instead of numbered ones.
            named={'Jump':['JumpCrouch','JumpAir'],'Charge':['ChargeWindup','ChargeRun']}
            individual=ROOT/(f'source_{named[ident][i]}.png' if ident in named else f'source_{ident}_{i+1:02}.png')
            poses.append(extract(individual,drop_shadow=(ident=='Charge' and i==0)) if individual.exists() else extract(source,i))
        # Establish an attack scale, then apply recorded uniform pose corrections.
        scale=900/max(bounds[3]-bounds[1] for _,bounds,_ in poses)
        cw=CANVAS_W.get(ident,1432)
        frames=[]
        info=[]
        for i in range(2):
            cell, bounds, ratio = poses[i]
            alpha=np.array(cell.getchannel('A'))
            foot_band=alpha[max(bounds[1],bounds[3]-round((bounds[3]-bounds[1])*.12)):bounds[3]]
            xs=np.where(foot_band>0)[1]
            foot_center=(int(xs.min())+int(xs.max())+1)/2
            pose_scale=scale*POSE_SCALE[ident][i]
            # Keep every opaque pixel inside the shared canvas after foot anchoring.
            extent=max(foot_center-bounds[0],bounds[2]-foot_center)
            pose_scale=min(pose_scale,(cw/2-16)/extent,MAX_H.get(ident,1000)/(bounds[3]-bounds[1]))
            scaled=cell.resize((round(cell.width*pose_scale),round(cell.height*pose_scale)),Image.Resampling.NEAREST)
            frame=Image.new('RGBA',(cw,1198))
            frame.alpha_composite(scaled,(cw//2-round(foot_center*pose_scale),1190-round(bounds[3]*pose_scale)))
            filename=f'{ident}_{i+1:02}.png'
            frame.save(ROOT/filename)
            small=frame.resize((cw//2,599), Image.Resampling.NEAREST)
            # GIF studies use a dark backing; transparent PNGs remain the source deliverable.
            display=Image.new('RGBA',small.size,(28,23,25,255))
            display.alpha_composite(small)
            frames.append(display.convert('RGB'))
            info.append({'file':filename,'sourceBounds':bounds,'removedGreenFraction':ratio,'uniformScale':pose_scale,'footCenter':foot_center})
        frames[0].save(ROOT/(ident+'.gif'), save_all=True, append_images=[frames[1],frames[0]],
                       duration=[round(hit*1000),round((duration-hit)*1000),500], loop=0)
        results.append({'id':ident,'name':label,'duration':duration,'hitTime':hit,'frames':info,
                        'canvas':[cw,1198],'pivot':[.5,.006678]})
    (ROOT/'animation_manifest.json').write_text(json.dumps({'patterns':results,'source':'VARCO GPT_Work_Room, GPT Image 1.5',
        'notes':'Two generated key poses per attack; chroma key and foot alignment only. No Unity integration.'},ensure_ascii=False,indent=2),encoding='utf-8')
    for p in results:
        for f in p['frames']:
            im=Image.open(ROOT/f['file'])
            assert im.size==tuple(p['canvas']) and im.mode=='RGBA'
            assert im.getchannel('A').getextrema()==(0,255)
        gif=Image.open(ROOT/(p['id']+'.gif'))
        assert gif.n_frames==3
    print('Verified: 12 RGBA PNGs and 6 animated GIFs')

if __name__=='__main__':
    build()
