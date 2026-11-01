# Background Blender: the player's melee weapon, a rusty steel pipe with a T-fitting head and a taped grip (PS1 style:
# 8-sided, one 128 px atlas, point filtered in Unity). Origin = where the right hand grips, pipe along +Z (head up).
#   "D:/Blender/blender.exe" -b --factory-startup --python AnimationTools/melee/build_pipe.py
# -> Assets/Models/Weapons/Pipe/Pipe.fbx + PipeAtlas.png  (Unity: Tools > Weapons > Build Melee Pipe)
import bpy, bmesh, math, random, os

OUT = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/Assets/Models/Weapons/Pipe/"
SEG = 8
A = 128
random.seed(7)

for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)

# atlas regions (u0, v0, u1, v1) in 0..1
REG = {
    "pipe": (0.0, 0.0, 0.5, 1.0),       # long rusty steel
    "tape": (0.5, 0.5, 1.0, 1.0),       # grip tape
    "iron": (0.5, 0.0, 1.0, 0.5),       # fittings (coupling / tee), bloodied
}


def uvr(reg, u, v):
    u0, v0, u1, v1 = REG[reg]
    return (u0 + (u1 - u0) * u, v0 + (v1 - v0) * v)


def tube(bm, uv, reg, r, z0, z1, axis="z", centre=(0, 0, 0), caps=True, vrep=1.0):
    """open/capped prism of SEG sides along an axis; u wraps around, v runs along the length (vrep times)"""
    def P(a, z):
        c, s = math.cos(a) * r, math.sin(a) * r
        if axis == "z": return (centre[0] + c, centre[1] + s, centre[2] + z)
        return (centre[0] + z, centre[1] + s, centre[2] + c)      # along x
    rings = []
    for z in (z0, z1):
        rings.append([bm.verts.new(P(2 * math.pi * k / SEG + math.pi / SEG, z)) for k in range(SEG)])
    for k in range(SEG):
        k2 = (k + 1) % SEG
        f = bm.faces.new((rings[0][k], rings[0][k2], rings[1][k2], rings[1][k]))
        for loop, (u, v) in zip(f.loops, ((k / SEG, 0), ((k + 1) / SEG, 0), ((k + 1) / SEG, vrep), (k / SEG, vrep))):
            loop[uv].uv = uvr(reg, u, min(v, 1.0) if vrep <= 1 else (v / vrep))
    if caps:
        for ring, flip in ((rings[0], True), (rings[1], False)):
            vs = list(reversed(ring)) if flip else ring
            f = bm.faces.new(vs)
            for loop in f.loops:
                i = ring.index(loop.vert)
                a = 2 * math.pi * i / SEG
                loop[uv].uv = uvr(reg, 0.5 + 0.12 * math.cos(a), 0.5 + 0.12 * math.sin(a))
    return rings


bm = bmesh.new()
uv = bm.loops.layers.uv.new("UVMap")
tube(bm, uv, "pipe", 0.0165, 0.06, 0.60, caps=False)                  # bare pipe (hand at z 0, tape below 0.06)
tube(bm, uv, "tape", 0.0195, -0.13, 0.06)                             # taped grip
tube(bm, uv, "iron", 0.0215, -0.15, -0.13)                            # end cap
tube(bm, uv, "iron", 0.0250, 0.58, 0.66)                              # coupling
tube(bm, uv, "iron", 0.0235, -0.075, 0.075, axis="x", centre=(0, 0, 0.665))   # tee bar (the hammer head)
tube(bm, uv, "iron", 0.0270, -0.095, -0.065, axis="x", centre=(0, 0, 0.665))  # tee collars
tube(bm, uv, "iron", 0.0270, 0.065, 0.095, axis="x", centre=(0, 0, 0.665))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
me = bpy.data.meshes.new("Pipe")
bm.to_mesh(me); bm.free()
for p in me.polygons: p.use_smooth = True
obj = bpy.data.objects.new("Pipe", me)
bpy.context.scene.collection.objects.link(obj)

