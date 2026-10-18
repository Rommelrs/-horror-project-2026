# Emerald Salon version of the storefront texture, built from the Ruby Salon photo (ruby_salon_ref.png).
# Run AFTER build_emerald_letters.py (Blender) - it reads letters_mask.json to paint the neon halo exactly behind the
# 3D letters.   python make_emerald_textures.py
# Output: Assets/Models/RubySalon/EmeraldSalon_Facade.png  (same atlas layout as RubySalon_Facade.png, so the
# storefront mesh UVs don't change - only the material's texture is swapped)
#   - red cladding / sign panel recoloured to emerald (door left untouched)
#   - the old lettering erased from the sign panel (the 3D letters replace it), green-white glow painted behind them
#   - small strap line under the sign and the door sticker rewritten for Emerald
import os, json
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Models", "RubySalon", "EmeraldSalon_Facade.png")
FONTS = r"C:\Windows\Fonts"
ATLAS_RECT = (36, 16, 224, 380)
UP = 4
EMERALD_HUE = 156 / 360.0
CLAD_V = 0.60          # brightness of the emerald cladding relative to the photo red
PANEL_V = 0.42         # sign panel (darker, like the photo)
GLOW = np.array([0.55, 1.0, 0.72])        # halo colour around the letters
FACE = np.array([0.93, 1.0, 0.94])

def shape(forms):            # presentation forms in logical order -> visual string
    return "".join(reversed(forms))
AR_SMALL = shape(["\uFEBB", "\uFE8E", "\uFEDF", "\uFEEE", "\uFEE5", " ", "\uFE8D", "\uFEDF", "\uFEB0", "\uFEE3", "\uFEAE", "\uFEA9",
                  " ", "\uFEDF", "\uFEE0", "\uFEAE", "\uFE9F", "\uFE8E", "\uFEDD"])          # صالون الزمرد للرجال

ref = np.asarray(Image.open(os.path.join(HERE, "ruby_salon_ref.png")).convert("RGB")).astype(np.float32) / 255
img = ref.copy()

# ---- bicycle removal (same as make_ruby_textures.py)
for y in range(322, 380):
    for x in range(209, 224):
        img[y, x] = ref[y, 209 - (x - 209) - 2]
img[176:380, 220:224] = ref[176:300, 220:224].mean(axis=(0, 1))

# ---- remove the downlight pools baked into the photo on the cladding under the ledge (the 3D downlights + LampGlow do
# that job now): every cladding column takes its plain colour from just below the pools, above the door from the sides
POOL_Y0, POOL_Y1 = 176, 240
ref_rows = slice(POOL_Y1, POOL_Y1 + 18)
side = np.median(np.concatenate([img[ref_rows, 50:92], img[ref_rows, 186:216]], axis=1).reshape(-1, 3), axis=0)
for x in range(47, 220):
    in_door = 96 <= x < 183
    col = side if in_door else np.median(img[ref_rows, x], axis=0)
    y_end = 195 if in_door else POOL_Y1
    t = np.linspace(0, 1, y_end - POOL_Y0)[:, None]
    # tiny darkening towards the ledge (it shades the top of the wall), blended into the untouched rows below
    img[POOL_Y0:y_end, x] = col * (0.9 + 0.1 * t)
for y in range(POOL_Y1, POOL_Y1 + 6):                      # soften the seam
    a = (y - POOL_Y1 + 1) / 7
    for x in list(range(47, 96)) + list(range(183, 220)):
        img[y, x] = img[POOL_Y1 - 1, x] * (1 - a) + img[y, x] * a

# ---- erase the old letters + strap line from the sign panel (diffusion fill from the surrounding panel)
hole = np.zeros(img.shape[:2], bool)
hole[90:167, 39:221] = True
known = ~hole
fill = img.copy()
fill[hole] = img[known & (np.arange(img.shape[0])[:, None] >= 80) & (np.arange(img.shape[0])[:, None] < 175)].mean(axis=0)
for _ in range(1500):
    p = np.pad(fill, ((1, 1), (1, 1), (0, 0)), mode="edge")
    avg = (p[:-2, 1:-1] + p[2:, 1:-1] + p[1:-1, :-2] + p[1:-1, 2:]) / 4
    fill[hole] = avg[hole]
img = fill
# the old letters' orange glow leaked onto the ledge just below - tone it to the plain ledge colour
img[167:176, 39:222] = np.minimum(img[167:176, 39:222], img[170:173, 40:60].mean(axis=(0, 1)) * 1.15)

