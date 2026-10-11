# Texture for the knocked-over bucket by the park toilet: cheap blue plastic, sun-bleached on the upper half, scuffed, with dried mud
# and sand caked on the lower part (photo grain from the user's concrete/sand photos). UV: u = around the bucket, v = height (0 = base).
# The top band (v > 0.9) is the moulded rim; the inside uses the same texture.
import numpy as np
from PIL import Image, ImageFilter

REF = "D:/Unity Stuff/Shamkha Refs/"
OUT = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/Assets/Textures/Props/"
rng = np.random.default_rng(4)
N = 512


def blur(a, r):
    im = Image.fromarray(np.clip(a * 255, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r))
    return np.asarray(im, np.float32) / 255.0


def smooth(t):
    t = np.clip(t, 0, 1); return t * t * (3 - 2 * t)


def noise(r):
    n = blur(rng.random((N, N)).astype(np.float32), r)
    return (n - n.mean()) / (n.std() + 1e-6)


v = np.linspace(0, 1, N, dtype=np.float32)[:, None] * np.ones((1, N), np.float32)
conc = np.asarray(Image.open(REF + "IMG_2021.jpg").convert("L").crop((800, 600, 2800, 2600)).resize((N, N)), np.float32) / 255.0
conc = (conc - blur(conc, 30)); conc /= conc.std() + 1e-6
sand = np.asarray(Image.open(REF + "IMG_2017.jpg").convert("RGB").crop((1000, 1500, 2500, 3000)).resize((N, N)), np.float32) / 255.0
sand = sand / sand.reshape(-1, 3).mean(0) * np.array([0.58, 0.48, 0.34])

blue = np.array([0.16, 0.33, 0.52]); bleached = np.array([0.42, 0.55, 0.62])
fade = smooth((v - 0.35) / 0.6) * (0.6 + 0.4 * smooth((noise(40) + 1) / 2))
col = blue[None, None] * (1 - fade[..., None]) + bleached[None, None] * fade[..., None]
col *= (1 + 0.05 * noise(2))[..., None]
# moulded rim band and a couple of ribs
col *= (1 - 0.18 * smooth((v - 0.9) / 0.02) * (1 - smooth((v - 0.97) / 0.02)))[..., None]
for r in (0.12, 0.62):
    col *= (1 - 0.12 * np.exp(-((v - r) / 0.006) ** 2))[..., None]
# scratches
sc = np.zeros((N, N), np.float32)
for _ in range(30):
    x0, y0 = rng.integers(0, N), rng.integers(0, N); L = rng.integers(10, 60); a = rng.uniform(-0.4, 0.4) + (np.pi / 2 if rng.random() < 0.3 else 0)
    for t in range(L): sc[int(y0 + t * np.sin(a)) % N, int(x0 + t * np.cos(a)) % N] = 1
col = col + blur(sc, 0.6)[..., None] * 0.1
# grime: darker in the moulding recesses, dirty streaks, caked wet sand on the lower third
col *= (1 - 0.25 * smooth((noise(20) * 0.6 + conc * 0.3 - 0.4) / 0.8))[..., None]
cake = smooth((0.38 - v + 0.12 * noise(12) + 0.06 * conc) / 0.08)
col = col * (1 - cake[..., None]) + sand * (0.75 + 0.25 * (conc[..., None] * 0.2 + 0.5)) * cake[..., None]
mud = smooth((noise(6) * 0.6 + conc * 0.4 - 1.1) / 0.4) * smooth((0.6 - v) / 0.3)
col = col * (1 - 0.6 * mud[..., None]) + np.array([0.2, 0.15, 0.1]) * 0.6 * mud[..., None]

import os
os.makedirs(OUT, exist_ok=True)
Image.fromarray((np.flipud(np.clip(col, 0, 1)) * 255 + 0.5).astype(np.uint8)).save(OUT + "Bucket_Plastic.png")
print("saved")
