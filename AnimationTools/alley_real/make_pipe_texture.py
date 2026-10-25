# 64 px atlas for the Alley 1 wall taps (build_wall_taps.py): four horizontal bands, v from the bottom -
#   0.00-0.25 galvanised steel (grey, rust blooms, dirt), 0.25-0.50 black PE hose (sun-faded, dusty),
#   0.50-0.75 red paint (valve wheel, chipped), 0.75-1.00 brass (tap body, tarnished).
# u runs along the pipe (repeats), v round it within the band. Deterministic. Output: Assets/Textures/AlleyReal/AlleyPipes.png
import os, random
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
OUT = os.path.join(ROOT, "Assets", "Textures", "AlleyReal", "AlleyPipes.png")
S = 64
rnd = random.Random(21)
img = Image.new("RGB", (S, S))
px = img.load()


def band(row0, base, noise, spots):
    # row0: first image row (top = 0); 16 rows per band
    blooms = [(rnd.uniform(0, S), rnd.uniform(0, 16), rnd.uniform(1.5, 4)) for _ in range(spots[0])]
    for y in range(16):
        for x in range(S):
            c = [base[i] * (1 + rnd.uniform(-noise, noise)) for i in range(3)]
            for bx, by, br in blooms:
                d = ((x - bx) ** 2 + (y - by) ** 2) ** 0.5
                if d < br:
                    k = 1 - d / br
                    c = [c[i] * (1 - k) + spots[1][i] * k for i in range(3)]
            px[x, row0 + y] = tuple(max(0, min(255, int(v * 255))) for v in c)


# image rows count from the top, UV v from the bottom: band at v 0.75-1 = rows 0-15
band(0, (0.47, 0.37, 0.20), 0.10, (6, (0.30, 0.25, 0.15)))     # brass, tarnish
band(16, (0.62, 0.13, 0.10), 0.08, (10, (0.55, 0.50, 0.44)))   # red paint, chips show grey metal
band(32, (0.11, 0.105, 0.10), 0.12, (9, (0.42, 0.37, 0.29)))   # black hose, sand dust
band(48, (0.40, 0.395, 0.37), 0.09, (12, (0.36, 0.24, 0.14)))  # galvanised gone dull and dusty, rust
os.makedirs(os.path.dirname(OUT), exist_ok=True)
img.save(OUT)
print("wrote", OUT)