# ---- red -> emerald (everything except the glass door)
mx, mn = img.max(-1), img.min(-1)
sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
r, g, b = img[..., 0], img[..., 1], img[..., 2]
d = np.maximum(mx - mn, 1e-6)
hue = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) / 6
hdist = np.minimum(np.abs(hue - 0), 1 - np.abs(hue - 0))                 # distance from red
w = np.clip((0.27 - hdist) / 0.05, 0, 1) * np.clip((sat - 0.03) / 0.10, 0, 1)   # reds, pinks, magentas
w[199:380, 101:178] = 0                                                   # door
def hsv2rgb(h, s, v):
    i = np.floor(h * 6) % 6; f = h * 6 - np.floor(h * 6)
    p, q, t = v * (1 - s), v * (1 - f * s), v * (1 - (1 - f) * s)
    out = np.zeros(h.shape + (3,))
    for k, (a, bb, c) in enumerate([(v, t, p), (q, v, p), (p, v, t), (p, q, v), (t, p, v), (v, p, q)]):
        m = i == k
        out[m] = np.stack([a[m], bb[m], c[m]], -1)
    return out
vscale = np.full(hue.shape, CLAD_V)
vscale[40:167, :] = PANEL_V
green = hsv2rgb(np.full(hue.shape, EMERALD_HUE), np.clip(sat * 1.05, 0, 1), mx * vscale)
img = img * (1 - w[..., None]) + green * w[..., None]

# ---- crop + upscale
x0, y0, x1, y1 = ATLAS_RECT
big = Image.fromarray((np.clip(img[y0:y1, x0:x1], 0, 1) * 255).astype(np.uint8))
big = big.resize(((x1 - x0) * UP, (y1 - y0) * UP), Image.LANCZOS).filter(ImageFilter.UnsharpMask(radius=3, percent=60, threshold=2))
W, H = big.size
A = np.asarray(big).astype(np.float32) / 255
to_atlas = lambda px, py: ((px - x0) * UP, (py - y0) * UP)

# ---- neon halo behind the 3D letters
mask = Image.new("L", (W, H), 0)
md = ImageDraw.Draw(mask)
for tri in json.load(open(os.path.join(HERE, "letters_mask.json"))):
    md.polygon([to_atlas(px, py) for px, py in tri], fill=255)
wide = np.asarray(mask.filter(ImageFilter.GaussianBlur(26))).astype(np.float32) / 255
tight = np.asarray(mask.filter(ImageFilter.GaussianBlur(7))).astype(np.float32) / 255
A = A + GLOW * (wide[..., None] * 0.65 + tight[..., None] * 0.55)
core = np.asarray(mask).astype(np.float32)[..., None] / 255             # (hidden under the 3D letters anyway)
A = A * (1 - core) + FACE * core

def text_layer(draws):
    lay = Image.new("L", (W, H), 0)
    dd = ImageDraw.Draw(lay)
    for txt, font, size, (cx, cy) in draws:
        f = ImageFont.truetype(os.path.join(FONTS, font), size)
        l, t, r_, b_ = f.getbbox(txt)
        dd.text((cx - (r_ - l) / 2 - l, cy - (b_ - t) / 2 - t), txt, font=f, fill=255)
    return np.asarray(lay).astype(np.float32)[..., None] / 255

# ---- strap line under the sign (photo y ~163): dim lit text, English left / Arabic right
strap = text_layer([("DELUXE EMERALD GENTS SALON - L.L.C", "arialbd.ttf", 14, to_atlas(88, 163)),
                    (AR_SMALL, "tahomabd.ttf", 17, to_atlas(178, 163))])
A = A * (1 - strap * 0.75) + np.array([0.78, 0.92, 0.82]) * strap * 0.75

# ---- door sticker (photo x 110..181, y 286..299): repaint the white strip, then two dark lines of text
sx0, sy0 = to_atlas(110, 286); sx1, sy1 = to_atlas(182, 299)
paper = np.median(A[sy0:sy1, sx0:sx1].reshape(-1, 3), axis=0)
paper = np.maximum(paper, A[sy0:sy1, sx0:sx1].reshape(-1, 3).max(0) * 0.9)
A[sy0:sy1, sx0:sx1] = paper
stick = text_layer([(AR_SMALL, "tahomabd.ttf", 21, to_atlas(146, 289.5)),
                    ("EMERALD GENTS SALON", "arialbd.ttf", 17, to_atlas(146, 295.8))])
A = A * (1 - stick) + np.array([0.08, 0.12, 0.10]) * stick

Image.fromarray((np.clip(A, 0, 1) * 255 + 0.5).astype(np.uint8)).save(OUT)
print("saved", OUT, (W, H))
