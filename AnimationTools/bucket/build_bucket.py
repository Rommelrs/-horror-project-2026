# Old rusty water bucket that sits under the dripping pipe (ParkPublicToilet).   Blender: exec(open(this).read())
# Z up, origin = centre of the base on the floor, metres.  Own scene "WaterBucket".  One mesh, 2 material slots:
#   0 Bucket_Metal (rusty pail + rim + wire handle)   1 Bucket_Water (the water surface, flat disc)
# UV0 = metres (the PSXAOLit shader tiles the albedo on UV0).  Exports Assets/Models/Bucket/WaterBucket.fbx
import bpy, bmesh, math, random, os
from mathutils import Vector, Matrix

ROOT = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026"
OUT_FBX = os.path.join(ROOT, "Assets", "Models", "Bucket", "WaterBucket.fbx")
os.makedirs(os.path.dirname(OUT_FBX), exist_ok=True)
H, R_BOT, R_TOP, T = 0.30, 0.105, 0.145, 0.004     # height, outer radii bottom/top, sheet thickness
WATER_Z = 0.289                                     # water surface height: filled right up to the rim (wet ground around it makes sense)
SEG = 28
rng = random.Random(5)

scn = bpy.data.scenes.get("WaterBucket") or bpy.data.scenes.new("WaterBucket")
for o in list(scn.objects):
    bpy.data.objects.remove(o, do_unlink=True)
for m in list(bpy.data.meshes):
    if m.name == "WaterBucket":
        bpy.data.meshes.remove(m)

bm = bmesh.new()
uv_layer = bm.loops.layers.uv.new("UVMap")


def r_outer(z):
    k = z / H
    r = R_BOT + (R_TOP - R_BOT) * k
    for rz in (0.10, 0.20):                            # two stamped stiffening ribs
        r += 0.005 * math.exp(-((z - rz) / 0.012) ** 2)
    return r


# profile (r, z) going up the outside, over the rim, down the inside, across the floor
prof = []
zs = [0.0, 0.012] + [0.012 + (H - 0.012 - 0.01) * i / 14 for i in range(1, 15)] + [H - 0.006]
for z in zs:
    prof.append((r_outer(z), z))
prof.append((r_outer(H - 0.006) + 0.006, H - 0.002))             # rolled rim bead (outer)
prof.append((r_outer(H) + 0.004, H + 0.003))
prof.append((r_outer(H) - T * 0.2, H + 0.005))
prof.append((r_outer(H) - T - 0.002, H - 0.002))                 # (inner side of the bead)
for z in reversed(zs[2:]):
    prof.append((r_outer(z) - T, z - 0.002))
prof.append((R_BOT - T - 0.002, 0.012 + T))
prof.append((0.0, 0.012 + T))

# per-segment radial dents / wobble so the pail is not a perfect lathe
dent = [1.0 + rng.uniform(-0.012, 0.012) for _ in range(SEG)]
dent_z = [rng.uniform(0.0, 1.0) for _ in range(SEG)]
rings = []
for (r, z) in prof:
    ring = []
    for s in range(SEG):
        a = 2 * math.pi * s / SEG
        k = dent[s] if r > 0 else 1.0
        kk = 1.0 + (k - 1.0) * (0.4 + 0.6 * (z / H)) * (1.0 + 0.8 * math.sin(dent_z[s] * 6 + z * 18))
        ring.append(bm.verts.new((math.cos(a) * r * kk, math.sin(a) * r * kk, z)))
    rings.append(ring)

TILE = 0.5
for i in range(len(rings) - 1):
    for s in range(SEG):
        a, b = rings[i][s], rings[i][(s + 1) % SEG]
        c, d = rings[i + 1][(s + 1) % SEG], rings[i + 1][s]
        if (a.co.xy.length < 1e-6 and b.co.xy.length < 1e-6):
            continue
        vs = [a, b, c, d] if (a.co.xy - b.co.xy).length > 1e-6 and (c.co.xy - d.co.xy).length > 1e-6 else None
        try:
            if vs is None:
                tri = [a, b, d] if (c.co.xy - d.co.xy).length < 1e-6 else [a, b, c]
                f = bm.faces.new(tri)
            else:
                f = bm.faces.new(vs)
        except ValueError:
            continue
        f.material_index = 0

