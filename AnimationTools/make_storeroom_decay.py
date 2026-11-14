# Storage-room decay textures, built from the user's own location photos (Shamkha Refs):
#   IMG_2016 = peeling cream wall with grey cement showing at the base (rectified) -> the paint, its crazing and stains
#   IMG_2021 = weathered concrete ground -> the floor
#   Toilet_ConcretePhoto (already photo based) -> the exposed cement
# Writes Assets/Textures/StoreMakeover/sr_*.png.  Positions (metres) match StoreFittingsDecay.cs.
import numpy as np, math, sys, os
from PIL import Image, ImageFilter, ImageDraw

ROOT = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/"
OUT = ROOT + "Assets/Textures/StoreMakeover/"
REFS = "D:/Unity Stuff/Shamkha Refs/"
HERE = os.path.dirname(os.path.abspath(__file__)) + "/"
PPM = 300                      # wall pixels per metre
WALL_H = 3.555
rng = np.random.default_rng(417)


def fbm(w, h, scale, octaves=5, seed=None, persistence=0.5):
    r = np.random.default_rng(seed) if seed is not None else rng
    out = np.zeros((h, w), np.float32); amp = 1.0; tot = 0.0
    for o in range(octaves):
        gw = max(2, int(round(w / scale * 2 ** o)) + 1); gh = max(2, int(round(h / scale * 2 ** o)) + 1)
        g = (r.random((gh, gw)) * 255).astype(np.uint8)
        out += np.asarray(Image.fromarray(g).resize((w, h), Image.BICUBIC), np.float32) / 255.0 * amp
        tot += amp; amp *= persistence
    return out / tot


