# The Questioner spec moveset clips (keyed poses on QuestionerRig, built on top of his own idle / walk so the posture matches).
#   blender -b TheQuestioner_v2.blend --python q20_moves.py -- [preview DIR] [only clip,clip]
# Writes Assets/Models/TheQuestioner/TheQuestioner_Moves.fbx (armature only, one take per clip; same bone paths as the model
# FBX, so the generic clips drive TheQuestionerModel's Animator directly).
# Rig space: front = -Y, up = +Z, his left = +X. Rotations below are about rig axes through the bone's head:
#   +X on an upright bone = lean FORWARD, -X = back;  +Z turns him to HIS LEFT, -Z to his right.
# Timing matches QuestionerTuning (30 fps):
#   Q_ScreamWindup 24 f (0.8 s) | Q_Scream 30 f (1.0) | Q_ScreamRecover 18 f (0.6)
#   Q_GrabWindup 18 f (0.6) | Q_GrabRush loop (Walk1 legs, leaning, arms out) | Q_GrabHold loop 45 f | Q_Bite 18 f | Q_PushedOff 30 f
#   Q_Swipe 42 f: wind-up to 12 (0.4 s), right claw hits at 12, left at 21 (0.7 s), recovered by 42 (1.4 s)
#   Q_StaggerDown 15 f | Q_StaggerLoop 45 f | Q_StaggerUp 24 f | Q_DeathKneel 36 f
import bpy, math, sys, os, random
from mathutils import Vector, Matrix, Quaternion

ROOT = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/"
OUT_FBX = ROOT + "Assets/Models/TheQuestioner/TheQuestioner_Moves.fbx"
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PREVIEW = argv[argv.index("preview") + 1] if "preview" in argv else None
ONLY = set(argv[argv.index("only") + 1].split(",")) if "only" in argv else None

rig = bpy.data.objects["QuestionerRig"]
P = rig.pose.bones
B = rig.data.bones
scene = bpy.context.scene
scene.render.fps = 30
X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
FWD = Vector((0, -1, 0))
rng = random.Random(7)
for _t in (rig.animation_data.nla_tracks if rig.animation_data else []): _t.mute = True


def ev():
    bpy.context.view_layer.update()


def set_action(act):
    ad = rig.animation_data or rig.animation_data_create()
    ad.action = act
    if act is not None:
        try:
            if ad.action_slot is None and len(act.slots): ad.action_slot = act.slots[0]
        except Exception:
            pass


def capture(action, frame):
    """local (basis) matrices of every bone at a frame of one of his clips"""
    ad = rig.animation_data
    saved = [(t, t.mute) for t in ad.nla_tracks]
    for t, _ in saved: t.mute = True
    set_action(bpy.data.actions[action])
    scene.frame_set(frame)
    ev()
    basis = {pb.name: pb.matrix_basis.copy() for pb in P}
    set_action(None)
    for t, m in saved: t.mute = m
    return basis


def apply(basis):
    for n, m in basis.items():
        P[n].matrix_basis = m
    ev()


# ---------------- posing helpers (rig space) ----------------

def rot(n, axis, deg, pivot=None):
    pb = P[n]
    M = pb.matrix.copy()
    h = M.translation.copy() if pivot is None else pivot
    T = Matrix.Translation(h) @ Matrix.Rotation(math.radians(deg), 4, axis) @ Matrix.Translation(-h)
    pb.matrix = T @ M
    ev()


def move(n, delta):
    pb = P[n]
    M = pb.matrix.copy()
    M.translation = M.translation + delta
    pb.matrix = M
    ev()


def aim(n, d, twist_ref=None):
    """swing bone n so its Y axis points along d (smallest rotation)"""
    pb = P[n]
    M = pb.matrix.copy()
    y = M.to_3x3().col[1].normalized()
    q = y.rotation_difference(d.normalized())
    h = M.translation.copy()
    pb.matrix = Matrix.Translation(h) @ q.to_matrix().to_4x4() @ Matrix.Translation(-h) @ M
    ev()


