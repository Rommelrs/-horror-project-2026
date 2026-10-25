# PS2-res texture for the black wrought-iron house doors (after the user's photo: a black steel sheet door with iron
# bars and scrollwork on its face - lens-shaped loops up the middle, curls on the outer bars, a crest on top - dusty, with
# pale streaks and white flecks). Used by Assets/Script/Environment/Editor/HouseDoorReskin.cs. Deterministic.
# Layout (256 x 512, v from the bottom, the mesh UVs depend on it):
#   u   0..168 px  main leaf face (0.96 x 2.60 m)
#   u 168..240 px  narrow fixed leaf face (0.41 x 2.60 m)
#   u 240..256 px  plain dusty steel (frame, edges, back, hinges, lock)
import os, math, random
from PIL import Image, ImageDraw

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
OUT = os.path.join(ROOT, "Assets", "Textures", "AlleyReal", "IronDoor.png")
W, H = 256, 512
rnd = random.Random(31)
BASE = (30, 27, 25)          # black paint, a little warm
HI = (92, 86, 80)            # sunlit edge of a raised bar
LO = (8, 7, 7)               # shadow side
img = Image.new("RGB", (W, H), BASE)
px = img.load()

# sheet: noise, vertical pale dust streaks (rain-washed dust), dust thicker towards the foot
streaks = [(rnd.uniform(0, W), rnd.uniform(1, 4), rnd.uniform(0.04, 0.16)) for _ in range(26)]
for y in range(H):
    for x in range(W):
        c = [v * (1 + rnd.uniform(-0.12, 0.12)) for v in BASE]
        k = 0.0
        for sx, sw, sa in streaks:
            d = abs(x - sx)
            if d < sw: k = max(k, sa * (1 - d / sw))
        foot = max(0.0, (y - H * 0.80) / (H * 0.20)) * 0.30     # image y grows downwards: bottom of the door
        k = min(0.5, k + foot * rnd.uniform(0.6, 1.0))
        dust = (150, 132, 108)
        px[x, y] = tuple(int(c[i] * (1 - k) + dust[i] * k) for i in range(3))

d = ImageDraw.Draw(img)


def bar(pts, w=3):
    # a raised iron bar: shadow below-right, highlight above-left, body on top
    pts = [(round(x), round(y)) for x, y in pts]
    d.line([(x + 1, y + 1) for x, y in pts], fill=LO, width=w + 1, joint="curve")
    d.line([(x - 1, y - 1) for x, y in pts], fill=HI, width=w, joint="curve")
    d.line(pts, fill=BASE, width=max(1, w - 1), joint="curve")


def curl(cx, cy, r, turns=1.4, start=0.0, sign=1, steps=40):
    # a flat spiral scroll ending in the middle
    pts = []
    for i in range(steps + 1):
        t = i / steps
        a = start + sign * t * turns * 2 * math.pi
        rr = r * (1 - 0.75 * t)
        pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
    return pts


def lens(cx, y0, y1, half):
    # two mirrored S-curves meeting at both ends: a pointed oval
    L, R = [], []
    for i in range(25):
        t = i / 24
        x = math.sin(t * math.pi) * half
        y = y0 + (y1 - y0) * t
        L.append((cx - x, y)); R.append((cx + x, y))
    return L, R


def leaf(x0, x1, narrow=False):
    w = x1 - x0
    top, bot = 8, H - 8
    # raised rim round the leaf
    bar([(x0 + 4, top), (x1 - 5, top), (x1 - 5, bot), (x0 + 4, bot), (x0 + 4, top)], 2)
    if narrow:
        cols = [x0 + w * 0.30, x0 + w * 0.70]
        for x in cols: bar([(x, top + 6), (x, bot - 6)])
        L, R = lens(x0 + w / 2, H * 0.30, H * 0.62, w * 0.24)
        bar(L); bar(R)
        for y in (H * 0.16, H * 0.82):
            bar(curl(x0 + w / 2, y, w * 0.16, 1.2, -math.pi / 2, 1))
        return
    xs = [x0 + w * f for f in (0.12, 0.31, 0.69, 0.88)]
    for x in xs: bar([(x, top + 6), (x, bot - 6)])
    cx = x0 + w / 2
    # middle column: a bar down the centre through two pointed loops, a knot between them
    bar([(cx, H * 0.10), (cx, bot - 6)])
    for y0, y1 in ((H * 0.13, H * 0.45), (H * 0.52, H * 0.86)):
        L, R = lens(cx, y0, y1, w * 0.17)
        bar(L); bar(R)
    d.ellipse([cx - 6, H * 0.485 - 6, cx + 6, H * 0.485 + 6], outline=HI, width=2)
    # crest on top: two scrolls curling out from the centre bar
    bar(curl(cx - w * 0.09, H * 0.075, w * 0.08, 1.3, 0.0, -1))
    bar(curl(cx + w * 0.09, H * 0.075, w * 0.08, 1.3, math.pi, 1))
    # outer columns: C-curls hanging off the bars, alternating sides
    for i, y in enumerate((H * 0.20, H * 0.40, H * 0.60, H * 0.80)):
        for xa, xb, s in ((xs[0], xs[1], 1), (xs[3], xs[2], -1)):
            r = abs(xb - xa) * 0.42
            cxl = (xa + xb) / 2
            bar(curl(cxl, y, r, 1.25, (0 if s > 0 else math.pi) + (i % 2) * math.pi, s))
    # foot: a cross rail and two curls
    bar([(x0 + 6, H * 0.92), (x1 - 7, H * 0.92)])
    bar(curl(cx - w * 0.2, H * 0.955, w * 0.07, 1.2, 0, 1)); bar(curl(cx + w * 0.2, H * 0.955, w * 0.07, 1.2, math.pi, -1))


leaf(0, 168)
leaf(168, 240, narrow=True)
# white flecks / old paint marks like in the photo, and rust where the dust is
for _ in range(40):
    x, y = rnd.randrange(0, 240), rnd.randrange(0, H)
    c = (200, 196, 186) if rnd.random() < 0.6 else (96, 56, 32)
    for dx in range(rnd.choice((1, 2))):
        px[min(W - 1, x + dx), y] = c
os.makedirs(os.path.dirname(OUT), exist_ok=True)
img.save(OUT)
print("wrote", OUT)
