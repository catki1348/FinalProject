"""
build_attacks.py - Stage 5 boss (Twin-Blade Demon Overlord) attack keypose post-processing.

AI attestation (rule 54): written by Claude Sonnet 5.5, 2026-10-15.

What this script does (rule 40 explanation):
  1. cutout()      : removes the green chroma background and the drawn ground line from a
                     generated source image, keeps only the largest connected figure (RGBA result).
  2. place()       : puts the figure on a fixed-size transparent canvas. Grounded poses share one
                     foot baseline and a foot-centre pivot; the airborne pose is lifted by a fixed hover.
  3. main()        : builds final PNGs (final/<ID>.png), one looping GIF per attack (gif/<Attack>.gif),
                     a contact-sheet preview (BossPreview_Stage5.png) and animation_manifest.json.
No RGB pixels of the figure are repainted; only the alpha channel is computed.
Run:  python build_attacks.py   (inside @Edit/Stage5_Attacks)
"""
import json
import os
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
CANVAS_W, CANVAS_H = 2400, 1600   # shared game canvas for every frame
BASELINE_Y = 1560                 # y of the foot contact line
PIVOT_X = CANVAS_W // 2           # x of the foot centre
HOVER = {"S2_02": 380}            # airborne keyposes: pixels lifted above the baseline

# attack id -> (display name, frame ids, hold seconds per frame, category)
ATTACKS = {
    "N1_RightSlash":   ("우횡 베기",         ["N1_01", "N1_02"],          [0.40, 0.40], "중단"),
    "N2_GroundSweep":  ("지면 쓸기",         ["N2_01", "N2_02"],          [0.45, 0.45], "하단"),
    "N3_ThrustLunge":  ("쌍검 찌르기",       ["N3_01", "N3_02"],          [0.45, 0.45], "중단"),
    "N4_CrossSlash":   ("십자 베기",         ["N4_01", "N4_02"],          [0.35, 0.35], "중단"),
    "N5_SpinLowSlash": ("회전 하단 베기",    ["N5_01", "N5_02"],          [0.40, 0.50], "하단"),
    "S1_DoomCross":    ("파멸의 십자참",     ["S1_01", "S1_02", "S1_03"], [0.60, 0.60, 0.40], "중단"),
    "S2_WingDive":     ("암흑 날개 급강하",  ["S2_01", "S2_02", "S2_03"], [0.50, 0.50, 0.40], "중단"),
    "S3_AbyssQuake":   ("심연 지맥파",       ["S3_01", "S3_02", "S3_03"], [0.55, 0.55, 0.40], "하단"),
}


def cutout(path):
    """Return (RGBA array, info) with the green background and ground line removed."""
    rgb = np.asarray(Image.open(path).convert("RGB")).astype(np.int16)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    green = (g - np.maximum(r, b)) > 22            # green-dominant pixels = background (incl. vignette)
    olive = (g * 100 > r * 95) & ((g - b) > 18) & (g > 45)   # olive/yellow-green fringe halo (cream swords have r>g, so they stay)
    green |= olive
    fg = ~green
    h, w = fg.shape
    # ground line: rows where foreground spans most of the width are cleared
    rows = fg.sum(axis=1)
    cand = rows > 0.6 * w
    cand[: int(h * 0.6)] = False                  # a ground line is never in the upper part of the image
    line_rows = np.zeros_like(cand)
    i = 0
    while i < h:                                  # only thin (<=10 rows) wide runs count as the drawn line
        if cand[i]:
            j = i
            while j < h and cand[j]:
                j += 1
            if j - i <= 10:
                line_rows[i:j] = True
            i = j
        else:
            i += 1
    fg[line_rows, :] = False
    fg = ndimage.binary_opening(fg, iterations=1)  # drop single-pixel specks
    lab, n = ndimage.label(fg)
    if n == 0:
        raise RuntimeError("no figure found: " + path)
    sizes = ndimage.sum(fg, lab, range(1, n + 1))
    keep = np.zeros_like(fg)
    big = max(sizes)
    for i, s in enumerate(sizes, 1):               # keep the figure and big detached parts (swords, wing tips)
        if s >= big * 0.01:
            keep |= lab == i
    # soften the edge by one pixel so green fringes are not hard-cut
    alpha = (ndimage.binary_erosion(keep, iterations=1) * 255).astype(np.uint8)
    out = np.dstack([rgb.clip(0, 255).astype(np.uint8), alpha])
    ys, xs = np.nonzero(alpha)
    info = {"bounds": [int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())],
            "removedLineRows": int(line_rows.sum())}
    return out, info


