"""생성된 PNG는 수정하지 않고 발 기준점 측정과 좌우 동작 미리보기만 만든다."""
from pathlib import Path
import json
from PIL import Image, ImageDraw, ImageOps

root = Path(__file__).parent
settings, images = [], []
sizes = {s['Name']: s['PixelsPerUnit'] for s in json.loads((root / 'motion_scale_settings.json').read_text(encoding='utf-8'))}
actual = {Path(s['Path']).name: s for s in json.loads((root / 'current_motion_imports.json').read_text(encoding='utf-8'))}
for number in range(1, 9):
    name = f"attack_{number}.png"
    im = Image.open(actual[name]['Path']).convert("RGBA")
    bbox = im.getchannel("A").point(lambda a: 255 if a >= 40 else 0).getbbox()
    bottom = bbox[3] - 1
    pixels = im.load()
    feet = []
    # 아래쪽 갈색 부츠를 사용해 흰 검 궤적이 가로 정렬 기준을 밀지 않게 한다.
    for y in range(max(0, bottom - 100), bottom + 1):
        for x in range(im.width):
            r, g, b, a = pixels[x, y]
            if im.width*.15 < x < im.width*.70 and a >= 40 and 20 < r < 210 and r > g * 1.12 and g > b * 1.10:
                feet.append(x)
    # 한쪽 부츠가 더 낮아도 피벗이 그 발로 쏠리지 않도록 양쪽 부츠 범위의 중앙을 사용한다.
    center = (min(feet)+max(feet))/2 if feet else (bbox[0] + bbox[2]) / 2
    settings.append(dict(Name=name, Width=im.width, Height=im.height,
                         PivotX=actual[name]['PivotX'], PivotY=actual[name]['PivotY'],
                         PixelsPerUnit=sizes[name], BottomY=bottom))
    images.append(im)
(root / "slash_import_settings.json").write_text(json.dumps(settings, indent=2), encoding="utf-8")

def draw_pose(canvas, image, setting, anchor, baseline, scale, mirror=False):
    # 원본 픽셀 수가 달라도 Unity의 PPU를 반영한 동일한 월드 배율로 표시한다.
    scale *= 100 / setting['PixelsPerUnit']
    sprite = ImageOps.mirror(image) if mirror else image
    px = (1-setting["PivotX"] if mirror else setting["PivotX"]) * image.width
    py = image.height-1-setting["PivotY"]*image.height
    sprite = sprite.resize((round(sprite.width*scale), round(sprite.height*scale)), Image.Resampling.NEAREST)
    canvas.alpha_composite(sprite, (round(anchor-px*scale), round(baseline-py*scale)))

frames = []
for i, (im, setting) in enumerate(zip(images, settings)):
    frame = Image.new("RGBA", (1000, 390), (31, 66, 80, 255))
    draw = ImageDraw.Draw(frame)
    draw.line((0, 342, 1000, 342), fill=(78, 115, 122), width=2)
    draw.text((20, 12), "LEFT SLASH", fill="white")
    draw.text((520, 12), "RIGHT SLASH", fill="white")
    draw.text((460, 368), f"{i+1} / 8", fill="white")
    draw_pose(frame, im, setting, 280, 340, .23, mirror=True)
    draw_pose(frame, im, setting, 720, 340, .23)
    frames.append(frame.convert("RGB"))
poster = Image.new("RGBA", (1840, 680), (31, 66, 80, 255))
for i, (im, setting) in enumerate(zip(images, settings)):
    x, y = (i % 4) * 460, (i // 4) * 340
    ImageDraw.Draw(poster).text((x+10, y+10), f"{i+1} / 8", fill="white")
    draw_pose(poster, im, setting, x+190, y+310, .23)
poster.save(root / "Player_Slash_Frames.png")
palette = poster.convert("RGB").convert("P", palette=Image.Palette.ADAPTIVE, colors=256)
quantized = [f.quantize(palette=palette, dither=Image.Dither.NONE) for f in frames]
quantized[0].save(root / "Player_Slash_Preview.gif", save_all=True,
                  append_images=quantized[1:], duration=[60]*7+[600], loop=0, disposal=2)
print(json.dumps(settings))

# 실제 임포터 값을 읽어 기본 대기 자세와 새 모션을 같은 월드 배율로 비교한다.
imports = json.loads((root / 'current_motion_imports.json').read_text(encoding='utf-8'))
imports = {Path(s['Path']).name: s for s in imports}
comparison = Image.new('RGBA', (1600, 1050), (31, 66, 80, 255))
names = ['idle_right', 'run_1', 'run_5', 'dash_1', 'damage_right', 'damage_left',
         'attack_1', 'attack_2', 'attack_3', 'attack_4', 'attack_6', 'attack_8']
for i, name in enumerate(names):
    if name == 'idle_right':
        path = Path('Assets/Sprites/Warrior/Transparent/idle_right.png')
        setting = dict(PixelsPerUnit=100, PivotX=466/1024, PivotY=221/1536)
    else:
        setting = imports[name+'.png']
        path = Path(setting['Path'])
    im = Image.open(path).convert('RGBA')
    x, y = (i % 4)*400, (i // 4)*350
    draw = ImageDraw.Draw(comparison)
    draw.text((x+10, y+10), name, fill='white')
    draw.line((x, y+310, x+400, y+310), fill=(78,115,122), width=2)
    draw_pose(comparison, im, setting, x+180, y+310, .23)
comparison.save(root / 'Player_Motion_Scale_Comparison.png')
