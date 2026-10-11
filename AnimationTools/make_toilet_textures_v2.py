# More realistic weathered textures for the park public toilet (v2). Tileable plaster/concrete/metal at 512/256/128 px built from the PSX
# library grain plus layered procedural weathering (paint -> plaster -> concrete chipping with lit/shadowed edges, hairline cracks, rain
# streaks, pitting), a unique portrait door texture (peeling paint, rust creeping up from the bottom, scratches), weathered signs, painted
# lettering that has worn away in patches, and RGBA grime overlays (base dirt, roof-edge streaks, damp stain under the dripping pipe).
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

LIB = "D:/Unity Stuff/Shamkha Refs/PSX textures/512x512/"
OUT = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/Assets/Textures/ParkToilet/"
FONT = "C:/Windows/Fonts/arialbd.ttf"


# ------------------------------------------------------------------ helpers
def lib(cat, n, size=None):
    im = Image.open(f"{LIB}{cat}/{cat}_{n:02d}-512x512.png").convert("RGB")
    if size and size != 512:
        im = im.resize((size, size), Image.BOX)
    return np.asarray(im).astype(np.float32) / 255.0


def luma(a):
    return a[..., 0] * 0.3 + a[..., 1] * 0.59 + a[..., 2] * 0.11


def smooth(t):
    t = np.clip(t, 0, 1)
    return t * t * (3 - 2 * t)


def sstep(e0, e1, x):
    return smooth((x - e0) / (e1 - e0))


def pnoise(w, h, cx, cy, rng):
    """Tileable value noise 0..1 with cx x cy lattice cells over a w x h image."""
    lat = rng.random((cy, cx)).astype(np.float32)
    u = np.arange(w, dtype=np.float32) / w * cx
    v = np.arange(h, dtype=np.float32) / h * cy
    iu = np.floor(u).astype(int); iv = np.floor(v).astype(int)
    fu = smooth(u - iu); fv = smooth(v - iv)
    i0 = iu % cx; i1 = (iu + 1) % cx; j0 = iv % cy; j1 = (iv + 1) % cy
    a = lat[np.ix_(j0, i0)]; b = lat[np.ix_(j0, i1)]; c = lat[np.ix_(j1, i0)]; d = lat[np.ix_(j1, i1)]
    fu = fu[None, :]; fv = fv[:, None]
    return (a * (1 - fu) + b * fu) * (1 - fv) + (c * (1 - fu) + d * fu) * fv


def fbm(w, h, cx, cy, octaves, rng, gain=0.5):
    tot = np.zeros((h, w), np.float32); amp = 1.0; norm = 0.0
    for _ in range(octaves):
        tot += amp * pnoise(w, h, max(1, int(cx)), max(1, int(cy)), rng); norm += amp
        amp *= gain; cx *= 2; cy *= 2
    return tot / norm


def warp(field, w, h, amount, rng, cells=6):
    dx = (fbm(w, h, cells, cells, 3, rng) - 0.5) * 2 * amount
    dy = (fbm(w, h, cells, cells, 3, rng) - 0.5) * 2 * amount
    X, Y = np.meshgrid(np.arange(w), np.arange(h))
    xs = (X + dx) % w; ys = (Y + dy) % h
    x0 = np.floor(xs).astype(int); y0 = np.floor(ys).astype(int)
    x1 = (x0 + 1) % w; y1 = (y0 + 1) % h
    fx = xs - x0; fy = ys - y0
    return (field[y0, x0] * (1 - fx) * (1 - fy) + field[y0, x1] * fx * (1 - fy) + field[y1, x0] * (1 - fx) * fy + field[y1, x1] * fx * fy)


def blur(a, r):
    """Gaussian blur of a 2D float array (wraps at the edges, so tileable textures stay tileable)."""
    n = max(1, int(np.ceil(r * 3)))
    k = np.exp(-0.5 * (np.arange(-n, n + 1) / max(r, 1e-3)) ** 2); k /= k.sum()
    out = np.zeros_like(a, dtype=np.float32)
    for i, w in zip(range(-n, n + 1), k):
        out += w * np.roll(a, i, 1)
    tmp = out; out = np.zeros_like(tmp)
    for i, w in zip(range(-n, n + 1), k):
        out += w * np.roll(tmp, i, 0)
    return out


