# Textures for the wall circuit box + its key.   python make_circuit_textures.py
#   CB_Paint.png     256  weathered grey painted steel (box body + door), tiles every 0.5 m
#   CB_Plate.png     256  galvanised mounting plate inside the box
#   CB_Labels.png    512  atlas: [0,0,256,192] DANGER sign   [256,0,512,128] ID plate   [0,192,256,448] wiring diagram sheet
#                         [256,128,512,256] "authorised only" strip
#   CBKey_Tag.png    128  yellow key tag
import os, sys, random
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
sys.path.insert(0, r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\AnimationTools\ruby_salon")
from arabic_shape import shape

OUT = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\Assets\Models\CircuitBox\Textures"
os.makedirs(OUT, exist_ok=True)
FONTS = r"C:\Windows\Fonts"
rng = np.random.default_rng(7)
random.seed(7)

def font(name, size):
    return ImageFont.truetype(os.path.join(FONTS, name), size)

def noise(n, octaves=4, base=4):
    """tileable value noise 0..1"""
    out = np.zeros((n, n), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        g = base * 2 ** o
        grid = rng.random((g, g)).astype(np.float32)
        img = Image.fromarray((grid * 255).astype(np.uint8)).resize((n, n), Image.BICUBIC)
        out += np.asarray(img, np.float32) / 255 * amp
        tot += amp
        amp *= 0.5
    return out / tot

def save(arr, name):
    Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8)).save(os.path.join(OUT, name))

# ---------------- painted steel
N = 256
n1, n2 = noise(N, 5, 3), noise(N, 3, 8)
base = np.array([156, 160, 154], np.float32)                  # faded grey-green paint
img = np.ones((N, N, 3), np.float32) * base
img *= (0.86 + 0.28 * n1)[..., None]
# vertical dust / rain streaks
streak = noise(N, 2, 24)
cols = (rng.random(N) ** 3)
img *= (1 - 0.18 * (cols[None, :] * np.linspace(0.2, 1, N)[:, None]))[..., None]
# rust blooms: dark orange where noise is high, more toward the bottom of the texture
rust = np.clip((n2 - 0.64) * 4, 0, 1) * np.linspace(0.1, 1, N)[:, None] * 0.75
img = img * (1 - rust[..., None]) + np.array([112, 66, 38], np.float32) * rust[..., None] * (0.7 + 0.5 * n1[..., None])
# scratches
pil = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8))
d = ImageDraw.Draw(pil)
for _ in range(40):
    x, y = random.randrange(N), random.randrange(N)
    l = random.randrange(6, 28)
    a = random.uniform(-0.5, 0.5)
    c = random.choice([(205, 205, 200), (92, 88, 82)])
    d.line([(x, y), (x + l * np.cos(a), y + l * np.sin(a))], fill=c, width=1)
pil = pil.filter(ImageFilter.GaussianBlur(0.4))
pil.save(os.path.join(OUT, "CB_Paint.png"))

# ---------------- galvanised plate
n3 = noise(N, 4, 6)
p = np.ones((N, N, 3), np.float32) * np.array([150, 154, 156], np.float32) * (0.82 + 0.3 * n3)[..., None]
# fine brushed lines
p *= (0.96 + 0.08 * rng.random((N, 1)))[..., None]
pil = Image.fromarray(np.clip(p, 0, 255).astype(np.uint8))
d = ImageDraw.Draw(pil)
for _ in range(14):
    x, y = random.randrange(N), random.randrange(N)
    d.ellipse([x - 2, y - 2, x + 2, y + 2], fill=(70, 72, 74))      # screw / mounting holes
pil.save(os.path.join(OUT, "CB_Plate.png"))

# ---------------- label atlas
A = Image.new("RGB", (512, 512), (90, 90, 90))
d = ImageDraw.Draw(A)

