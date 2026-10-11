# Builds the park public toilet in Blender (run inside Blender with exec(open(path).read())).
# Blender axes: front of the building faces -Y, +X is the viewer's right, Z is up, origin on the ground in the middle of the footprint.
# (The FBX export turns that into Unity: front +Z, men's door on local +X, Y up -- the same layout as the old primitive prefab.)
# Parts: footing + steps and roof slab with roughly chipped edges, bevelled walls with real recessed doors and vent openings, real diamond
# lattice vent blocks, doors with louvres/handles, signs with bolts, caged lamp, water tank, roof vent, electrical box + conduit.
# The dripping pipe stays a separate Unity prefab. Afterwards: UV0 = metric box projection (same texel density everywhere) or 0..1 for
# doors/signs, UV1 = packed unwrap for a baked ambient-occlusion texture (Cycles), then export FBX.
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix, noise

PROJ = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/"
TEX = PROJ + "Assets/Textures/ParkToilet/"
random.seed(11)

# ------------------------------------------------------------------ clean slate
for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
    for d in list(coll): coll.remove(d)

# name: (texture file, tiles per metre, fallback colour)
MATS = {
    "ParkToilet_Plaster":   ("Toilet_Plaster.png", 0.4, (0.78, 0.70, 0.58)),
    "ParkToilet_Concrete":  ("Toilet_Concrete.png", 0.5, (0.72, 0.67, 0.60)),
    "ParkToilet_Base":      ("Toilet_Concrete.png", 0.5, (0.54, 0.50, 0.44)),
    "ParkToilet_Tank":      ("Toilet_Concrete.png", 0.8, (0.70, 0.66, 0.60)),
    "ParkToilet_Door":      ("Toilet_Door.png", None, (0.39, 0.42, 0.40)),
    "ParkToilet_Metal":     ("Toilet_Metal.png", 1.2, (0.39, 0.42, 0.40)),
    "ParkToilet_Rust":      ("Toilet_Rust.png", 2.0, (0.40, 0.33, 0.25)),
    "ParkToilet_RustDark":  ("Toilet_Rust.png", 2.0, (0.20, 0.17, 0.14)),
    "ParkToilet_Pipe":      ("Toilet_Pipe.png", 2.0, (0.42, 0.42, 0.40)),
    "ParkToilet_SignMen":   ("Toilet_SignMen.png", None, (0.8, 0.8, 0.8)),
    "ParkToilet_SignWomen": ("Toilet_SignWomen.png", None, (0.8, 0.8, 0.8)),
    "ParkToilet_Lamp":      (None, None, (0.85, 0.78, 0.55)),
    "ParkToilet_VentDark":  (None, None, (0.02, 0.02, 0.02)),
}
mats = {}
for name, (tex, tpm, col) in MATS.items():
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*col, 1)
    bsdf.inputs["Roughness"].default_value = 0.9
    if tex:
        img = bpy.data.images.load(TEX + tex); img.name = tex
        n = nt.nodes.new("ShaderNodeTexImage"); n.image = img; n.interpolation = 'Linear'
        uv = nt.nodes.new("ShaderNodeUVMap"); uv.uv_map = "UVMain"
        nt.links.new(uv.outputs["UV"], n.inputs["Vector"]); nt.links.new(n.outputs["Color"], bsdf.inputs["Base Color"])
    mats[name] = m
TPM = {n: v[1] for n, v in MATS.items()}

scene = bpy.context.scene
parts = []                   # (object, uv mode, material name)

# ------------------------------------------------------------------ helpers
def link(obj):
    scene.collection.objects.link(obj); return obj

def mesh_obj(name, bm, matname, uv="box"):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = link(bpy.data.objects.new(name, me)); o.data.materials.append(mats[matname])
    parts.append((o, uv, matname)); return o

def cube_bm(center, size, segments=1, rot=None):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts: v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    if segments > 1: bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=segments - 1, use_grid_fill=True)
    M = Matrix.Translation(center)
    if rot is not None: M = M @ rot
    bm.transform(M); return bm

