"""K2(성+고원)와 K3(호수)의 차이로 '무너질 영역' 마스크를 만든다. 사용: python make_mask.py keyframes"""
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

W, H = 1920, 1080
d = sys.argv[1]
k2 = Image.open(f"{d}/k2.png").convert("RGB").resize((W, H), Image.LANCZOS)
k3 = Image.open(f"{d}/k3.png").convert("RGB").resize((W, H), Image.LANCZOS)
a = np.asarray(k2.filter(ImageFilter.GaussianBlur(2)), np.int16)
b = np.asarray(k3.filter(ImageFilter.GaussianBlur(2)), np.int16)
diff = np.abs(a - b).max(axis=2)
s = 1920 / 1376
poly = [(380, 340), (470, 290), (540, 125), (650, 185), (675, 185), (740, 110), (830, 5), (900, 100), (985, 175),
        (1100, 115), (1160, 100), (1320, 270), (1170, 380), (1000, 450), (850, 510), (760, 490), (600, 450), (480, 410)]
pm = Image.new("L", (W, H), 0)
ImageDraw.Draw(pm).polygon([(x * s, y * s) for x, y in poly], fill=255)
m = (diff > 55).astype(np.uint8) * 255
m = Image.fromarray(m).filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.MinFilter(5))
m = Image.fromarray((np.asarray(m) & np.asarray(pm)).astype(np.uint8))
m = m.filter(ImageFilter.MaxFilter(7)).filter(ImageFilter.GaussianBlur(2.5))
m.save(f"{d}/collapse_mask.png")
vis = Image.composite(k2, Image.new("RGB", (W, H), (255, 0, 255)), m)
vis.resize((960, 540)).save(f"{d}/collapse_mask_preview.png")
