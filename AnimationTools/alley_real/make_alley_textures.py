# Alley 1 "real look" textures from the user's location photos (D:/Unity Stuff/Shamkha Refs + the alley photos sent
# 2026-10-25), PS2-sized (256 px, point-filtered in Unity):
#   AlleyWall_V1..V5.png  five house styles (colour + kind/amount of decay), two 3.2 m panels x 3.6 m, mapped by world position
#       (WorldWall shader): weathered stucco (IMG_1973) (pilasters are geometry, at the panel seams), the plaster peeled off near
#       the base exposing grey concrete (IMG_2021) like the photos, sand splash and grime at the foot, a few stains.
#   AlleySand.png  seamless pale sand (IMG_2017), ~4 m per tile, for a new terrain layer.
#   AlleyLeaves.png / AlleyBougainvillea.png  RGBA leaf clumps cut from the photos against the sky (alpha test).
# Run: python AnimationTools/alley_real/make_alley_textures.py   (Pillow + numpy)
import os
import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
OUT = os.path.join(ROOT, "Assets", "Textures", "AlleyReal")
REFS = "D:/Unity Stuff/Shamkha Refs/"
SENT = os.path.join(os.environ.get("LOCALAPPDATA", ""), "Temp", "claude", "D--Unity-Stuff-NEWHorrorProject--horror-project-2026",
                    "cf51bab8-e583-42ee-8ab4-15af6ac1a2cc", "images")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(11)
S = 256


def load(path, box=None):
    im = Image.open(path).convert("RGB")
    if box:
        w, h = im.size
        im = im.crop((int(box[0] * w), int(box[1] * h), int(box[2] * w), int(box[3] * h)))
    return im


def arr(im):
    return np.asarray(im).astype(np.float32) / 255.0


def to_img(a):
    return Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8))


def delight(a, radius):
    # remove large-scale lighting (shadows, gradients): divide by a heavy blur, keep the mean colour
    blur = arr(to_img(a).filter(ImageFilter.GaussianBlur(radius)))
    return a / np.maximum(blur, 1e-3) * a.reshape(-1, 3).mean(0)


