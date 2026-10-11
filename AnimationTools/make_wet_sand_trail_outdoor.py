# Outdoor half of the wet sandy footprint trail (see make_wet_sand_trail.py for the indoor half): from the sand beside the park toilet,
# round the front, up the steps to the men's door. Used by a top-down Projector, so the texture is a map of the area seen from above:
# u = toilet-local +X, v = toilet-local +Z, covering REGION (x0, z0, size) in the toilet prefab's local units.
# Prints follow a spline (toes along the walking direction), alternate left/right, carry wet sand all the way (they come from the wet
# patch behind the building), with soaked halos, a damp streak, drips and sand grit. Output: RGBA colour/coverage + wetness map.
import numpy as np
from PIL import Image, ImageFilter

OUT = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/Assets/Decals/Wet/"
REF = "D:/Unity Stuff/Shamkha Refs/"
rng = np.random.default_rng(12)

REGION = (0.7, 0.1, 4.05)        # x0, z0, size (local)
BUCKET_MOUTH = (3.56, 0.50)      # where the knocked-over bucket's mouth lies (local x, z); the water spilled from here
N = 1024
PX = N / REGION[2]               # px per local metre
SCALE = 1.34                     # toilet instance scale: shoe sizes are authored in real metres
PATH = [(3.45, 0.85), (3.3, 2.2), (3.0, 3.4), (2.4, 3.95), (1.75, 3.65), (1.45, 2.9), (1.35, 2.2), (1.3, 1.72)]
STRIDE = 0.45                    # local metres between prints


def blur(a, r):
    if r <= 0: return a
    im = Image.fromarray(np.clip(a * 255, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r))
    return np.asarray(im, dtype=np.float32) / 255.0


def smooth(t):
    t = np.clip(t, 0, 1); return t * t * (3 - 2 * t)


def noise(r):
    n = rng.standard_normal((N, N)).astype(np.float32)
    n = blur((n - n.min()) / (n.max() - n.min()), r)
    return (n - n.mean()) / (n.std() + 1e-6)


def to_px(x, z):                 # local (x, z) -> array (col, row), row = v
    return (x - REGION[0]) * PX, (z - REGION[1]) * PX


