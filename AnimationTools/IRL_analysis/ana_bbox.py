import numpy as np, glob, sys, json
from PIL import Image
sp = sys.argv[1]
files = sorted(glob.glob(sp + "/irlfull/g_*.png"))
imgs = [np.asarray(Image.open(f).convert("L").resize((960, 540)), dtype=np.float32) for f in files]
bg = np.median(np.stack(imgs), axis=0)
out = []
for i, a in enumerate(imgs):
    d = np.abs(a - bg) > 28
    ys, xs = np.where(d)
    if len(xs) < 400: out.append(None); continue
    x0, x1 = np.percentile(xs, [2, 98]); y0, y1 = np.percentile(ys, [1, 99.5])
    out.append([int(x0*2), int(x1*2), int(y0*2), int(y1*2)])
json.dump(out, open(sp + "/ana/bbox.json", "w"))
for i, b in enumerate(out): print(i + 1, b, (b[3]-b[2]) if b else None)
