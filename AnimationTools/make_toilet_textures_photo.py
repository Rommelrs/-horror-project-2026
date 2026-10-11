# Photo-based textures for the park toilet, made from the user's own location photos in "Shamkha Refs":
#   IMG_1973 = close-up of a real stucco wall  -> plaster albedo + normal map
#   IMG_2021 = weathered concrete ground        -> concrete albedo + normal map (footing, steps, roof)
# Each crop is de-lit (divided by a heavy blur so sun gradients/shadows go away), made seamless (offset copy blended in with a patchy
# mask), colour-graded to the target colour, and turned into a tangent-space normal map (OpenGL / Unity convention) from its fine detail.
# The wall overlays are regenerated so the paint chips are filled with the real concrete photo instead of a flat colour.
import numpy as np
from PIL import Image, ImageFilter

REF = "D:/Unity Stuff/Shamkha Refs/"
OUT = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/Assets/Textures/ParkToilet/"
rng = np.random.default_rng(3)


def blur(a, r):
    if a.ndim == 3:
        return np.dstack([blur(a[..., i], r) for i in range(a.shape[2])])
    im = Image.fromarray(np.clip(a * 255, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r))
    return np.asarray(im, dtype=np.float32) / 255.0


def blur_f(a, r):                       # float-precision, wrapping gaussian (keeps tileability)
    n = max(1, int(np.ceil(r * 3)))
    k = np.exp(-0.5 * (np.arange(-n, n + 1) / r) ** 2); k /= k.sum()
    out = np.zeros_like(a)
    for i, w in zip(range(-n, n + 1), k): out += w * np.roll(a, i, 1)
    tmp = out; out = np.zeros_like(a)
    for i, w in zip(range(-n, n + 1), k): out += w * np.roll(tmp, i, 0)
    return out


def load_crop(name, x, y, size, out_size):
    im = Image.open(REF + name).convert("RGB").crop((x, y, x + size, y + size)).resize((out_size, out_size), Image.LANCZOS)
    return np.asarray(im, dtype=np.float32) / 255.0


def delight(a, radius):
    low = blur(a, radius)
    return a / np.maximum(low, 1e-3) * low.reshape(-1, 3).mean(0)


def smooth(t):
    t = np.clip(t, 0, 1); return t * t * (3 - 2 * t)


