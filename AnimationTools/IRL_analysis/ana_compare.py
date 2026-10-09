import sys, json
from PIL import Image, ImageDraw, ImageFont
sp = sys.argv[1]; sheet = sys.argv[2]; vids = [int(x) for x in sys.argv[3].split(",")]; labels = sys.argv[4].split(","); out = sys.argv[5]
per = int(sys.argv[6]) if len(sys.argv) > 6 else 4
bb = json.load(open(sp + "/ana/bbox.json"))
kim = Image.open(sp + "/" + sheet).convert("RGB")
tw, th = kim.size[0] // per, 330
n = len(vids); TW = 260; TH = 330
fnt = ImageFont.truetype("arial.ttf", 16)
comp = Image.new("RGB", (TW * n, TH * 2 + 24), (30, 30, 30))
d = ImageDraw.Draw(comp)
for k, f in enumerate(vids):
    b = bb[f - 1]; cx = (b[0] + b[1]) // 2 if (b[1] - b[0]) < 400 else b[1] - 150
    W = 420; im = Image.open(sp + "/irlfull/g_%02d.png" % f).convert("RGB").crop((cx - W // 2, 320, cx + W // 2, 880)).resize((int(W * TH / 560), TH))
    comp.paste(im.crop(((im.size[0] - TW) // 2, 0, (im.size[0] - TW) // 2 + TW, TH)), (k * TW, 0))
    r, c = divmod(k, per)
    t = kim.crop((c * tw, r * th, (c + 1) * tw, (r + 1) * th))
    comp.paste(t.crop(((tw - TW) // 2, 0, (tw - TW) // 2 + TW, TH)), (k * TW, TH))
    d.text((k * TW + 4, TH * 2 + 4), labels[k], fill=(255, 255, 0), font=fnt)
comp.save(sp + "/" + out)
print(comp.size)
