"""엔딩 영상 합성: 키프레임 5장 + 기사 레이어 + 연출 -> mp4(+선택 BGM).

사용: python build_ending.py <keyframes 폴더> <기사 레이어 png> <출력 mp4> [bgm.mp3] [--preview=t1,t2,...]
폴더 필요 파일: k1~k5.png, collapse_mask.png (make_mask.py 로 생성)
  k1=원본 배경, k2=숲+금간 마왕성, k3=호수, k4=묘목, k5=거대한 나무

타임라인(초)
  0~3     원본(오프닝 배경) 정지, 천천히 줌
  3~8     3~4스테이지(화산·어두운 동굴)가 재와 불씨로 흩어져 사라지고 숲이 드러남, 하늘이 갬
  9~16.5  스테이지5(성+고원) 진동 -> 열별로 무너져 내리며 먼지·파편, 그 자리에 호수가 드러남
  16.5~19 호수 정지
  19~22   묘목 돋음 / 22.5~28.5 거대한 나무로 성장(빛줄기·꽃잎)
  29~31   THE END, 마지막 1.5초 페이드 아웃
"""
import math
import subprocess
import sys

import imageio_ffmpeg
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

W, H, FPS = 1920, 1080, 30
DUR = 36.0
FFMPEG = imageio_ffmpeg.get_ffmpeg_exe()

DISS = (3.0, 8.0)        # 3~4스테이지 소멸
COLLAPSE = (10.0, 16.0)  # 성+고원 붕괴
SHAKE = (9.0, 16.5)
SKY23 = (13.5, 16.5)     # 붕괴 영역 밖(하늘 등) K2->K3
SEG45 = {4: (19.0, 22.0), 5: (22.5, 28.5)}
GROW0 = 19.0
ROOT = "C:/Users/MBC-501-21/Documents/FinalProject"
# 현재 엔딩 씬(EndingSceneController)의 자막 그대로: (문구, 시작, 끝)
SUBS = [
    ("마왕은 쓰러졌다.", 3.4, 6.6),
    ("어둠에 잠겨 있던 땅 위로, 오랜만에 새벽빛이 번져 간다.", 7.0, 10.4),
    ("숲을 지나, 이끼 낀 동굴을 지나, 용암과 어둠의 땅을 넘어 — 기사의 긴 여정은 이곳에서 끝났다.", 10.8, 17.0),
    ("기사는 칼을 거두고, 밝아 오는 절벽 위에서 조용히 지평선을 바라보았다.", 17.6, 24.0),
]
THANKS = "플레이해 주셔서 감사합니다"
FINAL0 = 28.0  # 마지막 화면(로고/THE END/감사 문구) 시작
LAKE_C = (977, 633)
rng = np.random.default_rng(7)


def smooth(x):
    x = min(max(x, 0.0), 1.0)
    return x * x * (3 - 2 * x)


def smooth_arr(x):
    x = np.clip(x, 0.0, 1.0)
    return x * x * (3 - 2 * x)


def load(path):
    return Image.open(path).convert("RGB").resize((W, H), Image.LANCZOS)


