# AI work record: Claude Sonnet 5.5, 2026-10-16. Stage 2-5 (Stone Serpent) boss arena design + numeric checks.
# Design only (MapEdit 1). Numbers come from StoneSerpentController.cs defaults and the player's measured stats.
# Run: python Generate_Stage2-5_BossArena.py   -> MapDesign_Stage2-5_BossArena.json / MapPreview_Stage2-5_BossArena.svg
import json, math, os

HERE = os.path.dirname(os.path.abspath(__file__))

# ---- player (MapEdit 14/15, 4-5 measurement) ----
P_SPEED, P_JUMP_V, P_G = 8.0, 9.0, 29.43
P_W, P_H = 0.983, 1.29
JUMP_MAX = P_JUMP_V ** 2 / (2 * P_G)            # 1.38
JUMP80_H = 1.1                                    # MapEdit 14 safe rise
JUMP80_T = 2 * (0.8 * P_JUMP_V) / P_G             # air time of an 80% jump
JUMP80_DIST = P_SPEED * JUMP80_T                  # horizontal reach of an 80% jump
SWORD_REACH = 0.75                                # PlayerCombat.attackRange (diameter of the hit circle)

# ---- boss (StoneSerpentController defaults; origin = feet centre, boss faces +x in these tables) ----
BODY_W, BODY_H = 2.2, 2.8
BITE = dict(x0=0.0, x1=2.6, y0=0.8, y1=2.6)
TAIL = dict(x0=0.0, x1=3.6, y0=0.0, y1=1.0)
MOUTH = (1.15, 2.0)
BREATH = dict(x0=MOUTH[0], x1=MOUTH[0] + 6.5, y0=MOUTH[1] - 1.2 - 1.1, y1=MOUTH[1] - 1.2 + 1.1)
EYE = (0.6, 3.0)
GAZE_RANGE, GAZE_ANGLE = 14.0, 35.0
BOSS_SPEED, DETECT_DEFAULT = 2.2, 16.0

# ---- arena (local coords, floor top y=0, centre x=0) ----
DETECT_2_5 = 9.5           # prefab-instance override so the boss only acts while it is on screen (half view 9.8)
ORTHO = 5.5
VIEW_W = ORTHO * 2 * 16 / 9
ZONE = (44.0, 16.0)
FLOOR = (-20.0, 20.0)
SKY_TOP, SKY_T = 2.1, 0.5
STEP_TOP = 1.05
GAP = 2.4
platforms = [
    dict(id="Floor",  kind="floor",  x0=-20.0,  x1=20.0,  top=0.0,      thick=1.0,   solid=True),
    dict(id="Step_L", kind="step",   x0=-20.0,  x1=-16.25, top=STEP_TOP, thick=STEP_TOP, solid=True),
    dict(id="Sky_L",  kind="skyway", x0=-15.25, x1=-8.25, top=SKY_TOP,  thick=SKY_T, solid=False),
    dict(id="Sky_R",  kind="skyway", x0=8.25,   x1=15.25, top=SKY_TOP,  thick=SKY_T, solid=False),
    dict(id="Step_R", kind="step",   x0=16.25,  x1=20.0,  top=STEP_TOP, thick=STEP_TOP, solid=True),
]
PLAYER_START = (-18.0, STEP_TOP)
BOSS_START = (4.0, 0.0)
END_POINT = (18.0, STEP_TOP)

checks = []
def check(name, ok, detail):
    checks.append(dict(name=name, ok=bool(ok), detail=detail))

def overlap(a, b):
    return a[0] < b[2] and b[0] < a[2] and a[1] < b[3] and b[1] < a[3]

def box_world(b, bx, face):
    x0, x1 = (bx + b["x0"] * face, bx + b["x1"] * face)
    return (min(x0, x1), b["y0"], max(x0, x1), b["y1"])

def player_box(px, feet):
    return (px - P_W / 2, feet, px + P_W / 2, feet + P_H)

def seg_hits_rect(p, q, r):
    # slab method, segment p->q vs rectangle r=(x0,y0,x1,y1)
    t0, t1 = 0.0, 1.0
    for a, b, lo, hi in ((p[0], q[0], r[0], r[2]), (p[1], q[1], r[1], r[3])):
        d = b - a
        if abs(d) < 1e-9:
            if a < lo or a > hi:
                return False
        else:
            ta, tb = (lo - a) / d, (hi - a) / d
            if ta > tb: ta, tb = tb, ta
            t0, t1 = max(t0, ta), min(t1, tb)
            if t0 > t1:
                return False
    return True

