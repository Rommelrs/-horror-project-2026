# v3: re-tuned against the concept sheet's swatches (colours measured from the sheet itself). The sheet's materials are pale, desaturated
# and quiet: warm off-white plaster with fine speckle and small ragged chips, grey-beige concrete with a few pits, dull grey-green painted
# metal with only light rust speckle, chunky concrete diamond vent blocks, grey fixtures with orange rust spots. v2 was far too saturated,
# too contrasty and had big camouflage-like blotches, so v3 keeps everything low-contrast and fine-grained.
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import make_toilet_textures_v2 as v2
OUT_DIR = v2.OUT
from make_toilet_textures_v2 import lib, luma, sstep, fbm, warp, blur, edge_relief, cracks, save, save_rgba, FONT


def grain_of(cat, n, size):
    g = luma(lib(cat, n, size)); return g / g.mean()


def tint(base, grain, amount, lo=0.7, hi=1.3):
    return np.array(base)[None, None, :] * np.clip(1 + amount * (grain - 1), lo, hi)[..., None]


def make_plaster():
    S = 512; rng = np.random.default_rng(31)
    col = tint((0.775, 0.700, 0.585), grain_of("Plaster", 19, S), 0.35)
    col *= (0.96 + 0.08 * fbm(S, S, 4, 4, 4, rng))[..., None]                       # very gentle sun-fading
    # small ragged chips: slightly darker brown-grey where the paint has flaked, and a few smaller specks of bare plaster
    c = warp(fbm(S, S, 24, 24, 6, rng), S, S, 8, rng)
    chip = sstep(0.725, 0.75, c)
    chip2 = sstep(0.78, 0.80, warp(fbm(S, S, 30, 30, 4, rng), S, S, 6, rng))
    m = np.maximum(chip, chip2)
    chip_col = tint((0.60, 0.54, 0.45), grain_of("Plaster", 1, S), 0.5)
    col = col * (1 - m[..., None] * 0.75) + chip_col * m[..., None] * 0.75
    col += (edge_relief(blur(m, 0.7), 1) * 0.045)[..., None]
    speck = sstep(0.80, 0.86, fbm(S, S, 170, 170, 2, rng))                           # fine dark speckle
    col *= (1 - 0.22 * speck)[..., None]
    st = sstep(0.55, 0.85, fbm(S, S, 26, 3, 4, rng)) * 0.07                         # barely-there rain streaks
    col *= (1 - st)[..., None]
    cr = cracks(S, S, rng, 3, (50, 140), 0.22)                                       # a couple of faint hairlines
    col = col * (1 - cr[..., None]) + np.array([0.45, 0.40, 0.34]) * cr[..., None]
    save(col, "Toilet_Plaster.png")


def make_concrete():
    S = 512; rng = np.random.default_rng(32)
    col = tint((0.725, 0.675, 0.605), grain_of("Plaster", 20, S), 0.45)
    col *= (0.95 + 0.10 * fbm(S, S, 5, 5, 4, rng))[..., None]
    pits = sstep(0.83, 0.90, fbm(S, S, 120, 120, 2, rng))
    col *= (1 - 0.40 * pits)[..., None]
    big = sstep(0.60, 0.80, fbm(S, S, 7, 7, 4, rng)) * 0.10                          # faint damp patches
    col *= (1 - big)[..., None]
    save(col, "Toilet_Concrete.png")


def make_metal_tile():
    S = 256; rng = np.random.default_rng(33)
    col = tint((0.385, 0.415, 0.395), grain_of("Metal", 17, S), 0.35)
    col *= (0.95 + 0.10 * fbm(S, S, 4, 4, 4, rng))[..., None]
    rust = sstep(0.68, 0.74, warp(fbm(S, S, 48, 48, 4, rng), S, S, 5, rng)) * 0.65
    rust_col = tint((0.42, 0.28, 0.17), grain_of("Metal", 11, S), 0.5)
    col = col * (1 - rust[..., None]) + rust_col * rust[..., None]
    save(col, "Toilet_Metal.png")


