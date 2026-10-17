# Emerald Salon neon lightbox (replaces "Shop signs/Cube (1)").  Blender: exec(open(this).read())
# Origin = centre of the lightbox's back face (against the wall).  Front faces Blender -Y (= Unity +Z; the prefab is
# rotated in Unity so it faces the street).  Objects:
#   EmeraldSign_Box    7.5 x 1.5 x 0.25 m lightbox. material slot 0 = face (sign texture), slot 1 = body
#   EmeraldSign_Globe  round disc lamp above the sign (slot 0 = lens, slot 1 = body)
#   EmeraldSign_Downlights  two small fixtures under the sign (slot 0 = lens, slot 1 = body)
import bpy, os, math
from mathutils import Vector

ROOT = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026"
OUT = os.path.join(ROOT, "Assets", "Models", "EmeraldSign")
for o in [o for o in bpy.data.objects if o.name.startswith("EmeraldSign_")]:
    bpy.data.objects.remove(o, do_unlink=True)

def get_mat(name, col):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = col
    return m

M_FACE = get_mat("M_EmeraldSign_Face", (0.1, 0.6, 0.3, 1))
M_BODY = get_mat("M_EmeraldSign_Body", (0.05, 0.08, 0.07, 1))
M_LENS = get_mat("M_EmeraldSign_Lens", (1, 1, 0.9, 1))

def build(name, verts, faces, uvs, mats, slots):
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    uvl = me.uv_layers.new(name="UVMap")
    for pi, poly in enumerate(me.polygons):
        poly.use_smooth = False
        poly.material_index = slots[pi]
        for li, uv in zip(poly.loop_indices, uvs[pi]):
            uvl.data[li].uv = uv
    for m in mats: me.materials.append(m)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob

def add_box(V, F, U, S, c, size, front_slot, body_slot, front_uv=None):
    """axis aligned box; the -Y face gets front_slot (+ front_uv rect), all others body_slot"""
    cx, cy, cz = c; sx, sy, sz = size[0] / 2, size[1] / 2, size[2] / 2
    base = len(V)
    for dx in (-1, 1):
        for dy in (-1, 1):
            for dz in (-1, 1):
                V.append((cx + dx * sx, cy + dy * sy, cz + dz * sz))
    idx = lambda dx, dy, dz: base + ((dx > 0) * 4 + (dy > 0) * 2 + (dz > 0))
    quads = {
        "-y": [(-1, -1, -1), (1, -1, -1), (1, -1, 1), (-1, -1, 1)],
        "+y": [(1, 1, -1), (-1, 1, -1), (-1, 1, 1), (1, 1, 1)],
        "-x": [(-1, 1, -1), (-1, -1, -1), (-1, -1, 1), (-1, 1, 1)],
        "+x": [(1, -1, -1), (1, 1, -1), (1, 1, 1), (1, -1, 1)],
        "-z": [(-1, 1, -1), (1, 1, -1), (1, -1, -1), (-1, -1, -1)],
        "+z": [(-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)],
    }
    for k, q in quads.items():
        F.append([idx(*p) for p in q])
        if k == "-y":
            S.append(front_slot)
            u0, v0, u1, v1 = front_uv or (0, 0, 1, 1)
            U.append([(u0, v0), (u1, v0), (u1, v1), (u0, v1)])
        else:
            S.append(body_slot)
            U.append([(0.5, 0.5)] * 4)

def add_disc(V, F, U, S, c, r, depth, segs, lens_slot, body_slot):
    """disc lamp facing -Y: body ring + lens cap on the front"""
    cx, cy, cz = c
    back = cy + depth / 2; front = cy - depth / 2
    ring_b, ring_f = [], []
    for i in range(segs):
        a = 2 * math.pi * i / segs
        x, z = cx + math.cos(a) * r, cz + math.sin(a) * r
        ring_b.append(len(V)); V.append((x, back, z))
        ring_f.append(len(V)); V.append((x, front, z))
    for i in range(segs):
        j = (i + 1) % segs
        F.append([ring_b[i], ring_b[j], ring_f[j], ring_f[i]]); S.append(body_slot); U.append([(0.5, 0.5)] * 4)
    # lens: slightly domed cap
    cap_c = len(V); V.append((cx, front - r * 0.18, cz))
    for i in range(segs):
        j = (i + 1) % segs
        F.append([ring_f[i], ring_f[j], cap_c]); S.append(lens_slot)
        U.append([(0.5 + 0.5 * math.cos(2 * math.pi * i / segs), 0.5 + 0.5 * math.sin(2 * math.pi * i / segs)),
                  (0.5 + 0.5 * math.cos(2 * math.pi * j / segs), 0.5 + 0.5 * math.sin(2 * math.pi * j / segs)), (0.5, 0.5)])
    back_c = len(V); V.append((cx, back, cz))
    for i in range(segs):
        j = (i + 1) % segs
        F.append([ring_b[j], ring_b[i], back_c]); S.append(body_slot); U.append([(0.5, 0.5)] * 3)

W, H, D = 7.5, 1.5, 0.25
# ---- lightbox (back face on the wall at y = 0, front at y = -D)
V, F, U, S = [], [], [], []
add_box(V, F, U, S, (0, -D / 2, 0), (W, D, H), 0, 1)
# thin capping strip on top and bottom (a little deeper than the box, like an aluminium cap)
add_box(V, F, U, S, (0, -D / 2 - 0.01, H / 2 + 0.02), (W + 0.06, D + 0.04, 0.04), 1, 1)
add_box(V, F, U, S, (0, -D / 2 - 0.01, -H / 2 - 0.02), (W + 0.06, D + 0.04, 0.04), 1, 1)
build("EmeraldSign_Box", V, F, U, [M_FACE, M_BODY], S)

# ---- globe / disc lamp above the sign on a short wall plate
V, F, U, S = [], [], [], []
gz = H / 2 + 0.55
add_box(V, F, U, S, (0, -0.02, gz), (0.18, 0.04, 0.18), 1, 1)                 # wall plate
add_disc(V, F, U, S, (0, -0.12, gz), 0.36, 0.16, 20, 0, 1)
build("EmeraldSign_Globe", V, F, U, [M_LENS, M_BODY], S)

# ---- two small downlights under the sign
V, F, U, S = [], [], [], []
for x in (-2.3, 2.3):
    add_box(V, F, U, S, (x, -0.16, -H / 2 - 0.09), (0.05, 0.05, 0.12), 1, 1)  # stem
    # lens faces down: build a small box whose -Y face is irrelevant; mark the bottom with the lens slot afterwards
    s = len(F)
    add_box(V, F, U, S, (x, -0.16, -H / 2 - 0.17), (0.16, 0.12, 0.06), 1, 1)
    S[s + 4] = 0                                                              # "-z" face = lens (faces down)
build("EmeraldSign_Downlights", V, F, U, [M_LENS, M_BODY], S)

bpy.ops.object.select_all(action="DESELECT")
for o in bpy.data.objects:
    if o.name.startswith("EmeraldSign_"): o.select_set(True)
bpy.context.view_layer.objects.active = bpy.data.objects["EmeraldSign_Box"]
os.makedirs(OUT, exist_ok=True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "EmeraldSign.fbx"), use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                         bake_space_transform=True, object_types={"MESH"}, axis_forward="-Z", axis_up="Y")
print("exported", os.path.join(OUT, "EmeraldSign.fbx"))
