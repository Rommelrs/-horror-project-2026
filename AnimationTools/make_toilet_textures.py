# Low-res PSX-style textures for the park public toilet. Built from the PSX texture library (Shamkha Refs/PSX textures/512x512), reduced
# to 128/64 px so they stay chunky, plus a few hand-made ones (vent lattice, signs, wall lettering). Everything is point-filtered in Unity.
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
LIB = "D:/Unity Stuff/Shamkha Refs/PSX textures/512x512/"
OUT = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/Assets/Textures/ParkToilet/"
rng = np.random.default_rng(7)

def lib(cat, n, size):
    im = Image.open(f"{LIB}{cat}/{cat}_{n:02d}-512x512.png").convert("RGB")
    return np.asarray(im.resize((size, size), Image.BOX)).astype(np.float32) / 255.0

def noise(size, cell, seed):
    r = np.random.default_rng(seed)
    g = r.random((size // cell + 2, size // cell + 2)).astype(np.float32)
    im = Image.fromarray((g * 255).astype("uint8")).resize((size + cell * 2, size + cell * 2), Image.BICUBIC)
    return np.asarray(im, dtype=np.float32)[:size, :size] / 255.0

def save(arr, name):
    Image.fromarray((np.clip(arr, 0, 1) * 255).astype("uint8")).save(OUT + name)

# ---- plaster: sun-faded warm grey, with darker chipped-paint patches
S = 128
base = 0.55 * lib("Plaster", 19, S) + 0.45 * lib("Plaster", 10, S)
base = base * np.array([0.92, 0.89, 0.84])
chip = noise(S, 16, 1) * 0.6 + noise(S, 6, 2) * 0.4
mask = (chip > 0.76).astype(np.float32)[..., None]
patch = lib("Plaster", 1, S) * np.array([0.91, 0.88, 0.83])         # exposed grey plaster
plaster = base * (1 - mask) + patch * mask
save(plaster, "Toilet_Plaster.png")

# ---- concrete (base, steps, roof slab)
conc = lib("Plaster", 20, S) * np.array([0.78, 0.75, 0.70])
save(conc, "Toilet_Concrete.png")

# ---- painted metal door: blue-grey, rust coming through
door = lib("Metal", 17, S) * np.array([0.58, 0.72, 0.74])
rust = lib("Metal", 11, S) * np.array([0.95, 0.75, 0.6])
rm = np.clip((noise(S, 20, 3) * 0.6 + noise(S, 7, 4) * 0.4 - 0.55) * 4, 0, 1)[..., None]
door = door * (1 - rm * 0.8) + rust * rm * 0.8
save(door, "Toilet_Door.png")

# ---- rusty fixtures, pipes, tank
S2 = 64
save(lib("Metal", 13, S2) * np.array([0.9, 0.85, 0.8]), "Toilet_Rust.png")
save(lib("Metal", 18, S2) * np.array([0.62, 0.64, 0.62]), "Toilet_Pipe.png")

# ---- vent block: dark openings in a diamond lattice, dusty concrete bars
V = 64
vent = np.zeros((V, V, 3), np.float32)
yy, xx = np.mgrid[0:V, 0:V]
d1 = ((xx + yy) % 16); d2 = ((xx - yy) % 16)
bars = (d1 < 4) | (d2 < 4)
vent[:] = np.array([0.04, 0.035, 0.03])
vent[bars] = np.array([0.55, 0.50, 0.42]) * (0.85 + 0.3 * noise(V, 4, 5)[bars][:, None])
save(vent, "Toilet_Vent.png")

# ---- signs (plate with pictogram + Arabic, using presentation forms so no shaping engine is needed)
font_path = "C:/Windows/Fonts/arialbd.ttf"
def sign(name, glyphs, fig_col, text_col):
    W, H = 128, 48
    im = Image.new("RGB", (W, H), (215, 205, 185)); d = ImageDraw.Draw(im)
    d.rectangle([0, 0, W - 1, H - 1], outline=(110, 100, 88)); d.rectangle([2, 2, W - 3, H - 3], outline=(150, 140, 125))
    # pictogram
    cx = 24
    d.ellipse([cx - 5, 8, cx + 5, 18], fill=fig_col)
    d.rectangle([cx - 7, 20, cx + 7, 32], fill=fig_col)
    d.rectangle([cx - 6, 33, cx - 2, 42], fill=fig_col); d.rectangle([cx + 2, 33, cx + 6, 42], fill=fig_col)
    d.rectangle([cx - 11, 20, cx - 9, 31], fill=fig_col); d.rectangle([cx + 9, 20, cx + 11, 31], fill=fig_col)
    f = ImageFont.truetype(font_path, 30)
    txt = "".join(chr(c) for c in glyphs)
    d.text((82, 24), txt, font=f, fill=text_col, anchor="mm")
    arr = np.asarray(im, dtype=np.float32) / 255.0
    arr = arr * (0.85 + 0.2 * noise(W, 8, 9)[:H, :W, None])          # grime
    save(arr, name)
sign("Toilet_SignMen.png", [0xFEDD, 0xFE8E, 0xFE9F, 0xFEAD], (28, 90, 190), (60, 80, 80))          # رجال
sign("Toilet_SignWomen.png", [0xFE80, 0xFE8E, 0xFEB4, 0xFEE7], (190, 40, 40), (60, 80, 80))         # نساء

# ---- wall lettering "دورة مياه عامة" (RGBA, painted on, faded)
W, H = 256, 128
im = Image.new("RGBA", (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
f = ImageFont.truetype(font_path, 46)
f2 = ImageFont.truetype(font_path, 34)
l1 = "".join(chr(c) for c in [0xFE93, 0xFEAD, 0xFEED, 0xFEA9])                       # دورة
l2 = "".join(chr(c) for c in [0xFEE9, 0xFE8E, 0xFEF4, 0xFEE3]) + " " + "".join(chr(c) for c in [0xFE94, 0xFEE3, 0xFE8E, 0xFECB])   # مياه عامة  (visual order, left to right)
d.text((W // 2, 34), l1, font=f, fill=(70, 88, 78, 255), anchor="mm")
d.text((W // 2, 94), l2, font=f2, fill=(70, 88, 78, 255), anchor="mm")
a = np.asarray(im, dtype=np.float32) / 255.0
fade = 0.55 + 0.45 * noise(W, 5, 11)[:H, :W]
a[..., 3] *= fade * 0.85
Image.fromarray((a * 255).astype("uint8"), "RGBA").save(OUT + "Toilet_WallText.png")
print("done")
