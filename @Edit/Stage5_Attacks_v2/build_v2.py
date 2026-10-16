"""
build_v2.py (frame cut-out/scale helpers; GIF assembly moved to build_v3.py) - Stage 5 boss (Twin-Blade Demon Overlord) attack animation builder, version 2.

AI attestation (rule 54): written by Claude Sonnet 5.5, 2026-10-15.

Why v2 (rule 33 failure analysis of v1): v1 frames were drawn independently (inconsistent look) and each
attack had only 2-3 frames with long holds (no sense of motion). v2 edits ONE side-profile reference for every
pose (consistent look), uses 4-5 key poses per attack, normalizes body size, and adds motion cues in the
preview only: anticipation/strike timing, forward lunge offset, smear frame, afterimage, impact shake/flash.

Steps (rule 40 explanations are on each function):
  cutout()    : green background + drawn ground line removed, largest figure kept -> RGBA.
  body_ruler(): measures dark-armor area to fine-tune body scale between frames.
  main()      : cut out all poses, scale to a common body size, place on one canvas (feet on baseline),
                write final/<ID>.png, gif/<Attack>.gif, BossPreview_Stage5_v2.png, animation_manifest.json.
Run:  python build_v2.py   (inside @Edit/Stage5_Attacks_v2)
"""
import json
import os
import numpy as np
from PIL import Image, ImageDraw, ImageChops
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "source")
CW, CH = 2000, 1700          # shared canvas
BASE_Y = 1640                # foot baseline
PIVOT_X = 1000               # foot-centre pivot
HOVER = {}   # airborne poses (none left after the special attacks were dropped)

# attack -> (display name, category, frame ids, hold ms per frame, impact frame index, forward offsets px)
ATTACKS = {
    "N1_RightSlash":   ("우횡 베기", "중단", ["N1_1", "N1_2", "N1_3", "N1_4"], [110, 150, 80, 150], 2, [-8, -22, 34, 52]),
    "N2_GroundSweep":  ("지면 쓸기", "하단", ["N2_1", "N2_2", "N2_3", "N2_4"], [120, 160, 70, 170], 3, [-6, -20, 20, 48]),
    "N3_ThrustLunge":  ("쌍검 찌르기", "중단", ["N3_1", "N3_2", "N3_3", "N3_4"], [110, 160, 70, 170], 3, [-8, -24, 26, 56]),
    "N4_CrossSlash":   ("십자 베기", "중단", ["N4_1", "N4_2", "N4_3", "N4_4"], [100, 150, 70, 170], 3, [-8, -20, 22, 46]),
    "N5_SpinLowSlash": ("회전 하단 베기", "하단", ["N5_1", "N5_2", "N5_3", "N5_4"], [110, 170, 70, 180], 3, [-6, -18, 18, 40]),
}


def cutout(path):
    """Remove the green backdrop and the drawn ground line; keep the main figure. Returns RGBA array."""
    rgb = np.asarray(Image.open(path).convert("RGB")).astype(np.int16)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    bg = ((g - np.maximum(r, b)) > 22) | ((g * 100 > r * 95) & ((g - b) > 18) & (g > 45))
    fg = ~bg
    h, w = fg.shape
    cand = fg.sum(axis=1) > 0.6 * w
    cand[: int(h * 0.6)] = False
    line = np.zeros_like(cand)
    i = 0
    while i < h:                                   # only thin wide runs are the ground line
        if cand[i]:
            j = i
            while j < h and cand[j]:
                j += 1
            if j - i <= 10:
                line[i:j] = True
            i = j
        else:
            i += 1
    fg[line, :] = False
    fg = ndimage.binary_opening(fg, iterations=1)
    lab, n = ndimage.label(fg)
    sizes = ndimage.sum(fg, lab, range(1, n + 1))
    keep = np.zeros_like(fg)
    for k, s in enumerate(sizes, 1):               # keep the figure plus large detached parts (sword tips)
        if s >= max(sizes) * 0.01:
            keep |= lab == k
    alpha = (ndimage.binary_erosion(keep, iterations=1) * 255).astype(np.uint8)
    return np.dstack([rgb.clip(0, 255).astype(np.uint8), alpha])


def body_ruler(rgba):
    """Body-size ruler: sqrt of the dark-armor pixel area (wings, blades and glow excluded).
    Measured on the generated frames this stays within about +-15% across poses, so it only fine-tunes scale."""
    rgb = rgba[..., :3].astype(np.int16)
    solid = rgba[..., 3] > 200
    dark = solid & (rgb.max(-1) < 125) & ((rgb[..., 0] - np.maximum(rgb[..., 1], rgb[..., 2])) < 38)
    return float(dark.sum() ** 0.5)


def armor_tone(rgba):
    """Mean RGB of the dark-armor pixels (used to even out frames the generator tinted darker or warmer)."""
    rgb = rgba[..., :3].astype(np.float32)
    solid = rgba[..., 3] > 200
    dark = solid & (rgb.max(-1) < 125) & ((rgb[..., 0] - np.maximum(rgb[..., 1], rgb[..., 2])) < 38)
    return rgb[dark].mean(0) if dark.sum() > 500 else None


def match_tone(img, ref_tone):
    """Apply a per-channel gain so the armor tone of this frame matches the idle reference (clipped, RGB only)."""
    a = np.asarray(img).copy()
    t = armor_tone(a)
    if t is None:
        return img
    gain = np.clip(ref_tone / np.maximum(t, 1.0), 0.85, 1.9)
    a[..., :3] = np.clip(a[..., :3].astype(np.float32) * gain, 0, 255).astype(np.uint8)
    return Image.fromarray(a, "RGBA")