def orient(n, ydir, axis_local, axis_world):
    """bone n: local Y along ydir, then roll about it so local axis `axis_local` (Vector) points as near axis_world as possible"""
    aim(n, ydir)
    pb = P[n]
    M = pb.matrix.copy()
    R = M.to_3x3().normalized()
    y = R.col[1].normalized()
    a = (R @ axis_local).normalized()
    w = axis_world - y * axis_world.dot(y)
    if w.length < 1e-5: return
    w.normalize()
    a = a - y * a.dot(y); a.normalize()
    ang = math.atan2(a.cross(w).dot(y), a.dot(w))
    h = M.translation.copy()
    pb.matrix = Matrix.Translation(h) @ Matrix.Rotation(ang, 4, y) @ Matrix.Translation(-h) @ M
    ev()


def ik(upper, lower, end, target, pole):
    """two-bone IK: end bone's head onto target, the middle joint bending toward the pole point"""
    a = P[upper].head.copy()
    l1 = (P[lower].head - P[upper].head).length
    l2 = (P[end].head - P[lower].head).length
    d = target - a
    dist = max(min(d.length, (l1 + l2) * 0.998), abs(l1 - l2) + 1e-3)
    dn = d.normalized()
    cosA = max(-1.0, min(1.0, (l1 * l1 + dist * dist - l2 * l2) / (2 * l1 * dist)))
    A = math.acos(cosA)
    pv = pole - a
    pv = pv - dn * pv.dot(dn)
    if pv.length < 1e-5: pv = FWD.copy()
    pv.normalize()
    elbow = a + dn * math.cos(A) * l1 + pv * math.sin(A) * l1
    aim(upper, elbow - a)
    aim(lower, (a + dn * dist) - P[lower].head)


def side_x(s): return 1.0 if s == "Left" else -1.0


# palm normal (out of the palm) in the hand's local axes: at rest the palms face the thighs
PALM = {}
for s in ("Left", "Right"):
    R = B[s + "Hand"].matrix_local.to_3x3()
    inward = Vector((-side_x(s), 0, 0))
    best = max((Vector(v) for v in ((1, 0, 0), (-1, 0, 0), (0, 0, 1), (0, 0, -1))), key=lambda v: (R @ v).dot(inward))
    PALM[s] = best


def hand_to(s, wrist, fingers, palm_world, pole):
    ik(s + "Arm", s + "ForeArm", s + "Hand", wrist, pole)
    orient(s + "Hand", fingers, PALM[s], palm_world)


def head_front():
    return (P["Head"].matrix.to_3x3() @ Vector((0, 0, 1))).normalized()


def mouth():
    j = P["Jaw"]
    return j.head.lerp(j.tail, 0.45) + head_front() * 0.05


def hands_over_mouth(press=0.0):
    m = mouth()
    f = head_front()
    up = Z.copy()
    left = (up.cross(f)).normalized() * -1.0        # his left (+X when facing -Y)
    if left.dot(X) < 0: left = -left
    # right hand flat over the mouth, left hand over it
    hand_to("Right", m + f * (0.05 - press) - left * 0.085 + up * 0.0, (left + up * 0.25).normalized(), -f,
            P["RightArm"].head + Vector((-0.35, -0.15, -0.55)))
    hand_to("Left", m + f * (0.09 - press) + left * 0.085 - up * 0.025, (-left + up * 0.15).normalized(), -f,
            P["LeftArm"].head + Vector((0.35, -0.15, -0.55)))


def kneel(depth=1.0, lean=0.0):
    """knees on the ground, shins back, feet flat behind (depth 0 = his base legs, 1 = kneeling)"""
    if depth <= 0: return
    hip0 = P["Hips"].head.copy()
    target_hip_z = 0.56
    move("Hips", Vector((0, -0.04 * depth, (target_hip_z - hip0.z) * depth)))
    for s in ("Left", "Right"):
        sx = side_x(s)
        knee = Vector((0.13 * sx, -0.22, 0.09))
        ankle = Vector((0.14 * sx, 0.27, 0.11))
        a0 = P[s + "Foot"].head.copy()
        k0 = P[s + "Leg"].head.copy()
        k = k0.lerp(knee, depth)
        an = a0.lerp(ankle, depth)
        ik(s + "UpLeg", s + "Leg", s + "Foot", an, k + FWD * 0.5)
        # foot lies flat behind, toes down
        aim(s + "Foot", Vector((0.0, 0.9, -0.42)).lerp((P[s + "ToeBase"].head - P[s + "Foot"].head), 1 - depth))


