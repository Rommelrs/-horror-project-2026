# Textures for Samir's credit book (a Gulf grocery "debt book": a hardback ledger, green book-cloth boards (as in the open-book picture), black cloth
# spine, a white stick-on label written in blue pen) and for the delivery map's notes-tab sheet.
#   python AnimationTools/make_credit_book.py
# -> Assets/Textures/Props/credit_book_cover.png (front board, UV 0..1 over the face, worn at the edges + corners)
#    Assets/Textures/Props/credit_book_spine.png, credit_book_pages.png
import numpy as np, os
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import arabic_reshaper
from bidi.algorithm import get_display

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
OUT = os.path.join(ROOT, "Assets", "Textures", "Props")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(417)


def fbm(w, h, scale, octaves=4, seed=0):
    r = np.random.default_rng(seed)
    out = np.zeros((h, w), np.float32); amp = 1.0; tot = 0.0
    for o in range(octaves):
        gw = max(2, int(w / scale * 2 ** o) + 1); gh = max(2, int(h / scale * 2 ** o) + 1)
        g = (r.random((gh, gw)) * 255).astype(np.uint8)
        out += np.asarray(Image.fromarray(g).resize((w, h), Image.BICUBIC), np.float32) / 255 * amp
        tot += amp; amp *= 0.5
    return out / tot


def cloth(w, h, base, seed, weave=2):
    """book cloth: a fine linen weave, mottled dye, rubbed lighter where hands go"""
    yy, xx = np.mgrid[0:h, 0:w]
    wv = (np.sin(xx * np.pi / weave) * np.sin(yy * np.pi / weave))
    thread = 0.92 + 0.06 * wv + 0.05 * (rng.random((h, w)) - 0.5)
    mott = 0.85 + 0.3 * fbm(w, h, 120, 4, seed)
    img = np.ones((h, w, 3), np.float32) * np.array(base, np.float32)
    img *= (thread * mott)[..., None]
    return img, xx, yy


def wear(img, xx, yy, w, h, seed, strength=1.0, board=(0.42, 0.36, 0.3)):
    """edges + corners rubbed through to the grey board under the cloth; scuffs; a grease patch at the opening edge"""
    d = np.minimum(np.minimum(xx, w - 1 - xx), np.minimum(yy, h - 1 - yy)).astype(np.float32) / min(w, h)
    n = fbm(w, h, 18, 3, seed)
    edge = np.clip((0.012 + 0.02 * n - d) / 0.014, 0, 1) * strength
    # corners worn round and fuzzy
    for cx, cy in ((0, 0), (w, 0), (0, h), (w, h)):
        r = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / min(w, h)
        edge = np.maximum(edge, np.clip((0.05 + 0.03 * n - r) / 0.03, 0, 1) * strength)
    rub = np.clip(edge * 1.2, 0, 1)[..., None]
    lighter = np.clip(img * 1.35 + 0.05, 0, 1)
    img = img * (1 - rub * 0.6) + lighter * rub * 0.6
    expose = np.clip(edge - 0.7, 0, 1)[..., None] * 3.0
    img = img * (1 - expose) + np.array(board, np.float32) * (0.8 + 0.4 * n)[..., None] * expose
    # scuffs and handling grime
    sc = (fbm(w, h, 6, 2, seed + 1) > 0.66).astype(np.float32) * (fbm(w, h, 80, 2, seed + 2) > 0.5)
    img = img * (1 - 0.18 * np.asarray(Image.fromarray((sc * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8)), np.float32)[..., None] / 255)
    grime = np.clip(1 - xx / (w * 0.35), 0, 1) * 0 + np.clip((xx - w * 0.7) / (w * 0.3), 0, 1) * (0.5 + 0.5 * fbm(w, h, 60, 3, seed + 3))
    img *= (1 - 0.22 * grime)[..., None]
    return img


def to_img(a):
    return Image.fromarray(np.clip(a * 255, 0, 255).astype(np.uint8))


def ar(text):
    return get_display(arabic_reshaper.reshape(text))


# ---------------- front cover (portrait 170 x 240 mm -> 680 x 960)
W, H = 680, 960
img, xx, yy = cloth(W, H, (0.12, 0.21, 0.13), 11)
img = wear(img, xx, yy, W, H, 12)
cover = to_img(img)
d = ImageDraw.Draw(cover)
# a blind-stamped frame line, as these ledgers have
for i, a in ((36, 70), (44, 40)):
    d.rectangle([i, i, W - i, H - i], outline=(10, a, 22), width=3)
