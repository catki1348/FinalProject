# Blender headless: images for the game-clear screen and the ending scene (no Unity needed).
#  - GameClear_Title.png / TheEnd_Title.png : gold 3D block lettering (same style as the FINAL DUNGEON title / menu words)
#  - Ending_Background.png : "dawn" grade of the opening landscape (storm-red sky -> warm gold) WITHOUT the knight
#  - Ending_Knight.png = copy of Opening_Player.png (knight layer, so the scene can animate it separately)
#  - Ending_Logo.png   = copy of Opening_Title.png
# usage: blender -b -P build_ending_images.py -- <sprites_dir (Assets/Sprites/Opening)> <out_dir (Assets/Resources/Ending)>
import bpy, sys, math, os, shutil
import numpy as np
sd, od = sys.argv[sys.argv.index("--")+1:][:2]
os.makedirs(od, exist_ok=True)
FONT = "C:/Windows/Fonts/georgiab.ttf"

def make_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True); sc = bpy.context.scene
    def mat(name, base, metal, rough, ramp=None):
        m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; b = nt.nodes["Principled BSDF"]
        b.inputs["Metallic"].default_value = metal; b.inputs["Roughness"].default_value = rough; b.inputs["Base Color"].default_value = base
        if ramp:
            tc = nt.nodes.new("ShaderNodeTexCoord"); sp = nt.nodes.new("ShaderNodeSeparateXYZ"); cr = nt.nodes.new("ShaderNodeValToRGB")
            nt.links.new(tc.outputs["Generated"], sp.inputs[0]); nt.links.new(sp.outputs["Y"], cr.inputs["Fac"])
            el = cr.color_ramp.elements; el[0].position, el[0].color = ramp[0]; el[1].position, el[1].color = ramp[-1]
            for p, c in ramp[1:-1]: e = el.new(p); e.color = c
            nt.links.new(cr.outputs["Color"], b.inputs["Base Color"])
        return m
    face = mat("face", (1, .7, .2, 1), .85, .28, [(0.0, (.55, .28, .05, 1)), (.55, (1, .72, .22, 1)), (1.0, (1, .95, .65, 1))])
    body = mat("body", (.45, .22, .04, 1), .9, .3); line = mat("line", (.02, .03, .12, 1), 0, .6)
    return sc, face, body, line

def render_word(word, path):
    sc, face, body, line = make_scene(); font = bpy.data.fonts.load(FONT)
    def layer(ext, bev, off, m, dx, dy_, name, dz=0.0):
        cu = bpy.data.curves.new(name, 'FONT'); cu.body = word; cu.font = font; cu.align_x = 'CENTER'; cu.size = 1.0
        cu.extrude = ext; cu.bevel_depth = bev; cu.bevel_resolution = 2; cu.offset = off
        ob = bpy.data.objects.new(name, cu); ob.data.materials.append(m); sc.collection.objects.link(ob)
        ob.rotation_euler = (math.pi/2, 0, 0); ob.location = (dx, dy_, dz); return ob
    layer(0.02, 0.0, 0.07, line, -0.06, 0.30, "o")
    for i in range(8, 0, -1): layer(0.02, 0.0, 0.035, body, -0.045*i/8, 0.1 + 0.01*i, "b%d" % i, dz=-0.045*i/8)
    layer(0.04, 0.012, -0.008, face, 0, -0.1, "f")
    cd = bpy.data.cameras.new("c"); cd.type = 'ORTHO'; cd.ortho_scale = 12.0
    cam = bpy.data.objects.new("c", cd); sc.collection.objects.link(cam); sc.camera = cam
    cam.location = (0, -20, 0.2); cam.rotation_euler = (math.pi/2, 0, 0)
    sun = bpy.data.lights.new("s", 'SUN'); sun.energy = 2.4; so = bpy.data.objects.new("s", sun); sc.collection.objects.link(so)
    so.rotation_euler = (math.radians(50), math.radians(10), math.radians(-25))
    w = bpy.data.worlds.new("w"); w.use_nodes = True; sc.world = w
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (.9, .95, 1, 1); w.node_tree.nodes["Background"].inputs["Strength"].default_value = .7
    sc.render.film_transparent = True; sc.render.engine = 'BLENDER_EEVEE'; sc.view_settings.view_transform = 'Standard'
    sc.render.resolution_x, sc.render.resolution_y = 4800, 1200
    tmp = os.path.join(od, "_w.png"); sc.render.filepath = tmp; bpy.ops.render.render(write_still=True)
    im = bpy.data.images.load(tmp); W, H = im.size
    a = np.array(im.pixels[:], dtype=np.float32).reshape(H, W, 4)
    ys, xs = np.where(a[..., 3] > 0.05); a = a[ys.min():ys.max()+1, xs.min():xs.max()+1]
    o = bpy.data.images.new("o", a.shape[1], a.shape[0], alpha=True); o.pixels = a.ravel().tolist()
    o.filepath_raw = path; o.file_format = 'PNG'; o.save(); os.remove(tmp); print("WORD", word, a.shape[1], a.shape[0])

render_word("GAME CLEAR", os.path.join(od, "GameClear_Title.png"))
render_word("THE END", os.path.join(od, "TheEnd_Title.png"))

# ---- dawn background: storm-red -> warm gold, a little brighter, soft golden haze around the castle plateau ----
im = bpy.data.images.load(os.path.join(sd, "Opening_Background.png")); w, h = im.size
a = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)[..., :3]
r, g, b = a[..., 0], a[..., 1], a[..., 2]
red = np.clip((r - np.maximum(g, b) - 0.06)/0.35, 0, 1)[..., None]
warm = np.stack([np.minimum(r*1.05 + 0.10, 1), np.minimum(g*1.55 + 0.10, 1), np.minimum(b*1.10 + 0.02, 1)], -1)
out = a*(1 - red) + warm*red
yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
haze = np.exp(-(((xx/w - 0.62)/0.45)**2 + ((yy/h - 0.55)/0.30)**2))[..., None]
out = 1 - (1 - out)*(1 - 0.38*haze*np.array([1.0, 0.82, 0.52], np.float32))
out = np.clip(out*1.06, 0, 1)
res = np.dstack([out, np.ones((h, w))]).astype(np.float32)
o = bpy.data.images.new("bg", w, h, alpha=True); o.pixels = res.ravel().tolist(); o.filepath_raw = os.path.join(od, "Ending_Background.png"); o.file_format = 'PNG'; o.save()
shutil.copyfile(os.path.join(sd, "Opening_Player.png"), os.path.join(od, "Ending_Knight.png"))
shutil.copyfile(os.path.join(sd, "Opening_Title.png"), os.path.join(od, "Ending_Logo.png"))
print("DONE", w, h)