def build(name, keys, loop=False):
    """keys: list of (frame, base_basis, pose_fn). Each key = base pose, then pose_fn applied. Poses are made with no action
    assigned (so nothing re-evaluates over them), then written straight into the new action's f-curves."""
    if ONLY and name not in ONLY: return None
    poses = []
    for frame, base, fn in keys:
        apply(base)
        if fn: fn()
        poses.append((frame, {pb.name: (pb.rotation_quaternion.copy(), pb.location.copy()) for pb in P}))
    old = bpy.data.actions.get(name)
    if old: bpy.data.actions.remove(old)
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    set_action(act)
    for pb in P:
        path = 'pose.bones["%s"]' % pb.name
        prev = None
        qs = []
        for frame, pose in poses:
            q = pose[pb.name][0].copy()
            if prev is not None and prev.dot(q) < 0: q.negate()
            prev = q
            qs.append((frame, q))
        for i in range(4):
            fc = act.fcurve_ensure_for_datablock(rig, path + ".rotation_quaternion", index=i, group_name=pb.name)
            for frame, q in qs:
                fc.keyframe_points.insert(frame, q[i], options={"FAST"})
        if pb.name in ("Root", "Hips"):
            for i in range(3):
                fc = act.fcurve_ensure_for_datablock(rig, path + ".location", index=i, group_name=pb.name)
                for frame, pose in poses:
                    fc.keyframe_points.insert(frame, pose[pb.name][1][i], options={"FAST"})
    set_action(None)
    for fc in iter_fcurves(act):
        for kp in fc.keyframe_points:
            kp.interpolation = "BEZIER"
            kp.handle_left_type = kp.handle_right_type = "AUTO_CLAMPED"
        fc.update()
    if loop:
        act["loop"] = True
    print("BUILT", name, tuple(act.frame_range))
    return act


def iter_fcurves(act):
    try:
        for layer in act.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for fc in bag.fcurves:
                        yield fc
    except AttributeError:
        for fc in act.fcurves:
            yield fc


# ---------------- the clips ----------------

IDLE = capture("Questioner_Idle", 1)
IDLE2 = capture("Questioner_Idle", 40)


def seq(*fns):
    def f():
        for g in fns: g()
    return f


def arms_to(k, lw, rw, fingers_l, fingers_r, palm_l, palm_r):
    """blend both wrists from where they are toward targets (k 0..1), hands turned to fingers / palm directions"""
    if k <= 0: return
    for s, w, fg, pm in (("Left", lw, fingers_l, palm_l), ("Right", rw, fingers_r, palm_r)):
        sx = side_x(s)
        t = P[s + "Hand"].head.lerp(Vector(w), k)
        hand_to(s, t, Vector(fg).normalized(), Vector(pm).normalized(), P[s + "Arm"].head + Vector((0.55 * sx, 0.35, -0.2)))


def scream_inhale(k=1.0):
    rot("Spine1", X, -5 * k); rot("Spine2", X, -9 * k)
    rot("Neck", X, -8 * k); rot("Head", X, -14 * k)
    for s in ("Left", "Right"):
        rot(s + "Shoulder", Y, 9 * k * side_x(s))       # shoulders lift with the breath
    arms_to(k, (0.36, 0.02, 1.02), (-0.36, 0.02, 1.02), (0.15, 0.1, -1), (-0.15, 0.1, -1), (-1, 0, 0), (1, 0, 0))


def scream_push(k=1.0, tremble=0.0):
    scream_inhale(1.0 - k)
    rot("Spine", X, 4 * k); rot("Spine1", X, 7 * k); rot("Spine2", X, 11 * k)
    rot("Neck", X, 22 * k); rot("Head", X, 4 * k)
    # arms flung back and out, fingers splayed down
    arms_to(k, (0.50, 0.24, 1.10), (-0.50, 0.24, 1.10), (0.35, 0.45, -1), (-0.35, 0.45, -1), (0.2, -1, 0.1), (-0.2, -1, 0.1))
    if tremble:
        for n in ("Spine2", "Neck", "Head"):
            rot(n, X, rng.uniform(-1, 1) * tremble)
            rot(n, Z, rng.uniform(-1, 1) * tremble * 0.7)


