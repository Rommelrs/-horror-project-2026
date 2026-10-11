# Wet-ground patch for under a dripping pipe (RGBA PNG 1024x1024, 512 px/m -> 2 m x 2 m quad). Dark damp stain with a ragged edge, a slightly
# darker rim where the water stopped spreading, a darker core where the drops land, a few splashed spots around it. Alpha is zero well before
# the texture border so no straight cut shows.
import numpy as np
from PIL import Image, ImageFilter
rng = np.random.default_rng(11)
W = H = 1024
Y, X = np.mgrid[0:H, 0:W].astype(np.float32)

def octave(cell):
    th = rng.uniform(0, np.pi); c, s = np.cos(th), np.sin(th)
    u = (X * c + Y * s) / cell; v = (-X * s + Y * c) / cell
    u -= u.min(); v -= v.min()
    gu, gv = int(u.max()) + 3, int(v.max()) + 3
    lat = rng.random((gv, gu)).astype(np.float32)
    iu = np.floor(u).astype(int); iv = np.floor(v).astype(int)
    fu = u - iu; fv = v - iv
    fu = fu * fu * (3 - 2 * fu); fv = fv * fv * (3 - 2 * fv)
    top = lat[iv, iu] * (1 - fu) + lat[iv, iu + 1] * fu
    bot = lat[iv + 1, iu] * (1 - fu) + lat[iv + 1, iu + 1] * fu
    return (top * (1 - fv) + bot * fv).astype(np.float32)

def fractal(base, octaves=5, gain=0.55):
    tot = np.zeros((H, W), np.float32); amp = 1.0; norm = 0.0; s = base
    for _ in range(octaves):
        tot += amp * octave(max(2, s)); norm += amp; amp *= gain; s /= 2.0
    return tot / norm

def warp(field, amount, base):
    dx = (fractal(base, 3) - 0.5) * amount; dy = (fractal(base * 1.3, 3) - 0.5) * amount
    xs = np.clip(X + dx, 0, W - 1.001); ys = np.clip(Y + dy, 0, H - 1.001)
    x0 = np.floor(xs).astype(int); y0 = np.floor(ys).astype(int); fx = xs - x0; fy = ys - y0
    return (field[y0, x0] * (1 - fx) * (1 - fy) + field[y0, x0 + 1] * fx * (1 - fy)
            + field[y0 + 1, x0] * (1 - fx) * fy + field[y0 + 1, x0 + 1] * fx * fy)

cx = cy = W / 2
# slightly stretched blob, wobbly outline
ang = np.arctan2(Y - cy, X - cx)
r = np.hypot((X - cx) / 1.12, (Y - cy) * 1.0)
wob = 1.0 + 0.14 * (warp(fractal(200, 5), 120, 220) - 0.5) * 2 + 0.12 * (fractal(70, 5) - 0.5) * 2
R = 300.0 * wob
dist = r / R                                       # 1 = edge of the stain
inside = np.clip((1.0 - dist) / 0.13, 0, 1)         # soft but not too soft edge
inside = inside * inside * (3 - 2 * inside)

# rim: a thin darker line just inside the edge, broken up
rim = np.exp(-((dist - 0.9) / 0.07) ** 2) * (0.5 + 1.0 * fractal(50, 4))
# core where the drops land
core = np.exp(-(r / 120.0) ** 2) * (0.7 + 0.5 * fractal(40, 3))
# mottling so the stain is not flat
mott = 0.8 + 0.4 * warp(fractal(150, 4), 90, 200)

alpha = inside * (0.34 * mott) + rim * inside * 0.22 + core * 0.30
# small splashed spots just beyond the edge
spots = (octave(5) > 0.93) * np.clip(1.5 - dist, 0, 1) * np.clip(dist - 0.8, 0, 1) * 4 * 0.5
alpha = np.maximum(alpha, spots)
alpha = np.clip(alpha, 0, 0.72)
edge = np.clip((W / 2 - np.abs(X - W / 2)) / 80.0, 0, 1) * np.clip((H / 2 - np.abs(Y - H / 2)) / 80.0, 0, 1)
alpha *= edge * edge * (3 - 2 * edge)

# colour: dark damp earth, a touch cooler in the middle
rgb = np.zeros((H, W, 3), np.float32)
rgb[..., 0] = 0.115 + 0.02 * mott; rgb[..., 1] = 0.105 + 0.02 * mott; rgb[..., 2] = 0.095 + 0.02 * mott
rgb *= (1.0 - 0.25 * core)[..., None]
img = Image.fromarray((np.dstack([np.clip(rgb, 0, 1), alpha]) * 255).astype("uint8"), "RGBA")
img.putalpha(img.getchannel("A").filter(ImageFilter.GaussianBlur(1.0)))
img.save("D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/Assets/Decals/Wet/WetPatch.png")
bg = Image.new("RGBA", img.size, (120, 114, 106, 255)); bg.alpha_composite(img)
bg.convert("RGB").resize((512, 512)).save("C:/Users/Zayed/AppData/Local/Temp/claude/D--Unity-Stuff-NEWHorrorProject--horror-project-2026/cf51bab8-e583-42ee-8ab4-15af6ac1a2cc/scratchpad/wet_preview.png")
print("saved")
