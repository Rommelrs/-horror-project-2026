# Emerald Salon round logo for the lamp on the sign (modelled on the Ruby Salon "Happy & Ruby Group" roundel).
#   python make_emerald_logo.py   ->  Assets/Models/RubySalon/EmeraldSalon_Logo.png (1024x1024, disc fills the square)
# Layout: emerald disc, white inner ring, yellow Arabic arc on top (tops outward), yellow English arc at the bottom
# (tops inward), blue ornate oval in the middle with a white "ES" monogram, ESTD 1983 and a ribbon.
# Arabic is hand-shaped with Presentation Forms-B; curved text is drawn flat and polar-warped so the joins stay intact.
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "Assets", "Models", "RubySalon", "EmeraldSalon_Logo.png"))
F = r"C:\Windows\Fonts"
N = 2048                       # work size (downsampled to 1024 at the end)
C = N / 2
R = N / 2 - 4

EMERALD = (14, 150, 92)
EMERALD_DK = (6, 96, 58)
YELLOW = (255, 232, 28)
WHITE = (250, 250, 246)
BLUE = (38, 52, 150)
OUTLINE = (20, 30, 60)

def visual(forms):
    return "".join(reversed(forms))
# مجموعة مؤسسات المسرور و الزمرد
AR_RING = visual(["\uFEE3", "\uFEA0", "\uFEE4", "\uFEEE", "\uFECB", "\uFE94", " ",
                  "\uFEE3", "\uFE86", "\uFEB3", "\uFEB4", "\uFE8E", "\uFE95", " ",
                  "\uFE8D", "\uFEDF", "\uFEE4", "\uFEB4", "\uFEAE", "\uFEED", "\uFEAD", " ",
                  "\uFEED", " ",
                  "\uFE8D", "\uFEDF", "\uFEB0", "\uFEE3", "\uFEAE", "\uFEA9"])
EN_RING = "HAPPY & EMERALD GROUP"

img = Image.new("RGB", (N, N), (0, 0, 0))
d = ImageDraw.Draw(img)
def circ(r, **kw):
    d.ellipse([C - r, C - r, C + r, C + r], **kw)

# ---- disc + rings
circ(R, fill=WHITE)
circ(R * 0.965, fill=EMERALD)
# subtle radial shading (lightbox: brighter centre)
yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
rr = np.hypot(xx - C, yy - C) / R
A = np.asarray(img).astype(np.float32)
shade = (1.08 - 0.18 * rr ** 2)[..., None]
inside = (rr < 0.965)[..., None]
A = np.where(inside, A * shade, A)
img = Image.fromarray(np.clip(A, 0, 255).astype(np.uint8)); d = ImageDraw.Draw(img)
circ(R * 0.655, outline=WHITE, width=int(R * 0.022))

# ---- arc text (flat strip -> polar warp)
R_OUT_TXT, R_IN_TXT = R * 0.925, R * 0.69
def strip(text, font, size, height):
    f = ImageFont.truetype(os.path.join(F, font), size)
    l, t, r_, b_ = f.getbbox(text)
    im = Image.new("L", (r_ - l + 40, height), 0)
    ImageDraw.Draw(im).text((20 - l, (height - (b_ - t)) / 2 - t), text, font=f, fill=255)
    return np.asarray(im).astype(np.float32) / 255

def arc(mask_strip, top, span_deg):
    h, w = mask_strip.shape
    rmid = (R_OUT_TXT + R_IN_TXT) / 2
    # scale the strip so it covers span_deg along the mid radius and the ring height radially
    sx = w / (np.radians(span_deg) * rmid)
    sy = h / (R_OUT_TXT - R_IN_TXT)
    dx, dy = xx - C, yy - C
    r = np.hypot(dx, dy)
    if top:
        th = np.arctan2(dx, -dy)                       # 0 at the top, + to the right
        ys = (R_OUT_TXT - r) * sy                      # letter tops at the outer radius
    else:
        th = np.arctan2(dx, dy)                        # 0 at the bottom, + to the right
        ys = (r - R_IN_TXT) * sy                       # letter tops at the inner radius
    xs = th * rmid * sx + w / 2
    ok = (xs >= 0) & (xs < w - 1) & (ys >= 0) & (ys < h - 1)
    xi, yi = np.clip(xs.astype(int), 0, w - 2), np.clip(ys.astype(int), 0, h - 2)
    fx, fy = xs - xi, ys - yi
    v = (mask_strip[yi, xi] * (1 - fx) * (1 - fy) + mask_strip[yi, xi + 1] * fx * (1 - fy)
         + mask_strip[yi + 1, xi] * (1 - fx) * fy + mask_strip[yi + 1, xi + 1] * fx * fy)
    return np.where(ok, v, 0)