def scream_clips():
    windup = [(0, IDLE, None), (10, IDLE, lambda: scream_inhale(0.55)), (20, IDLE, lambda: scream_inhale(1.0)), (24, IDLE, lambda: scream_inhale(1.04))]
    build("Q_ScreamWindup", windup)
    act = [(0, IDLE, lambda: scream_inhale(1.04)), (4, IDLE, lambda: scream_push(1.0))]
    for f in range(6, 29, 2):
        act.append((f, IDLE, (lambda: scream_push(1.0, 1.6))))
    act.append((30, IDLE, lambda: scream_push(1.0)))
    build("Q_Scream", act)
    build("Q_ScreamRecover", [(0, IDLE, lambda: scream_push(1.0)), (8, IDLE, lambda: scream_push(0.4)),
                              (18, IDLE, lambda: (rot("Spine2", X, 3), [rot(s + "Shoulder", Y, -5 * side_x(s)) for s in ("Left", "Right")]))])


def grab_reach(k=1.0, hx=0.24, fwd=0.52, hz=1.36, hunch=1.0):
    rot("Spine", X, 12 * hunch); rot("Spine1", X, 10 * hunch); rot("Spine2", X, 6 * hunch)
    rot("Neck", X, -6 * hunch); rot("Head", X, -6 * hunch)
    if k <= 0: return
    for s in ("Left", "Right"):
        sx = side_x(s)
        sh = P[s + "Arm"].head
        w0 = P[s + "Hand"].head.copy()
        w = w0.lerp(Vector((hx * sx, -fwd, hz)), k)
        hand_to(s, w, (FWD + Z * 0.15 - X * sx * 0.25).normalized(), (-X * sx * 0.6 + Z * -0.3 + FWD * 0.2).normalized(),
                sh + Vector((0.45 * sx, 0.1, -0.35)))


def grab_clips():
    build("Q_GrabWindup", [(0, IDLE, None), (8, IDLE, lambda: grab_reach(0.5, hunch=0.8)), (15, IDLE, lambda: grab_reach(1.0, hunch=1.2)),
                           (18, IDLE, lambda: grab_reach(1.0, hz=1.42, hunch=1.25))])
    # rush: his faster walk's legs + leaning hunch, arms out in front
    walk = bpy.data.actions["Questioner_Walk1"]
    f0, f1 = int(walk.frame_range[0]), int(walk.frame_range[1])
    keys = []
    for f in range(f0, f1 + 1, 3):
        b = capture("Questioner_Walk1", f)
        ph = (f - f0) / (f1 - f0) * 2 * math.pi * 2
        def pose(ph=ph):
            rot("Hips", X, 8)
            grab_reach(1.0, hx=0.30, fwd=0.80, hz=1.24 + 0.03 * math.sin(ph), hunch=1.5)
        keys.append((f - f0, b, pose))
    if (f1 - f0) % 3:
        keys.append((f1 - f0, capture("Questioner_Walk1", f1), lambda: (rot("Hips", X, 8), grab_reach(1.0, hx=0.30, fwd=0.80, hz=1.24, hunch=1.5))))
    build("Q_GrabRush", keys, loop=True)

    def hold(pull=0.0, lean=1.0):
        rot("Spine", X, 4 * lean); rot("Spine1", X, 5 * lean); rot("Spine2", X, 4 * lean)
        rot("Neck", X, 12 * lean); rot("Head", X, 8 * lean)
        for s in ("Left", "Right"):
            sx = side_x(s)
            hand_to(s, Vector((0.12 * sx, -0.54 + pull, 1.36)), (FWD * 0.3 - X * sx + Z * 0.2).normalized(), (-X * sx * 0.2 + FWD).normalized(),
                    P[s + "Arm"].head + Vector((0.5 * sx, 0.0, -0.4)))
    build("Q_GrabHold", [(0, IDLE, hold), (10, IDLE, lambda: hold(0.05, 1.05)), (16, IDLE, lambda: hold(0.0, 1.0)),
                         (28, IDLE, lambda: hold(0.06, 1.08)), (34, IDLE, hold), (45, IDLE, hold)], loop=True)

    def bite(k):
        hold(0.04 * k, 1.0 + 0.35 * k)
        rot("Spine2", X, 6 * k); rot("Neck", X, 14 * k)
    def release():
        rot("Spine1", X, 6); rot("Spine2", X, 4)
        for s in ("Left", "Right"):
            rot(s + "Arm", Y, 12 * side_x(s)); rot(s + "Arm", X, -10)
    build("Q_Bite", [(0, IDLE, hold), (4, IDLE, lambda: bite(1.0)), (8, IDLE, lambda: bite(0.9)), (18, IDLE, release)])

    def thrown(k, arms=1.0):
        rot("Spine", X, -10 * k); rot("Spine1", X, -10 * k); rot("Spine2", X, -6 * k)
        rot("Neck", X, -6 * k); rot("Head", X, -10 * k)
        # arms thrown out to the sides, flailing for balance
        arms_to(arms, (0.58, 0.02, 1.38), (-0.56, 0.08, 1.30), (0.7, 0.2, 0.5), (-0.7, 0.2, 0.3), (0, -1, -0.3), (0, -1, -0.2))
    build("Q_PushedOff", [(0, IDLE, hold), (5, IDLE, lambda: thrown(1.0)), (12, IDLE, lambda: thrown(0.7, 0.8)),
                          (20, IDLE, lambda: (rot("Spine1", X, 8), rot("Spine2", X, 6))), (30, IDLE, lambda: rot("Spine1", X, 3))])


