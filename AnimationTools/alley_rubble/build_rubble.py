# Collapsed-wall rubble pile that blocks the narrow alley beside the park toilet.   Blender: exec(open(this).read())
# Frame (metres): origin = middle of the pile on the ground, X = along the alley (depth), Y = across the alley (walls at about +-1.0),
# Z = up.  Built in its own scene "AlleyRubble" so the other scenes in the .blend are untouched.  One mesh, 5 material slots:
#   0 Debris_Concrete  1 Debris_Plaster  2 Debris_Rust  3 Debris_Wood  4 Debris_Sand
# UV0 = world-axis box projection in metres (the PSXAOLit shader tiles the albedo on UV0), no UV1 needed (AO off).
import bpy, bmesh, math, random, os
from mathutils import Vector, Matrix, Euler

ROOT = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026"
OUT_FBX = os.path.join(ROOT, "Assets", "Models", "AlleyRubble", "AlleyRubble.fbx")
TEX = {
    0: os.path.join(ROOT, "Assets", "Textures", "ParkToilet", "Toilet_ConcretePhoto.png"),
    1: os.path.join(ROOT, "Assets", "Textures", "ParkToilet", "Toilet_PlasterPhoto.png"),
    2: os.path.join(ROOT, "Assets", "Textures", "ParkToilet", "Toilet_Rust.png"),
    3: os.path.join(ROOT, "AnimationTools", "alley_rubble", "Debris_Wood.png"),
    4: os.path.join(ROOT, "Assets", "NEWTextures", "SandTexture.png"),
}
TILE_M = {0: 1.0, 1: 1.0, 2: 1.2, 3: 1.0, 4: 1.0}        # how many metres one texture repeat covers
NAMES = ["Debris_Concrete", "Debris_Plaster", "Debris_Rust", "Debris_Wood", "Debris_Sand"]
CONC, PLAS, RUST, WOOD, SAND = range(5)
rng = random.Random(11)

# ---------- scene ----------
scn = bpy.data.scenes.get("AlleyRubble") or bpy.data.scenes.new("AlleyRubble")
for o in list(scn.objects):
    bpy.data.objects.remove(o, do_unlink=True)
for m in list(bpy.data.meshes):
    if m.name.startswith("AlleyRubble"):
        bpy.data.meshes.remove(m)

bm = bmesh.new()


def V(p):
    return bm.verts.new(p)


def face(vs, mat):
    try:
        f = bm.faces.new(vs)
        f.material_index = mat
        return f
    except ValueError:
        return None


def xform(points, center, euler):
    M = Euler([math.radians(a) for a in euler], "XYZ").to_matrix()
    return [M @ Vector(p) + Vector(center) for p in points]


CUBE = [(-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1), (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)]
CUBE_FACES = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (2, 3, 7, 6), (1, 2, 6, 5), (3, 0, 4, 7)]


def lump(center, size, euler, jitter=0.18, mats=None, sink=0.0):
    """Irregular broken chunk: a box whose corners are pushed around."""
    sx, sy, sz = size
    pts = []
    for c in CUBE:
        p = (c[0] * sx / 2 + rng.uniform(-jitter, jitter) * sx, c[1] * sy / 2 + rng.uniform(-jitter, jitter) * sy, c[2] * sz / 2 + rng.uniform(-jitter, jitter) * sz)
        pts.append(p)
    ws = xform(pts, center, euler)
    # keep the bottom from poking below the ground
    ws = [Vector((w.x, w.y, max(w.z, -sink))) for w in ws]
    vs = [V(w) for w in ws]
    for i, idx in enumerate(CUBE_FACES):
        m = mats[i] if isinstance(mats, (list, tuple)) else (mats if mats is not None else CONC)
        face([vs[j] for j in idx], m)


