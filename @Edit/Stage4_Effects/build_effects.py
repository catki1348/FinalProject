"""Key the green background out of the VARCO effect sources and crop them to game-ready RGBA PNGs.

Only background pixels connected to the image border are removed, so green-ish pixels inside an
effect keep their colour. The background colour is the median of the border pixels.
"""
from collections import deque
from pathlib import Path
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent
# source file -> output file, colour-distance threshold, padding (px)
JOBS = [('src_wave', 'vfx_sonic_wave', 55, 8), ('src_rock', 'vfx_falling_rock', 55, 8),
        ('src_warn', 'vfx_rock_warning', 45, 6), ('src_charge', 'vfx_charge_warning', 45, 6)]

def key(name, out, threshold, pad):
    im = Image.open(ROOT / f'{name}.png').convert('RGBA')
    a = np.array(im)
    rgb = a[:, :, :3].astype(int)
    border = np.concatenate([rgb[0], rgb[-1], rgb[:, 0], rgb[:, -1]])
    bg = np.median(border, axis=0)
    dist = np.abs(rgb - bg).sum(axis=2)
    near = dist < threshold
    h, w = near.shape
    seen = np.zeros_like(near)
    queue = deque()
    for x in range(w):
        for y in (0, h - 1):
            if near[y, x] and not seen[y, x]: seen[y, x] = True; queue.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if near[y, x] and not seen[y, x]: seen[y, x] = True; queue.append((y, x))
    while queue:
        y, x = queue.popleft()
        for yy, xx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if 0 <= yy < h and 0 <= xx < w and near[yy, xx] and not seen[yy, xx]:
                seen[yy, xx] = True; queue.append((yy, xx))
    a[seen, 3] = 0
    a[seen, :3] = 0
    # Despill: leftover green fringes inside the effect are pulled down to the red/blue level.
    r, g, b = (a[:, :, k].astype(int) for k in range(3))
    spill = (a[:, :, 3] > 0) & (g > np.maximum(r, b) + 8)
    a[:, :, 1][spill] = np.maximum(r, b)[spill].astype(np.uint8)
    cell = Image.fromarray(a)
    box = cell.getbbox()
    box = (max(0, box[0] - pad), max(0, box[1] - pad), min(w, box[2] + pad), min(h, box[3] + pad))
    final = cell.crop(box)
    if out == 'vfx_falling_rock':
        # Drop the loose chips above the boulder so the sprite's centre is the boulder's centre.
        alpha = np.array(final.getchannel('A')) > 0
        body = int(np.argmax(alpha.sum(axis=1) >= 100))
        final = final.crop((0, max(0, body - 6), final.width, final.height))
        final = final.crop(final.getbbox())
    final.save(ROOT / f'{out}.png')
    return out, final.size, [int(v) for v in bg], float(seen.mean())

if __name__ == '__main__':
    for job in JOBS:
        print(key(*job))