def place(rgba, fid):
    """Place a cut-out on the shared canvas using the foot centre as pivot."""
    alpha = rgba[..., 3]
    ys, xs = np.nonzero(alpha)
    top, bottom = ys.min(), ys.max()
    low = ys > bottom - max(8, int((bottom - top) * 0.06))   # lowest 6% = feet
    foot_x = int(xs[low].mean())
    img = Image.fromarray(rgba, "RGBA")
    canvas = Image.new("RGBA", (CANVAS_W, CANVAS_H), (0, 0, 0, 0))
    dx = PIVOT_X - foot_x
    dy = BASELINE_Y - HOVER.get(fid, 0) - int(bottom)
    canvas.alpha_composite(img, (max(0, dx), max(0, dy))) if (dx >= 0 and dy >= 0) else canvas.paste(img, (dx, dy), img)
    return canvas, {"footX": foot_x, "shiftX": dx, "shiftY": dy}


def main():
    os.makedirs(os.path.join(HERE, "final"), exist_ok=True)
    os.makedirs(os.path.join(HERE, "gif"), exist_ok=True)
    manifest = {"canvas": [CANVAS_W, CANVAS_H], "baselineY": BASELINE_Y, "pivotX": PIVOT_X, "frames": {}, "attacks": {}}
    finals = {}
    for key, (_, fids, _, _) in ATTACKS.items():
        for fid in fids:
            rgba, info = cutout(os.path.join(HERE, f"source_{fid}.png"))
            canvas, pinfo = place(rgba, fid)
            canvas.save(os.path.join(HERE, "final", f"{fid}.png"))
            finals[fid] = canvas
            manifest["frames"][fid] = {**info, **pinfo, "hover": HOVER.get(fid, 0)}
    BG = (24, 20, 28, 255)
    scale = 0.34
    gw, gh = int(CANVAS_W * scale), int(CANVAS_H * scale)
    for key, (name, fids, holds, cat) in ATTACKS.items():
        frames, durs = [], []
        seq = list(zip(fids, holds)) + [(fids[0], 0.25)]       # recover pause before the loop restarts
        for fid, hold in seq:
            bg = Image.new("RGBA", (CANVAS_W, CANVAS_H), BG)
            bg.alpha_composite(finals[fid])
            d = ImageDraw.Draw(bg)
            d.line([(0, BASELINE_Y), (CANVAS_W, BASELINE_Y)], fill=(90, 90, 90, 255), width=4)
            frames.append(bg.convert("RGB").resize((gw, gh), Image.NEAREST).convert("P", palette=Image.ADAPTIVE))
            durs.append(int(hold * 1000))
        frames[0].save(os.path.join(HERE, "gif", f"{key}.gif"), save_all=True, append_images=frames[1:],
                       duration=durs, loop=0, disposal=2)
        manifest["attacks"][key] = {"name": name, "category": cat, "frames": fids, "holdSeconds": holds}
    # contact sheet: one row per attack, frames in order
    cell_w, cell_h = 420, 280
    sheet = Image.new("RGB", (cell_w * 3, cell_h * len(ATTACKS)), BG[:3])
    sd = ImageDraw.Draw(sheet)
    for row, (key, (name, fids, _, cat)) in enumerate(ATTACKS.items()):
        for col, fid in enumerate(fids):
            bg = Image.new("RGBA", (CANVAS_W, CANVAS_H), BG)
            bg.alpha_composite(finals[fid])
            bbox = finals[fid].getbbox()
            crop = bg.crop((max(0, bbox[0] - 40), max(0, bbox[1] - 40), min(CANVAS_W, bbox[2] + 40), min(CANVAS_H, bbox[3] + 40))).convert("RGB")
            crop.thumbnail((cell_w - 8, cell_h - 8))
            sheet.paste(crop, (col * cell_w + 4, row * cell_h + 4))
            sd.text((col * cell_w + 8, row * cell_h + 6), f"{fid} {key} [{cat}]", fill=(255, 220, 80))
    sheet.save(os.path.join(HERE, "BossPreview_Stage5.png"))
    with open(os.path.join(HERE, "animation_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
    print("done", len(finals), "frames")


if __name__ == "__main__":
    main()
