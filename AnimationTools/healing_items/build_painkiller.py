# PS1-era painkiller bottle + tablets for Unity (run headless):
#   D:/Blender/blender.exe -b --factory-startup --python AnimationTools/healing_items/build_painkiller.py
# Amber pill bottle (bevelled base, shoulder, neck thread), wrap-around paper label (separate shell, 300 deg), child-proof
# cap with real ridges and a recessed top, plus a scored tablet. Real metres, Z up. One 256x256 atlas painted here with
# numpy (label art, fake AO), point-filtered in Unity.
# Writes Assets/Models/HealingItems/PainkillerBlender.fbx (objects "PainkillerBottle", "Tablet") + PainkillerAtlas.png.
import bpy, bmesh, math
import numpy as np

PROJ = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/"
OUT = PROJ + "Assets/Models/HealingItems/"
SEG = 32
A = 256  # atlas size

# atlas regions in pixels (x, y, w, h), origin bottom-left
LABEL = (0, 0, 256, 88)
AMBER = (0, 96, 128, 64)
WHITE = (0, 168, 128, 24)
CAPSIDE = (128, 96, 128, 32)
CAPTOP = (128, 128, 64, 64)
TABTOP = (192, 128, 64, 64)


def uvr(reg, u, v):
    x, y, w, h = reg
    return ((x + 0.5 + u * (w - 1)) / A, (y + 0.5 + v * (h - 1)) / A)


bpy.ops.wm.read_factory_settings(use_empty=True)


def new_obj(name, bm):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def lathe(bm, uv, profile, reg, seg=SEG, a0=0.0, a1=2 * math.pi, radius_fn=None, vmap=None):
    """profile: list of (r, z). Quads between consecutive profile points, u = angle across reg, v from vmap(z) or
    cumulative profile length. radius_fn(i, r) can modulate the radius per segment column (cap ridges)."""
    full = abs(a1 - a0 - 2 * math.pi) < 1e-6
    cols = seg if full else seg + 1
    if vmap is None:
        lens = [0.0]
        for i in range(1, len(profile)):
            lens.append(lens[-1] + math.dist(profile[i - 1], profile[i]))
        tot = lens[-1] or 1.0
        vs = [l / tot for l in lens]
    else:
        vs = [vmap(z) for (_, z) in profile]
    rings = []
    for (r, z) in profile:
        ring = []
        for i in range(cols):
            a = a0 + (a1 - a0) * i / seg
            rr = radius_fn(i, r) if radius_fn else r
            ring.append(bm.verts.new((math.cos(a) * rr, math.sin(a) * rr, z)))
        rings.append(ring)
    for k in range(len(profile) - 1):
        for i in range(seg):
            j = (i + 1) % cols if full else i + 1
            f = bm.faces.new((rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]))
            u0, u1 = i / seg, (i + 1) / seg
            for loop, (u, v) in zip(f.loops, ((u0, vs[k]), (u1, vs[k]), (u1, vs[k + 1]), (u0, vs[k + 1]))):
                loop[uv].uv = uvr(reg, u, v)
    return rings


def cap_disc(bm, uv, ring, z, reg, up):
    c = bm.verts.new((0, 0, z))
    n = len(ring)
    for i in range(n):
        a, b = ring[i], ring[(i + 1) % n]
        f = bm.faces.new((c, a, b) if up else (c, b, a))
        for loop in f.loops:
            co = loop.vert.co
            r = max(math.hypot(ring[0].co.x, ring[0].co.y), 1e-6)
            loop[uv].uv = uvr(reg, 0.5 + 0.5 * co.x / r, 0.5 + 0.5 * co.y / r)


# ---------------------------------------------------------------- bottle body (amber)
bm = bmesh.new()
uv = bm.loops.layers.uv.new("UVMap")
H_NECK = 0.073
body = [(0.0195, 0.0), (0.0208, 0.0006), (0.0213, 0.0022), (0.0213, 0.0600), (0.0207, 0.0626), (0.0190, 0.0646),
        (0.0170, 0.0656), (0.0168, 0.0688), (0.0176, 0.0693), (0.0176, 0.0703), (0.0168, 0.0708), (0.0168, H_NECK)]
