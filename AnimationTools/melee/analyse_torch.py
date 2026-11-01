# prints per-clip info for the Mixamo axe clips: frames, hips travel, right-hand speed (strike timing), hand vs shoulder
import bpy, sys
TOOLS = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/AnimationTools/"
names = ["Torch_Attack_01", "Torch_Idle", "Torch_Walk_Forward"]
sc = bpy.context.scene
for n in names:
    bpy.ops.import_scene.fbx(filepath=TOOLS + "mixamo/" + n + ".fbx")
    arm = [o for o in bpy.context.selected_objects if o.type == 'ARMATURE'][0]
    act = arm.animation_data.action
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    pb = {b.name.split(":")[-1]: b for b in arm.pose.bones}
    mw = arm.matrix_world
    rows = []
    prev = None
    for f in range(f0, f1 + 1):
        sc.frame_set(f)
        hips = mw @ pb["Hips"].head
        rh = mw @ pb["RightHand"].head
        rs = mw @ pb["RightShoulder"].head
        lh = mw @ pb["LeftHand"].head
        sp = (rh - prev).length * 30 if prev is not None else 0
        prev = rh.copy()
        rows.append((f, hips.copy(), rh.copy(), rs.copy(), sp, lh.copy()))
    print("CLIP", n, f0, f1, "hips start", tuple(round(v, 2) for v in rows[0][1]), "end", tuple(round(v, 2) for v in rows[-1][1]))
    step = 1 if "Attack" in n or "Equip" in n else 6
    for (f, hips, rh, rs, sp, lh) in rows[::step]:
        d = rh - rs
        print("   f%3d hips(%.2f,%.2f,%.2f) RH-shoulder(x%.2f y%.2f z%.2f) speed %.2f LH(%.2f,%.2f,%.2f)" % (f, hips.x, hips.y, hips.z, d.x, d.y, d.z, sp, lh.x, lh.y, lh.z))
    for o in list(bpy.context.selected_objects): bpy.data.objects.remove(o, do_unlink=True)
