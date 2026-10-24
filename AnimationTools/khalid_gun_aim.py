# Khalid's cutscene gun clips, built on the Mixamo "Pistol Idle - Idle With Aimed Pistol" mocap (mixamo/Pistol_Idle_Aimed.fbx):
#   Khalid_GunRaise  idle (gun hanging at his side) -> two-handed aim; the right foot steps back into the bladed stance
#   Khalid_GunAim    the mocap aim loop, untouched apart from the stance offset (left foot stays where it stood in idle)
#   Khalid_GunLower  aim -> idle, the right foot steps back up beside the left
# Run in Blender (khalid_cutscene_anims.blend) after exec(mocap_retarget.py) and importing the FBX as armature "MX_Pistol_Idle_Aimed":
#   exec(open(TOOLS + "mocap_retarget.py").read()); exec(open(TOOLS + "khalid_gun_aim.py").read()); build_all()
# Poses are handled per body segment (world rotation of each segment + hip position, the same model mocap_retarget uses), so
# blends never stretch a bone; the legs are re-solved with a two-bone IK so the planted foot stays put and the stepping foot
# lifts and lands.
import bpy, math
from mathutils import Matrix, Vector, Quaternion

AIM_ARM = "MX_Pistol_Idle_Aimed"
IDLE_FRAME = 484                     # Khalid's idle stand pose (start of the idle action)
RAISE_FRAMES, LOWER_FRAMES = 24, 34  # 0.8 s up (tense), 1.13 s down (relaxing)
STEP_LIFT = 0.07                     # stepping foot clearance at mid-swing (m)
# aim corrections measured in Unity with the real gun (sight line vs the coward at the C1 mark, 2.2 m away): the whole
# stance turns about the front foot, both arms tip about the shoulder line (keeps the two-handed grip intact)
AIM_YAW_DEG = -7.0
AIM_PITCH_DEG = 10.0                 # + = gun points lower
UP = Vector((0, 0, 1))

def _rest_k0(rig):
    K0 = {}
    for key, (kb, kj, kt, mb, mt) in SEGS.items():
        K0[key] = (_kpt(rig, "c_thigh_fk.l") + _kpt(rig, "c_thigh_fk.r")) / 2 if key == "pelvis" else _kpt(rig, kj)
    return K0

def _from_M(M, K0):
    P = {key: M[SEGS[key][0][0]].to_quaternion() for key in ORDER}
    P["_hip"] = M[SEGS["pelvis"][0][0]] @ K0["pelvis"]
    return P

def capture_khalid(rig, action, frame, K0):
    sc = bpy.context.scene; ad = rig.animation_data
    ad.use_nla = False; ad.action = bpy.data.actions[action]
    sl = [s for s in ad.action.slots if s.name_display == "rig"]
    if sl: ad.action_slot = sl[0]
    sc.frame_set(frame); bpy.context.view_layer.update()
    M = {pb.name: pb.matrix @ rig.data.bones[pb.name].matrix_local.inverted() for pb in rig.pose.bones}
    ad.action = None
    return _from_M(M, K0)

def capture_mixamo(rig, arm, frame, prep, K0):
    bpy.context.scene.frame_set(frame)
    return _from_M(pose_frame(arm, rig, prep), K0)

def build_M(P, K0):
    M = {}
    for key in ORDER:
        kb = SEGS[key][0]; k0 = K0[key]
        R = P[key].to_matrix().to_4x4()
        newp = P["_hip"] if key == "pelvis" else M[PARENT[key]] @ k0
        Mk = Matrix.Translation(newp) @ R @ Matrix.Translation(-k0)
        M[key] = Mk
        for b in kb: M[b] = Mk
        if key in ("l_5", "r_5"):
            q = Quaternion().slerp(P[key], TWIST_FOLLOW).to_matrix().to_4x4()
            Mt = Matrix.Translation(newp) @ q @ Matrix.Translation(-k0)
            for b in ("arm_twist." + key[0], "c_arm_twist_offset." + key[0]): M[b] = Mt
    return M

def joints(P, K0, s):
    M = build_M(P, K0)
    return M["pelvis"] @ K0[s + "_0"], M[s + "_0"] @ K0[s + "_1"], M[s + "_1"] @ K0[s + "_2"]

def arm_joints(P, K0, s):
    M = build_M(P, K0)
    return M[s + "_4"] @ K0[s + "_5"], M[s + "_5"] @ K0[s + "_6"], M[s + "_6"] @ K0[s + "_7"]

