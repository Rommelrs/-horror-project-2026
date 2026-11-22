# Duct tape + broken wall hole textures for the tape clues in Alley 3 (ripped tape round the whispering hole, a taped-over
# hole on the hanged-man wall). PS2-ish: small, 8-bit palette look, lighting left to the engine.
# Reference: grey cloth duct tape (48 mm, silver poly face over a cotton scrim you can see as a fine grid + lengthwise
# ribs, hand-torn ends = ragged zigzag with loose threads, adhesive back = duller, lighter, fabric texture), dusty fingers.
# Run with system python (NOT -I: numpy/PIL are user site-packages):  python make_duct_tape_textures.py
import math, os
import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Models", "DuctTape", "Textures")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(48)

W, RH, ROWS = 512, 64, 8          # atlas: 8 strips, each 512 x 64 (64 px = 48 mm wide, so a row is ~38 cm long)


def smooth_noise(h, w, cell, seed, octaves=3):
    r = np.random.default_rng(seed)
    out = np.zeros((h, w)); amp = 1.0; tot = 0.0; c = cell
    for _ in range(octaves):
        gh, gw = max(2, int(h / c) + 1), max(2, int(w / c) + 1)
        img = Image.fromarray((r.random((gh, gw)) * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
        out += amp * np.asarray(img, dtype=np.float64) / 255
        tot += amp; amp *= 0.5; c = max(1, c / 2)
    return out / tot


def blur(a, r):
    return np.asarray(Image.fromarray(np.clip(a * 255, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r)), dtype=np.float64) / 255


def mix(base, col, m):
    m = np.clip(m, 0, 1)[..., None]
    return base * (1 - m) + np.array(col, dtype=np.float64) * m


def torn_profile(n, seed, depth, style):
    """per-row-pixel x offset of a torn end (0 = cut line, positive = tape missing). style: 'zig' hand tear, 'rag' ripped"""
    r = np.random.default_rng(seed)
    y = np.arange(n)
    if style == "zig":
        teeth = r.integers(2, 4)
        base = np.abs(((y / n * teeth + r.random()) % 1.0) - 0.5) * 2 * depth * 0.8
    else:
        base = smooth_noise(1, n, 14, seed, 2)[0] * depth * 1.2
    base = base + r.random(n) * depth * 0.12 + smooth_noise(1, n, 5, seed + 1, 1)[0] * depth * 0.25
    return base


def strip(seed, front=True, dirt=0.3, blood=0.0, left="zig", right="zig", tongue=False):
    """one 512x64 RGBA strip; u along the tape, v across it"""
    vv, uu = np.mgrid[0:RH, 0:W]
    if front:
        col = np.ones((RH, W, 3)) * np.array([0.60, 0.61, 0.61])
        col *= (0.9 + 0.18 * smooth_noise(RH, W, 60, seed))[..., None]                 # uneven sheen
        # scrim showing through: lengthwise ribs every 3 px, cross threads every 5 px
        col *= (1 - 0.06 * ((vv % 3) == 0) - 0.035 * ((uu % 5) == 0))[..., None]
        # creases: lengthwise wrinkles where it was pressed down by hand
        small = np.random.default_rng(seed + 3).random((9, 7))          # lengthwise: few rows across, few columns along
        cre = np.asarray(Image.fromarray((small * 255).astype(np.uint8)).resize((W, RH), Image.BICUBIC), dtype=np.float64) / 255
        fine = np.random.default_rng(seed + 4).random((RH, 1)) * np.ones((1, W))
        col *= (0.88 + 0.2 * cre + 0.05 * blur(fine, 0.8))[..., None]
        # darker rolled edges
        e = np.minimum(vv, RH - 1 - vv)
        col *= (1 - 0.22 * (e < 2) - 0.08 * (e == 2))[..., None]
    else:
        col = np.ones((RH, W, 3)) * np.array([0.70, 0.69, 0.64])                       # adhesive: duller, warmer
        col *= (0.92 + 0.12 * smooth_noise(RH, W, 30, seed))[..., None]
        col *= (1 - 0.10 * (((uu + vv) % 4) == 0) - 0.06 * ((vv % 3) == 0))[..., None]
        col = mix(col, (0.55, 0.50, 0.42), smooth_noise(RH, W, 20, seed + 9) * 0.55)   # sand stuck to the glue
    # dust / sand and dirty fingers
    d = np.clip(smooth_noise(RH, W, 40, seed + 5) * 1.6 - 0.55, 0, 1) * dirt
    col = mix(col, (0.48, 0.40, 0.29), d)
    for k in range(int(3 * dirt + 1)):
        cx, cy = rng.uniform(30, W - 30), rng.uniform(8, RH - 8)
        rr = np.hypot((uu - cx) / 14, (vv - cy) / 11)
        ridges = 0.5 + 0.5 * np.sin(rr * 9)
        col = mix(col, (0.32, 0.28, 0.22), np.clip(1 - rr, 0, 1) * ridges * 0.45 * dirt)
    if blood > 0:
        b = np.clip(smooth_noise(RH, W, 26, seed + 7) * 1.8 - 0.95, 0, 1) * blood
        for k in range(5):
            cx, cy = rng.uniform(20, W - 20), rng.uniform(4, RH - 4)
            r0 = rng.uniform(2, 6)
            b = np.maximum(b, np.clip(1.3 - np.hypot(uu - cx, vv - cy) / r0, 0, 1) * blood)
        col = mix(col, (0.36, 0.05, 0.04), b * 0.9)
    # alpha: torn ends + loose threads
    a = np.ones((RH, W))
    lp = torn_profile(RH, seed + 11, 22, left)
    rp = torn_profile(RH, seed + 12, 22, right)
    a[uu < lp[vv]] = 0
    a[(W - 1 - uu) < rp[vv]] = 0
    for side, prof in ((0, lp), (1, rp)):
        for k in range(rng.integers(1, 4)):                      # threads poking out of the tear
            row = int(rng.integers(2, RH - 2))
            ln = int(rng.integers(4, 14))
            x0 = int(prof[row])
            xs = range(max(0, x0 - ln), x0) if side == 0 else range(W - x0, min(W, W - x0 + ln))
            for x in xs:
                a[row, x] = 1
                col[row, x] = (0.78, 0.76, 0.70)
    if tongue:                                                    # ripped lengthwise: the far half tore away
        cut = (RH * 0.45 + smooth_noise(1, W, 30, seed + 13, 2)[0] * RH * 0.35).astype(int)
        far = uu > W * 0.55
        a[far & (vv > cut[uu])] = 0
    # edge darkening on the tear + a little alpha noise for a ragged look
    ab = blur(a, 1.2)
    col *= (0.75 + 0.25 * ab)[..., None]
    return col, a


def finish_rgba(rgb, a, name, colors=128):
    rgb8 = Image.fromarray(np.clip(rgb[::-1] * 255, 0, 255).astype(np.uint8))
    rgb8 = rgb8.quantize(colors=colors, method=Image.MEDIANCUT, dither=Image.FLOYDSTEINBERG).convert("RGB")
    al = Image.fromarray(((a[::-1] > 0.5) * 255).astype(np.uint8))
    rgb8.putalpha(al)
    rgb8.save(os.path.join(OUT, name))
    print("wrote", name, rgb8.size)


# ---------------- atlas: rows from the bottom (v 0) up ----------------
specs = [
    dict(front=True, dirt=0.25),                                  # 0 clean-ish, zigzag tears
    dict(front=True, dirt=0.45, left="rag"),                     # 1 dusty
    dict(front=True, dirt=0.30, right="rag"),                    # 2
    dict(front=True, dirt=0.60, left="rag", right="rag"),        # 3 dirty
    dict(front=False, dirt=0.40),                                 # 4 adhesive back
    dict(front=True, dirt=0.55, blood=0.6, right="rag", tongue=True),   # 5 ripped remnant, bloody fingers
    dict(front=True, dirt=0.65, blood=0.35, left="rag", right="rag"),   # 6 ripped remnant
    dict(front=False, dirt=0.70, right="rag", tongue=True),      # 7 adhesive back, ripped
]
rgb = np.zeros((ROWS * RH, W, 3)); alpha = np.zeros((ROWS * RH, W))
for i, sp in enumerate(specs):
    c, a = strip(100 + i * 17, **sp)
    rgb[i * RH:(i + 1) * RH] = c
    alpha[i * RH:(i + 1) * RH] = a
finish_rgba(rgb, alpha, "DuctTape_Atlas.png")

# ---------------- broken hole in plastered brick (behind the tape) ----------------
N = 256
vv, uu = np.mgrid[0:N, 0:N]
x = (uu + 0.5) / N * 2 - 1
y = (vv + 0.5) / N * 2 - 1
ang = np.arctan2(y, x)
rad = np.hypot(x, y)
# irregular punched-through outline (5-6 lobes like the whispering hole) + chipped bricks
shape = 0.56 * (1 + 0.09 * np.sin(ang * 3 + 0.7) + 0.06 * np.sin(ang * 5 + 2.1) + 0.025 * np.sin(ang * 13 + 1.0))
chip = smooth_noise(N, N, 12, 3, 2)
hole_edge = shape + 0.06 * (chip - 0.5)
inside = rad < hole_edge
ring = (rad >= hole_edge) & (rad < hole_edge + 0.16 + 0.12 * smooth_noise(N, N, 18, 4, 2))
col = np.zeros((N, N, 3))
depth = np.clip(rad / np.maximum(hole_edge, 1e-3), 0, 1)
col[:] = np.array([0.025, 0.02, 0.018])
col = mix(col, (0.14, 0.12, 0.10), np.clip((depth - 0.82) / 0.18, 0, 1) * 0.9 * inside)            # broken brick faces just inside the rim
mortar = smooth_noise(N, N, 8, 6, 2)
col = np.where(ring[..., None], (np.array([0.46, 0.44, 0.41]) * (0.85 + 0.25 * mortar)[..., None]), col)  # crushed plaster
col = mix(col, (0.30, 0.27, 0.24), ring * (smooth_noise(N, N, 5, 7, 1) > 0.62) * 0.8)                  # broken brick bits
a = (inside | ring).astype(np.float64)
a = np.where(blur(a, 1.5) > 0.5, 1.0, 0.0)
finish_rgba(col, a, "TapedHole_Back.png", colors=64)
