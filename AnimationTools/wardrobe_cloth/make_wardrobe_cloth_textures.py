# Paints the wardrobe tarp + rope textures (PS2 style: 512 / 64x256, 8-bit palette look) and multiplies the cloth with the
# lighting Blender baked (lighting_intact.npy / lighting_cut.npy, written by build_wardrobe_cloth.py).
# Reference (2026-11-21, recoloured for readability against the sand + fog): sun-faded blue canvas tarp like the
# blue covers on Gulf rooftops/building sites, lashed with yellow polypropylene rope. Was: old khaki/off-white tent canvas tarp (sewn panels, hemmed edges, brass eyelets, a stitched-on patch), lashed with
# hemp rope; dust at the hem, rope grime, water tide marks, and a little dried blood (a smeared hand + drips).
# Run with system python:  python make_wardrobe_cloth_textures.py
import json, math, os
import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Models", "WardrobeCloth", "Textures")
os.makedirs(OUT, exist_ok=True)
L = json.load(open(os.path.join(HERE, "layout.json")))
N = 512
rng = np.random.default_rng(5)

AMAX, BMIN, BMAX, HW, HTOP = L["AMAX"], L["bmin"], L["bmax"], L["HW"], L["HTOP"]
# texel centre -> cloth coordinates: a (across, metres) and b (up the cloth / over the top, metres); row 0 = v 0 (bottom)
v_idx, u_idx = np.mgrid[0:N, 0:N]
A = (u_idx + 0.5) / N * 2 * AMAX - AMAX
B = (v_idx + 0.5) / N * (BMAX - BMIN) + BMIN
PX_M = N / (2 * AMAX)          # texels per metre across
PY_M = N / (BMAX - BMIN)


def noise(scale_px, seed, octaves=3):
    """smooth value noise in [0,1]"""
    r = np.random.default_rng(seed)
    out = np.zeros((N, N))
    amp, tot = 1.0, 0.0
    s = scale_px
    for _ in range(octaves):
        n = max(2, int(N / s))
        small = r.random((n + 1, n + 1))
        img = Image.fromarray((small * 255).astype(np.uint8)).resize((N, N), Image.BICUBIC)
        out += amp * np.asarray(img, dtype=np.float64) / 255
        tot += amp
        amp *= 0.5
        s /= 2
    return out / tot


def blur(a, r):
    img = Image.fromarray(np.clip(a * 255, 0, 255).astype(np.uint8))
    return np.asarray(img.filter(ImageFilter.GaussianBlur(r)), dtype=np.float64) / 255


def mix(base, col, m):
    m = np.clip(m, 0, 1)[..., None]
    return base * (1 - m) + np.array(col, dtype=np.float64) * m


def rope_z(i, x):
    z, tilt, _ = L["ropes"][i]
    return z + tilt * np.clip(x, -HW, HW)