def seamless(a):
    """Blend in a half-offset copy (whose seams are in the middle) near the borders, with a noisy mask so no straight blend line shows."""
    h, w = a.shape[:2]
    b = np.roll(np.roll(a, h // 2, 0), w // 2, 1)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.minimum(np.minimum(xx, w - 1 - xx) / w, np.minimum(yy, h - 1 - yy) / h)           # 0 at the border, 0.5 in the middle
    nz = blur(rng.random((h // 16, w // 16)).astype(np.float32), 1)
    nz = np.asarray(Image.fromarray((nz * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC), dtype=np.float32) / 255.0
    m = smooth((d - 0.06 + (nz - 0.5) * 0.10) / 0.14)[..., None]                              # 1 = keep original, 0 = offset copy
    out = a * m + b * (1 - m)
    # the blend averages two layers: restore the lost contrast locally
    lo = blur_f(out.mean(2), 12)[..., None]; lo_a = blur_f(a.mean(2), 12)[..., None]
    gain = 1 + 0.35 * (1 - np.abs(2 * m - 1))
    return lo + (out - lo) * gain


def grade(a, target, contrast=1.0):
    mean = a.reshape(-1, 3).mean(0)
    lum = a.mean(2, keepdims=True); lum_mean = lum.mean()
    detail = (lum - lum_mean) * contrast                                                       # keep the photo's luminance detail
    chroma = (a - lum) - (mean - mean.mean())                                                  # keep a little of its colour variation
    return np.clip(np.array(target)[None, None, :] + detail + chroma * 0.5, 0, 1)


def normal_map(a, strength, hp_radius):
    lum = a.mean(2)
    h = lum - blur_f(lum, hp_radius)                                                           # fine relief only
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5
    gy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5
    n = np.dstack([-gx * strength, gy * strength, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return n * 0.5 + 0.5


def save(a, name):
    Image.fromarray((np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8)).save(OUT + name)


# ------------------------------------------------------------------ plaster (stucco wall, IMG_1973, clean sunlit area right of the shadow)
S = 1024
pl = load_crop("IMG_1973.jpg", 3664, 2236, 2048, S)
pl = delight(pl, 90)
pl = seamless(pl)
plaster = grade(pl, (0.79, 0.715, 0.595), contrast=1.15)
# very slight large-scale colour drift so it is not perfectly uniform
drift = blur_f(rng.random((S, S)).astype(np.float32), 120); drift = (drift - drift.mean()) / (drift.std() + 1e-6)
plaster *= (1 + 0.025 * drift)[..., None]
save(plaster, "Toilet_PlasterPhoto.png")
save(normal_map(pl, 9.0, 6), "Toilet_PlasterPhoto_N.png")

# ------------------------------------------------------------------ concrete (IMG_2021, upper part above the feet)
co = load_crop("IMG_2021.jpg", 540, 0, 3200, S)
co = delight(co, 160)
co = seamless(co)
# flatten most of the big dark/light patches: they are what makes the repeat obvious; keep pits, grain and small stains
lo = np.dstack([blur_f(co[..., i], S / 10) for i in range(3)])
co = co - 0.75 * (lo - lo.reshape(-1, 3).mean(0))
concrete = grade(co, (0.70, 0.66, 0.60), contrast=1.0)
save(concrete, "Toilet_ConcretePhoto.png")
save(normal_map(co, 5.0, 5), "Toilet_ConcretePhoto_N.png")
print("photo textures saved")


# ------------------------------------------------------------------ painted steel door (unique, 512 x 1024, v = 0 at the bottom)
# Paint: dull grey-green with the stucco photo's fine relief as orange-peel; rust shapes taken from the concrete photo's dark patches
# (organic, not blobby noise), kept to the bottom, the edges and around the handle; rust runs downward; sandy dust at the very bottom.
def make_door_photo():
    W, H = 512, 1024
    Y, X = np.mgrid[0:H, 0:W].astype(np.float32); v = 1 - Y / (H - 1); u = X / (W - 1)
    peel = np.asarray(Image.fromarray((np.clip(pl, 0, 1) * 255).astype(np.uint8)).resize((W, W), Image.LANCZOS), dtype=np.float32) / 255.0
    peel = np.vstack([peel, peel])[:H]
    pl_l = peel.mean(2); pl_hp = pl_l - blur_f(pl_l, 8)
    paint = np.array([0.37, 0.405, 0.385])[None, None, :] * (1 + 0.9 * pl_hp)[..., None]
    paint *= (1 + 0.03 * blur_f(rng.standard_normal((H, W)).astype(np.float32), 60) * 6)[..., None]
    # recessed panel line
    panel = ((u > 0.09) & (u < 0.91) & (v > 0.09) & (v < 0.91)).astype(np.float32)
    pb = blur_f(panel, 2.0); relief = np.roll(np.roll(pb, 3, 0), 3, 1) - pb
    paint += (relief * 0.06)[..., None]
    # rust mask from the concrete photo's dark patches
    cc = np.asarray(Image.fromarray((np.clip(co, 0, 1) * 255).astype(np.uint8)).resize((W, W), Image.LANCZOS), dtype=np.float32) / 255.0
    cc = np.vstack([cc, np.flipud(cc)])[:H].mean(2)
    dark = (cc.mean() - cc) / (cc.std() + 1e-6)                                    # > 0 where the concrete was darker
    edge_d = np.minimum.reduce([u, 1 - u, v, 1 - v])
    bias = (1 - smooth(v / 0.30)) * 1.6 + (1 - smooth(edge_d / 0.035)) * 1.2 \
        + np.exp(-(((u - 0.82) / 0.07) ** 2 + ((v - 0.48) / 0.08) ** 2)) * 1.0
    rm = smooth((dark + bias - 1.75) / 0.5)
    rust_tex = np.asarray(Image.open("D:/Unity Stuff/Shamkha Refs/PSX textures/512x512/Metal/Metal_13-512x512.png").convert("RGB").resize((W, W)), dtype=np.float32) / 255.0
    rust_tex = np.vstack([rust_tex, rust_tex])[:H]
    rl = rust_tex.mean(2, keepdims=True)
    rust_col = np.array([0.36, 0.22, 0.13])[None, None, :] * (rl / rl.mean()) ** 1.2
    rust_col *= (0.75 + 0.5 * (cc[..., None] - cc.min()) / (cc.max() - cc.min() + 1e-6))
    col = paint * (1 - rm[..., None]) + rust_col * rm[..., None]
    # rust stains running down below the rusty areas
    run = rm.copy()
    for k in range(1, 60, 3): run = np.maximum(run, np.roll(rm, k, 0) * (1 - k / 60) * 0.6)
    run = blur_f(run, 1.5) * (1 - rm)
    col = col * (1 - run[..., None] * 0.35) + np.array([0.34, 0.22, 0.14]) * run[..., None] * 0.35
    # dust at the bottom, slight grime from hands around the handle
    dust = (1 - smooth(v / 0.10)) * 0.45
    col = col * (1 - dust[..., None]) + np.array([0.56, 0.49, 0.39]) * dust[..., None]
    hand = np.exp(-(((u - 0.82) / 0.10) ** 2 + ((v - 0.50) / 0.12) ** 2)) * 0.15
    col *= (1 - hand)[..., None]
    # louvre opening (dark; the slats are geometry)
    hole = smooth((u - 0.26) / 0.02) * smooth((0.74 - u) / 0.02) * smooth((v - 0.10) / 0.01) * smooth((0.245 - v) / 0.01)
    col = col * (1 - hole[..., None] * 0.93) + 0.03 * hole[..., None] * 0.93
    save(col, "Toilet_DoorPhoto.png")
    h = pl_hp * 0.6 + rm * 0.012 * (cc - cc.mean()) * 8
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5; gy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5
    n = np.dstack([-gx * 6, gy * 6, np.ones_like(h)]); n /= np.linalg.norm(n, axis=2, keepdims=True)
    save(n * 0.5 + 0.5, "Toilet_DoorPhoto_N.png")


make_door_photo()
print("door saved")
