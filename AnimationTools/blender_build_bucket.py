# Low-poly PSX plastic bucket (real metres, ~2 m bucket = 0.30 m tall), built standing upright in Blender then exported; Unity lays it
# on its side. Hollow tapered body (12 sides, open top, wall thickness), rolled rim, wire handle with ear lugs.
# UV: u = angle around, v = height / 0.30 (inside walls share the same mapping). Own scene "Bucket" so other work is untouched.
import bpy, bmesh, math
from mathutils import Vector, Matrix
PROJ = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/"
H, RB, RT, SEG, TH = 0.30, 0.115, 0.15, 12, 0.006

sc = bpy.data.scenes.get("Bucket") or bpy.data.scenes.new("Bucket")
for o in list(sc.objects): bpy.data.objects.remove(o, do_unlink=True)
bpy.context.window.scene = sc
mat = bpy.data.materials.get("Bucket_Plastic") or bpy.data.materials.new("Bucket_Plastic")
bm = bmesh.new()
# outer + inner wall rings, joined at the rim (open top), closed bottom (outer + inner floor)
def ring(r, z):
    return [bm.verts.new((r * math.cos(2 * math.pi * i / SEG), r * math.sin(2 * math.pi * i / SEG), z)) for i in range(SEG)]
ob, ot = ring(RB, 0.0), ring(RT, H)
ib, it = ring(RB - TH, TH), ring(RT - TH, H)
def band(a, b, flip=False):
    for i in range(SEG):
        j = (i + 1) % SEG
        f = (a[i], a[j], b[j], b[i]) if not flip else (a[i], b[i], b[j], a[j])
        bm.faces.new(f)
band(ob, ot)                 # outside wall
band(ib, it, flip=True)      # inside wall (faces inward)
band(ot, it, flip=True)      # rim top
bm.faces.new(list(reversed(ob)))      # base underside
bm.faces.new(ib)                      # inside floor
# rolled rim lip: a slightly wider short band just below the top
lo, hi = ring(RT + 0.008, H - 0.022), ring(RT + 0.008, H - 0.002)
band(lo, hi)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
# handle ears + wire handle (arc over the top, in the XZ plane)
def boxv(c, s):
    r = bmesh.ops.create_cube(bm, size=1.0)
    for v in r["verts"]: v.co = Vector((v.co.x * s[0] + c[0], v.co.y * s[1] + c[1], v.co.z * s[2] + c[2]))
for sx in (-1, 1): boxv((sx * (RT + 0.006), 0, H - 0.05), (0.014, 0.03, 0.04))
pts = [Vector((math.cos(a) * (RT + 0.012), 0, H - 0.05 + math.sin(a) * 0.16)) for a in [math.pi * k / 8 for k in range(9)]]
# flopped back over the body (toward -Y): lying on its side in Unity, that side faces up, so the handle rests on the bucket
HR = Matrix.Rotation(math.radians(115), 3, 'X'); C = Vector((0, 0, H - 0.05))
pts = [HR @ (p - C) + C for p in pts]
for a, b in zip(pts[:-1], pts[1:]):
    d = b - a; R = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
    r = bmesh.ops.create_cube(bm, size=1.0)
    for v in r["verts"]: v.co = (Matrix.Translation((a + b) / 2) @ R) @ Vector((v.co.x * 0.006, v.co.y * 0.006, v.co.z * d.length))
me = bpy.data.meshes.new("Bucket"); bm.to_mesh(me); bm.free()
uv = me.uv_layers.new(name="UVMain")
for poly in me.polygons:
    us = {li: math.atan2(me.vertices[me.loops[li].vertex_index].co.y, me.vertices[me.loops[li].vertex_index].co.x) / (2 * math.pi) + 0.5
          for li in poly.loop_indices}
    wrap = max(us.values()) - min(us.values()) > 0.5          # face straddles the seam: keep its u continuous
    for li in poly.loop_indices:
        co = me.vertices[me.loops[li].vertex_index].co
        u = us[li] + (1.0 if wrap and us[li] < 0.5 else 0.0)
        uv.data[li].uv = (u, co.z / H)
me.materials.append(mat)
ob_ = bpy.data.objects.new("Bucket", me); sc.collection.objects.link(ob_)
for o in bpy.context.selected_objects: o.select_set(False)
ob_.select_set(True); bpy.context.view_layer.objects.active = ob_
import os; os.makedirs(PROJ + "Assets/Models/Props", exist_ok=True)
bpy.ops.export_scene.fbx(filepath=PROJ + "Assets/Models/Props/Bucket.fbx", use_selection=True, object_types={'MESH'},
    axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
    mesh_smooth_type='FACE', path_mode='STRIP')
result = {"verts": len(me.vertices), "faces": len(me.polygons)}
