# Blood painted into the WallsHole wall (Alley 3 WallHoleInteractable), matched to the user's reference shot.  Run inside Blender.
#  - WallHoleBlood.png      : 512x1088, 320 px/m. Projected by the wall shader (HorrorProject/Damaged Brick Wall, _BloodRect) over world
#                             x -19.44..-17.84, y 0.85..4.25.  Column 0 = world maxX (the +X side, screen-LEFT when looking at the wall).
#                             The hole interior is filled so the hole's inner rim faces pick up blood too (shader fades it with depth).
#  - WallHoleBloodFloor.png : 256x256, 128 px/m, stippled cut-out stain at the wall base.
# Reference look: dark maroon, irregular grainy rim that follows the hexagonal hole, heavier on the screen-left side,
# a curtain of many fine vertical streaks running from the bottom of the hole to the ground, grainy splatter on the floor.
import numpy as np, os, bpy

OUT_DIR = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\Assets\Textures"
rng = np.random.default_rng(118)

MINX, MAXX, MINY, MAXY = -19.44, -17.84, 0.85, 4.25
PPM = 320.0
W, H = 512, 1088
def to_px(X, Y): return ((MAXX - X) * PPM, (MAXY - Y) * PPM)

# hexagonal hole outline, taken from the WallsHole mesh rim (world x, y)
HOLE = [(-18.736, 3.473), (-18.267, 3.322), (-18.355, 2.977), (-18.686, 2.785), (-18.959, 2.926), (-18.904, 3.213)]
poly = np.array([to_px(x, y) for x, y in HOLE], np.float32)

def vnoise(h, w, cy, cx=None):
    cx = cx or cy
    g = rng.random((int(h / cy) + 3, int(w / cx) + 3)); ys = np.linspace(0, h / cy, h); xs = np.linspace(0, w / cx, w)
    y0 = ys.astype(int); x0 = xs.astype(int); fy = (ys - y0)[:, None]; fx = (xs - x0)[None, :]
    fy = fy * fy * (3 - 2 * fy); fx = fx * fx * (3 - 2 * fx)
    return (g[y0][:, x0] * (1 - fx) + g[y0][:, x0 + 1] * fx) * (1 - fy) + (g[y0 + 1][:, x0] * (1 - fx) + g[y0 + 1][:, x0 + 1] * fx) * fy

def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)

yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)

# signed distance (px) to the hexagon: negative inside
dmin = np.full((H, W), 1e9, np.float32); inside = np.zeros((H, W), bool)
for i in range(len(poly)):
    (x1, y1), (x2, y2) = poly[i], poly[(i + 1) % len(poly)]
    ex, ey = x2 - x1, y2 - y1
    t = np.clip(((xx - x1) * ex + (yy - y1) * ey) / (ex * ex + ey * ey), 0, 1)
    dmin = np.minimum(dmin, np.hypot(xx - (x1 + t * ex), yy - (y1 + t * ey)))
    inside ^= ((y1 > yy) != (y2 > yy)) & (xx < (x2 - x1) * (yy - y1) / (y2 - y1 + 1e-9) + x1)
sd = np.where(inside, -dmin, dmin)
cxp, cyp = poly[:, 0].mean(), poly[:, 1].mean()
ang = np.arctan2(yy - cyp, xx - cxp)           # 0 = px-right (= world -X, screen right), pi = px-left (= world +X, screen left)

# ---- rim: grainy irregular band following the hexagon. heavy on the screen-left (+X) side and lower-left, thin on top/right
left = np.clip(-np.cos(ang), 0, 1); low = np.clip(np.sin(ang), 0, 1)
width = (0.04 + 0.10 * left ** 1.2 + 0.05 * low * left + 0.03 * vnoise(H, W, 18)) * PPM
grain = vnoise(H, W, 2.5) * 0.55 + vnoise(H, W, 7) * 0.45
rim = smooth(0.30, 0.62, (1 - sd / width) * 0.85 + (grain - 0.5) * 0.75)
rim *= (sd > -1)
rim_a = rim * (0.9 + 0.1 * vnoise(H, W, 5))
alpha = rim_a.copy()
thick = rim * smooth(0.0, 0.6, 1 - sd / width)
alpha[inside] = 0.95; thick[inside] = 1.0       # fills the cut so the inner rim faces sample blood

