# Globe lamp (spherical bulb on a small round base, like the white globes on gate pillars).  Blender 5.x
# exec(open(this).read()) -> builds object 'GlobeLamp', textures, exports FBX + PNGs into Assets/Models/GlobeLamp
# Origin = centre of the underside of the base, Z up (it sits on top of a wall).  ~170 tris, flat shaded, 2 materials (base, glow).
import bpy, math, os
import numpy as np
from mathutils import Vector

V = Vector
ROOT = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026"
OUT = os.path.join(ROOT, "Assets", "Models", "GlobeLamp")
TEX = os.path.join(ROOT, "AnimationTools", "globe_lamp", "tex")
os.makedirs(OUT, exist_ok=True); os.makedirs(TEX, exist_ok=True)

for o in [o for o in bpy.data.objects if o.name == 'GlobeLamp']:
    bpy.data.objects.remove(o, do_unlink=True)

verts = []; faces = []
def vtx(p): verts.append(V(p)); return len(verts) - 1
def face(idx, mat, uvs): faces.append((tuple(idx), mat, uvs))

def ring_stack(rings, n, mat, uv_of, cap_top=True, cap_bottom=True, bottom_z=None, top_z=None):
    """rings: list of (z, radius) from bottom to top. Outward-wound quads between rings, optional fan caps
    (the cap centres sit at the first / last ring height unless bottom_z / top_z say otherwise, e.g. the poles of a sphere)."""
    ids = []
    for z, r in rings:
        ids.append([vtx(V((r * math.cos(2 * math.pi * k / n), r * math.sin(2 * math.pi * k / n), z))) for k in range(n)])
    R = len(rings)
    for i in range(R - 1):
        for j in range(n):
            j2 = (j + 1) % n
            # counter-clockwise seen from outside when walking up
            idx = (ids[i][j], ids[i][j2], ids[i + 1][j2], ids[i + 1][j])
            face(idx, mat, [uv_of(i, j), uv_of(i, j + 1), uv_of(i + 1, j + 1), uv_of(i + 1, j)])
    if cap_bottom:
        c = vtx(V((0, 0, rings[0][0] if bottom_z is None else bottom_z)))
        for j in range(n):
            j2 = (j + 1) % n
            face((c, ids[0][j2], ids[0][j]), mat, [uv_of(0, j + .5), uv_of(0, j + 1), uv_of(0, j)])
    if cap_top:
        c = vtx(V((0, 0, rings[-1][0] if top_z is None else top_z)))
        for j in range(n):
            j2 = (j + 1) % n
            face((c, ids[-1][j], ids[-1][j2]), mat, [uv_of(R - 1, j + .5), uv_of(R - 1, j), uv_of(R - 1, j + 1)])
    return ids

N = 8
# ---- base: wide plate, a step, a short neck (iron). UV: planar strips on the 32x32 base texture
def base_uv(i, j): return ((j % N) / N * 0.9 + 0.05, 0.1 + 0.8 * (i % 3) / 2.0)
ring_stack([(0.000, 0.135), (0.030, 0.135), (0.030, 0.105), (0.060, 0.105), (0.060, 0.070), (0.082, 0.070)], N, 0, base_uv, cap_top=True, cap_bottom=True)

# ---- the globe: low poly sphere (10 around, 6 bands), frosted white (emissive material)
GN = 10; GR = 0.150; GC = 0.082 + 0.115          # radius, centre height (sphere dips into the neck)
bands = 6
rings = []
for b in range(1, bands):                         # skip the poles (replaced by cap fans)
    a = math.pi * b / bands                      # polar angle from the bottom
    rings.append((GC - GR * math.cos(a), GR * math.sin(a)))
ring_stack(rings, GN, 1, lambda i, j: ((j / GN), 0.06 + 0.88 * ((i + 1) / bands)), cap_top=True, cap_bottom=True,
           bottom_z=GC - GR, top_z=GC + GR)

# ============================================================ mesh object
mesh = bpy.data.meshes.new('GlobeLamp')
mesh.from_pydata([tuple(v) for v in verts], [], [f[0] for f in faces])
mesh.update()
uvl = mesh.uv_layers.new(name='UVMap')
for pi, poly in enumerate(mesh.polygons):
    poly.use_smooth = False
    for li, uv in zip(poly.loop_indices, faces[pi][2]):
        uvl.data[li].uv = uv
obj = bpy.data.objects.new('GlobeLamp', mesh)
bpy.context.scene.collection.objects.link(obj)

# ============================================================ textures (32x32, 15-bit colour, like the other PSX props)
rng = np.random.default_rng(4242)
def vnoise(h, w, c):
    gh, gw = h // c + 3, w // c + 3
    g = rng.random((gh, gw)); ys = np.linspace(0, gh - 3, h); xs = np.linspace(0, gw - 3, w)
    y0 = ys.astype(int); x0 = xs.astype(int); fy = (ys - y0)[:, None]; fx = (xs - x0)[None, :]
    return (g[y0][:, x0] * (1 - fx) + g[y0][:, x0 + 1] * fx) * (1 - fy) + (g[y0 + 1][:, x0] * (1 - fx) + g[y0 + 1][:, x0 + 1] * fx) * fy
def quant(a): return np.round(np.clip(a, 0, 1) * 31.0) / 31.0

def base_tex():
    S = 32
    f = (vnoise(S, S, 8) * 0.6 + vnoise(S, S, 4) * 0.4)[..., None]
    a = np.array((0.30, 0.28, 0.26)) * (0.55 + 0.9 * f)
    rust = np.clip((vnoise(S, S, 4) - 0.6) * 4, 0, 1)[..., None]
    a = a * (1 - rust * 0.5) + np.array((0.38, 0.22, 0.12)) * rust * 0.5
    return quant(a)

def glow_tex():
    W, H = 32, 32
    yy, xx = np.mgrid[0:H, 0:W]
    v = yy / (H - 1.0)
    # frosted white globe: nearly full bright, a touch dimmer towards the poles, tiny dust speckle
    k = 0.80 + 0.20 * np.clip(1 - np.abs(v - 0.5) / 0.5, 0, 1)
    a = np.stack([k * 1.0, k * 0.97, k * 0.90], -1)
    a *= (0.96 + 0.07 * vnoise(H, W, 4))[..., None]
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

b_img = save_img('GlobeLamp_Base.png', base_tex())
g_img = save_img('GlobeLamp_Glow.png', glow_tex())
mesh.materials.append(make_mat('M_GlobeLamp_Base', b_img))
mesh.materials.append(make_mat('M_GlobeLamp_Glow', g_img, emit=True))
for pi, poly in enumerate(mesh.polygons): poly.material_index = faces[pi][1]

print('globe lamp: verts', len(verts), 'tris', sum(len(f[0]) - 2 for f in faces), 'dims', [round(x, 3) for x in obj.dimensions])
