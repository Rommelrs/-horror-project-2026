# two-handed hold candidates: per sampled frame, the fists' midpoint and the hand line (left -> right wrist) in the
# CHEST frame (x = his right, y = forward, z = up) - picking a stance where the pipe rests over the right shoulder
import bpy, math
from mathutils import Vector, Matrix
TOOLS = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/AnimationTools/"
sc = bpy.context.scene
for n in ("BB_Idle", "Club2H_Combo", "Heavy_Swing", "GS_BlockIdle", "GS_Walk", "GS_SlashPower"):
    bpy.ops.import_scene.fbx(filepath=TOOLS + "mixamo/" + n + ".fbx")
    arm = [o for o in bpy.context.selected_objects if o.type == 'ARMATURE'][0]
    act = arm.animation_data.action
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    pb = {b.name.split(":")[-1]: b for b in arm.pose.bones}
    mw = arm.matrix_world
    out = []
    for f in sorted(set([f0, f0 + 5, f0 + 10, f0 + 20, f1])):
        sc.frame_set(f)
        P = lambda k: mw @ pb[k].head
        right = (P("RightShoulder") - P("LeftShoulder")); right.z = 0; right.normalize()
        up = Vector((0, 0, 1)); fwd = up.cross(right)
        chest = (P("RightShoulder") + P("LeftShoulder")) / 2
        def local(v): return Vector((v.dot(right), v.dot(fwd), v.dot(up)))
        mid = local((P("RightHand") + P("LeftHand")) / 2 - chest)
        line = local((P("RightHand") - P("LeftHand")).normalized())
        out.append("f%d mid(r%.2f f%.2f u%.2f) line(r%.2f f%.2f u%.2f) gap %.2f" % (f, mid.x, mid.y, mid.z, line.x, line.y, line.z, (P("RightHand") - P("LeftHand")).length))
    print("HOLD", n, " | ".join(out))
    for o in list(bpy.context.selected_objects): bpy.data.objects.remove(o, do_unlink=True)
