# Textures for the park back alley (behind the park toilet, between the shop backs and the BACK SHOP HOUSE), from the
# user's photos of a real alley sent 2026-10-25 (white rendered walls, octagon-and-dot pavers in pink and grey, a steel
# grid manhole cover, spray graffiti, a window with an exhaust fan, a grey meter box). PS2-sized, point-filtered in Unity.
#   ParkAlleyRender.png    256^2 white plaster, seamless, one tile = 3.2 x 3.2 m (Custom/ParkAlleyWall, tiled both ways)
#   ParkAlleyFoot.png      256x64 RGBA grime/rising damp along the wall foot, 3.2 m x 0.8 m, alpha = amount
#   ParkAlleyPavers.png    256^2 octagon-and-dot pavers, seamless, one tile = 2.4 m (8 x 8 octagons of 30 cm)
#   ParkAlleyManhole.png   128x96 cover + concrete frame, rectified from the photo
#   ParkAlleyGraffiti.png  256x256 RGBA spray marks cut from the photo (alpha = paint)
#   ParkAlleyBits.png      128^2 atlas: left half window with exhaust fan, right half meter box door
# Run: python AnimationTools/park_alley/make_park_alley_textures.py   (Pillow + numpy)
import os
import numpy as np
from PIL import Image, ImageFilter, ImageDraw

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
OUT = os.path.join(ROOT, "Assets", "Textures", "ParkAlley")
SENT = os.path.join(os.environ.get("LOCALAPPDATA", ""), "Temp", "claude", "D--Unity-Stuff-NEWHorrorProject--horror-project-2026",
                    "cf51bab8-e583-42ee-8ab4-15af6ac1a2cc", "images")
PHOTO_ENTRY = os.path.join(SENT, "188.jpg")    # alley mouth: meter box, white wall
PHOTO_CORNER = os.path.join(SENT, "189.jpg")   # graffiti wall, window with fan, white render
PHOTO_LANE = os.path.join(SENT, "190.jpg")     # pavers, manhole covers
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(23)


def arr(im):
    return np.asarray(im).astype(np.float32) / 255.0


def to_img(a):
    return Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8))


def blur(a, r):
    if a.ndim == 2:
        return arr(Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r)))
    return arr(to_img(a).filter(ImageFilter.GaussianBlur(r)))


def delight(a, radius):
    # remove large-scale lighting (shadows, gradients): divide by a heavy blur, keep the mean colour
    return a / np.maximum(blur(a, radius), 1e-3) * a.reshape(-1, 3).mean(0)


