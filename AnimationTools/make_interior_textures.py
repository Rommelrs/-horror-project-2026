# Textures for the park toilet INTERIOR (men's side), matched to the concept sheet: sun-faded cream plaster with flaking paint above,
# chipped blue-grey painted dado below, broken tile floor with sand in the joints, painted steel partitions with rust, stained porcelain,
# cloudy mirror, dirty ceiling. Paint flakes are Voronoi cells (paint cracks into little plates, a cluster of plates falls off); the
# grain/substrate comes from the user's location photos (IMG_1973 stucco, IMG_2021 concrete, IMG_2017 sandy dust).
# Arrays are built in UV space (row 0 = v 0 = bottom) and flipped when saved.
import numpy as np
from PIL import Image, ImageFilter

REF = "D:/Unity Stuff/Shamkha Refs/"
OUT = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/Assets/Textures/ParkToiletInterior/"
rng = np.random.default_rng(21)


# ------------------------------------------------------------------ helpers
def blur_f(a, r, axis=None):
    """Wrapping gaussian (keeps textures tileable). axis=0/1 blurs only along that axis."""
    n = max(1, int(np.ceil(r * 3)))
    k = np.exp(-0.5 * (np.arange(-n, n + 1) / r) ** 2); k /= k.sum()
    out = a
    for ax in ((1, 0) if axis is None else (axis,)):
        acc = np.zeros_like(out)
        for i, w in zip(range(-n, n + 1), k): acc += w * np.roll(out, i, ax)
        out = acc
    return out


def blur_img(a, r):
    if a.ndim == 3: return np.dstack([blur_img(a[..., i], r) for i in range(a.shape[2])])
    im = Image.fromarray(np.clip(a * 255, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r))
    return np.asarray(im, dtype=np.float32) / 255.0


def smooth(t):
    t = np.clip(t, 0, 1); return t * t * (3 - 2 * t)


def noise(h, w, r, seed_shift=0):
    """Tileable smooth noise, roughly unit variance, feature size ~r px."""
    n = blur_f(rng.standard_normal((h, w)).astype(np.float32), r)
    return (n - n.mean()) / (n.std() + 1e-6)


def fbm(h, w, r, octaves=4):
    out = np.zeros((h, w), np.float32); amp = 1.0; tot = 0
    for o in range(octaves):
        out += amp * noise(h, w, max(1.0, r / 2 ** o)); tot += amp; amp *= 0.5
    return out / tot