def slab(center, L, H, T, euler, seg=7, jag=0.10, front=PLAS, back=CONC, edge=CONC, tilt_top=0.0):
    """Broken wall slab. Local axes: X thickness, Y length, Z height. Jagged top edge."""
    cols = []
    for i in range(seg + 1):
        y = -L / 2 + L * i / seg
        top = H / 2 - (rng.uniform(0, jag) * (1 if 0 < i < seg else 0.4)) + tilt_top * (i / seg - 0.5)
        cols.append((y, top))
    pts_front, pts_back = [], []
    for y, top in cols:
        pts_front.append(((T / 2, y, -H / 2), (T / 2, y, top)))
        pts_back.append(((-T / 2, y, -H / 2), (-T / 2, y, top)))
    allp = [p for pair in pts_front + pts_back for p in pair]
    ws = xform(allp, center, euler)
    vs = [V(w) for w in ws]
    nf = len(pts_front)
    F = lambda i, k: vs[i * 2 + k]
    B = lambda i, k: vs[(nf + i) * 2 + k]
    for i in range(seg):
        face([F(i, 0), F(i + 1, 0), F(i + 1, 1), F(i, 1)], front)             # +X face (plaster)
        face([B(i + 1, 0), B(i, 0), B(i, 1), B(i + 1, 1)], back)              # -X face
        face([F(i, 1), F(i + 1, 1), B(i + 1, 1), B(i, 1)], edge)              # broken top
        face([F(i + 1, 0), F(i, 0), B(i, 0), B(i + 1, 0)], edge)              # bottom
    face([F(0, 0), F(0, 1), B(0, 1), B(0, 0)], edge)
    face([F(seg, 1), F(seg, 0), B(seg, 0), B(seg, 1)], edge)


def rod(p0, p1, r, seg=6, mat=RUST):
    a, b = Vector(p0), Vector(p1)
    d = (b - a)
    L = d.length
    q = d.normalized().to_track_quat("Z", "Y")
    M = q.to_matrix()
    ring0, ring1 = [], []
    for i in range(seg):
        ang = 2 * math.pi * i / seg
        loc = M @ Vector((r * math.cos(ang), r * math.sin(ang), 0))
        ring0.append(V(a + loc))
        ring1.append(V(b + loc))
    for i in range(seg):
        j = (i + 1) % seg
        face([ring0[i], ring0[j], ring1[j], ring1[i]], mat)
    face(ring1, mat)


def plank(p0, p1, width, thick, mat=WOOD, broken_end=True, twist=0.0):
    a, b = Vector(p0), Vector(p1)
    d = (b - a)
    z = d.normalized()
    up = Vector((0, 0, 1))
    side = z.cross(up)
    if side.length < 1e-4:
        side = Vector((0, 1, 0))
    side.normalize()
    nrm = z.cross(side).normalized()
    corners = []
    for end, pt in ((0, a), (1, b)):
        jit = (rng.uniform(-0.05, 0.05) if (broken_end and end == 1) else 0.0)
        for sx, sn in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            off = side * sx * width / 2 + nrm * sn * thick / 2
            extra = z * (jit + (rng.uniform(0, 0.12) if (broken_end and end == 1 and sx > 0) else 0.0))
            corners.append(V(pt + off + extra))
    q0, q1 = corners[:4], corners[4:]
    for i in range(4):
        j = (i + 1) % 4
        face([q0[i], q0[j], q1[j], q1[i]], mat)
    face(q0[::-1], mat)
    face(q1, mat)


def corrugated(center, W, Hh, euler, ribs=14, rib_amp=0.022, bend=0.25, mat=RUST, nv=7):
    pts = []
    for iy in range(nv + 1):
        for ix in range(ribs * 2 + 1):
            u = ix / (ribs * 2)
            v = iy / nv
            x = (u - 0.5) * W
            z = (v - 0.5) * Hh
            y = rib_amp * math.sin(u * ribs * 2 * math.pi) + bend * (v - 0.5) ** 2 * 2 + rng.uniform(-0.006, 0.006)
            # ragged bottom/side edge
            if iy == 0:
                z += rng.uniform(0, 0.08)
            pts.append((x, y, z))
    ws = xform(pts, center, euler)
    vs = [V(w) for w in ws]
    nx = ribs * 2 + 1
    for iy in range(nv):
        for ix in range(nx - 1):
            a, b, c, d = vs[iy * nx + ix], vs[iy * nx + ix + 1], vs[(iy + 1) * nx + ix + 1], vs[(iy + 1) * nx + ix]
            face([a, b, c, d], mat)
            face([d, c, b, a], mat)          # thin sheet, visible from both sides