# DANGER sign [0,0,256,192]: black-bordered yellow plate, triangle with a bolt, EN + AR
d.rectangle([0, 0, 255, 191], fill=(20, 20, 20))
d.rectangle([5, 5, 250, 186], fill=(236, 190, 32))
tri = [(82, 20), (30, 112), (134, 112)]
d.polygon([(82, 14), (24, 116), (140, 116)], fill=(20, 20, 20))
d.polygon([(82, 26), (35, 109), (129, 109)], fill=(236, 190, 32))
d.polygon([(86, 44), (68, 84), (80, 84), (74, 106), (98, 70), (85, 70)], fill=(20, 20, 20))   # lightning bolt
f1 = font("arialbd.ttf", 22)
d.text((146, 24), "DANGER", font=f1, fill=(20, 20, 20))
d.text((146, 54), "380 V", font=font("arialbd.ttf", 24), fill=(20, 20, 20))
d.text((146, 84), "LIVE", font=font("arialbd.ttf", 20), fill=(20, 20, 20))
ar = shape("خطر - كهرباء")
fa = font("tahomabd.ttf", 26)
w = d.textlength(ar, font=fa)
d.text((128 - w / 2, 120), ar, font=fa, fill=(20, 20, 20))
d.text((24, 162), "ELECTRICAL  EQUIPMENT", font=font("arialbd.ttf", 15), fill=(20, 20, 20))
# wear on the sign
for _ in range(120):
    x, y = random.randrange(256), random.randrange(192)
    d.point((x, y), fill=(120, 96, 30))

# ID plate [256,0,512,128]
d.rectangle([256, 0, 511, 127], fill=(176, 178, 176))
d.rectangle([262, 6, 505, 121], outline=(50, 50, 50), width=2)
fm = font("courbd.ttf", 24)
d.text((274, 14), "DB-04", font=font("arialbd.ttf", 34), fill=(30, 30, 30))
d.text((274, 58), "400V  3PH  50Hz", font=fm, fill=(40, 40, 40))
d.text((274, 88), "BARBER ST. SUPPLY", font=font("courbd.ttf", 18), fill=(40, 40, 40))
for (x, y) in [(266, 10), (500, 10), (266, 116), (500, 116)]:
    d.ellipse([x - 3, y - 3, x + 3, y + 3], fill=(90, 90, 90))

# wiring diagram sheet [0,192,256,448]
d.rectangle([0, 192, 255, 447], fill=(214, 208, 188))
d.text((10, 198), "SINGLE LINE DIAGRAM", font=font("arialbd.ttf", 17), fill=(40, 40, 60))
d.line([(128, 230), (128, 270)], fill=(50, 50, 80), width=2)
d.rectangle([108, 270, 148, 296], outline=(50, 50, 80), width=2)
d.text((113, 274), "MAIN", font=font("arial.ttf", 14), fill=(50, 50, 80))
d.line([(128, 296), (128, 320)], fill=(50, 50, 80), width=2)
d.line([(40, 320), (216, 320)], fill=(50, 50, 80), width=3)
labels = ["SHOP", "BARBER", "SIGN", "AC", "PUMP", "SPARE"]
for i, lab in enumerate(labels):
    x = 40 + i * 35
    d.line([(x, 320), (x, 346)], fill=(50, 50, 80), width=2)
    d.rectangle([x - 11, 346, x + 11, 368], outline=(50, 50, 80), width=2)
    d.text((x - 12, 374 + (i % 2) * 14), lab, font=font("arial.ttf", 11), fill=(60, 40, 40))
d.text((10, 418), "REV 2 - DO NOT REMOVE", font=font("arial.ttf", 12), fill=(130, 40, 40))
for _ in range(500):                                        # stains
    x, y = random.randrange(256), 192 + random.randrange(256)
    d.point((x, y), fill=(168, 150, 110))

# authorised-only strip [256,128,512,256]
d.rectangle([256, 128, 511, 255], fill=(190, 40, 34))
d.text((270, 140), "AUTHORISED", font=font("arialbd.ttf", 30), fill=(240, 240, 236))
d.text((270, 178), "PERSONNEL ONLY", font=font("arialbd.ttf", 24), fill=(240, 240, 236))
ar2 = shape("للمخولين فقط")
w2 = d.textlength(ar2, font=font("tahomabd.ttf", 24))
d.text((384 - w2 / 2, 214), ar2, font=font("tahomabd.ttf", 24), fill=(240, 240, 236))
A.save(os.path.join(OUT, "CB_Labels.png"))

# ---------------- key tag
T = Image.new("RGB", (128, 128), (214, 168, 30))
d = ImageDraw.Draw(T)
d.rectangle([2, 2, 125, 125], outline=(60, 44, 10), width=3)
d.polygon([(64, 18), (40, 62), (60, 62), (50, 104), (92, 52), (70, 52), (82, 18)], fill=(30, 24, 8))
d.text((8, 104), "DB-04", font=font("arialbd.ttf", 18), fill=(30, 24, 8))
T.save(os.path.join(OUT, "CBKey_Tag.png"))
print("ok", os.listdir(OUT))
