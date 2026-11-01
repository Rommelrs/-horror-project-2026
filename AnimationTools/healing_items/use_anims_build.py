# Background Blender (khalid_cutscene_anims.blend, NOT saved): retarget Mixamo mocap onto Khalid for the quick-heal
# animations and export them to Assets/_TempKhalidClips/ (Unity: KhalidClipImporter.ImportAll("Assets/MainCharacter")).
#   painkiller - Mixamo "Drinking", hand to mouth and back, MIRRORED (the mocap uses the left hand; Khalid uses his right,
#                the gun hand - the gun is hidden while he uses an item)          -> Khalid_UsePainkiller
#   medkit     - an injection into the left shoulder, cut from Mixamo "Shoulder Rubbing" (mirrored so the RIGHT hand, holding the
#                syringe, does it): reach, press and HOLD at the shoulder (no rubbing), lower             -> Khalid_UseMedKit
#   reload     - pistol reload, Mixamo "Gun Reload While Walking"                 -> Khalid_Reload_Pistol
#   analyse    - print hand-to-mouth / hand-to-shoulder distances per frame to pick the ranges
#   "D:/Blender/blender.exe" -b AnimationTools/khalid_cutscene_anims.blend --python AnimationTools/healing_items/use_anims_build.py -- painkiller
import bpy, os, sys
from mathutils import Matrix

TOOLS = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/AnimationTools/"
OUT = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/Assets/_TempKhalidClips/"
MODE = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "analyse"

def injection_timeline():
    """source frames of Shoulder Rubbing, one per output frame (30 fps): reach (16-36), quick move onto the press point
    (36-56), hold the press there ~0.8 s while the plunger goes in, step to where the mocap still has the hand on the
    shoulder (90-96), lower (96-118). From the analyse pass: hand closest to the shoulder (0.13 m) at 56-90."""
    t = list(range(16, 37))
    t += list(range(38, 57, 2))
    t += [56] * 24
    t += list(range(90, 97))
    t += list(range(97, 119))
    return t


CLIPS = {
    # mode: (fbx, armature name, action name, source frame for each output frame, mirror)
    "painkiller": ("mixamo/Drinking.fbx", "MX_Drinking", "Khalid_UsePainkiller", list(range(55, 176)), True),
    "medkit": ("mixamo/Shoulder_Rubbing.fbx", "MX_Shoulder", "Khalid_UseMedKit", injection_timeline(), True),
    # pistol reload (Mixamo "Gun Reload While Walking": gun in the right hand at the chest, left hand fetches a magazine
    # from the hip and slaps it in) - replaces the old Reload clip on the Shooting layer (upper-body mask, he walks)
    # exported IN PLACE: ARP hangs the arms / hands / head off c_traj, so forward travel in the clip tore them away from
    # the (masked, movement-layer) hips - arms stretched metres ahead, head detached
    "reload": ("mixamo/Gun_Reload_InPlace.fbx", "MX_Reload", "Khalid_Reload_Pistol", list(range(1, 110)), False),
}


def import_mixamo(path, name):
    bpy.ops.import_scene.fbx(filepath=TOOLS + path)
    arm = [o for o in bpy.context.selected_objects if o.type == 'ARMATURE'][0]
    arm.name = name
    return arm


def analyse():
    sc = bpy.context.scene
    for path, name in (("mixamo/Drinking.fbx", "MX_A"), ("mixamo/Shoulder_Rubbing.fbx", "MX_B")):
        arm = import_mixamo(path, name)
        act = arm.animation_data.action
        f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
        pb = {b.name.split(":")[-1]: b for b in arm.pose.bones}
        rows = []
        for f in range(f0, f1 + 1, 5):
            sc.frame_set(f)
            mw = arm.matrix_world
            head = mw @ pb["Head"].head
            lh, rh = mw @ pb["LeftHand"].head, mw @ pb["RightHand"].head
            rs = mw @ pb["RightShoulder"].head
            rows.append("%d L-mouth %.2f R-mouth %.2f L-Rshoulder %.2f" % (f, (lh - head).length, (rh - head).length, (lh - rs).length))
        print("ANALYSE", path, f0, f1)
        for r in rows: print("  ", r)


CALM_BONES = ("Hips", "Spine", "Spine1", "Spine2", "Neck", "Head")


