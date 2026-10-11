# Decal: a trail of wet, sandy shoe prints - someone walked through sand, got their shoes wet and tracked the muck across the floor.
# One long RGBA texture covering the whole trail (1.0 x 3.6 m in the interior prefab's local units), alpha = how much muck is there.
# Prints alternate left/right, start heavy (clumps of wet sand, dark mud, tread pattern full of muck, a damp halo) and dry out along
# the way (thinner, patchier, mostly a damp outline), with a few drips and a heel smear. Toes point toward v = 0.
# Channels: RGB colour, A coverage. A second texture (_Wet) stores wetness for the shader's sheen.
import numpy as np
from PIL import Image, ImageFilter

OUT = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/Assets/Decals/Wet/"
REF = "D:/Unity Stuff/Shamkha Refs/"
rng = np.random.default_rng(8)

W, H = 512, 1844                 # 1.0 m x 3.6 m (local), ~1.95 mm per px
PX = W / 1.0                     # px per local metre
SCALE = 1.34                     # the toilet prefab instance is scaled 1.34: author shoe sizes in real metres, then divide


def blur(a, r):
    if r <= 0: return a
    im = Image.fromarray(np.clip(a * 255, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r))
    return np.asarray(im, dtype=np.float32) / 255.0


def smooth(t):
    t = np.clip(t, 0, 1); return t * t * (3 - 2 * t)


def noise(r, seed=None):
    n = rng.standard_normal((H, W)).astype(np.float32)
    n = blur((n - n.min()) / (n.max() - n.min()), r)
    return (n - n.mean()) / (n.std() + 1e-6)


yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
mud = np.zeros((H, W), np.float32)       # dark wet mud coverage
sand = np.zeros((H, W), np.float32)      # gritty sand clumps
damp = np.zeros((H, W), np.float32)      # wet halo / water film
grain = noise(0.8); fine = noise(2.0); blot = noise(9.0)


def sole_mask(cx, cy, ang, left):
    """Shoe sole (sneaker) in px, toes toward -v. Returns soft contact mask and a tread pattern."""
    L = 0.34 / SCALE * PX; Wd = 0.128 / SCALE * PX     # a touch bigger than life so it reads through the PSX filter
    c, s = np.cos(ang), np.sin(ang)
    dx, dy = xx - cx, yy - cy
    u = (c * dx + s * dy) / Wd               # across the sole, -0.5..0.5
    v = (-s * dx + c * dy) / L               # along: +0.5 = heel end (higher v) , -0.5 = toe end
    if left: u = -u
    # forefoot (wider, slightly to the inside), heel, narrow shank between
    fore = ((u + 0.04) / 0.52) ** 2 + ((v + 0.22) / 0.30) ** 2
    heel = (u / 0.42) ** 2 + ((v - 0.32) / 0.18) ** 2
    shank = (np.abs(u - 0.08) < 0.22) & (np.abs(v - 0.08) < 0.14)
    m = np.maximum(smooth((1.0 - fore) / 0.12), smooth((1.0 - heel) / 0.15))
    m = np.maximum(m, shank.astype(np.float32) * 0.45 * smooth((0.22 - np.abs(u - 0.08)) / 0.05))
    # tread: chevrons on the forefoot, bars on the heel
    chev = 0.5 + 0.5 * np.sign(np.sin((v * 34 + np.abs(u) * 9) * np.pi))
    bars = 0.5 + 0.5 * np.sign(np.sin(v * 30 * np.pi))
    tread = np.where(v > 0.12, bars, chev)
    return m, tread, u, v


# ---------------------------------------------------------------- the trail
n_steps = 7
stride = 0.42                                  # local m between consecutive prints (~0.54 m real)
start_v = 0.93
cx0 = 0.5 * W
for i in range(n_steps):
    left = (i % 2 == 0)
    t = i / (n_steps - 1)                      # 0 = just out of the sand pile, 1 = last print at the stall door
    cy = (1 - start_v) * H + i * stride * PX * 0.98      # row in UV-space array (row 0 = v 0); prints go toward v 0 below
    cy = H - cy                                 # flip so the first print is near v = start_v (door end)
    cx = cx0 + (-1 if left else 1) * 0.055 * PX + np.sin(i * 0.9) * 0.025 * PX + rng.normal(0, 4)
    ang = np.radians((-1 if left else 1) * 7 + rng.normal(0, 3))
    m, tread, u, v = sole_mask(cx, cy, ang, left)
    load = 1 - 0.6 * t ** 1.2                    # how much muck is still on the shoe (dries out, but stays nasty to the end)
    wet = 0.6 + 0.4 * (1 - t)
    breakup = smooth((fine * 0.45 + 0.85 - (1 - load) * 0.95 + 0.25 * tread) / 0.35)
    contact = m * breakup
    mud = np.maximum(mud, contact * (0.7 + 0.3 * load) * (0.8 + 0.2 * tread))
    # slurry squeezed out around the sole on the wet early steps
    ooze = np.clip(blur(m, 3) * 1.6 - m * 0.4, 0, 1) * smooth((blot * 0.6 + fine * 0.4 + 0.2) / 0.6) * load
    mud = np.maximum(mud, ooze * 0.75)
    # sand: clumps pressed out along the rim of the sole and in the tread grooves, mostly on the early heavy prints
    rim = smooth(1 - np.abs(blur(m, 2.5) - 0.5) * 2.2)
    clumps = smooth((grain * 0.8 + fine * 0.6 + 1.5 * load - 0.9) / 0.4)
    sand = np.maximum(sand, (rim * 0.9 + m * (1 - tread) * 0.6 + blur(m, 4) * 0.3) * clumps * load)
    # damp halo: water spreads past the sole and soaks the floor
    halo = np.maximum.reduce([blur(m, 10) * 2.4, blur(m, 30 + 14 * (1 - t)) * 2.2, blur(m, 60) * 1.6]) * (0.75 + 0.25 * smooth((blot + 1) / 2))
    damp = np.maximum(damp, np.clip(halo, 0, 1) * wet)
    # heel drag smear behind (toward the previous print) on a couple of steps
    if i in (1, 4):
        sm = smooth(1 - (((xx - cx) / (0.05 * PX)) ** 2 + ((yy - (cy + 0.16 * PX)) / (0.10 * PX)) ** 2))
        mud = np.maximum(mud, sm * 0.45 * load * smooth((fine + 0.3) / 0.6))
    # drips falling off the shoe between steps
    for _ in range(int(9 * wet)):
        dx, dy = rng.normal(0, 0.06 * PX), cy - rng.uniform(0.05, 0.3) * PX
        r = rng.uniform(2.0, 5.0)
        d = smooth(1 - np.hypot(xx - cx - dx, yy - dy) / r)
        damp = np.maximum(damp, d * 0.9); mud = np.maximum(mud, d * 0.35 * load)

