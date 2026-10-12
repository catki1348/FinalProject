# Blender headless: re-pose the sword arm of the (unrigged) VARCO knight mesh - extend the whole arm toward the target.
# Step 1: forearm + hand + sword rotate rigidly about the shoulder S so the arm points along the target direction
#         (soft weight ramp near the shoulder keeps the pauldron/torso untouched).
# Step 2: sword + gripping hand rotate about the fist so the blade is collinear with the arm.
# Only arm/sword vertices move; the body, shield, cape, helmet and legs keep their original shape.
# usage: blender -b -P repose_arm.py -- <in.glb> <out.glb> [yaw_to_right_deg=20] [elevation_deg=3]
import bpy, bmesh, sys, math
import numpy as np
from mathutils import Vector

args = sys.argv[sys.argv.index("--")+1:]
src, dst = args[0], args[1]
yaw = math.radians(float(args[2])) if len(args) > 2 else math.radians(20)   # how far the arm swings toward the model's right (-X)
elev = math.radians(float(args[3])) if len(args) > 3 else math.radians(3)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
ob = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
me = ob.data
P = np.array([tuple(v.co) for v in me.vertices], dtype=np.float64)

# --- arm / sword vertices = whole loose islands (armor pieces) located on the sword-arm side, high and in front ---
bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
seen, cand = set(), np.zeros(len(P), dtype=bool)
for v in bm.verts:
    if v.index in seen: continue
    st, comp = [v], []; seen.add(v.index)
    while st:
        p = st.pop(); comp.append(p)
        for e in p.link_edges:
            q = e.other_vert(p)
            if q.index not in seen: seen.add(q.index); st.append(q)
    m = sum((x.co for x in comp), Vector())/len(comp)
    if (m.x < -0.12 and m.z > 0.15 and m.y < 0.04) or (m.y < -0.12 and m.z > 0.3):
        for x in comp: cand[x.index] = True
print("ARM_VERTS", int(cand.sum()))

def rot_between(a, b):
    """Rotation matrix taking unit vector a onto unit vector b."""
    v = np.cross(a, b); s = np.linalg.norm(v); c = a @ b
    if s < 1e-9: return np.eye(3)
    K = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]])
    return np.eye(3) + K + K@K*((1-c)/(s*s))

def smooth(x): x = np.clip(x, 0, 1); return x*x*(3-2*x)

# --- measured joints (see repose_sword.py): shoulder S, fist/grip centre G, blade axis a ---
S = np.array([-0.17, -0.02, 0.20])
G = np.array([-0.218, -0.133, 0.366])
a = np.array([0.912, -0.311, 0.266]); a /= np.linalg.norm(a)
dt = np.array([-math.sin(yaw)*math.cos(elev), -math.cos(yaw)*math.cos(elev), math.sin(elev)])   # target arm/blade direction

# --- step 1: arm swings about the shoulder ---
u0 = (G - S)/np.linalg.norm(G - S)
R1 = rot_between(u0, dt)
w1 = cand * smooth(((P - S) @ u0)/0.10)                      # 0 at the shoulder, 1 once 10 cm along the arm
P1 = P + (((P - S) @ R1.T + S) - P)*w1[:, None]
G1 = (G - S) @ R1.T + S
print("ARM_ANGLE", math.degrees(math.acos(np.clip(u0 @ dt, -1, 1))))

# --- step 2: sword + hand swing about the fist so the blade lies along the arm ---
a1 = R1 @ a
R2 = rot_between(a1, dt)
seg0, seg1 = G - a*0.05, G + a*0.49
ab = seg1 - seg0; t = np.clip(((P - seg0) @ ab)/(ab @ ab), 0, 1)
d = np.linalg.norm(P - (seg0 + np.outer(t, ab)), axis=1)
w2 = smooth((0.085 - d)/(0.085 - 0.05)) * cand
w2[P[:, 2] < G[2] - 0.06] = 0
P2 = P1 + (((P1 - G1) @ R2.T + G1) - P1)*w2[:, None]
print("SWORD_ANGLE", math.degrees(math.acos(np.clip(a1 @ dt, -1, 1))))

for vtx, p in zip(me.vertices, P2): vtx.co = Vector(p)
me.update()
bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB')
print("EXPORTED", dst)
