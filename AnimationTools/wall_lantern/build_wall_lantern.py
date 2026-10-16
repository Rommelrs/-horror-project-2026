# Wall mounted PSX lantern (Blender 5.x).  exec(open(this).read())  -> builds object 'WallLantern', textures, exports FBX into Assets/Models/WallLantern
# Layout: wall plane is Y=0, the lantern projects towards -Y (Blender front).  FBX default axes => Unity +Z points away from the wall.
# Origin = centre of the back plate on the wall surface.
import bpy, math, os
import numpy as np
from mathutils import Vector

V = Vector
ROOT = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026"
OUT = os.path.join(ROOT, "Assets", "Models", "WallLantern")
TEX = os.path.join(ROOT, "AnimationTools", "wall_lantern", "tex")
os.makedirs(OUT, exist_ok=True); os.makedirs(TEX, exist_ok=True)

for o in [o for o in bpy.data.objects if o.name == 'WallLantern']:
    bpy.data.objects.remove(o, do_unlink=True)

verts = []; faces = []   # faces: (idx tuple, mat, uvs)
def vtx(p): verts.append(V(p)); return len(verts) - 1
def face(idx, mat, uvs): faces.append((tuple(idx), mat, uvs))

def frame(T):
    T = T.normalized(); ref = V((0, -1, 0))
    if abs(T.dot(ref)) > 0.95: ref = V((0, 0, 1))
    u = ref.cross(T).normalized(); v = T.cross(u).normalized()
    return u, v

def loft(centers, radii, n, mat, uv, phase=0.0, cap0=False, cap1=False, tangents=None):
    """rings along centers; radii (rx, ry) per ring; faces wound outward (same convention as the Questioner build)"""
    R = len(centers); rings = []
    for i in range(R):
        if tangents: T = tangents[i]
        else:
            a = centers[max(i - 1, 0)]; b = centers[min(i + 1, R - 1)]; T = b - a
        u, v = frame(T); rx, ry = radii[i]
        rings.append([vtx(centers[i] + u * rx * math.sin(phase + 2 * math.pi * k / n) + v * ry * math.cos(phase + 2 * math.pi * k / n)) for k in range(n)])
    for i in range(R - 1):
        for j in range(n):
            j2 = (j + 1) % n
            idx = (rings[i][j], rings[i + 1][j], rings[i + 1][j2], rings[i][j2])
            face(idx, mat, [uv(i, j), uv(i + 1, j), uv(i + 1, j + 1), uv(i, j + 1)])
    if cap0:
        c = vtx(centers[0])
        for j in range(n):
            face((c, rings[0][j], rings[0][(j + 1) % n]), mat, [uv(0, j + .5), uv(0, j), uv(0, j + 1)])
    if cap1:
        c = vtx(centers[-1])
        for j in range(n):
            face((c, rings[-1][(j + 1) % n], rings[-1][j]), mat, [uv(R - 1, j + .5), uv(R - 1, j + 1), uv(R - 1, j)])
    return rings

def box(c, s, mat=0):
    """axis aligned box, c centre, s full size; UV = planar on the dominant axis, in 0..1 of the metal texture"""
    hx, hy, hz = s[0] / 2, s[1] / 2, s[2] / 2
    P = [V((c[0] + sx * hx, c[1] + sy * hy, c[2] + sz * hz)) for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]
    ids = [vtx(p) for p in P]
    def vi(sx, sy, sz): return ids[((sx > 0) * 4) + ((sy > 0) * 2) + (sz > 0)]
    quads = [  # outward wound
        ((1, -1, -1), (1, 1, -1), (1, 1, 1), (1, -1, 1)), ((-1, -1, -1), (-1, -1, 1), (-1, 1, 1), (-1, 1, -1)),
        ((-1, 1, -1), (-1, 1, 1), (1, 1, 1), (1, 1, -1)), ((-1, -1, -1), (1, -1, -1), (1, -1, 1), (-1, -1, 1)),
        ((-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)), ((-1, -1, -1), (-1, 1, -1), (1, 1, -1), (1, -1, -1)),
    ]
    for q in quads:
        pts = [V((c[0] + a * hx, c[1] + b * hy, c[2] + d * hz)) for a, b, d in q]
        face([vi(*t) for t in q], mat, [uvbox(p, c, s) for p in pts])

def uvbox(p, c, s):
    return ((p[0] - c[0]) / (s[0] + 1e-6) % 1.0 * 0.9 + 0.05, (p[2] - c[2]) / (s[2] + 1e-6) % 1.0 * 0.9 + 0.05)

