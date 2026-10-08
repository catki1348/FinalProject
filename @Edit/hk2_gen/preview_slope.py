"""Unity에 연결한 PNG/PPU/발 기준선으로 경사 모션을 합성한다. 게임 플레이 녹화는 아니다."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageOps
import json, math

root = Path(__file__).parent
settings = {s['Name']: s for s in json.loads((root/'slope_import_settings.json').read_text(encoding='utf-8'))}
images = {name: Image.open(Path('Assets/Sprites/Warrior/HeroKnightSlope')/name).convert('RGBA') for name in settings}

def panel(kind, index, right):
    name = f'{kind}_{index+1}.png'
    im, s = images[name], settings[name]
    angle = (25 if kind == 'climb' else -25) * (1 if right else -1)
    authored = s['AuthoredGroundAngle'] * (1 if right else -1)
    scale = 23 / s['PixelsPerUnit']
    pivot_x = s['PivotX'] if right else 1-s['PivotX']
    sprite = im if right else ImageOps.mirror(im)
    sprite = sprite.resize((round(im.width*scale),round(im.height*scale)),Image.Resampling.NEAREST)
    # 회전 중심을 임포트된 발 기준선과 일치시킨다.
    layer = Image.new('RGBA',(1000,1000))
    layer.alpha_composite(sprite,(round(500-pivot_x*im.width*scale),round(500-(im.height-1-s['PivotY']*im.height)*scale)))
    layer = layer.rotate(angle-authored,resample=Image.Resampling.NEAREST,center=(500,500))
    out = Image.new('RGBA',(500,400),(31,66,80,255))
    d = ImageDraw.Draw(out)
    y0, y1 = 315+math.tan(math.radians(angle))*250,315-math.tan(math.radians(angle))*250
    d.polygon([(0,y0),(500,y1),(500,400),(0,400)],fill=(38,48,50))
    d.line((0,y0,500,y1),fill=(99,150,109),width=2)
    out.alpha_composite(layer,(-250,-185))
    d.text((15,15),('UPHILL ' if kind=='climb' else 'DOWNHILL SLIDE ')+('RIGHT' if right else 'LEFT'),fill='white')
    return out

frames=[]
for tick in range(60):
    out=Image.new('RGBA',(1000,800))
    for row,kind in enumerate(['climb','slide']):
        index=int(tick*.05*(10 if kind=='climb' else 8))%(6 if kind=='climb' else 4)
        for col,right in enumerate([False,True]):out.alpha_composite(panel(kind,index,right),(col*500,row*400))
    frames.append(out.convert('RGB'))
frames[0].save(root/'Player_Slope_Preview.png')
palette=frames[0].convert('P',palette=Image.Palette.ADAPTIVE,colors=256)
frames=[f.quantize(palette=palette,dither=Image.Dither.NONE) for f in frames]
frames[0].save(root/'Player_Slope_Preview.gif',save_all=True,append_images=frames[1:],duration=50,loop=0,disposal=2)
print('Slope preview: 6 climbing frames / 4 sliding frames, both directions, 25 degree example ramps.')
