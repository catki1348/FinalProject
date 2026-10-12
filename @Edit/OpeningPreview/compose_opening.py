# Blender headless: composes the opening-screen preview (design in @Edit/OpeningDesign.md).
# Row 1 (bottom tier): Stage1..4 theme images left->right. Row 2 (top tier): Stage5 centred above. Player seen from behind
# on a cliff edge in the foreground. Pure preview image - no Unity involved.
# usage: blender -b -P compose_opening.py -- <glb> <backgrounds_dir> <outpng>
import bpy, sys, os
import numpy as np
from mathutils import Vector

glb, bgdir, outpng = sys.argv[sys.argv.index("--")+1:][:3]
W, H = 1920, 1080

def load_rgba(path, w, h):
    """Load an image file and resample it to (w, h); returns float array [h, w, 4] (rows bottom-up like Blender)."""
    im = bpy.data.images.load(path); im.scale(w, h)
    return np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)

def paste(canvas, img, x, y):
    """Alpha-composite img onto canvas with its bottom-left at (x, y) (y measured from the bottom)."""
    h, w = img.shape[:2]
    x0, y0 = max(x, 0), max(y, 0); x1, y1 = min(x+w, W), min(y+h, H)
    if x1 <= x0 or y1 <= y0: return
    src = img[y0-y:y1-y, x0-x:x1-x]; dst = canvas[y0:y1, x0:x1]
    a = src[..., 3:4]
    dst[..., :3] = src[..., :3]*a + dst[..., :3]*(1-a)

# --- render the player from behind with a transparent background ---
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
d = Vector((0, 1, 0))                                   # model back faces +Y (see Preview image)
cam.location = ctr + d*size*3
cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
tmp = os.path.join(os.path.dirname(outpng), "_player_back.png")
sc.render.filepath = tmp; bpy.ops.render.render(write_still=True)
pl = load_rgba(tmp, 1400, 1400); os.remove(tmp)
ys, xs = np.where(pl[..., 3] > 0.05)                    # crop to the character's silhouette
pl = pl[ys.min():ys.max()+1, xs.min():xs.max()+1]

# --- build the canvas ---
cv = np.zeros((H, W, 4), dtype=np.float32); cv[..., 3] = 1; cv[..., :3] = (0.05, 0.03, 0.04)
pw = W//4; ph = int(pw*768/1344)                        # lower tier panel size (keeps 1344x768 ratio)
low_y = 250                                             # bottom of the lower tier (y from bottom)
for i in range(4):                                      # Stage1..4, left -> right
    paste(cv, load_rgba(os.path.join(bgdir, f"Stage{i+1}_Background.png"), pw, ph), i*pw, low_y)
tw = W//2; th = int(tw*768/1344)                        # Stage5: centred above the four panels
paste(cv, load_rgba(os.path.join(bgdir, "Stage5_Background.png"), tw, th), (W-tw)//2, low_y+ph)
# thin dark separators between panels
for i in range(1, 4): cv[low_y:low_y+ph, i*pw-2:i*pw+2, :3] = 0.02
cv[low_y+ph-2:low_y+ph+2, :, :3] = 0.02
# cliff: dark rocky foreground below the lower tier with a jagged top edge (peak in the middle under the player)
rng = np.random.default_rng(3)
edge = low_y - 40 + (np.convolve(rng.normal(0, 14, W), np.ones(40)/40, 'same')*3).astype(int)
for x in range(W):
    col = cv[:max(edge[x], 0), x, :3]
    col[...] = np.array((0.09, 0.07, 0.07))*(0.6+0.4*np.linspace(0,1,col.shape[0]))[:,None]
    cv[max(edge[x]-3, 0):max(edge[x], 0), x, :3] = (0.2, 0.15, 0.14)   # lit rim of the cliff edge
# player: back to the camera, standing at the cliff edge, centred
sh = 760; sw = int(pl.shape[1]*sh/pl.shape[0])
im = bpy.data.images.new("p", pl.shape[1], pl.shape[0], alpha=True); im.pixels = pl.ravel().tolist(); im.scale(sw, sh)
pl2 = np.array(im.pixels[:], dtype=np.float32).reshape(sh, sw, 4)
paste(cv, pl2, (W-sw)//2, 40)
out = bpy.data.images.new("o", W, H, alpha=True); out.pixels = cv.ravel().tolist()
out.filepath_raw = outpng; out.file_format = 'PNG'; out.save(); print("SAVED", outpng)