def catmull(pts, n=400):
    P = [np.array(p, float) for p in pts]; P = [P[0]] + P + [P[-1]]
    out = []
    for i in range(1, len(P) - 2):
        for t in np.linspace(0, 1, n // (len(P) - 3), endpoint=False):
            p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
            out.append(0.5 * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    out.append(P[-2]); return np.array(out)


yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
mud = np.zeros((N, N), np.float32); sand = np.zeros_like(mud); damp = np.zeros_like(mud)
grain = noise(0.8); fine = noise(2.0); blot = noise(9.0)


def sole(cx, cy, heading, left):
    """heading = unit walking direction in px space. Toes point along it."""
    L = 0.34 / SCALE * PX; Wd = 0.128 / SCALE * PX
    fx, fy = heading                                  # forward
    rx, ry = fy, -fx                                  # right
    dx, dy = xx - cx, yy - cy
    v = -(dx * fx + dy * fy) / L                      # + = heel end (behind), - = toe end (ahead)
    u = (dx * rx + dy * ry) / Wd
    if left: u = -u
    fore = ((u + 0.04) / 0.52) ** 2 + ((v + 0.22) / 0.30) ** 2
    heel = (u / 0.42) ** 2 + ((v - 0.32) / 0.18) ** 2
    m = np.maximum(smooth((1.0 - fore) / 0.12), smooth((1.0 - heel) / 0.15))
    chev = 0.5 + 0.5 * np.sign(np.sin((v * 34 + np.abs(u) * 9) * np.pi))
    bars = 0.5 + 0.5 * np.sign(np.sin(v * 30 * np.pi))
    return m, np.where(v > 0.12, bars, chev)


path = catmull(PATH)
seg = np.linalg.norm(np.diff(path, axis=0), axis=1); arc = np.concatenate([[0], np.cumsum(seg)])
n_steps = int(arc[-1] / STRIDE)
for i in range(n_steps + 1):
    s = min(i * STRIDE + 0.15, arc[-1] - 0.05)
    k = np.searchsorted(arc, s); k = max(1, min(k, len(path) - 1))
    p = path[k - 1] + (path[k] - path[k - 1]) * ((s - arc[k - 1]) / max(seg[k - 1], 1e-6))
    d = path[k] - path[k - 1]; d = d / (np.linalg.norm(d) + 1e-9)
    left = (i % 2 == 0)
    side = np.array([d[1], -d[0]])                    # right of the walking direction (local x,z)
    p = p + side * (-0.055 if left else 0.055) + rng.normal(0, 0.008, 2)
    ang = np.radians((-1 if left else 1) * 7 + rng.normal(0, 3))
    c, sn = np.cos(ang), np.sin(ang); d2 = np.array([d[0] * c - d[1] * sn, d[0] * sn + d[1] * c])
    cx, cy = to_px(*p)
    m, tread = sole(cx, cy, (d2[0], d2[1]), left)
    t = i / max(n_steps, 1)
    load = 0.85 + 0.15 * rng.random()                 # fresh wet sand the whole way (they came from the wet patch round the back)
    breakup = smooth((fine * 0.45 + 0.85 - (1 - load) * 0.95 + 0.25 * tread) / 0.35)
    mud = np.maximum(mud, m * breakup * (0.7 + 0.3 * load) * (0.8 + 0.2 * tread))
    ooze = np.clip(blur(m, 3) * 1.6 - m * 0.4, 0, 1) * smooth((blot * 0.6 + fine * 0.4 + 0.2) / 0.6) * load
    mud = np.maximum(mud, ooze * 0.7)
    rim = smooth(1 - np.abs(blur(m, 2.0) - 0.5) * 2.2)
    clumps = smooth((grain * 0.8 + fine * 0.6 + 1.5 * load - 0.9) / 0.4)
    sand = np.maximum(sand, (rim * 0.9 + m * (1 - tread) * 0.6 + blur(m, 3) * 0.3) * clumps * load)
    halo = np.maximum.reduce([blur(m, 6) * 2.4, blur(m, 16) * 2.0, blur(m, 34) * 1.4]) * (0.75 + 0.25 * smooth((blot + 1) / 2))
    damp = np.maximum(damp, np.clip(halo, 0, 1) * 0.85)
    for _ in range(6):                                # drips around the step
        q = p + d * rng.uniform(0.05, 0.3) + side * rng.normal(0, 0.05)
        qx, qy = to_px(*q); r = rng.uniform(1.5, 3.5)
        dd = smooth(1 - np.hypot(xx - qx, yy - qy) / r)
        damp = np.maximum(damp, dd * 0.9); mud = np.maximum(mud, dd * 0.3)

# the spill: water poured out of the bucket mouth toward the trail, soaking the sand (an irregular lobed puddle, a run-off tongue,
# a lighter rim of washed sand where it soaked in), with mud slurry where the first steps went through it
bx, by = to_px(*BUCKET_MOUTH)
dirx, diry = to_px(3.43, 1.05); dirx -= bx; diry -= by; dl = np.hypot(dirx, diry); dirx /= dl; diry /= dl
ax = (xx - bx) * dirx + (yy - by) * diry              # along the spill
cx_ = -(xx - bx) * diry + (yy - by) * dirx            # across
R = 0.5 * PX
lobe = 1 - ((ax - 0.45 * R) / (0.75 * R)) ** 2 - (cx_ / (0.55 * R)) ** 2 + 0.35 * noise(14) + 0.15 * noise(5)
tongue = 1 - ((ax - 1.25 * R) / (0.6 * R)) ** 2 - (cx_ / (0.22 * R)) ** 2 + 0.3 * noise(10)
pud = smooth(np.maximum(lobe, tongue * 0.9) / 0.35) * smooth((ax + 0.15 * R) / (0.15 * R))
damp = np.maximum(damp, pud)
rimw = smooth(1 - np.abs(blur(pud, 3) - 0.35) / 0.2) * (1 - pud * 0.6)
sand = np.maximum(sand, rimw * smooth((grain * 0.4 + fine * 0.6 + 0.6) / 0.5) * 0.55)
mud = np.maximum(mud, pud * smooth((blot - 0.3) / 0.8) * 0.3)          # a few muddy patches, mostly just soaked sand

# damp streak along the walking line
pts_px = np.array([to_px(*q) for q in path[::4]])
dist = np.full((N, N), 1e9, np.float32)
for qx, qy in pts_px: dist = np.minimum(dist, np.hypot(xx - qx, yy - qy))
streak = smooth(1 - dist / (0.12 * PX)) * smooth((noise(16) + 0.4) / 0.9)
damp = np.maximum(damp, streak * 0.6)
grit = smooth((grain * 0.7 + fine * 0.5 - 0.6) / 0.3) * damp * (1 - pud)
sand = np.maximum(sand, grit * 0.7)

edge = smooth(np.minimum.reduce([xx, N - 1 - xx, yy, N - 1 - yy]) / 16)
mud *= edge; sand *= edge; damp *= edge

c_damp = np.array([0.12, 0.09, 0.06]); c_mud = np.array([0.15, 0.105, 0.065]); c_sand = np.array([0.74, 0.61, 0.42])
sp = np.asarray(Image.open(REF + "IMG_2017.jpg").convert("RGB").crop((1000, 1500, 3000, 3500)).resize((N, N)), np.float32) / 255.0
sp = sp / sp.reshape(-1, 3).mean(0)
col = c_damp[None, None] * np.ones((N, N, 3), np.float32)
col = col * (1 - mud[..., None]) + (c_mud * (0.85 + 0.3 * (grain * 0.15 + 0.5))[..., None]) * mud[..., None]
col = col * (1 - sand[..., None]) + (c_sand * sp * (0.9 + 0.1 * grain[..., None])) * sand[..., None]
alpha = np.clip(np.maximum.reduce([damp * 0.22, mud * 1.0, sand * 1.0]), 0, 1)
wetness = np.clip(np.maximum(damp, mud * 0.8) - sand * 0.4, 0, 1)

Image.fromarray((np.flipud(np.clip(np.dstack([col, alpha]), 0, 1)) * 255 + 0.5).astype(np.uint8), "RGBA").save(OUT + "WetSandTrail_Outdoor.png")
Image.fromarray((np.flipud(wetness) * 255 + 0.5).astype(np.uint8), "L").convert("RGB").save(OUT + "WetSandTrail_Outdoor_Wet.png")
print("saved", n_steps + 1, "prints")
