"""티저 최종 조립: 챕터/보스 자막 오버레이, 보스 슬로모션, 페이드, 이어붙이기, 바로크 음악 합성."""
import os, subprocess, sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter

SCR = os.path.dirname(os.path.abspath(__file__))
CLIPS = os.path.join(SCR, 'clips')
OV = os.path.join(SCR, 'overlays')
SEG = os.path.join(SCR, 'segs')
os.makedirs(OV, exist_ok=True)
os.makedirs(SEG, exist_ok=True)
FF = r'C:\Users\catkl\AppData\Local\Programs\Python\Python311\Lib\site-packages\imageio_ffmpeg\binaries\ffmpeg-win-x86_64-v7.1.exe'
BOLD = r'C:\Windows\Fonts\malgunbd.ttf'
AUDIO = r'C:\Users\catkl\OneDrive\문서\Unity\@Edit\teaser\audio_raw\baroque_2.mp3'
OUT_DIR = r'C:\Users\catkl\OneDrive\문서\LHS\컨셉'
W, H = 1920, 1080

GOLD = (255, 214, 120, 255)
WHITE = (250, 250, 250, 255)
RED = (255, 96, 84, 255)


def overlay(name, small, small_color, big, big_color=WHITE):
    """왼쪽 아래 자막(작은 줄 + 큰 줄) 투명 PNG."""
    base = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    sh = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    f1 = ImageFont.truetype(BOLD, 40)
    f2 = ImageFont.truetype(BOLD, 74)
    x, y1, y2 = 96, 880, 960
    for layer_draw, dx in ((ImageDraw.Draw(sh), 3), (ImageDraw.Draw(base), 0)):
        c1 = (0, 0, 0, 230) if dx else small_color
        c2 = (0, 0, 0, 230) if dx else big_color
        layer_draw.text((x + dx, y1 + dx), small, font=f1, fill=c1, anchor='ls')
        layer_draw.text((x + dx, y2 + dx), big, font=f2, fill=c2, anchor='ls')
    sh = sh.filter(ImageFilter.GaussianBlur(5))
    out = Image.alpha_composite(sh, base)
    p = os.path.join(OV, name + '.png')
    out.save(p)
    return p


# (출력 이름, 입력 클립, 슬로모션 배율(1=원속), 오버레이 파일)
SEGMENTS = [
    ('s1', 's1_run.mp4', 1.0, overlay('s1', 'STAGE 1', GOLD, '숲을 지나')),
    ('s2', 's2_run.mp4', 1.0, overlay('s2', 'STAGE 2', GOLD, '고대 유적을 넘어')),
    ('s3', 's3_run.mp4', 1.0, overlay('s3', 'STAGE 3', GOLD, '용암 지대를 건너')),
    ('s4', 's4_run.mp4', 1.0, overlay('s4', 'STAGE 4', GOLD, '악마의 동굴을 뚫고')),
    ('s5', 's5_run.mp4', 1.0, overlay('s5', 'STAGE 5', RED, '마왕성으로')),
    ('b3', 'b3.mp4', 1.6, overlay('b3', 'BOSS', RED, '화산 전갈')),
    ('b4', 'b4.mp4', 1.4, overlay('b4', 'BOSS', RED, '눈먼 동굴 괴수')),
    ('b5', 'b5.mp4', 1.4, overlay('b5', 'FINAL BOSS', RED, '쌍검 악마군주')),
]


def run(args):
    r = subprocess.run([FF, '-y', '-v', 'error'] + args, capture_output=True, text=True)
    if r.returncode != 0:
        print(r.stderr[-1500:])
        sys.exit(1)


def probe_duration(path):
    r = subprocess.run([FF, '-hide_banner', '-i', path], capture_output=True, text=True)
    for line in r.stderr.splitlines():
        if 'Duration:' in line:
            h, m, s = line.split('Duration:')[1].split(',')[0].strip().split(':')
            return int(h) * 3600 + int(m) * 60 + float(s)
    raise RuntimeError(path)


TRIM = {'b3': 0.4, 'b4': 0.5, 'b5': 1.7}   # 앞에서 잘라낼 초(원속 기준)


def build_segment(name, clip, slow, ov_png):
    src = os.path.join(CLIPS, clip)
    ss = TRIM.get(name, 0.0)
    dur_in = probe_duration(src) - ss
    dur = dur_in * slow
    out = os.path.join(SEG, name + '.mp4')
    vf = []
    if slow != 1.0:
        vf.append(f'setpts={slow}*PTS,minterpolate=fps=30:mi_mode=blend')
    base = ','.join(vf) if vf else 'null'
    fc = (f'[0:v]{base}[v0];'
          f'[1:v]format=rgba,fade=t=in:st=0.5:d=0.7:alpha=1,fade=t=out:st=3.6:d=0.7:alpha=1[ov];'
          f'[v0][ov]overlay=0:0:shortest=1,fade=t=in:st=0:d=0.5,fade=t=out:st={dur - 0.5:.3f}:d=0.5,format=yuv420p[v]')
    run(['-ss', f'{ss}', '-i', src, '-loop', '1', '-i', ov_png, '-filter_complex', fc, '-map', '[v]', '-t', f'{dur:.3f}',
         '-r', '30', '-c:v', 'libx264', '-crf', '17', '-preset', 'medium', out])
    print(name, f'{dur_in:.2f}s -> {dur:.2f}s')
    return out


def main():
    segs = [os.path.join(CLIPS, 'intro.mp4')]
    for name, clip, slow, ov in SEGMENTS:
        segs.append(build_segment(name, clip, slow, ov))
    segs.append(os.path.join(CLIPS, 'end.mp4'))
    lst = os.path.join(SEG, 'list.txt')
    with open(lst, 'w', encoding='utf-8') as f:
        for s in segs:
            f.write("file '" + s.replace('\\', '/') + "'\n")
    silent = os.path.join(SEG, 'teaser_silent.mp4')
    run(['-f', 'concat', '-safe', '0', '-i', lst, '-c:v', 'libx264', '-crf', '21', '-preset', 'slow', '-pix_fmt', 'yuv420p', '-r', '30', silent])
    total = probe_duration(silent)
    print('video total %.2fs' % total)
    os.makedirs(OUT_DIR, exist_ok=True)
    final = os.path.join(OUT_DIR, 'FinalDungeon_Teaser_v1.mp4')
    # 음악: 0초부터 영상 길이만큼, 끝 5초 페이드 아웃, -2dB
    af = f'volume=-2dB,afade=t=in:st=0:d=1.5,afade=t=out:st={total - 5:.3f}:d=5'
    run(['-i', silent, '-i', AUDIO, '-filter_complex', f'[1:a]{af}[a]', '-map', '0:v', '-map', '[a]', '-t', f'{total:.3f}',
         '-c:v', 'copy', '-c:a', 'aac', '-b:a', '192k', '-movflags', '+faststart', final])
    print('final', final, os.path.getsize(final))


if __name__ == '__main__':
    main()
