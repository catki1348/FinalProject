# -*- coding: utf-8 -*-
# AI work record: Claude Sonnet 5.5, 2026-10-16. Generates and validates the 5-5 boss arena design
# (MapDesign_Stage5-5_BossArena.json + MapPreview_Stage5-5_BossArena.svg). Design only; nothing is applied to Unity.
import json, math, os

HERE = os.path.dirname(os.path.abspath(__file__))

# ---- reference numbers -------------------------------------------------------------------------------------------
PLAYER_SPEED = 8.0        # world units / second (MapEdit 15)
PLAYER_JUMP = 1.39        # measured max jump height (MapDesign_Stage4-5_BossArena.md)
PLAYER_JUMP80 = 1.1       # MapEdit 14: 80% jump
PLAYER_HEIGHT = 1.29
BOSS_SPEED = 2.6          # BossDesign_Stage5 section 2
BOSS_HEIGHT = 3.6
BOSS_BODY_LEN = 2.6       # estimate: torso + wings folded, used only for margins
DETECT = 16.0
GAP = 1.0                 # pause between patterns
# attack timing from Stage5BossAttackDefs.cs: (holds per frame, impact frame, reach xmin..xmax, symmetric)
ATTACKS = {
    "N1": ([.11, .15, .08, .15], 2, 3.4, False, "mid", 14),
    "N2": ([.12, .16, .07, .17], 3, 4.0, False, "low", 12),
    "N3": ([.11, .16, .07, .17], 3, 4.4, False, "mid", 16),
    "N4": ([.10, .15, .07, .17], 3, 3.6, False, "mid", 40),   # chained: frame 0 skipped
    "N5": ([.11, .16, .07, .18], 3, 3.2, True,  "low", 40),   # chained: frame 0 skipped
}
COMBOS = {"P1": ["N1"], "P2": ["N2"], "P3": ["N3"], "P4": ["N1", "N4", "N5"], "P5": ["N2", "N4", "N5"], "P6": ["N3", "N4", "N5"]}
LUNGE = 0.6               # assumed forward step during an attack (sprite offsets are cosmetic); margin only


def timeline(combo):
    """Return [(attack, impactTime, reach, height)] and the total duration of a pattern, in seconds."""
    t, out = 0.0, []
    for i, name in enumerate(COMBOS[combo]):
        holds, impact, reach, sym, h, dmg = ATTACKS[name]
        skip = 1 if (i > 0 and name in ("N4", "N5")) else 0
        used = holds[skip:]
        out.append((name, t + sum(holds[skip:impact]), reach, h, dmg))
        t += sum(used)
    return out, t


# ---- arena -------------------------------------------------------------------------------------------------------
W = 40.0                  # floor width (design size 5.0 = 40 / 8)
FLOOR_TOP = 0.0
WALL_X = W / 2 + 0.25
WALL_H = 14.0
ZONE_W, ZONE_H, ORTHO = 44.0, 18.0, 5.5
ZONE_CY = 5.0             # zone centre above the floor surface (floor top is 4 above the zone bottom)
PLAYER_START = -16.0
BOSS_START = 14.0
EXIT_X = 16.0
FALL_Y = -10.0

arena = {
    "schema": "stage5_5_boss_arena_v1",
    "status": "design_only",
    "designSize": W / 8.0,
    "floor": {"x0": -W / 2, "x1": W / 2, "top": FLOOR_TOP, "thickness": 1.0, "tiled": True, "scale": 1},
    "walls": [{"x": -WALL_X, "height": WALL_H, "width": .5}, {"x": WALL_X, "height": WALL_H, "width": .5}],
    "camera": {"center": [0, ZONE_CY], "size": [ZONE_W, ZONE_H], "orthographicSize": ORTHO},
    "playerStart": [PLAYER_START, FLOOR_TOP],
    "bossStart": [BOSS_START, FLOOR_TOP],
    "exitMarker": {"x": EXIT_X, "y": FLOOR_TOP, "activeAfterBossDeath": True},
    "fallLineY": FALL_Y,
    "platforms": "none (flat floor): mid hits cover 0.6-2.4, so raised ledges would not avoid them",
}

# ---- validation ---------------------------------------------------------------------------------------------------
checks = []

def check(name, ok, detail):
    checks.append({"name": name, "ok": bool(ok), "detail": detail})

screen_w = ORTHO * 2 * 16 / 9
check("start gap exceeds detection", BOSS_START - PLAYER_START > DETECT,
      "gap %.1f > detection %.1f; the player must walk %.1f (%.2fs) before the boss reacts"
      % (BOSS_START - PLAYER_START, DETECT, BOSS_START - PLAYER_START - DETECT, (BOSS_START - PLAYER_START - DETECT) / PLAYER_SPEED))
