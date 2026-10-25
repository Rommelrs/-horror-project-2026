# PS2-res alpha-cutout texture for the white steel park fence (after the user's photos of the park entrance fences:
# white-painted panels between square posts, interlaced pointed arches in the top band, "vase" motifs - a cup hanging off
# the middle rail into a ring with a twist inside, joined to its neighbours by long curves - spear finials on top, rust
# coming through at the foot). Used by Assets/Script/Environment/Editor/ParkFenceReskin.cs. Deterministic.
# Layout (256 x 256 RGBA, image top = top of the finials):
#   u   0..224 px  one panel, 2.0 m wide, 0..1.75 m high (alpha cut)
#   u 228..256 px  solid painted steel for the posts
import os, math, random
from PIL import Image, ImageDraw

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
OUT = os.path.join(ROOT, "Assets", "Textures", "AlleyReal", "ParkFence.png")
S = 256
PW = 224                     # panel pixels across
HM = 1.75                    # metres the image height covers
PAINT = (226, 224, 214)
rnd = random.Random(44)

img = Image.new("RGBA", (S, S), PAINT + (0,))   # transparent texels keep the paint colour (no dark fringe in mips)
d = ImageDraw.Draw(img)
X = lambda m: m / 2.0 * (PW - 1)                 # metres across the panel -> px
Y = lambda h: (S - 1) - h / HM * (S - 1)         # metres up -> px (image y down)
INK = PAINT + (255,)


def line(pts, w=3):
    d.line([(X(x), Y(y)) for x, y in pts], fill=INK, width=w)


def arc_pts(cx, cy, rx, ry, a0, a1, n=24):
    return [(cx + rx * math.cos(a0 + (a1 - a0) * i / n), cy + ry * math.sin(a0 + (a1 - a0) * i / n)) for i in range(n + 1)]


W = 2.0
BOT, MID, TOP = 0.13, 0.95, 1.40
# frame
line([(0.02, BOT), (W - 0.02, BOT)], 5)
line([(0.02, MID), (W - 0.02, MID)], 4)
line([(0.02, TOP), (W - 0.02, TOP)], 5)
line([(0.02, BOT), (0.02, TOP)], 5)
line([(W - 0.02, BOT), (W - 0.02, TOP)], 5)

# top band: interlaced pointed arches springing off the middle rail (each spans two bays, one starts every bay)
bays = 6
s = W / bays
for i in range(-1, bays):
    x0, x1 = i * s, (i + 2) * s
    if x1 <= 0 or x0 >= W: continue
    span, rise = x1 - x0, (TOP - MID) * 0.92
    left = [(x0 + span * (1 - math.cos(t * math.pi / 3)), MID + rise * math.sin(t * math.pi / 3) / math.sin(math.pi / 3)) for t in [k / 16 for k in range(17)]]
    right = [(x1 - (px - x0), py) for px, py in left]
    for curve in (left, right):
        pts = [(min(max(px, 0.02), W - 0.02), py) for px, py in curve]
        line(pts, 3)

# lower band: four vases, each a cup off the middle rail, a ring with a twist, a stem down to the foot rail
n = 4
for i in range(n):
    cx = (i + 0.5) * W / n
    r = 0.15
    cy = 0.55
    line([(cx - 0.09, MID), (cx + 0.09, MID), (cx, cy + r + 0.02), (cx - 0.09, MID)], 3)
    d.ellipse([X(cx - r), Y(cy + r), X(cx + r), Y(cy - r)], outline=INK, width=3)
    # the twist across the ring
    line([(cx - r * 0.75 + r * 1.5 * k / 10, cy + 0.035 * math.sin(k / 10 * 2 * math.pi)) for k in range(11)], 2)
    line([(cx, cy - r), (cx, BOT + 0.07), (cx - 0.07, BOT), (cx, BOT + 0.07), (cx + 0.07, BOT)], 3)
    # long curves from this ring's sides up to the cups of the neighbours: the lens shapes of the photo
    for side in (-1, 1):
        x_end = cx + side * W / n * 0.5
        line([(cx + side * r, cy)] + [(cx + side * (r + (W / n * 0.5 - r) * t), cy + (MID - cy - 0.02) * (t ** 1.6)) for t in [k / 10 for k in range(1, 11)]], 2)

# spear finials on the top rail
for k in range(9):
    x = 0.1 + k * (W - 0.2) / 8
    line([(x, TOP), (x, 1.62)], 3)
    d.polygon([(X(x), Y(1.72)), (X(x) + 4, Y(1.63)), (X(x), Y(1.59)), (X(x) - 4, Y(1.63))], fill=INK)

# posts strip: solid paint
d.rectangle([228, 0, S - 1, S - 1], fill=INK)

# weathering: grime and rust on painted texels, heavier towards the foot
px = img.load()
for y in range(S):
    h = (S - 1 - y) / (S - 1) * HM
    for x in range(S):
        r_, g_, b_, a = px[x, y]
        if a == 0: continue
        k = rnd.uniform(-0.05, 0.05)
        rust = max(0.0, 0.35 - h) / 0.35 * rnd.uniform(0.0, 0.9) if rnd.random() < 0.55 else 0.0
        if rnd.random() < 0.015: rust = max(rust, rnd.uniform(0.4, 0.8))      # chips up the fence
        c = [v * (1 + k) for v in (r_, g_, b_)]
        rc = (150, 82, 40)
        px[x, y] = tuple(int(max(0, min(255, c[i] * (1 - rust) + rc[i] * rust))) for i in range(3)) + (255,)
# a few rust runs down the posts strip
for _ in range(6):
    x = rnd.randrange(229, S)
    y0 = rnd.randrange(0, S - 40)
    for y in range(y0, min(S, y0 + rnd.randrange(20, 60))):
        r_, g_, b_, a = px[x, y]
        px[x, y] = (int(r_ * 0.75 + 150 * 0.25), int(g_ * 0.75 + 82 * 0.25), int(b_ * 0.75 + 40 * 0.25), 255)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
img.save(OUT)
print("wrote", OUT)