def make_door():
    W, H = 256, 512; rng = np.random.default_rng(34)
    Y, X = np.mgrid[0:H, 0:W].astype(np.float32); vv = 1 - Y / (H - 1); u = X / (W - 1)
    gr = luma(lib("Metal", 17, 512))[:H, :W]; gr = gr / gr.mean()
    col = tint((0.385, 0.415, 0.395), gr, 0.35)
    col *= (0.95 + 0.10 * fbm(W, H, 3, 5, 4, rng))[..., None]
    streak = sstep(0.55, 0.85, fbm(W, H, 20, 2, 3, rng)) * 0.10                      # faint vertical grime
    col *= (1 - streak)[..., None]
    panel = ((u > 0.09) & (u < 0.91) & (vv > 0.09) & (vv < 0.91)).astype(np.float32)
    col += (edge_relief(blur(panel, 1.0), 3) * 0.06)[..., None]                       # subtle recessed-panel line
    # light rust speckle, denser toward the bottom and along the edges
    edge_d = np.minimum.reduce([u, 1 - u, vv, 1 - vv])
    bias = (1 - sstep(0.0, 0.35, vv)) * 0.5 + (1 - sstep(0.0, 0.04, edge_d)) * 0.35
    n = warp(fbm(W, H, 24, 36, 5, rng), W, H, 6, rng)
    rm = sstep(0.62, 0.68, n + bias * 0.35) * 0.8
    rust_col = tint((0.40, 0.26, 0.15), grain_of("Metal", 11, 512)[:H, :W], 0.5)
    col = col * (1 - rm[..., None]) + rust_col * rm[..., None]
    sk = sstep(0.55, 0.85, fbm(W, H, 18, 2, 3, rng)) * (1 - sstep(0.0, 0.4, vv)) * 0.18   # rust runs below the speckle
    col = col * (1 - sk[..., None]) + np.array([0.33, 0.24, 0.16]) * sk[..., None]
    from PIL import ImageFilter
    sc = Image.new("L", (W, H), 0); d = ImageDraw.Draw(sc)
    for _ in range(14):
        x = rng.uniform(0, W); y = rng.uniform(0, H); a = rng.normal(1.57, 0.6); l = rng.uniform(12, 50)
        d.line([(x, y), (x + np.cos(a) * l, y + np.sin(a) * l)], fill=int(rng.uniform(70, 150)), width=1)
    sc = np.asarray(sc.filter(ImageFilter.GaussianBlur(0.4)), dtype=np.float32) / 255.0
    col = col * (1 - sc[..., None] * 0.25) + np.array([0.60, 0.60, 0.56]) * sc[..., None] * 0.18
    hole = sstep(0.0, 0.02, u - 0.26) * sstep(0.0, 0.02, 0.74 - u) * sstep(0.0, 0.01, vv - 0.10) * sstep(0.0, 0.01, 0.245 - vv)
    col = col * (1 - hole[..., None] * 0.93) + np.array([0.03, 0.03, 0.03]) * hole[..., None] * 0.93
    save(col, "Toilet_Door.png")


def make_rust():                       # fixtures: grey metal with orange rust spots (not solid orange)
    S = 256; rng = np.random.default_rng(35)
    col = tint((0.395, 0.375, 0.325), grain_of("Metal", 18, S), 0.4)
    col *= (0.92 + 0.16 * fbm(S, S, 5, 5, 4, rng))[..., None]
    r = sstep(0.62, 0.70, warp(fbm(S, S, 40, 40, 5, rng), S, S, 6, rng))
    rust_col = tint((0.43, 0.27, 0.15), grain_of("Metal", 13, S), 0.6)
    col = col * (1 - r[..., None] * 0.7) + rust_col * r[..., None] * 0.7
    save(col, "Toilet_Rust.png")


def make_pipe():
    S = 256; rng = np.random.default_rng(36)
    col = tint((0.42, 0.42, 0.395), grain_of("Metal", 18, S), 0.45)
    col *= (0.92 + 0.16 * fbm(S, S, 5, 5, 4, rng))[..., None]
    r = sstep(0.66, 0.73, warp(fbm(S, S, 16, 16, 5, rng), S, S, 8, rng)) * 0.8
    rust_col = tint((0.45, 0.28, 0.15), grain_of("Metal", 13, S), 0.5)
    col = col * (1 - r[..., None]) + rust_col * r[..., None]
    save(col, "Toilet_Pipe.png")


def make_vent():                       # chunky concrete diamond lattice with deep dark openings
    S = 128; rng = np.random.default_rng(37)
    Y, X = np.mgrid[0:S, 0:S].astype(np.float32)
    pitch = 64.0; t = 0.36
    bars = (((X + Y) % pitch) / pitch < t) | (((X - Y) % pitch) / pitch < t)
    bars = blur(bars.astype(np.float32), 0.8)
    inner = (0.03 + 0.05 * fbm(S, S, 4, 4, 2, rng))[..., None] * np.ones(3)
    g = grain_of("Plaster", 20, S)
    bar_col = tint((0.66, 0.60, 0.50), g, 0.5)
    bar_col *= (0.9 + 0.2 * fbm(S, S, 4, 4, 3, rng))[..., None]
    shade = 1 + 0.30 * edge_relief(bars, 3)                                           # bevel: lit one side, dark the other
    col = inner * (1 - bars[..., None]) + (bar_col * shade[..., None]) * bars[..., None]
    save(col, "Toilet_Vent.png")


