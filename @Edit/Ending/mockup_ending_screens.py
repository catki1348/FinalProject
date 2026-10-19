# Blender headless: PREVIEW mockups of the game-clear screen and ending scene pages (layout copied from the Unity scripts; 1920x1080).
# usage: blender -b -P mockup_ending_screens.py -- <Resources/Ending dir> <out dir>
import bpy, sys, os
import numpy as np
rd, od = sys.argv[sys.argv.index("--")+1:][:2]
W, H = 1920, 1080
FONT = "C:/Windows/Fonts/malgunbd.ttf"
def load(n):
    im = bpy.data.images.load(os.path.join(rd, n)); w, h = im.size
    return np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
def resize(a, w, h):
    im = bpy.data.images.new("r", a.shape[1], a.shape[0], alpha=True); im.pixels = a.ravel().tolist(); im.scale(w, h)
    return np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
def over(B, P, cx, cy, scale=1.0):          # straight-alpha over, centre at (cx, cy) in top-down px
    h, w = P.shape[:2]; by = int(H - cy - h/2); x = int(cx - w/2)
    y0, y1, x0, x1 = max(by, 0), min(by+h, H), max(x, 0), min(x+w, W)
    sp = P[y0-by:y1-by, x0-x:x1-x]; d = B[y0:y1, x0:x1]; a = sp[..., 3:4]*scale
    d[..., :3] = sp[..., :3]*a + d[..., :3]*(1-a)
def text_img(txt, size_px, color):
    bpy.ops.wm.read_factory_settings(use_empty=True); sc = bpy.context.scene
    cu = bpy.data.curves.new("t", 'FONT'); cu.body = txt; cu.font = bpy.data.fonts.load(FONT); cu.align_x = 'CENTER'; cu.size = 1.0
    ob = bpy.data.objects.new("t", cu); sc.collection.objects.link(ob); ob.color = (1, 1, 1, 1); bpy.context.view_layer.update()
    xs = [v[0] for v in ob.bound_box]; ys = [v[1] for v in ob.bound_box]; w, h = max(xs)-min(xs), max(ys)-min(ys)
    ob.location = (-(min(xs)+max(xs))/2, -(min(ys)+max(ys))/2, 0)
    cd = bpy.data.cameras.new("c"); cd.type = 'ORTHO'; cd.ortho_scale = w*1.1; cam = bpy.data.objects.new("c", cd); sc.collection.objects.link(cam); cam.location = (0, 0, 5); sc.camera = cam
    sc.render.resolution_x = 2400; sc.render.resolution_y = max(int(2400*h*1.7/(w*1.1)), 64); sc.render.engine = 'BLENDER_WORKBENCH'; sc.render.film_transparent = True
    sc.display.shading.light = 'FLAT'; sc.display.shading.color_type = 'OBJECT'; sc.view_settings.view_transform = 'Standard'
    tmp = os.path.join(od, "_t.png"); sc.render.filepath = tmp; bpy.ops.render.render(write_still=True)
    im = bpy.data.images.load(tmp); a = np.array(im.pixels[:], dtype=np.float32).reshape(im.size[1], im.size[0], 4); os.remove(tmp)
    ys_, xs_ = np.where(a[..., 3] > .05); a = a[ys_.min():ys_.max()+1, xs_.min():xs_.max()+1]
    k = size_px*0.72/ a.shape[0]                                    # glyph body ≈ 72% of font size (approx. UI Text look)
    a = resize(a, max(int(a.shape[1]*k), 2), max(int(a.shape[0]*k), 2)); a[..., :3] = np.array(color, np.float32)
    return a
def outlined(a):                                                    # black outline like UI Outline(3px)
    h, w = a.shape[:2]; m = np.zeros((h+12, w+12), np.float32); m[6:h+6, 6:w+6] = a[..., 3]; d = m.copy()
    for dy in range(-3, 4):
        for dx in range(-3, 4): d = np.maximum(d, np.roll(np.roll(m, dy, 0), dx, 1))
    out = np.zeros((h+12, w+12, 4), np.float32); out[..., 3] = d*0.9; out[6:h+6, 6:w+6] = np.where(a[..., 3:4] > 0.5, a, out[6:h+6, 6:w+6])
    out[6:h+6, 6:w+6, 3] = np.maximum(out[6:h+6, 6:w+6, 3], a[..., 3]); return out
def save(B, name):
    B[..., 3] = 1; o = bpy.data.images.new(name, W, H, alpha=True); o.pixels = B.ravel().tolist()
    o.filepath_raw = os.path.join(od, name + ".png"); o.file_format = 'PNG'; o.save(); print("SAVED", name)

bg = load("Ending_Background.png"); kn = load("Ending_Knight.png"); logo = load("Ending_Logo.png")
base = bg.copy(); base[..., 3] = 1; a = kn[..., 3:4]; base[..., :3] = kn[..., :3]*a + base[..., :3]*(1-a)
# 1) game clear (placeholder background = ending art; in game it is the frozen 5-5 arena)
B = base.copy(); B[..., :3] *= (1 - 0.80)
t = load("GameClear_Title.png"); t = resize(t, 1300, int(t.shape[0]*1300/t.shape[1])); over(B, t, 960, 540 - 90)
over(B, outlined(text_img("마왕을 쓰러뜨리고 던전을 정복했다!", 54, (1, .93, .7))), 960, 540 + 110)
over(B, outlined(text_img("PRESS ENTER", 46, (1, 1, 1))), 960, 540 + 330, 0.85)
save(B, "Mock_GameClear")
# 2) ending story page
B = base.copy()
band = np.zeros((250, W), np.float32); B[0:250, :, :3] *= (1 - 0.55)
over(B, outlined(text_img("숲을 지나, 이끼 낀 동굴을 지나, 용암과 어둠의 땅을 넘어 — 기사의 긴 여정은 이곳에서 끝났다.", 50, (1, .95, .82))), 960, H - 125)
save(B, "Mock_Ending_Story")
# 3) ending final
B = base.copy(); B[..., :3] *= (1 - 0.55)
lg = resize(logo, 1030, int(logo.shape[0]*1030/logo.shape[1])); over(B, lg, 960, 540 - 250)
te = load("TheEnd_Title.png"); te = resize(te, 760, int(te.shape[0]*760/te.shape[1])); over(B, te, 960, 540)
over(B, outlined(text_img("플레이해 주셔서 감사합니다", 52, (1, .93, .7))), 960, 540 + 150)
over(B, outlined(text_img("PRESS ENTER", 44, (1, 1, 1))), 960, 540 + 330, 0.85)
save(B, "Mock_Ending_Final")
