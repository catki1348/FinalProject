# Blender headless: opening-screen preview v2 (see @Edit/OpeningDesign.md). Preview image only - no Unity.
# Composition: player stands on a pointed cliff spire at the BOTTOM-LEFT, seen from behind in 3/4 view, gazing up-right.
# Stage5 theme image sits at the UPPER-RIGHT (diagonal from the player); Stage1~4 are spread out in order as a rising
# staircase leading from the player's side toward Stage5.
# usage: blender -b -P compose_opening_v2.py -- <glb> <backgrounds_dir> <outpng>
import bpy, sys, os
import numpy as np
from mathutils import Vector

glb, bgdir, outpng = sys.argv[sys.argv.index("--")+1:][:3]
W, H = 1920, 1080

def load_rgba(path, w, h):
    """Load an image file resampled to (w, h) -> float array [h, w, 4] (rows bottom-up like Blender)."""
    im = bpy.data.images.load(path); im.scale(w, h)
    return np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)

def paste(canvas, img, x, y):
    """Alpha-composite img onto canvas, bottom-left at (x, y) with y measured from the bottom."""
    h, w = img.shape[:2]
    x0, y0 = max(x, 0), max(y, 0); x1, y1 = min(x+w, W), min(y+h, H)
    if x1 <= x0 or y1 <= y0: return
    src = img[y0-y:y1-y, x0-x:x1-x]; dst = canvas[y0:y1, x0:x1]
    a = src[..., 3:4]
    dst[..., :3] = src[..., :3]*a + dst[..., :3]*(1-a)

def frame(canvas, x, y, w, h, color, t=4):
    """Draw a t-pixel border around a panel (bottom-left x,y)."""
    c = np.array(color, dtype=np.float32)
    canvas[y-t:y, x-t:x+w+t, :3] = c; canvas[y+h:y+h+t, x-t:x+w+t, :3] = c
    canvas[y:y+h, x-t:x, :3] = c;     canvas[y:y+h, x+w:x+w+t, :3] = c

# --- 1. render the player: behind-left camera so the knight appears from behind, turned to face image-right ---
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
sc = bpy.context.scene
ms = [o for o in sc.objects if o.type == 'MESH']
pts = [o.matrix_world @ Vector(c) for o in ms for c in o.bound_box]
mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
ctr, size = (mn+mx)/2, max(mx-mn)
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
sc.render.film_transparent = True
sc.render.resolution_x = sc.render.resolution_y = 1400
sc.view_settings.view_transform = 'Standard'
cd = bpy.data.cameras.new("c"); cd.type = 'ORTHO'; cd.ortho_scale = size*1.05
cam = bpy.data.objects.new("c", cd); sc.collection.objects.link(cam); sc.camera = cam
d = Vector((-1, 1, -0.12)).normalized()               # model back = +Y, model front = -Y; this view turns the front to image-right, slight low angle
cam.location = ctr + d*size*3
cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
tmp = os.path.join(os.path.dirname(outpng), "_player_tmp.png")
sc.render.filepath = tmp; bpy.ops.render.render(write_still=True)
pl = load_rgba(tmp, 1400, 1400); os.remove(tmp)
ys, xs = np.where(pl[..., 3] > 0.05); pl = pl[ys.min():ys.max()+1, xs.min():xs.max()+1]

# --- 2. canvas: dark sky tinted red toward the upper-right (the demon castle) ---
yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
glow = np.clip(1.0 - np.hypot((xx-1540)/1100, (yy-(H-260))/800), 0, 1)**1.6     # radial glow around Stage5
cv = np.zeros((H, W, 4), dtype=np.float32); cv[..., 3] = 1
cv[..., 0] = 0.04 + 0.30*glow; cv[..., 1] = 0.04 + 0.04*glow; cv[..., 2] = 0.07 + 0.04*glow + 0.05*(yy/H)

# Stage5: upper-right, diagonal from the player
s5w = 640; s5h = int(s5w*768/1344)
s5x, s5y = 1200, H-60-s5h
paste(cv, load_rgba(os.path.join(bgdir, "Stage5_Background.png"), s5w, s5h), s5x, s5y)
frame(cv, s5x, s5y, s5w, s5h, (0.75, 0.12, 0.10), 5)
# Stage1..4: rising staircase toward Stage5, left -> right (stage order)
pw = 360; ph = int(pw*768/1344)
for i in range(4):
    px, py = 545 + i*325, 150 + i*95                  # py from bottom: each step 95px higher
    paste(cv, load_rgba(os.path.join(bgdir, f"Stage{i+1}_Background.png"), pw, ph), px, py)
    frame(cv, px, py, pw, ph, (0.55, 0.5, 0.42), 3)

# --- 3. pointed cliff spire at the bottom-left (scanline fill of left/right edge profiles, y from bottom) ---
rng = np.random.default_rng(7)
ctrl_y = [-10, 120, 220, 300, 360, 400, 425]
left_x = [-40,  80, 160, 215, 250, 272, 288]
right_x = [500, 430, 370, 340, 322, 314, 310]
for y in range(0, 426):
    xl = int(np.interp(y, ctrl_y, left_x) + rng.integers(-3, 4)); xr = int(np.interp(y, ctrl_y, right_x) + rng.integers(-3, 4))
    xl = max(xl, 0); t = (y/425.0)
    base = np.array((0.10, 0.08, 0.08))*(0.7+0.3*t)
    cv[y, xl:xr, :3] = base
    cv[y, max(xr-7, xl):xr, :3] = (0.26, 0.17, 0.15)  # rim light on the right side (faces the red glow)
    cv[y, xl:xl+4, :3] = (0.04, 0.03, 0.03)

# --- 4. place the player on the spire tip, facing up-right ---
sh = 540; sw = int(pl.shape[1]*sh/pl.shape[0])
im = bpy.data.images.new("p", pl.shape[1], pl.shape[0], alpha=True); im.pixels = pl.ravel().tolist(); im.scale(sw, sh)
pl2 = np.array(im.pixels[:], dtype=np.float32).reshape(sh, sw, 4)
paste(cv, pl2, 300 - sw//2, 405)
out = bpy.data.images.new("o", W, H, alpha=True); out.pixels = cv.ravel().tolist()
out.filepath_raw = outpng; out.file_format = 'PNG'; out.save(); print("SAVED", outpng)
