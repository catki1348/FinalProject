"""티저용 타이틀/자막 카드 이미지를 만든다(1920x1080 PNG). 배경은 프로젝트의 스테이지 배경 이미지를 사용."""
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance

ROOT = r'C:\Users\catkl\OneDrive\문서\Unity'
BG = os.path.join(ROOT, 'Assets', 'Sprites', 'Backgrounds')
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'cards')
os.makedirs(OUT, exist_ok=True)
BOLD = r'C:\Windows\Fonts\malgunbd.ttf'
REG = r'C:\Windows\Fonts\malgun.ttf'
W, H = 1920, 1080


def cover(img, w=W, h=H):
    """이미지를 비율 유지로 화면을 꽉 채우도록 자른다."""
    s = max(w / img.width, h / img.height)
    img = img.resize((int(img.width * s) + 1, int(img.height * s) + 1), Image.LANCZOS)
    x = (img.width - w) // 2
    y = (img.height - h) // 2
    return img.crop((x, y, x + w, y + h))


def shadow_text(draw_img, xy, text, font, fill, anchor='mm', shadow=(0, 0, 0), spread=6):
    layer = Image.new('RGBA', draw_img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.text(xy, text, font=font, fill=shadow + (200,), anchor=anchor)
    layer = layer.filter(ImageFilter.GaussianBlur(spread))
    draw_img.alpha_composite(layer)
    ImageDraw.Draw(draw_img).text(xy, text, font=font, fill=fill, anchor=anchor)


def card(name, bg_file, lines, darken=0.45, blur=0):
    bg = cover(Image.open(os.path.join(BG, bg_file)).convert('RGB'))
    if blur:
        bg = bg.filter(ImageFilter.GaussianBlur(blur))
    bg = ImageEnhance.Brightness(bg).enhance(darken)
    img = bg.convert('RGBA')
    # 위아래 비네팅
    vig = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    vd = ImageDraw.Draw(vig)
    for i in range(260):
        a = int(170 * (1 - i / 260) ** 2)
        vd.line([(0, i), (W, i)], fill=(0, 0, 0, a))
        vd.line([(0, H - 1 - i), (W, H - 1 - i)], fill=(0, 0, 0, a))
    img.alpha_composite(vig)
    for text, size, y, color, bold in lines:
        font = ImageFont.truetype(BOLD if bold else REG, size)
        shadow_text(img, (W // 2, y), text, font, color)
    img.convert('RGB').save(os.path.join(OUT, name + '.png'))
    print('card', name)


GOLD = (255, 214, 120, 255)
WHITE = (245, 245, 245, 255)
RED = (255, 90, 80, 255)

card('title', 'Stage5_Background.png', [
    ('파이널 던전', 190, 470, GOLD, True),
    ('마왕성을 향한 마지막 여정', 62, 640, WHITE, False),
], darken=0.5)
card('coming', 'Stage5_Background.png', [
    ('파이널 던전', 120, 420, GOLD, True),
    ('COMING SOON', 74, 580, WHITE, True),
    ('마왕성을 향한 마지막 여정', 40, 690, (210, 210, 210, 255), False),
], darken=0.35, blur=4)
for i, (f, t) in enumerate([
    ('Stage1_Background.png', '숲을 지나'),
    ('Stage2_Background.png', '고대 유적을 넘어'),
    ('Stage3_Background.png', '용암 지대를 건너'),
    ('Stage4_Background.png', '악마의 동굴을 뚫고'),
    ('Stage5_Background.png', '마왕성으로'),
], start=1):
    card(f'stage{i}', f, [(t, 120, 540, WHITE if i < 5 else RED, True)], darken=0.55)
