"""스위치 눌림 프레임 후처리.

흰 배경 제거(가장자리와 이어진 영역만) → 흰 테두리 잔상 제거 → 공통 영역 자르기 → 정수 배 축소 → PNG 저장.
모든 프레임은 같은 캔버스·같은 카메라로 생성되어 있어야 하며, 같은 자르기 영역을 공유한다.

사용:
  python Process_SwitchFrames.py <출력 폴더> <기본.png> <충전.png> <발동.png> [--crop x0,y0,x1,y1] [--width 168]

자르기 영역을 주지 않으면 첫 프레임에서 바닥 슬랩 윗면(불투명 폭이 이미지 폭의 60% 이상이 되는 첫 행)을 찾아
그 위쪽, 세 프레임 불투명 영역의 합집합을 자른다(슬랩은 포함하지 않는다).
결과 폭이 --width(기본 168px)에 가까운 정수 배율로 축소하며, Unity에서는 PPU를 결과 폭으로 두면 폭 1유닛이 된다.
3-스테이지 용암 스위치는 --crop 520,180,855,352 로 만들었다(자동 검출과 같은 결과).
"""
import argparse
import os
from collections import deque

from PIL import Image, ImageChops, ImageFilter

WHITE_MIN = 232
FRINGE_MIN = 170   # 투명 영역에 닿은 이 밝기 이상의 픽셀은 배경 잔상(흰 테두리)으로 보고 제거
FRINGE_PASSES = 3
SLAB_FRACTION = 0.6
MASK_GROW = 7        # 기본 프레임 실루엣을 넓히는 필터 크기(홀수, 픽셀). 이 밖으로 번진 빛 번짐은 잘라낸다


# 가장자리와 이어진 흰색 영역만 투명 처리(버튼 안의 흰 하이라이트는 보존)
def remove_outer_white(img):
    img = img.convert("RGBA")
    w, h = img.size
    px = img.load()
    seen = [[False] * h for _ in range(w)]
    q = deque()
    for x in range(w):
        q.append((x, 0)); q.append((x, h - 1))
    for y in range(h):
        q.append((0, y)); q.append((w - 1, y))
    while q:
        x, y = q.popleft()
        if x < 0 or y < 0 or x >= w or y >= h or seen[x][y]:
            continue
        r, g, b, _ = px[x, y]
        if min(r, g, b) < WHITE_MIN:
            continue
        seen[x][y] = True
        px[x, y] = (r, g, b, 0)
        q.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
    return img


# 투명 영역과 맞닿은 밝은 픽셀(흰 테두리 잔상)을 몇 겹 깎아낸다. 검은 외곽선 안쪽은 건드리지 않는다
def peel_fringe(img, passes=None):
    w, h = img.size
    px = img.load()
    for _ in range(passes or FRINGE_PASSES):
        kill = []
        for y in range(h):
            for x in range(w):
                r, g, b, a = px[x, y]
                if a == 0 or min(r, g, b) < FRINGE_MIN:
                    continue
                for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if nx < 0 or ny < 0 or nx >= w or ny >= h or px[nx, ny][3] == 0:
                        kill.append((x, y))
                        break
        for x, y in kill:
            px[x, y] = (0, 0, 0, 0)
    return img


# 충전·발동 프레임은 기본 프레임과 같은 형태이므로, 기본 프레임 실루엣(살짝 확대) 밖으로 번진 빛 번짐 조각을 제거한다
def clip_to_base_silhouette(frames):
    base_alpha = frames[0].split()[3].point(lambda a: 255 if a > 0 else 0).filter(ImageFilter.MaxFilter(MASK_GROW))
    for f in frames[1:]:
        f.putalpha(ImageChops.multiply(f.split()[3], base_alpha))


# 불투명 폭이 이미지 폭의 SLAB_FRACTION 이상인 첫 행(바닥 슬랩 윗면)의 y
def find_slab_top(img):
    w, h = img.size
    px = img.load()
    for y in range(h):
        opaque = sum(1 for x in range(w) if px[x, y][3] > 0)
        if opaque >= w * SLAB_FRACTION:
            return y
    return h


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out_dir")
    ap.add_argument("frames", nargs=3)
    ap.add_argument("--crop", help="x0,y0,x1,y1 (원본 좌표). 생략 시 슬랩 윗면 기준 자동")
    ap.add_argument("--width", type=int, default=168)
    ap.add_argument("--peel", type=int, default=None, help="흰 테두리·빛 번짐 깎기 횟수(기본 3). 위쪽 빛 번짐이 크면 늘린다")
    args = ap.parse_args()

    cleaned = [peel_fringe(remove_outer_white(Image.open(p)), args.peel) for p in args.frames]
    clip_to_base_silhouette(cleaned)
    cleaned = [cleaned[0]] + [peel_fringe(c) for c in cleaned[1:]]  # 잘린 가장자리에 남은 밝은 테두리 재제거

    if args.crop:
        box = tuple(int(v) for v in args.crop.split(","))
    else:
        slab_y = find_slab_top(cleaned[0])
        boxes = [c.crop((0, 0, c.width, slab_y)).getbbox() for c in cleaned]
        box = (min(b[0] for b in boxes), min(b[1] for b in boxes), max(b[2] for b in boxes), slab_y)
    scale = max(1, round((box[2] - box[0]) / args.width))
    print("crop", box, "scale 1/%d" % scale)

    os.makedirs(args.out_dir, exist_ok=True)
    for i, img in enumerate(cleaned):
        out = img.crop(box)
        out = out.resize((out.width // scale, out.height // scale), Image.NEAREST)
        path = os.path.join(args.out_dir, f"switch_{i}.png")
        out.save(path)
        print(path, out.size)


if __name__ == "__main__":
    main()
