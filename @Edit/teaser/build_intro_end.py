"""티저의 인트로(30초)와 엔딩(34초)을 프로젝트의 오프닝 일러스트(원본 PNG)로 합성한다.
레이어: Opening_Background / Opening_Player / Opening_Title + 자막 텍스트. 느린 줌(켄 번즈)과 페이드로 연출."""
import os, subprocess, sys
import numpy as np
import cv2
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = r'C:\Users\catkl\OneDrive\문서\Unity'
SPR = os.path.join(ROOT, 'Assets', 'Sprites', 'Opening')
SCR = os.path.dirname(os.path.abspath(__file__))
CARDS = os.path.join(SCR, 'cards')
CLIPS = os.path.join(SCR, 'clips')
FF = r'C:\Users\catkl\AppData\Local\Programs\Python\Python311\Lib\site-packages\imageio_ffmpeg\binaries\ffmpeg-win-x86_64-v7.1.exe'
BOLD = r'C:\Windows\Fonts\malgunbd.ttf'
REG = r'C:\Windows\Fonts\malgun.ttf'
W, H, FPS = 1920, 1080, 30


def load_rgba(p):
    return Image.open(p).convert('RGBA')


def text_layer(items):
    """items: (text, size, center_y, bold, color). 그림자 포함 RGBA 레이어를 만든다."""
    layer = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    sh = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    sd = ImageDraw.Draw(sh)
    d = ImageDraw.Draw(layer)
    for text, size, cy, bold, color in items:
        f = ImageFont.truetype(BOLD if bold else REG, size)
        sd.text((W // 2 + 3, cy + 4), text, font=f, fill=(0, 0, 0, 230), anchor='mm')
        d.text((W // 2, cy), text, font=f, fill=color, anchor='mm')
    sh = sh.filter(ImageFilter.GaussianBlur(5))
    out = Image.alpha_composite(sh, layer)
    return out


def premult(img_rgba):
    a = np.asarray(img_rgba, dtype=np.float32)
    alpha = a[..., 3:4] / 255.0
    return a[..., :3] * alpha, alpha


def ease(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3 - 2 * t)


def ramp(t, a, b):
    """t가 a~b 구간에서 0->1로 부드럽게 올라가는 값."""
    return ease((t - a) / (b - a)) if b > a else float(t >= b)


class Layer:
    def __init__(self, rgba):
        self.rgb, self.alpha = premult(rgba)

    def apply(self, frame, f):
        if f <= 0.001:
            return frame
        return frame * (1.0 - self.alpha * f) + self.rgb * f


# ----- 오프닝 레이어 -----
bg = np.asarray(load_rgba(os.path.join(SPR, 'Opening_Background.png')).convert('RGB'), dtype=np.float32)
hero = Layer(load_rgba(os.path.join(SPR, 'Opening_Player.png')))

logo_img = load_rgba(os.path.join(SPR, 'Opening_Title.png'))
title_full = Image.new('RGBA', (W, H), (0, 0, 0, 0))
title_full.alpha_composite(logo_img, ((W - logo_img.width) // 2, 405 - logo_img.height // 2))
ko = text_layer([('파이널 던전', 66, 690, True, (255, 255, 255, 255)),
                 ('마왕성을 향한 마지막 여정', 40, 790, False, (235, 235, 235, 255))])
title_full = Image.alpha_composite(title_full, ko)
rule = ImageDraw.Draw(title_full)
rule.rectangle((W // 2 - 465, 735, W // 2 + 465, 738), fill=(235, 235, 235, 200))
title = Layer(title_full)

cap1 = Layer(text_layer([('모든 길은 마왕성으로 이어진다', 62, 930, True, (255, 255, 255, 255))]))
cap2 = Layer(text_layer([('그리고, 한 명의 기사가 나선다', 62, 930, True, (255, 230, 170, 255))]))
soon = Layer(text_layer([('COMING SOON', 76, 930, True, (255, 214, 120, 255))]))


def zoom_frame(img, scale, fx, fy):
    """img를 (fx, fy) 비율 지점을 중심으로 scale배 확대해 W x H로 자른다."""
    cx, cy = fx * W, fy * H
    m = np.array([[scale, 0, cx - scale * cx], [0, scale, cy - scale * cy]], dtype=np.float32)
    return cv2.warpAffine(img, m, (W, H), flags=cv2.INTER_LANCZOS4, borderMode=cv2.BORDER_REPLICATE)


def open_writer(path):
    return subprocess.Popen([FF, '-y', '-v', 'error', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', f'{W}x{H}', '-r', str(FPS), '-i', '-',
                             '-c:v', 'libx264', '-crf', '17', '-preset', 'medium', '-pix_fmt', 'yuv420p', path], stdin=subprocess.PIPE)


def write(proc, frame):
    proc.stdin.write(np.clip(frame, 0, 255).astype(np.uint8).tobytes())


def build_intro(path, dur=30.0):
    proc = open_writer(path)
    n = int(dur * FPS)
    for i in range(n):
        t = i / FPS
        frame = bg.copy()
        frame = hero.apply(frame, ramp(t, 11.0, 14.0))              # 영웅 등장
        frame = cap1.apply(frame, min(ramp(t, 3.0, 4.2), 1 - ramp(t, 8.0, 9.2)))
        frame = cap2.apply(frame, min(ramp(t, 12.0, 13.2), 1 - ramp(t, 18.0, 19.2)))
        frame = title.apply(frame, ramp(t, 22.5, 25.0))             # 로고 등장
        s = 1.0 + 0.16 * (t / dur)                                  # 천천히 확대
        frame = zoom_frame(frame, s, 0.55, 0.40)
        frame *= ramp(t, 0.0, 1.5)                                  # 검은 화면에서 페이드 인
        frame *= 1.0 - ramp(t, dur - 0.6, dur)
        write(proc, frame)
    proc.stdin.close(); proc.wait()
    print('intro done', n, 'frames')


def build_end(path, recap_dur=2.4, final_dur=26.0):
    proc = open_writer(path)
    cards = [np.asarray(Image.open(os.path.join(CARDS, f'stage{i}.png')).convert('RGB'), dtype=np.float32) for i in range(1, 6)]
    n_card = int(recap_dur * FPS)
    xf = int(0.5 * FPS)
    # 회상 몽타주: 카드 5장, 서로 0.5초 디졸브, 각 카드는 느린 줌
    total = n_card * 5
    for i in range(total):
        k = min(i // n_card, 4)
        local = i % n_card
        zoom = lambda idx, loc: zoom_frame(cards[idx], 1.0 + 0.07 * loc / n_card, 0.5, 0.5)
        frame = zoom(k, local)
        if k < 4 and local >= n_card - xf:
            a = (local - (n_card - xf)) / xf
            frame = frame * (1 - a) + zoom_frame(cards[k + 1], 1.0, 0.5, 0.5) * a
        if i < 6:
            frame *= i / 6.0
        write(proc, frame)
    # 최종 카드: 오프닝 전체 일러스트 + 로고, 느린 줌 아웃, COMING SOON
    n_final = int(final_dur * FPS)
    base = hero.apply(bg.copy(), 1.0)
    base = title.apply(base, 1.0)
    for i in range(n_final):
        t = i / FPS
        frame = base.copy()
        frame = soon.apply(frame, ramp(t, 4.0, 5.5))
        s = 1.12 - 0.12 * (t / final_dur)
        frame = zoom_frame(frame, s, 0.5, 0.45)
        frame *= ramp(t, 0.0, 0.8)
        frame *= 1.0 - ramp(t, final_dur - 3.5, final_dur)
        write(proc, frame)
    proc.stdin.close(); proc.wait()
    print('end done', total + n_final, 'frames')


if __name__ == '__main__':
    which = sys.argv[1]
    if which == 'intro':
        build_intro(os.path.join(CLIPS, 'intro.mp4'))
    elif which == 'end':
        build_end(os.path.join(CLIPS, 'end.mp4'))