# metal UV: wrap u around, v along the ring list
def metal_uv(R, n):
    return lambda i, j: ((j / n) * 0.5 + 0.1, 0.1 + 0.8 * i / max(R - 1, 1))

LY = -0.26             # lantern centre line (distance from the wall)
# ---- wall plate + 2 screws
box((0, -0.011, 0), (0.15, 0.022, 0.34))
for z in (0.13, -0.13):
    loft([V((0, -0.022, z)), V((0, -0.032, z))], [(0.014, 0.014)] * 2, 6, 0, lambda i, j: (0.8, 0.8), cap1=True, tangents=[V((0, -1, 0))] * 2)
# ---- arm (horizontal), downward hook and diagonal brace
box((0, (-0.022 + LY) / 2, 0.13), (0.03, abs(LY + 0.022) + 0.03, 0.03))
box((0, LY, 0.105), (0.02, 0.02, 0.05))
brace = [V((0, -0.022, -0.07)), V((0, LY + 0.04, 0.115))]
loft(brace, [(0.011, 0.011)] * 2, 4, 0, metal_uv(2, 4), phase=math.pi / 4, cap0=True, cap1=True, tangents=[(brace[1] - brace[0]).normalized()] * 2)
# ---- lantern: roof (hex)
roof_z = [0.082, 0.072, 0.050, 0.030]
roof_r = [0.010, 0.030, 0.108, 0.093]
loft([V((0, LY, z)) for z in roof_z], [(r, r) for r in roof_r], 6, 0, metal_uv(4, 6), tangents=[V((0, 0, -1))] * 4, cap0=True)
# roof underside lip
loft([V((0, LY, 0.030)), V((0, LY, 0.026))], [(0.093, 0.093), (0.080, 0.080)], 6, 0, metal_uv(2, 6), tangents=[V((0, 0, -1))] * 2)
# ---- glass chamber (emissive)
GZ = [0.026, -0.190]
GR = [0.074, 0.067]
gt = [vtx(V((math.sin(2 * math.pi * k / 6) * GR[0], LY + math.cos(2 * math.pi * k / 6) * GR[0], GZ[0]))) for k in range(6)]
gb = [vtx(V((math.sin(2 * math.pi * k / 6) * GR[1], LY + math.cos(2 * math.pi * k / 6) * GR[1], GZ[1]))) for k in range(6)]
for k in range(6):       # one full texture per glass pane (outward wound like loft)
    k2 = (k + 1) % 6
    face((gt[k], gb[k], gb[k2], gt[k2]), 1, [(0.0, 0.97), (0.0, 0.22), (1.0, 0.22), (1.0, 0.97)])
# ---- frame posts at the 6 corners (thin 4 sided bars)
for k in range(6):
    a = 2 * math.pi * k / 6
    top = V((math.sin(a) * 0.080, LY + math.cos(a) * 0.080, 0.030)); bot = V((math.sin(a) * 0.073, LY + math.cos(a) * 0.073, -0.196))
    loft([top, bot], [(0.0065, 0.0065)] * 2, 4, 0, lambda i, j: (0.85, 0.15 + 0.7 * i), phase=math.pi / 4, cap0=True, cap1=True, tangents=[(bot - top).normalized()] * 2)
# ---- base
BZ = [-0.190, -0.200, -0.228, -0.246]
BR = [0.083, 0.088, 0.060, 0.022]
loft([V((0, LY, z)) for z in BZ], [(r, r) for r in BR], 6, 0, metal_uv(4, 6), tangents=[V((0, 0, -1))] * 4, cap1=True)
# ---- flame (bright, emissive material, uv on the hot strip of the glow texture)
FZ = [-0.150, -0.112, -0.070, -0.030]
FR = [0.0, 0.020, 0.014, 0.0]
loft([V((0, LY, z)) for z in FZ], [(r, r) for r in FR], 6, 1, lambda i, j: (0.5, 0.05), tangents=[V((0, 0, -1))] * 4)
# small oil pot under the flame (metal)
loft([V((0, LY, -0.185)), V((0, LY, -0.158))], [(0.030, 0.030), (0.022, 0.022)], 6, 0, metal_uv(2, 6), tangents=[V((0, 0, 1))] * 2, cap1=True)

# ============================================================ mesh object
mesh = bpy.data.meshes.new('WallLantern')
mesh.from_pydata([tuple(v) for v in verts], [], [f[0] for f in faces])
mesh.update()
uvl = mesh.uv_layers.new(name='UVMap')
for pi, poly in enumerate(mesh.polygons):
    poly.use_smooth = False
    for li, uv in zip(poly.loop_indices, faces[pi][2]):
        uvl.data[li].uv = uv