# the white label, stuck on slightly crooked, gone yellow, edges lifting
lab = Image.new("RGBA", (330, 210), (236, 228, 205, 255))
ld = ImageDraw.Draw(lab)
ld.rectangle([6, 6, 323, 203], outline=(170, 40, 40, 255), width=3)
ld.line([20, 70, 310, 70], fill=(170, 40, 40, 255), width=2)
for y in (115, 160):
    ld.line([20, y, 310, y], fill=(150, 160, 190, 255), width=1)
fb = ImageFont.truetype("C:/Windows/Fonts/majalla.ttf", 52)
fp = ImageFont.truetype("C:/Windows/Fonts/majalla.ttf", 58)
ld.text((165, 38), ar("حسابات"), font=fb, fill=(150, 30, 30, 255), anchor="mm")
ld.text((165, 108), ar("الديون"), font=fp, fill=(30, 45, 140, 255), anchor="mm")       # blue pen
ld.text((165, 156), ar("بقالة سمير"), font=ImageFont.truetype("C:/Windows/Fonts/majalla.ttf", 44), fill=(30, 45, 140, 255), anchor="mm")
la = np.asarray(lab, np.float32) / 255
lh, lw = la.shape[:2]
ly, lx = np.mgrid[0:lh, 0:lw]
n = fbm(lw, lh, 40, 3, 13)
la[..., :3] *= (0.88 + 0.12 * n)[..., None]
dd = np.minimum(np.minimum(lx, lw - 1 - lx), np.minimum(ly, lh - 1 - ly))
la[..., :3] *= np.clip(0.75 + dd / 30, 0.75, 1)[..., None]              # dirty edges
lab = Image.fromarray((la * 255).astype(np.uint8)).rotate(-2.2, resample=Image.BICUBIC, expand=True)
shadow = Image.new("RGBA", lab.size, (0, 0, 0, 0)); shadow.paste((0, 0, 0, 90), mask=lab.split()[3])
shadow = shadow.filter(ImageFilter.GaussianBlur(3))
cover.paste(shadow, (W // 2 - lab.size[0] // 2 + 3, 200 + 4), shadow)
cover.paste(lab, (W // 2 - lab.size[0] // 2, 200), lab)
# a ring stain from a tea glass
c = np.asarray(cover, np.float32) / 255
r = np.sqrt((xx - 470) ** 2 + (yy - 720) ** 2)
ring = np.exp(-((r - 95) / 5) ** 2) * (0.6 + 0.4 * fbm(W, H, 20, 2, 14))
c *= (1 - 0.25 * ring)[..., None]
to_img(c).save(os.path.join(OUT, "credit_book_cover.png"))

# ---------------- back cover: same cloth, no label
img, xx, yy = cloth(W, H, (0.12, 0.21, 0.13), 21)
img = wear(img, xx, yy, W, H, 22)
to_img(img).save(os.path.join(OUT, "credit_book_back.png"))

# ---------------- spine: black cloth tape, scuffed grey along the hinges (u = round the spine, v = along it)
W2, H2 = 256, 960
img, xx, yy = cloth(W2, H2, (0.06, 0.06, 0.065), 31)
hinge = np.exp(-(((xx - 0) / 26) ** 2)) + np.exp(-(((xx - W2) / 26) ** 2))
img = img * (1 - 0.0) + np.array((0.32, 0.31, 0.3), np.float32) * (hinge * fbm(W2, H2, 10, 2, 32))[..., None] * 0.7
ends = np.exp(-(yy / 30.0)) + np.exp(-((H2 - yy) / 30.0))
img = img + np.array((0.25, 0.22, 0.2), np.float32) * (ends * fbm(W2, H2, 8, 2, 33))[..., None] * 0.8
to_img(img).save(os.path.join(OUT, "credit_book_spine.png"))

# ---------------- page edges (fore-edge / head / tail): stacked sheets, grubby where thumbs go
W3, H3 = 512, 128
yy, xx = np.mgrid[0:H3, 0:W3]
lines = 0.86 + 0.1 * np.sin(yy * np.pi / 1.3 + fbm(W3, H3, 40, 2, 41) * 6)
paper = np.ones((H3, W3, 3), np.float32) * np.array((0.86, 0.83, 0.74), np.float32) * lines[..., None]
paper *= (0.9 + 0.12 * fbm(W3, H3, 50, 3, 42))[..., None]
thumb = np.exp(-(((xx - W3 * 0.78) / 60) ** 2)) * 0.25
paper *= (1 - thumb)[..., None]
to_img(paper).save(os.path.join(OUT, "credit_book_pages.png"))
print("written to", OUT)