def claw(s, w, fingers, palm, twist=0.0, bend=0.0):
    rot("Spine", Z, twist * 0.4); rot("Spine1", Z, twist * 0.35); rot("Spine2", Z, twist * 0.25)
    rot("Spine1", X, bend)
    sx = side_x(s)
    hand_to(s, Vector(w), Vector(fingers).normalized(), Vector(palm).normalized(), P[s + "Arm"].head + Vector((0.5 * sx, 0.2, -0.3)))


def swipe_clips():
    R, L = "Right", "Left"
    def cockR():
        claw(R, (-0.42, 0.18, 1.92), (-0.2, 0.3, 1.0), (0.3, -1, 0), twist=-22, bend=-4)
        claw(L, (0.30, -0.35, 1.15), (0.2, -1, 0.2), (-1, 0, 0), twist=0)
    def hitR():
        claw(R, (-0.12, -0.72, 1.45), (0.6, -0.8, -0.1), (0.0, -0.3, -1), twist=0, bend=4)
        claw(L, (0.38, -0.15, 1.35), (0.3, -0.4, 0.8), (-1, 0, 0))
    def followR_cockL():
        claw(R, (0.38, -0.45, 0.98), (0.7, -0.3, -0.6), (0, 0, -1), twist=24, bend=10)
        claw(L, (0.44, 0.15, 1.88), (0.2, 0.3, 1.0), (-0.3, -1, 0))
    def hitL():
        claw(L, (0.10, -0.72, 1.45), (-0.6, -0.8, -0.1), (0.0, -0.3, -1), twist=0, bend=6)
        claw(R, (0.05, -0.40, 1.05), (0.6, -0.4, -0.4), (0, 0, -1))
    def followL():
        claw(L, (-0.38, -0.45, 0.98), (-0.7, -0.3, -0.6), (0, 0, -1), twist=-22, bend=12)
        claw(R, (-0.30, -0.25, 1.05), (-0.2, -0.6, -0.4), (1, 0, 0))
    def settle():
        rot("Spine1", X, 7); rot("Spine2", X, 3)
    build("Q_Swipe", [(0, IDLE, None), (6, IDLE, lambda: (cockR(),)), (11, IDLE, cockR), (13, IDLE, hitR), (16, IDLE, followR_cockL),
                      (19, IDLE, followR_cockL), (22, IDLE, hitL), (25, IDLE, followL), (32, IDLE, lambda: (followL(), rot("Spine1", X, -4))),
                      (42, IDLE, settle)])


