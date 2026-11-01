# Background Blender (khalid_cutscene_anims.blend, NOT saved): retarget candidate Mixamo clips onto Khalid and render a
# few frames of each (side + front three-quarter, Workbench) so they can be compared before picking. The cameras follow
# each clip's own facing on its first frame (some Mixamo clips are captured side-on), and the distance between the two
# hands is printed per frame (two-handed grip check).
#   "D:/Blender/blender.exe" -b AnimationTools/khalid_cutscene_anims.blend --python AnimationTools/melee/preview_candidates.py -- OUTDIR [FRAMES] name1 name2 ...
# (names = files in AnimationTools/mixamo without .fbx). Writes OUTDIR/<name>_<view>_<i>.png
import bpy, sys, os, math
from mathutils import Vector

TOOLS = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/AnimationTools/"
args = sys.argv[sys.argv.index("--") + 1:]
OUT = args[0]
FRAMES = int(args[1]) if len(args) > 1 and args[1].isdigit() else 10
NAMES = args[2:] if len(args) > 1 and args[1].isdigit() else args[1:]

exec(open(TOOLS + "mocap_retarget.py").read(), globals())

sc = bpy.context.scene
for o in bpy.data.objects:
    if o.type == 'MESH' and o.name != "SM_MainCharacter": o.hide_render = True
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'
sc.display.shading.color_type = 'TEXTURE'
sc.render.resolution_x, sc.render.resolution_y, sc.render.resolution_percentage = 260, 340, 100
cam = bpy.data.objects["mx_cam"]
sc.camera = cam
cam.data.lens = 50
rig = bpy.data.objects["rig"]


def look(pos, target):
    cam.location = pos
    cam.rotation_euler = (Vector(target) - Vector(pos)).to_track_quat('-Z', 'Y').to_euler()


def bone_world(name):
    return rig.matrix_world @ rig.pose.bones[name].head


for name in NAMES:
    bpy.ops.import_scene.fbx(filepath=TOOLS + "mixamo/" + name + ".fbx")
    arm = [o for o in bpy.context.selected_objects if o.type == 'ARMATURE'][0]
    arm.name = "PV_" + name
    for o in bpy.context.selected_objects: o.hide_render = True
    info = retarget(arm.name, "PV_" + name)
    f0, f1 = info["frames"]
    rig.animation_data.action = bpy.data.actions["PV_" + name]
    sc.frame_set(f0)
    # facing on the first frame: perpendicular to the hip line (left thigh -> right thigh), horizontal
    side = bone_world("c_thigh_fk.r") - bone_world("c_thigh_fk.l"); side.z = 0
    fwd = Vector((0, 0, 1)).cross(side).normalized()
    right = side.normalized()
    dists = []
    for i in range(FRAMES):
        f = f0 + round((f1 - f0) * i / (FRAMES - 1))
        sc.frame_set(f)
        dists.append("%d:%.2f" % (f, (bone_world("hand.l") - bone_world("hand.r")).length))
        hips = bone_world("c_root_master.x") if "c_root_master.x" in rig.pose.bones else Vector((0, 0, 1))
        c = Vector((hips.x, hips.y, 1.05))
        for view, off in (("side", right * 2.8 + Vector((0, 0, 0.2))), ("front", fwd * 2.6 + right * 1.0 + Vector((0, 0, 0.3)))):
            look(c + off, c)
            sc.render.filepath = os.path.join(OUT, "%s_%s_%02d.png" % (name, view, i))
            bpy.ops.render.render(write_still=True)
    print("PREVIEW_DONE", name, f0, f1, "fwd", tuple(round(v, 2) for v in fwd), "hand gap", " ".join(dists))
    bpy.data.objects.remove(arm, do_unlink=True)