def edge_relief(mask, k=2):
    """+1 on the side a raised/lowered edge catches light, -1 on the shadowed side."""
    return np.roll(np.roll(mask, k, 0), k, 1) - mask


def cracks(w, h, rng, count, length=(60, 220), alpha=0.55):
    """Tileable hairline cracks (drawn on a 3x3 tiling and cropped)."""
    big = Image.new("L", (w * 3, h * 3), 0); d = ImageDraw.Draw(big)
    for _ in range(count):
        x = rng.uniform(0, w); y = rng.uniform(0, h); ang = rng.uniform(0, 2 * np.pi)
        pts = [(x, y)]
        for _ in range(int(rng.uniform(*length) / 9)):
            ang += rng.normal(0, 0.45); x += np.cos(ang) * 9; y += np.sin(ang) * 9; pts.append((x, y))
            if rng.random() < 0.06:                                     # a short branch
                bx, by, ba = x, y, ang + rng.choice([-1, 1]) * 0.7; bp = [(bx, by)]
                for _ in range(int(rng.uniform(2, 6))):
                    ba += rng.normal(0, 0.4); bx += np.cos(ba) * 8; by += np.sin(ba) * 8; bp.append((bx, by))
                for ox in (0, 1, 2):
                    for oy in (0, 1, 2):
                        d.line([(px + ox * w, py + oy * h) for px, py in bp], fill=150, width=1)
        for ox in (0, 1, 2):
            for oy in (0, 1, 2):
                d.line([(px + ox * w, py + oy * h) for px, py in pts], fill=255, width=1)
    c = np.asarray(big, dtype=np.float32)[h:2 * h, w:2 * w] / 255.0
    return blur(c, 0.6) * alpha * 1.6


def save(arr, name):
    Image.fromarray((np.clip(arr, 0, 1) * 255 + 0.5).astype("uint8")).save(OUT + name)


def save_rgba(arr, alpha, name):
    out = np.dstack([np.clip(arr, 0, 1), np.clip(alpha, 0, 1)])
    Image.fromarray((out * 255 + 0.5).astype("uint8"), "RGBA").save(OUT + name)


# ------------------------------------------------------------------ plaster (tileable 512)
def make_plaster():
    S = 512; rng = np.random.default_rng(21)
    grain = luma(lib("Plaster", 19)); grain = grain / grain.mean()
    base = np.array([0.80, 0.74, 0.63])
    col = base[None, None, :] * np.clip(1 + 0.55 * (grain - 1), 0.6, 1.4)[..., None]
    col *= (0.88 + 0.24 * fbm(S, S, 3, 3, 4, rng))[..., None]                        # sun-faded blotches
    # exposed layers
    c1 = warp(fbm(S, S, 5, 5, 6, rng), S, S, 22, rng)
    c1b = warp(fbm(S, S, 16, 16, 5, rng), S, S, 10, rng)
    m1 = np.maximum(sstep(0.62, 0.66, c1), sstep(0.70, 0.73, c1b))                   # paint gone -> bare plaster (big patches + small flakes)
    c2 = warp(fbm(S, S, 9, 9, 6, rng), S, S, 14, rng)
    m2 = m1 * sstep(0.68, 0.72, c2)                                                  # plaster gone -> block/concrete
    g2 = luma(lib("Plaster", 1)); g2 = g2 / g2.mean()
    plaster2 = np.array([0.66, 0.62, 0.55])[None, None, :] * np.clip(1 + 0.5 * (g2 - 1), 0.5, 1.5)[..., None]
    conc = np.array([0.45, 0.43, 0.40])[None, None, :] * (0.8 + 0.4 * fbm(S, S, 40, 40, 3, rng))[..., None]
    col = col * (1 - m1[..., None]) + plaster2 * m1[..., None]
    col = col * (1 - m2[..., None]) + conc * m2[..., None]
    # edge relief for both layers (light catches one side, shadow the other)
    for m, s in ((m1, 0.10), (m2, 0.12)):
        e = edge_relief(blur(m, 0.8), 2)
        col += (e * s)[..., None]
    # rain streaks + grime
    st = fbm(S, S, 28, 3, 4, rng)
    streak = sstep(0.52, 0.80, st) * 0.20
    col = col * (1 - streak[..., None]) + (col * np.array([0.78, 0.72, 0.62]))[...] * streak[..., None]
    # pits and fine cracks
    pits = sstep(0.86, 0.92, fbm(S, S, 220, 220, 2, rng))
    col *= (1 - 0.32 * pits)[..., None]
    cr = cracks(S, S, rng, 7)
    col = col * (1 - cr[..., None] * 0.9) + np.array([0.18, 0.15, 0.12]) * cr[..., None] * 0.9
    save(col, "Toilet_Plaster.png")