def blur_noise(shape, sigma, seed):
    r = np.random.default_rng(seed).random(shape).astype(np.float32)
    im = Image.fromarray((r * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(sigma))
    a = np.asarray(im, np.float32)
    a = (a - a.min()) / (a.max() - a.min() + 1e-6)
    return a


class Assets:
    def __init__(self, kdir, knight_path):
        self.k = {i: np.asarray(load(f"{kdir}/k{i}.png"), np.float32) for i in range(1, 6)}
        self.mask = np.asarray(Image.open(f"{kdir}/collapse_mask.png").convert("L"), np.float32) / 255.0
        self.knight = Image.open(knight_path).convert("RGBA")
        self.font = ImageFont.truetype("C:/Windows/Fonts/georgiab.ttf", 150)
        self.kfont = ImageFont.truetype("C:/Windows/Fonts/malgunbd.ttf", 50)
        self.tfont = ImageFont.truetype("C:/Windows/Fonts/malgunbd.ttf", 52)
        self.logo = Image.open(f"{ROOT}/Assets/Resources/Ending/Ending_Logo.png").convert("RGBA")
        lw = 1030
        self.logo = self.logo.resize((lw, int(self.logo.height * lw / self.logo.width)), Image.LANCZOS)
        yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
        # 소멸 순서: 화산·동굴 쪽(0)에서 먼 곳(1)일수록 늦게 사라진다
        d1 = np.hypot(xx - 1180, yy - 900)
        d2 = np.hypot(xx - 1750, yy - 680)
        zone = np.clip(np.minimum(d1, d2) / 1400.0, 0, 1)
        self.zone = zone
        self.diss_order = 0.55 * blur_noise((H, W), 14, 11) + 0.45 * zone
        # 붕괴: 열(세로 띠)마다 시작 시점·낙하 거리가 다르다
        self.strip_w = 40
        n = W // self.strip_w + 1
        centre = np.abs(np.arange(n) * self.strip_w - 960) / 700.0
        self.strip_delay = np.clip(0.55 * rng.random(n) + 0.25 * centre, 0, 0.8)
        self.strip_drop = 380 + 330 * rng.random(n)
        # 파편: 마스크 안 임의 위치의 색을 따서 떨어뜨린다
        ys, xs = np.nonzero(self.mask > 0.6)
        idx = rng.choice(len(ys), 260, replace=False)
        self.deb = {
            "x": xs[idx].astype(np.float32), "y": ys[idx].astype(np.float32),
            "vx": rng.uniform(-90, 90, 260).astype(np.float32), "vy": rng.uniform(-60, 30, 260).astype(np.float32),
            "s": rng.integers(4, 13, 260), "t0": rng.uniform(COLLAPSE[0], COLLAPSE[0] + 4.5, 260),
            "c": self.k[2][ys[idx], xs[idx]].astype(np.uint8),
        }
        self.embers = {
            "x": rng.uniform(0, W, 160), "y": rng.uniform(H * 0.45, H, 160),
            "v": rng.uniform(70, 220, 160), "ph": rng.uniform(0, 6.28, 160), "s": rng.uniform(1.5, 4.5, 160),
        }
        self.sparkles = {
            "x": rng.uniform(0, 1, 140), "y": rng.uniform(0, 1, 140), "s": rng.uniform(0.6, 2.2, 140),
            "v": rng.uniform(0.01, 0.045, 140), "ph": rng.uniform(0, 6.28, 140), "sw": rng.uniform(0.01, 0.04, 140),
        }
        self.dust = {
            "x": rng.uniform(800, 1500, 70), "y": rng.uniform(420, 640, 70), "t0": rng.uniform(COLLAPSE[0] + 0.3, COLLAPSE[1] - 1.0, 70),
            "r": rng.uniform(50, 130, 70), "vx": rng.uniform(-25, 25, 70),
        }


def add(frame, layer, k=1.0):
    return np.clip(frame + layer * k, 0, 255)


def stage_background(t, A):
    """배경 float 배열(HxWx3). 소멸·붕괴·성장 단계를 합쳐서 만든다."""
    k = A.k
    img = k[1].copy()
    glow = np.zeros((H, W), np.float32)

    # (1) 3~4스테이지 소멸: 노이즈 순서로 K1 -> K2, 경계에 주황빛
    p = (t - DISS[0]) / (DISS[1] - DISS[0])
    if p > 0:
        a = smooth_arr((p * 1.45 - A.diss_order) / 0.28)
        img = k[1] * (1 - a[..., None]) + k[2] * a[..., None]
        glow = np.clip(1.0 - np.abs(a - 0.35) / 0.35, 0, 1) * (a > 0.01) * (a < 0.99) * np.clip(1.0 - A.zone * 1.6, 0, 1)
        glow = np.asarray(Image.fromarray((glow * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(3)), np.float32) / 255.0
        if p >= 1.0:
            img = k[2].copy()
    if t >= DISS[1]:
        img = k[2].copy()

    # (2) 붕괴: 영역 밖은 하늘 전환, 영역 안은 호수가 드러남(이동 레이어는 별도)
    layer_info = None
    if t > COLLAPSE[0] - 0.5:
        m = A.mask
        s = smooth((t - SKY23[0]) / (SKY23[1] - SKY23[0]))
        base = k[2] * (1 - s) + k[3] * s
        cp = (t - COLLAPSE[0]) / (COLLAPSE[1] - COLLAPSE[0])
        inner = k[3]
        mm = m[..., None]
        img = base * (1 - mm) + inner * mm
        layer_info = cp

    # (3) 묘목 / 거대한 나무
    if t >= SKY23[1]:
        img = k[3].copy()
    for kk, (a0, b0) in SEG45.items():
        q = smooth((t - a0) / (b0 - a0))
        if q > 0:
            img = img * (1 - q) + k[kk] * q
    return img, glow, layer_info


def crumble_layer(frame, t, A, cp):
    """고원+성을 열 단위로 떨어뜨려 합성."""
    sw = A.strip_w
    src = A.k[2]
    m = A.mask
    out = frame
    n = W // sw + 1
    for i in range(n):
        x0, x1 = i * sw, min((i + 1) * sw, W)
        if x0 >= W:
            break
        local = (cp - A.strip_delay[i] * 0.55) / 0.55
        local = min(max(local, 0.0), 1.0)
        dy = int(A.strip_drop[i] * local * local)
        jitter = 0
        sm = m[:, x0:x1]
        sc = src[:, x0:x1]
        if dy > 0:
            sm = np.roll(sm, dy, axis=0)
            sm[:dy] = 0
            sc = np.roll(sc, dy, axis=0)
            sc[:dy] = 0
        fade = 1.0 - smooth((local - 0.55) / 0.45)  # 가라앉으면서 사라진다
        al = (sm * fade)[..., None]
        xs0, xs1 = x0 + jitter, x1 + jitter
        if xs0 < 0 or xs1 > W:
            continue
        out[:, xs0:xs1] = out[:, xs0:xs1] * (1 - al) + sc * al
    return out


def draw_effects(frame, t, A):
    pil = Image.fromarray(np.clip(frame, 0, 255).astype(np.uint8))
    # 불씨 (소멸 구간)
    p = (t - DISS[0]) / (DISS[1] - DISS[0])
    if 0 < p < 1.15:
        lay = Image.new("RGB", (W, H), (0, 0, 0))
        d = ImageDraw.Draw(lay)
        amp = math.sin(math.pi * min(p, 1.0))
        e = A.embers
        for i in range(160):
            y = (e["y"][i] - e["v"][i] * (t - DISS[0])) % H
            x = e["x"][i] + math.sin(t * 2 + e["ph"][i]) * 18
            tw = 0.5 + 0.5 * math.sin(t * 9 + e["ph"][i])
            r = e["s"][i] * (0.6 + tw)
            a = amp * tw * (0.35 if (x < 800 and y < 500) else 1.0)
            d.ellipse((x - r, y - r, x + r, y + r), fill=(int(255 * a), int(130 * a), int(30 * a)))
        lay = lay.filter(ImageFilter.GaussianBlur(1.2))
        pil = Image.fromarray(np.clip(np.asarray(pil, np.float32) + np.asarray(lay, np.float32), 0, 255).astype(np.uint8))

    # 먼지 + 파편 + 물결 (붕괴 구간)
    if COLLAPSE[0] - 0.2 < t < COLLAPSE[1] + 2.5:
        pil = pil.convert("RGBA")
        ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        d = ImageDraw.Draw(ov)
        du = A.dust
        for i in range(70):
            tt = t - du["t0"][i]
            if tt < 0 or tt > 3.5:
                continue
            q = tt / 3.5
            r = du["r"][i] * (0.5 + 1.6 * q)
            x = du["x"][i] + du["vx"][i] * tt
            y = du["y"][i] - 90 * q
            al = int(150 * math.sin(math.pi * q) ** 0.8)
            d.ellipse((x - r, y - r * 0.7, x + r, y + r * 0.7), fill=(150, 125, 100, al))
        ov = ov.filter(ImageFilter.GaussianBlur(14))
        d2 = ImageDraw.Draw(ov)
        de = A.deb
        for i in range(260):
            tt = t - de["t0"][i]
            if tt < 0:
                continue
            x = de["x"][i] + de["vx"][i] * tt
            y = de["y"][i] + de["vy"][i] * tt + 0.5 * 900 * tt * tt
            if y > H + 20 or y < -20:
                continue
            s = int(de["s"][i])
            c = tuple(int(v) for v in de["c"][i])
            d2.rectangle((x, y, x + s, y + s), fill=c + (255,))
        for j in range(3):
            tj = t - COLLAPSE[0] - 2.5 - j * 0.9
            if tj < 0 or t > COLLAPSE[1] + 2.5:
                continue
            q = (tj % 3.0) / 3.0
            rx, ry = 120 + 520 * q, 30 + 150 * q
            al = int(170 * (1 - q) * (1 - smooth((t - (COLLAPSE[1] + 0.5)) / 2.0)))
            d2.ellipse((LAKE_C[0] - rx, LAKE_C[1] - 40 - ry, LAKE_C[0] + rx, LAKE_C[1] - 40 + ry),
                       outline=(255, 255, 255, max(al, 0)), width=3)
        pil = Image.alpha_composite(pil, ov).convert("RGB")
    return pil


def rays_and_sparkles(pil, t, A):
    arr = np.asarray(pil, np.float32)
    p = smooth((t - 19.5) / 4.0) * (1.0 - smooth((t - 33.0) / 2.5)) * 0.35
    if p > 0.01:
        lay = Image.new("RGB", (W, H), (0, 0, 0))
        d = ImageDraw.Draw(lay)
        cx, cy = LAKE_C[0], LAKE_C[1] - 300
        for i in range(9):
            ang = math.radians(-60 + i * 15 + math.sin(t * 0.4 + i) * 3)
            L = 1300
            w = 60 + 25 * math.sin(t * 0.7 + i * 1.7)
            d.polygon([(cx, cy), (cx + math.sin(ang) * L - w, cy + math.cos(ang) * L),
                       (cx + math.sin(ang) * L + w, cy + math.cos(ang) * L)],
                      fill=(int(255 * p), int(225 * p), int(140 * p)))
        lay = lay.filter(ImageFilter.GaussianBlur(40))
        arr = add(arr, np.asarray(lay, np.float32), 0.6)
    st = smooth((t - 15.0) / 4.0) * 0.9
    if st > 0.01:
        lay = Image.new("RGB", (W, H), (0, 0, 0))
        d = ImageDraw.Draw(lay)
        s = A.sparkles
        for i in range(140):
            y = (s["y"][i] - s["v"][i] * t) % 1.0
            x = (s["x"][i] + math.sin(t * 0.8 + s["ph"][i]) * s["sw"][i]) % 1.0
            tw = 0.5 + 0.5 * math.sin(t * 3.0 + s["ph"][i] * 3)
            r = s["s"][i] * (2.0 + 2.5 * tw)
            a = st * (0.4 + 0.6 * tw)
            d.ellipse((x * W - r, y * H - r, x * W + r, y * H + r), fill=(int(255 * a), int(235 * a), int(150 * a)))
        lay = lay.filter(ImageFilter.GaussianBlur(1.6))
        arr = add(arr, np.asarray(lay, np.float32))
    return Image.fromarray(arr.astype(np.uint8))


def camera(t):
    zoom = 1.0 + 0.04 * smooth(t / 16.0)
    cx, cy = 0.5, 0.5
    if t > GROW0:
        p = smooth((t - GROW0) / 9.5)
        zoom = 1.04 + 0.20 * p
        cx = 0.5 + (LAKE_C[0] / W - 0.5) * 0.55 * p
        cy = 0.5 - 0.12 * p
    if t > 28.5:
        zoom += 0.01 * (t - 28.5)
    return zoom, cx, cy


def shake_offset(t):
    if not (SHAKE[0] <= t <= SHAKE[1]):
        return 0, 0
    amp = 14 * math.sin(math.pi * (t - SHAKE[0]) / (SHAKE[1] - SHAKE[0])) ** 0.7
    return int(math.sin(t * 53) * amp), int(math.cos(t * 61) * amp * 0.7)


def wrap_text(draw, text, font, max_w):
    lines, cur = [], ""
    for word in text.split(" "):
        trial = (cur + " " + word).strip()
        if draw.textlength(trial, font=font) <= max_w:
            cur = trial
        else:
            lines.append(cur)
            cur = word
    lines.append(cur)
    return lines


def text_with_outline(d, xy, txt, font, fill, alpha):
    x, y = xy
    for dx, dy in [(-3, -3), (3, -3), (-3, 3), (3, 3), (0, 4), (4, 0), (-4, 0), (0, -4)]:
        d.text((x + dx, y + dy), txt, font=font, fill=(0, 0, 0, int(230 * alpha)))
    d.text((x, y), txt, font=font, fill=fill[:3] + (int(255 * alpha),))


def draw_text_layers(frame, t, A):
    """자막(이야기 문구) + 마지막 화면(어둠막, 로고, THE END, 감사 문구)."""
    base = frame.convert("RGBA")
    # 마지막 화면 어둠막
    fd = smooth((t - FINAL0) / 1.4) * 0.55
    if fd > 0:
        base = Image.alpha_composite(base, Image.new("RGBA", (W, H), (0, 0, 0, int(255 * fd))))
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)

    # 이야기 자막: 아래 어두운 띠 + 문구(나타남 0.9s / 사라짐 0.7s)
    cur = None
    for txt, a0, b0 in SUBS:
        al = smooth((t - a0) / 0.9) * (1 - smooth((t - (b0 - 0.7)) / 0.7))
        if al > 0:
            cur = (txt, al)
    band = max([smooth((t - 3.0) / 0.6) * (1 - smooth((t - (SUBS[-1][2] - 0.2)) / 0.8))], default=0) * 0.55
    if band > 0:
        d.rectangle((0, H - 250, W, H), fill=(0, 0, 0, int(255 * band)))
    if cur:
        txt, al = cur
        lines = wrap_text(d, txt, A.kfont, 1500)
        lh = 70
        y0 = H - 125 - lh * len(lines) / 2
        for i, ln in enumerate(lines):
            w = d.textlength(ln, font=A.kfont)
            text_with_outline(d, ((W - w) / 2, y0 + i * lh), ln, A.kfont, (255, 242, 209), al)

    # 마지막 화면: 로고 -> THE END -> 감사 문구
    la = smooth((t - (FINAL0 + 0.6)) / 1.4)
    if la > 0:
        lg = A.logo.copy()
        lg.putalpha(lg.getchannel("A").point(lambda v: int(v * la)))
        ov.alpha_composite(lg, ((W - lg.width) // 2, 290 - lg.height // 2))
    ea = smooth((t - (FINAL0 + 1.8)) / 1.4)
    if ea > 0:
        w = d.textlength("THE END", font=A.font)
        text_with_outline(d, ((W - w) / 2, 540 - 95), "THE END", A.font, (255, 230, 160), ea)
    ta = smooth((t - (FINAL0 + 3.2)) / 1.2)
    if ta > 0:
        w = d.textlength(THANKS, font=A.tfont)
        text_with_outline(d, ((W - w) / 2, 690 - 35), THANKS, A.tfont, (255, 237, 179), ta)
    return Image.alpha_composite(base, ov).convert("RGB")


def render(t, A):
    img, glow, cp = stage_background(t, A)
    if cp is not None and t < COLLAPSE[1] + 0.2:
        img = crumble_layer(img, t, A, min(cp, 1.0))
    if glow.any():
        img = add(img, glow[..., None] * np.array([255, 120, 30], np.float32), 0.85)
    for a0, b0 in SEG45.values():  # 묘목·나무 단계 전환 하얀 빛
        if a0 <= t <= b0:
            g = math.sin(math.pi * (t - a0) / (b0 - a0)) * 0.3
            img = img * (1 - g * 0.5) + 255 * g * 0.5 * np.array([1.0, 0.97, 0.85], np.float32)
    frame = draw_effects(img, t, A)
    frame.paste(A.knight, (0, 0), A.knight)

    zoom, cx, cy = camera(t)
    sx, sy = shake_offset(t)
    cw, ch = W / zoom, H / zoom
    x0 = min(max(cx * W - cw / 2 + sx, 0), W - cw)
    y0 = min(max(cy * H - ch / 2 + sy, 0), H - ch)
    frame = frame.resize((W, H), Image.BICUBIC, box=(x0, y0, x0 + cw, y0 + ch))
    frame = rays_and_sparkles(frame, t, A)

    frame = draw_text_layers(frame, t, A)

    fade = smooth(t / 1.5)   # 시작만 검은 화면에서 밝아짐(끝은 유지, 페이드 아웃은 Unity 가 담당)
    if fade < 1.0:
        frame = Image.fromarray((np.asarray(frame, np.float32) * fade).astype(np.uint8))
    return frame


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    preview = None
    for a in sys.argv[1:]:
        if a.startswith("--preview"):
            preview = [float(x) for x in a.split("=", 1)[1].split(",")]
    kdir, knight_path, out = args[0], args[1], args[2]
    bgm = args[3] if len(args) > 3 else None
    A = Assets(kdir, knight_path)

    if preview:
        for t in preview:
            render(t, A).save(f"{kdir}/preview_{t:05.1f}.png")
        return

    cmd = [FFMPEG, "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-"]
    if bgm:
        cmd += ["-i", bgm]
    cmd += ["-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "18", "-preset", "medium"]
    if bgm:
        cmd += ["-c:a", "aac", "-b:a", "192k", "-af", f"afade=t=in:d=1.5,afade=t=out:st={DUR - 4}:d=4", "-t", str(DUR)]
    cmd += [out]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    total = int(DUR * FPS)
    for i in range(total):
        proc.stdin.write(np.asarray(render(i / FPS, A)).tobytes())
        if i % 150 == 0:
            print(f"{i}/{total}", flush=True)
    proc.stdin.close()
    proc.wait()


if __name__ == "__main__":
    main()