def calm_source(arm, window, bones=CALM_BONES):
    """Moving-average the torso / head channels of the source mocap over 'window' frames: removes the walk-cycle sway, bob
    and head wobble of a walking mocap while keeping slow intent (leaning in, looking down at the gun). Quaternions are
    sign-aligned before averaging and renormalised after. Arms / hands are left alone - that's the actual motion."""
    act = arm.animation_data.action
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    frames = list(range(f0, f1 + 1))
    half = window // 2
    for pb in arm.pose.bones:
        short = pb.name.split(":")[-1]
        if short not in bones: continue
        for prop, size in (("rotation_quaternion", 4), ("location", 3)):
            path = 'pose.bones["%s"].%s' % (pb.name, prop)
            fcs = [act.fcurves.find(path, index=i) for i in range(size)] if hasattr(act, "fcurves") else None
            if fcs is None:
                cb = act.layers[0].strips[0].channelbag(act.slots[0])
                fcs = [cb.fcurves.find(path, index=i) for i in range(size)]
            if any(fc is None for fc in fcs): continue
            vals = [[fc.evaluate(f) for fc in fcs] for f in frames]
            if prop == "rotation_quaternion":
                for i in range(1, len(vals)):
                    if sum(a * b for a, b in zip(vals[i], vals[i - 1])) < 0: vals[i] = [-v for v in vals[i]]
            out = []
            for i in range(len(vals)):
                lo, hi = max(0, i - half), min(len(vals), i + half + 1)
                avg = [sum(vals[j][k] for j in range(lo, hi)) / (hi - lo) for k in range(size)]
                if prop == "rotation_quaternion":
                    n = sum(v * v for v in avg) ** 0.5 or 1.0
                    avg = [v / n for v in avg]
                out.append(avg)
            for k, fc in enumerate(fcs):
                fc.keyframe_points.clear()
                fc.keyframe_points.add(len(frames))
                for i, f in enumerate(frames):
                    kp = fc.keyframe_points[i]
                    kp.co = (f, out[i][k]); kp.interpolation = 'LINEAR'
                fc.update()
        print("CALMED", short)


def resample_action(arm, timeline, mirror):
    """new action keyed at frames 1..len(timeline), each frame taking the pose of its source frame - mirrored left <-> right
    if asked: every bone takes its partner's armature-space pose reflected across the body's centre plane,
    M' = S * M_partner * S, S = diag(-1, 1, 1) (armature-space X is left-right; the Mixamo rest pose is symmetric)"""
    sc = bpy.context.scene
    S = Matrix.Diagonal((-1.0, 1.0, 1.0, 1.0))

    def partner(n):
        if not mirror: return n
        if "Left" in n: return n.replace("Left", "Right")
        if "Right" in n: return n.replace("Right", "Left")
        return n

    def depth(pb):
        d = 0
        while pb.parent: pb = pb.parent; d += 1
        return d

    frames = {}
    for f in sorted(set(timeline)):
        sc.frame_set(f)
        frames[f] = {pb.name: pb.matrix.copy() for pb in arm.pose.bones}
    out = bpy.data.actions.new(arm.name + "_Resampled")
    arm.animation_data.action = out
    bones = sorted(arm.pose.bones, key=depth)
    for i, src in enumerate(timeline):
        f = i + 1
        sc.frame_set(f)
        for d in sorted(set(depth(pb) for pb in bones)):
            for pb in bones:
                if depth(pb) != d: continue
                m = frames[src][partner(pb.name)]
                pb.matrix = S @ m @ S if mirror else m.copy()
            bpy.context.view_layer.update()
        for pb in bones:
            pb.keyframe_insert("location", frame=f); pb.keyframe_insert("rotation_quaternion", frame=f); pb.keyframe_insert("scale", frame=f)
    return out


def build(mode):
    path, name, out_name, timeline, mirror = CLIPS[mode]
    exec(open(TOOLS + "mocap_retarget.py").read(), globals())
    exec(open(TOOLS + "blender_helpers.py").read(), globals())
    exec(bpy.app.driver_namespace["export_code"], globals())

    arm = import_mixamo(path, name)
    if mode == "reload":
        calm_source(arm, 31)   # ~1 s at 30 fps: one full walk step pair averages out
    else:
        # heal clips: only the swallow bobs / fidgets go (~0.5 s), the big intended moves stay - tipping the head back
        # to drink, bowing over the injection
        calm_source(arm, 15, ("Spine", "Spine1", "Spine2", "Neck", "Head"))
    src = resample_action(arm, timeline, mirror)
    f0, f1 = 1, len(timeline)
    src.use_frame_range = True; src.frame_start = f0; src.frame_end = f1

    info = retarget(name, out_name)
    act = bpy.data.actions[out_name]
    cb = act.layers[0].strips[0].channelbag(act.slots[0])
    for fc in cb.fcurves:                      # start at frame 1, no cycling
        for m in list(fc.modifiers): fc.modifiers.remove(m)
        for kp in fc.keyframe_points:
            kp.co.x -= f0 - 1; kp.handle_left.x -= f0 - 1; kp.handle_right.x -= f0 - 1
        fc.update()
    act.use_frame_range = True; act.frame_start = 1; act.frame_end = f1 - f0 + 1
    os.makedirs(OUT, exist_ok=True)
    export_action(out_name, OUT + out_name + ".fbx")
    print("USE_ANIM_DONE", out_name, info["frames"], act.frame_range[:], os.path.getsize(OUT + out_name + ".fbx"))


if MODE == "analyse":
    analyse()
else:
    build(MODE)