ar = arc(strip(AR_RING, "tahomabd.ttf", 260, 300), True, 188)
en = arc(strip(EN_RING, "COOPBL.TTF", 230, 260), False, 140)
A = np.asarray(img).astype(np.float32)
for m in (ar, en):
    sh = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(9))).astype(np.float32) / 255
    A = A * (1 - sh[..., None] * 0.55) + np.array(EMERALD_DK) * sh[..., None] * 0.55     # dark outline
    A = A * (1 - m[..., None]) + np.array(YELLOW) * m[..., None]
# separators between the two arcs (white dots at the sides)
img = Image.fromarray(np.clip(A, 0, 255).astype(np.uint8)); d = ImageDraw.Draw(img)
for sgn in (-1, 1):
    ang = np.radians(90 + 13) * sgn
    px, py = C + np.sin(ang) * (R_OUT_TXT + R_IN_TXT) / 2, C - np.cos(ang) * (R_OUT_TXT + R_IN_TXT) / 2
    d.ellipse([px - 22, py - 22, px + 22, py + 22], fill=WHITE)

# ---- centre emblem: ornate oval mirror frame
ox, oy, oa, ob = C, C - R * 0.03, R * 0.30, R * 0.40
def ell(cx, cy, a, b, **kw):
    d.ellipse([cx - a, cy - b, cx + a, cy + b], **kw)
# scroll ornaments (behind the frame)
for sgn in (-1, 1):
    ell(ox + sgn * oa * 1.05, oy + ob * 0.55, oa * 0.30, ob * 0.16, fill=WHITE, outline=OUTLINE, width=10)
    ell(ox + sgn * oa * 1.12, oy + ob * 0.50, oa * 0.13, ob * 0.07, fill=BLUE)
    ell(ox + sgn * oa * 0.95, oy - ob * 0.55, oa * 0.18, ob * 0.10, fill=WHITE, outline=OUTLINE, width=10)
# crown on top
for k, (dxk, rk) in enumerate([(0, 0.15), (-0.16, 0.09), (0.16, 0.09)]):
    ell(ox + dxk * oa * 2, oy - ob * 1.02 + abs(dxk) * ob * 0.35, oa * rk, oa * rk, fill=WHITE, outline=OUTLINE, width=10)
ell(ox, oy - ob * 1.02, oa * 0.07, oa * 0.07, fill=BLUE)
# frame + oval
ell(ox, oy, oa * 1.13, ob * 1.10, fill=WHITE, outline=OUTLINE, width=12)
ell(ox, oy, oa * 1.03, ob * 1.01, fill=(205, 210, 220), outline=OUTLINE, width=6)
ell(ox, oy, oa * 0.95, ob * 0.94, fill=BLUE)
ell(ox, oy, oa * 0.86, ob * 0.86, outline=(200, 215, 255), width=6)

# monogram
mono = Image.new("L", (N, N), 0)
md = ImageDraw.Draw(mono)
f = ImageFont.truetype(os.path.join(F, "segoescb.ttf"), int(R * 0.52))
l, t, r_, b_ = f.getbbox("ES")
md.text((ox - (r_ - l) / 2 - l, oy - ob * 0.12 - (b_ - t) / 2 - t), "ES", font=f, fill=255)
mono = mono.rotate(-8, center=(ox, oy), resample=Image.BICUBIC)
mo = np.asarray(mono).astype(np.float32)[..., None] / 255
mo_out = np.asarray(mono.filter(ImageFilter.MaxFilter(15))).astype(np.float32)[..., None] / 255
A = np.asarray(img).astype(np.float32)
A = A * (1 - mo_out) + np.array(OUTLINE) * mo_out
A = A * (1 - mo) + np.array(WHITE) * mo
img = Image.fromarray(np.clip(A, 0, 255).astype(np.uint8)); d = ImageDraw.Draw(img)

# ESTD + ribbon
fs = ImageFont.truetype(os.path.join(F, "arialbd.ttf"), 46)
d.text((ox, oy + ob * 0.55), "ESTD. 1983", font=fs, fill=WHITE, anchor="mm")
rw, rh, ry = oa * 1.45, ob * 0.17, oy + ob * 0.86
d.polygon([(ox - rw, ry - rh), (ox + rw, ry - rh), (ox + rw * 0.92, ry), (ox + rw, ry + rh), (ox - rw, ry + rh), (ox - rw * 0.92, ry)],
          fill=WHITE, outline=OUTLINE, width=10)
fr = ImageFont.truetype(os.path.join(F, "COOPBL.TTF"), 54)
d.text((ox, ry), "HAPPY & EMERALD GROUP", font=fr, fill=BLUE, anchor="mm")
d.text((ox + oa * 1.25, oy + ob * 0.35), "TM", font=ImageFont.truetype(os.path.join(F, "arialbd.ttf"), 40), fill=YELLOW, anchor="mm")

# ---- outside the disc: black (hidden on the round lamp anyway)
A = np.asarray(img).astype(np.float32)
A[rr > 1.0] = 0
out = Image.fromarray(np.clip(A, 0, 255).astype(np.uint8)).resize((1024, 1024), Image.LANCZOS)
out.save(OUT)
print("saved", OUT)