check("boss start clear of the right wall", WALL_X - BOSS_START > BOSS_BODY_LEN, "%.1f from wall" % (WALL_X - BOSS_START))
check("player start clear of the left wall", PLAYER_START + WALL_X > 2, "%.1f from wall" % (PLAYER_START + WALL_X))
check("floor spans the camera width at 16:9", ZONE_W >= W, "zone %.0f vs floor %.0f; screen width %.1f at ortho %.1f" % (ZONE_W, W, screen_w, ORTHO))
check("boss and player fit on one screen during melee", screen_w > 2 * (4.4 + LUNGE + 1), "screen %.1f, melee span <= %.1f" % (screen_w, 2 * (4.4 + LUNGE + 1)))
check("zone bottom shows the fall line", ZONE_CY - ZONE_H / 2 > FALL_Y, "zone bottom %.1f above fall line %.1f" % (ZONE_CY - ZONE_H / 2, FALL_Y))
check("zone top clears the jump + boss", ZONE_CY + ZONE_H / 2 > BOSS_HEIGHT + 5, "zone top %.1f" % (ZONE_CY + ZONE_H / 2))
check("walls taller than jump + boss", WALL_H > PLAYER_JUMP + BOSS_HEIGHT, "%.1f > %.1f" % (WALL_H, PLAYER_JUMP + BOSS_HEIGHT))

# retreat requirement per pattern: stand at the boss-side reach limit, run away until beyond every later reach
retreat = {}
for combo in COMBOS:
    tl, total = timeline(combo)
    worst = max(r for (_, _, r, _, _) in tl) + LUNGE
    first = tl[0][1]
    retreat[combo] = {"duration": round(total, 3), "firstImpact": round(first, 3), "lastImpact": round(tl[-1][1], 3),
                      "clearDistance": round(worst + 1.0, 2), "secondsToClear": round((worst + 1.0) / PLAYER_SPEED, 3),
                      "maxDamage": sum(d for (_, _, _, _, d) in tl)}
need_room = max(v["clearDistance"] for v in retreat.values())
check("retreat room beyond the longest reach", W / 2 - 3 >= need_room + BOSS_BODY_LEN,
      "needed %.1f + body %.1f; half floor %.0f minus 3 wall margin" % (need_room, BOSS_BODY_LEN, W / 2))
# informational: the first hit cannot be outrun (0.26-0.35s), so avoiding it is about spacing; the chained hits can be retreated from
for v in retreat.values():
    v["retreatDuringChain"] = round((v["lastImpact"] - v["firstImpact"]) * PLAYER_SPEED, 2)
# low hits: a jump must clear the 0.6 band; jump apex time and airtime
airtime = 2 * math.sqrt(2 * PLAYER_JUMP / (9.81 * 3.0)) if PLAYER_JUMP > 0 else 0
check("a full jump clears the low band", PLAYER_JUMP > 0.6 and PLAYER_JUMP80 > 0.6,
      "jump %.2f / 80%% %.2f vs low band 0.6; airtime ~%.2fs" % (PLAYER_JUMP, PLAYER_JUMP80, airtime))
# boss traversal: how long the arena lets the boss chase before the player is cornered
cross = W / BOSS_SPEED
check("chase time across the floor is bounded", cross < 20, "boss crosses the floor in %.1fs (%.1fs for the player)" % (cross, W / PLAYER_SPEED))

result = {"arena": arena, "retreat": retreat, "checks": checks, "allPassed": all(c["ok"] for c in checks)}
with open(os.path.join(HERE, "MapDesign_Stage5-5_BossArena.json"), "w", encoding="utf-8") as f:
    json.dump(result, f, ensure_ascii=False, indent=2)

# ---- preview SVG --------------------------------------------------------------------------------------------------
S = 20.0                                   # px per world unit
OX, OY = 60 + (ZONE_W / 2) * S, 60 + (ZONE_CY + ZONE_H / 2) * S   # screen position of local (0, 0)
def X(x): return OX + x * S
def Y(y): return OY - y * S
width = int(ZONE_W * S + 120); height = int(ZONE_H * S + 200)
svg = []
svg.append('<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d" viewBox="0 0 %d %d" font-family="Arial, sans-serif" font-size="12">' % (width, height, width, height))
svg.append('<!-- AI work record: Claude Sonnet 5.5, 2026-10-16. 5-5 boss arena preview generated by Generate_Stage5-5_BossArena.py. -->')
svg.append('<rect width="100%" height="100%" fill="#1b1220"/>')
svg.append('<text x="%d" y="30" fill="#f2e6ff" font-size="18" font-weight="bold">5-5 Boss Arena - Twin-Blade Demon Overlord (floor %.0f, design size %.1f)</text>' % (60, W, W / 8))
# camera zone
svg.append('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="#2a1a3a" stroke="#8a6ad0" stroke-dasharray="6 4"/>' % (X(-ZONE_W / 2), Y(ZONE_CY + ZONE_H / 2), ZONE_W * S, ZONE_H * S))
svg.append('<text x="%.1f" y="%.1f" fill="#b79df0">camera zone %.0fx%.0f, ortho %.1f (screen %.1f wide)</text>' % (X(-ZONE_W / 2) + 6, Y(ZONE_CY + ZONE_H / 2) + 16, ZONE_W, ZONE_H, ORTHO, screen_w))
# screen frame centred on the arena middle
svg.append('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="none" stroke="#6cd6a0" stroke-dasharray="3 3"/>' % (X(-screen_w / 2), Y(ORTHO * 2 - 4), screen_w * S, ORTHO * 2 * S))
# walls and floor
for wx in (-WALL_X, WALL_X):
    svg.append('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="#4a3a5c"/>' % (X(wx) - .25 * S, Y(WALL_H), .5 * S, WALL_H * S))
