# Background Blender (khalid_cutscene_anims.blend, NOT saved): Mixamo "Left Turn 90" / "Right Turn 90" (AnimationTools/mixamo/
# Left_Turn_90.fbx, Right_Turn_90.fbx) -> Khalid_TurnLeft90 / Khalid_TurnRight90, exported to Assets/_TempKhalidClips/
# (Unity: KhalidClipImporter.ImportAll("Assets/MainCharacter", both names)).
#
# In the game PlayerMovement turns his transform itself and drives the clip by the animator float TurnPhase = degrees turned / one
# cycle, so a tap plays a bit of the clip and holding plays it round and round. For that the clip must NOT turn the body itself
# (it would turn twice), and its time must run with the turn, not the clock:
#   1. the mocap's own body yaw (measured from the hip line) is taken out of every frame, turning the whole skeleton back about the
#      vertical through the origin - what's left is the stepping feet and the body's twist relative to the turn;
#   2. the clip is re-timed so output frame k is the moment the mocap had turned k/N of its total, so TurnPhase lines up with it.
# Run: "D:/Blender/blender.exe" -b AnimationTools/khalid_cutscene_anims.blend --python AnimationTools/turn_build.py
import bpy, os, math
from mathutils import Matrix, Vector

TOOLS = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/AnimationTools/"
OUT = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/Assets/_TempKhalidClips/"
N = 30                                    # output frames 1..N+1 (frame N+1 = the full turn = frame 1 again)
CLIPS = {"Khalid_TurnLeft90": "Left_Turn_90", "Khalid_TurnRight90": "Right_Turn_90"}

exec(open(TOOLS + "mocap_retarget.py").read(), globals())
exec(open(TOOLS + "blender_helpers.py").read(), globals())
exec(bpy.app.driver_namespace["export_code"], globals())


def depth(arm, n):
    d, pb = 0, arm.pose.bones[n]
    while pb.parent: pb = pb.parent; d += 1
    return d


def hip_yaw(arm):
    l = arm.matrix_world @ arm.pose.bones[MX + "LeftUpLeg"].head
    r = arm.matrix_world @ arm.pose.bones[MX + "RightUpLeg"].head
    v = r - l
    return math.atan2(v.y, v.x)


def build(out_name, src_name):
    sc = bpy.context.scene
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=TOOLS + "mixamo/" + src_name + ".fbx")
    arm = [o for o in bpy.data.objects if o not in before and o.type == 'ARMATURE'][0]
    src = arm.animation_data.action
    f0, f1 = int(src.frame_range[0]), int(src.frame_range[1])

    # body yaw per source frame, unwrapped, relative to the first frame
    yaw = []
    for f in range(f0, f1 + 1):
        sc.frame_set(f)
        a = hip_yaw(arm)
        if yaw:
            while a - yaw[-1] > math.pi: a -= 2 * math.pi
            while a - yaw[-1] < -math.pi: a += 2 * math.pi
        yaw.append(a)
    yaw = [a - yaw[0] for a in yaw]
    total = yaw[-1]
    sgn = 1.0 if total >= 0 else -1.0
    mono = []                             # running max of the turn so far (ignores the small wobbles back)
    for a in yaw: mono.append(max(mono[-1], a * sgn) if mono else a * sgn)

    def time_at(target):                  # first fractional source frame where the turn reaches target (unsigned)
        for i in range(1, len(mono)):
            if mono[i] >= target:
                lo, hi = mono[i - 1], mono[i]
                return f0 + i - 1 + ((target - lo) / (hi - lo) if hi > lo else 0.0)
        return float(f1)

    order = sorted((pb.name for pb in arm.pose.bones), key=lambda n: depth(arm, n))
    W, Wi = arm.matrix_world.copy(), arm.matrix_world.inverted()
    poses, times = [], []
    for k in range(N + 1):
        t = time_at(abs(total) * k / N)
        fi = int(math.floor(t)); sub = t - fi
        if fi >= f1: fi, sub = f1, 0.0
        sc.frame_set(fi, subframe=sub)
        bpy.context.view_layer.update()
        cur = hip_yaw(arm)
        # yaw of this pose relative to frame f0, unwrapped near the expected value
        exp = sgn * abs(total) * k / N
        rel = cur - first_abs[out_name]
        while rel - exp > math.pi: rel -= 2 * math.pi
        while rel - exp < -math.pi: rel += 2 * math.pi
        R = Matrix.Rotation(-rel, 4, 'Z')
        poses.append({n: Wi @ R @ W @ arm.pose.bones[n].matrix for n in order})
        times.append(round(t, 2))

    act = bpy.data.actions.new(arm.name + "_stripped")
    arm.animation_data.action = act
    levels = sorted(set(depth(arm, n) for n in order))
    for i, pose in enumerate(poses):
        f = i + 1
        sc.frame_set(f)
        for d in levels:
            for n in order:
                if depth(arm, n) == d: arm.pose.bones[n].matrix = pose[n]
            bpy.context.view_layer.update()
        for pb in arm.pose.bones:
            for prop in ("location", "rotation_quaternion", "scale"): pb.keyframe_insert(prop, frame=f)
    act.use_frame_range = True; act.frame_start = 1; act.frame_end = N + 1

    info = retarget(arm.name, out_name)
    out = bpy.data.actions[out_name]
    cb = out.layers[0].strips[0].channelbag(out.slots[0])
    for fc in cb.fcurves:
        for m in list(fc.modifiers): fc.modifiers.remove(m)
        fc.update()
    out.use_frame_range = True; out.frame_start = 1; out.frame_end = N + 1
    os.makedirs(OUT, exist_ok=True)
    export_action(out_name, OUT + out_name + ".fbx")
    print("TURN_DONE", out_name, "src frames", f0, f1, "total yaw deg %.1f" % math.degrees(total),
          "source times", times[0], times[N // 2], times[-1], "size", os.path.getsize(OUT + out_name + ".fbx"))
    for o in [o for o in bpy.data.objects if o not in before]: bpy.data.objects.remove(o, do_unlink=True)


first_abs = {}
for out_name, src_name in CLIPS.items():
    # absolute hip yaw at the first source frame (needed inside build for the per-pose strip)
    sc = bpy.context.scene
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=TOOLS + "mixamo/" + src_name + ".fbx")
    a = [o for o in bpy.data.objects if o not in before and o.type == 'ARMATURE'][0]
    sc.frame_set(int(a.animation_data.action.frame_range[0]))
    first_abs[out_name] = hip_yaw(a)
    for o in [o for o in bpy.data.objects if o not in before]: bpy.data.objects.remove(o, do_unlink=True)
    build(out_name, src_name)