# ---- curtain of fine streaks below the hole (screen-left two thirds of the bottom), down to the ground
def bottom_y(px):                                 # lowest hole edge y (px) at a given column
    ys = yy[:, 0]
    col = inside[:, int(np.clip(px, 0, W - 1))]
    idx = np.nonzero(col)[0]
    return idx.max() if len(idx) else None
x_lo, x_hi = to_px(-18.33, 0)[0], to_px(-18.82, 0)[0]          # px range (screen-left .. bottom vertex and a bit past it)
xc_heavy = to_px(-18.55, 0)[0]
ground = H - 2

def blur(a, r):
    """separable box blur applied 3x ~ gaussian, radius r px"""
    r = int(max(1, r)); n = 2 * r + 1
    for _ in range(3):
        for ax in (0, 1):
            pad = [(r + 1, r) if i == ax else (0, 0) for i in range(2)]
            c = np.cumsum(np.pad(a, pad, mode='edge'), axis=ax)
            a = (np.take(c, np.arange(n, c.shape[ax]), axis=ax) - np.take(c, np.arange(0, c.shape[ax] - n), axis=ax)) / n
    return a

# a handful of real drips (not a curtain): varied thickness and length, a couple reach the ground, softened afterwards
drip = np.zeros((H, W), np.float32)
vlong = vnoise(H, W, 45, 6)                       # patchy opacity along each run
specs = [  # (x offset in metres from the heavy centre, width px, length m, strength)
    (+0.17, 9.0, 2.35, 1.00), (+0.10, 6.0, 1.10, 0.95), (+0.04, 12.0, 2.20, 1.00), (-0.02, 4.0, 0.55, 0.85),
    (-0.07, 7.5, 1.55, 0.95), (-0.13, 5.0, 0.80, 0.90), (-0.19, 10.0, 2.30, 0.95), (-0.24, 3.5, 0.40, 0.80),
    (+0.13, 3.0, 0.35, 0.75), (-0.10, 2.8, 1.90, 0.70),
]
for k, (dxm, wid, Lm, st) in enumerate(specs):
    x0 = xc_heavy - dxm * PPM + rng.uniform(-3, 3)
    y0 = bottom_y(x0)
    if y0 is None: continue
    y0 -= 3
    L = min(Lm * PPM * rng.uniform(0.92, 1.08), ground - y0)
    ph = rng.uniform(0, 6)
    ys = np.arange(int(y0), int(y0 + L))
    t = (ys - y0) / max(L, 1)
    xs = x0 + np.sin((ys - y0) * 0.012 + ph) * 1.2 + (ys - y0) * rng.uniform(-0.004, 0.004)
    wv = wid * (1.3 - 0.75 * t) * (1.0 + 0.12 * np.sin((ys - y0) * 0.05 + ph))
    for ox in range(-9, 10):
        cols = np.clip(np.round(xs + ox).astype(int), 0, W - 1)
        cover = np.clip(wv * 0.5 + 0.5 - np.abs(cols - xs), 0, 1)
        drip[ys, cols] = np.maximum(drip[ys, cols], cover * st * (1.0 - 0.6 * t) * (0.55 + 0.45 * vlong[ys, cols]))
    # rounded drop at the end of the run
    ey, ex = y0 + L, xs[-1] if len(xs) else x0
    if ey < ground - 6:                           # small soft bead only where the run stopped on the wall
        m = (xx - ex) ** 2 + ((yy - ey) * 0.85) ** 2 <= (wid * 0.55 + 1.0) ** 2
        drip[m] = np.maximum(drip[m], st * 0.7)