def blur(a, radius):
    if a.ndim == 2:
        return np.asarray(Image.fromarray(np.clip(a * 255, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(radius)), np.float32) / 255.0
    return np.dstack([blur(a[..., i], radius) for i in range(a.shape[2])])


def to_img(a):
    return Image.fromarray(np.clip(a * 255, 0, 255).astype(np.uint8))


def load(path, size=None):
    im = Image.open(path).convert("RGB")
    if size: im = im.resize(size, Image.LANCZOS)
    return np.asarray(im, np.float32) / 255.0


def flatten(a, radius, keep=0.0):
    """remove the photo's own lighting (divide by its blur), keep a share of it for natural large stains"""
    low = np.maximum(blur(a, radius), 0.02)
    mean = a.reshape(-1, 3).mean(0)
    flat = a / low * mean
    return np.clip(flat * (1 - keep) + a * keep, 0, 1)


def bomb(src, w, h, patch=(300, 230), seed=1):
    """cover a canvas with overlapping feathered crops of a photo (random place / flip): no visible repeat"""
    r = np.random.default_rng(seed)
    acc = np.zeros((h, w, 3), np.float32); wsum = np.zeros((h, w), np.float32)
    sh, sw = src.shape[:2]
    pw, ph = patch
    yy, xx = np.mgrid[0:ph, 0:pw].astype(np.float32)
    fx = np.minimum(xx, pw - 1 - xx) / (pw * 0.5); fy = np.minimum(yy, ph - 1 - yy) / (ph * 0.5)
    feather = np.clip(np.minimum(fx, fy) * 2.2, 0, 1) ** 2 + 1e-4
    step_x, step_y = int(pw * 0.5), int(ph * 0.5)
    for by in range(-ph // 2, h, step_y):
        for bx in range(-pw // 2, w, step_x):
            dx = bx + int(r.integers(-step_x // 3, step_x // 3 + 1)); dy = by + int(r.integers(-step_y // 3, step_y // 3 + 1))
            sx = int(r.integers(0, sw - pw)); sy = int(r.integers(0, sh - ph))
            p = src[sy:sy + ph, sx:sx + pw]
            if r.random() < 0.5: p = p[:, ::-1]
            wgt = feather * (0.6 + 0.8 * r.random())
            x0, y0 = max(0, dx), max(0, dy); x1, y1 = min(w, dx + pw), min(h, dy + ph)
            if x1 <= x0 or y1 <= y0: continue
            ps = p[y0 - dy:y1 - dy, x0 - dx:x1 - dx]; ws = wgt[y0 - dy:y1 - dy, x0 - dx:x1 - dx] ** 3
            acc[y0:y1, x0:x1] += ps * ws[..., None]; wsum[y0:y1, x0:x1] += ws
    return acc / np.maximum(wsum, 1e-6)[..., None]


def grade(a, target_mean, desat=0.0, contrast=1.0):
    lum = a.mean(2, keepdims=True)
    a = a * (1 - desat) + lum * desat
    m = a.reshape(-1, 3).mean(0)
    a = (a - m) * contrast + m
    return np.clip(a * (np.array(target_mean, np.float32) / np.maximum(m, 1e-3)), 0, 1)


# ------------------------------------------------------------------ photo sources
print("sources")
quad = (60, 640, 120, 2720, 1120, 2040, 960, 900)
rect = Image.open(REFS + "IMG_2016.jpg").convert("RGB").transform((1500, 1150), Image.QUAD, quad, Image.BICUBIC)
rect = np.asarray(rect, np.float32) / 255.0
paint_src = rect[385:800, 190:1000]                                  # painted area only (below the big diagonal stain, left of the white patch)
paint_src = flatten(paint_src, 70, keep=0.18)
k = PPM / 525.0
paint_src = np.asarray(to_img(paint_src).resize((int(paint_src.shape[1] * k), int(paint_src.shape[0] * k)), Image.LANCZOS), np.float32) / 255.0
# nail holes / dark specks of the photo would repeat all over the wall: paint them out
_low = blur(paint_src, 5)
_spot = blur(((_low.mean(2) - paint_src.mean(2)) > 0.075).astype(np.float32), 2.0)
_spot = np.clip(_spot * 4, 0, 1)[..., None]
paint_src = paint_src * (1 - _spot) + blur(paint_src, 9) * _spot
paint_src = grade(paint_src, (0.71, 0.675, 0.60), desat=0.35, contrast=1.25)

cem = load(ROOT + "Assets/Textures/ParkToilet/Toilet_ConcretePhoto.png", (640, 640))
cem = grade(np.tile(cem, (2, 2, 1)), (0.43, 0.425, 0.41), desat=0.8, contrast=1.35)

floor_src = Image.open(REFS + "IMG_2021.jpg").convert("RGB").crop((300, 300, 3900, 3300))
floor_src = np.asarray(floor_src.resize((900, 750), Image.LANCZOS), np.float32) / 255.0
floor_src = grade(flatten(floor_src, 60, keep=0.25), (0.44, 0.435, 0.415), desat=0.75, contrast=1.3)


# ------------------------------------------------------------------ walls
def wall(name, length, patches, drips, rust, seed, base=0.42, top=0.10, extra=None):
    """patches: (u, v, ru, rv, strength) exposed cement blobs; drips: (u, v_top, length, width, strength) brown runs;
    rust: (u, v) bracket points. u from the viewer's left end, v up from the floor (metres)."""
    W, H = int(round(length * PPM)), int(round(WALL_H * PPM))
    print(name, W, H)
    paint = bomb(paint_src, W, H, seed=seed)
    cement = np.tile(cem, (H // cem.shape[0] + 1, W // cem.shape[1] + 1, 1))[:H, :W]
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    um = xx / PPM; vm = (H - 1 - yy) / PPM
    f = fbm(W, H, 230, 6, seed + 1) * 0.7 + fbm(W, H, 45, 3, seed + 2) * 0.3
    bias = base * np.exp(-vm / 0.33) + top * np.exp(-(WALL_H - vm) / 0.16)
    for (u, v, ru, rv, s) in patches:
        bias += s * np.exp(-(((um - u) / ru) ** 2 + ((vm - v) / rv) ** 2))
    val = f + bias
    m = 1.0 / (1.0 + np.exp(-(val - 0.695) * 70))                    # exposed cement
    skim = 1.0 / (1.0 + np.exp(-(val - 0.635) * 55))                  # the paler layer under the paint, around it
    # paint flaking in small scabs near the edges
    flakes = (fbm(W, H, 14, 2, seed + 3) > 0.62) & (val > 0.595)
    skim = np.maximum(skim, blur(flakes.astype(np.float32), 0.7) * 0.8)

    under = grade(paint, (0.80, 0.785, 0.74), desat=0.55, contrast=0.6)
    img = paint * (1 - skim[..., None]) + under * skim[..., None]
    damp = np.clip(1.0 - vm / 0.5, 0, 1)[..., None]
    cement_d = cement * (1 - 0.22 * damp) * (0.9 + 0.2 * fbm(W, H, 120, 3, seed + 4))[..., None]
    img = img * (1 - m[..., None]) + cement_d * m[..., None]
    # thickness of the paint/plaster: shadow under its upper lip, a light edge on its lower lip
    up = np.roll(m, 4, axis=0); up[:4] = m[:4]
    lip = np.clip(m - up, 0, 1)                                       # cement here, paint just above
    img *= (1 - 0.6 * blur(lip, 1.2))[..., None]
    dn = np.roll(m, -3, axis=0); dn[-3:] = m[-3:]
    img *= (1 + 0.16 * blur(np.clip(m - dn, 0, 1), 1.0))[..., None]
    side = np.clip(m - np.roll(m, 3, axis=1), 0, 1)
    img *= (1 - 0.3 * blur(side, 1.0))[..., None]

    D = np.ones((H, W, 3), np.float32)

    def stain(alpha, colour):
        nonlocal D
        D *= 1 - alpha[..., None] * (1 - np.array(colour, np.float32))

    # dirt: grime that settles low, dust line at the top, corners, uneven all over
    stain(np.clip(np.exp(-vm / 0.28) * (0.5 + 0.6 * fbm(W, H, 90, 3, seed + 5)), 0, 1) * 0.55, (0.62, 0.57, 0.5))
    stain(np.exp(-(WALL_H - vm) / 0.2) * 0.5, (0.55, 0.52, 0.47))
    stain((np.exp(-um / 0.22) + np.exp(-(length - um) / 0.22)) * 0.45, (0.55, 0.53, 0.5))
    stain(np.clip(fbm(W, H, 420, 4, seed + 6) - 0.45, 0, 1) * 1.1, (0.74, 0.69, 0.6))
    # water that ran down from above (brown tide lines)
    streak = fbm(W, 8, 9, 2, seed + 7)[0]
    streaks = np.clip(streak[None, :] - 0.58, 0, 1) * 4.0 * np.clip(1 - (WALL_H - vm) / (0.5 + 1.4 * fbm(W, 8, 60, 2, seed + 8)[0][None, :]), 0, 1)
    stain(np.clip(blur(streaks, 2.5), 0, 1) * 0.4, (0.62, 0.53, 0.42))
    for (u, v, ln, wd, s) in drips:
        wob = (fbm(8, H, 70, 2, seed + int(u * 100))[:, 0][:, None] - 0.5) * 0.06
        prof = np.exp(-(((um - u - wob) / wd) ** 2))
        ramp = np.clip((v - vm) / 0.05, 0, 1) * np.clip(1 - (v - vm) / ln, 0, 1) ** 0.7
        tex = 0.55 + 0.45 * fbm(W, H, 16, 2, seed + 9)
        stain(np.clip(prof * ramp * tex * s, 0, 1), (0.47, 0.38, 0.27))
        stain(np.clip(np.exp(-(((um - u - wob) / (wd * 0.35)) ** 2)) * ramp * s * 0.8, 0, 1), (0.33, 0.29, 0.23))
    for (u, v) in rust:
        halo = np.exp(-(((um - u) / 0.06) ** 2 + ((vm - v) / 0.06) ** 2))
        run = np.exp(-(((um - u) / 0.022) ** 2)) * np.clip((v - vm) / 0.02, 0, 1) * np.clip(1 - (v - vm) / 0.55, 0, 1)
        stain(np.clip(halo * 0.8 + run * 0.75, 0, 1), (0.6, 0.38, 0.22))
    # mould where it stays damp
    mould = (fbm(W, H, 9, 3, seed + 10) > 0.6).astype(np.float32) * np.clip(bias * 2.2 - 0.25, 0, 1) * (1 - m * 0.5)
    stain(np.clip(blur(mould, 0.9), 0, 1) * 0.7, (0.3, 0.33, 0.27))
    if extra: extra(stain, um, vm, W, H)
    img = np.clip(img * D, 0, 1)
    to_img(img).save(OUT + name + ".png")
    return img


# positions: see StoreFittingsDecay.cs
SOUTH = dict(length=10.42,
             patches=[(3.1, 2.75, 0.75, 0.55, 0.36), (3.05, 1.1, 0.4, 0.9, 0.22), (0.35, 1.6, 0.35, 1.2, 0.2), (7.8, 3.0, 0.9, 0.35, 0.22), (5.6, 0.2, 0.5, 0.5, 0.2)],
             drips=[(3.1, 3.2, 3.2, 0.07, 0.95), (3.22, 3.18, 2.3, 0.035, 0.7), (2.95, 3.2, 1.5, 0.03, 0.6), (0.55, 3.25, 1.4, 0.05, 0.5), (8.86, 3.4, 0.9, 0.06, 0.5)],
             rust=[(1.3, 3.36), (3.8, 3.36), (5.6, 3.36), (7.0, 3.36), (8.2, 3.36), (0.2, 2.6), (0.2, 1.5), (4.71, 3.2)])
EAST = dict(length=7.56,
            patches=[(1.4, 1.3, 0.75, 0.8, 0.42), (5.72, 0.6, 0.38, 0.4, 0.4), (6.6, 0.25, 0.9, 0.5, 0.22), (3.6, 3.2, 1.2, 0.3, 0.2), (7.3, 2.2, 0.3, 1.0, 0.16)],
             drips=[(5.72, 0.55, 0.55, 0.05, 0.8), (6.02, 0.5, 0.5, 0.03, 0.7), (1.2, 3.5, 2.2, 0.06, 0.6), (1.75, 3.5, 1.6, 0.04, 0.55)],
             rust=[(6.3, 0.6), (7.0, 0.6), (5.9, 0.6)])
NORTH = dict(length=10.42,
             patches=[(1.2, 3.1, 0.8, 0.4, 0.26), (6.4, 3.25, 1.3, 0.3, 0.24), (9.7, 2.9, 0.6, 0.6, 0.28), (3.8, 3.3, 0.5, 0.25, 0.2)],
             drips=[(6.1, 3.5, 1.2, 0.05, 0.6), (6.9, 3.5, 0.8, 0.035, 0.5), (9.9, 3.5, 2.6, 0.06, 0.7), (1.0, 3.5, 0.9, 0.05, 0.5)],
             rust=[])
WEST = dict(length=7.56,
            patches=[(0.5, 2.9, 0.5, 0.6, 0.26), (4.2, 3.2, 1.0, 0.3, 0.22), (7.1, 1.2, 0.4, 1.0, 0.2)],
            drips=[(0.4, 3.5, 2.0, 0.06, 0.6), (4.4, 3.5, 0.9, 0.04, 0.5), (7.2, 3.5, 2.8, 0.05, 0.6)],
            rust=[])


# ------------------------------------------------------------------ floor (u = x - SX0, v = z - SZ0)
def floor():
    FP = 196.5
    W, H = 2048, 1486
    print("floor", W, H)
    img = bomb(floor_src, W, H, patch=(360, 300), seed=71)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    um = xx / FP; vm = (H - 1 - yy) / FP
    Lx, Lz = 10.42, 7.56
    D = np.ones((H, W, 3), np.float32)

    def stain(alpha, colour):
        nonlocal D
        D *= 1 - np.clip(alpha, 0, 1)[..., None] * (1 - np.array(colour, np.float32))
    stain(np.clip(fbm(W, H, 500, 4, 72) - 0.42, 0, 1) * 1.3, (0.72, 0.7, 0.66))
    stain(np.clip(fbm(W, H, 130, 4, 73) - 0.55, 0, 1) * 1.6, (0.6, 0.58, 0.55))
    # saw-cut joints with chipped edges
    for j in (2.6, 5.2, 7.8):
        w_ = (fbm(8, H, 30, 2, int(j * 10))[:, 0][:, None] - 0.5) * 0.004
        stain(np.exp(-(((um - j - w_) / 0.006) ** 2)) * 0.9, (0.22, 0.21, 0.2))
        stain(np.exp(-(((um - j) / 0.03) ** 2)) * (fbm(W, H, 20, 2, 74) > 0.6) * 0.5, (0.5, 0.48, 0.45))
    for j in (2.5, 5.0):
        stain(np.exp(-(((vm - j) / 0.006) ** 2)) * 0.9, (0.22, 0.21, 0.2))
        stain(np.exp(-(((vm - j) / 0.03) ** 2)) * (fbm(W, H, 20, 2, 75) > 0.6) * 0.5, (0.5, 0.48, 0.45))
    # grime along the walls and in the corners
    edge = np.minimum(np.minimum(um, Lx - um), np.minimum(vm, Lz - vm))
    stain(np.exp(-edge / 0.3) * (0.45 + 0.5 * fbm(W, H, 60, 3, 76)), (0.5, 0.47, 0.43))
    # where the drain joint drips (south wall, x = -214.64 -> u 7.32): a dark wet patch with a dried rim
    px, pz = 7.32, 0.45
    d = np.sqrt(((um - px) / 0.55) ** 2 + ((vm - pz) / 0.42) ** 2) + (fbm(W, H, 40, 3, 77) - 0.5) * 0.5
    stain(np.clip(1.15 - d, 0, 1) * 2.0 * 0.6, (0.42, 0.4, 0.37))
    stain(np.exp(-((d - 1.0) / 0.06) ** 2) * 0.5, (0.78, 0.72, 0.6))
    # the tap on the east wall (x = -211.6, z = 88.0 -> u 10.3, v 1.64)
    d = np.sqrt(((um - 10.2) / 0.3) ** 2 + ((vm - 1.7) / 0.4) ** 2) + (fbm(W, H, 30, 3, 78) - 0.5) * 0.5
    stain(np.clip(1.1 - d, 0, 1) * 1.6 * 0.6, (0.5, 0.42, 0.33))
    # plaster dust under the broken wall (east wall, z 92 - 93 -> v 5.7 - 6.7)
    d = np.sqrt(((um - 10.2) / 0.5) ** 2 + ((vm - 6.2) / 0.8) ** 2) + (fbm(W, H, 25, 3, 79) - 0.5) * 0.6
    dust = np.clip(1.1 - d, 0, 1)
    img = img * (1 - 0.45 * dust[..., None]) + np.array((0.66, 0.64, 0.6), np.float32) * 0.45 * dust[..., None]
    # scuffs: dragged pallets
    for i in range(26):
        r = np.random.default_rng(900 + i)
        x0, y0 = r.random() * Lx, r.random() * Lz; ang = r.random() * math.pi; ln = 0.4 + r.random() * 1.6
        t = (um - x0) * math.cos(ang) + (vm - y0) * math.sin(ang); s = -(um - x0) * math.sin(ang) + (vm - y0) * math.cos(ang)
        a = np.exp(-((s / 0.008) ** 2)) * (np.abs(t) < ln * 0.5) * (0.25 + 0.3 * r.random())
        stain(a, (0.3, 0.29, 0.28) if r.random() < 0.6 else (0.95, 0.95, 0.95))
    img = np.clip(img * D, 0, 1)
    to_img(img).save(OUT + "sr_floor.png")


# ------------------------------------------------------------------ ceiling (u = SX1 - x, v = z - SZ0), 0.6 m tiles
HOLES = [(14, 0), (0, 10), (1, 10)]            # tile cells (iu, iv) that are gone: see StoreFittingsDecay.cs
def ceiling():
    FP = 196.5
    W, H = 2048, 1486
    print("ceiling")
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    um = xx / FP; vm = (H - 1 - yy) / FP
    base = np.array((0.80, 0.79, 0.75), np.float32)
    img = np.ones((H, W, 3), np.float32) * base
    # mineral-fibre face: pin holes and short fissures
    pits = (rng.random((H, W)) > 0.93).astype(np.float32)
    fiss = (fbm(W, H, 5, 2, 31) > 0.66).astype(np.float32) * (fbm(W, H, 60, 2, 32) > 0.45)
    img *= (1 - 0.22 * blur(pits, 0.6) - 0.16 * blur(fiss, 0.7))[..., None]
    iu = np.floor(um / 0.6).astype(int); iv = np.floor(vm / 0.6).astype(int)
    r = np.random.default_rng(33)
    tone = r.random((20, 20)).astype(np.float32)
    img *= (0.9 + 0.1 * tone[np.clip(iv, 0, 19), np.clip(iu, 0, 19)])[..., None]
    D = np.ones((H, W, 3), np.float32)

    def stain(alpha, colour):
        nonlocal D
        D *= 1 - np.clip(alpha, 0, 1)[..., None] * (1 - np.array(colour, np.float32))
    stain(np.clip(fbm(W, H, 400, 4, 34) - 0.4, 0, 1) * 1.2, (0.8, 0.77, 0.7))
    # water stains: brown rings, strongest near the holes and the drain
    spots = [(8.9, 0.5, 0.9), (8.2, 1.0, 0.5), (0.7, 6.3, 1.0), (1.6, 5.7, 0.6), (3.1, 0.4, 0.8), (3.6, 1.1, 0.45), (5.5, 4.6, 0.5), (9.6, 6.6, 0.7), (6.8, 2.2, 0.4), (2.2, 3.0, 0.45), (7.4, 6.9, 0.5)]
    for i, (su, sv, rad) in enumerate(spots):
        d = np.sqrt((um - su) ** 2 + (vm - sv) ** 2) / rad + (fbm(W, H, 50, 3, 40 + i) - 0.5) * 0.7
        stain(np.clip(1.0 - d, 0, 1) ** 0.6 * 0.42, (0.74, 0.64, 0.48))
        stain(np.exp(-((d - 0.93) / 0.09) ** 2) * 0.42 * (0.4 + 0.6 * fbm(W, H, 70, 2, 140 + i)), (0.55, 0.44, 0.3))
    img = np.clip(img * D, 0, 1)
    # the grid: white T-bars gone yellow, a shadow line beside each
    fu = (um / 0.6) % 1.0; fv = (vm / 0.6) % 1.0
    du = np.minimum(fu, 1 - fu) * 0.6; dv = np.minimum(fv, 1 - fv) * 0.6
    dg = np.minimum(du, dv)
    bar = (dg < 0.012).astype(np.float32)
    shadow = np.exp(-((dg - 0.016) / 0.006) ** 2) * (1 - bar)
    img *= (1 - 0.35 * shadow)[..., None]
    barcol = np.array((0.74, 0.72, 0.66), np.float32) * (0.85 + 0.15 * fbm(W, H, 80, 2, 35))[..., None]
    rustb = np.clip((fbm(W, H, 170, 4, 36) - 0.6) * 6, 0, 1)[..., None]
    barcol = barcol * (1 - rustb * (1 - np.array((0.7, 0.55, 0.42), np.float32)))
    img = img * (1 - bar[..., None]) + barcol * bar[..., None]
    # missing tiles: the dark void above (the grid bars stay)
    for (hu, hv) in HOLES:
        inside = ((iu == hu) & (iv == hv)).astype(np.float32) * (1 - bar)
        void = np.array((0.045, 0.045, 0.05), np.float32) * (0.6 + 0.8 * fbm(W, H, 40, 2, 37))[..., None]
        img = img * (1 - inside[..., None]) + void * inside[..., None]
    to_img(np.clip(img, 0, 1)).save(OUT + "sr_ceiling.png")


# ------------------------------------------------------------------ small tiling materials
def small():
    print("small")
    S = 256
    rust = load(ROOT + "Assets/Textures/ParkToilet/Toilet_Rust.png", (S, S))
    # galvanised pipe: dull zinc, spangle, rust blooming at the threads
    z = fbm(S, S, 40, 4, 51); sp = fbm(S, S, 6, 2, 52)
    galv = np.ones((S, S, 3), np.float32) * np.array((0.47, 0.48, 0.48), np.float32) * (0.82 + 0.3 * z + 0.12 * sp)[..., None]
    rmask = np.clip((fbm(S, S, 30, 4, 53) - 0.52) * 7, 0, 1)[..., None]
    rcol = np.array((0.36, 0.2, 0.1), np.float32) * (0.7 + 0.8 * fbm(S, S, 10, 3, 54))[..., None]
    galv = galv * (1 - rmask) + rcol * rmask
    galv = seam(galv)
    to_img(galv).save(OUT + "sr_pipe_galv.png")
    # grey PVC drain: chalky, dirty runs along it
    pv = np.ones((S, S, 3), np.float32) * np.array((0.6, 0.6, 0.58), np.float32) * (0.9 + 0.16 * fbm(S, S, 60, 3, 55))[..., None]
    runs = np.clip(fbm(S, 8, 7, 2, 56)[0][None, :] - 0.55, 0, 1) * 3.0 * (0.5 + 0.5 * fbm(S, S, 30, 2, 57))
    pv *= (1 - np.clip(runs, 0, 1)[..., None] * (1 - np.array((0.5, 0.42, 0.32), np.float32)))
    pv *= (1 - 0.25 * (fbm(S, S, 5, 2, 58) > 0.7))[..., None]
    to_img(seam(pv)).save(OUT + "sr_pipe_pvc.png")
    # hollow cement block / broken blockwork: coarse cement
    b = np.asarray(to_img(cem[:640, :640]).resize((S, S), Image.LANCZOS), np.float32) / 255.0
    b = grade(b, (0.4, 0.395, 0.38), contrast=1.5)
    b *= (1 - 0.3 * blur((rng.random((S, S)) > 0.9).astype(np.float32), 0.8))[..., None]
    to_img(seam(b)).save(OUT + "sr_cmu.png")
    # blockwork seen in the broken wall: 0.4 x 0.2 m blocks, mortar joints (1 m x 1 m per repeat, 512 px)
    T = 512
    bw = np.asarray(to_img(np.tile(b, (2, 2, 1))).resize((T, T), Image.LANCZOS), np.float32) / 255.0
    yy, xx = np.mgrid[0:T, 0:T].astype(np.float32)
    row = np.floor(yy / (T / 5.0)); fy = (yy / (T / 5.0)) % 1.0
    fx = ((xx / (T / 2.5)) + (row % 2) * 0.5) % 1.0
    joint = ((np.minimum(fy, 1 - fy) < 0.045) | (np.minimum(fx, 1 - fx) < 0.022)).astype(np.float32)
    joint = blur(joint, 0.8)
    tone = np.random.default_rng(60).random((6, 8)).astype(np.float32)
    cell = tone[np.clip(row.astype(int), 0, 5), np.clip(np.floor((xx / (T / 2.5)) + (row % 2) * 0.5).astype(int) % 8, 0, 7)]
    bw *= (0.82 + 0.3 * cell)[..., None]
    mortar = np.array((0.56, 0.55, 0.52), np.float32) * (0.8 + 0.3 * fbm(T, T, 12, 2, 61))[..., None]
    bw = bw * (1 - joint[..., None]) + mortar * joint[..., None]
    to_img(np.clip(bw, 0, 1)).save(OUT + "sr_blockwork.png")
    # the broken edge of plaster (side of the chunks): pale, gritty
    pl = grade(paint_src[:S, :S], (0.7, 0.68, 0.63), desat=0.6, contrast=1.6)
    to_img(seam(pl)).save(OUT + "sr_plaster_edge.png")
    # a tiling piece of the painted wall, for the chips on the floor
    to_img(seam(paint_src[100:100 + S, 200:200 + S])).save(OUT + "sr_paint_chip.png")


def seam(a):
    """make tileable: blend with the half-shifted copy"""
    h, w = a.shape[:2]
    b = np.roll(np.roll(a, h // 2, 0), w // 2, 1)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    wx = np.abs(xx - w / 2) / (w / 2); wy = np.abs(yy - h / 2) / (h / 2)
    m = np.clip((np.maximum(wx, wy) - 0.55) / 0.35, 0, 1)[..., None]
    return a * (1 - m) + b * m


todo = sys.argv[1:] or ["walls", "floor", "ceiling", "small"]
if "walls" in todo:
    s = wall("sr_wall_s", seed=101, **SOUTH)
    e = wall("sr_wall_e", seed=201, **EAST)
    wall("sr_wall_n", seed=301, base=0.34, **NORTH)
    wall("sr_wall_w", seed=401, base=0.34, **WEST)
    to_img(s).resize((1563, 533)).save(HERE + "prev_wall_s.jpg", quality=88)
    to_img(e).resize((1134, 533)).save(HERE + "prev_wall_e.jpg", quality=88)
if "floor" in todo:
    floor(); Image.open(OUT + "sr_floor.png").resize((1024, 743)).save(HERE + "prev_floor.jpg", quality=88)
if "ceiling" in todo:
    ceiling(); Image.open(OUT + "sr_ceiling.png").resize((1024, 743)).save(HERE + "prev_ceiling.jpg", quality=88)
if "small" in todo:
    small()
print("done")