def mound(nx=26, ny=18, ext=(1.3, 1.15), height=0.34):
    vs = []
    for iy in range(ny + 1):
        row = []
        for ix in range(nx + 1):
            x = -ext[0] + 2 * ext[0] * ix / nx
            y = -ext[1] + 2 * ext[1] * iy / ny
            ux = max(0.0, 1 - (x / ext[0]) ** 2)
            uy = max(0.0, 1 - abs(y / ext[1]) ** 4)
            h = height * (ux ** 1.4) * (0.55 + 0.45 * uy) + rng.uniform(-0.012, 0.012) * (ux > 0.05)
            # a tail of sand drifting out on the -X (road) side
            if x < -0.5:
                h += 0.05 * (1 - (x + ext[0]) / (ext[0] - 0.5)) * 0
            row.append(V((x, y, max(h, 0.0) + 0.002)))
        vs.append(row)
    for iy in range(ny):
        for ix in range(nx):
            face([vs[iy][ix], vs[iy][ix + 1], vs[iy + 1][ix + 1], vs[iy + 1][ix]], SAND)


# ---------- the pile ----------
mound()

# cracked wall slabs, none of them flat-on: they lean on each other and on the walls (ends buried in the walls)
slab(center=(-0.42, -0.58, 0.46), L=1.25, H=1.00, T=0.15, euler=(7, -40, 5), seg=6, jag=0.20, front=PLAS, back=CONC, edge=CONC)
slab(center=(-0.30, 0.56, 0.40), L=1.20, H=0.85, T=0.14, euler=(-9, -50, -8), seg=6, jag=0.18, front=PLAS, back=PLAS, edge=CONC)
slab(center=(0.12, -0.05, 1.00), L=1.35, H=0.70, T=0.13, euler=(16, -22, 4), seg=6, jag=0.16, front=PLAS, back=CONC, edge=CONC)   # bridges the two, resting on top
slab(center=(0.42, 0.80, 1.00), L=0.75, H=0.60, T=0.12, euler=(32, -10, 0), seg=4, jag=0.12, front=CONC, back=PLAS, edge=CONC)
slab(center=(-0.90, -0.30, 0.13), L=0.95, H=0.85, T=0.12, euler=(0, 83, 12), seg=4, jag=0.08, front=CONC, back=CONC, edge=CONC)  # flat piece on the ground, the "step" up

# pyramid of concrete chunks behind (and spilling in front of) the slab
rows = [
    (0.20, 0.55, [-0.88, -0.50, -0.12, 0.26, 0.60, 0.92], (0.42, 0.66)),
    (0.55, 0.32, [-0.66, -0.22, 0.24, 0.68], (0.38, 0.56)),
    (0.84, 0.16, [-0.44, 0.05, 0.50], (0.32, 0.46)),
    (1.05, 0.10, [-0.12, 0.30], (0.24, 0.32)),
]
for z, x0, ys, (smin, smax) in rows:
    for y in ys:
        s = rng.uniform(smin, smax)
        lump((x0 + rng.uniform(-0.12, 0.12), y + rng.uniform(-0.08, 0.08), z), (s * rng.uniform(0.9, 1.3), s * rng.uniform(0.9, 1.2), s * rng.uniform(0.7, 1.0)),
             (rng.uniform(-18, 18), rng.uniform(-18, 18), rng.uniform(0, 180)), jitter=0.2, mats=CONC if rng.random() < 0.7 else [PLAS] * 6, sink=0.02)
for i in range(7):
    s = rng.uniform(0.18, 0.34)
    lump((rng.uniform(-1.15, -0.65), rng.uniform(-0.95, 0.95), s * 0.35), (s * 1.2, s, s * 0.7), (rng.uniform(-12, 12), rng.uniform(-12, 12), rng.uniform(0, 180)), jitter=0.22, mats=CONC, sink=0.01)