def voronoi(h, w, g, jitter=0.9):
    """Jittered-grid Voronoi, wrapping. Returns F1, F2 (px) and the nearest cell id."""
    gh, gw = h // g, w // g
    sy = (rng.random((gh, gw)) - 0.5) * jitter * g + g / 2
    sx = (rng.random((gh, gw)) - 0.5) * jitter * g + g / 2
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    cy, cx = (yy // g).astype(int), (xx // g).astype(int)
    f1 = np.full((h, w), 1e9, np.float32); f2 = np.full((h, w), 1e9, np.float32); cid = np.zeros((h, w), int)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            ny, nx = cy + dy, cx + dx
            py = ny * g + sy[ny % gh, nx % gw]; px = nx * g + sx[ny % gh, nx % gw]
            d = np.sqrt((yy - py) ** 2 + (xx - px) ** 2)
            ids = (ny % gh) * gw + (nx % gw)
            closer = d < f1
            f2 = np.where(closer, f1, np.minimum(f2, d)); cid = np.where(closer, ids, cid); f1 = np.where(closer, d, f1)
    return f1, f2, cid, gh * gw


def load_crop(name, x, y, size, out_wh):
    im = Image.open(REF + name).convert("RGB").crop((x, y, x + size, y + size)).resize(out_wh, Image.LANCZOS)
    return np.asarray(im, dtype=np.float32) / 255.0


def delight(a, radius):
    low = blur_img(a, radius)
    return a / np.maximum(low, 1e-3) * low.reshape(-1, 3).mean(0)


def seamless(a):
    h, w = a.shape[:2]
    b = np.roll(np.roll(a, h // 2, 0), w // 2, 1)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.minimum(np.minimum(xx, w - 1 - xx) / w, np.minimum(yy, h - 1 - yy) / h)
    nz = (noise(h, w, 18) * 0.5 + 0.5)
    m = smooth((d - 0.05 + (nz - 0.5) * 0.08) / 0.14)[..., None]
    return a * m + b * (1 - m)


def tile_to(a, h, w):
    """Repeat/crop a tileable (H, W[, C]) array to (h, w)."""
    reps = (int(np.ceil(h / a.shape[0])), int(np.ceil(w / a.shape[1]))) + ((1,) if a.ndim == 3 else ())
    return np.tile(a, reps)[:h, :w]


def normal_from_height(hgt, strength):
    gx = (np.roll(hgt, -1, 1) - np.roll(hgt, 1, 1)) * 0.5
    gy = (np.roll(hgt, -1, 0) - np.roll(hgt, 1, 0)) * 0.5
    n = np.dstack([-gx * strength, -gy * strength, np.ones_like(hgt)])      # UV space rows go up = +v
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return n * 0.5 + 0.5


def save(a, name):
    a = np.flipud(np.clip(a, 0, 1))
    Image.fromarray((a * 255 + 0.5).astype(np.uint8)).save(OUT + name)


def col(c): return np.array(c, np.float32)[None, None, :]


# ------------------------------------------------------------------ photo sources (de-lit, tileable)
stucco = seamless(delight(load_crop("IMG_1973.jpg", 3664, 2236, 2048, (1024, 1024)), 90))
st_l = stucco.mean(2); GRAIN = (st_l - blur_f(st_l, 6)) / (st_l.std() + 1e-6)          # fine stucco relief, ~unit
conc = seamless(delight(load_crop("IMG_2021.jpg", 540, 0, 3200, (1024, 1024)), 160))
co_l = conc.mean(2); CONC = (co_l - co_l.mean()) / (co_l.std() + 1e-6)                    # concrete blotches + pits
sandp = seamless(delight(load_crop("IMG_2017.jpg", 1000, 1500, 2000, (512, 512)), 80))


def flaking_paint(h, w, cell, paint, under, chip_amount, cluster_r, extra_bias=None, grain_amt=0.10, under_detail=None):
    """Paint layer that has cracked into plates; clusters of plates are missing. Returns colour, paint mask (1 = paint), height."""
    f1, f2, cid, n = voronoi(h, w, cell)
    crack = 1 - smooth((f2 - f1) / 1.6)                                   # thin dark lines between plates
    cell_rand = rng.random(n)[cid]
    cluster = noise(h, w, cluster_r) * 0.8 + noise(h, w, cluster_r / 3) * 0.35
    bias = cluster + (cell_rand - 0.5) * 1.3 + (extra_bias if extra_bias is not None else 0)
    chipped = (bias > (2.2 - 4.0 * chip_amount)).astype(np.float32)
    # soften the plate edges a touch so they read as flakes, not pixels
    paint_m = 1 - chipped
    g = tile_to(GRAIN, h, w)
    pcol = col(paint) * (1 + grain_amt * g)[..., None] * (1 + 0.04 * noise(h, w, 40))[..., None]
    pcol = pcol * (1 - 0.35 * crack * paint_m)[..., None]
    ucol = under_detail if under_detail is not None else col(under) * (1 + 0.12 * tile_to(CONC, h, w))[..., None]
    # paint edge: the rim of a chip is slightly lighter (lifted paint) and casts a hairline shadow on the substrate
    rim = smooth(1 - np.abs(blur_f(paint_m, 1.2) - 0.5) * 2) * paint_m
    shade = np.roll(np.roll(paint_m, 1, 0), -1, 1) * chipped
    c = pcol * paint_m[..., None] + ucol * chipped[..., None]
    c = c * (1 + 0.10 * rim)[..., None] * (1 - 0.25 * shade)[..., None]
    height = paint_m * 1.0 + g * 0.08 - crack * 0.3 * paint_m
    return c, paint_m, height


def streaks(h, w, r_x, r_y, from_top=True, length=0.6):
    """Vertical run stains (water/rust) hanging from the top edge (or anywhere)."""
    s = rng.standard_normal((h, w)).astype(np.float32)
    s = blur_f(blur_f(s, r_x, axis=1), r_y, axis=0)
    s = (s - s.mean()) / (s.std() + 1e-6)
    s = smooth((s - 0.6) / 1.4)
    if from_top:
        v = np.linspace(0, 1, h, dtype=np.float32)[:, None]            # 0 bottom .. 1 top
        s = s * smooth((v - (1 - length)) / length)
    return s


# ------------------------------------------------------------------ upper wall plaster (1024 x 512 = 3.0 m x 1.5 m, bottom = dado seam)
def make_plaster():
    h, w = 512, 1024
    v = np.linspace(0, 1, h, dtype=np.float32)[:, None] * np.ones((1, w), np.float32)
    under = col((0.60, 0.50, 0.37)) * (1 + 0.16 * tile_to(CONC, h, w))[..., None]
    c, pm, hgt = flaking_paint(h, w, 20, (0.80, 0.735, 0.605), None, 0.30, 70, under_detail=under,
                               extra_bias=0.6 * (1 - smooth(v / 0.18)) + 0.25 * smooth((v - 0.8) / 0.2))
    # blue paint remnants creeping up from the dado below (irregular, plate-wise)
    f1, f2, cid, n = voronoi(h, w, 20)
    edge = 0.10 + 0.07 * noise(h, w, 30) + (rng.random(n)[cid] - 0.5) * 0.08
    blue = (v < edge).astype(np.float32)
    bcol = col((0.37, 0.43, 0.47)) * (1 + 0.08 * tile_to(GRAIN, h, w))[..., None]
    c = c * (1 - blue[..., None]) + bcol * blue[..., None]
    # sun-faded / dirty: large blotches, brown water stains running down, darker toward the ceiling
    c *= (1 + 0.05 * fbm(h, w, 120))[..., None]
    st = streaks(h, w, 2.5, 60, True, 0.75)
    c = c * (1 - 0.30 * st[..., None]) + col((0.42, 0.31, 0.19)) * 0.30 * st[..., None]
    rust = smooth((noise(h, w, 50) - 1.4) / 0.8) * (1 - pm) * 0.6
    c = c * (1 - rust[..., None]) + col((0.45, 0.28, 0.15)) * rust[..., None]
    c *= (1 - 0.18 * smooth((v - 0.75) / 0.25))[..., None]
    save(c, "Int_Plaster.png"); save(normal_from_height(hgt + blue * 0.6, 3.0), "Int_Plaster_N.png")


# ------------------------------------------------------------------ painted dado (1024 x 512 = 2.6 m x 1.3 m, top = dado seam)
def make_dado():
    h, w = 512, 1024
    v = np.linspace(0, 1, h, dtype=np.float32)[:, None] * np.ones((1, w), np.float32)
    # what shows where the blue paint is gone: mostly the cream plaster, sometimes bare brown render
    cream = col((0.77, 0.71, 0.59)) * (1 + 0.08 * tile_to(GRAIN, h, w))[..., None]
    render = col((0.55, 0.45, 0.33)) * (1 + 0.15 * tile_to(CONC, h, w))[..., None]
    rm = smooth((noise(h, w, 40) - 0.5) / 0.6)[..., None]
    under = cream * (1 - rm) + render * rm
    c, pm, hgt = flaking_paint(h, w, 22, (0.37, 0.43, 0.47), None, 0.28, 60, under_detail=under,
                               extra_bias=0.35 * smooth((v - 0.8) / 0.2) + 0.5 * (1 - smooth(v / 0.15)))
    # the top edge: the blue paint ends unevenly below the seam, cream plaster above (matches the plaster texture's bottom)
    f1, f2, cid, n = voronoi(h, w, 22)
    above = np.zeros((h, w), np.float32)     # blue runs right up to the seam; the ragged paint edge is in the plaster texture
    c = c * (1 - above[..., None]) + cream * above[..., None]
    pm = pm * (1 - above)
    # rust specks/runs, scuffs, and dirt + sand at the floor
    st = streaks(h, w, 2.0, 45, False)
    c = c * (1 - 0.25 * st[..., None]) + col((0.40, 0.28, 0.17)) * 0.25 * st[..., None]
    c *= (1 + 0.06 * fbm(h, w, 100))[..., None]
    dirt = (1 - smooth(v / 0.22)) * (0.55 + 0.25 * noise(h, w, 20))
    dirt = np.clip(dirt, 0, 0.85)
    c = c * (1 - dirt[..., None]) + col((0.42, 0.35, 0.26)) * dirt[..., None]
    save(c, "Int_Dado.png"); save(normal_from_height(hgt * (1 - above) + above * 0.2, 3.0), "Int_Dado_N.png")


# ------------------------------------------------------------------ broken tile floor (1024^2 = 1.67 m), sand in the joints
def make_floor():
    h = w = 1024; T = 128                                              # 8 x 8 square tiles
    yy, xx = np.mgrid[0:h, 0:w]
    ty, tx = yy // T, xx // T; tid = ty * 8 + tx
    # joints: distance to the tile border, wobbled a little so the tiles are not laser cut
    wob = noise(h, w, 12) * 1.2
    dx = np.minimum(xx % T, T - 1 - xx % T) + wob; dy = np.minimum(yy % T, T - 1 - yy % T) - wob
    joint = 1 - smooth((np.minimum(dx, dy) - 1.5) / 2.0)
    tint = rng.normal(1.0, 0.07, 64)[tid]
    hue = rng.uniform(-1, 1, 64)[tid]
    base = col((0.63, 0.56, 0.45)) * tint[..., None] + np.dstack([hue * 0.03, hue * 0.015, -hue * 0.005])
    base = base * (1 + 0.15 * tile_to(CONC, h, w))[..., None] * (1 + 0.06 * tile_to(GRAIN, h, w))[..., None]
    # cracked tiles: Voronoi cracks only inside tiles flagged as cracked
    f1, f2, cid, n = voronoi(h, w, 64, jitter=1.0)
    cracked = (rng.random(64)[tid] < 0.45).astype(np.float32)
    crack = (1 - smooth((f2 - f1 - 0.6) / 1.4)) * cracked
    # broken pieces: some Voronoi pieces inside cracked tiles are missing (low, dark, filled with dirt)
    gone = ((rng.random(n)[cid] < 0.18) * cracked).astype(np.float32) * (1 - joint)
    sand = tile_to(sandp, h, w)
    sand = sand / sand.reshape(-1, 3).mean(0) * np.array([0.60, 0.50, 0.36])
    grout = col((0.26, 0.21, 0.16))
    fill = np.where((noise(h, w, 30) > -0.3)[..., None], sand * 0.8, grout)
    c = base * (1 - 0.6 * crack)[..., None]
    c = c * (1 - gone[..., None]) + (sand * 0.72) * gone[..., None]     # broken-out pieces are full of packed sand/dirt
    c = c * (1 - joint[..., None]) + fill * joint[..., None]
    drift = smooth((fbm(h, w, 90) - 0.3) / 0.9) * 0.5
    c = c * (1 - drift[..., None]) + sand * drift[..., None]
    stain = smooth((fbm(h, w, 70) - 0.9) / 0.6) * 0.5
    c = c * (1 - stain[..., None]) + col((0.33, 0.24, 0.15)) * stain[..., None]
    hgt = (1 - joint) * (1 - gone) - crack * 0.3 + tile_to(GRAIN, h, w) * 0.05 - drift * 0.3
    save(c, "Int_Floor.png"); save(normal_from_height(hgt, 2.0), "Int_Floor_N.png")


# ------------------------------------------------------------------ ceiling (1024^2 = 2 m): dirty render, water rings, soot
def make_ceiling():
    h = w = 1024
    c = col((0.44, 0.40, 0.34)) * (1 + 0.20 * tile_to(CONC, h, w))[..., None] * (1 + 0.05 * tile_to(GRAIN, h, w))[..., None]
    n = fbm(h, w, 110)
    ring = smooth(1 - np.abs(n - 0.6) / 0.05) * 0.35
    inside = smooth((n - 0.6) / 0.05) * 0.12
    c = c * (1 - ring - inside)[..., None] + col((0.30, 0.22, 0.14)) * (ring + inside)[..., None]
    c *= (1 + 0.08 * fbm(h, w, 60))[..., None]
    save(c, "Int_Ceiling.png")


# ------------------------------------------------------------------ partition / stall door steel (512 x 1024 = panel, v 0 = bottom)
def make_partition():
    h, w = 1024, 512
    v = np.linspace(0, 1, h, dtype=np.float32)[:, None] * np.ones((1, w), np.float32)
    u = np.ones((h, 1), np.float32) * np.linspace(0, 1, w, dtype=np.float32)[None, :]
    edge = np.minimum.reduce([u, 1 - u, v, 1 - v])
    rustc = col((0.42, 0.26, 0.14)) * (1 + 0.25 * tile_to(CONC, h, w))[..., None]
    metal = col((0.40, 0.40, 0.38)) * (1 + 0.1 * tile_to(GRAIN, h, w))[..., None]
    mm = smooth((noise(h, w, 25) + 0.5) / 0.6)[..., None]
    under = rustc * mm + metal * (1 - mm)
    bias = 1.1 * (1 - smooth(v / 0.18)) + 0.8 * (1 - smooth(edge / 0.04))
    c, pm, hgt = flaking_paint(h, w, 12, (0.36, 0.42, 0.46), None, 0.22, 45, extra_bias=bias, under_detail=under, grain_amt=0.05)
    st = streaks(h, w, 1.5, 70, False)
    c = c * (1 - 0.35 * st[..., None]) + col((0.40, 0.25, 0.13)) * 0.35 * st[..., None]
    # scratches: thin random lines
    sc = np.zeros((h, w), np.float32)
    for _ in range(90):
        x0, y0 = rng.integers(0, w), rng.integers(0, h); L = rng.integers(20, 120); ang = rng.uniform(0, np.pi)
        for t in range(L):
            x = int(x0 + t * np.cos(ang)) % w; y = int(y0 + t * np.sin(ang)) % h; sc[y, x] = 1
    sc = blur_f(sc, 0.6) * 1.5
    c = c * (1 + 0.25 * sc * pm)[..., None]
    c *= (1 - 0.15 * (1 - smooth(v / 0.3)))[..., None]
    c *= (1 + 0.05 * fbm(h, w, 90))[..., None]
    save(c, "Int_Partition.png"); save(normal_from_height(hgt - sc * 0.2, 2.5), "Int_Partition_N.png")


# ------------------------------------------------------------------ porcelain (1024^2, 0.67 m): cream glaze, crazing, brown stains
def make_porcelain():
    h = w = 1024
    c = col((0.85, 0.82, 0.74)) * (1 + 0.025 * tile_to(GRAIN, h, w))[..., None]
    f1, f2, cid, n = voronoi(h, w, 64)
    craze = (1 - smooth((f2 - f1) / 1.2)) * 0.18
    c = c * (1 - craze[..., None])
    st = smooth((fbm(h, w, 60) - 0.4) / 0.9)
    c = c * (1 - 0.45 * st[..., None]) + col((0.55, 0.40, 0.24)) * 0.45 * st[..., None]
    run = streaks(h, w, 2.0, 50, False)
    c = c * (1 - 0.18 * run[..., None]) + col((0.48, 0.32, 0.17)) * 0.18 * run[..., None]
    save(c, "Int_Porcelain.png")


# ------------------------------------------------------------------ mirror (512^2): cloudy, smeared, desilvered at the edges
def make_mirror():
    h = w = 512
    v = np.linspace(0, 1, h, dtype=np.float32)[:, None] * np.ones((1, w), np.float32)
    u = np.ones((h, 1), np.float32) * np.linspace(0, 1, w, dtype=np.float32)[None, :]
    edge = np.minimum.reduce([u, 1 - u, v, 1 - v])
    c = col((0.52, 0.56, 0.56)) * (0.85 + 0.25 * v)[..., None]                 # fake reflection gradient
    cloud = smooth((fbm(h, w, 50) + 0.5) / 1.5)
    c = c * (1 - 0.25 * cloud[..., None]) + col((0.70, 0.68, 0.62)) * 0.25 * cloud[..., None]
    smear = blur_f(blur_f(rng.standard_normal((h, w)).astype(np.float32), 18, axis=1), 2, axis=0)
    c *= (1 + 0.06 * smear / (smear.std() + 1e-6))[..., None]
    desil = smooth((noise(h, w, 8) * 0.45 + (1 - smooth(edge / 0.05)) * 1.4 - 1.0) / 0.35)
    c = c * (1 - desil[..., None] * 0.8) + col((0.22, 0.18, 0.13)) * desil[..., None] * 0.8
    spots = smooth((noise(h, w, 3) - 2.8) / 0.4) * 0.6
    c = c * (1 - spots[..., None]) + col((0.25, 0.2, 0.15)) * spots[..., None]
    save(c, "Int_Mirror.png")


# ------------------------------------------------------------------ sand (512^2, 1 m)
def make_sand():
    s = sandp / sandp.reshape(-1, 3).mean(0) * np.array([0.62, 0.52, 0.38])
    save(s, "Int_Sand.png")


import os
os.makedirs(OUT, exist_ok=True)
import sys
ONLY = sys.argv[1:]
for f in (make_plaster, make_dado, make_floor, make_ceiling, make_partition, make_porcelain, make_mirror, make_sand):
    if ONLY and f.__name__ not in ONLY: continue
    f(); print("done", f.__name__)
