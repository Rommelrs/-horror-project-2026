import sys, json
from PIL import Image, ImageDraw, ImageFont
sp = sys.argv[1]; fr = [int(x) for x in sys.argv[2].split(",")]; outn = sys.argv[3]
bb = json.load(open(sp + "/ana/bbox.json"))
W, H, TOP = 400, 560, 320
fnt = ImageFont.truetype("arial.ttf", 16)
tiles = []
for f in fr:
    b = bb[f - 1]
    cx = (b[0] + b[1]) // 2 if (b[1] - b[0]) < 400 else b[1] - 150
    left = int(round((cx - W / 2) / 50.0) * 50)
    im = Image.open(sp + "/irlfull/g_%02d.png" % f).convert("RGB").crop((left, TOP, left + W, TOP + H)).resize((W * 2, H * 2), Image.LANCZOS)
    d = ImageDraw.Draw(im, "RGBA")
    for x in range(left, left + W, 10):
        big = x % 50 == 0
        d.line([((x - left) * 2, 0), ((x - left) * 2, H * 2)], fill=(255, 0, 0, 200 if big else 70), width=1)
        if big: d.text(((x - left) * 2 + 2, 2), str(x), fill=(255, 0, 0), font=fnt)
    for y in range(TOP, TOP + H, 10):
        big = y % 50 == 0
        d.line([(0, (y - TOP) * 2), (W * 2, (y - TOP) * 2)], fill=(0, 0, 255, 200 if big else 70), width=1)
        if big: d.text((2, (y - TOP) * 2 + 2), str(y), fill=(0, 0, 255), font=fnt)
    d.text((W * 2 - 60, 20), "F%d" % f, fill=(255, 255, 0), font=fnt)
    tiles.append(im)
sheet = Image.new("RGB", (W * 2 * len(tiles), H * 2))
for k, t in enumerate(tiles): sheet.paste(t, (k * W * 2, 0))
sheet.save(sp + "/ana/" + outn)
print(outn)