def box(name, center, size, mat, uv="box", rot=None, bevel=0.0):
    o = mesh_obj(name, cube_bm(center, size, rot=rot), mat, uv)
    if bevel > 0: add_bevel(o, bevel)
    return o

def cyl(name, center, radius, depth, mat, segs=16, axis='Z', uv="box", scale_xy=(1, 1)):
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=radius, radius2=radius, depth=depth)
    for v in bm.verts: v.co.x *= scale_xy[0]; v.co.y *= scale_xy[1]
    R = {'Z': Matrix.Identity(4), 'X': Matrix.Rotation(math.radians(90), 4, 'Y'), 'Y': Matrix.Rotation(math.radians(90), 4, 'X')}[axis]
    bm.transform(Matrix.Translation(center) @ R); return mesh_obj(name, bm, mat, uv)

def add_bevel(o, width, segs=2, angle=40):
    m = o.modifiers.new("bev", 'BEVEL'); m.width = width; m.segments = segs; m.limit_method = 'ANGLE'; m.angle_limit = math.radians(angle)

def bake_mods(o):
    keep = [m for m in o.data.materials if m is not None]
    dg = bpy.context.evaluated_depsgraph_get(); ev = o.evaluated_get(dg)
    me = bpy.data.meshes.new_from_object(ev); old = o.data
    o.modifiers.clear(); o.data = me; bpy.data.meshes.remove(old)
    me.materials.clear()
    for m in keep: me.materials.append(m)
    for p in me.polygons: p.material_index = 0

def boolean(o, cutter, op='DIFFERENCE'):
    m = o.modifiers.new("bool", 'BOOLEAN'); m.operation = op; m.solver = 'EXACT'; m.object = cutter; m.use_self = True
    bake_mods(o)

def delete(o):
    bpy.data.objects.remove(o, do_unlink=True)

def rough_box(name, center, size, mat, amp=0.012, cut=0.3, chips=0, chip_depth=0.04, seed=0):
    """Box with a fine grid, outline wobbled by noise and a few chipped notches on the upper rim = worn concrete."""
    n = max(2, int(max(size) / cut))
    bm = cube_bm(Vector((0, 0, 0)), size, segments=n)
    rnd = random.Random(seed); hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
    chip_pts = [Vector((rnd.uniform(-hx, hx), rnd.uniform(-hy, hy), hz)) for _ in range(chips)]
    for v in bm.verts:
        p = v.co.copy()
        on_x = abs(abs(p.x) - hx) < 1e-4; on_y = abs(abs(p.y) - hy) < 1e-4; on_z = abs(abs(p.z) - hz) < 1e-4
        if on_x or on_y or on_z:
            nz = noise.noise(Vector((p.x * 2.3 + seed, p.y * 2.3, p.z * 2.3)))
            if on_x: v.co.x += math.copysign(amp * nz, p.x)
            if on_y: v.co.y += math.copysign(amp * nz, p.y)
            if on_z and (on_x or on_y): v.co.z += amp * 0.8 * noise.noise(Vector((p.x * 3 + 9, p.y * 3 + seed, p.z)))
        if p.z > hz - 0.02:
            for c in chip_pts:
                d = (Vector((p.x, p.y, 0)) - Vector((c.x, c.y, 0))).length
                if d < 0.14 and (abs(abs(p.x) - hx) < 0.05 or abs(abs(p.y) - hy) < 0.05):
                    k = 1 - d / 0.14; v.co.z -= chip_depth * k;
                    if abs(abs(p.x) - hx) < 0.05: v.co.x -= math.copysign(0.03 * k, p.x)
                    if abs(abs(p.y) - hy) < 0.05: v.co.y -= math.copysign(0.03 * k, p.y)
    bm.transform(Matrix.Translation(center))
    return mesh_obj(name, bm, mat)

def to_world(objs, M):
    for o in objs: o.data.transform(M)

# ------------------------------------------------------------------ dimensions
W, D = 4.6, 3.2                 # walls
FY = -D / 2                     # front wall plane (-Y), back wall plane = +D/2
Z0, ZT = 0.25, 2.75             # wall bottom/top
DOOR_X = (-1.3, 1.3)            # -1.3 = men (viewer's left), +1.3 = women
DOOR_W, DOOR_H = 0.94, 1.9