# water dripped/dragged along the walking line
path_x = cx0 + np.sin((H * start_v - yy) / PX / stride * 0.9) * 0.025 * PX
streak = smooth(1 - np.abs(xx - path_x) / (0.13 * PX)) * smooth((noise(18) + 0.5) / 0.9)
span = smooth((H * start_v - yy) / 20) * smooth((yy - (H * start_v - (n_steps - 1) * stride * PX * 0.98)) / 60)
damp = np.maximum(damp, streak * span * 0.75)
# puddles of muddy water where they stood still / dripped a lot (organic blobs)
for k in range(4):
    py = H * start_v - rng.uniform(0.1, 0.85) * (n_steps - 1) * stride * PX
    px = cx0 + rng.normal(0, 0.06 * PX)
    rx, ry = rng.uniform(0.07, 0.12) * PX, rng.uniform(0.09, 0.16) * PX
    pud = smooth((1 - (((xx - px) / rx) ** 2 + ((yy - py) / ry) ** 2) + 0.35 * noise(10)) / 0.25)
    damp = np.maximum(damp, pud * 0.8)
    mud = np.maximum(mud, pud * smooth((fine + 0.6) / 0.8) * 0.22)
# sand dragged through the wet trail (gritty smears, more near the start)
grit = smooth((grain * 0.7 + fine * 0.5 - 0.6) / 0.3) * damp * smooth((yy - H * 0.3) / (H * 0.6))
sand = np.maximum(sand, grit * 0.8)

# the start: a messy scuffed patch where they came out of the sand pile (near the door end)
sy = H * start_v + 0.04 * PX
patch = smooth(1 - (((xx - cx0) / (0.30 * PX)) ** 2 + ((yy - sy) / (0.12 * PX)) ** 2)) * smooth((blot + 0.6) / 0.8)
sand = np.maximum(sand, patch * smooth((grain + fine * 0.5 + 0.2) / 0.5) * 0.9)
mud = np.maximum(mud, patch * 0.8 * smooth((fine + 0.8) / 0.8))
damp = np.maximum(damp, np.clip(blur(patch, 8) * 1.5, 0, 1))

# keep everything off the texture border
edge = smooth(np.minimum.reduce([xx, W - 1 - xx, yy, H - 1 - yy]) / 12)
mud *= edge; sand *= edge; damp *= edge

# ---------------------------------------------------------------- colour
c_damp = np.array([0.12, 0.09, 0.06]); c_mud = np.array([0.15, 0.105, 0.065]); c_sand = np.array([0.74, 0.61, 0.42])
sand_photo = Image.open(REF + "IMG_2017.jpg").convert("RGB").crop((1000, 1500, 3000, 3500)).resize((W, W))
sp = np.asarray(sand_photo, np.float32) / 255.0; sp = sp / sp.reshape(-1, 3).mean(0)
sp = np.tile(sp, (int(np.ceil(H / W)), 1, 1))[:H]
col = c_damp[None, None] * np.ones((H, W, 3), np.float32)
col = col * (1 - mud[..., None]) + (c_mud * (0.85 + 0.3 * (grain * 0.15 + 0.5))[..., None]) * mud[..., None]
col = col * (1 - sand[..., None]) + (c_sand * sp * (0.9 + 0.1 * grain[..., None])) * sand[..., None]
# colour layer = muck only; the soaked floor is done by the shader darkening the tiles underneath with the wet map
alpha = np.clip(np.maximum.reduce([damp * 0.35, mud * 1.0, sand * 1.0]), 0, 1)
wetness = np.clip(np.maximum(damp, mud * 0.8) - sand * 0.4, 0, 1)

img = np.dstack([col, alpha])
Image.fromarray((np.flipud(np.clip(img, 0, 1)) * 255 + 0.5).astype(np.uint8), "RGBA").save(OUT + "WetSandTrail.png")
Image.fromarray((np.flipud(wetness) * 255 + 0.5).astype(np.uint8), "L").save(OUT + "WetSandTrail_Wet.png")
print("saved", OUT + "WetSandTrail.png")
