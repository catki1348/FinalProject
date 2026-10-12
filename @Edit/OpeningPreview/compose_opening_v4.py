# Blender headless: final opening-screen preview. Background = chosen generated landscape (Blend_Center); the repositioned
# knight (SwordForward model, own rock slab removed) stands on the lower-left cliff ledge, seen from behind in 3/4 view,
# sword pointing toward the Stage5 castle. Image preview only - no Unity involved.
# usage: blender -b -P compose_opening_v4.py -- <glb> <background.png> <outpng> [feet_x feet_y figure_h]   (px in 1920x1080, y from top)
# feet_x = centre of the boots, feet_y = sole line, figure_h = height from soles to the top of the horns (independent of the arm/sword pose).
import bpy, bmesh, sys, os
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index("--")+1:]
glb, bgpath, outpng = a[:3]
fx, fy, ph = (float(v) for v in a[3:6]) if len(a) >= 6 else (160.0, 858.0, 520.0)
W, H = 1920, 1080

def load_rgba(path, w, h):
    """Load an image resampled to (w, h) -> float array [h, w, 4] (rows bottom-up like Blender)."""
    im = bpy.data.images.load(path); im.scale(w, h)
    return np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)

# --- 1. render the knight (rock slab removed) from behind-left: back to camera, facing image-right ---
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
sc = bpy.context.scene
ob = [o for o in sc.objects if o.type == 'MESH'][0]
bm = bmesh.new(); bm.from_mesh(ob.data); bm.verts.ensure_lookup_table()
seen, kill = set(), []
for v in bm.verts:                                      # drop the generated rock slab: wide islands that sit entirely at the bottom
    if v.index in seen: continue
    st, comp = [v], []; seen.add(v.index)
    while st:
        p = st.pop(); comp.append(p)
        for e in p.link_edges:
            q = e.other_vert(p)
            if q.index not in seen: seen.add(q.index); st.append(q)
    zs = [c.co.z for c in comp]; xs = [c.co.x for c in comp]
    if max(zs) < -0.40: kill += comp
bmesh.ops.delete(bm, geom=kill, context='VERTS'); bm.to_mesh(ob.data)
print("SLAB_VERTS_REMOVED", len(kill))
pts = [v.co for v in ob.data.vertices]
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
d = Vector((-1, 1, -0.12)).normalized()
cam.location = ctr + d*size*3
cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
tmp = os.path.join(os.path.dirname(outpng), "_player_tmp.png")
sc.render.filepath = tmp; bpy.ops.render.render(write_still=True)
pl = load_rgba(tmp, 1400, 1400); os.remove(tmp)
ys, xs = np.where(pl[..., 3] > 0.05); pl = pl[ys.min():ys.max()+1, xs.min():xs.max()+1]

# --- 2. background scaled to cover 1920x1080 (centre-cropped) ---
bw, bh = 1935, 1080
bg = load_rgba(bgpath, bw, bh)[:, (bw-W)//2:(bw-W)//2+W]
cv = bg.copy(); cv[..., 3] = 1

# --- 3. soft contact shadow under the feet, then the player ---
# scale by the model's real height (z extent) so the figure size does not change with the arm/sword pose
ppu_render = 1400/cd.ortho_scale; factor = (ph/(mx.z - mn.z))/ppu_render
sh = int(pl.shape[0]*factor); sw = int(pl.shape[1]*factor)
feet_rows = np.where(pl[-max(int(pl.shape[0]*0.05), 3):, :, 3] > 0.05)[1]      # boots: bottom 5% of the silhouette
fc = float(feet_rows.mean())*factor if len(feet_rows) else sw/2
im = bpy.data.images.new("p", pl.shape[1], pl.shape[0], alpha=True); im.pixels = pl.ravel().tolist(); im.scale(sw, sh)
p2 = np.array(im.pixels[:], dtype=np.float32).reshape(sh, sw, 4)
bx = int(fx - fc); by = int(H - fy)                   # bottom-left of the player; y from the bottom
yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
sd = np.clip(1 - (((xx-fx)/(sw*0.42))**2 + ((yy-(H-fy))/18.0)**2), 0, 1)
cv[..., :3] *= (1 - 0.55*sd[..., None])
x0, y0 = max(bx, 0), max(by, 0); x1, y1 = min(bx+sw, W), min(by+sh, H)
src = p2[y0-by:y1-by, x0-bx:x1-bx]; dst = cv[y0:y1, x0:x1]
al = src[..., 3:4]; dst[..., :3] = src[..., :3]*al + dst[..., :3]*(1-al)

out = bpy.data.images.new("o", W, H, alpha=True); out.pixels = cv.ravel().tolist()
out.filepath_raw = outpng; out.file_format = 'PNG'; out.save(); print("SAVED", outpng)
