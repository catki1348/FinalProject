"""정지 이미지 폴더의 파일을 번호가 붙은 컨택트 시트(4열)로 만든다. 사용: still_sheet.py 폴더 [시트경로]"""
import os, sys
from PIL import Image, ImageDraw, ImageFont
D = sys.argv[1]
out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(D, '_sheet.jpg')
files = sorted(f for f in os.listdir(D) if f.lower().endswith('.png'))
cols, tw, th = 4, 480, 270
rows = (len(files) + cols - 1) // cols
sheet = Image.new('RGB', (cols * tw, rows * th))
dr = ImageDraw.Draw(sheet)
font = ImageFont.truetype(r'C:\Windows\Fonts\malgunbd.ttf', 26)
for i, f in enumerate(files):
    im = Image.open(os.path.join(D, f)).convert('RGB').resize((tw, th), Image.LANCZOS)
    sheet.paste(im, ((i % cols) * tw, (i // cols) * th))
    tag = f.rsplit('_', 1)[-1].replace('.png', '')
    dr.text(((i % cols) * tw + 8, (i // cols) * th + 4), f'{i+1} {tag}', font=font, fill=(255, 255, 0), stroke_width=3, stroke_fill=(0, 0, 0))
sheet.save(out, quality=84)
print(len(files), 'files ->', out)