def stagger_clips():
    apply(IDLE)
    STAND = {sd: P[sd + "Foot"].head.copy() for sd in ("Left", "Right")}
    HIP_STAND = P["Hips"].head.copy()
    FOOT_FWD = Vector((0.0, -0.93, -0.37))

    def legs(hip, la, ra, l_flat, r_flat, lean=0.0):
        move("Hips", Vector(hip) - P["Hips"].head)
        rot("Spine", X, lean)
        for sd, a, flat in (("Left", la, l_flat), ("Right", ra, r_flat)):
            ik(sd + "UpLeg", sd + "Leg", sd + "Foot", Vector(a), P[sd + "Leg"].head + FWD * 0.6 + Z * 0.2)
            aim(sd + "Foot", Vector((0.0, 0.9, -0.42)) if flat else FOOT_FWD)

    def kneeling(k=1.0, rock=0.0, curl=1.0, hands=1.0):
        kneel(k)
        rot("Spine", X, (14 + rock) * curl * k); rot("Spine1", X, (10 + rock * 0.6) * curl * k); rot("Spine2", X, 6 * curl * k)
        rot("Neck", X, 14 * curl * k); rot("Head", X, 12 * curl * k)
        if hands > 0: hands_over_mouth()
    build("Q_StaggerDown", [(0, IDLE, None), (4, IDLE, lambda: (legs((HIP_STAND.x, HIP_STAND.y - 0.04, HIP_STAND.z - 0.2), tuple(STAND["Left"]), tuple(STAND["Right"]), False, False, 8),
                                                              rot("Spine1", X, 10), rot("Spine2", X, 8), arms_to(0.5, (0.2, -0.3, 1.45), (-0.2, -0.3, 1.45), (0, -0.3, 1), (0, -0.3, 1), (0, 1, 0), (0, 1, 0)))),
                            (8, IDLE, lambda: kneeling(1.0, curl=1.3)), (11, IDLE, lambda: kneeling(1.0, curl=1.15)), (15, IDLE, lambda: kneeling(1.0))])
    loop = []
    for i, f in enumerate(range(0, 46, 5)):
        r = 5 * math.sin(f / 45 * 2 * math.pi * 2)
        loop.append((f, IDLE, (lambda r=r: kneeling(1.0, rock=r))))
    build("Q_StaggerLoop", loop, loop=True)

    KL, KR = (0.14, 0.27, 0.11), (-0.14, 0.27, 0.11)
    def rise_keys():
        hs = HIP_STAND
        return [
            (0, IDLE, lambda: kneeling(1.0)),
            (5, IDLE, lambda: (kneeling(1.0), legs((0, -0.06, 0.60), KL, (-0.15, -0.02, 0.26), True, False, 6))),
            (9, IDLE, lambda: (legs((0, -0.06, 0.62), KL, (-0.15, -0.33, 0.09), True, False, 22), hands_over_mouth())),
            (14, IDLE, lambda: legs((0, -0.10, 0.84), (0.14, 0.12, 0.16), (-0.15, -0.33, 0.09), False, False, 26)),
            (19, IDLE, lambda: legs((hs.x, hs.y - 0.06, hs.z - 0.06), tuple(STAND["Left"]), (-0.15, -0.20, 0.09), False, False, 10)),
            (24, IDLE, None)]
    build("Q_StaggerUp", rise_keys())

    def fall(k):
        kneeling(1.0, hands=0.0)
        knees = (P["LeftLeg"].head + P["RightLeg"].head) * 0.5
        base = {pb.name: pb.matrix_basis.copy() for pb in P}
        # tip forward about the knees until his face is just above the ground (the curl would push it through)
        lo, hi = 0.0, 95.0
        for _ in range(14):
            mid = (lo + hi) * 0.5
            apply(base); rot("Hips", X, mid, pivot=knees)
            if P["Head"].head.z > 0.20: lo = mid
            else: hi = mid
        apply(base)
        rot("Hips", X, lo * k, pivot=knees)
        kneel_fix_legs()
        hands_over_mouth()
    build("Q_DeathKneel", [(0, IDLE, lambda: kneeling(1.0)), (10, IDLE, lambda: fall(0.25)), (22, IDLE, lambda: fall(0.85)),
                           (26, IDLE, lambda: fall(1.03)), (30, IDLE, lambda: fall(0.98)), (36, IDLE, lambda: fall(1.0))])


