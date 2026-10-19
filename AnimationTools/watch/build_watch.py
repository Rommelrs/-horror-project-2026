# Wristwatch fitted to the Khalid cutscene forearm.  Blender: exec(open(this).read())
# Watch frame (metres): origin = arm axis point, +X = along the arm toward the hand (3 o'clock / crown),
# +Y = 12 o'clock (circumferential), +Z = dorsal side (dial normal).  Strap follows the measured wrist octagon.
import bpy, bmesh, math
import numpy as np
from mathutils import Vector

CM = 0.01
OUT_FBX = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\Assets\Prefab\Props\Handwatch\Watch_v2.fbx"
DIAL_PNG = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\AnimationTools\watch\T_Watch_Dial.png"

# wrist outline at the watch position, (Y, Z) cm, counter-clockwise from -140 deg
WRIST = [(-2.05, -1.71), (-0.33, -4.12), (2.36, -2.75), (4.52, -1.06), (1.67, 2.66), (-0.07, 4.83), (-1.78, 2.87), (-3.54, 1.36)]
GAP_IN, GAP_OUT = 0.30, 0.85       # strap inner / outer offset from the skin (cm)
STRAP_W = 2.6                       # strap width along the arm (cm)

# clean up a previous run
for n in ("Watch_v2",):
    o = bpy.data.objects.get(n)
    if o:
        bpy.data.objects.remove(o, do_unlink=True)
for m in list(bpy.data.meshes):
    if m.name.startswith("Watch_v2"):
        bpy.data.meshes.remove(m)

bm = bmesh.new()
MAT = {"strap": 0, "metal": 1, "dial": 2, "crown": 3}


def V(x, y, z):
    return bm.verts.new((x * CM, y * CM, z * CM))


def quad(vs, mat):
    f = bm.faces.new(vs)
    f.material_index = MAT[mat]
    return f


def box(cx, cy, cz, sx, sy, sz, mat):
    h = [(-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1), (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)]
    vs = [V(cx + a * sx / 2, cy + b * sy / 2, cz + c * sz / 2) for a, b, c in h]
    for idx in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (2, 3, 7, 6), (1, 2, 6, 5), (3, 0, 4, 7)):
        quad([vs[i] for i in idx], mat)


def cyl_z(cx, cy, z0, z1, r0, r1, seg, mat, cap_top=True, cap_bot=True):
    ring0 = [V(cx + r0 * math.cos(2 * math.pi * i / seg), cy + r0 * math.sin(2 * math.pi * i / seg), z0) for i in range(seg)]
    ring1 = [V(cx + r1 * math.cos(2 * math.pi * i / seg), cy + r1 * math.sin(2 * math.pi * i / seg), z1) for i in range(seg)]
    for i in range(seg):
        j = (i + 1) % seg
        quad([ring0[i], ring0[j], ring1[j], ring1[i]], mat)
    if cap_bot:
        bm.faces.new(ring0[::-1]).material_index = MAT[mat]
    if cap_top:
        bm.faces.new(ring1).material_index = MAT[mat]
    return ring0, ring1


# ---------- strap: octagon ring offset out from the skin ----------
P = np.array(WRIST, dtype=float)
n = len(P)
normals = []
for i in range(n):
    a, b = P[i], P[(i + 1) % n]
    e = b - a
    nn = np.array([e[1], -e[0]])
    nn /= np.linalg.norm(nn)
    normals.append(nn)


def offset(d):
    out = []
    for i in range(n):
        n0, n1 = normals[i - 1], normals[i]
        m = (n0 + n1) / (1.0 + float(np.dot(n0, n1)))
        out.append(P[i] + m * d)
    return out


inner, outer = offset(GAP_IN), offset(GAP_OUT)
hw = STRAP_W / 2
ring = []
for i in range(n):
    ring.append((V(-hw, *inner[i]) if False else None))
bm.clear()
# (re)build with explicit helper because V takes x,y,z
def Vx(x, y, z):
    return bm.verts.new((x * CM, y * CM, z * CM))


vin_a = [Vx(-hw, inner[i][0], inner[i][1]) for i in range(n)]
vin_b = [Vx(hw, inner[i][0], inner[i][1]) for i in range(n)]
vout_a = [Vx(-hw, outer[i][0], outer[i][1]) for i in range(n)]
vout_b = [Vx(hw, outer[i][0], outer[i][1]) for i in range(n)]
for i in range(n):
    j = (i + 1) % n
    quad([vout_a[i], vout_a[j], vout_b[j], vout_b[i]], "strap")     # outside
    quad([vin_b[i], vin_b[j], vin_a[j], vin_a[i]], "strap")         # inside
    quad([vin_a[i], vin_a[j], vout_a[j], vout_a[i]], "strap")       # edge -X
    quad([vout_b[i], vout_b[j], vin_b[j], vin_b[i]], "strap")       # edge +X