def arm_ik(P, K0, s, target):
    # same two-bone solve for upper arm + forearm (wrist on `target`, elbow in its current bend plane, hand keeps its rotation)
    # elbows point down and a little out (Khalid faces -Y, his left is +X)
    H, Kn, A = arm_joints(P, K0, s)
    _solve(P, s + "_5", s + "_6", H, Kn, A, target, Vector((0.6 if s == "l" else -0.6, 0, -1)), force_pole=True)

def leg_ik(P, K0, s, target):
    # two-bone IK: thigh + shin put the ankle on `target`, bending in the plane the knee already bends in; foot keeps its rotation
    H, Kn, A = joints(P, K0, s)
    _solve(P, s + "_0", s + "_1", H, Kn, A, target, Vector((0, -1, 0)))

def _solve(P, sa, sb, H, Kn, A, target, fallback, force_pole=False):
    a, b = (Kn - H).length, (A - Kn).length
    d = target - H
    L = min(max(d.length, abs(a - b) + 1e-4), a + b - 1e-4)
    dn = d.normalized()
    pole = (Kn - H) - dn * (Kn - H).dot(dn)
    if force_pole or pole.length < 1e-6: pole = fallback - dn * dn.dot(fallback)
    pole.normalize()
    cosA = (a * a + L * L - b * b) / (2 * a * L)
    Kn2 = H + dn * (a * cosA) + pole * (a * math.sqrt(max(0.0, 1 - cosA * cosA)))
    A2 = H + dn * L
    q1 = (Kn - H).rotation_difference(Kn2 - H)
    P[sa] = q1 @ P[sa]
    q2 = (q1 @ (A - Kn)).rotation_difference(A2 - Kn2)
    P[sb] = q2 @ q1 @ P[sb]

def blend(Pa, Pb, w_upper, w_lower):
    P = {}
    lower = {"pelvis"} | {"%s_%d" % (s, k) for s in "lr" for k in range(4)}
    for key in ORDER:
        w = w_lower if key in lower else w_upper
        P[key] = Pa[key].slerp(Pb[key], w)
    P["_hip"] = Pa["_hip"].lerp(Pb["_hip"], w_lower)
    return P

def shifted(P, off):
    Q = dict(P); Q["_hip"] = P["_hip"] + off; return Q

def turned(P, pivot, yaw_deg):
    R = Quaternion(UP, math.radians(yaw_deg))
    Q = {k: (R @ v if k != "_hip" else v) for k, v in P.items()}
    Q["_hip"] = pivot + R @ (P["_hip"] - pivot)
    return Q

def arms_tipped(P, deg):
    sl, sr = arm_joints(P, K0G, "l")[0], arm_joints(P, K0G, "r")[0]
    R = Quaternion((sl - sr).normalized(), math.radians(deg))
    Q = dict(P)
    for s in "lr":
        for k in range(5, 14): Q["%s_%d" % (s, k)] = R @ P["%s_%d" % (s, k)]
    return Q

def smooth(x): x = min(max(x, 0.0), 1.0); return x * x * (3 - 2 * x)

def bake(rig, name, poses, cyclic=False):
    ad = rig.animation_data or rig.animation_data_create(); ad.use_nla = False
    if name in bpy.data.actions: bpy.data.actions.remove(bpy.data.actions[name])
    act = bpy.data.actions.new(name); act.use_fake_user = True
    prev = {}
    for i, P in enumerate(poses):
        ad.action = None
        apply_pose(rig, build_M(P, K0G))
        for pb in rig.pose.bones:
            q = pb.rotation_quaternion
            if pb.name in prev and q.dot(prev[pb.name]) < 0: pb.rotation_quaternion = (-q.w, -q.x, -q.y, -q.z)
            prev[pb.name] = pb.rotation_quaternion.copy()
        ad.action = act
        for pb in rig.pose.bones:
            for prop in ("location", "rotation_quaternion", "scale"): pb.keyframe_insert(prop, frame=1 + i, group=pb.name)
    if cyclic:
        cb = act.layers[0].strips[0].channelbag(act.slots[0])
        for fc in cb.fcurves: fc.modifiers.new('CYCLES'); fc.update()
    ad.action = None
    return act

