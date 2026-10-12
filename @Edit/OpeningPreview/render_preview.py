# Blender headless: GLB -> 5-view (front/back/left/right/top) renders -> one combined preview image (rule 28).
# usage: blender -b -P render_preview.py -- <glb> <outdir> <outname>
import bpy, sys, math, os
from mathutils import Vector
import numpy as np

glb, outdir, outname = sys.argv[sys.argv.index("--")+1:][:3]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
center, size = (mn+mx)/2, max(mx-mn)
print("BBOX", mn, mx)

sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sh = sc.display.shading
sh.light = 'STUDIO'; sh.color_type = 'TEXTURE'; sh.show_cavity = False
sc.render.resolution_x = sc.render.resolution_y = 640
sc.render.film_transparent = False
sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.82, 0.82, 0.82)
sc.view_settings.view_transform = 'Standard'

cam_d = bpy.data.cameras.new("c"); cam_d.type = 'ORTHO'; cam_d.ortho_scale = size*1.15
cam = bpy.data.objects.new("c", cam_d); sc.collection.objects.link(cam); sc.camera = cam
dist = size*3
# camera positions around the model (glTF import: Blender -Y is the model's front)
views = {"front": (0,-1,0), "back": (0,1,0), "left": (-1,0,0), "right": (1,0,0), "top": (0,0,1)}
imgs = {}
for name, d in views.items():
    d = Vector(d)
    cam.location = center + d*dist
    up = Vector((0,1,0)) if name == "top" else Vector((0,0,1))
    # top view: fixed rotation (image-up = +Y/back, so the front points down); others: track toward the model
    cam.rotation_euler = (0, 0, 0) if name == "top" else (-d).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(outdir, f"_{name}.png")
    bpy.ops.render.render(write_still=True)
    imgs[name] = sc.render.filepath

# combine: 4 corners + top in the centre, tiles 640px, no overlap
T, C = 640, 2048
canvas = np.ones((C, C, 4), dtype=np.float32)*0.5; canvas[...,3] = 1
pos = {"front": (0, C-T), "back": (C-T, C-T), "left": (0, 0), "right": (C-T, 0), "top": ((C-T)//2, (C-T)//2)}  # (x, y) y from bottom
for name, p in imgs.items():
    im = bpy.data.images.load(p)
    a = np.array(im.pixels[:], dtype=np.float32).reshape(T, T, 4)
    x, y = pos[name]
    canvas[y:y+T, x:x+T] = a
out = bpy.data.images.new("preview", C, C, alpha=True)
out.pixels = canvas.ravel().tolist()
out.filepath_raw = os.path.join(outdir, outname + ".png"); out.file_format = 'PNG'; out.save()
print("SAVED", out.filepath_raw)
