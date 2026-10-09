import sys, json
from PIL import Image, ImageDraw, ImageFont
sp = sys.argv[1]; fr = [int(x) for x in sys.argv[2].split(",")]; outn = sys.argv[3]
bb = json.load(open(sp + "/ana/bbox.json"))
W, H, TOP = 460, 600, 310
tiles = []
for f in fr:
    b = bb[f - 1]
    cx = (b[0] + b[1]) // 2 if (b[1] - b[0]) < 400 else b[1] - 150
    left = int(round((cx - W / 2) / 50.0) * 50)
    im = Image.open(sp + "/irlfull/g_%02d.png" % f).convert("RGB").crop((left, TOP, left + W, TOP + H))
    im = im.resize((W * 2, H * 2), Image.LANCZOS)
    d = ImageDraw.Draw(im)
    try: fnt = ImageFont.truetype("arial.ttf", 18)
    except: fnt = None
    for x in range(left - left % 25 + 25, left + W, 25):
        c = (255, 0, 0) if x % 100 == 0 else (255, 160, 160)
        d.line([((x - left) * 2, 0), ((x - left) * 2, H * 2)], fill=c, width=1 if x % 100 else 2)
        if x % 100 == 0: d.text(((x - left) * 2 + 3, 3), str(x), fill=(255, 0, 0), font=fnt)
    for y in range(TOP - TOP % 25 + 25, TOP + H, 25):
        c = (0, 0, 255) if y % 100 == 0 else (160, 160, 255)
        d.line([(0, (y - TOP) * 2), (W * 2, (y - TOP) * 2)], fill=c, width=1 if y % 100 else 2)
        if y % 100 == 0: d.text((3, (y - TOP) * 2 + 3), str(y), fill=(0, 0, 255), font=fnt)
    d.text((W * 2 - 90, 5), "F%d" % f, fill=(255, 255, 0), font=fnt)
    tiles.append(im)
sheet = Image.new("RGB", (W * 2 * len(tiles), H * 2))
for k, t in enumerate(tiles): sheet.paste(t, (k * W * 2, 0))
sheet.save(sp + "/ana/" + outn)
