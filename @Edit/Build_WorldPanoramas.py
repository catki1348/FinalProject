# Claude Sonnet 5.5, 2026-10-16. Stitches generated tile images into one panorama per zone (Stage 1).
# Tiles overlap and are cross-faded, so adjacent images blend and no image repeats inside a zone.
# Usage: python Build_WorldPanoramas.py <tiles_dir> <out_dir>
#   tiles_dir contains SET_n.png files (A, B, C, TOP, DENSE, DARK). Output: Stage1_Area{1-5}_Panorama.png
import sys, math, os
import numpy as np
from PIL import Image

TILE_W, TILE_H = 1536, 864          # source tile pixels
PPU = 48                             # pixels per world unit -> one tile is 32 x 18 world units
OVERLAP = 0.12                       # fraction of a tile blended with its neighbour

STEP_X = int(TILE_W * (1 - OVERLAP))
STEP_Y = int(TILE_H * (1 - OVERLAP))

# zone -> (region width, region height in world units, row sets from top to bottom)
ZONES = {
    1: (215.0, 18.0, ["A"]),
    2: (99.0, 93.0, ["TOP", "DENSE", "DENSE", "DENSE", "DENSE", "DARK"]),
    3: (56.8, 16.5, ["B"]),
    4: (130.6, 31.0, ["TOP", "DENSE"]),
    5: (31.3, 17.9, ["C"]),
}


def tiles_needed(extent_units, tile_px, step_px):
    px = extent_units * PPU
    return max(1, math.ceil((px - tile_px) / step_px) + 1)


def ramp(n, ov, lo, hi):
    # Weight 1 in the middle, linear ramp over the overlap on interior edges, 1 on outer edges.
    w = np.ones(n, dtype=np.float32)
    ovp = max(1, int(ov))
    if lo:
        w[:ovp] = np.linspace(0.02, 1.0, ovp)
    if hi:
        w[n - ovp:] = np.minimum(w[n - ovp:], np.linspace(1.0, 0.02, ovp))
    return w


def main(tiles_dir, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    pools = {}
    for name in ["A", "B", "C", "TOP", "DENSE", "DARK"]:
        files = sorted(f for f in os.listdir(tiles_dir) if f.startswith(name + "_") and f.endswith(".png"))
        pools[name] = [os.path.join(tiles_dir, f) for f in files]
    used = {k: 0 for k in pools}
    for zone, (rw, rh, rows) in ZONES.items():
        cols = tiles_needed(rw, TILE_W, STEP_X)
        nrows = tiles_needed(rh, TILE_H, STEP_Y)
        # Tall zones need more rows than the row list: repeat the middle set to fill.
        if len(rows) < nrows:
            rows = rows[:1] + ["DENSE"] * (nrows - 2) + rows[-1:] if nrows > 2 else rows + ["DENSE"] * (nrows - len(rows))
        rows = rows[:nrows]
        W = (cols - 1) * STEP_X + TILE_W
        H = (nrows - 1) * STEP_Y + TILE_H
        acc = np.zeros((H, W, 3), dtype=np.float32)
        wsum = np.zeros((H, W, 1), dtype=np.float32)
        for r in range(nrows):
            for c in range(cols):
                pool = pools[rows[r]]
                path = pool[used[rows[r]] % len(pool)]
                used[rows[r]] += 1
                tile = np.asarray(Image.open(path).convert("RGB").resize((TILE_W, TILE_H), Image.LANCZOS), dtype=np.float32)
                wx = ramp(TILE_W, TILE_W * OVERLAP, c > 0, c < cols - 1)
                wy = ramp(TILE_H, TILE_H * OVERLAP, r > 0, r < nrows - 1)
                w = (wy[:, None] * wx[None, :])[:, :, None]
                x0, y0 = c * STEP_X, r * STEP_Y
                acc[y0:y0 + TILE_H, x0:x0 + TILE_W] += tile * w
                wsum[y0:y0 + TILE_H, x0:x0 + TILE_W] += w
        out = (acc / np.maximum(wsum, 1e-6)).clip(0, 255).astype(np.uint8)
        dest = os.path.join(out_dir, f"Stage1_Area{zone}_Panorama.png")
        Image.fromarray(out).save(dest)
        print(f"zone {zone}: {cols}x{nrows} tiles, {W}x{H}px = {W / PPU:.1f}x{H / PPU:.1f} units -> {dest}")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
