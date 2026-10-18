# contact sheet: python sheet.py out.png in1.png in2.png ...   (scaled to a common height, 2 rows if > 3)
import sys
from PIL import Image
out, ins = sys.argv[1], sys.argv[2:]
ims = [Image.open(p).convert("RGB") for p in ins]
h = 420
ims = [im.resize((int(im.width * h / im.height), h)) for im in ims]
cols = 3 if len(ims) > 3 else len(ims)
rows = (len(ims) + cols - 1) // cols
w = max(im.width for im in ims)
S = Image.new("RGB", (w * cols, h * rows), (40, 40, 40))
for i, im in enumerate(ims):
    S.paste(im, ((i % cols) * w, (i // cols) * h))
S.save(out)
