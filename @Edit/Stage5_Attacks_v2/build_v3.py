"""
build_v3.py - Stage 5 boss (Twin-Blade Demon Overlord): 5-attack structure (design v3), GIFs and preview.

AI attestation (rule 54): written by Claude Sonnet 5.5, 2026-10-15.

Design v3 (user decision, 2026-10-15): five attack motions.
  Basic attacks  N1 / N2 / N3   : used alone, or as the opener of a chain.
  Chain attacks  N4 + N5 ("특공 4+5"): always played together right after a basic attack.
Patterns (6): N1, N2, N3 alone  |  N1+N4+N5, N2+N4+N5, N3+N4+N5.
Chained N4/N5 skip their anticipation pose (the preceding attack already telegraphs), so they start at pose 2.

This script reuses the cut-out / tone / scale helpers of build_v2.py and writes:
  final/<ID>.png            aligned frames (2000x1700 RGBA, feet on baseline)
  gif/<Pattern>.gif         one looping GIF per pattern (timing, lunge offset, smear, afterimage, impact shake/flash)
  BossPreview_Stage5_v3.png contact sheet, one row per pattern
  animation_manifest.json   pattern data used by the Unity test scene
Run:  python build_v3.py   (inside @Edit/Stage5_Attacks_v2)
"""
import json
import os
import numpy as np
from PIL import Image, ImageDraw
import build_v2 as B

HERE = B.HERE
CW, CH, BASE_Y = B.CW, B.CH, B.BASE_Y

# step = (frame id, hold seconds, forward lunge px, is_hit_frame)
N1 = [("N1_1", .11, -8, False), ("N1_2", .15, -22, False), ("N1_3", .08, 34, True), ("N1_4", .15, 52, False)]
N2 = [("N2_1", .12, -6, False), ("N2_2", .16, -20, False), ("N2_3", .07, 20, False), ("N2_4", .17, 48, True)]
N3 = [("N3_1", .11, -8, False), ("N3_2", .16, -24, False), ("N3_3", .07, 26, False), ("N3_4", .17, 56, True)]
C4 = [("N4_2", .15, 0, False), ("N4_3", .07, 22, False), ("N4_4", .17, 46, True)]     # chain attack 4 (starts at the windup pose)
C5 = [("N5_2", .16, 10, False), ("N5_3", .07, 28, False), ("N5_4", .18, 40, True)]    # chain attack 5

PATTERNS = {
    "P1_N1": ("일공1", "중단", N1),
    "P2_N2": ("일공2", "하단", N2),
    "P3_N3": ("일공3", "중단", N3),
    "P4_N1_4_5": ("일공1+특공4+5", "중단→중단→하단", N1 + C4 + C5),
    "P5_N2_4_5": ("일공2+특공4+5", "하단→중단→하단", N2 + C4 + C5),
    "P6_N3_4_5": ("일공3+특공4+5", "중단→중단→하단", N3 + C4 + C5),
}