rings = lathe(bm, uv, body, AMBER, vmap=lambda z: z / H_NECK)
cap_disc(bm, uv, rings[0], 0.0, AMBER, up=False)

# ---------------------------------------------------------------- label (paper shell, open at the back)
LZ0, LZ1, LR = 0.012, 0.050, 0.02145
lab = [(LR, LZ0), (LR, LZ1)]
LA0, LA1 = math.radians(-150), math.radians(150)   # centred on +X; Unity side turns the bottle so it faces the camera
lathe(bm, uv, lab, LABEL, seg=28, a0=LA0, a1=LA1, vmap=lambda z: (z - LZ0) / (LZ1 - LZ0))

# ---------------------------------------------------------------- child-proof cap (white, ridged)
CZ0, CZ1, CR = 0.0668, 0.0880, 0.0236
lathe(bm, uv, [(0.0226, CZ0 - 0.0004), (CR - 0.0004, CZ0), (CR, CZ0 + 0.0012)], CAPSIDE, seg=SEG,
      vmap=lambda z: 0.0)
ridge = lambda i, r: r if i % 2 == 0 else r - 0.0007
lathe(bm, uv, [(CR, CZ0 + 0.0012), (CR, CZ1 - 0.0010)], CAPSIDE, seg=72, radius_fn=ridge,
      vmap=lambda z: (z - CZ0) / (CZ1 - CZ0))
top = [(CR, CZ1 - 0.0010), (CR - 0.0006, CZ1), (0.0200, CZ1), (0.0192, CZ1 - 0.0005)]
rings = lathe(bm, uv, top, CAPSIDE, vmap=lambda z: 1.0)
# recessed top face
for loop_ring in [rings[-1]]:
    pass
inner = rings[-1]
c = bm.verts.new((0, 0, CZ1 - 0.0005))
for i in range(len(inner)):
    a, b = inner[i], inner[(i + 1) % len(inner)]
    f = bm.faces.new((c, a, b))
    for loop in f.loops:
        co = loop.vert.co
        loop[uv].uv = uvr(CAPTOP, 0.5 + 0.5 * co.x / 0.0192, 0.5 + 0.5 * co.y / 0.0192)
# underside of the cap skirt (hidden, keeps it closed)
bm.normal_update()
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bottle = new_obj("PainkillerBottle", bm)

# ---------------------------------------------------------------- tablet
bm = bmesh.new()
uv = bm.loops.layers.uv.new("UVMap")
tab = [(0.0, 0.0), (0.0058, 0.0), (0.0067, 0.0008), (0.0069, 0.0019), (0.0067, 0.0030), (0.0058, 0.0037),
       (0.0034, 0.0042)]
rings = lathe(bm, uv, tab[1:], WHITE, seg=20)
cap_disc(bm, uv, rings[0], 0.0, TABTOP, up=False)
c = bm.verts.new((0, 0, 0.0043))
last = rings[-1]
for i in range(len(last)):
    a, b = last[i], last[(i + 1) % len(last)]
    f = bm.faces.new((c, a, b))
    for loop in f.loops:
        co = loop.vert.co
        loop[uv].uv = uvr(TABTOP, 0.5 + 0.5 * co.x / 0.0069, 0.5 + 0.5 * co.y / 0.0069)
bm.normal_update()
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
tablet = new_obj("Tablet", bm)

# smooth with sharp creases at the profile corners
for ob in (bottle, tablet):
    ob.data.shade_smooth()
    ob.data.set_sharp_from_angle(angle=math.radians(38))

# ---------------------------------------------------------------- atlas
img = np.ones((A, A, 4), dtype=np.float32)
rng = np.random.default_rng(3)


def region(reg):
    x, y, w, h = reg
    return img[y:y + h, x:x + w]


def noise(h, w, amt):
    return (rng.random((h, w, 1)) - 0.5) * 2 * amt