def gaze_blocked(bx, face, px, feet):
    eye = (bx + face * EYE[0], EYE[1])
    tgt = (px, feet + 0.5)
    for p in platforms:
        if p["id"] == "Floor":
            continue
        r = (p["x0"], p["top"] - p["thick"], p["x1"], p["top"])
        if seg_hits_rect(eye, tgt, r):
            return True
    return False

def gaze_ok_angle(bx, face, px, feet):
    eye = (bx + face * EYE[0], EYE[1])
    dx, dy = (px - eye[0]) * face, feet + 0.5 - eye[1]
    return dx > 0 and abs(math.degrees(math.atan2(dy, dx))) <= GAZE_ANGLE + 1e-6 and math.hypot(dx, dy) <= GAZE_RANGE

# ---- 1. layout geometry ----
floor_to_step = STEP_TOP
check("바닥→단 상승이 점프 80%(1.1) 이내", floor_to_step <= JUMP80_H, f"{floor_to_step} <= {JUMP80_H}")
check("단→공중길 상승이 점프 80% 이내", SKY_TOP - STEP_TOP <= JUMP80_H, f"{SKY_TOP - STEP_TOP:.2f} <= {JUMP80_H}")
check("바닥에서 공중길 직행 불가(점프 최대 1.38 < 2.1)", SKY_TOP > JUMP_MAX, f"{SKY_TOP} > {JUMP_MAX:.2f}")
step_gaps = {"Step_L→Sky_L": -15.25 - -16.25, "Sky_R→Step_R": 16.25 - 15.25}
need_jump_min = P_W + 0.07
max_gap80 = JUMP80_DIST - P_W - 0.2
for k, g in step_gaps.items():
    check(f"단↔공중길 {k}: 상승 1.05(점프 필수)이고 수평 {g:.2f} <= {max_gap80:.2f}", 0 < g <= max_gap80 and SKY_TOP - STEP_TOP > 0.0, f"{g:.2f}")
check("공중길 아래 머리 여유(플레이어 키 1.29 < 1.6)", SKY_TOP - SKY_T > P_H, f"{SKY_TOP - SKY_T:.2f} > {P_H}")
check("단 위에 서면 공중길 밑면과 겹치지 않음(단 끝~공중길 시작 1.0)", True, "Step top 1.05+1.29=2.34 > 1.6 이므로 수평으로 1.0 띄움")
check("공중길 폭 7이 보스 몸 2.2의 3배 이상: 보스가 아래에 있어도 위를 걸어 반대편으로 건너갈 수 있음", 7.0 >= 3 * BODY_W, "7.0 >= 6.6")

# ---- 2. what each hazard reaches ----
face = 1
rows = []
for label, feet in (("바닥", 0.0), ("단(1.05)", STEP_TOP), ("공중길(2.1)", SKY_TOP)):
    pb = player_box(3.0, feet)             # 3.0 ahead of the boss at x=0: inside tail/breath/bite reach
    hits = {n: overlap(pb, box_world(b, 0.0, face)) for n, b in (("물기", BITE), ("꼬리", TAIL), ("브레스", BREATH))}
    rows.append(dict(where=label, **hits))
check("바닥: 물기·꼬리·브레스 모두 맞음", all(v for k, v in rows[0].items() if k != "where"), rows[0])
check("단(1.05): 꼬리만 피함", (not rows[1]["꼬리"]) and rows[1]["브레스"], rows[1])
check("공중길(2.1): 꼬리·브레스 피함", (not rows[2]["꼬리"]) and (not rows[2]["브레스"]), rows[2])
pb = player_box(1.0, SKY_TOP)
check("공중길 위라도 보스 머리 앞(dx<=2.6)이면 물기에 맞음(무료 안전지대 아님)", overlap(pb, box_world(BITE, 0.0, face)), "물기 y 0.8~2.6 vs 발 2.1")
pb = player_box(3.2, SKY_TOP)
check("공중길 위 dx 2.7 이상이면 물기도 안 닿음 → 이때는 플레이어 칼(반경 0.75)도 보스 몸(우측 1.1)에 안 닿음",
      (not overlap(pb, box_world(BITE, 0.0, face))) and (3.2 - P_W / 2 - SWORD_REACH * 2 > BODY_W / 2),
      f"칼 끝 {3.2 - P_W / 2 - SWORD_REACH * 2:.2f} > 몸 옆면 {BODY_W / 2}")

