# Sand drag-mark decal texture (RGBA PNG 512x2048, 341 px/m -> 1.5 m x 6 m quad). The start of the drag is at the BOTTOM of the
# image, the far end (the way the thing was dragged) at the TOP, where the mark thins out and breaks up.
# A shallow wavering trough with raised banks of sand along both sides and two faint heel lines inside it.
import numpy as np
from PIL import Image, ImageFilter
rng = np.random.default_rng(11)
W, H = 512, 2048

def smooth_noise(n, scale):            # 1D smooth noise in [-1,1], 'scale' px per feature
    k = max(4, int(n / scale) + 2)
    pts = rng.uniform(-1, 1, k)
    xs = np.linspace(0, k - 1, n)
    i = np.floor(xs).astype(int).clip(0, k - 2); f = xs - i; f = f * f * (3 - 2 * f)
    return pts[i] * (1 - f) + pts[i + 1] * f

def stretched(sx, sy):                  # 2D noise stretched along y (long streaks), values 0..1
    a = rng.random((H // sy + 2, W // sx + 2)).astype("float32")
    im = Image.fromarray((a * 255).astype("uint8")).resize((W, H), Image.BICUBIC)
    return np.array(im, dtype=float) / 255.0

ys = np.arange(H)
t = 1.0 - ys / (H - 1)                  # 0 at the start (bottom), 1 at the far end (top)
cx = W / 2 + 16 * np.sin(2 * np.pi * ys / 1700 + 0.8) + 6 * np.sin(2 * np.pi * ys / 520 + 2.1) + 10 * smooth_noise(H, 300)
hw = 0.185 * 341 * (1 - 0.30 * t) * (0.93 + 0.07 * smooth_noise(H, 90))      # half width of the trough in px

X = np.arange(W)[None, :]
d = (X - cx[:, None]) / hw[:, None]     # signed distance across the drag, 1.0 = edge of the trough
ad = np.abs(d)

streak = 0.45 + 0.55 * (0.5 * stretched(1, 150) + 0.5 * stretched(1, 41))                                       # long lengthwise streaks in the sand
edge = np.clip((1 - ad) / 0.22, 0, 1); edge = edge * edge * (3 - 2 * edge)
trough = edge * (0.75 + 0.25 * np.clip(1 - ad, 0, 1)) * streak
heel = (np.exp(-((ad - 0.46) / 0.07) ** 2) * (ad < 0.8)) * (0.4 + 0.6 * stretched(1, 120))   # two thin heel grooves
bank = np.exp(-((ad - 1.16) / 0.20) ** 2) * (0.5 + 0.5 * stretched(2, 60))      # raised sand pushed up along the sides
lit = bank * (d < 0)                    # lit bank (light from the left)
shade = bank * (d > 0)                  # bank in shadow

# fade + break-up toward the far end: the mark thins out, then skips in patches
low = smooth_noise(H, 180)
fade = np.clip(1.0 - 0.80 * t ** 1.4, 0, 1) * (0.62 + 0.38 * (low * 0.5 + 0.5) ** 1.5)
start = np.clip(ys / 90.0 * -1 + (H - 1 - ys) / 70.0, 0, 1)                      # soft start in the last ~70 px at the bottom
start = np.clip((H - 1 - ys) / 70.0, 0, 1)
fade = (fade * start * np.clip((1.0 - t) / 0.18, 0, 1))[:, None]

grain = rng.random((H, W)) * 0.14 + 0.93
a_dark = np.clip(trough * 0.58 + heel * 0.34, 0, 1) * grain * fade
a_lit = np.clip(lit * 0.55, 0, 1) * fade
a_shade = np.clip(shade * 0.38, 0, 1) * fade

dark = np.array([0.19, 0.135, 0.08]); light = np.array([0.86, 0.74, 0.55]); shadow = np.array([0.22, 0.16, 0.10])
alpha = np.clip(a_dark + a_lit + a_shade, 0, 1)
den = np.maximum(alpha, 1e-4)[..., None]
rgb = (dark * a_dark[..., None] + light * a_lit[..., None] + shadow * a_shade[..., None]) / den
rgba = np.dstack([np.clip(rgb, 0, 1), alpha])
img = Image.fromarray((rgba * 255).astype("uint8"), "RGBA")
alpha_img = img.getchannel("A").filter(ImageFilter.GaussianBlur(0.8))                 # soften edges, nothing hard
img.putalpha(alpha_img)
img.save("D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/Assets/Decals/Sand/SandDragMark.png")
bg = Image.new("RGBA", img.size, (176, 150, 106, 255)); bg.alpha_composite(img)
bg.convert("RGB").resize((256, 1024)).save("C:/Users/Zayed/AppData/Local/Temp/claude/D--Unity-Stuff-NEWHorrorProject--horror-project-2026/cf51bab8-e583-42ee-8ab4-15af6ac1a2cc/scratchpad/drag_preview.png")
print("saved")