# ---- wire handle: a tube along a half circle, flopped over to one side
HR, TR = R_TOP + 0.005, 0.0035
pts = []
for i in range(0, 25):
    th = math.pi * i / 24
    p = Vector((HR * math.cos(th), 0, HR * math.sin(th)))
    p = Matrix.Rotation(math.radians(-68), 3, 'X') @ p              # tilt the arch away from vertical
    pts.append(p + Vector((0, 0, H - 0.01)))
SIDES = 6
tube = []
for i, p in enumerate(pts):
    t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
    n = t.cross(Vector((0, 0, 1)))
    if n.length < 1e-3:
        n = Vector((0, 1, 0))
    n.normalize(); bn = t.cross(n).normalized()
    ring = []
    for j in range(SIDES):
        a = 2 * math.pi * j / SIDES
        ring.append(bm.verts.new(p + n * math.cos(a) * TR + bn * math.sin(a) * TR))
    tube.append(ring)
for i in range(len(tube) - 1):
    for j in range(SIDES):
        f = bm.faces.new([tube[i][j], tube[i][(j + 1) % SIDES], tube[i + 1][(j + 1) % SIDES], tube[i + 1][j]])
        f.material_index = 0

# ---- water: flat disc slightly inside the wall, a tiny lip where it climbs the metal
def r_inner(z):
    return r_outer(z) - T
rw = r_inner(WATER_Z) - 0.0015
c0 = bm.verts.new((0, 0, WATER_Z + 0.004))                       # slight dome: brim-full water bulges a little
wr = [bm.verts.new((math.cos(2 * math.pi * s / SEG) * rw, math.sin(2 * math.pi * s / SEG) * rw, WATER_Z)) for s in range(SEG)]
for s in range(SEG):
    f = bm.faces.new([c0, wr[s], wr[(s + 1) % SEG]])                 # normals up
    f.material_index = 1

bm.normal_update()
# ---- UV0 in metres: cylindrical wrap for the metal, planar for the water
for f in bm.faces:
    for l in f.loops:
        co = l.vert.co
        if f.material_index == 1:
            l[uv_layer].uv = (co.x / TILE, co.y / TILE)
        else:
            ang = math.atan2(co.y, co.x) / (2 * math.pi)
            l[uv_layer].uv = (ang * 2 * math.pi * 0.125 / TILE, co.z / TILE)
# fix the seam: wrap-around faces get u offset by one full turn
for f in bm.faces:
    us = [l[uv_layer].uv.x for l in f.loops]
    if max(us) - min(us) > 0.5 * 2 * math.pi * 0.125 / TILE:
        full = 2 * math.pi * 0.125 / TILE
        for l in f.loops:
            if l[uv_layer].uv.x < 0:
                l[uv_layer].uv.x += full

mesh = bpy.data.meshes.new("WaterBucket")
bm.to_mesh(mesh)
bm.free()
for p in mesh.polygons:
    p.use_smooth = (p.material_index == 0)
obj = bpy.data.objects.new("WaterBucket", mesh)
scn.collection.objects.link(obj)

for name, col, rough in (("Bucket_Metal", (0.42, 0.30, 0.22, 1), 0.8), ("Bucket_Water", (0.04, 0.06, 0.07, 1), 0.05)):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = col
    b.inputs["Roughness"].default_value = rough
    mesh.materials.append(m)

# ---- export (own scene only)
bpy.context.window.scene = scn
bpy.context.view_layer.update()
for o in list(bpy.context.view_layer.objects):
    if o is not None:
        o.select_set(False)
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.export_scene.fbx(filepath=OUT_FBX, use_selection=True, object_types={'MESH'}, apply_unit_scale=True,
                         axis_forward='-Z', axis_up='Y', add_leaf_bones=False, mesh_smooth_type='FACE')
xs = [v.co.x for v in mesh.vertices]; ys = [v.co.y for v in mesh.vertices]; zs_ = [v.co.z for v in mesh.vertices]
result = {"verts": len(mesh.vertices), "tris": sum(len(p.vertices) - 2 for p in mesh.polygons),
          "x": [round(min(xs), 3), round(max(xs), 3)], "y": [round(min(ys), 3), round(max(ys), 3)], "z": [round(min(zs_), 3), round(max(zs_), 3)]}