# ------------------------------------------------------------------ concrete (tileable 512)
def make_concrete():
    S = 512; rng = np.random.default_rng(33)
    grain = luma(lib("Plaster", 20)); grain = grain / grain.mean()
    base = np.array([0.60, 0.58, 0.53])
    col = base[None, None, :] * np.clip(1 + 0.6 * (grain - 1), 0.55, 1.5)[..., None]
    col *= (0.84 + 0.32 * fbm(S, S, 4, 4, 5, rng))[..., None]
    pits = sstep(0.80, 0.90, fbm(S, S, 150, 150, 2, rng))
    col *= (1 - 0.40 * pits)[..., None]
    # sand dust settled in low patches
    dust = sstep(0.55, 0.75, warp(fbm(S, S, 6, 6, 5, rng), S, S, 25, rng)) * 0.35
    col = col * (1 - dust[..., None]) + np.array([0.74, 0.62, 0.45]) * dust[..., None]
    # dark water stains
    stain = sstep(0.60, 0.78, fbm(S, S, 5, 14, 4, rng)) * 0.28
    col = col * (1 - stain[..., None]) + col * np.array([0.55, 0.52, 0.47]) * stain[..., None]
    cr = cracks(S, S, rng, 4, (90, 260), 0.6)
    col = col * (1 - cr[..., None] * 0.9) + np.array([0.12, 0.11, 0.10]) * cr[..., None] * 0.9
    save(col, "Toilet_Concrete.png")


# ------------------------------------------------------------------ metal tile (electric box etc., 256)
def make_metal_tile():
    S = 256; rng = np.random.default_rng(44)
    grain = luma(lib("Metal", 17, S)); grain = grain / grain.mean()
    col = np.array([0.34, 0.45, 0.46])[None, None, :] * np.clip(1 + 0.5 * (grain - 1), 0.6, 1.4)[..., None]
    col *= (0.9 + 0.2 * fbm(S, S, 4, 4, 4, rng))[..., None]
    rust = lib("Metal", 11, S) * np.array([1.0, 0.72, 0.5])
    rm = sstep(0.60, 0.68, warp(fbm(S, S, 6, 6, 5, rng), S, S, 10, rng))
    col = col * (1 - rm[..., None]) + rust * rm[..., None] * 0.9
    e = edge_relief(blur(rm, 0.7), 1); col += (e * 0.12)[..., None]
    st = sstep(0.55, 0.85, fbm(S, S, 16, 2, 3, rng)) * 0.22
    col = col * (1 - st[..., None]) + np.array([0.28, 0.17, 0.10]) * st[..., None]
    save(col, "Toilet_Metal.png")