def seamless(a):
    # cross-fade the image with its half-offset copy so opposite edges meet
    h, w, _ = a.shape
    sh = np.roll(np.roll(a, h // 2, 0), w // 2, 1)
    y = np.abs(np.linspace(-1, 1, h))[:, None]; x = np.abs(np.linspace(-1, 1, w))[None, :]
    m = np.clip(np.maximum(x, y) * 1.6 - 0.6, 0, 1)[..., None]
    return a * (1 - m) + sh * m


def tileable_noise(h, w, cells, octaves=4):
    # value noise on a wrapping lattice (seamless)
    out = np.zeros((h, w), np.float32); amp = 1.0; total = 0
    for o in range(octaves):
        c = cells * 2 ** o
        lat = rng.random((c, c)).astype(np.float32)
        lat = np.concatenate([lat, lat[:1]], 0); lat = np.concatenate([lat, lat[:, :1]], 1)
        n = arr(Image.fromarray((lat * 255).astype(np.uint8)).resize((w + w // c, h + h // c), Image.BICUBIC))
        out += n[:h, :w] * amp; total += amp; amp *= 0.5
    return out / total


def rectify(im, quad, size):
    # quad = TL, TR, BR, BL in image pixels -> size (w, h) straight-on
    tl, tr, br, bl = quad
    return im.transform(size, Image.QUAD, (tl[0], tl[1], bl[0], bl[1], br[0], br[1], tr[0], tr[1]), Image.BICUBIC)


# ── white render: the smooth white plaster of the photos' middle building, hairline cracks, faint dirt ──
def render():
    S = 256
    src = Image.open(PHOTO_CORNER).convert("RGB").crop((1520, 280, 1960, 720)).resize((S, S), Image.BICUBIC)
    a = delight(arr(src), 16)
    g = a.mean(2, keepdims=True)
    a = (g - g.mean()) * 1.4 + np.array([0.86, 0.85, 0.81])          # keep the grain, set the paint colour
    a = seamless(a)
    # broad soft patches: a slightly greyer repaint here and there
    n = tileable_noise(S, S, 3)
    a *= (1 - 0.06 * np.clip((n - 0.55) * 4, 0, 1))[..., None]
    # hairline cracks: wandering lines, mostly vertical, a few branching
    im = to_img(a); d = ImageDraw.Draw(im)
    for _ in range(4):
        x, y = rng.uniform(0, S), rng.uniform(0, S)
        ang = rng.uniform(-0.5, 0.5) + (np.pi / 2 if rng.random() < 0.7 else 0)
        col = tuple(int(c) for c in (np.array([0.62, 0.60, 0.56]) * 255))
        for _ in range(int(rng.uniform(20, 60))):
            nx, ny = x + np.cos(ang) * 3, y + np.sin(ang) * 3
            for ox in (-S, 0, S):
                for oy in (-S, 0, S):
                    d.line([(x + ox, y + oy), (nx + ox, ny + oy)], fill=col, width=1)
            x, y = nx % S, ny % S; ang += rng.uniform(-0.45, 0.45)
    a = arr(im) * 0.25 + a * 0.75   # cracks only faint
    to_img(a).save(os.path.join(OUT, "ParkAlleyRender.png"))


# ── foot grime: brownish damp and sand splash, ragged top edge, strongest at the ground ──
def foot():
    W, H = 256, 64
    n = tileable_noise(H, W, 4)[:, :W]
    edge = tileable_noise(4, W, 8, 3)[0]                                    # ragged top, wraps in u
    v = np.linspace(1, 0, H)[:, None]                                       # 0 at the foot (image bottom)
    top = 0.35 + 0.45 * edge[None, :]
    alpha = np.clip((top - v) / 0.25, 0, 1) * (0.55 + 0.45 * n)
    alpha = np.maximum(alpha, np.clip(0.18 - v, 0, 1) * 4)                  # the last few cm always dirty
    speck = (rng.random((H, W)) < 0.04 * (1 - v)).astype(np.float32)        # splash dots
    alpha = np.clip(alpha + speck * 0.5, 0, 0.9)
    col = np.array([0.58, 0.52, 0.45]) * (0.85 + 0.3 * n[..., None])
    rgba = np.concatenate([col, alpha[..., None]], 2)
    Image.fromarray((np.clip(rgba, 0, 1) * 255).astype(np.uint8), "RGBA").save(os.path.join(OUT, "ParkAlleyFoot.png"))


# ── pavers: octagon-and-dot like the photo, pink and grey octagons, small grey square dots, sandy joints ──
def pavers():
    S, N = 256, 8
    c = S / N                                       # 32 px per octagon (30 cm)
    y, x = np.mgrid[0:S, 0:S].astype(np.float32) + 0.5
    cx, cy = np.floor(x / c), np.floor(y / c)
    lx, ly = x / c - cx - 0.5, y / c - cy - 0.5     # -0.5..0.5 inside the octagon's square
    # octagon: |x|,|y| <= 0.5 and |x|+|y| <= 0.5 + k (corners cut where four meet -> the dot)
    k = 0.207                                       # regular octagon
    oct_d = np.maximum(np.maximum(np.abs(lx), np.abs(ly)), (np.abs(lx) + np.abs(ly)) - k)   # 0.5 = octagon edge
    in_dot = (np.abs(lx) + np.abs(ly)) - k > 0.5
    # grain from the photo's grey pavers (delighted, desaturated)
    src = Image.open(PHOTO_LANE).convert("RGB").crop((560, 1300, 900, 1480)).resize((S, S), Image.BICUBIC)
    grain = delight(arr(src), 10).mean(2)
    grain = seamless(np.repeat(grain[..., None], 3, 2))[..., 0]
    grain = (grain - grain.mean()) * 0.8
    # colour per octagon: diagonal bands of pink and grey like the photo, with randomness
    pink = np.array([0.70, 0.52, 0.49]); grey = np.array([0.75, 0.71, 0.65]); dark = np.array([0.62, 0.46, 0.43])
    ids = (cx.astype(int) % N, cy.astype(int) % N)
    choice = np.zeros((N, N), int)
    for i in range(N):
        for j in range(N):
            r = rng.random()
            band = ((i + j) // 2) % 2 == 0
            choice[j, i] = (0 if r < 0.75 else 1) if band else (1 if r < 0.7 else (0 if r < 0.9 else 2))
    cols = np.stack([pink, grey, dark])[choice[ids[1], ids[0]]]
    tint = (0.92 + 0.16 * rng.random((N, N)))[ids[1], ids[0]][..., None]
    a = cols * tint
    # dots: grey square between four octagons
    a = np.where(in_dot[..., None], np.array([0.60, 0.57, 0.52]), a)
    # bevel: darken towards each octagon's edge, joints sandy
    edge = np.clip((0.5 - oct_d) / 0.06, 0, 1)
    dot_edge = np.clip(((np.abs(lx) + np.abs(ly)) - k - 0.5) / 0.05, 0, 1)
    shade = np.where(in_dot, 0.8 + 0.2 * dot_edge, 0.72 + 0.28 * edge)
    a = a * shade[..., None] + grain[..., None] * 0.6
    joint = np.where(in_dot, dot_edge < 0.3, oct_d > 0.482)
    a = np.where(joint[..., None], np.array([0.80, 0.74, 0.63]), a)
    # dirt: soft dark patches and sand drift (seamless)
    n1 = tileable_noise(S, S, 2); n2 = tileable_noise(S, S, 4)
    a *= (1 - 0.18 * np.clip((n1 - 0.55) * 3, 0, 1))[..., None]
    a = a * (1 - 0.25 * np.clip((n2 - 0.6) * 4, 0, 1))[..., None] + np.array([0.78, 0.70, 0.56]) * 0.25 * np.clip((n2 - 0.6) * 4, 0, 1)[..., None]
    to_img(a).save(os.path.join(OUT, "ParkAlleyPavers.png"))


# ── manhole: the nearer cover in the lane photo, rectified (outer concrete frame) ──
def manhole():
    im = Image.open(PHOTO_LANE).convert("RGB")
    out = rectify(im, ((925, 1043), (1228, 1043), (1245, 1152), (893, 1148)), (128, 96))
    a = delight(arr(out), 30)
    a = (a - a.mean()) * 1.1 + a.mean() * 0.92
    to_img(a).save(os.path.join(OUT, "ParkAlleyManhole.png"))


# ── graffiti: spray marks off the corner photo's white wall; alpha = how far a pixel is from the wall paint ──
def graffiti():
    S = 256
    im = Image.open(PHOTO_CORNER).convert("RGB").crop((0, 120, 760, 880)).resize((S, S), Image.LANCZOS)
    a = arr(im)
    wall = blur(arr(im.filter(ImageFilter.MedianFilter(15))), 6)
    diff = wall - a
    darker = np.clip(diff.mean(2), 0, 1)
    sat = a.max(2) - a.min(2) - (wall.max(2) - wall.min(2))
    alpha = np.clip((np.maximum(darker * 3.2, sat * 3.0) - 0.2) * 2.5, 0, 1)
    alpha = np.where(blur(alpha, 1.2) > 0.25, alpha, 0)                       # drop lone grain specks
    # keep marks only (drop the shadowed right edge/planter at the bottom)
    yy, xx = np.mgrid[0:S, 0:S]
    alpha *= ((xx < S * 0.86) & (yy < S * 0.83)).astype(np.float32)
    col = np.clip(a / np.maximum(wall, 1e-3) * 0.86, 0, 1)                  # paint colour against a white wall
    rgba = np.concatenate([col, alpha[..., None]], 2)
    Image.fromarray((np.clip(rgba, 0, 1) * 255).astype(np.uint8), "RGBA").save(os.path.join(OUT, "ParkAlleyGraffiti.png"))


# ── bits atlas: window with exhaust fan (left half, 64x128) + meter box door (right half, 64x128) ──
def bits():
    atlas = Image.new("RGB", (128, 128))
    win = Image.open(PHOTO_CORNER).convert("RGB").crop((942, 292, 1042, 528)).resize((64, 128), Image.LANCZOS)
    atlas.paste(win, (0, 0))
    box = Image.open(PHOTO_ENTRY).convert("RGB").crop((1297, 277, 1365, 375)).resize((64, 128), Image.LANCZOS)
    atlas.paste(box, (64, 0))
    a = delight(arr(atlas), 40) * 0.5 + arr(atlas) * 0.5
    to_img(a).save(os.path.join(OUT, "ParkAlleyBits.png"))


render(); foot(); pavers(); manhole(); graffiti(); bits()
print("wrote", OUT)
