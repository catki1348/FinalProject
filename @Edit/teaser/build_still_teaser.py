"""정지 이미지 티저 조립(플레이 영상 없음): 오프닝 일러스트 + 스테이지 카드 + 게임 장면 캡처 이미지 16장 + 엔딩.
느린 줌(켄 번즈), 크로스페이드, 비네팅, 자막 오버레이를 적용하고 바로크 음악을 합성한다."""
import os, subprocess, sys
import numpy as np
import cv2
from PIL import Image, ImageDraw, ImageFont, ImageFilter

SCR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, SCR)
import build_intro_end as bi   # 인트로/엔딩용 레이어와 공용 함수(ramp, zoom_frame 등)

SEL = os.path.join(SCR, 'selected')
CARDS = os.path.join(SCR, 'cards')
FF = bi.FF
W, H, FPS = 1920, 1080, 30
XF = 0.8                 # 크로스페이드 길이(초)
BOLD = bi.BOLD
AUDIO = r'C:\Users\catkl\OneDrive\문서\LHS\컨셉\Epic_baroque_journey_2.mp3'
OUT = r'C:\Users\catkl\OneDrive\문서\LHS\컨셉\FinalDungeon_Teaser_Stills_v1.mp4'
STILL_DIR_OUT = r'C:\Users\catkl\OneDrive\문서\LHS\컨셉\Teaser_Stills'

GOLD = (255, 214, 120, 255)
WHITE = (250, 250, 250, 255)
RED = (255, 96, 84, 255)


def caption(small, small_color, big, big_color=WHITE):
    """왼쪽 아래 자막(작은 줄 + 큰 줄) 레이어."""
    base = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    sh = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    f1 = ImageFont.truetype(BOLD, 40)
    f2 = ImageFont.truetype(BOLD, 76)
    x, y1, y2 = 96, 880, 962
    for d, dx in ((ImageDraw.Draw(sh), 3), (ImageDraw.Draw(base), 0)):
        d.text((x + dx, y1 + dx), small, font=f1, fill=(0, 0, 0, 230) if dx else small_color, anchor='ls')
        d.text((x + dx, y2 + dx), big, font=f2, fill=(0, 0, 0, 230) if dx else big_color, anchor='ls')
    sh = sh.filter(ImageFilter.GaussianBlur(5))
    return bi.Layer(Image.alpha_composite(sh, base))


def vignette():
    """가장자리를 살짝 어둡게 하는 곱셈 마스크."""
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    d = np.sqrt(((xx - W / 2) / (W / 2)) ** 2 + ((yy - H / 2) / (H / 2)) ** 2)
    return (1.0 - 0.38 * np.clip(d - 0.45, 0, 1) ** 1.4)[..., None]


VIG = vignette()


class StillItem:
    """정지 이미지 한 컷: 느린 줌 + (선택) 자막."""
    def __init__(self, path, dur, focus=(0.30, 0.62), zoom_in=True, cap=None, cap_time=(0.7, 5.0)):
        self.img = np.asarray(Image.open(path).convert('RGB'), dtype=np.float32)
        self.dur, self.focus, self.zoom_in, self.cap, self.cap_time = dur, focus, zoom_in, cap, cap_time

    def frame(self, t):
        k = t / self.dur
        s = 1.0 + 0.10 * (k if self.zoom_in else 1 - k)
        f = bi.zoom_frame(self.img, s, *self.focus)
        if self.cap is not None:
            a, b = self.cap_time
            f = self.cap.apply(f, min(bi.ramp(t, a, a + 0.6), 1 - bi.ramp(t, b, b + 0.6)))
        return f * VIG


class CardItem:
    """스테이지 카드(배경 + 큰 글자)."""
    def __init__(self, name, dur):
        self.img = np.asarray(Image.open(os.path.join(CARDS, name + '.png')).convert('RGB'), dtype=np.float32)
        self.dur = dur

    def frame(self, t):
        return bi.zoom_frame(self.img, 1.0 + 0.06 * t / self.dur, 0.5, 0.5)


class IntroItem:
    """오프닝 일러스트 연출(인트로)."""
    dur = 28.0

    def frame(self, t):
        d = self.dur
        frame = bi.bg.copy()
        frame = bi.hero.apply(frame, bi.ramp(t, 11.0, 14.0))
        frame = bi.cap1.apply(frame, min(bi.ramp(t, 3.0, 4.2), 1 - bi.ramp(t, 8.0, 9.2)))
        frame = bi.cap2.apply(frame, min(bi.ramp(t, 12.0, 13.2), 1 - bi.ramp(t, 18.0, 19.2)))
        frame = bi.title.apply(frame, bi.ramp(t, 22.5, 25.0))
        return bi.zoom_frame(frame, 1.0 + 0.16 * (t / d), 0.55, 0.40)


genre = bi.Layer(bi.text_layer([('2D 액션 플랫포머', 40, 1010, False, (225, 225, 225, 255))]))