# ------------------------------------------------------------------ door (unique portrait 256 x 512; v = 0 at the bottom)
def make_door():
    W, H = 256, 512; rng = np.random.default_rng(55)
    Y, X = np.mgrid[0:H, 0:W].astype(np.float32)
    v = 1 - Y / (H - 1); u = X / (W - 1)
    grain = luma(lib("Metal", 17, 512))[:H, :W]; grain = grain / grain.mean()
    paint = np.array([0.31, 0.43, 0.45])[None, None, :] * np.clip(1 + 0.45 * (grain - 1), 0.6, 1.4)[..., None]
    paint *= (0.88 + 0.24 * fbm(W, H, 3, 5, 4, rng))[..., None]
    paint *= (0.93 + 0.14 * (1 - v))[..., None]                         # a bit darker/dirtier toward the bottom
    # recessed panel: inner rectangle with a lit top-left edge and a shadowed bottom-right edge
    inset = 0.09
    panel = ((u > inset) & (u < 1 - inset) & (v > inset) & (v < 1 - inset)).astype(np.float32)
    edge = edge_relief(blur(panel, 1.0), 3)
    paint += (edge * 0.14)[..., None]
    # rust: creeps up from the bottom, plus around the lock/handle and the frame edges
    noise = warp(fbm(W, H, 5, 8, 6, rng), W, H, 18, rng)
    rust_amt = (1 - sstep(0.0, 0.40, v)) * 0.75 + 0.05 * (1 - v)
    edge_d = np.minimum.reduce([u, 1 - u, v, 1 - v])
    rust_amt += (1 - sstep(0.0, 0.035, edge_d)) * 0.30
    rust_amt += np.exp(-(((u - 0.82) / 0.06) ** 2 + ((v - 0.48) / 0.07) ** 2)) * 0.35   # handle
    rm = sstep(0.58, 0.64, noise + rust_amt * 0.50 - 0.20)
    r_grain = luma(lib("Metal", 11, 512))[:H, :W]; r_grain = r_grain / r_grain.mean()
    rust_col = np.array([0.46, 0.25, 0.12])[None, None, :] * np.clip(1 + 0.6 * (r_grain - 1), 0.5, 1.5)[..., None]
    dark_rust = np.array([0.26, 0.15, 0.09])
    deep = sstep(0.4, 0.9, rm * (noise + 0.2)) * rm
    rust_col = rust_col * (1 - deep[..., None] * 0.5) + dark_rust * deep[..., None] * 0.5
    col = paint * (1 - rm[..., None]) + rust_col * rm[..., None]
    col += (edge_relief(blur(rm, 0.8), 1) * 0.12)[..., None]
    # rust streaks running down from the rusty areas
    sk = sstep(0.55, 0.85, fbm(W, H, 22, 2, 3, rng)) * sstep(0.2, 0.6, 1 - v) * 0.25
    col = col * (1 - sk[..., None]) + np.array([0.30, 0.17, 0.10]) * sk[..., None]
    # scratches
    sc = Image.new("L", (W, H), 0); d = ImageDraw.Draw(sc)
    for _ in range(28):
        x = rng.uniform(0, W); y = rng.uniform(0, H); a = rng.normal(1.57, 0.5); l = rng.uniform(15, 70)
        d.line([(x, y), (x + np.cos(a) * l, y + np.sin(a) * l)], fill=int(rng.uniform(90, 200)), width=1)
    sc = np.asarray(sc.filter(ImageFilter.GaussianBlur(0.4)), dtype=np.float32) / 255.0
    col = col * (1 - sc[..., None] * 0.6) + np.array([0.62, 0.64, 0.62]) * sc[..., None] * 0.5
    # louvre opening (dark, the slat geometry sits in front of it)
    hole = sstep(0.0, 0.02, u - 0.26) * sstep(0.0, 0.02, 0.74 - u) * sstep(0.0, 0.01, v - 0.10) * sstep(0.0, 0.01, 0.245 - v)
    col = col * (1 - hole[..., None] * 0.93) + np.array([0.03, 0.03, 0.03]) * hole[..., None] * 0.93
    save(col, "Toilet_Door.png")


# ------------------------------------------------------------------ rust / pipe / vent tiles (128..256)
def make_rust():
    S = 256; rng = np.random.default_rng(66)
    g = luma(lib("Metal", 13, S)); g = g / g.mean()
    col = np.array([0.40, 0.23, 0.12])[None, None, :] * np.clip(1 + 0.7 * (g - 1), 0.4, 1.6)[..., None]
    col *= (0.75 + 0.5 * fbm(S, S, 6, 6, 5, rng))[..., None]
    flake = sstep(0.70, 0.76, fbm(S, S, 18, 18, 3, rng))
    col = col * (1 - flake[..., None]) + np.array([0.18, 0.10, 0.06]) * flake[..., None]
    col += (edge_relief(blur(flake, 0.6), 1) * 0.10)[..., None]
    save(col, "Toilet_Rust.png")


def make_pipe():
    S = 256; rng = np.random.default_rng(77)
    g = luma(lib("Metal", 18, S)); g = g / g.mean()
    col = np.array([0.34, 0.35, 0.34])[None, None, :] * np.clip(1 + 0.6 * (g - 1), 0.5, 1.5)[..., None]
    col *= (0.8 + 0.4 * fbm(S, S, 5, 5, 4, rng))[..., None]
    rust = sstep(0.62, 0.74, warp(fbm(S, S, 8, 8, 5, rng), S, S, 8, rng))
    col = col * (1 - rust[..., None] * 0.85) + np.array([0.40, 0.22, 0.10]) * rust[..., None] * 0.85
    st = sstep(0.55, 0.85, fbm(S, S, 14, 2, 3, rng)) * 0.20
    col = col * (1 - st[..., None]) + np.array([0.28, 0.17, 0.10]) * st[..., None]
    save(col, "Toilet_Pipe.png")