def seamless(a):
    # cross-fade the image with its half-offset copy so opposite edges meet
    h, w, _ = a.shape
    sh = np.roll(np.roll(a, h // 2, 0), w // 2, 1)
    y = np.abs(np.linspace(-1, 1, h))[:, None]; x = np.abs(np.linspace(-1, 1, w))[None, :]
    m = np.clip(np.maximum(x, y) * 1.6 - 0.6, 0, 1)[..., None]
    return a * (1 - m) + sh * m


def grade(a, target):
    # match the mean colour to `target` (0-1 rgb), keeping the photo's variation
    mean = a.reshape(-1, 3).mean(0)
    return a - mean + np.array(target)


def noise(shape, scale, octaves=4):
    out = np.zeros(shape, np.float32); amp = 1.0; total = 0
    for o in range(octaves):
        s = max(1, int(scale * 2 ** o))
        n = rng.random((s + 1, s + 1)).astype(np.float32)
        n = np.asarray(Image.fromarray((n * 255).astype(np.uint8)).resize((shape[1], shape[0]), Image.BICUBIC)).astype(np.float32) / 255
        out += n * amp; total += amp; amp *= 0.5
    return out / total


def tileable_noise(shape, scale, octaves=4):
    n = noise((shape[0] * 2, shape[1] * 2), scale * 2, octaves)
    a = n[:shape[0], :shape[1]]; b = n[shape[0]:, :shape[1]]; c = n[:shape[0], shape[1]:]; d = n[shape[0]:, shape[1]:]
    y = np.linspace(0, 1, shape[0])[:, None]; x = np.linspace(0, 1, shape[1])[None, :]
    wy = 0.5 - 0.5 * np.cos(np.pi * y); wx = 0.5 - 0.5 * np.cos(np.pi * x)
    return (a * (1 - wx) + c * wx) * (1 - wy) + (b * (1 - wx) + d * wx) * wy


# ── walls: five house styles, after the user's photos (one house's wall peach and crazed with cracks and orange
# stains, grey patches even halfway up; the next ochre and clean but rotten at the pilaster feet; cream; tan; off-white
# with dirt streaks). Each is 512 x 256 = two 3.2 m panels (pilaster at each seam) x 3.6 m, v = 0 at the wall foot. ──
PW, PH = 6.4, 3.6


def wrap_voronoi_edges(h, w, n, rnd):
    # crack network: cell borders of a tileable Voronoi pattern (distance to the 2nd nearest point - nearest is small)
    pts = rnd.random((n, 2)) * np.array([w, h])
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d1 = np.full((h, w), 1e9, np.float32); d2 = np.full((h, w), 1e9, np.float32)
    for px, py in pts:
        dx = np.abs(xx - px); dx = np.minimum(dx, w - dx)
        dy = np.abs(yy - py); dy = np.minimum(dy, h - dy)
        d = np.sqrt(dx * dx + dy * dy)
        d2 = np.where(d < d1, d1, np.minimum(d2, d)); d1 = np.minimum(d1, d)
    return (d2 - d1) < 0.9


def wall(name, color, stain, crackle, peel_amt, mid, pil_rot, streak, vcracks, seed):
    rnd = np.random.default_rng(seed)
    W, H = 2 * S, S
    grain = arr(load(REFS + "IMG_1973.jpg", (0.32, 0.02, 0.60, 0.30)).resize((W // 2, H), Image.LANCZOS))
    grain = seamless(grain).mean(-1)
    grain = grain - arr(to_img(np.repeat(grain[..., None], 3, 2)).filter(ImageFilter.GaussianBlur(6)))[..., 0]
    grain = np.tile(grain, (1, 2))
    stucco = np.ones((H, W, 3), np.float32) * np.array(color) * (1 + 0.4 * grain[..., None])
    conc = arr(load(REFS + "IMG_2021.jpg", (0.05, 0.08, 0.95, 0.55)).resize((W // 2, H), Image.LANCZOS))
    conc = np.tile(grade(seamless(delight(conc, 30)), (0.40, 0.395, 0.38)), (1, 2, 1))

    y = np.linspace(1, 0, H)[:, None] * PH       # metres above the foot (image row 0 = top)
    xm = np.linspace(0, PW, W, endpoint=False)[None, :]
    # broad repaint / sun-fade patches, and orange-rust stains blooming down from the top on the stained styles
    img = stucco * (0.9 + 0.2 * tileable_noise((H, W), 3, 3)[..., None])
    st = np.clip((tileable_noise((H, W), 4, 3) - 0.62 + 0.25 * stain) * 4, 0, 1) * stain
    img = img * (1 - st[..., None]) + img * np.array([1.05, 0.84, 0.66]) * st[..., None]

    # the decay (plaster gone, grey concrete showing):
    reach = 0.08 + peel_amt * 1.3 * np.clip(tileable_noise((1, W), 6, 3)[0] * 2.0 - 0.6, 0, 1)[None, :]
    rough = 0.25 * (tileable_noise((H, W), 16, 3) - 0.5)
    peel = (y < reach + rough)                                                    # ragged patches at the foot
    band = np.clip((y - 0.3) / 0.5, 0, 1) * np.clip((3.4 - y) / 0.7, 0, 1)        # fades out, no straight edges
    patches = (tileable_noise((H, W), 5, 4) + 0.12 * rough) * (0.7 + 0.3 * band) > 0.74 - 0.2 * mid + 0.3 * (1 - band)
    peel |= patches if mid > 0.05 else np.zeros_like(peel)                         # grey patches up the wall
    for k in range(2):                                                           # rotting pilaster feet
        dist = np.abs(((xm - k * 3.2 + PW / 2) % PW) - PW / 2)
        # a tall narrow strip up the pilaster with ragged sides (as on the photo's right wall)
        ragged = dist + 0.12 * (tileable_noise((H, W), 32, 3) - 0.5)
        prof = np.clip(1 - (ragged / (0.2 + 0.15 * pil_rot)) ** 4, 0, 1)
        top = 0.2 + 1.5 * pil_rot * (0.6 + 0.4 * tileable_noise((1, W), 24, 3)[0][None, :]) * prof
        peel |= y < top + rough    # ragged, tallest right at the pilaster
    flakes = (tileable_noise((H, W), 10, 3) - y * 0.12) > 0.72 - 0.06 * peel_amt
    peel = (peel | flakes).astype(np.float32)
    leftover = (tileable_noise((H, W), 20, 2) > 0.72).astype(np.float32)
    peel *= 1 - leftover * (y > 0.12)
    peel = arr(to_img(np.repeat(peel[..., None], 3, 2)).filter(ImageFilter.GaussianBlur(0.6)))[..., 0]
    img = img * (1 - peel[..., None]) + conc * peel[..., None]
    lip = np.clip(np.abs(np.gradient(peel, axis=0)) * 3 + np.abs(np.gradient(peel, axis=1)) * 3, 0, 1)
    img *= (1 - 0.35 * lip[..., None])

    # crazing (a crack network over the plaster, patchy) and a few long vertical cracks
    if crackle > 0.05:
        net = wrap_voronoi_edges(H, W, int(250 + 650 * crackle), rnd) & (tileable_noise((H, W), 4, 2) > 0.66 - 0.25 * crackle)
        img *= (1 - 0.16 * (net & (peel < 0.5)))[..., None]     # fine crazing, faint
    for c in range(vcracks):
        cx = rnd.uniform(0, W); top = rnd.uniform(0.1, 0.5) * H; length = rnd.uniform(0.3, 0.8) * H
        for r in range(int(top), int(min(H - 2, top + length))):
            cx += rnd.normal(0, 0.6)
            img[r, int(cx) % W] *= 0.62
    # grime at the foot, dirt streaks running down from the top
    img *= (1 - 0.4 * np.clip(1 - y / 0.4, 0, 1)[..., None] * np.array([1.0, 1.05, 1.2]))
    streaks = tileable_noise((1, W), 24, 2)[0][None, :] * np.clip(1 - (PH - y) / (1.0 + 1.5 * streak), 0, 1)
    img *= (1 - (0.1 + 0.35 * streak) * streaks[..., None])
    # (no painted pilasters: they are real geometry now, AlleyRealLook.PlacePillars puts one on every panel seam,
    # x = 0 and 3.2 m here, which is where the rotting feet above are centred)
    to_img(img).save(os.path.join(OUT, name))
    print("wrote", name)


#     name                color                stain crackle peel mid  pilaster streak vcracks seed
wall("AlleyWall_V1.png", (0.87, 0.70, 0.53), 0.7, 0.8, 0.7, 0.6, 0.3, 0.15, 2, 1)   # peach, crazed, stained
wall("AlleyWall_V2.png", (0.84, 0.71, 0.42), 0.1, 0.1, 0.5, 0.0, 1.0, 0.1, 4, 2)    # ochre, rotten pilaster feet
wall("AlleyWall_V3.png", (0.86, 0.80, 0.65), 0.15, 0.3, 0.4, 0.2, 0.3, 0.25, 1, 3)  # cream
wall("AlleyWall_V4.png", (0.80, 0.67, 0.50), 0.3, 0.4, 0.8, 0.3, 0.5, 0.15, 2, 4)   # tan
wall("AlleyWall_V5.png", (0.84, 0.83, 0.79), 0.0, 0.2, 0.25, 0.05, 0.2, 0.45, 1, 5) # off-white, dirt streaks

# ── sand ──
sand = arr(load(REFS + "IMG_2017.jpg", (0.05, 0.04, 0.95, 0.62)).resize((S * 2, S * 2), Image.LANCZOS))
sand = seamless(delight(sand, 50))
sand = arr(to_img(sand).resize((S, S), Image.LANCZOS))
sand = (sand - sand.mean()) * 1.6 + sand.mean()
sand = grade(sand, (0.64, 0.555, 0.42))
sand *= (0.94 + 0.12 * tileable_noise((S, S), 3, 3))[..., None]
to_img(sand).save(os.path.join(OUT, "AlleySand.png"))
print("wrote AlleySand.png")


# ── foliage: leaves cut from the photos against the sky, shaped into ragged clumps / drapes (no straight card edges) ──
# Atlas, 256 x 256: left column = two round clumps (top: v 0.5-1, bottom: v 0-0.5) for the mound on the wall top,
# right column = a drape (v 1 = its top) whose strands hang to uneven lengths, for the part spilling down the wall.
def leaf_layer(path, box, w, h, flowers):
    a = arr(load(path, box).resize((w, h), Image.LANCZOS))
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    sky = (b > r * 1.08) & (b > g * 0.98) & (b > 0.45)
    wallish = (r > 0.62) & (g > 0.52) & (r - b > 0.12) & (np.abs(r - g) < 0.18)
    leaf = ((g >= r * 0.92) & (g > b * 1.05) | (a.mean(-1) < 0.25)) & ~sky & ~wallish
    m = leaf.astype(np.float32)
    # the gaps get the surrounding leaf colour (they show inside the clump; sky blue must never appear)
    blur = lambda x, rad: arr(to_img(x).filter(ImageFilter.GaussianBlur(rad)))
    wsum = blur(np.repeat(m[..., None], 3, 2), 6)
    fill = np.where(wsum > 0.05, blur(a * m[..., None], 6) / np.maximum(wsum, 1e-3), (a[m > 0].mean(0) if m.any() else 0.3))
    col = np.where(m[..., None] > 0, a, fill * 0.75)
    # toned down to sit with the rest of the scene (less saturated, a bit darker)
    grey = col.mean(-1, keepdims=True)
    col = (grey + (col - grey) * 0.85) * 0.95
    if flowers:   # bougainvillea: magenta bracts in clusters
        spots = (noise((h, w), 24, 3) > 0.62) & (noise((h, w), 8, 2) > 0.45)
        col[spots] = col[spots] * 0.3 + np.array([0.72, 0.16, 0.42]) * 0.7
    return col, m


def clump_alpha(w, h, seed_shift):
    y, x = np.mgrid[0:h, 0:w]
    dx = (x - w / 2) / (w / 2); dy = (y - h / 2) / (h / 2)
    d = np.sqrt(dx * dx + dy * dy)
    ang = (np.arctan2(dy, dx) + np.pi) / (2 * np.pi)
    ring = noise((1, 128), 10 + seed_shift, 4)[0]
    rim = 0.62 + 0.34 * ring[(ang * 127).astype(int)]
    return d, rim


def foliage(path, box, name, flowers):
    atlas = np.zeros((S, S, 4), np.float32)
    for row, (bx, shift) in enumerate([(box, 0), ((box[0] + 0.03, box[1] + 0.05, box[2], box[3]), 3)]):
        col, m = leaf_layer(path, bx, S // 2, S // 2, flowers)
        d, rim = clump_alpha(S // 2, S // 2, shift)
        fray = noise((S // 2, S // 2), 20, 2)
        # solid core, the outer third frayed into leaves and gaps
        alpha = ((m > 0) | (d < rim * 0.55)) & (d < rim) & ((d < rim * 0.68) | (fray > 0.45 + 0.4 * (d - rim * 0.68) / (rim * 0.32)))
        y0 = row * S // 2
        atlas[y0:y0 + S // 2, :S // 2, :3] = col; atlas[y0:y0 + S // 2, :S // 2, 3] = alpha
    # drape: full at the top, strands of uneven length below, narrowing as it hangs
    # (from the middle of the crown, clear of the sky above it)
    col, m = leaf_layer(path, (box[0] + 0.04, box[1] + (box[3] - box[1]) * 0.3, box[2] - 0.04, box[3]), S // 2, S, flowers)
    h, w = S, S // 2
    y, x = np.mgrid[0:h, 0:w]
    length = (0.3 + 0.45 * noise((1, w), 7, 2)[0] + 0.25 * noise((1, w), 40, 1)[0]) * h   # broad sweeps + stringy tips
    wob = noise((h, 1), 10, 3)[:, 0][:, None]
    side = np.abs(x - w / 2) / (w / 2) < (0.9 - 0.35 * y / h + 0.25 * (wob - 0.5))   # wavy sides, narrowing
    alpha = ((m > 0) | (y < 0.25 * h)) & (y < length[None, :] - 6 * noise((h, w), 12, 2)) & side
    atlas[:, S // 2:, :3] = col; atlas[:, S // 2:, 3] = alpha
    img = to_img(atlas[..., :3]); a8 = (atlas[..., 3] * 255).astype(np.uint8)
    Image.fromarray(np.dstack([np.asarray(img), a8]), "RGBA").save(os.path.join(OUT, name))
    print("wrote", name, "coverage %.2f" % (a8.mean() / 255))


foliage(os.path.join(SENT, "170.jpg"), (0.15, 0.10, 0.45, 0.36), "AlleyLeaves.png", False)
foliage(os.path.join(SENT, "170.jpg"), (0.17, 0.12, 0.45, 0.36), "AlleyBougainvillea.png", True)