svg.append('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="#6b4f87" stroke="#c9a8f0"/>' % (X(-W / 2), Y(0), W * S, 1.0 * S))
svg.append('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="#ff5d5d" stroke-dasharray="5 4"/>' % (X(-ZONE_W / 2), Y(FALL_Y), X(ZONE_W / 2), Y(FALL_Y)))
svg.append('<text x="%.1f" y="%.1f" fill="#ff8a8a">fall line y=%.0f</text>' % (X(-ZONE_W / 2) + 6, Y(FALL_Y) - 4, FALL_Y))
# boss reach bands (low / mid) around the boss start for scale
bx = BOSS_START
svg.append('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="#ffd24a" fill-opacity=".25"/>' % (X(bx - 4.4 - 1.3), Y(2.4), 4.4 * S, 1.8 * S))
svg.append('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="#4aa8ff" fill-opacity=".35"/>' % (X(bx - 4.0 - 1.3), Y(0.6), 4.0 * S, 0.6 * S))
svg.append('<text x="%.1f" y="%.1f" fill="#ffe58a">mid 0.6-2.4 (reach 3.4-4.4)</text>' % (X(bx - 4.4 - 1.3), Y(2.4) - 4))
svg.append('<text x="%.1f" y="%.1f" fill="#8cc8ff">low 0-0.6 (jump 1.39)</text>' % (X(bx - 4.0 - 1.3), Y(0.6) + 14))
# boss and player
svg.append('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="#b32323" stroke="#ffb0b0"/>' % (X(bx) - BOSS_BODY_LEN / 2 * S, Y(BOSS_HEIGHT), BOSS_BODY_LEN * S, BOSS_HEIGHT * S))
svg.append('<text x="%.1f" y="%.1f" fill="#fff">BOSS x=%.0f</text>' % (X(bx) - 26, Y(BOSS_HEIGHT) - 6, bx))
svg.append('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="#3a7bd5" stroke="#bcd8ff"/>' % (X(PLAYER_START) - .5 * S, Y(PLAYER_HEIGHT), 1.0 * S, PLAYER_HEIGHT * S))
svg.append('<text x="%.1f" y="%.1f" fill="#cfe4ff">START x=%.0f</text>' % (X(PLAYER_START) - 30, Y(PLAYER_HEIGHT) - 6, PLAYER_START))
# detection ring and exit
svg.append('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="#ffd24a" stroke-dasharray="4 3"/>' % (X(bx - DETECT), Y(0) , X(bx - DETECT), Y(6)))
svg.append('<text x="%.1f" y="%.1f" fill="#ffe58a">detect 16</text>' % (X(bx - DETECT) - 20, Y(6) - 6))
svg.append('<rect x="%.1f" y="%.1f" width="%.1f" height="%.1f" fill="none" stroke="#7dffb0" stroke-dasharray="3 3"/>' % (X(EXIT_X) - .4 * S, Y(3), .8 * S, 3 * S))
svg.append('<text x="%.1f" y="%.1f" fill="#9dffc8">EXIT (after boss death) x=%.0f</text>' % (X(EXIT_X) - 70, Y(3) - 6, EXIT_X))
# scale ruler
svg.append('<line x1="%.1f" y1="%.1f" x2="%.1f" y2="%.1f" stroke="#fff"/>' % (X(-W / 2), Y(FALL_Y) + 30, X(-W / 2 + 8), Y(FALL_Y) + 30))
svg.append('<text x="%.1f" y="%.1f" fill="#fff">8 units = player 1 second</text>' % (X(-W / 2), Y(FALL_Y) + 46))
# pattern table
ty = Y(FALL_Y) + 70
svg.append('<text x="60" y="%.1f" fill="#f2e6ff" font-weight="bold">pattern timing (s) - first impact / last impact / retreat needed</text>' % ty)
for i, (c, v) in enumerate(retreat.items()):
    svg.append('<text x="60" y="%.1f" fill="#d8c8ee">%s  total %.2f  first %.2f  last %.2f  clear %.1f units (%.2fs)  max damage %d</text>' % (ty + 18 + i * 16, c, v["duration"], v["firstImpact"], v["lastImpact"], v["clearDistance"], v["secondsToClear"], v["maxDamage"]))
svg.append('</svg>')
with open(os.path.join(HERE, "MapPreview_Stage5-5_BossArena.svg"), "w", encoding="utf-8") as f:
    f.write("\n".join(svg))

print("allPassed:", result["allPassed"])
for c in checks:
    print(("PASS " if c["ok"] else "FAIL ") + c["name"] + " -- " + c["detail"])
for k, v in retreat.items():
    print(k, v)
