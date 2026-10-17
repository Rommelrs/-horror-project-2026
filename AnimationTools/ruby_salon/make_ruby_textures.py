# Ruby Salon storefront textures, built straight from the reference photo (ruby_salon_ref.png, 271x399).
# Run with the system python (PIL):  python make_ruby_textures.py
# Outputs (Assets/Models/RubySalon/):
#   RubySalon_Facade.png  the whole storefront (header, sign, ledge, red cladding, door) cropped from the photo,
#                         bike removed, upscaled x4.  The Unity build script maps every face into it using the
#                         photo pixel coordinates in ATLAS_RECT below.
#   RubySalon_Turf.png    green artificial-grass mat in front of the shop (colour sampled from the photo)
import os
import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Models", "RubySalon")
os.makedirs(OUT, exist_ok=True)

ATLAS_RECT = (36, 16, 224, 380)   # photo px (x0, y0, x1, y1) -> atlas; keep in sync with the Unity build script
UP = 4

ref = np.asarray(Image.open(os.path.join(HERE, "ruby_salon_ref.png")).convert("RGB")).astype(np.float32)

# ---- remove the bicycle wheel that overlaps the right edge of the red cladding (photo x >= 209, y >= 322):
# rebuild those pixels from the clean red columns just to their left (mirrored), keeping the grime gradient.
fix = ref.copy()
for y in range(322, 380):
    for x in range(209, 224):
        src = 209 - (x - 209) - 2
        fix[y, x] = ref[y, src]
# the cladding ends at x ~219 in the photo; beyond it is the neighbour's dark shutter -> keep it dark
fix[176:380, 220:224] = ref[176:300, 220:224].mean(axis=(0, 1))

x0, y0, x1, y1 = ATLAS_RECT
crop = Image.fromarray(np.clip(fix[y0:y1, x0:x1], 0, 255).astype(np.uint8))
big = crop.resize(((x1 - x0) * UP, (y1 - y0) * UP), Image.LANCZOS)
big = big.filter(ImageFilter.UnsharpMask(radius=3, percent=60, threshold=2))
big.save(os.path.join(OUT, "RubySalon_Facade.png"))
print("saved RubySalon_Facade.png", big.size)

# ---- turf: noisy green blades, colour from the photo's mat (~ (142,197,167) under the downlights, darker away)
rng = np.random.default_rng(3)
N = 256
base = np.array([0.40, 0.62, 0.45])
n1 = rng.random((N, N))
n2 = np.asarray(Image.fromarray((rng.random((N // 8, N // 8)) * 255).astype(np.uint8)).resize((N, N), Image.BILINEAR)) / 255.0
blades = np.asarray(Image.fromarray((n1 * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6))) / 255.0
turf = base * (0.75 + 0.35 * blades[..., None]) * (0.85 + 0.25 * n2[..., None])
Image.fromarray((np.clip(turf, 0, 1) * 255).astype(np.uint8)).save(os.path.join(OUT, "RubySalon_Turf.png"))
print("saved RubySalon_Turf.png")
