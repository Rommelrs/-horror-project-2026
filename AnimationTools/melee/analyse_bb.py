# per-frame facing (shoulder line), hands midpoint speed and gap for the baseball clips (picking contact / cut frames)
import bpy, math
from mathutils import Vector
TOOLS = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/AnimationTools/"
sc = bpy.context.scene
for n in ("BB_Idle", "BB_HitSingle"):
    bpy.ops.import_scene.fbx(filepath=TOOLS + "mixamo/" + n + ".fbx")
    arm = [o for o in bpy.context.selected_objects if o.type == 'ARMATURE'][0]
    act = arm.animation_data.action
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    pb = {b.name.split(":")[-1]: b for b in arm.pose.bones}
    mw = arm.matrix_world
    prev = None
    print("CLIP", n, f0, f1)
    for f in range(f0, f1 + 1):
        sc.frame_set(f)
        P = lambda k: mw @ pb[k].head
        r = P("RightShoulder") - P("LeftShoulder"); r.z = 0
        fwd = Vector((0, 0, 1)).cross(r).normalized()
        hr = Vector((0, 0, 1)).cross(P("RightUpLeg") - P("LeftUpLeg")); hr.z = 0; hr.normalize()
        mid = (P("RightHand") + P("LeftHand")) / 2
        sp = (mid - prev).length * 30 if prev is not None else 0
        prev = mid.copy()
        rel = mid - P("Hips")
        print("  f%3d chestYaw %6.1f hipYaw %6.1f handsMid(rel hips) %5.2f %5.2f %5.2f  speed %5.2f gap %.2f hips %.2f %.2f" % (
            f, math.degrees(math.atan2(fwd.x, -fwd.y)), math.degrees(math.atan2(hr.x, -hr.y)), rel.x, rel.y, rel.z, sp,
            (P("RightHand") - P("LeftHand")).length, P("Hips").x, P("Hips").y))
    for o in list(bpy.context.selected_objects): bpy.data.objects.remove(o, do_unlink=True)
