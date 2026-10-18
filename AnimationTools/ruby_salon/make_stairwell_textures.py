# Textures for the stairwell behind the Emerald Salon glass door (reference: grey granite stairs, white walls, steel rail)
# and the Ramadan poster stuck on the glass.   python make_stairwell_textures.py
# Outputs (Assets/Models/RubySalon/):
#   Stair_Granite.png   tileable grey speckled granite (landings, 0.6 m per tile)
#   Stair_Step.png      256x512 atlas: top half = tread (back at the top, bullnose + two grooves at the bottom edge),
#                       bottom half = riser granite. Unity: tread v 0.5 (nose) -> 1.0 (back), riser v 0 -> 0.31
#   Stair_Plaster.png   tileable white wall plaster (0.8 m per tile)
#   EmeraldSalon_Poster.png  600x840 Ramadan timings poster (Emerald version of the reference poster)
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from arabic_shape import shape

OUT = os.path.abspath(os.path.join(HERE, "..", "..", "Assets", "Models", "RubySalon"))
F = r"C:\Windows\Fonts"
rng = np.random.default_rng(11)

def tile_blur(a, s):
    """gaussian blur that wraps around (keeps the texture tileable)"""
    h, w = a.shape[:2]
    big = np.tile(a, (3, 3, 1) if a.ndim == 3 else (3, 3))
    im = Image.fromarray(np.clip(big * 255, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(s))
    return np.asarray(im).astype(np.float32)[h:2 * h, w:2 * w] / 255

def granite(n, base=0.60):
    g = np.full((n, n), base, np.float32)
    g += (tile_blur(rng.random((n, n)).astype(np.float32), 6) - 0.5) * 0.25          # soft cloudiness
    specks = rng.random((n, n))
    g = np.where(specks < 0.10, g - 0.28 * rng.random((n, n)), g)                   # dark mica
    g = np.where(specks > 0.93, g + 0.22 * rng.random((n, n)), g)                   # white quartz
    g = tile_blur(np.clip(g, 0, 1)[..., None].repeat(3, -1), 0.6)[..., 0]
    rgb = np.stack([g * 0.99, g * 1.0, g * 1.03], -1)
    return np.clip(rgb, 0, 1)

def save(a, name):
    Image.fromarray((np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8)).save(os.path.join(OUT, name))
    print("saved", name)

# ---- granite + step atlas
save(granite(256), "Stair_Granite.png")
tread = granite(256, 0.63)
riser = granite(256, 0.57)
rows = np.arange(256)[:, None, None]
# tread: nose at the bottom rows. lighter polished bullnose, then two thin grooves ~4 cm and ~7 cm back from the nose
tread = np.where(rows >= 244, tread * 1.08, tread)
for r0 in (214, 196):
    tread = np.where((rows >= r0) & (rows < r0 + 4), tread * 0.55, tread)
    tread = np.where((rows == r0 + 4), tread * 1.12, tread)                       # tiny highlight under the groove
# grime collects at the back of the tread (top rows) and the riser foot
tread *= (1 - 0.10 * np.clip((40 - rows) / 40, 0, 1))
riser *= (1 - 0.15 * np.clip((rows - 200) / 56, 0, 1))
save(np.concatenate([tread, riser], 0), "Stair_Step.png")

# ---- plaster: near-white, faint mottling
n = 256
p = np.full((n, n), 0.90, np.float32)
p += (tile_blur(rng.random((n, n)).astype(np.float32), 10) - 0.5) * 0.10
p += (tile_blur(rng.random((n, n)).astype(np.float32), 1.2) - 0.5) * 0.05
save(np.stack([p * 0.99, p * 0.99, p * 0.97], -1), "Stair_Plaster.png")

# ---- poster
W, H = 600, 840
im = Image.new("RGB", (W, H), (246, 244, 240))
d = ImageDraw.Draw(im)
# soft paper gradient
A = np.asarray(im).astype(np.float32)
yy = np.arange(H)[:, None, None] / H
A *= (1.0 - 0.06 * yy)
im = Image.fromarray(A.astype(np.uint8)); d = ImageDraw.Draw(im)

def lantern(cx, top, length, w, h, body, glass):
    d.line([(cx, 0), (cx, top + length)], fill=(150, 120, 60), width=3)
    y = top + length
    d.polygon([(cx - w * 0.3, y), (cx + w * 0.3, y), (cx + w * 0.55, y + h * 0.18), (cx - w * 0.55, y + h * 0.18)], fill=body)      # cap
    d.ellipse([cx - 6, y - 10, cx + 6, y + 2], outline=body, width=3)
    yb = y + h * 0.18
    d.polygon([(cx - w * 0.5, yb), (cx + w * 0.5, yb), (cx + w * 0.42, yb + h * 0.55), (cx - w * 0.42, yb + h * 0.55)], fill=glass)  # glass
    for k in (-1, 0, 1):
        d.line([(cx + k * w * 0.22, yb), (cx + k * w * 0.19, yb + h * 0.55)], fill=body, width=4)
    yg = yb + h * 0.55
    d.polygon([(cx - w * 0.42, yg), (cx + w * 0.42, yg), (cx, yg + h * 0.27)], fill=body)                                           # bottom
    d.ellipse([cx - 5, yg + h * 0.25, cx + 5, yg + h * 0.25 + 10], fill=(220, 180, 60))

lantern(70, 0, 170, 62, 150, (40, 70, 150), (255, 230, 150))
lantern(150, 0, 40, 40, 95, (190, 160, 70), (255, 240, 180))
lantern(190, 0, 250, 66, 160, (70, 50, 100), (255, 225, 140))
lantern(240, 0, 110, 58, 140, (200, 95, 40), (255, 228, 150))
# crescent + stars
d.ellipse([20, 110, 60, 150], fill=(230, 190, 60)); d.ellipse([30, 104, 70, 144], fill=(246, 244, 240))
def star(cx, cy, r, col):
    pts = []
    for k in range(10):
        a = -np.pi / 2 + k * np.pi / 5
        rr = r if k % 2 == 0 else r * 0.45
        pts.append((cx + np.cos(a) * rr, cy + np.sin(a) * rr))
    d.polygon(pts, fill=col)
for cx, cy, r, col in [(540, 735, 22, (40, 140, 70)), (480, 760, 11, (60, 110, 200)), (535, 790, 8, (200, 90, 50)),
                       (440, 790, 10, (180, 70, 70)), (120, 770, 18, (230, 190, 50)), (90, 805, 7, (60, 110, 200)),
                       (290, 70, 7, (230, 190, 50)), (110, 330, 7, (230, 190, 50))]:
    star(cx, cy, r, col)

# Emerald Salon pill with the round logo
logo = Image.open(os.path.join(OUT, "EmeraldSalon_Logo.png")).convert("RGB").resize((96, 96), Image.LANCZOS)
mask = Image.new("L", (96, 96), 0); ImageDraw.Draw(mask).ellipse([0, 0, 95, 95], fill=255)
d.rounded_rectangle([330, 130, 580, 200], radius=35, fill=(14, 130, 80))
im.paste(logo, (318, 117), mask); d = ImageDraw.Draw(im)
fa = ImageFont.truetype(os.path.join(F, "tahomabd.ttf"), 23)
fe = ImageFont.truetype(os.path.join(F, "ARLRDBD.TTF"), 19)
d.text((492, 150), shape("صالون الزمرد"), font=fa, fill=(255, 232, 28), anchor="mm")
d.text((492, 181), "EMERALD SALON", font=fe, fill=(255, 232, 28), anchor="mm")

fs = ImageFont.truetype(os.path.join(F, "segoescb.ttf"), 54)
d.text((430, 290), "Ramadan", font=fs, fill=(110, 40, 30), anchor="mm")
d.text((490, 340), "Kareem", font=ImageFont.truetype(os.path.join(F, "segoesc.ttf"), 28), fill=(110, 40, 30), anchor="mm")

far = ImageFont.truetype(os.path.join(F, "tahomabd.ttf"), 30)
d.text((W / 2 + 20, 500), shape("أوقات الدوام في شهر رمضان المبارك"), font=far, fill=(40, 40, 110), anchor="mm")
d.text((W / 2 + 20, 555), shape("من الساعة 8:00 صباحا حتى 3:00 ليلا"), font=far, fill=(40, 40, 110), anchor="mm")
fb = ImageFont.truetype(os.path.join(F, "arialbd.ttf"), 40)
d.text((W / 2 + 20, 625), "RAMADAN TIMINGS", font=fb, fill=(225, 100, 60), anchor="mm")
d.text((W / 2 + 20, 672), "8:00AM - 3:00AM", font=fb, fill=(225, 100, 60), anchor="mm")
d.rectangle([0, 0, W - 1, H - 1], outline=(200, 198, 192), width=4)
im.save(os.path.join(OUT, "EmeraldSalon_Poster.png"))
print("saved EmeraldSalon_Poster.png")
