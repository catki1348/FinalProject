"""Build the four Death key poses (Death_01..04.png) from the generated sources.

Reuses build_attacks.extract (green key + largest connected piece) and the same canvas rules:
2200x1198 RGBA, ground line at y=1190, feet centre on the horizontal middle. Each pose gets a recorded
uniform scale (SCALE) tuned by eye so the body size matches the idle/attack poses.
"""
import json
from pathlib import Path
import numpy as np
from PIL import Image
from build_attacks import extract

ROOT = Path(__file__).resolve().parent
CANVAS_W, CANVAS_H, GROUND = 2200, 1198, 1190
# uniform scale per pose (source px -> canvas px); poses are generated at similar body size
SCALE = [1.45, 1.32, 1.07, 1.12]

def build():
    info = []
    for i in range(4):
        cell, bounds, ratio = extract(ROOT / f'source_Death_{i+1:02}.png', drop_shadow=True)
        alpha = np.array(cell.getchannel('A'))
        band = alpha[max(bounds[1], bounds[3] - round((bounds[3] - bounds[1]) * .12)):bounds[3]]
        xs = np.where(band > 0)[1]
        foot_center = (int(xs.min()) + int(xs.max()) + 1) / 2
        scale = SCALE[i]
        extent = max(foot_center - bounds[0], bounds[2] - foot_center)
        scale = min(scale, (CANVAS_W / 2 - 16) / extent, 1170 / (bounds[3] - bounds[1]))
        scaled = cell.resize((round(cell.width * scale), round(cell.height * scale)), Image.Resampling.NEAREST)
        frame = Image.new('RGBA', (CANVAS_W, CANVAS_H))
        frame.alpha_composite(scaled, (CANVAS_W // 2 - round(foot_center * scale), GROUND - round(bounds[3] * scale)))
        name = f'Death_{i+1:02}.png'
        frame.save(ROOT / name)
        info.append({'file': name, 'sourceBounds': bounds, 'uniformScale': scale, 'footCenter': foot_center})
    (ROOT / 'death_manifest.json').write_text(json.dumps({'canvas': [CANVAS_W, CANVAS_H], 'frames': info}, indent=2), encoding='utf-8')
    return info

if __name__ == '__main__':
    for f in build():
        print(f)