def patch_signs():
    """Same signs as v2 but with much lighter wear (only the odd rust fleck at the edges)."""
    import re
    src = open(v2.__file__, encoding="utf-8").read()
    src = src.replace("wear = sstep(0.68, 0.72, warp(fbm(W, H, 5, 3, 5, rng), W, H, 6, rng))", "wear = sstep(0.80, 0.83, warp(fbm(W, H, 5, 3, 5, rng), W, H, 6, rng))")
    src = src.replace("sstep(0.58, 0.68, fbm(W, H, 14, 8, 3, rng)))", "sstep(0.70, 0.78, fbm(W, H, 14, 8, 3, rng)))")
    src = src.replace("a = a * (1 - wear[..., None] * 0.85) + rustc * wear[..., None] * 0.85", "a = a * (1 - wear[..., None] * 0.6) + rustc * wear[..., None] * 0.6")
    src = src.replace("a *= (0.82 + 0.3 * fbm(W, H, 6, 3, 4, rng))[..., None]", "a *= (0.90 + 0.14 * fbm(W, H, 6, 3, 4, rng))[..., None]")
    src = src.replace("* 0.3                         # grime streaks", "* 0.15                        # grime streaks")
    ns = {"__name__": "v2sign"}
    exec(compile(src.replace('if __name__ == "__main__":', "if False:"), "v2patched", "exec"), ns)
    ns["make_signs"]()