def make_vent():
    S = 128; rng = np.random.default_rng(88)
    Y, X = np.mgrid[0:S, 0:S].astype(np.float32)
    pitch = 16.0
    d1 = ((X + Y) % pitch) / pitch; d2 = ((X - Y) % pitch) / pitch
    t = 0.30
    bars = ((d1 < t) | (d2 < t)).astype(np.float32)
    bars_s = blur(bars, 0.7)
    col = np.zeros((S, S, 3), np.float32)
    inner = 0.03 + 0.10 * fbm(S, S, 6, 6, 2, rng)                         # looking into the vent
    col[:] = inner[..., None]
    g = luma(lib("Plaster", 20, S)); g = g / g.mean()
    bar_col = np.array([0.56, 0.52, 0.45])[None, None, :] * np.clip(1 + 0.5 * (g - 1), 0.6, 1.4)[..., None]
    bar_col *= (0.8 + 0.4 * fbm(S, S, 4, 4, 3, rng))[..., None]
    shade = 1 + 0.22 * edge_relief(bars_s, 2)                             # bevelled bars: lit one side, dark the other
    col = col * (1 - bars_s[..., None]) + (bar_col * shade[..., None]) * bars_s[..., None]
    dirt = sstep(0.6, 0.85, fbm(S, S, 8, 8, 4, rng)) * 0.4
    col = col * (1 - dirt[..., None]) + np.array([0.30, 0.25, 0.18]) * dirt[..., None] * bars_s[..., None]
    save(col, "Toilet_Vent.png")


# ------------------------------------------------------------------ signs (256 x 96)
def make_signs():
    W, H = 256, 96
    def sign(name, glyphs, fig, seed):
        rng = np.random.default_rng(seed)
        im = Image.new("RGB", (W, H), (222, 212, 190)); d = ImageDraw.Draw(im)
        d.rectangle([0, 0, W - 1, H - 1], outline=(96, 86, 74), width=3)
        d.rectangle([5, 5, W - 6, H - 6], outline=(170, 158, 138), width=1)
        cx = 52
        d.ellipse([cx - 10, 16, cx + 10, 36], fill=fig)
        d.rounded_rectangle([cx - 15, 40, cx + 15, 66], radius=3, fill=fig)
        d.rectangle([cx - 13, 66, cx - 3, 86], fill=fig); d.rectangle([cx + 3, 66, cx + 13, 86], fill=fig)
        d.rectangle([cx - 22, 40, cx - 18, 64], fill=fig); d.rectangle([cx + 18, 40, cx + 22, 64], fill=fig)
        f = ImageFont.truetype(FONT, 58)
        d.text((168, 50), "".join(chr(c) for c in glyphs), font=f, fill=(52, 70, 70), anchor="mm")
        for bx, by in ((12, 12), (W - 13, 12), (12, H - 13), (W - 13, H - 13)):    # bolts
            d.ellipse([bx - 3, by - 3, bx + 3, by + 3], fill=(110, 100, 92)); d.ellipse([bx - 1, by - 1, bx + 1, by + 1], fill=(60, 55, 50))
        a = np.asarray(im, dtype=np.float32) / 255.0
        # faded & chipped enamel: wear takes paint (and the pictogram) down to rusty metal in patches
        wear = sstep(0.68, 0.72, warp(fbm(W, H, 5, 3, 5, rng), W, H, 6, rng))
        ex = np.minimum(np.arange(W), W - 1 - np.arange(W))[None, :]; ey = np.minimum(np.arange(H), H - 1 - np.arange(H))[:, None]; edge = np.minimum(ex, ey).astype(np.float32)
        wear = np.maximum(wear, (1 - sstep(0, 7, edge)) * sstep(0.58, 0.68, fbm(W, H, 14, 8, 3, rng)))
        rustc = np.array([0.42, 0.26, 0.15])[None, None, :] * (0.7 + 0.6 * fbm(W, H, 20, 10, 3, rng))[..., None]
        a = a * (1 - wear[..., None] * 0.85) + rustc * wear[..., None] * 0.85
        a *= (0.82 + 0.3 * fbm(W, H, 6, 3, 4, rng))[..., None]
        sk = sstep(0.55, 0.85, fbm(W, H, 30, 2, 3, rng)) * 0.3                         # grime streaks
        a = a * (1 - sk[..., None]) + np.array([0.30, 0.26, 0.20]) * sk[..., None]
        sc = Image.new("L", (W, H), 0); dd = ImageDraw.Draw(sc)
        for _ in range(10):
            x = rng.uniform(0, W); y = rng.uniform(0, H); ang = rng.uniform(0, 3.14); l = rng.uniform(10, 45)
            dd.line([(x, y), (x + np.cos(ang) * l, y + np.sin(ang) * l)], fill=int(rng.uniform(80, 180)), width=1)
        sc = np.asarray(sc, dtype=np.float32) / 255.0
        a = a * (1 - sc[..., None] * 0.35) + np.array([0.75, 0.74, 0.70]) * sc[..., None] * 0.2
        save(a, name)
    sign("Toilet_SignMen.png", [0xFEDD, 0xFE8E, 0xFE9F, 0xFEAD], (26, 84, 184), 91)
    sign("Toilet_SignWomen.png", [0xFE80, 0xFE8E, 0xFEB4, 0xFEE7], (178, 38, 38), 92)