# ---------- buckle under the wrist ----------
bot = outer[1]
box(0.0, bot[0], bot[1] - 0.05, 2.5, 1.9, 0.38, "metal")

# ---------- case ----------
CASE_Z0, CASE_Z1 = 5.15, 6.95
cyl_z(0, 0, CASE_Z0, CASE_Z1, 1.72, 1.75, 28, "metal", cap_top=False, cap_bot=True)
# bezel ring (open in the middle, dial sits inside)
rb0, rb1 = cyl_z(0, 0, CASE_Z1, CASE_Z1 + 0.22, 1.82, 1.82, 28, "metal", cap_top=False, cap_bot=False)
# bezel top annulus -> inner ring
inner_ring = [Vx(1.55 * math.cos(2 * math.pi * i / 28), 1.55 * math.sin(2 * math.pi * i / 28), CASE_Z1 + 0.22) for i in range(28)]
for i in range(28):
    j = (i + 1) % 28
    quad([rb1[i], rb1[j], inner_ring[j], inner_ring[i]], "metal")
# inner wall dropping to the dial
dial_z = CASE_Z1 + 0.04
dial_ring = [Vx(1.55 * math.cos(2 * math.pi * i / 28), 1.55 * math.sin(2 * math.pi * i / 28), dial_z) for i in range(28)]
for i in range(28):
    j = (i + 1) % 28
    quad([inner_ring[j], inner_ring[i], dial_ring[i], dial_ring[j]], "metal")
# case top flat between case wall and bezel (hidden), skip.  Dial: fan with UVs
centre = Vx(0, 0, dial_z)
uvl = bm.loops.layers.uv.new("UVMap")
for i in range(28):
    j = (i + 1) % 28
    f = bm.faces.new([centre, dial_ring[i], dial_ring[j]])
    f.material_index = MAT["dial"]
    for l in f.loops:
        px, py = l.vert.co.x / CM, l.vert.co.y / CM
        # +Y = 12 o'clock = up in the texture (v), +X = 3 o'clock = right (u)
        l[uvl].uv = (0.5 + px / (2 * 1.55), 0.5 + py / (2 * 1.55))

# ---------- lugs: columns from the case sides down onto the strap ----------
pass

# ---------- crown (3 o'clock = +X) ----------
cr = []
seg = 10
cx0, cx1, rr = 1.75, 2.3, 0.32
for i in range(seg):
    a = 2 * math.pi * i / seg
    cr.append((Vx(cx0, rr * math.cos(a), CASE_Z0 + 0.9 + rr * math.sin(a)), Vx(cx1, rr * math.cos(a), CASE_Z0 + 0.9 + rr * math.sin(a))))
for i in range(seg):
    j = (i + 1) % seg
    quad([cr[i][0], cr[j][0], cr[j][1], cr[i][1]], "crown")
bm.faces.new([c[1] for c in cr]).material_index = MAT["crown"]

bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
mesh = bpy.data.meshes.new("Watch_v2")
bm.to_mesh(mesh)
bm.free()
for p in mesh.polygons:
    p.use_smooth = False
obj = bpy.data.objects.new("Watch_v2", mesh)
bpy.context.scene.collection.objects.link(obj)

# materials (names are what Unity shows)
def mk(name, col, metallic, rough):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*col, 1)
    b.inputs["Metallic"].default_value = metallic
    b.inputs["Roughness"].default_value = rough
    return m


mats = [mk("Watch_Strap", (0.55, 0.34, 0.16), 0.0, 0.7), mk("Watch_Metal", (0.62, 0.62, 0.60), 0.9, 0.35),
        mk("Watch_Dial", (1, 1, 1), 0.0, 0.5), mk("Watch_Crown", (0.75, 0.6, 0.25), 0.9, 0.35)]
for m in mats:
    mesh.materials.append(m)
dm = mats[2]
tex = dm.node_tree.nodes.new("ShaderNodeTexImage")
tex.image = bpy.data.images.load(DIAL_PNG, check_existing=True)
dm.node_tree.links.new(tex.outputs["Color"], dm.node_tree.nodes["Principled BSDF"].inputs["Base Color"])

# export just this object
bpy.ops.object.select_all(action="DESELECT")
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.export_scene.fbx(filepath=OUT_FBX, use_selection=True, object_types={"MESH"}, global_scale=1.0,
                         apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", mesh_smooth_type="FACE",
                         add_leaf_bones=False, path_mode="COPY", embed_textures=False)
result = {"verts": len(mesh.vertices), "tris": sum(len(p.vertices) - 2 for p in mesh.polygons), "bounds_cm": [round(c * 100, 2) for c in obj.dimensions], "fbx": OUT_FBX}