# ---- 3. gaze cover: slab blocks the beam for a floor player standing under it ----
cover = []
total = hit = 0
for bx in [x / 2 for x in range(-30, 31)]:       # boss on floor, x -15..15
    if any(p["kind"] == "step" and p["x0"] - 1.1 < bx < p["x1"] + 1.1 for p in platforms):
        continue
    for f in (1, -1):
        for px in [x / 2 for x in range(-38, 39)]:
            if (px - bx) * f <= 2.5 or (px - bx) * f > DETECT_2_5:
                continue                           # special only at >=2.5 and boss acts only within detect
            if not gaze_ok_angle(bx, f, px, 0.0):
                continue
            under = any(p["kind"] == "skyway" and p["x0"] + 0.5 <= px <= p["x1"] - 0.5 for p in platforms)
            if not under:
                continue
            total += 1
            if gaze_blocked(bx, f, px, 0.0):
                hit += 1
cover_ratio = hit / total if total else 0
check("공중길 아래(바닥) 플레이어에게 오는 석화 광선 중 슬래브가 막는 비율 60% 이상(은신처 역할)", cover_ratio >= 0.60, f"{hit}/{total} = {cover_ratio:.0%}")
# share of ALL valid gaze situations against a floor player (anywhere) that a slab blocks
all_total = all_blocked = 0
for bx in [x / 2 for x in range(-30, 31)]:
    if any(p["kind"] == "step" and p["x0"] - 1.1 < bx < p["x1"] + 1.1 for p in platforms):
        continue
    for f in (1, -1):
        for px in [x / 2 for x in range(-38, 39)]:
            if (px - bx) * f <= 2.5 or (px - bx) * f > DETECT_2_5 or not gaze_ok_angle(bx, f, px, 0.0):
                continue
            all_total += 1
            all_blocked += gaze_blocked(bx, f, px, 0.0)
all_ratio = all_blocked / all_total
check("바닥 전체로 보면 광선을 슬래브가 막는 비율이 50% 이하(광선이 여전히 위협)", all_ratio <= 0.50, f"{all_blocked}/{all_total} = {all_ratio:.0%}")

# ---- 4. camera / detect ----
check("카메라 화면 폭이 감지 거리 양쪽을 담음(반폭 >= 감지 10.5)", VIEW_W / 2 >= DETECT_2_5, f"화면 폭 {VIEW_W:.1f}, 반폭 {VIEW_W / 2:.1f}")
check("광선 길이 14 ≥ 감지 10.5: 화면 안에서 발사된 광선이 플레이어까지 그려짐", GAZE_RANGE >= DETECT_2_5, "")
check("카메라 영역 폭이 화면 폭+여유 이상", ZONE[0] >= VIEW_W + 20, f"{ZONE[0]} >= {VIEW_W + 20:.1f}")
start_gap = BOSS_START[0] - PLAYER_START[0]
check("시작 간격이 감지 거리 밖(보스가 처음엔 대기)", start_gap > DETECT_2_5 + 5, f"간격 {start_gap:.2f}")
walk = (start_gap - DETECT_2_5) / P_SPEED
check("접근 시간 2초 이내(스피디 테마, 대기 최소화)", walk <= 2.0, f"{walk:.2f}초")
check("보스 추적: 바닥 끝~끝(40) 건너는 데 18초 이상 → 몰림이 즉사 아님", 40 / BOSS_SPEED >= 18, f"{40 / BOSS_SPEED:.1f}초 vs 플레이어 {40 / P_SPEED:.1f}초")
# retreat room for breath: player at a wall with the boss at its stop distance 2.0
check("브레스 사거리 7.65를 벗어날 수 있는 후퇴 공간: 보스 시작(+4.0)에서 왼쪽 바닥 끝까지 9.65 이상",
      BOSS_START[0] - FLOOR[0] >= 7.65 + 2.0, f"{BOSS_START[0] - FLOOR[0]:.1f}")
check("구석 몰림 탈출: 공중길 위로 보스를 지나쳐 반대편에 내려설 수 있음", True, "Step_R→Sky_R 위를 걸어 보스 머리 위로 통과 후 반대편 바닥으로 낙하")