class EndItem:
    """엔딩: 오프닝 일러스트 전체 + 로고 + COMING SOON."""
    def __init__(self, dur):
        self.dur = dur
        base = bi.hero.apply(bi.bg.copy(), 1.0)
        self.base = bi.title.apply(base, 1.0)

    def frame(self, t):
        frame = self.base.copy()
        frame = bi.soon.apply(frame, bi.ramp(t, 3.0, 4.5))
        frame = genre.apply(frame, bi.ramp(t, 5.0, 6.5))
        return bi.zoom_frame(frame, 1.12 - 0.12 * (t / self.dur), 0.5, 0.45)


def build_items():
    S = lambda n: os.path.join(SEL, n)
    D = 8.4
    boss = lambda name, col=RED: caption('FINAL BOSS' if 'overlord' in name else 'BOSS', RED, {'scorpion': '화산 전갈', 'horror': '눈먼 동굴 괴수', 'overlord': '쌍검 악마군주'}[name.split('_')[1]])
    guard_cap = caption('PERFECT BLOCK', GOLD, '완벽 방어')
    bf = (0.42, 0.60)   # 보스전 초점(주인공과 보스 사이)
    items = [IntroItem(),
             CardItem('stage1', 2.6), StillItem(S('01_stage1_slash.png'), D), StillItem(S('02_stage1_jump.png'), D, zoom_in=False),
             CardItem('stage2', 2.6), StillItem(S('03_stage2_slash_fire.png'), D), StillItem(S('04_stage2_jump.png'), D, zoom_in=False),
             StillItem(S('05_guard_perfect_a.png'), D, cap=guard_cap), StillItem(S('06_guard_perfect_b.png'), D, zoom_in=False),
             CardItem('stage3', 2.6), StillItem(S('07_stage3_jump_trap.png'), D), StillItem(S('08_stage3_jump_lava.png'), D, zoom_in=False),
             StillItem(S('09_boss3_scorpion.png'), D, focus=bf, cap=boss('boss3_scorpion')),
             CardItem('stage4', 2.6), StillItem(S('10_stage4_slash.png'), D),
             StillItem(S('11_boss4_horror_a.png'), D, focus=bf, cap=boss('boss4_horror_a'), zoom_in=False),
             StillItem(S('12_boss4_horror_b.png'), D, focus=bf),
             CardItem('stage5', 2.6), StillItem(S('13_stage5_slash.png'), D), StillItem(S('14_stage5_jump.png'), D, zoom_in=False),
             StillItem(S('15_boss5_overlord_a.png'), D, focus=bf, cap=boss('boss5_overlord_a')),
             StillItem(S('16_boss5_overlord_b.png'), D, focus=bf, zoom_in=False),
             ]
    used = sum(i.dur for i in items) - XF * len(items)          # 마지막 항목(엔딩) 앞까지의 길이
    end_dur = 180.0 - used
    items.append(EndItem(end_dur))
    return items


def render(path):
    items = build_items()
    starts, t0 = [], 0.0
    for it in items:
        starts.append(t0)
        t0 += it.dur - XF
    total = starts[-1] + items[-1].dur
    print('items', len(items), 'total %.2fs' % total, 'end dur %.2f' % items[-1].dur)
    proc = bi.open_writer(path)
    n = int(round(total * FPS))
    for i in range(n):
        t = i / FPS
        acc = None
        for it, s in zip(items, starts):
            local = t - s
            if local < 0 or local > it.dur:
                continue
            a = bi.ramp(local, 0.0, XF) * (1 - bi.ramp(local, it.dur - XF, it.dur))
            if it is items[0]:
                a = 1 - bi.ramp(local, it.dur - XF, it.dur)          # 인트로는 자체 페이드 인
            if it is items[-1]:
                a = bi.ramp(local, 0.0, XF) * (1 - bi.ramp(local, it.dur - 3.5, it.dur))   # 엔딩은 길게 페이드 아웃
            if a <= 0.001:
                continue
            fr = it.frame(local) * a
            acc = fr if acc is None else acc + fr
        if acc is None:
            acc = np.zeros((H, W, 3), np.float32)
        if t < 1.5:
            acc *= bi.ramp(t, 0.0, 1.5)
        bi.write(proc, acc)
        if i % 450 == 0:
            print('frame', i, '/', n, flush=True)
    proc.stdin.close()
    proc.wait()
    return total


def mux(silent, total):
    af = f'volume=-2dB,afade=t=in:st=0:d=1.5,afade=t=out:st={total - 5:.3f}:d=5'
    r = subprocess.run([FF, '-y', '-v', 'error', '-i', silent, '-i', AUDIO, '-filter_complex', f'[1:a]{af}[a]', '-map', '0:v', '-map', '[a]',
                        '-t', f'{total:.3f}', '-c:v', 'copy', '-c:a', 'aac', '-b:a', '192k', '-movflags', '+faststart', OUT], capture_output=True, text=True)
    if r.returncode != 0:
        print(r.stderr[-1200:])
        sys.exit(1)


if __name__ == '__main__':
    silent = os.path.join(SCR, 'still_teaser_silent.mp4')
    total = render(silent)
    mux(silent, total)
    print('done', OUT, os.path.getsize(OUT))