# ---------------- texture ----------------
px = [[(0.0, 0.0, 0.0, 1.0)] * A for _ in range(A)]


def region_px(reg):
    u0, v0, u1, v1 = REG[reg]
    return int(u0 * A), int(v0 * A), int(u1 * A), int(v1 * A)


def blobs(w, h, n, rmin, rmax):
    m = [[0.0] * w for _ in range(h)]
    for _ in range(n):
        cx, cy, r = random.uniform(0, w), random.uniform(0, h), random.uniform(rmin, rmax)
        for y in range(h):
            for x in range(w):
                dx = min(abs(x - cx), w - abs(x - cx))       # wraps around the pipe
                d = math.hypot(dx, (y - cy) * 0.28) / r      # streaks run along the pipe
                if d < 1: m[y][x] = max(m[y][x], (1 - d) ** 0.7)
    return m


def lerp(a, b, t): return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def paint_metal(reg, base, rust_amt, blood=False):
    x0, y0, x1, y1 = region_px(reg)
    w, h = x1 - x0, y1 - y0
    rust = blobs(w, h, int(9 * rust_amt), 2, 7)
    pits = blobs(w, h, 30, 0.6, 1.6)
    blood_m = blobs(w, h, 7, 2, 7) if blood else None
    for y in range(h):
        for x in range(w):
            n = random.uniform(-0.05, 0.05)
            streak = 0.05 * math.sin(y * 0.9 + x * 0.05)            # drawn-steel lengthwise streaks
            c = tuple(min(1, max(0, v + n + streak * 0.4)) for v in base)
            # a dull highlight band (8 sides -> two columns lighter)
            col = x / w
            if 0.08 < col < 0.2: c = lerp(c, (0.62, 0.62, 0.6), 0.35)
            r = rust[y][x]
            if r > 0: c = lerp(c, lerp((0.34, 0.19, 0.1), (0.19, 0.11, 0.07), random.random()), min(0.85, r * random.uniform(0.6, 1.1)))
            if pits[y][x] > 0.3: c = lerp(c, (0.1, 0.06, 0.04), 0.6)
            if blood_m and blood_m[y][x] > 0.15: c = lerp(c, (0.22, 0.02, 0.02), min(1, blood_m[y][x] * 1.6))
            px[y0 + y][x0 + x] = (c[0], c[1], c[2], 1.0)


def paint_tape():
    x0, y0, x1, y1 = region_px("tape")
    w, h = x1 - x0, y1 - y0
    for y in range(h):
        for x in range(w):
            band = (y * 1.0 + x * 0.35) % 9                       # spiral wraps
            v = 0.13 + (0.05 if band < 1.2 else 0) + random.uniform(-0.02, 0.02)
            if random.random() < 0.012: v = 0.3                    # frayed bits
            px[y0 + y][x0 + x] = (v, v, v * 1.05, 1.0)


paint_metal("pipe", (0.38, 0.38, 0.36), 1.0)
paint_metal("iron", (0.24, 0.23, 0.22), 1.6, blood=True)
paint_tape()

os.makedirs(OUT, exist_ok=True)
im = bpy.data.images.new("PipeAtlas", A, A, alpha=False)
im.pixels = [c for row in px for p in row for c in p]
im.filepath_raw = OUT + "PipeAtlas.png"
im.file_format = 'PNG'
im.save()

mat = bpy.data.materials.new("Pipe")
mat.use_nodes = True
tex = mat.node_tree.nodes.new("ShaderNodeTexImage"); tex.image = im
mat.node_tree.links.new(tex.outputs[0], mat.node_tree.nodes["Principled BSDF"].inputs[0])
me.materials.append(mat)

bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.export_scene.fbx(filepath=OUT + "Pipe.fbx", use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
                         object_types={'MESH'}, mesh_smooth_type='FACE', path_mode='STRIP', bake_anim=False)
print("PIPE_DONE", len(me.vertices), len(me.polygons))
