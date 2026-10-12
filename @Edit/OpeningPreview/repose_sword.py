# Blender headless: re-pose the sword arm of the (unrigged) VARCO knight mesh without touching the rest of the shape.
# The sword + gripping hand are rotated rigidly about the grip centre G so the blade points forward-up (toward the demon
# castle); vertices within a soft falloff band around the sword segment blend smoothly into the untouched body.
# usage: blender -b -P repose_sword.py -- <in.glb> <out.glb> [elevation_deg]
import bpy, sys, math
import numpy as np
from mathutils import Vector, Matrix

args = sys.argv[sys.argv.index("--")+1:]
src, dst = args[0], args[1]
elev = math.radians(float(args[2])) if len(args) > 2 else math.radians(25)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
ob = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
P = np.array([tuple(v.co) for v in ob.data.vertices], dtype=np.float64)

# --- measured from the mesh (see _axis.py): blade axis, grip centre, ends of the sword segment ---
a = np.array([0.912, -0.311, 0.266]); a /= np.linalg.norm(a)          # pommel -> tip direction
G = np.array([-0.218, -0.133, 0.366])                                  # fist / grip centre (rotation pivot)
seg0 = G - a*0.05                                                      # pommel end of the sword segment
seg1 = G + a*0.49                                                      # blade tip end
dt = np.array([0.0, -math.cos(elev), math.sin(elev)])                  # target: model forward (-Y), raised by `elev`

# --- soft weights: 1 inside r1 of the sword segment, smooth fall-off to 0 at r2 ---
ab = seg1 - seg0; t = np.clip(((P - seg0) @ ab)/(ab @ ab), 0, 1)
d = np.linalg.norm(P - (seg0 + np.outer(t, ab)), axis=1)
r1, r2 = 0.05, 0.085
w = np.clip((r2 - d)/(r2 - r1), 0, 1); w = w*w*(3 - 2*w)               # smoothstep
w[(P[:, 2] < G[2] - 0.06)] = 0                                         # never touch the forearm/torso below the hand
w[P[:, 1] > 0.0] = 0                                                   # never touch the back (cape) side
print("MOVED", int((w > 0).sum()), "of", len(P))

# --- rotation taking the current blade axis onto the target axis, about G ---
v = np.cross(a, dt); s = np.linalg.norm(v); c = a @ dt
K = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]])
R = np.eye(3) + K + K@K*((1-c)/(s*s))
print("ANGLE", math.degrees(math.acos(c)))
Pr = (P - G) @ R.T + G
Pn = P + (Pr - P)*w[:, None]                                           # blend rotated and original positions
for vtx, p in zip(ob.data.vertices, Pn): vtx.co = Vector(p)
ob.data.update()
bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB')
print("EXPORTED", dst)
