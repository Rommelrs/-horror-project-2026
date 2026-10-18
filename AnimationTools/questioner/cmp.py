# side-by-side: python cmp.py out.png img1 img2 ...  (all scaled to height 520, one row)
import sys
from PIL import Image
out, ins = sys.argv[1], sys.argv[2:]
h = 520
ims = [Image.open(p).convert("RGB") for p in ins]
ims = [im.resize((int(im.width * h / im.height), h)) for im in ims]
S = Image.new("RGB", (sum(i.width for i in ims) + 6 * (len(ims) - 1), h), (90, 20, 20))
x = 0
for im in ims:
    S.paste(im, (x, 0)); x += im.width + 6
S.save(out)
