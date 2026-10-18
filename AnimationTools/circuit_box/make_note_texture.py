# Handwritten notice taped to the barber's storefront: BarberNote_Paper.png (512 x 640)
import os, random
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
OUT = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\Assets\Models\CircuitBox\Textures"
random.seed(11); rng = np.random.default_rng(11)
W, H = 480, 640
base = np.ones((H, W, 3), np.float32) * np.array([226, 214, 178], np.float32)
n = np.asarray(Image.fromarray((rng.random((H // 8, W // 8)) * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC), np.float32) / 255
base *= (0.86 + 0.22 * n)[..., None]
# darker, stained corners
yy, xx = np.mgrid[0:H, 0:W]
vig = np.clip(((xx - W / 2) / (W / 2)) ** 2 + ((yy - H / 2) / (H / 2)) ** 2, 0, 2)
base *= (1 - 0.14 * vig)[..., None]
img = Image.fromarray(np.clip(base, 0, 255).astype(np.uint8))
d = ImageDraw.Draw(img)
text = ("Kids have been entering the barber to get free candies instead of shaving so instead of leaving the door open, "
        "I decided to lock it, any coworkers there you should find the key in the circuit box, be sure to lock it afterwards "
        "and leave it at your car at least")
font = ImageFont.truetype(r"C:\Windows\Fonts\Inkfree.ttf", 33)
words, lines, cur = text.split(), [], ""
for w in words:
    t = (cur + " " + w).strip()
    if d.textlength(t, font=font) > W - 76:
        lines.append(cur); cur = w
    else:
        cur = t
lines.append(cur)
y = 46
for i, ln in enumerate(lines):
    x = 38 + random.randint(-3, 5)
    d.text((x, y + random.randint(-2, 2)), ln, font=font, fill=(36, 34, 52))
    y += 42
# torn top edge shadow and tape
d.rectangle([0, 0, W, 5], fill=(190, 176, 140))
tape = Image.new("RGBA", (130, 52), (235, 228, 196, 150))
for pos in [(40, -12), (W - 170, -10)]:
    img.paste(tape, pos, tape)
d.rectangle([0, H - 4, W, H], fill=(196, 182, 146))
img = img.filter(ImageFilter.GaussianBlur(0.5))
img.save(os.path.join(OUT, "BarberNote_Paper.png"))
print("ok", len(lines), "lines, last y", y)
