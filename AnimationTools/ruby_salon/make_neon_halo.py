# Halo mask for the Custom/NeonGlow quad on the Emerald Salon sign (glow that shows through fog).
# Run AFTER build_emerald_letters.py (reads letters_mask.json).   python make_neon_halo.py
# Output: Assets/Models/RubySalon/EmeraldSalon_NeonHalo.png  - covers photo px HALO_RECT (keep in sync with the Unity quad)
import os, json
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "Assets", "Models", "RubySalon", "EmeraldSalon_NeonHalo.png"))
HALO_RECT = (22, 74, 238, 182)      # photo px x0, y0, x1, y1
K = 3                               # texture px per photo px

x0, y0, x1, y1 = HALO_RECT
W, H = (x1 - x0) * K, (y1 - y0) * K
mask = Image.new("L", (W, H), 0)
d = ImageDraw.Draw(mask)
for tri in json.load(open(os.path.join(HERE, "letters_mask.json"))):
    d.polygon([((px - x0) * K, (py - y0) * K) for px, py in tri], fill=255)
f = lambda s: np.asarray(mask.filter(ImageFilter.GaussianBlur(s * K))).astype(np.float32) / 255
tight, mid, wide = f(1.5), f(4), f(11)
halo = np.clip(tight * 0.9 + mid * 0.7 + wide * 0.55, 0, 1)
# fade to black at the borders so the quad's edge never shows
yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
edge = np.clip(np.minimum.reduce([xx, W - 1 - xx, yy, H - 1 - yy]) / (6 * K), 0, 1)
halo *= edge
Image.fromarray((halo * 255 + 0.5).astype(np.uint8)).save(OUT)
print("saved", OUT, (W, H))