# ------------------------------------------------------------------ footing, steps, roof
rough_box("Plinth", Vector((0, 0, 0.025)), (5.1, 3.7, 0.45), "ParkToilet_Base", amp=0.012, cut=0.35, seed=1)
rough_box("Step1", Vector((0, -2.1, -0.015)), (4.4, 0.5, 0.37), "ParkToilet_Base", amp=0.010, cut=0.3, chips=2, chip_depth=0.025, seed=2)
rough_box("Step2", Vector((0, -2.6, -0.06)), (4.4, 0.5, 0.29), "ParkToilet_Base", amp=0.010, cut=0.3, chips=2, chip_depth=0.025, seed=3)
rough_box("Roof", Vector((0, 0, 2.875)), (5.2, 3.8, 0.25), "ParkToilet_Concrete", amp=0.016, cut=0.22, chips=7, chip_depth=0.05, seed=4)
# slight lip along the roof edge
# ------------------------------------------------------------------ walls with cut-outs
walls = box("Walls", Vector((0, 0, (Z0 + ZT) / 2)), (W, D, ZT - Z0), "ParkToilet_Plaster", bevel=0.03)
bake_mods(walls)
cutters = []
for x in DOOR_X:       # recessed door openings (0.12 deep)
    cutters.append(cube_bm(Vector((x, FY + 0.05, Z0 + 0.98)), (DOOR_W + 0.04, 0.22, 1.96)))
cut_objs = []
for i, bm in enumerate(cutters):
    me = bpy.data.meshes.new("cut%d" % i); bm.to_mesh(me); bm.free(); c = link(bpy.data.objects.new("cut%d" % i, me)); cut_objs.append(c)

# the lattice: bar boxes merged into one mesh, then clipped to the opening with a boolean
def make_lattice(M, name):
    me = bpy.data.meshes.new(name + "_lat"); bm = bmesh.new()
    pitch, th, ln = 0.105, 0.030, 1.2
    for ang in (45, -45):
        R = Matrix.Rotation(math.radians(ang), 4, 'Y')
        for k in range(-8, 9):
            vs = []
            sub = bmesh.new(); bmesh.ops.create_cube(sub, size=1.0)
            for v in sub.verts: v.co = Vector((v.co.x * th, v.co.y * 0.05, v.co.z * ln))
            sub.transform(R @ Matrix.Translation(Vector((k * pitch, 0, 0))))
            sub.transform(Matrix.Translation(Vector((0, 0.055, 0))))
            tmp = bpy.data.meshes.new("t"); sub.to_mesh(tmp); sub.free()
            bm.from_mesh(tmp); bpy.data.meshes.remove(tmp)
    bm.to_mesh(me); bm.free()
    lat = link(bpy.data.objects.new(name + "_lat", me)); lat.data.materials.append(mats["ParkToilet_Concrete"])
    clip_bm = cube_bm(Vector((0, 0.055, 0)), (0.88, 0.12, 0.30))
    cme = bpy.data.meshes.new(name + "_clip"); clip_bm.to_mesh(cme); clip_bm.free(); clip = link(bpy.data.objects.new(name + "_clip", cme))
    boolean(lat, clip, 'INTERSECT'); delete(clip)
    lat.data.transform(M)
    parts.append((lat, "box", "ParkToilet_Concrete"))
    return lat