# ------------------------------------------------------------------ painted wall lettering (RGBA, worn away in patches)
def make_wall_text():
    W, H = 512, 256; rng = np.random.default_rng(101)
    im = Image.new("L", (W, H), 0); d = ImageDraw.Draw(im)
    f1 = ImageFont.truetype(FONT, 96); f2 = ImageFont.truetype(FONT, 70)
    l1 = "".join(chr(c) for c in [0xFE93, 0xFEAD, 0xFEED, 0xFEA9])
    l2 = "".join(chr(c) for c in [0xFEE9, 0xFE8E, 0xFEF4, 0xFEE3]) + " " + "".join(chr(c) for c in [0xFE94, 0xFEE3, 0xFE8E, 0xFECB])
    d.text((W // 2, 66), l1, font=f1, fill=255, anchor="mm"); d.text((W // 2, 188), l2, font=f2, fill=255, anchor="mm")
    m = np.asarray(im, dtype=np.float32) / 255.0
    wear = 1 - sstep(0.60, 0.70, warp(fbm(W, H, 7, 4, 5, rng), W, H, 10, rng))          # patches where the paint has gone
    fine = 0.75 + 0.25 * fbm(W, H, 90, 45, 2, rng)
    alpha = m * wear * fine * 0.88
    col = np.array([0.20, 0.31, 0.27])[None, None, :] * (0.8 + 0.4 * fbm(W, H, 10, 5, 3, rng))[..., None]
    save_rgba(col, alpha, "Toilet_WallText.png")


# ------------------------------------------------------------------ grime overlays (RGBA) for the four walls
def streak_source(a, W, H, x_px, y_px, width, length, strength, rng):
    """A rain streak running down from (x_px, y_px) (y_px counted from the top)."""
    X = np.arange(W, dtype=np.float32)[None, :]; Y = np.arange(H, dtype=np.float32)[:, None]
    wob = (fbm(W, H, 3, 10, 3, rng) - 0.5) * 2
    xc = x_px + wob * width * 0.8
    cross = np.exp(-(((X - xc) / width) ** 2))
    run = np.clip((Y - y_px) / length, 0, 1)
    fall = np.exp(-run * 2.2) * (Y >= y_px)
    fade_in = sstep(0, 0.05, run)
    a += cross * fall * fade_in * strength * (0.6 + 0.8 * fbm(W, H, 2, 14, 3, rng))


def base_grime(W, H, rng, top_streaks=True):
    Y = np.arange(H, dtype=np.float32)[:, None] / (H - 1)                         # 0 top -> 1 bottom
    ragged = fbm(W, H, 14, 3, 5, rng)
    height = 0.22 + 0.12 * (ragged - 0.5) * 2
    splash = sstep(1 - height - 0.03, 1 - height + 0.10, Y + 0.0 * ragged)       # dirt band at the bottom
    a = splash * (0.45 + 0.35 * fbm(W, H, 20, 6, 4, rng)) * 0.75
    a += sstep(1 - 0.05, 1.0, Y) * 0.25                                            # darkest right at the ground
    if top_streaks:
        a += sstep(0.12, 0.0, Y) * 0.30 * fbm(W, H, 40, 2, 3, rng)                # soot under the roof edge
        for _ in range(int(W / 55)):                                              # general rain streaks off the roof
            streak_source(a, W, H, rng.uniform(0, W), 0, rng.uniform(5, 14), rng.uniform(H * 0.25, H * 0.7), rng.uniform(0.08, 0.20), rng)
    a += 0.08 * sstep(0.45, 0.8, fbm(W, H, 5, 3, 4, rng))                          # big blotchy dampness
    return a


def make_grime(name, W, H, seed, features=None, pipe_stain=None):
    rng = np.random.default_rng(seed)
    a = base_grime(W, H, rng)
    if features:
        for (x, y, w, l, s) in features:
            streak_source(a, W, H, x * W, y * H, w, l * H, s, rng)
    if pipe_stain:                                                                # the dripping pipe: damp, dark and slightly green
        px, py = pipe_stain
        X = np.arange(W, dtype=np.float32)[None, :]; Y = np.arange(H, dtype=np.float32)[:, None]
        run = np.clip((Y - py * H) / ((1 - py) * H), 0, 1) * (Y >= py * H)
        width = 6 + 22 * np.sqrt(run)
        cross = np.exp(-(((X - px * W) / width) ** 2))
        damp = cross * (1 - run * 0.35) * (0.35 + 0.5 * fbm(W, H, 3, 12, 4, rng)) * sstep(0.0, 0.03, run)
        a += damp * 0.55
        a += np.exp(-(((X - px * W) / 60) ** 2 + ((Y - (H - 6)) / 34) ** 2)) * 0.30     # soaked wall base
    a = np.clip(a, 0, 0.78)
    col = np.zeros((H, W, 3), np.float32)
    dirt = np.array([0.22, 0.17, 0.11]); dark = np.array([0.10, 0.09, 0.07])
    t = np.clip(a / 0.78, 0, 1)[..., None]
    col[:] = dirt * (1 - t) + dark * t
    if pipe_stain:
        g = np.clip(a - 0.35, 0, 1)[..., None]
        col = col * (1 - g * 0.3) + np.array([0.12, 0.14, 0.09]) * g * 0.3
    save_rgba(col, a, name)


def make_all_grime():
    # front wall (4.6 x 2.5): image x = 0 is the viewer's left = local +X. Doors at x 0.217W / 0.783W, lamp at 0.5W
    W, H = 736, 400
    make_grime("Toilet_GrimeFront.png", W, H, 201, features=[
        (0.217, 0.07, 22, 0.25, 0.32), (0.783, 0.07, 22, 0.25, 0.32),         # drips off the sign boards
        (0.5, 0.20, 10, 0.55, 0.30),                                            # lamp
        (0.04, 0.0, 12, 0.7, 0.28), (0.97, 0.0, 12, 0.7, 0.28),
    ])
    # back wall: image x = 0 is local -X.  vents at 0.217/0.783, conduit/box at 0.5, dripping pipe at (1.95+2.3)/4.6 = 0.924
    make_grime("Toilet_GrimeBack.png", W, H, 202, features=[
        (0.217, 0.18, 26, 0.3, 0.30), (0.783, 0.18, 26, 0.3, 0.30), (0.5, 0.52, 10, 0.4, 0.30),
    ], pipe_stain=(0.924, 1 - (0.85 - 0.25) / 2.5))
    make_grime("Toilet_GrimeSide.png", 512, 400, 203, features=[(0.5, 0.18, 24, 0.35, 0.30), (0.1, 0.0, 14, 0.6, 0.25), (0.88, 0.0, 14, 0.6, 0.25)])
    make_grime("Toilet_GrimeSide2.png", 512, 400, 204, features=[(0.2, 0.0, 14, 0.6, 0.25), (0.75, 0.0, 14, 0.6, 0.25)])


if __name__ == "__main__":
    make_plaster(); print("plaster")
    make_concrete(); print("concrete")
    make_metal_tile(); print("metal")
    make_door(); print("door")
    make_rust(); make_pipe(); make_vent(); print("rust/pipe/vent")
    make_signs(); print("signs")
    make_wall_text(); print("text")
    make_all_grime(); print("grime")
