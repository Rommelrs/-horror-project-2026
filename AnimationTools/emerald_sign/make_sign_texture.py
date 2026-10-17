# Emerald Salon neon lightbox face (Ruby Salon style: glowing channel letters on a coloured panel, Arabic over English).
# Run with the system python (PIL):  python make_sign_texture.py
# Output: Assets/Models/EmeraldSign/EmeraldSign_Face.png   1280x256  (panel is 7.5 x 1.5 m -> 5:1)
# No Arabic shaping library is installed, so the joined forms are written out by hand (Arabic Presentation Forms-B),
# in visual (left-to-right) order.
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026"
OUT = os.path.join(ROOT, "Assets", "Models", "EmeraldSign", "EmeraldSign_Face.png")
os.makedirs(os.path.dirname(OUT), exist_ok=True)
W, H = 1280, 256
FONTS = r"C:\Windows\Fonts"

# صالون الزمرد  -> logical: ص(ini) ا(fin) ل(ini) و(fin) ن(iso)  space  ا(iso) ل(ini) ز(fin) م(ini) ر(fin) د(iso)
logical = ["\uFEBB", "\uFE8E", "\uFEDF", "\uFEEE", "\uFEE5", " ", "\uFE8D", "\uFEDF", "\uFEB0", "\uFEE3", "\uFEAE", "\uFEA9"]
ARABIC = "".join(reversed(logical))
ENGLISH = "EMERALD SALON"
SMALL = "EMERALD GENTS SALON  L.L.C"

def fit_font(path, text, max_w, max_h):
    size = 10
    best = ImageFont.truetype(path, size)
    while True:
        f = ImageFont.truetype(path, size + 2)
        l, t, r, b = f.getbbox(text)
        if r - l > max_w or b - t > max_h: break
        best = f; size += 2
    return best

def draw_centered(mask, text, font, cy):
    d = ImageDraw.Draw(mask)
    l, t, r, b = font.getbbox(text)
    x = (W - (r - l)) / 2 - l
    y = cy - (b - t) / 2 - t
    d.text((x, y), text, font=font, fill=255)

# ---- letter masks
ar_mask = Image.new("L", (W, H), 0)
en_mask = Image.new("L", (W, H), 0)
sm_mask = Image.new("L", (W, H), 0)
draw_centered(ar_mask, ARABIC, fit_font(os.path.join(FONTS, "tahomabd.ttf"), ARABIC, W * 0.62, H * 0.40), H * 0.30)
draw_centered(en_mask, ENGLISH, fit_font(os.path.join(FONTS, "ARIALNB.TTF"), ENGLISH, W * 0.80, H * 0.30), H * 0.66)
draw_centered(sm_mask, SMALL, fit_font(os.path.join(FONTS, "arialbd.ttf"), SMALL, W * 0.40, H * 0.075), H * 0.89)
letters = np.maximum(np.maximum(np.asarray(ar_mask), np.asarray(en_mask)), np.asarray(sm_mask)).astype(np.float32) / 255

# ---- panel: deep emerald, slightly lighter in the middle (lit from inside), aluminium trim border
yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
u = xx / (W - 1); v = yy / (H - 1)
rng = np.random.default_rng(7)
noise = np.asarray(Image.fromarray((rng.random((H // 4, W // 4)) * 255).astype(np.uint8)).resize((W, H), Image.BILINEAR)).astype(np.float32) / 255
centre = 1 - 0.55 * ((u - 0.5) ** 2 * 1.6 + (v - 0.5) ** 2 * 1.2)
panel = np.stack([0.015 + 0.02 * centre, 0.20 + 0.10 * centre, 0.11 + 0.05 * centre], -1) * (0.9 + 0.2 * noise[..., None])
# grime towards the bottom edge
panel *= (1 - 0.25 * np.clip((v - 0.75) / 0.25, 0, 1))[..., None]
# trim
b = 10
trim = np.zeros((H, W), bool); trim[:b] = trim[-b:] = True; trim[:, :b] = trim[:, -b:] = True
metal = np.array([0.55, 0.57, 0.56]) * (0.75 + 0.35 * noise[..., None])
img = np.where(trim[..., None], metal, panel)
inner = np.zeros((H, W), bool); inner[b:b + 3] = inner[-b - 3:-b] = True; inner[:, b:b + 3] = inner[:, -b - 3:-b] = True
img = np.where((inner & ~trim)[..., None], img * 0.45, img)

# ---- neon: wide soft emerald halo + tighter bright halo + warm white core
L = Image.fromarray((letters * 255).astype(np.uint8))
halo_wide = np.asarray(L.filter(ImageFilter.GaussianBlur(14))).astype(np.float32) / 255
halo_tight = np.asarray(L.filter(ImageFilter.GaussianBlur(4))).astype(np.float32) / 255
glow_col = np.array([0.25, 1.0, 0.55])
img = img + glow_col * (halo_wide[..., None] * 0.75 + halo_tight[..., None] * 0.9)
core = np.array([0.93, 1.0, 0.92])
img = img * (1 - letters[..., None]) + core * letters[..., None]
img = np.where(trim[..., None], np.clip(metal + glow_col * halo_wide[..., None] * 0.3, 0, 1), img)
img = np.clip(img, 0, 1)
Image.fromarray((img * 255 + 0.5).astype(np.uint8)).save(OUT)
print("saved", OUT)