def build_vent(pos, rot_z_deg, name):
    M = Matrix.Translation(pos) @ Matrix.Rotation(math.radians(rot_z_deg), 4, 'Z')
    cb = cube_bm(Vector((0, 0.02, 0)), (0.88, 0.24, 0.30))
    cme = bpy.data.meshes.new(name + "_cut"); cb.to_mesh(cme); cb.free(); cutter = link(bpy.data.objects.new(name + "_cut", cme)); cutter.data.transform(M)
    make_lattice(M, name)
    dark = box(name + "_dark", Vector((0, 0.135, 0)), (0.88, 0.012, 0.30), "ParkToilet_VentDark", uv="box"); dark.data.transform(M)
    # rim frame around the opening, standing 2 cm proud of the wall
    rim = [((0, -0.01, 0.185), (1.0, 0.03, 0.07)), ((0, -0.01, -0.185), (1.0, 0.03, 0.07)), ((0.47, -0.01, 0), (0.06, 0.03, 0.30)), ((-0.47, -0.01, 0), (0.06, 0.03, 0.30))]
    for i, (c, s) in enumerate(rim):
        r = box(name + "_rim%d" % i, Vector(c), s, "ParkToilet_Base"); r.data.transform(M)
    return cutter

vent_cutters = []
vent_cutters.append(build_vent(Vector((-1.3, -FY, 2.2)), 180, "VentB1"))     # back wall (+Y), faces +Y -> rotate 180
vent_cutters.append(build_vent(Vector((1.3, -FY, 2.2)), 180, "VentB2"))
vent_cutters.append(build_vent(Vector((-W / 2, 0, 2.2)), -90, "VentL"))       # left wall (-X)

for c in cut_objs + vent_cutters:
    boolean(walls, c)
for c in cut_objs + vent_cutters:
    delete(c)
parts.append((walls, "box", "ParkToilet_Plaster"))

# ------------------------------------------------------------------ doors, frames, signs, lamp (front, -Y)
for x in DOOR_X:
    men = x < 0
    # frame around the recess (protrudes 2 cm)
    zc = Z0 + 1.04
    for nm, c, s in (("Lintel", (x, FY - 0.01, Z0 + 2.04 + 0.07), (1.22, 0.04, 0.14)), ("JambL", (x - 0.57, FY - 0.01, zc), (0.12, 0.04, 2.1)), ("JambR", (x + 0.57, FY - 0.01, zc), (0.12, 0.04, 2.1))):
        box("Frame" + nm, Vector(c), s, "ParkToilet_Base", bevel=0.008)
    door = box("Door", Vector((x, FY + 0.075, Z0 + DOOR_H / 2)), (DOOR_W, 0.07, DOOR_H), "ParkToilet_Door", uv="norm", bevel=0.008)
    for s in range(5):
        slat = box("Louvre", Vector((x, FY + 0.035, Z0 + 0.22 + s * 0.05)), (0.46, 0.04, 0.022), "ParkToilet_RustDark", rot=Matrix.Rotation(math.radians(25), 4, 'X'))
    box("Handle", Vector((x + 0.36, FY + 0.03, Z0 + 0.9)), (0.045, 0.05, 0.2), "ParkToilet_RustDark", bevel=0.01)
    cyl("Lock", Vector((x + 0.36, FY + 0.04, Z0 + 1.12)), 0.03, 0.04, "ParkToilet_RustDark", segs=10, axis='Y')
    sg = box("Sign" + ("Men" if men else "Women"), Vector((x, FY - 0.02, 2.45)), (0.96, 0.04, 0.36), "ParkToilet_SignMen" if men else "ParkToilet_SignWomen", uv="norm", bevel=0.006)
    for bx in (-0.43, 0.43):
        for bz in (-0.15, 0.15):
            cyl("Bolt", Vector((x + bx, FY - 0.045, 2.45 + bz)), 0.012, 0.012, "ParkToilet_RustDark", segs=8, axis='Y')

# caged lamp between the doors
box("LampPlate", Vector((0, FY - 0.01, Z0 + 2.0)), (0.34, 0.04, 0.18), "ParkToilet_RustDark", bevel=0.01)
box("LampGlass", Vector((0, FY - 0.09, Z0 + 2.0)), (0.26, 0.12, 0.11), "ParkToilet_Lamp", bevel=0.015)
for bx in (-0.1, 0, 0.1):
    box("LampBar", Vector((bx, FY - 0.155, Z0 + 2.0)), (0.02, 0.02, 0.15), "ParkToilet_Rust")