# cinder blocks (hollow ones, lying about)
for (x, y, z, yaw) in [(0.20, -0.30, 0.72, 20), (0.34, 0.50, 0.50, -35), (-0.75, 0.40, 0.11, 70), (0.70, -0.62, 0.36, 5), (-0.95, -0.80, 0.10, 115), (0.05, 0.05, 0.95, 40), (0.60, 0.20, 0.82, -15)]:
    lump((x, y, z), (0.40, 0.20, 0.20), (rng.uniform(-8, 8), rng.uniform(-8, 8), yaw), jitter=0.04, mats=CONC, sink=0.0)
    # dark hollow cores on the visible ends
    # (kept simple: two small recessed quads)

# a few short bars of rebar sticking out of the broken edges, bent over
for (p0, ang) in [((-0.18, -0.20, 1.30), (30, 10)), ((0.25, 0.30, 1.32), (-25, 25)), ((0.70, -0.55, 0.92), (35, -15)),
                  ((-0.35, 0.80, 0.78), (10, 35))]:
    L = rng.uniform(0.28, 0.45)
    a, b = math.radians(ang[0]), math.radians(ang[1])
    d = Vector((math.sin(a) * 0.7, math.sin(b) * 0.7, 1.0)).normalized()
    p1 = Vector(p0) + d * L
    rod(p0, p1, 0.017, 6, RUST)
    bend = Vector((rng.uniform(-0.6, 0.6), rng.uniform(-0.6, 0.6), -0.25)).normalized()
    rod(p1, p1 + bend * rng.uniform(0.12, 0.22), 0.017, 6, RUST)

# planks wedged through the pile
plank((-0.25, -1.05, 1.58), (0.30, 0.45, 0.62), 0.16, 0.045, WOOD)
plank((0.05, 1.00, 1.38), (-0.25, -0.05, 0.86), 0.14, 0.04, WOOD)
plank((0.45, -0.55, 0.90), (0.15, 0.35, 1.30), 0.10, 0.04, WOOD)
plank((-0.55, 0.30, 0.45), (-0.95, 0.85, 0.22), 0.14, 0.04, WOOD)

# bent sheet of corrugated metal leaning on the right wall
corrugated(center=(-0.05, 0.62, 0.95), W=0.95, Hh=1.05, euler=(8, -18, 82), ribs=9, rib_amp=0.02, bend=0.22, mat=RUST)

# ---------- UVs: world-axis box projection in metres ----------
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
uvl = bm.loops.layers.uv.new("UVMap")
for f in bm.faces:
    n = f.normal
    ax = max(range(3), key=lambda i: abs(n[i]))
    a, b = [(1, 2), (0, 2), (0, 1)][ax]
    t = TILE_M[f.material_index]
    for l in f.loops:
        co = l.vert.co
        l[uvl].uv = (co[a] / t, co[b] / t)

mesh = bpy.data.meshes.new("AlleyRubble")
bm.to_mesh(mesh)
bm.free()
for p in mesh.polygons:
    p.use_smooth = False
obj = bpy.data.objects.new("AlleyRubble", mesh)
scn.collection.objects.link(obj)

# ---------- materials (textured so the Blender preview matches; Unity gets its own PSXAOLit materials) ----------
for i, name in enumerate(NAMES):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = 1.0
    if os.path.exists(TEX[i]):
        tex = nt.nodes.get("Tex") or nt.nodes.new("ShaderNodeTexImage")
        tex.name = "Tex"
        tex.image = bpy.data.images.load(TEX[i], check_existing=True)
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    mesh.materials.append(m)

# ---------- report ----------
xs = [v.co.x for v in mesh.vertices]; ys = [v.co.y for v in mesh.vertices]; zs = [v.co.z for v in mesh.vertices]
result = {"verts": len(mesh.vertices), "tris": sum(len(p.vertices) - 2 for p in mesh.polygons),
          "bounds": {"x": [round(min(xs), 2), round(max(xs), 2)], "y": [round(min(ys), 2), round(max(ys), 2)], "z": [round(min(zs), 2), round(max(zs), 2)]}}
