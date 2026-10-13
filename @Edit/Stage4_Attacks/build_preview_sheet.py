"""Build BossPreview_Stage4.png: every key pose at the same scale, from animation_manifest.json."""
from PIL import Image, ImageDraw, ImageFont
import json
d=json.load(open('animation_manifest.json',encoding='utf-8'))['patterns']
info={'ClawSlash':'일반1 · 피해 16','LowSweep':'일반2 · 피해 14','SonicRoar':'특수1 · 16/틱','CaveCollapse':'특수2 · 피해 20','Jump':'이동2 · 피해 없음','Charge':'이동3 · 피해 18'}
F=lambda s:ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',s)
S=.4; CW,CH=round(2200*S),round(1160*S); head=110; lab=44; rows=len(d)
img=Image.new('RGB',(2*CW+60,head+rows*(CH+lab)+20),(23,19,28));dr=ImageDraw.Draw(img)
dr.text((30,22),'스테이지4 보스 · 눈먼 동굴 괴수 (Blind Cave Horror)',font=F(34),fill=(240,231,238))
dr.text((30,68),'공격 4종 + 이동 2종 · 준비/타격 키포즈 12장 · 모든 칸 동일 배율 (미리보기, 게임 적용 아님)',font=F(20),fill=(192,173,196))
labels={'Jump':['도약 준비','공중'],'Charge':['돌진 준비','질주']}
for r,p in enumerate(d):
    y=head+r*(CH+lab)
    dr.text((30,y+4),f"{p['name']} {p['id']}  |  {info[p['id']]}  |  {p['hitTime']:.2f}초 / {p['duration']:.1f}초",font=F(22),fill=(215,182,234))
    for c,f in enumerate(p['frames']):
        im=Image.open(f['file']).convert('RGBA')
        full=Image.new('RGBA',(2200,1198));full.alpha_composite(im,((2200-im.width)//2,0))
        im=full.crop((0,38,2200,1198)).resize((CW,CH),Image.Resampling.NEAREST)
        bg=Image.new('RGBA',(CW,CH),(36,29,44,255));bg.alpha_composite(im)
        img.paste(bg.convert('RGB'),(30+c*CW,y+lab))
        dr.text((38+c*CW,y+lab+6),labels.get(p['id'],['준비','타격'])[c],font=F(18),fill=(240,231,238))
img.save('BossPreview_Stage4.png')