box("LampCageTop", Vector((0, FY - 0.12, Z0 + 2.075)), (0.3, 0.1, 0.02), "ParkToilet_Rust")
box("LampCageBottom", Vector((0, FY - 0.12, Z0 + 1.925)), (0.3, 0.1, 0.02), "ParkToilet_Rust")

# ------------------------------------------------------------------ roof extras
cyl("TankBase", Vector((-0.5, 0.9, 3.11)), 0.475, 0.22, "ParkToilet_Tank", segs=20)
bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=10, radius=1.0)
for v in bm.verts: v.co = Vector((v.co.x * 0.45, v.co.y * 0.45, v.co.z * 0.21))
bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -0.001], context='VERTS')
bm.transform(Matrix.Translation(Vector((-0.5, 0.9, 3.22)))); mesh_obj("TankDome", bm, "ParkToilet_Tank")
cyl("TankCap", Vector((-0.5, 0.9, 3.44)), 0.07, 0.07, "ParkToilet_Pipe", segs=10)
cyl("RoofVent", Vector((1.5, 1.2, 3.22)), 0.035, 0.44, "ParkToilet_Pipe", segs=10)
cyl("RoofVentCap", Vector((1.5, 1.2, 3.45)), 0.06, 0.02, "ParkToilet_Pipe", segs=10)

# ------------------------------------------------------------------ back wall: electrical box + conduit
BY = D / 2
box("ElectricBox", Vector((0, BY + 0.08, 1.25)), (0.42, 0.16, 0.56), "ParkToilet_Metal", bevel=0.01)
box("ElectricBoxDoor", Vector((0, BY + 0.162, 1.25)), (0.36, 0.015, 0.50), "ParkToilet_RustDark")
cyl("Conduit", Vector((0, BY + 0.04, (1.53 + ZT) / 2)), 0.03, ZT - 1.53, "ParkToilet_Pipe", segs=8)
for z in (1.9, 2.4):
    box("ConduitClamp", Vector((0, BY + 0.045, z)), (0.07, 0.025, 0.035), "ParkToilet_RustDark")

# ------------------------------------------------------------------ UV0 (metric box projection / normalised) -----------------
def assign_uv(o, mode, matname):
    me = o.data
    if "UVMain" not in me.uv_layers: me.uv_layers.new(name="UVMain")
    layer = me.uv_layers["UVMain"]
    bm = bmesh.new(); bm.from_mesh(me); uvl = bm.loops.layers.uv["UVMain"]
    xs = [v.co.x for v in bm.verts]; zs = [v.co.z for v in bm.verts]
    x0, x1, z0, z1 = min(xs), max(xs), min(zs), max(zs)
    tpm = TPM.get(matname) or 1.0
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for l in f.loops:
            p = l.vert.co
            if mode == "norm":
                u, v = (p.x - x0) / max(x1 - x0, 1e-6), (p.z - z0) / max(z1 - z0, 1e-6)
            else:
                u, v = {0: (p.y, p.z), 1: (p.x, p.z), 2: (p.x, p.y)}[ax]; u *= tpm; v *= tpm
            l[uvl].uv = (u, v)
    bm.to_mesh(me); bm.free()

empty = [o.name for o, _, _ in parts if len(o.data.vertices) == 0]
if empty: raise RuntimeError('empty parts: %s' % empty)
for o, mode, mn in parts:
    # sanity: make sure normals point outward
    bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(o.data); bm.free()
    assign_uv(o, mode, mn)

# ------------------------------------------------------------------ join ---------------------------------------------------
for o in bpy.context.selected_objects: o.select_set(False)
for o, _, _ in parts:
    if o.name in bpy.data.objects: o.select_set(True)
act = parts[0][0]; bpy.context.view_layer.objects.active = act
bpy.ops.object.join()
toilet = bpy.context.view_layer.objects.active; toilet.name = "ParkToilet"; toilet.data.name = "ParkToilet"
bpy.ops.object.select_all(action='DESELECT'); toilet.select_set(True)
result = {"verts": len(toilet.data.vertices), "faces": len(toilet.data.polygons), "materials": [m.name for m in toilet.data.materials], "uv": [u.name for u in toilet.data.uv_layers]}