def main():
    ids = sorted({s[0] for _, (_, _, steps) in PATTERNS.items() for s in steps} | {"N4_1", "N5_1"}) + ["IDLE"]
    cut = {f: Image.fromarray(B.cutout(os.path.join(B.SRC, f + ".png")), "RGBA") for f in ids}
    ruler = {f: B.body_ruler(np.asarray(cut[f])) for f in ids}
    ref_tone = B.armor_tone(np.asarray(cut["IDLE"]))
    for f in ids:
        cut[f] = B.match_tone(cut[f], ref_tone)
    target = float(np.median(list(ruler.values())))
    scaled, manifest = {}, {"canvas": [CW, CH], "baselineY": BASE_Y, "pivotX": B.PIVOT_X, "frames": {}, "patterns": {}}
    for f in ids:
        s = float(np.clip((target / ruler[f]) ** 0.6, 0.88, 1.12))
        im = cut[f]
        scaled[f] = im.resize((max(1, int(im.width * s)), max(1, int(im.height * s))), Image.LANCZOS)
        manifest["frames"][f] = {"ruler": round(ruler[f], 1), "scale": round(s, 3)}
    os.makedirs(os.path.join(HERE, "final"), exist_ok=True)
    os.makedirs(os.path.join(HERE, "gif"), exist_ok=True)
    for old in os.listdir(os.path.join(HERE, "gif")):      # old per-attack GIFs of v2 are replaced by the pattern GIFs
        os.remove(os.path.join(HERE, "gif", old))
    for f in ids:
        B.place(scaled[f], f).save(os.path.join(HERE, "final", f + ".png"))

    BG = (22, 18, 26, 255)
    gw, gh = int(CW * 0.5), int(CH * 0.5)

    def compose(layers, shake=(0, 0), flash=0.0):
        bg = Image.new("RGBA", (CW, CH), BG)
        for L in layers:
            bg.alpha_composite(L, shake)
        ImageDraw.Draw(bg).line([(0, BASE_Y), (CW, BASE_Y)], fill=(80, 80, 80, 255), width=4)
        if flash > 0:
            bg = Image.blend(bg, Image.new("RGBA", bg.size, (255, 190, 110, 255)), flash)
        return bg.convert("RGB").resize((gw, gh), Image.LANCZOS)

    for key, (label, cat, steps) in PATTERNS.items():
        idle = B.place(scaled["IDLE"], "IDLE")
        frames, durs = [compose([idle])], [160]
        prev = idle
        for i, (fid, hold, off, hit) in enumerate(steps):
            cur = B.place(scaled[fid], fid, off)
            if hit:
                frames.append(compose([Image.blend(prev, cur, 0.5)])); durs.append(40)      # smear frame into the hit
            layers = [cur]
            if i > 0 and (hit or steps[min(i + 1, len(steps) - 1)][3]):                      # afterimage on the fast frames
                layers = [B.fade(prev, 0.22), cur]
            if hit:                                                                          # impact: shake + faint flash
                for sh, fl, du in (((7, -4), 0.07, 45), ((-5, 3), 0.0, 45), ((0, 0), 0.0, max(20, int(hold * 1000) - 90))):
                    frames.append(compose(layers, sh, fl)); durs.append(du)
            else:
                frames.append(compose(layers)); durs.append(int(hold * 1000))
            prev = cur
        frames.append(compose([Image.blend(prev, idle, 0.5)])); durs.append(90)             # recovery to neutral
        frames.append(compose([idle])); durs.append(260)
        pal = [fr.convert("P", palette=Image.ADAPTIVE) for fr in frames]
        pal[0].save(os.path.join(HERE, "gif", key + ".gif"), save_all=True, append_images=pal[1:], duration=durs, loop=0, disposal=2)
        manifest["patterns"][key] = {"label": label, "category": cat,
                                     "steps": [{"frame": s[0], "hold": s[1], "offsetPx": s[2], "hit": s[3]} for s in steps]}

    cw, ch, cols = 280, 200, 11                                     # contact sheet: IDLE + every pose of the pattern
    sheet = Image.new("RGB", (cw * cols, ch * len(PATTERNS)), BG[:3])
    sd = ImageDraw.Draw(sheet)
    for row, (key, (label, cat, steps)) in enumerate(PATTERNS.items()):
        for col, fid in enumerate(["IDLE"] + [s[0] for s in steps]):
            bg = Image.new("RGBA", (CW, CH), BG)
            bg.alpha_composite(B.place(scaled[fid], fid))
            crop = bg.crop((100, 650, 1900, 1690)).convert("RGB")
            crop.thumbnail((cw - 4, ch - 4))
            sheet.paste(crop, (col * cw + 2, row * ch + 2))
            sd.text((col * cw + 6, row * ch + 4), f"{key.split('_', 1)[0]} {fid}", fill=(255, 220, 80))   # ASCII only: default font has no Hangul
    sheet.save(os.path.join(HERE, "BossPreview_Stage5_v3.png"))
    with open(os.path.join(HERE, "animation_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
    print("done", len(ids), "frames,", len(PATTERNS), "patterns")


if __name__ == "__main__":
    main()
