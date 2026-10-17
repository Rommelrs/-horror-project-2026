# Scaffolding tower for the Alley 2 bag scenario (replaces the stack of cardboard boxes).  Blender 5.x
# exec(open(this).read()) -> builds 4 stacked modules Scaff_1..Scaff_4 (each 1.0 x 1.0 x 0.9 m, origin at its centre so each can be its own
# physics piece) + a gallows mast / beam / rope on the top module, one 128x128 PSX atlas, exports FBX + PNG to Assets/Models/Scaffold.
# Total height 3.6 m (the box stack was 3.63 m).  Unity: Blender +X = +X, Blender -Y = +Z.
import bpy, math, os
import numpy as np
from mathutils import Vector

V = Vector
ROOT = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026"
OUT = os.path.join(ROOT, "Assets", "Models", "Scaffold")
TEX = os.path.join(ROOT, "AnimationTools", "scaffold", "tex")
os.makedirs(OUT, exist_ok=True); os.makedirs(TEX, exist_ok=True)
for o in [o for o in bpy.data.objects if o.name.startswith('Scaff_')]:
    bpy.data.objects.remove(o, do_unlink=True)

# ---- atlas 128x128: steel (0,64,64,64), wood (64,64,64,64), rope (0,32,64,32), dark metal (64,32,64,32)
REG = {'steel': (0, 64, 64, 64), 'wood': (64, 64, 64, 64), 'rope': (0, 32, 64, 32), 'dark': (64, 32, 64, 32)}
def regUV(name, u, v):
    x0, y0, w, h = REG[name]
    u = min(max(u, 0.0), 1.0); v = min(max(v, 0.0), 1.0)
    return ((x0 + 0.5 + u * (w - 1)) / 128.0, (y0 + 0.5 + v * (h - 1)) / 128.0)

class Part:
    def __init__(s, name, origin):
        s.name = name; s.origin = V(origin); s.verts = []; s.faces = []
    def v(s, p): s.verts.append(V(p) - s.origin); return len(s.verts) - 1

def box(part, c, size, region, grain_axis=None):
    """axis-aligned box centred c (world, Blender), outward-wound quads; uv: u along the longest axis (or grain_axis), v across"""
    c = V(c); h = [size[0] / 2, size[1] / 2, size[2] / 2]
    ids = {}
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                ids[(sx, sy, sz)] = part.v(V((c.x + sx * h[0], c.y + sy * h[1], c.z + sz * h[2])))
    faces = [((1, -1, -1), (1, 1, -1), (1, 1, 1), (1, -1, 1)), ((-1, -1, -1), (-1, -1, 1), (-1, 1, 1), (-1, 1, -1)),
             ((-1, 1, -1), (-1, 1, 1), (1, 1, 1), (1, 1, -1)), ((-1, -1, -1), (1, -1, -1), (1, -1, 1), (-1, -1, 1)),
             ((-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)), ((-1, -1, -1), (-1, 1, -1), (1, 1, -1), (1, -1, -1))]
    longest = grain_axis if grain_axis is not None else max(range(3), key=lambda i: size[i])
    for q in faces:
        pts = [V((c.x + a * h[0], c.y + b * h[1], c.z + d * h[2])) for a, b, d in q]
        n = (pts[1] - pts[0]).cross(pts[3] - pts[0]).normalized()
        axes = [i for i in range(3) if abs(n[i]) < 0.5]
        ua = longest if longest in axes else axes[0]
        va = [i for i in axes if i != ua][0] if len(axes) > 1 else ua
        def uv(p):
            return regUV(region, (p[ua] - (c[ua] - h[ua])) / max(size[ua], 1e-6), (p[va] - (c[va] - h[va])) / max(size[va], 1e-6))
        part.faces.append(([ids[t] for t in q], [uv(p) for p in pts]))

def bar(part, p0, p1, thick, region):
    """square section bar between two points (used for diagonals, rope, struts)"""
    p0 = V(p0); p1 = V(p1); d = (p1 - p0)
    L = d.length; T = d.normalized()
    ref = V((0, 0, 1)) if abs(T.z) < 0.95 else V((1, 0, 0))
    u = ref.cross(T).normalized(); w = T.cross(u).normalized()
    t = thick / 2
    corners = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    r0 = [part.v(p0 + u * a * t + w * b * t) for a, b in corners]
    r1 = [part.v(p1 + u * a * t + w * b * t) for a, b in corners]
    for i in range(4):
        j = (i + 1) % 4
        part.faces.append(([r0[i], r1[i], r1[j], r0[j]], [regUV(region, i / 4.0, 0.0), regUV(region, i / 4.0, 1.0), regUV(region, (i + 1) / 4.0, 1.0), regUV(region, (i + 1) / 4.0, 0.0)]))
    c0 = part.v(p0); c1 = part.v(p1)
    for i in range(4):
        j = (i + 1) % 4
        part.faces.append(([c0, r0[j], r0[i]], [regUV(region, .5, .5)] * 3))
        part.faces.append(([c1, r1[i], r1[j]], [regUV(region, .5, .5)] * 3))