# ---------------- canvas ----------------
canvas = np.array([46, 74, 112], dtype=np.float64) / 255
img = np.ones((N, N, 3)) * canvas
mott = noise(160, 1)
img *= (0.86 + 0.24 * mott)[..., None]
img = mix(img, (0.30, 0.38, 0.47), blur(np.clip((noise(120, 2, 2) - 0.5) * 1.2, 0, 1), 6))   # greyer, older patches
# weave: 2-texel over/under + slubs (thicker threads)
weave = ((u_idx // 1 + v_idx // 1) % 2) * 0.045 - 0.022
slub_r = (rng.random(N) < 0.07) * rng.uniform(-0.07, 0.05, N)
slub_c = (rng.random(N) < 0.07) * rng.uniform(-0.07, 0.05, N)
img *= (1 + weave + slub_r[:, None] + slub_c[None, :])[..., None]

# sewn panels: two seams across (a panel joint) with double stitching
for zs in (1.72, 2.40):
    d = np.abs(B - zs) * PY_M
    img *= (1 - 0.18 * np.exp(-(d / 1.2) ** 2) + 0.06 * np.exp(-((B - zs - 0.02) * PY_M / 1.5) ** 2))[..., None]
    for off in (-0.012, 0.012):
        st = (np.abs(B - zs - off) * PY_M < 0.6) & ((u_idx // 3) % 2 == 0)
        img = mix(img, (0.10, 0.14, 0.22), st * 0.7)

# hem all round: a darker folded band + a stitch line, brass eyelets every 40 cm
hem_bottom = B - (0.30 + 0.025 * np.sin(A * 6.1) + 0.012 * np.sin(A * 17.0 + 1.3))
edges = [hem_bottom, (BMAX - 0.04 - 0.03 * np.sin(A * 4.3 + 0.7)) - B, AMAX - np.abs(A)]
for e in edges:
    band = (e >= 0) & (e < 0.035)
    img *= (1 - 0.13 * band)[..., None]
    st = (np.abs(e - 0.028) * PX_M < 0.6) & (((u_idx + v_idx) // 3) % 2 == 0)
    img = mix(img, (0.10, 0.14, 0.22), st * 0.6)
for ax in np.arange(-1.2, 1.25, 0.4):          # eyelets along the top flap's edge
    bc = BMAX - 0.04 - 0.03 * math.sin(ax * 4.3 + 0.7) - 0.018
    r = np.hypot((A - ax) * PX_M, (B - bc) * PY_M)
    img = mix(img, (0.42, 0.36, 0.20), (r < 3.4) & (r > 1.6))
    img = mix(img, (0.70, 0.60, 0.34), ((r < 2.4) & (r > 1.9)) * 0.6)
    img = mix(img, (0.10, 0.09, 0.07), r < 1.6)

# a stitched-on repair patch (darker olive canvas), upper left
px0, px1, pz0, pz1 = -0.86, -0.50, 2.05, 2.33
inside = (A > px0) & (A < px1) & (B > pz0) & (B < pz1)
patch = np.ones((N, N, 3)) * np.array([0.55, 0.50, 0.36]) * (0.88 + 0.2 * noise(40, 9))[..., None]
img = np.where(inside[..., None], patch * (1 + weave)[..., None], img)
edge_d = np.minimum.reduce([np.abs(A - px0) * PX_M, np.abs(A - px1) * PX_M, np.abs(B - pz0) * PY_M, np.abs(B - pz1) * PY_M])
img *= (1 - 0.25 * (inside & (edge_d < 1.2)))[..., None]
st = inside & (np.abs(edge_d - 2.5) < 0.6) & (((u_idx + v_idx) // 2) % 2 == 0)
img = mix(img, (0.25, 0.22, 0.15), st * 0.8)

# ---------------- grime ----------------
# sand / dust up from the hem
dust = np.clip(1 - (hem_bottom / 0.55), 0, 1) ** 1.6 * (0.6 + 0.6 * noise(50, 3))
img = mix(img, (0.56, 0.45, 0.30), dust * 0.55)
# rope rub marks: grey-brown grime band along every lashing (front + sides)
for i in range(3):
    d = np.abs(B - rope_z(i, A))
    g = np.exp(-(d / 0.03) ** 2) * (0.5 + 0.5 * noise(30, 10 + i))
    img = mix(img, (0.16, 0.16, 0.16), g * 0.45)
# water tide marks: two irregular rings
for (cx, cz, rad, sd) in ((-0.45, 1.05, 0.22, 21), (0.75, 2.55, 0.17, 22)):
    ang = np.arctan2(B - cz, A - cx)
    rr = rad * (1 + 0.18 * np.sin(ang * 3 + sd) + 0.08 * np.sin(ang * 7))
    d = np.hypot(A - cx, B - cz)
    inner = d < rr
    img = mix(img, (0.40, 0.46, 0.52), inner * 0.25)
    img = mix(img, (0.70, 0.70, 0.64), np.exp(-(((d - rr) * PX_M) / 1.4) ** 2) * 0.55)
# fine grime speckle
img *= (0.96 + 0.08 * blur(np.random.default_rng(4).random((N, N)), 0.7))[..., None]

# ---------------- a little blood ----------------
blood = np.zeros((N, N))
dark = np.zeros((N, N))


def blob(cx, cz, rx, rz, strength=1.0, rot=0.0):
    x, z = A - cx, B - cz
    c, s = math.cos(rot), math.sin(rot)
    xr, zr = x * c + z * s, -x * s + z * c
    m = np.clip(1.4 - np.hypot(xr / rx, zr / rz), 0, 1)
    return np.minimum(1, m * 2.5) * strength


# smeared hand print right of the pocket, as if someone pushed something in with a bloody hand and dragged it off
hx, hz = 0.52, 1.58
blood = np.maximum(blood, blob(hx, hz, 0.055, 0.065, 0.95, 0.2))
for k, (fx, fz, ln) in enumerate(((-0.045, 0.07, 0.07), (-0.015, 0.085, 0.08), (0.015, 0.082, 0.075), (0.042, 0.068, 0.06))):
    for t in np.linspace(0, 1, 7):         # finger, then the drag down-right
        blood = np.maximum(blood, blob(hx + fx + 0.01 * t, hz + fz - 0.02 + ln * t * 0.5, 0.012, 0.018, 0.9 - 0.1 * t))
    for t in np.linspace(0, 1, 10):
        blood = np.maximum(blood, blob(hx + fx + 0.06 * t, hz + fz - 0.14 * t, 0.011, 0.016, 0.55 * (1 - t) + 0.1))
blood = np.maximum(blood, blob(hx + 0.07, hz - 0.03, 0.035, 0.022, 0.6, -0.4))     # thumb smear
# drips running down from the palm
for (dx, ln) in ((-0.025, 0.24), (0.01, 0.15), (0.035, 0.31)):
    zz = np.linspace(0, ln, 30)
    for k, t in enumerate(zz):
        w = 0.0065 * (1 - 0.4 * t / ln)
        blood = np.maximum(blood, blob(hx + dx + 0.004 * math.sin(t * 40), hz - 0.05 - t, w, 0.012, 0.85))
    blood = np.maximum(blood, blob(hx + dx, hz - 0.05 - ln, 0.011, 0.014, 0.9))
# a few spatter dots + a faint smudge on the pocket
for k in range(9):
    sx, sz = hx + rng.uniform(-0.2, 0.18), hz + rng.uniform(-0.12, 0.2)
    rr = rng.uniform(0.004, 0.009)
    blood = np.maximum(blood, blob(sx, sz, rr, rr, 0.8))
blood = np.maximum(blood, blob(L["XT"] - 0.12, L["ZT"] + 0.16, 0.06, 0.035, 0.28, 0.6))
blood = blur(blood, 0.6)
grain = 0.75 + 0.5 * noise(4, 7, 1)
alpha = np.clip(blood * grain, 0, 1)
edge = np.clip(blood - blur(blood, 2.0), 0, 1) * 2.5           # dried blood is darker at its rims
img = mix(img, (0.50, 0.05, 0.04), alpha * 0.95)
img = mix(img, (0.20, 0.02, 0.02), edge * alpha)


def finish(rgb, name, size=None, colors=256):
    out = Image.fromarray(np.clip(rgb[::-1] * 255, 0, 255).astype(np.uint8))        # row 0 = v 1 for PNG
    if size:
        out = out.resize(size, Image.BILINEAR)
    out = out.quantize(colors=colors, method=Image.MEDIANCUT, dither=Image.FLOYDSTEINBERG).convert("RGB")   # 8-bit CLUT look
    out.save(os.path.join(OUT, name))
    print("wrote", name, out.size)


# ---------------- lighting ----------------
for ver in ("intact", "cut"):
    light = np.load(os.path.join(HERE, "lighting_%s.npy" % ver)).mean(axis=2)
    covered = light > 1e-4
    ref = np.percentile(light[covered], 88)
    lit = np.clip(light / ref, 0.0, 1.1) ** 1.7     # stronger fold shading
    lit = 0.14 + 0.86 * lit                     # folds read on an unlit shader; the alley's ambient never goes fully black
    lit = np.where(covered, lit, 0.8)
    finish(img * lit[..., None], "WardrobeCloth_%s.png" % ver.capitalize())

# ---------------- rope ----------------
RW, RH = 64, 256       # u = round the rope, v = along it (one repeat = 15 cm)
vv, uu = np.mgrid[0:RH, 0:RW]
u = (uu + 0.5) / RW
v = (vv + 0.5) / RH
strands = 3
ph = (u + v * 3.0) * strands          # three strands twisting along the rope
f = ph - np.floor(ph)
prof = np.sin(f * math.pi) ** 0.7     # round strand: bright middle, dark grooves
fib = np.random.default_rng(11).random((RH, RW))
fib = np.asarray(Image.fromarray((fib * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6)), dtype=np.float64) / 255
# fibres follow the strand direction: smear noise along it
fiber = 0.5 * fib + 0.5 * np.roll(fib, 2, axis=0)
hemp = np.array([0.95, 0.70, 0.10])     # yellow polypropylene rope (was hemp 0.50, 0.41, 0.28)
rope = hemp * (0.62 + 0.38 * prof)[..., None] * (0.85 + 0.3 * fiber)[..., None]
dirt = np.clip((np.sin(v * 2 * math.pi + 1.3) * 0.5 + 0.5) * 0.35 + 0.25 * fib, 0, 1)
rope = rope * (1 - 0.25 * dirt)[..., None] + np.array([0.40, 0.30, 0.12]) * (0.25 * dirt)[..., None]
out = Image.fromarray(np.clip(rope[::-1] * 255, 0, 255).astype(np.uint8))
out = out.quantize(colors=64, method=Image.MEDIANCUT, dither=Image.FLOYDSTEINBERG).convert("RGB")
out.save(os.path.join(OUT, "WardrobeRope.png"))
print("wrote WardrobeRope.png", out.size)
