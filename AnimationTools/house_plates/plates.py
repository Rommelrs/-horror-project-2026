import random, math
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops
NUMS = [158,164,172,185,196,203,211,237,241,284,287,290,293,296,299,344,348,351,357,362,366,371,375,409,417]
CW, CH, COLS, ROWS = 512, 256, 4, 8
F = r"C:\Windows\Fonts"
fBig = ImageFont.truetype(F+r"\arialbd.ttf", 118)
fSmall = ImageFont.truetype(F+r"\arialbd.ttf", 30)
fAr = ImageFont.truetype(F+r"\arialbd.ttf", 40)
fArNum = ImageFont.truetype(F+r"\arialbd.ttf", 46)
MANZIL = "\uFEDD\uFEB0\uFEE8\uFEE3"   # منزل in presentation forms, visual (LTR) order
AD = "٠١٢٣٤٥٦٧٨٩"
atlas = Image.new("RGB", (CW*COLS, CH*ROWS), (20,50,110))
def cell(n, seed):
    rnd = random.Random(seed)
    base = (14+rnd.randint(-4,4), 62+rnd.randint(-6,6), 138+rnd.randint(-10,8))
    im = Image.new("RGB", (CW, CH), (60,62,66))            # bare steel round the rim
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((6,6,CW-7,CH-7), radius=26, fill=base)
    d.rounded_rectangle((20,20,CW-21,CH-21), radius=16, outline=(236,238,232), width=5)
    W = (240,241,236)
    d.text((44,30), "HOUSE", font=fSmall, fill=W)
    bb = d.textbbox((0,0), MANZIL, font=fAr); d.text((CW-44-(bb[2]-bb[0])-bb[0], 22), MANZIL, font=fAr, fill=W)
    s = str(n); bb = d.textbbox((0,0), s, font=fBig)
    d.text(((CW-(bb[2]-bb[0]))/2-bb[0], 62-bb[1]+6), s, font=fBig, fill=W)
    a = "".join(AD[int(c)] for c in s); bb = d.textbbox((0,0), a, font=fArNum)
    d.text(((CW-(bb[2]-bb[0]))/2-bb[0], 186-bb[1]), a, font=fArNum, fill=W)
    # screw holes
    for x in (34, CW-35):
        d.ellipse((x-9,CH//2-9,x+9,CH//2+9), fill=(70,72,74)); d.ellipse((x-6,CH//2-6,x+6,CH//2+6), fill=(150,150,145)); d.line((x-5,CH//2,x+5,CH//2), fill=(60,60,60), width=2)
        for i in range(rnd.randint(4,9)):   # rust bleeding down from the screw
            rx = x+rnd.randint(-6,6); ry = CH//2+rnd.randint(4,10); L = rnd.randint(10,50)
            d.line((rx,ry,rx+rnd.randint(-3,3),ry+L), fill=(120+rnd.randint(-20,20),62,30), width=rnd.randint(1,3))
    # chipped enamel along the edges
    for i in range(rnd.randint(10,22)):
        side = rnd.choice("tblr"); r = rnd.randint(2,9)
        x = rnd.randint(10,CW-10) if side in "tb" else (rnd.randint(6,16) if side=="l" else rnd.randint(CW-17,CW-7))
        y = rnd.randint(10,CH-10) if side in "lr" else (rnd.randint(6,16) if side=="t" else rnd.randint(CH-17,CH-7))
        d.ellipse((x-r,y-r,x+r,y+r), fill=rnd.choice([(52,50,48),(110,70,40),(90,90,88)]))
    # sun fade + dust: lighter top, dirtier bottom, grain
    noise = Image.effect_noise((CW,CH), 30).convert("L")
    grad = Image.linear_gradient("L").resize((CW,CH))
    im = Image.blend(im, Image.new("RGB",(CW,CH),(170,165,150)), 0.10+rnd.random()*0.08)
    dirt = Image.new("RGB",(CW,CH),(95,82,62))
    mask = ImageChops.multiply(grad, noise.point(lambda v: min(255, v*2))).point(lambda v: int(v*0.55))
    im = Image.composite(dirt, im, mask)
    speck = Image.effect_noise((CW,CH), 60).convert("L").point(lambda v: 255 if v > 205 else 0).filter(ImageFilter.GaussianBlur(0.6))
    im = Image.composite(Image.new("RGB",(CW,CH),(80,70,58)), im, speck.point(lambda v: v//3))
    # dust streaks running down
    st = Image.new("L",(CW,CH),0); sd = ImageDraw.Draw(st)
    for i in range(rnd.randint(8,18)):
        x = rnd.randint(20,CW-20); sd.line((x,20,x+rnd.randint(-4,4),CH-20), fill=rnd.randint(15,45), width=rnd.randint(2,7))
    im = Image.composite(Image.new("RGB",(CW,CH),(120,105,85)), im, st.filter(ImageFilter.GaussianBlur(2)))
    return im
for i,n in enumerate(NUMS):
    atlas.paste(cell(n, n*7+3), ((i%COLS)*CW, (i//COLS)*CH))
atlas.save(r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\Assets\Models\HousePlates\HousePlates_Atlas.png")
atlas.crop((0,0,CW*2,CH*2)).save("plates_preview.png")
print("ok")