W = 1.0; H = 0.9; POST = 0.06; RAIL = 0.045
parts = []
for m in range(4):
    zc = H / 2 + m * H
    P = Part('Scaff_%d' % (m + 1), (0, 0, zc)); parts.append(P)
    x0 = W / 2 - POST / 2 - 0.01
    # corner posts
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(P, (sx * x0, sy * x0, zc), (POST, POST, H), 'steel')
    # top and bottom rails on the 4 sides
    for zr in (zc - H / 2 + RAIL / 2 + 0.01, zc + H / 2 - RAIL / 2 - 0.01):
        for s in (-1, 1):
            box(P, (0, s * x0, zr), (W - POST, RAIL, RAIL), 'steel')       # along X
            box(P, (s * x0, 0, zr), (RAIL, W - POST, RAIL), 'steel')       # along Y
    # diagonal braces, alternating direction per face
    zb0 = zc - H / 2 + 0.06; zb1 = zc + H / 2 - 0.06
    for k, (a, b) in enumerate(((-1, -1), (1, -1), (1, 1), (-1, 1))):
        d = 1 if (k + m) % 2 == 0 else -1
        if k % 2 == 0:       # faces normal to Y
            bar(P, (-x0 * d, b * x0, zb0), (x0 * d, b * x0, zb1), 0.03, 'steel')
        else:                # faces normal to X
            bar(P, (a * x0, -x0 * d, zb0), (a * x0, x0 * d, zb1), 0.03, 'steel')
    # plank deck on the top of every second module (2 and 4): 5 planks across, slightly uneven
    if m in (1, 3):
        n = 5; pw = (W - 0.02) / n
        for i in range(n):
            yc = -W / 2 + 0.01 + pw * (i + 0.5)
            box(P, (0.0 + (0.012 if i % 2 else -0.012), yc, zc + H / 2 + 0.014), (W + 0.04, pw - 0.012, 0.028), 'wood', grain_axis=0)
        # toe board on the open sides
        for s in (-1, 1):
            box(P, (0, s * (W / 2 + 0.01), zc + H / 2 + 0.07), (W + 0.04, 0.02, 0.1), 'wood', grain_axis=0)

# ---- gallows on the top module: corner mast, beam over the middle of the deck, strut, pulley block, rope
top = parts[3]
zdeck = 4 * H                      # deck surface height (3.6)
mx, my = W / 2 - 0.05, -(W / 2 - 0.05)
mast_h = 1.6
box(top, (mx, my, zdeck + mast_h / 2), (0.075, 0.075, mast_h), 'steel')
beam_z = zdeck + mast_h - 0.04
beam_end = 0.0                      # over the middle of the tower, so the bag hangs in the centre
bx0 = mx - 0.04
box(top, ((bx0 + beam_end) / 2, my, beam_z), (abs(bx0 - beam_end) + 0.1, 0.06, 0.06), 'steel')
bar(top, (mx, my, beam_z - 0.55), (mx - 0.42, my, beam_z - 0.02), 0.035, 'steel')       # strut
box(top, (beam_end - 0.0, my, beam_z - 0.065), (0.1, 0.08, 0.07), 'dark')                  # pulley block under the beam end
ROPE_TOP = beam_z - 0.1; ROPE_LEN = 0.28
bar(top, (beam_end, my, ROPE_TOP), (beam_end, my, ROPE_TOP - ROPE_LEN), 0.026, 'rope')
box(top, (beam_end, my, ROPE_TOP - ROPE_LEN - 0.012), (0.07, 0.07, 0.03), 'dark')           # small ring plate where the bag's knot ties on
print('rope end (knot attach point) in tower space: x=%.3f y=%.3f z=%.3f ; deck z=%.3f ; mast top z=%.3f' % (beam_end, my, ROPE_TOP - ROPE_LEN, zdeck, zdeck + mast_h))