obj = bpy.data.objects.new('WallLantern', mesh)
bpy.context.scene.collection.objects.link(obj)

# ============================================================ textures
rng = np.random.default_rng(777)
def vnoise(h, w, c):
    gh, gw = h // c + 3, w // c + 3
    g = rng.random((gh, gw)); ys = np.linspace(0, gh - 3, h); xs = np.linspace(0, gw - 3, w)
    y0 = ys.astype(int); x0 = xs.astype(int); fy = (ys - y0)[:, None]; fx = (xs - x0)[None, :]
    return (g[y0][:, x0] * (1 - fx) + g[y0][:, x0 + 1] * fx) * (1 - fy) + (g[y0 + 1][:, x0] * (1 - fx) + g[y0 + 1][:, x0 + 1] * fx) * fy
def quant(a): return np.round(np.clip(a, 0, 1) * 31.0) / 31.0

def metal_tex():
    S = 64
    f = (vnoise(S, S, 16) * 0.5 + vnoise(S, S, 8) * 0.3 + vnoise(S, S, 4) * 0.2)[..., None]
    a = np.array((0.20, 0.17, 0.15)) * (0.55 + 0.9 * f)
    rust = np.clip((vnoise(S, S, 8) - 0.55) * 4, 0, 1)[..., None]
    a = a * (1 - rust * 0.5) + np.array((0.36, 0.19, 0.10)) * rust * 0.5 * (0.6 + 0.8 * f)
    a[:, ::16] *= 0.8
    return quant(a)

def glow_tex():
    W, H = 32, 32
    yy, xx = np.mgrid[0:H, 0:W]
    v = yy / (H - 1.0); u = xx / (W - 1.0)
    # glass: bright amber in the middle, darker at the top / bottom rims and at the edges
    mid = np.clip(1 - np.abs(v - 0.55) / 0.55, 0, 1)
    side = np.clip(1 - np.abs(u - 0.5) / 0.5, 0, 1)
    k = (0.35 + 0.65 * mid) * (0.65 + 0.35 * side)
    a = np.stack([np.clip(1.0 * k + 0.05, 0, 1), np.clip(0.52 * k + 0.02, 0, 1), np.clip(0.10 * k, 0, 1)], -1)
    soot = np.clip((vnoise(H, W, 8) - 0.5) * 2.0, 0, 1)[..., None] * np.clip((v - 0.5) * 2, 0, 1)[..., None]
    a = a * (1 - soot * 0.45)
    a[:, 0:2] *= 0.35; a[:, -2:] *= 0.35          # dark frame edges
    a[0:6, :] = np.array((1.0, 0.86, 0.35))       # flame strip (bottom rows)
    return quant(a)

def save_img(name, arr):
    h, w = arr.shape[:2]
    for im in [i for i in bpy.data.images if i.name == name]: bpy.data.images.remove(im)
    img = bpy.data.images.new(name, w, h, alpha=False)
    rgba = np.ones((h, w, 4)); rgba[..., :3] = arr
    img.pixels.foreach_set(rgba.astype(np.float32).ravel())
    img.filepath_raw = os.path.join(TEX, name); img.file_format = 'PNG'; img.save(); img.pack()
    return img

def make_mat(name, img, emit=False):
    for m in [m for m in bpy.data.materials if m.name == name]: bpy.data.materials.remove(m)
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes.get('Principled BSDF')
    t = nt.nodes.new('ShaderNodeTexImage'); t.image = img; t.interpolation = 'Closest'
    nt.links.new(t.outputs['Color'], b.inputs['Base Color'])
    if emit:
        nt.links.new(t.outputs['Color'], b.inputs['Emission Color']); b.inputs['Emission Strength'].default_value = 1.0
    try: b.inputs['Roughness'].default_value = 1.0
    except Exception: pass
    return m

m_img = save_img('WallLantern_Metal.png', metal_tex())
g_img = save_img('WallLantern_Glow.png', glow_tex())
mesh.materials.append(make_mat('M_WallLantern_Metal', m_img))
mesh.materials.append(make_mat('M_WallLantern_Glow', g_img, emit=True))
for pi, poly in enumerate(mesh.polygons): poly.material_index = faces[pi][1]

print('wall lantern: verts', len(verts), 'tris', sum(len(f[0]) - 2 for f in faces), 'dims', [round(x, 3) for x in obj.dimensions])
