# Blender headless: horizontally remap the generated background (piecewise-linear) so the cliff tip lands on the guide position.
# Left part [0, tip_src] is squeezed into [0, tip_dst]; the right part is stretched to fill the rest (screen edges stay fixed).
# usage: blender -b -P align_cliff.py -- <in.png> <out.png> <tip_src_x> <tip_dst_x>     (px in the 1376x768 source)
import bpy, sys
import numpy as np
inp, outp, ts, td = sys.argv[sys.argv.index("--")+1:][:4]; ts, td = float(ts), float(td)
im = bpy.data.images.load(inp); w, h = im.size
a = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
xo = np.arange(w, dtype=np.float32)
xs = np.where(xo <= td, xo*(ts/td), ts + (xo-td)*((w-1-ts)/(w-1-td)))      # source x for each output x
x0 = np.clip(np.floor(xs).astype(int), 0, w-2); f = (xs - x0)[None, :, None]
res = a[:, x0]*(1-f) + a[:, x0+1]*f                                         # bilinear along x
out = bpy.data.images.new("o", w, h, alpha=True); out.pixels = res.ravel().tolist()
out.filepath_raw = outp; out.file_format = 'PNG'; out.save(); print("SAVED", outp)
