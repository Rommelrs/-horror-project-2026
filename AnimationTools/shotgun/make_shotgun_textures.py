# PS2-res texture and inventory icons for the sawed-off shotgun prototype (ShotgunBuilder.cs). Deterministic.
#   Assets/Textures/Shotgun/Shotgun.png   64 px, four horizontal bands, v from the bottom (the mesh UVs use them):
#       0.00-0.25 gunmetal (barrels, receiver; worn edges), 0.25-0.50 walnut (grip, fore-end; grain),
#       0.50-0.75 red shell plastic, 0.75-1.00 brass (shell heads)
#   Assets/Sprites/Items/ShotgunIcon.png, ShotgunShellsIcon.png   256 px white silhouettes like PistolLogo.png
#   Assets/Sprites/ShotgunSpreadRing.png   256 px white ring with four gaps, the shotgun's spread crosshair (SpreadCrosshair.cs
#       sizes it to the pellet cone; the middle of the ring line is the cone edge)
import os, math, random
from PIL import Image, ImageDraw

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
TEX = os.path.join(ROOT, "Assets", "Textures", "Shotgun", "Shotgun.png")
ICONS = os.path.join(ROOT, "Assets", "Sprites", "Items")
rnd = random.Random(12)

# ── texture ──
S = 64
img = Image.new("RGB", (S, S))
px = img.load()


def band(row0, base, noise, fn=None):
    for y in range(16):
        for x in range(S):
            k = 1 + rnd.uniform(-noise, noise)
            if fn: k *= fn(x, y)
            px[x, row0 + y] = tuple(max(0, min(255, int(c * k * 255))) for c in base)


# image rows count from the top, v from the bottom: the v 0.75-1 band is rows 0-15
band(0, (0.62, 0.48, 0.24), 0.10)                                                     # brass
band(16, (0.55, 0.10, 0.08), 0.06)                                                    # red shell
band(32, (0.36, 0.20, 0.10), 0.08, lambda x, y: 0.85 + 0.15 * math.sin(x * 0.9 + y * 0.35 + math.sin(x * 0.3) * 2))  # walnut
band(48, (0.16, 0.17, 0.19), 0.10, lambda x, y: 1.4 if rnd.random() < 0.04 else 1.0)   # gunmetal, worn flecks
os.makedirs(os.path.dirname(TEX), exist_ok=True)
img.save(TEX)
print("wrote", TEX)

# ── icons ──
W = 256
WHITE = (255, 255, 255, 255)


def shotgun_icon():
    im = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    # side view, muzzle to the left: two barrels, receiver, top lever, fore-end, pistol grip, trigger guard
    d.rectangle([22, 98, 168, 112], fill=WHITE)            # upper barrel
    d.rectangle([22, 114, 168, 126], fill=WHITE)           # lower barrel line (side-by-side seen as a fat pair)
    d.rectangle([168, 94, 206, 132], fill=WHITE)           # receiver
    d.rectangle([196, 86, 214, 94], fill=WHITE)            # top lever
    d.polygon([(100, 126), (166, 126), (166, 140), (108, 138)], fill=WHITE)   # fore-end
    d.polygon([(198, 128), (226, 128), (240, 190), (214, 196)], fill=WHITE)   # pistol grip
    d.arc([168, 120, 200, 152], 0, 180, fill=WHITE, width=5)                  # trigger guard
    d.line([(184, 128), (182, 142)], fill=WHITE, width=4)                     # trigger
    return im


def shells_icon():
    im = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    for i, x in enumerate((70, 128, 186)):
        y0 = 60 + (i % 2) * 14
        d.rounded_rectangle([x - 22, y0, x + 22, y0 + 104], radius=6, fill=WHITE)        # hull
        d.rectangle([x - 25, y0 + 104, x + 25, y0 + 142], fill=WHITE)                       # brass head
        d.line([(x - 22, y0 + 100), (x + 22, y0 + 100)], fill=(0, 0, 0, 0), width=3)        # crimp line
    return im


os.makedirs(ICONS, exist_ok=True)
shotgun_icon().save(os.path.join(ICONS, "ShotgunIcon.png"))
shells_icon().save(os.path.join(ICONS, "ShotgunShellsIcon.png"))
print("wrote icons")


def spread_ring():
    # thin ring (outer radius 127, 5 px thick) broken at 12 / 3 / 6 / 9 o'clock, like a shotgun reticle
    im = Image.new("RGBA", (W, W), (255, 255, 255, 0))
    d = ImageDraw.Draw(im)
    for a0 in (12, 102, 192, 282):
        d.arc([1, 1, W - 2, W - 2], a0, a0 + 66, fill=WHITE, width=5)
    return im


spread_ring().save(os.path.join(ROOT, "Assets", "Sprites", "ShotgunSpreadRing.png"))
print("wrote spread ring")