def kneel_fix_legs():
    # after tipping the hips about the knees, put the shins / feet back on the ground behind
    for s in ("Left", "Right"):
        sx = side_x(s)
        ik(s + "UpLeg", s + "Leg", s + "Foot", Vector((0.14 * sx, 0.27, 0.11)), P[s + "Leg"].head + FWD * 0.5 + Z * -0.3)
        aim(s + "Foot", Vector((0.0, 0.9, -0.42)))


scream_clips()
grab_clips()
swipe_clips()
stagger_clips()
apply(IDLE)

NAMES = ["Q_ScreamWindup", "Q_Scream", "Q_ScreamRecover", "Q_GrabWindup", "Q_GrabRush", "Q_GrabHold", "Q_Bite", "Q_PushedOff",
         "Q_Swipe", "Q_StaggerDown", "Q_StaggerLoop", "Q_StaggerUp", "Q_DeathKneel"]


# ---------------- preview (workbench renders: front + side, a few frames per clip) ----------------

def preview(dirpath, names):
    os.makedirs(dirpath, exist_ok=True)
    mesh = bpy.data.objects["Q_Mesh"]
    for o in bpy.data.objects:
        if o.type == "MESH": o.hide_render = o is not mesh
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.render.resolution_x, scene.render.resolution_y = 270, 360
    scene.render.film_transparent = False
    cam_data = bpy.data.cameras.new("PrevCam")
    cam = bpy.data.objects.new("PrevCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam_data.lens = 62
    ad = rig.animation_data
    for t in ad.nla_tracks: t.mute = True
    views = {"front": Vector((1.0, -4.0, 1.25)), "side": Vector((4.1, -0.6, 1.25))}
    for n in names:
        act = bpy.data.actions.get(n)
        if act is None: continue
        set_action(act)
        f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
        keyed = sorted(set(int(k.co[0]) for fc in iter_fcurves(act) for k in fc.keyframe_points))
        frames = keyed if len(keyed) <= 8 else sorted(set([keyed[int(i * (len(keyed) - 1) / 7)] for i in range(8)]))
        for f in frames:
            scene.frame_set(f)
            for vn, vp in views.items():
                cam.location = vp
                d = Vector((0, -0.2, 1.0)) - vp
                cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
                scene.render.filepath = os.path.join(dirpath, "%s_%03d_%s.png" % (n, f, vn))
                bpy.ops.render.render(write_still=True)
    set_action(None)


if PREVIEW:
    preview(PREVIEW, [n for n in NAMES if not ONLY or n in ONLY])
else:
    # export the move clips only (armature, NLA strip per clip)
    ad = rig.animation_data
    saved = [(t, t.mute) for t in ad.nla_tracks]
    for t in list(ad.nla_tracks): ad.nla_tracks.remove(t)
    for n in NAMES:
        act = bpy.data.actions[n]
        tr = ad.nla_tracks.new(); tr.name = n
        st = tr.strips.new(n, int(act.frame_range[0]), act); st.name = n
        try: st.action_slot = act.slots[0]
        except Exception: pass
    rig.data.pose_position = "POSE"
    # a sibling next to the armature: with the armature alone in the file Unity makes it the file root, the clip paths lose
    # "QuestionerRig/" and its root rotation lands on the model's root object (he lay flat)
    dummy = bpy.data.objects.new("MovesDummy", None)
    scene.collection.objects.link(dummy)
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    dummy.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=OUT_FBX, use_selection=True, object_types={"ARMATURE", "EMPTY"},
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             add_leaf_bones=False, use_armature_deform_only=False, armature_nodetype="NULL",
                             bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=True,
                             bake_anim_use_all_bones=True, bake_anim_force_startend_keying=True, bake_anim_step=1.0,
                             bake_anim_simplify_factor=0.0)
    print("EXPORTED", OUT_FBX)