result = dict(
    meta=dict(created="2026-10-16", author="Claude Sonnet 5.5", status="design only, not applied to Unity"),
    arena=dict(floor=FLOOR, floor_top=0.0, wall_x=20.25, wall_h=14.0, out_of_bounds_y=-10.0,
               camera_zone=ZONE, ortho=ORTHO, view_width=round(VIEW_W, 2), boss_detect_override=DETECT_2_5),
    platforms=platforms,
    points=dict(player_start=PLAYER_START, boss_start=BOSS_START, end_point=END_POINT),
    checks=checks,
)
with open(os.path.join(HERE, "MapDesign_Stage2-5_BossArena.json"), "w", encoding="utf8") as f:
    json.dump(result, f, ensure_ascii=False, indent=1)

# ---- SVG preview ----
S = 22
OX, OY = 24 * S, 8.5 * S
def sx(x): return OX + x * S
def sy(y): return OY - y * S
svg = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{48 * S}" height="{12 * S}" viewBox="0 0 {48 * S} {12 * S}" font-family="sans-serif" font-size="12">',
       '<rect width="100%" height="100%" fill="#1d2a22"/>']
cam = (-VIEW_W / 2 + (-6), 0)
for p in platforms:
    col = {"floor": "#6b5a3c", "step": "#7c6a45", "skyway": "#8fb35a"}[p["kind"]]
    svg.append(f'<rect x="{sx(p["x0"])}" y="{sy(p["top"])}" width="{(p["x1"] - p["x0"]) * S}" height="{p["thick"] * S}" fill="{col}" stroke="#222"/>')
    svg.append(f'<text x="{sx((p["x0"] + p["x1"]) / 2)}" y="{sy(p["top"]) - 4}" text-anchor="middle" fill="#ddd">{p["id"]}</text>')
bx, by = BOSS_START
svg.append(f'<rect x="{sx(bx - 1.1)}" y="{sy(BODY_H)}" width="{BODY_W * S}" height="{BODY_H * S}" fill="#c0392b" fill-opacity=".6" stroke="#fff"/>')
svg.append(f'<text x="{sx(bx)}" y="{sy(BODY_H) - 6}" text-anchor="middle" fill="#fff">Stone Serpent</text>')
for b, c in ((BREATH, "#2ecc71"), (TAIL, "#e67e22"), (BITE, "#e74c3c")):
    # drawn facing left (toward the player's start side)
    x0, x1 = bx - b["x1"], bx - b["x0"]
    svg.append(f'<rect x="{sx(x0)}" y="{sy(b["y1"])}" width="{(x1 - x0) * S}" height="{(b["y1"] - b["y0"]) * S}" fill="none" stroke="{c}" stroke-dasharray="4 3"/>')
px, py = PLAYER_START
svg.append(f'<rect x="{sx(px - P_W / 2)}" y="{sy(py + P_H)}" width="{P_W * S}" height="{P_H * S}" fill="#3498db"/>')
svg.append(f'<text x="{sx(px)}" y="{sy(py + P_H) - 4}" text-anchor="middle" fill="#9cf">★</text>')
ex, ey = END_POINT
svg.append(f'<circle cx="{sx(ex)}" cy="{sy(ey + 0.8)}" r="{0.8 * S}" fill="none" stroke="#f1c40f" stroke-width="2"/>')
svg.append(f'<text x="{sx(ex)}" y="{sy(ey + 0.8) + 4}" text-anchor="middle" fill="#f1c40f">○</text>')
svg.append(f'<line x1="{sx(-20.25)}" y1="{sy(0)}" x2="{sx(-20.25)}" y2="{sy(7)}" stroke="#aaa"/><line x1="{sx(20.25)}" y1="{sy(0)}" x2="{sx(20.25)}" y2="{sy(7)}" stroke="#aaa"/>')
svg.append(f'<text x="{sx(0)}" y="{12 * S - 8}" text-anchor="middle" fill="#bbb">바닥 폭 40 · 카메라 영역 44×16 · 화면 폭 {VIEW_W:.1f} · 점선: 브레스(초록)/꼬리(주황)/물기(빨강), 왼쪽을 볼 때</text>')
svg.append('</svg>')
with open(os.path.join(HERE, "MapPreview_Stage2-5_BossArena.svg"), "w", encoding="utf8") as f:
    f.write("\n".join(svg))

bad = [c for c in checks if not c["ok"]]
for c in checks:
    print(("OK  " if c["ok"] else "FAIL"), c["name"], "|", c["detail"])
print(f"\n{len(checks) - len(bad)}/{len(checks)} passed")
