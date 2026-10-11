# Wind-blown sand dusting for laying on grey paving (RGBA PNG 1024x2048, 341 px/m -> 3 m x 6 m quad).
# v2: no band, no hair streaks. Irregular, patchy, semi-transparent sand: big soft blotches (domain-warped fractal noise), thicker clumps
# collected in places, thin film elsewhere, a ragged edge and loose grains blown beyond it, very faint wind ripples. Start at the BOTTOM,
# far end at the TOP where it thins out. Colours are dull and desaturated so it sits in the paving instead of glowing on top of it.
import numpy as np
from PIL import Image, ImageFilter
rng = np.random.default_rng(5)
W, H = 1024, 2048
Y, X = np.mgrid[0:H, 0:W].astype(np.float32)

def octave(cell_x, cell_y):                     # smooth random field 0..1, features cell_x by cell_y px, on a randomly rotated grid
    th = rng.uniform(0, np.pi)
    c, sn = np.cos(th), np.sin(th)
    u = (X * c + Y * sn) / cell_x; v = (-X * sn + Y * c) / cell_y
    u -= u.min(); v -= v.min()
    gu, gv = int(u.max()) + 3, int(v.max()) + 3
    lat = rng.random((gv, gu)).astype(np.float32)
    iu = np.floor(u).astype(int); iv = np.floor(v).astype(int)
    fu = u - iu; fv = v - iv
    fu = fu * fu * (3 - 2 * fu); fv = fv * fv * (3 - 2 * fv)
    top = lat[iv, iu] * (1 - fu) + lat[iv, iu + 1] * fu
    bot = lat[iv + 1, iu] * (1 - fu) + lat[iv + 1, iu + 1] * fu
    return (top * (1 - fv) + bot * fv).astype(np.float32)

def fractal(base, aspect=1.4, octaves=6, gain=0.55):
    tot = np.zeros((H, W), np.float32); amp = 1.0; norm = 0.0; s = base
    for _ in range(octaves):
        tot += amp * octave(max(2, s), max(2, s * aspect)); norm += amp; amp *= gain; s /= 2.0
    return tot / norm

def warp(field, amount, base):                  # push the pattern around so nothing looks like stacked noise layers
    dx = (fractal(base, 1.0, 3) - 0.5) * amount; dy = (fractal(base * 1.3, 1.0, 3) - 0.5) * amount
    xs = np.clip(X + dx, 0, W - 1.001); ys = np.clip(Y + dy, 0, H - 1.001)
    x0 = np.floor(xs).astype(int); y0 = np.floor(ys).astype(int); fx = xs - x0; fy = ys - y0
    return (field[y0, x0] * (1 - fx) * (1 - fy) + field[y0, x0 + 1] * fx * (1 - fy)
            + field[y0 + 1, x0] * (1 - fx) * fy + field[y0 + 1, x0 + 1] * fx * fy)

t = 1.0 - Y / (H - 1)                           # 0 at the start, 1 at the far end
# overall footprint of the drift: a wide, ragged, slowly meandering body that narrows and dies out
cx = W / 2 + 55 * np.sin(2 * np.pi * (Y / 2300) + 0.5)
half = W * (0.40 - 0.13 * t)
d = (X - cx) / half
envelope = np.exp(-np.abs(d) ** 2.2) * np.clip(1.0 - t ** 2.2, 0, 1)

blot = warp(fractal(300, 1.5, 6), 260, 400)     # big soft blotches
clump = warp(fractal(120, 1.3, 5), 120, 160)    # thicker clumps inside them
density = envelope * (0.35 + 1.5 * blot ** 1.3) * (0.7 + 0.8 * clump)

fine = 0.6 * octave(2, 2) + 0.4 * octave(4, 4)  # ragged, grainy edges
alpha = np.clip((density + (fine - 0.5) * 0.75 - 0.24) / 0.34, 0, 1) ** 1.15
alpha *= 0.82 * (0.78 + 0.44 * octave(1, 1))                                    # never fully opaque: the paving always shows through

# loose grains blown past the edge, more of them toward the far end
dist = np.clip(1.25 - np.abs(d) * 0.5, 0, 1)
stray = (octave(1, 1) > (0.955 - 0.03 * t)) * dist * (0.3 + 0.7 * t ** 0.5) * 0.55
alpha = np.maximum(alpha, stray)

# faint wind ripples (crescent-ish bands across the flow), only where there is sand
ripple = 0.5 + 0.5 * np.sin(2 * np.pi * (Y / 46.0 + 1.6 * fractal(260, 1.0, 3)))
# (no ripple bands: they read as seams when the decal is seen up close)

alpha *= np.clip((H - 1 - Y) / 220.0, 0, 1) ** 1.3          # soft start
alpha *= np.clip(Y / 120.0, 0, 1)                            # soft end
edge_x = np.clip((W / 2 - np.abs(X - W / 2)) / (0.16 * W), 0, 1); alpha *= edge_x * edge_x * (3 - 2 * edge_x)   # fully clear before the left/right edges

# colour: dull sand, lighter on thick clumps, darker/greyer in the thin film; grain brightness
thick = np.clip(density * 1.3, 0, 1)[..., None]
dark = np.array([0.37, 0.31, 0.24]); mid = np.array([0.50, 0.42, 0.31]); lite = np.array([0.60, 0.51, 0.38])
rgb = np.where(thick < 0.5, dark + (mid - dark) * (thick / 0.5), mid + (lite - mid) * ((thick - 0.5) / 0.5))
rgb = rgb * (0.88 + 0.24 * octave(1, 1))[..., None] * (0.94 + 0.12 * fractal(70, 1.0, 3))[..., None]
rgba = np.dstack([np.clip(rgb, 0, 1), alpha])
img = Image.fromarray((rgba * 255).astype("uint8"), "RGBA")
img.putalpha(img.getchannel("A").filter(ImageFilter.GaussianBlur(0.7)))
img.save("D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/Assets/Decals/Sand/SandDrift.png")
bg = Image.new("RGBA", img.size, (118, 112, 104, 255)); bg.alpha_composite(img)
bg.convert("RGB").resize((384, 768)).save("C:/Users/Zayed/AppData/Local/Temp/claude/D--Unity-Stuff-NEWHorrorProject--horror-project-2026/cf51bab8-e583-42ee-8ab4-15af6ac1a2cc/scratchpad/drift_preview.png")
print("saved")
