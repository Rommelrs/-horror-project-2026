# Helpers: camera snapshots (workbench or eevee), bone-stick proxy for a rig.   exec(open(this).read())
import bpy, math, os, bmesh
from mathutils import Vector, Matrix

SHOTS = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\AnimationTools\questioner\shots"
os.makedirs(SHOTS, exist_ok=True)

def _cam():
    cam = bpy.data.objects.get("SnapCam")
    if cam is None:
        cd = bpy.data.cameras.new("SnapCam")
        cam = bpy.data.objects.new("SnapCam", cd)
        bpy.context.scene.collection.objects.link(cam)
    bpy.context.scene.camera = cam
    return cam

def snap(name, loc, target, lens=50, res=(900, 900), engine="BLENDER_WORKBENCH", shading="MATERIAL", ortho=None):
    sc = bpy.context.scene
    cam = _cam()
    cam.location = Vector(loc)
    d = Vector(target) - Vector(loc)
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = lens
    cam.data.clip_start = 0.01
    if ortho:
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = ortho
    else:
        cam.data.type = "PERSP"
    sc.render.engine = engine
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    if engine == "BLENDER_WORKBENCH":
        sh = sc.display.shading
        sh.light = "STUDIO"
        sh.color_type = shading          # MATERIAL / TEXTURE / OBJECT / SINGLE
        sh.show_cavity = True
        sh.cavity_type = "BOTH"
        sh.show_shadows = False
        sh.background_type = "VIEWPORT"
        sh.background_color = (0.18, 0.17, 0.16)
    path = os.path.join(SHOTS, name + ".png")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path

def stick_proxy(rig_name, radius=0.012, name=None):
    """thin box per deform bone, skinned 100% to it, so a rig can be seen in renders"""
    rig = bpy.data.objects[rig_name]
    name = name or rig_name + "_Sticks"
    old = bpy.data.objects.get(name)
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    bm = bmesh.new()
    groups = []
    for b in rig.data.bones:
        if not b.use_deform:
            continue
        h = rig.matrix_world @ b.head_local
        t = rig.matrix_world @ b.tail_local
        r = radius * (0.5 if "Hand" in b.name and b.name[-1].isdigit() else 1.0)
        res = bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=r, radius2=r * 0.6, depth=(t - h).length)
        vs = res["verts"]
        q = Vector((0, 0, 1)).rotation_difference((t - h).normalized())
        bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=q.to_matrix(), verts=vs)
        bmesh.ops.translate(bm, vec=(h + t) / 2, verts=vs)
        groups.append((b.name, [v for v in vs]))
    me = bpy.data.meshes.new(name)
    idx = {}
    bm.verts.index_update()
    for gname, vs in groups:
        idx[gname] = [v.index for v in vs]
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    for gname, ids in idx.items():
        vg = ob.vertex_groups.new(name=gname)
        vg.add(ids, 1.0, "REPLACE")
    mod = ob.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    ob.parent = rig
    return ob