def make_wall_text():
    W, H = 512, 256; rng = np.random.default_rng(38)
    im = Image.new("L", (W, H), 0); d = ImageDraw.Draw(im)
    f1 = ImageFont.truetype(FONT, 96); f2 = ImageFont.truetype(FONT, 70)
    l1 = "".join(chr(c) for c in [0xFE93, 0xFEAD, 0xFEED, 0xFEA9])
    l2 = "".join(chr(c) for c in [0xFEE9, 0xFE8E, 0xFEF4, 0xFEE3]) + " " + "".join(chr(c) for c in [0xFE94, 0xFEE3, 0xFE8E, 0xFECB])
    d.text((W // 2, 66), l1, font=f1, fill=255, anchor="mm"); d.text((W // 2, 188), l2, font=f2, fill=255, anchor="mm")
    m = np.asarray(im, dtype=np.float32) / 255.0
    wear = 1 - sstep(0.64, 0.74, warp(fbm(W, H, 8, 4, 5, rng), W, H, 8, rng))
    alpha = m * wear * (0.8 + 0.2 * fbm(W, H, 90, 45, 2, rng)) * 0.78
    col = np.array([0.36, 0.44, 0.37])[None, None, :] * (0.9 + 0.2 * fbm(W, H, 10, 5, 3, rng))[..., None]
    save_rgba(col, alpha, "Toilet_WallText.png")


# ---- wall overlays: lighter dirt, plus ragged brown-grey paint chips that get denser toward the ground
def overlay(name, W, H, seed, streaks, pipe=None, holes=(), wall_w=4.6):
    rng = np.random.default_rng(seed)
    Y = np.arange(H, dtype=np.float32)[:, None] / (H - 1)
    # chips: density rises toward the bottom (and a little at the very top under the roof edge)
    density = 0.08 + 0.50 * sstep(0.30, 1.0, Y) + 0.10 * (1 - sstep(0.0, 0.12, Y))
    n = warp(fbm(W, H, int(W / 38), int(H / 38), 6, rng), W, H, 14, rng)
    chip = sstep(1.0 - density * 0.95 - 0.02, 1.0 - density * 0.95 + 0.02, n)
    chip = np.clip(chip, 0, 1)
    edge = edge_relief(blur(chip, 0.8), 2)
    a = chip * 0.80
    col = np.zeros((H, W, 3), np.float32)
    col[:] = np.array([0.52, 0.44, 0.34]) * (0.9 + 0.2 * fbm(W, H, 20, 8, 3, rng))[..., None]
    col += (edge * 0.08)[..., None]
    # dirt: soft darkening at the base and under the roof edge, light vertical streaks
    d = np.zeros((H, W), np.float32)
    d += sstep(0.80, 1.0, Y) * 0.35 * (0.6 + 0.8 * fbm(W, H, 18, 4, 3, rng))
    d += sstep(0.10, 0.0, Y) * 0.15 * fbm(W, H, 30, 2, 3, rng)
    for _ in range(int(W / 70)):
        v2.streak_source(d, W, H, rng.uniform(0, W), 0, rng.uniform(6, 16), rng.uniform(H * 0.25, H * 0.65), rng.uniform(0.05, 0.12), rng)
    for (x, y, w, l, s) in streaks:
        v2.streak_source(d, W, H, x * W, y * H, w, l * H, s * 0.55, rng)
    if pipe:
        px, py = pipe
        X = np.arange(W, dtype=np.float32)[None, :]; YY = np.arange(H, dtype=np.float32)[:, None]
        run = np.clip((YY - py * H) / ((1 - py) * H), 0, 1) * (YY >= py * H)
        width = 6 + 18 * np.sqrt(run)
        damp = np.exp(-(((X - px * W) / width) ** 2)) * (0.4 + 0.5 * fbm(W, H, 3, 12, 4, rng)) * sstep(0.0, 0.03, run)
        d += damp * 0.50
        d += np.exp(-(((X - px * W) / 60) ** 2 + ((YY - (H - 6)) / 34) ** 2)) * 0.25
    d = np.clip(d, 0, 0.55)
    dirt_col = np.array([0.20, 0.16, 0.11])
    # final image at 2x: the paint chips are filled with the real concrete photo (IMG_2021) at its true size
    S2 = 2; W2, H2 = W * S2, H * S2
    up = lambda arr: np.asarray(Image.fromarray(arr.astype(np.float32), mode="F").resize((W2, H2), Image.BICUBIC), dtype=np.float32)
    a2 = np.clip(up(a), 0, 1); d2 = np.clip(up(d), 0, 1); e2 = up(edge)
    conc = Image.open(OUT_DIR + "Toilet_ConcretePhoto.png").convert("RGB")
    tile_px = max(16, int(round(1.25 * W2 / wall_w)))
    conc = np.asarray(conc.resize((tile_px, tile_px), Image.LANCZOS), dtype=np.float32) / 255.0
    conc = np.tile(conc, (H2 // tile_px + 2, W2 // tile_px + 2, 1))[:H2, :W2]
    chip_col = conc * np.array([0.86, 0.85, 0.84]) + (e2 * 0.10)[..., None]
    tot = a2 + d2 * (1 - a2)
    col = np.where(tot[..., None] > 1e-4, (chip_col * a2[..., None] + dirt_col * (d2 * (1 - a2))[..., None]) / np.maximum(tot[..., None], 1e-4), dirt_col)
    # no overlay inside the recessed door / vent openings (the real geometry sits behind the wall plane)
    X = np.arange(W2, dtype=np.float32)[None, :] / (W2 - 1); Yf = np.arange(H2, dtype=np.float32)[:, None] / (H2 - 1)
    for (x0, x1, y0, y1) in holes:
        m = (sstep(x0 - 0.004, x0 + 0.004, X) * sstep(x1 + 0.004, x1 - 0.004, X) * sstep(y0 - 0.006, y0 + 0.006, Yf) * sstep(y1 + 0.006, y1 - 0.006, Yf))
        tot = tot * (1 - m)
    save_rgba(col, tot, name)


def make_overlays():
    W, H = 736, 400
    doors = [(0.217 - 0.115, 0.217 + 0.115, 0.205, 1.01), (0.783 - 0.115, 0.783 + 0.115, 0.205, 1.01)]
    overlay("Toilet_GrimeFront.png", W, H, 301, [(0.217, 0.07, 22, 0.22, 0.30), (0.783, 0.07, 22, 0.22, 0.30), (0.5, 0.20, 10, 0.45, 0.28)], holes=doors)
    vents = [(0.217 - 0.105, 0.217 + 0.105, 0.15, 0.29), (0.783 - 0.105, 0.783 + 0.105, 0.15, 0.29)]
    overlay("Toilet_GrimeBack.png", W, H, 302, [(0.217, 0.18, 26, 0.28, 0.28), (0.783, 0.18, 26, 0.28, 0.28)], pipe=(0.924, 1 - (0.85 - 0.25) / 2.5), holes=vents)
    overlay("Toilet_GrimeSide.png", 512, 400, 303, [(0.5, 0.18, 24, 0.3, 0.28)], holes=[(0.5 - 0.155, 0.5 + 0.155, 0.15, 0.29)], wall_w=3.2)
    overlay("Toilet_GrimeSide2.png", 512, 400, 304, [], wall_w=3.2)


import sys
if __name__ == "__main__" and "--overlays" in sys.argv:
    make_overlays(); print("overlays done"); sys.exit(0)
if __name__ == "__main__":
    make_plaster(); make_concrete(); make_metal_tile(); make_door(); make_rust(); make_pipe(); make_vent()
    patch_signs(); make_wall_text(); make_overlays(); print("v3 done")