# soft smear just under the bottom edge where the drips leave the hole (follows the hole edge, irregular lower border)
colbot = np.array([(np.nonzero(inside[:, c])[0].max() if inside[:, c].any() else -1) for c in range(W)], np.float32)[None, :]
under = (yy - colbot) / PPM
reach = 0.05 + 0.10 * vnoise(H, W, 40, 14)
smear = np.where((colbot >= 0) & (under > -0.01), np.clip(1 - under / reach, 0, 1), 0) * smooth(x_lo - 30, x_lo + 10, xx) * (1 - smooth(x_hi - 10, x_hi + 25, xx))
drip = np.maximum(drip, smear * 0.9)

drip = blur(drip, 3)                              # soften so they read as liquid, not hard lines
drip_a = smooth(0.04, 0.7, drip) * 0.85
alpha = np.maximum(alpha, drip_a)
thick = np.maximum(thick, np.clip(drip * 1.1, 0, 1))

# a few specks around the rim
for _ in range(70):
    a = rng.uniform(0, 2 * np.pi); r = rng.uniform(0.38, 0.6) * PPM * (1.25 if np.cos(a) < 0 else 0.9)
    sx, sy = cxp + np.cos(a) * r, cyp + np.sin(a) * r; rr = rng.uniform(0.6, 2.0)
    m = (xx - sx) ** 2 + (yy - sy) ** 2 <= rr * rr
    alpha[m] = np.maximum(alpha[m], 0.8)

# colour: dark maroon, darker where thick
n = vnoise(H, W, 4)
rgba = np.zeros((H, W, 4), np.float32)
rgba[..., 0] = 0.42 - 0.16 * thick + 0.05 * n
rgba[..., 1] = 0.035 - 0.015 * thick
rgba[..., 2] = 0.03 - 0.012 * thick
rgba[..., 3] = np.clip(alpha, 0, 1)

def save(name, img):
    img = np.ascontiguousarray(img[::-1]).astype(np.float32)
    h, w = img.shape[:2]
    for im in [i for i in bpy.data.images if i.name == name]: bpy.data.images.remove(im)
    im = bpy.data.images.new(name, w, h, alpha=True)
    im.pixels.foreach_set(img.ravel())
    im.filepath_raw = os.path.join(OUT_DIR, name); im.file_format = 'PNG'; im.save()
save('WallHoleBlood.png', rgba)

# ============================================================ floor stain (stippled, cut-out)
N = 256; FPM = 128.0
fy, fx = np.mgrid[0:N, 0:N].astype(np.float32)
# top row of the texture = wall side.  impact point just off the wall, under the streak curtain
ix, iy = N * 0.5, N * 0.08
f = np.zeros((N, N), np.float32)
dxm, dym = (fx - ix) / FPM, (fy - iy) / FPM
r = np.hypot(dxm * 1.25, dym)
f += np.exp(-r / 0.22) * 1.2                              # pool at the base
f += np.exp(-np.hypot(dxm * 2.2, (dym - 0.05) * 0.9) / 0.16) * 0.6
for a in rng.uniform(0.25, np.pi - 0.25, 14):               # streaky splashes thrown away from the wall
    L = rng.uniform(0.3, 0.85)
    along = dxm * np.cos(a) + dym * np.sin(a); across = -dxm * np.sin(a) + dym * np.cos(a)
    f += np.exp(-np.abs(across) / (0.012 + 0.02 * np.clip(1 - along / L, 0, 1))) * (along > 0) * np.clip(1 - along / L, 0, 1) * rng.uniform(0.5, 0.9)
stip = vnoise(N, N, 1.6) * 0.6 + vnoise(N, N, 5) * 0.4
fa = (f + (stip - 0.5) * 1.3) > 0.6
ft = np.clip(f, 0, 1)
frgba = np.zeros((N, N, 4), np.float32)
frgba[..., 0] = 0.22 - 0.08 * ft + 0.04 * vnoise(N, N, 4)
frgba[..., 1] = 0.02 - 0.008 * ft
frgba[..., 2] = 0.02 - 0.008 * ft
frgba[..., 3] = fa.astype(np.float32)
save('WallHoleBloodFloor.png', frgba)
print('saved wall + floor blood (reference match)')