# ============================================================ textures
rng = np.random.default_rng(31337)
def vnoise(h, w, c):
    gh, gw = h // c + 3, w // c + 3
    g = rng.random((gh, gw)); ys = np.linspace(0, gh - 3, h); xs = np.linspace(0, gw - 3, w)
    y0 = ys.astype(int); x0 = xs.astype(int); fy = (ys - y0)[:, None]; fx = (xs - x0)[None, :]
    return (g[y0][:, x0] * (1 - fx) + g[y0][:, x0 + 1] * fx) * (1 - fy) + (g[y0 + 1][:, x0] * (1 - fx) + g[y0 + 1][:, x0 + 1] * fx) * fy
def quant(a): return np.round(np.clip(a, 0, 1) * 31.0) / 31.0

def paint():
    img = np.zeros((128, 128, 3)); img[:] = (0.3, 0.3, 0.3)
    # steel: galvanised grey-blue, scratched, rust streaks
    h, w = 64, 64
    f = (vnoise(h, w, 16) * 0.5 + vnoise(h, w, 8) * 0.3 + vnoise(h, w, 4) * 0.2)[..., None]
    a = np.array((0.50, 0.53, 0.55)) * (0.55 + 0.7 * f)
    rust = np.clip((vnoise(h, w, 6) - 0.58) * 4, 0, 1)[..., None]
    a = a * (1 - rust * 0.6) + np.array((0.45, 0.24, 0.12)) * rust * 0.6 * (0.6 + 0.8 * f)
    a[::8, :, :] *= 0.9
    img[64:128, 0:64] = a
    # wood: planks with horizontal grain (grain along u = x)
    gy = vnoise(h, w, 2)[..., None]; gx = vnoise(h, w, 24)[..., None]
    grain = (0.6 * np.repeat(vnoise(1, w, 16), h, axis=0)[..., None] * 0 + 1)
    stripe = vnoise(h, 2, 3)[:, :1].repeat(w, axis=1)[..., None]
    wd = np.array((0.52, 0.38, 0.24)) * (0.65 + 0.5 * stripe) * (0.85 + 0.3 * gx)
    wd[::16, :, :] *= 0.7                 # plank seams
    wd *= (0.9 + 0.2 * gy)
    img[64:128, 64:128] = wd
    # rope: tan with diagonal twist
    yy, xx = np.mgrid[0:32, 0:64]
    twist = 0.78 + 0.22 * np.sin((xx + yy * 2.0) * 0.9)[..., None]
    img[32:64, 0:64] = np.array((0.62, 0.50, 0.32)) * twist
    # dark metal: nearly black iron
    f2 = vnoise(32, 64, 8)[..., None]
    img[32:64, 64:128] = np.array((0.16, 0.15, 0.15)) * (0.6 + 0.8 * f2)
    return quant(img)

def build_object(P, mat):
    mesh = bpy.data.meshes.new(P.name)
    mesh.from_pydata([tuple(v) for v in P.verts], [], [f[0] for f in P.faces])
    mesh.update()
    uvl = mesh.uv_layers.new(name='UVMap')
    for pi, poly in enumerate(mesh.polygons):
        poly.use_smooth = False
        for li, uv in zip(poly.loop_indices, P.faces[pi][1]): uvl.data[li].uv = uv
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(P.name, mesh)
    obj.location = P.origin
    bpy.context.scene.collection.objects.link(obj)
    return obj

for im in [i for i in bpy.data.images if i.name == 'Scaffold.png']: bpy.data.images.remove(im)
img = bpy.data.images.new('Scaffold.png', 128, 128, alpha=False)
rgba = np.ones((128, 128, 4)); rgba[..., :3] = paint()
img.pixels.foreach_set(rgba.astype(np.float32).ravel())
img.filepath_raw = os.path.join(TEX, 'Scaffold.png'); img.file_format = 'PNG'; img.save(); img.pack()
for m in [m for m in bpy.data.materials if m.name == 'M_Scaffold']: bpy.data.materials.remove(m)
mat = bpy.data.materials.new('M_Scaffold'); mat.use_nodes = True
nt = mat.node_tree; b = nt.nodes.get('Principled BSDF'); t = nt.nodes.new('ShaderNodeTexImage'); t.image = img; t.interpolation = 'Closest'
nt.links.new(t.outputs['Color'], b.inputs['Base Color'])
try: b.inputs['Roughness'].default_value = 1.0
except Exception: pass

objs = [build_object(P, mat) for P in parts]
tris = sum(len(f[0]) - 2 for P in parts for f in P.faces)
print('scaffold built:', len(objs), 'modules,', tris, 'tris')