def blobs(h, w, scale, seed):
    r = np.random.default_rng(seed)
    small = r.random((max(2, h // scale + 2), max(2, w // scale + 2)))
    ys = np.linspace(0, small.shape[0] - 1.001, h)
    xs = np.linspace(0, small.shape[1] - 1.001, w)
    y0, x0 = ys.astype(int), xs.astype(int)
    fy, fx = (ys - y0)[:, None], (xs - x0)[None, :]
    a = small[y0][:, x0]; b = small[y0][:, x0 + 1]; c = small[y0 + 1][:, x0]; d = small[y0 + 1][:, x0 + 1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def put(reg, rgb):
    region(reg)[..., :3] = rgb


# amber plastic: vertical gradient (v = height), darker near the base and under the shoulder, faint scuffs
x, y, w, h = AMBER
v = np.linspace(0, 1, h)[:, None, None]
amber = np.array([0.66, 0.29, 0.045])
dark = np.array([0.36, 0.13, 0.02])
col = amber * (0.82 + 0.18 * np.clip(v * 1.4, 0, 1)) * np.ones((h, w, 3))
shade = np.clip(1 - v / 0.06, 0, 1) * 0.5 + np.clip((v - 0.8) / 0.2, 0, 1) * 0.35
col = col * (1 - shade) + dark * shade
col += noise(h, w, 0.018)
col *= (0.93 + 0.07 * blobs(h, w, 10, 5))[..., None]
put(AMBER, np.clip(col, 0, 1))

# white plastic (tablet sides)
x, y, w, h = WHITE
put(WHITE, np.clip(np.array([0.9, 0.89, 0.85]) + noise(h, w, 0.015), 0, 1))

# cap side: ridges come from the geometry; v = 0 bottom lip (grimy) .. 1 top edge
x, y, w, h = CAPSIDE
v = np.linspace(0, 1, h)[:, None, None]
col = np.array([0.9, 0.89, 0.86]) * np.ones((h, w, 3))
col *= 1 - 0.28 * np.clip(1 - v / 0.25, 0, 1)
col *= (0.94 + 0.06 * blobs(h, w, 6, 7))[..., None]
put(CAPSIDE, np.clip(col + noise(h, w, 0.012), 0, 1))

# cap top: "PUSH DOWN & TURN" ring of arrows
x, y, w, h = CAPTOP
yy, xx = np.mgrid[0:h, 0:w]
cx = cy = (w - 1) / 2
rr = np.hypot(xx - cx, yy - cy) / cx
ang = np.arctan2(yy - cy, xx - cx)
col = np.array([0.92, 0.91, 0.88]) * np.ones((h, w, 3))
ringmask = (np.abs(rr - 0.72) < 0.05) & (np.mod(ang, math.pi / 3) < 0.7)
col[ringmask] = [0.55, 0.53, 0.5]
for k in range(6):   # arrow heads
    a = k * math.pi / 3 + 0.7
    px, py = cx + math.cos(a) * 0.72 * cx, cy + math.sin(a) * 0.72 * cx
    m = np.hypot(xx - px, yy - py) < 2.6
    col[m] = [0.55, 0.53, 0.5]
col[np.abs(rr - 0.95) < 0.05] *= 0.85
put(CAPTOP, np.clip(col + noise(h, w, 0.01), 0, 1))

# tablet top: off-white with a score line, edge slightly darker
x, y, w, h = TABTOP
yy, xx = np.mgrid[0:h, 0:w]
rr = np.hypot(xx - (w - 1) / 2, yy - (h - 1) / 2) / ((w - 1) / 2)
col = np.array([0.93, 0.92, 0.88]) * np.ones((h, w, 3))
col[np.abs(yy - (h - 1) / 2) < 2.2] *= 0.72
col *= (1 - 0.18 * np.clip((rr - 0.75) / 0.25, 0, 1))[..., None]
put(TABTOP, np.clip(col + noise(h, w, 0.012), 0, 1))

# ---- label art
x, y, w, h = LABEL
paper = np.array([0.94, 0.92, 0.85])
col = paper * np.ones((h, w, 3))
col *= (0.95 + 0.05 * blobs(h, w, 12, 11))[..., None]
yy, xx = np.mgrid[0:h, 0:w]
red = np.array([0.72, 0.1, 0.08])
orange = np.array([0.88, 0.42, 0.12])
col[(yy >= 79) & (yy <= 86)] = red
col[(yy >= 75) & (yy <= 76)] = orange
col[(yy >= 2) & (yy <= 8)] = red
col[(yy >= 11) & (yy <= 11)] = orange

# red crescent (anti-aliased) on the front left
def disc(cx, cy, r):
    return np.clip(r + 0.5 - np.hypot(xx - cx, yy - cy), 0, 1)
cres = np.clip(disc(40, 43, 19) - disc(47.5, 47, 16.5), 0, 1)[..., None]
col = col * (1 - cres) + red * cres

# 5x7 pixel font
G = {
    'P': ["11110", "10001", "10001", "11110", "10000", "10000", "10000"],
    'A': ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
    'I': ["11111", "00100", "00100", "00100", "00100", "00100", "11111"],
    'N': ["10001", "11001", "10101", "10011", "10001", "10001", "10001"],
    'K': ["10001", "10010", "10100", "11000", "10100", "10010", "10001"],
    'L': ["10000", "10000", "10000", "10000", "10000", "10000", "11111"],
    'E': ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
    'R': ["11110", "10001", "10001", "11110", "10100", "10010", "10001"],
    'T': ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
    'B': ["11110", "10001", "10001", "11110", "10001", "10001", "11110"],
    'S': ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
    'M': ["10001", "11011", "10101", "10101", "10001", "10001", "10001"],
    'G': ["01110", "10001", "10000", "10111", "10001", "10001", "01110"],
    '5': ["11111", "10000", "11110", "00001", "00001", "10001", "01110"],
    '0': ["01110", "10001", "10011", "10101", "11001", "10001", "01110"],
    '2': ["01110", "10001", "00001", "00110", "01000", "10000", "11111"],
    ' ': ["00000"] * 7,
}


def text(s, x0, y0, scale, rgb):
    global col
    cx = x0
    for ch in s:
        g = G[ch]
        for r_, row in enumerate(g):
            for c_, bit in enumerate(row):
                if bit == "1":
                    ys0 = y0 + (6 - r_) * scale
                    col[ys0:ys0 + scale, cx + c_ * scale:cx + c_ * scale + scale] = rgb
        cx += 6 * scale


ink = np.array([0.14, 0.11, 0.1])
text("PAINKILLER", 72, 50, 2, ink)
text("500 MG", 72, 36, 1, red)
text("20 TABLETS", 112, 36, 1, ink)
for i, (yl, xl) in enumerate([(26, 150), (22, 130), (18, 160)]):
    col[yl:yl + 1, 72:72 + xl - 70 * (i % 2)] = np.array([0.55, 0.52, 0.48])
# barcode on the back strip
for i in range(26):
    if rng.random() > 0.45:
        col[18:62, 222 + i:223 + i] = ink
# fake AO: label edges and a faint fold
col *= (1 - 0.12 * np.clip(1 - np.minimum(yy, h - 1 - yy) / 4, 0, 1))[..., None]
col += noise(h, w, 0.01)
put(LABEL, np.clip(col, 0, 1))

im = bpy.data.images.new("PainkillerAtlas", A, A, alpha=True)
im.pixels.foreach_set(img.ravel())
im.filepath_raw = OUT + "PainkillerAtlas.png"
im.file_format = "PNG"
im.save()

# ---------------------------------------------------------------- export
mat = bpy.data.materials.new("Painkiller")
for ob in (bottle, tablet):
    ob.data.materials.append(mat)
bpy.ops.object.select_all(action="DESELECT")
for ob in (bottle, tablet):
    ob.select_set(True)
bpy.context.view_layer.objects.active = bottle
bpy.ops.export_scene.fbx(filepath=OUT + "PainkillerBlender.fbx", use_selection=True, apply_unit_scale=True,
                         apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True, axis_forward="-Z", axis_up="Y",
                         mesh_smooth_type="FACE", use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False)
print("PAINKILLER_DONE", len(bottle.data.polygons), len(tablet.data.polygons))
