# Generates the sand footprint-trail decal texture (RGBA PNG, 512x2048, 341 px per metre -> 1.5 m x 6 m quad).
# The trail runs along +V (top of the image = far end, the way the prints are heading). Prints are shallow
# depressions in sand: a soft dark core with a faint lighter rim on one side, eroded by noise so it looks wind-worn.
import numpy as np, random
from PIL import Image, ImageDraw, ImageFilter
random.seed(7); np.random.seed(7)
W, H = 512, 2048
PXM = W / 1.5

def footprint(left, wear):
    # one shoe print, ~0.29 m long x 0.11 m wide, toe pointing up
    s = Image.new("L", (96, 160), 0); d = ImageDraw.Draw(s)
    d.ellipse([22, 8, 74, 86], fill=255)        # ball + toes
    d.ellipse([30, 70, 68, 110], fill=255)       # arch bridge
    d.ellipse([27, 98, 69, 150], fill=255)       # heel
    d.rectangle([34, 92, 62, 104], fill=140)     # slight arch narrowing is already implied by overlap
    # wear: eat chunks of the edge so no print is clean
    n = np.random.rand(160, 96)
    n = np.array(Image.fromarray((n * 255).astype("uint8")).filter(ImageFilter.GaussianBlur(5)), dtype=float) / 255.0
    a = np.array(s, dtype=float) / 255.0
    a = a * np.clip((n - 0.5 + wear) * 3.0, 0, 1)
    img = Image.fromarray((a * 255).astype("uint8"))
    if not left: img = img.transpose(Image.FLIP_LEFT_RIGHT)
    return img.rotate(random.uniform(-9, 9), resample=Image.BICUBIC, expand=True)

mask = Image.new("L", (W, H), 0)
n_prints = 8
stride = 0.72 * PXM                      # metres between consecutive prints along the trail
y0 = H - 120
for i in range(n_prints):
    left = (i % 2 == 0)
    wear = 0.12 + 0.30 * (i / (n_prints - 1))      # prints fade out and get more eroded toward the far end
    fp = footprint(left, wear)
    x = W / 2 + (-1 if left else 1) * 0.085 * PXM + random.uniform(-8, 8)
    y = y0 - i * stride - (0 if left else stride * 0.5) + random.uniform(-10, 10)
    layer = Image.new("L", (W, H), 0)
    layer.paste(fp, (int(x - fp.width / 2), int(y - fp.height / 2)))
    fade = 1.0 - 0.75 * (i / (n_prints - 1)) ** 1.3
    arr = np.maximum(np.array(mask, dtype=float), np.array(layer, dtype=float) * fade)
    mask = Image.fromarray(arr.astype("uint8"))

dep = np.array(mask.filter(ImageFilter.GaussianBlur(3.0)), dtype=float) / 255.0          # soft pressed-in core
shift = np.roll(np.roll(dep, -5, axis=0), -5, axis=1)                                      # light from up-left
rim = np.clip(np.array(Image.fromarray((shift * 255).astype("uint8")).filter(ImageFilter.GaussianBlur(2.0)), dtype=float) / 255.0 - dep, 0, 1)
grain = np.random.rand(H, W) * 0.12 + 0.94                                                 # fine sand grain in the alpha

dark = np.array([0.20, 0.145, 0.085]); light = np.array([0.82, 0.70, 0.52])
alpha = np.clip(dep * 0.62 * grain + rim * 0.30, 0, 1)
rgb = dark[None, None, :] * (1 - np.clip(rim * 3.0, 0, 1))[..., None] + light[None, None, :] * np.clip(rim * 3.0, 0, 1)[..., None]
out = np.dstack([rgb, alpha])
out[..., :3] = np.clip(out[..., :3], 0, 1)
Image.fromarray((out * 255).astype("uint8"), "RGBA").save(
    "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/Assets/Decals/Sand/SandFootprintTrail.png")
print("saved")