def place(img, fid, dx=0):
    """Place a cut-out image on the canvas: lowest 6% of pixels = feet -> pivot/baseline."""
    a = np.asarray(img)[..., 3]
    ys, xs = np.nonzero(a)
    bottom, top = ys.max(), ys.min()
    low = ys > bottom - max(8, int((bottom - top) * 0.06))
    fx = int(xs[low].mean())
    canvas = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    ox, oy = PIVOT_X - fx + dx, BASE_Y - HOVER.get(fid, 0) - int(bottom)
    canvas.paste(img, (ox, oy), img)
    return canvas


def fade(img, alpha):
    """Return the frame with its opacity multiplied by alpha (used for the afterimage trail)."""
    out = img.copy()
    out.putalpha(img.getchannel("A").point(lambda v: int(v * alpha)))
    return out


def main():
    ids = [f for _, (_, _, fs, _, _, _) in ATTACKS.items() for f in fs] + ["IDLE"]
    cut, ruler = {}, {}
    for fid in ids:
        cut[fid] = Image.fromarray(cutout(os.path.join(SRC, f"{fid}.png")), "RGBA")
        ruler[fid] = body_ruler(np.asarray(cut[fid]))
    ref_tone = armor_tone(np.asarray(cut["IDLE"]))
    for fid in ids:                                    # even out tone before scaling
        cut[fid] = match_tone(cut[fid], ref_tone)
    target = float(np.median(list(ruler.values())))
    manifest = {"canvas": [CW, CH], "baselineY": BASE_Y, "pivotX": PIVOT_X, "targetRuler": target, "frames": {}, "attacks": {}}
    scaled = {}
    for fid in ids:
        s = float(np.clip((target / ruler[fid]) ** 0.6, 0.88, 1.12))   # damped so pose changes are not mistaken for size changes
        im = cut[fid]
        scaled[fid] = im.resize((max(1, int(im.width * s)), max(1, int(im.height * s))), Image.LANCZOS)
        manifest["frames"][fid] = {"ruler": round(ruler[fid], 1), "scale": round(s, 3)}
    os.makedirs(os.path.join(HERE, "final"), exist_ok=True)
    os.makedirs(os.path.join(HERE, "gif"), exist_ok=True)
    for fid in ids:
        place(scaled[fid], fid).save(os.path.join(HERE, "final", f"{fid}.png"))

    BG = (22, 18, 26, 255)
    gscale = 0.5
    gw, gh = int(CW * gscale), int(CH * gscale)

    def compose(layers, shake=(0, 0), flash=0.0):
        bg = Image.new("RGBA", (CW, CH), BG)
        for L in layers:
            bg.alpha_composite(L, (shake[0], shake[1]))
        d = ImageDraw.Draw(bg)
        d.line([(0, BASE_Y), (CW, BASE_Y)], fill=(80, 80, 80, 255), width=4)
        if flash > 0:
            bg = Image.blend(bg, Image.new("RGBA", bg.size, (255, 190, 110, 255)), flash)
        return bg.convert("RGB").resize((gw, gh), Image.LANCZOS)

    for key, (name, cat, fids, holds, imp, offs) in ATTACKS.items():
        frames, durs = [], []
        idle = place(scaled["IDLE"], "IDLE")
        frames.append(compose([idle])); durs.append(160)                # neutral start
        prev_layer = idle
        for i, fid in enumerate(fids):
            cur = place(scaled[fid], fid, offs[i])
            if i == imp:                                                # smear frame: blend previous -> impact
                sm = Image.blend(prev_layer, cur, 0.5)
                frames.append(compose([sm])); durs.append(40)
            layers = [cur]
            if i >= imp - 1 and i > 0:                                  # afterimage trail behind fast frames
                layers = [fade(prev_layer, 0.22), cur]
            if i == imp:                                                # impact: shake + warm flash
                for sh, fl, du in (((7, -4), 0.07, 45), ((-5, 3), 0.0, 45), ((0, 0), 0.0, max(20, holds[i] - 90))):
                    frames.append(compose(layers, sh, fl)); durs.append(du)
            else:
                frames.append(compose(layers)); durs.append(holds[i])
            prev_layer = cur
        mid = Image.blend(prev_layer, idle, 0.5)                        # recovery back to neutral
        frames.append(compose([mid])); durs.append(90)
        frames.append(compose([idle])); durs.append(260)
        pal = [f.convert("P", palette=Image.ADAPTIVE) for f in frames]
        pal[0].save(os.path.join(HERE, "gif", f"{key}.gif"), save_all=True, append_images=pal[1:], duration=durs, loop=0, disposal=2)
        manifest["attacks"][key] = {"name": name, "category": cat, "frames": fids, "holdMs": holds, "impactIndex": imp, "forwardOffsetPx": offs}

    # contact sheet: one row per attack
    cw, ch = 330, 230
    sheet = Image.new("RGB", (cw * 6, ch * len(ATTACKS)), BG[:3])
    sd = ImageDraw.Draw(sheet)
    for row, (key, (name, cat, fids, *_)) in enumerate(ATTACKS.items()):
        for col, fid in enumerate(["IDLE"] + fids):
            fr = place(scaled[fid], fid)
            bg = Image.new("RGBA", (CW, CH), BG)
            bg.alpha_composite(fr)
            crop = bg.crop((100, 150, 1900, 1690)).convert("RGB")
            crop.thumbnail((cw - 6, ch - 6))
            sheet.paste(crop, (col * cw + 3, row * ch + 3))
            sd.text((col * cw + 8, row * ch + 6), f"{fid} {key} [{cat}]", fill=(255, 220, 80))
    sheet.save(os.path.join(HERE, "BossPreview_Stage5_v2.png"))
    with open(os.path.join(HERE, "animation_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
    print("done", len(ids), "frames; target ruler", round(target, 1))


if __name__ == "__main__":
    main()