def step(Pa, Pb, n, planted, stepping, lift_t0, lift_t1, upper_lead, aimP, mid_fwd, mid_drop):
    # n+1 poses from Pa to Pb; `planted` foot stays on its Pa spot (sliding to its Pb spot only if they differ), the other steps.
    # The hands travel through a point in front of the body (mid_fwd ahead of the shoulders, mid_drop below them) instead of
    # swinging on straight arms: up into the chest then pressed out when raising, down to a low ready then dropped when lowering.
    Ha = {s: joints(Pa, K0G, s)[2] for s in "lr"}; Hb = {s: joints(Pb, K0G, s)[2] for s in "lr"}
    Wa = {s: arm_joints(Pa, K0G, s)[2] - Pa["_hip"] for s in "lr"}; Wb = {s: arm_joints(Pb, K0G, s)[2] - Pb["_hip"] for s in "lr"}
    aw = {s: arm_joints(aimP, K0G, s)[2] for s in "lr"}; awm = (aw["l"] + aw["r"]) / 2
    ash = (arm_joints(aimP, K0G, "l")[0] + arm_joints(aimP, K0G, "r")[0]) / 2
    fwd = (awm - ash); fwd.z = 0; fwd.normalize()
    out = []
    for i in range(n + 1):
        t = i / n
        wu = smooth(t / upper_lead) if upper_lead < 1 else smooth(t)
        wl = smooth(t)
        P = blend(Pa, Pb, wu, wl)
        leg_ik(P, K0G, planted, Ha[planted].lerp(Hb[planted], wl))
        u = smooth((t - lift_t0) / (lift_t1 - lift_t0))
        tgt = Ha[stepping].lerp(Hb[stepping], u) + UP * STEP_LIFT * math.sin(math.pi * u)
        leg_ik(P, K0G, stepping, tgt)
        env = math.sin(math.pi * wu)
        if env > 1e-3:
            sh = (arm_joints(P, K0G, "l")[0] + arm_joints(P, K0G, "r")[0]) / 2
            mid = sh + fwd * mid_fwd - UP * mid_drop
            for s in "lr":
                p0, p2 = P["_hip"] + Wa[s], P["_hip"] + Wb[s]
                p1 = mid + (aw[s] - awm)
                c = 2 * p1 - (p0 + p2) / 2                       # quadratic Bezier through p1 at its middle
                tgt = p0 * (1 - wu) ** 2 + c * 2 * wu * (1 - wu) + p2 * wu ** 2
                Q = dict(P); arm_ik(Q, K0G, s, tgt)
                for k in (s + "_5", s + "_6"): P[k] = P[k].slerp(Q[k], env)
        out.append(P)
    return out

def build_all():
    global K0G
    rig = bpy.data.objects["rig"]; arm = bpy.data.objects[AIM_ARM]; sc = bpy.context.scene
    ad = rig.animation_data; saved = (ad.action, ad.action_slot, ad.use_nla, sc.frame_current)
    K0G = _rest_k0(rig)
    prep = prepare(arm, rig)
    idle = capture_khalid(rig, "idle", IDLE_FRAME, K0G)
    f0, f1 = map(int, arm.animation_data.action.frame_range)
    aim = [capture_mixamo(rig, arm, f, prep, K0G) for f in range(f0, f1 + 1)]
    # stance offset: the aim's front (left) foot lands where it stood in idle
    off = joints(idle, K0G, "l")[2] - joints(aim[0], K0G, "l")[2]; off.z = 0
    aim = [shifted(P, off) for P in aim]
    pivot = joints(aim[0], K0G, "l")[2]
    aim = [arms_tipped(turned(P, pivot, AIM_YAW_DEG), AIM_PITCH_DEG) for P in aim]
    for P in aim: leg_ik(P, K0G, "l", joints(aim[0], K0G, "l")[2])      # keep the front foot nailed through the loop's sway
    raise_ = step(idle, aim[0], RAISE_FRAMES, "l", "r", 0.12, 0.8, 0.85, aim[0], 0.28, 0.2)
    lower = step(aim[0], idle, LOWER_FRAMES, "l", "r", 0.25, 0.9, 1.0, aim[0], 0.3, 0.5)
    bake(rig, "Khalid_GunRaise", raise_)
    bake(rig, "Khalid_GunAim", aim, cyclic=True)               # last mocap frame == first, so Unity's loop is seamless
    bake(rig, "Khalid_GunLower", lower)
    ad.action = saved[0]
    if saved[1] is not None: ad.action_slot = saved[1]
    ad.use_nla = saved[2]; sc.frame_set(saved[3])
    rs = joints(raise_[-1], K0G, "r")[2] - joints(idle, K0G, "r")[2]
    return {"offset": [round(c, 3) for c in off], "right_step": [round(c, 3) for c in rs], "aim_frames": len(aim) - 1}
