# AI work record: Claude Sonnet 5.5; 2026-10-19. Teaser re-shoot assembly (gameplay re-recorded with the fixed TeaserBot).
"""티저 재촬영 조립: 새 플레이 프레임 -> 클립, 자막 오버레이, 스테이지 사이 슬라이드 전환, 바로크 음악 합성.
인트로/엔딩은 기존 v1 영상에서 잘라 재사용한다(자막/로고 연출 동일)."""
import os, subprocess, sys, shutil
from pathlib import Path
import imageio_ffmpeg
from PIL import Image, ImageDraw, ImageFont, ImageFilter

HOME = Path(os.environ['USERPROFILE'])
LHS = HOME / 'Documents' / 'LHS'
CONCEPT = LHS / '컨셉'
WORK = LHS / 'FinalProject_teaser_work'
FRAMES = WORK / 'frames'
SEG = WORK / 'segs'
OV = WORK / 'overlays'
for d in (SEG, OV):
    d.mkdir(parents=True, exist_ok=True)
FF = imageio_ffmpeg.get_ffmpeg_exe()
BOLD = r'C:\Windows\Fonts\malgunbd.ttf'
SRC_V1 = CONCEPT / 'FinalDungeon_Teaser_v1.mp4'
AUDIO = CONCEPT / 'Epic_baroque_journey_2.mp3'
OUT = CONCEPT / 'FinalDungeon_Teaser_v1_Reshoot.mp4'
W, H = 1920, 1080
XF = 0.6                     # 클립 사이 전환 시간(초)
GOLD = (255, 214, 120, 255)
WHITE = (250, 250, 250, 255)
RED = (255, 96, 84, 255)

# (이름, 프레임 폴더, 사용 길이(초), 자막 작은 줄, 작은 줄 색, 큰 줄, 큰 줄 색)
CLIPS = [
    ('s1', 's1_run', 16, 'STAGE 1', GOLD, '숲을 지나', WHITE),
    ('s2', 's2_run', 16, 'STAGE 2', GOLD, '고대 유적을 넘어', WHITE),
    ('s3', 's3_run', 16, 'STAGE 3', GOLD, '용암 지대를 건너', WHITE),
    ('b3', 'b3', 12, 'BOSS', RED, '화산 전갈', WHITE),
    ('s4', 's4_area3', 16, 'STAGE 4', GOLD, '악마의 동굴을 뚫고', WHITE),
    ('b4', 'b4', 12, 'BOSS', RED, '눈먼 동굴 괴수', WHITE),
    ('s5', 's5_run', 16, 'STAGE 5', RED, '마왕성으로', WHITE),
    ('b5', 'b5', 12, 'FINAL BOSS', RED, '쌍검 악마군주', WHITE),
]


def run(args):
    r = subprocess.run([FF, '-y', '-v', 'error'] + [str(a) for a in args], capture_output=True, text=True)
    if r.returncode != 0:
        print(r.stderr[-2000:])
        sys.exit(1)


def probe_duration(path):
    r = subprocess.run([FF, '-hide_banner', '-i', str(path)], capture_output=True, text=True)
    for line in r.stderr.splitlines():
        if 'Duration:' in line:
            h, m, s = line.split('Duration:')[1].split(',')[0].strip().split(':')
            return int(h) * 3600 + int(m) * 60 + float(s)
    raise RuntimeError(path)


def overlay(name, small, small_color, big, big_color):
    """왼쪽 아래 자막(작은 줄 + 큰 줄) 투명 PNG."""
    base = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    sh = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    f1 = ImageFont.truetype(BOLD, 40)
    f2 = ImageFont.truetype(BOLD, 74)
    x, y1, y2 = 96, 880, 960
    for draw, dx in ((ImageDraw.Draw(sh), 3), (ImageDraw.Draw(base), 0)):
        c1 = (0, 0, 0, 230) if dx else small_color
        c2 = (0, 0, 0, 230) if dx else big_color
        draw.text((x + dx, y1 + dx), small, font=f1, fill=c1, anchor='ls')
        draw.text((x + dx, y2 + dx), big, font=f2, fill=c2, anchor='ls')
    sh = sh.filter(ImageFilter.GaussianBlur(5))
    out = Image.alpha_composite(sh, base)
    p = OV / (name + '.png')
    out.save(p)
    return p


def frames_to_clip(folder, out, seconds):
    src = FRAMES / folder
    n = len(list(src.glob('frame_*.jpg')))
    if n < seconds * 30:
        print(f'WARN {folder}: {n} frames < {seconds * 30}')
    run(['-framerate', 30, '-i', src / 'frame_%05d.jpg', '-t', seconds, '-c:v', 'libx264', '-crf', 17,
         '-preset', 'medium', '-pix_fmt', 'yuv420p', '-r', 30, out])


def build_segment(name, folder, seconds, small, sc, big, bc):
    raw = SEG / (name + '_raw.mp4')
    frames_to_clip(folder, raw, seconds)
    ov = overlay(name, small, sc, big, bc)
    out = SEG / (name + '.mp4')
    # 자막은 1초 뒤에 나타나 3.5초 동안 보이고 사라진다. 클립 앞뒤 페이드는 전환(xfade)이 맡는다.
    fc = ('[1:v]format=rgba,fade=t=in:st=1.0:d=0.7:alpha=1,fade=t=out:st=4.5:d=0.7:alpha=1[ov];'
          '[0:v][ov]overlay=0:0:shortest=1,format=yuv420p[v]')
    run(['-i', raw, '-loop', 1, '-i', ov, '-filter_complex', fc, '-map', '[v]', '-t', seconds, '-r', 30,
         '-c:v', 'libx264', '-crf', 17, '-preset', 'medium', out])
    return out


def cut_from_v1(name, start, dur):
    out = SEG / (name + '.mp4')
    run(['-ss', start, '-i', SRC_V1, '-t', dur, '-an', '-r', 30, '-c:v', 'libx264', '-crf', 17,
         '-preset', 'medium', '-pix_fmt', 'yuv420p', out])
    return out


def main():
    v1_total = probe_duration(SRC_V1)
    intro = cut_from_v1('intro', 0, 30)
    end = cut_from_v1('end', v1_total - 38, 38)
    segs = [(intro, 'fade')]
    for spec in CLIPS:
        segs.append((build_segment(*spec), 'slideleft' if spec[0].startswith('s') else 'fade'))
    segs.append((end, 'fade'))
    # xfade 체인: 앞 클립과 뒤 클립을 XF초 겹쳐 전환한다(스테이지 사이는 오른쪽으로 밀리는 슬라이드).
    inputs = []
    for p, _ in segs:
        inputs += ['-i', p]
    durs = [probe_duration(p) for p, _ in segs]
    parts = []
    cum = durs[0]
    last = '[0:v]'
    for i in range(1, len(segs)):
        trans = segs[i][1] if segs[i][1] != 'fade' else 'fadeblack' if i == len(segs) - 1 else 'fade'
        # 전환 종류는 '들어오는' 클립 기준: 들어오는 클립이 스테이지 달리기면 슬라이드
        off = cum - XF
        parts.append(f'{last}[{i}:v]xfade=transition={trans}:duration={XF}:offset={off:.3f}[x{i}]')
        last = f'[x{i}]'
        cum = off + durs[i]
    fc = ';'.join(parts)
    silent = SEG / 'teaser_silent.mp4'
    run(inputs + ['-filter_complex', fc, '-map', last, '-c:v', 'libx264', '-crf', 19, '-preset', 'slow', '-pix_fmt', 'yuv420p', '-r', 30, silent])
    total = probe_duration(silent)
    print('video total %.2fs' % total)
    af = f'volume=-2dB,afade=t=in:st=0:d=1.5,afade=t=out:st={total - 5:.3f}:d=5'
    run(['-i', silent, '-i', AUDIO, '-filter_complex', f'[1:a]{af}[a]', '-map', '0:v', '-map', '[a]', '-t', f'{total:.3f}',
         '-c:v', 'copy', '-c:a', 'aac', '-b:a', '192k', '-movflags', '+faststart', OUT])
    print('final', OUT, OUT.stat().st_size)


if __name__ == '__main__':
    main()
