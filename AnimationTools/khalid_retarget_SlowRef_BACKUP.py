import bpy, math, json
from mathutils import Matrix, Vector
ns = bpy.app.driver_namespace
S = ns["S_stand"]; rig = bpy.data.objects["rig"]
BASE = "C:/Users/Zayed/AppData/Local/Temp/claude/D--Unity-Stuff-NEWHorrorProject--horror-project-2026/cf51bab8-e583-42ee-8ab4-15af6ac1a2cc/scratchpad/"
D = math.radians
def T(v): return Matrix.Translation(v)
def Rot(p, rx=0.0, ry=0.0, rz=0.0):
    R = Matrix.Rotation(rz, 4, 'Z') @ Matrix.Rotation(ry, 4, 'Y') @ Matrix.Rotation(rx, 4, 'X')
    return T(p) @ R @ T(-p)
def P(n): return S[n].translation.copy()
def depth(pb):
    n = 0
    while pb.parent: pb = pb.parent; n += 1
    return n
def sm(x): x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)

# ------------------------------------------------------------ settings (the "slight adjustments")
FPS = 30.0
KEYS = 21                         # key poses per cycle -> 0.70 s (171 steps/min, ref is 164)
SD = 4                            # dense samples per key pose
N = KEYS * SD
CYCLE = KEYS / FPS
V = 2.727                         # ground speed (BU/s) = 3 m/s game sprint / 1.1 import scale
FOOT_X = 0.115
BOB_SCALE = 0.85                  # reference bounces a lot; keep most of it
HEAD_LEVEL = 0.20                 # fraction of the reference head tilt that is kept
LEAN_SCALE = 0.85
ELBOW_OUT = 0.45                  # the reference flares the elbows out; Khalid keeps them closer to his sides
HAND_IN = 0.55
YAW_SCALE = 0.75                  # reference twists the chest a lot
HIP_LIFT = 0.02                   # slightly less crouched than the reference
Z0 = 0.084

REF = json.load(open(BASE + "ref_dump.json"))
RF = {int(k): v for k, v in REF["frames"].items()}
NREF = 22                          # frames 1..22 are the cycle (23 == 1)
def rp(n, j, tail=False): return Vector(RF[j + 1][n][1 if tail else 0])
def rrest(n): return Vector(REF["rest"][n])
def cr(p0, p1, p2, p3, t):
    return 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3)
def resample(series):               # periodic Catmull-Rom, NREF -> N samples
    out = []
    for i in range(N):
        u = i * NREF / N; j = int(math.floor(u)); t = u - j
        out.append(cr(series[(j - 1) % NREF], series[j % NREF], series[(j + 1) % NREF], series[(j + 2) % NREF], t))
    return out
JN = ["Hips", "Spine", "Spine1", "Spine2", "Neck", "Head", "LeftArm", "LeftForeArm", "LeftHand", "RightArm", "RightForeArm", "RightHand",
      "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase", "LeftToe_End", "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase", "RightToe_End"]
J = {n: resample([rp(n, j) for j in range(NREF)]) for n in JN}      # joint head positions, dense

# ------------------------------------------------------------ scale / placement
H0L, K0L, A0L = P("c_thigh_fk.l"), P("leg_fk.l"), P("foot.l")
L1, L2 = (K0L - H0L).length, (A0L - K0L).length
LTOT = L1 + L2
Hc = (P("c_thigh_fk.l") + P("c_thigh_fk.r")) / 2
rl = ((rp("LeftUpLeg", 0) - rp("LeftLeg", 0)).length + (rp("LeftLeg", 0) - rp("LeftFoot", 0)).length)
ref_flat = min(v.z for v in J["LeftFoot"])
SC = (LTOT + Z0) / (rl + ref_flat)
OFFZ = Z0 - SC * ref_flat
hcr = [(J["LeftUpLeg"][i] + J["RightUpLeg"][i]) / 2 for i in range(N)]
XC = sum(v.x for v in hcr) / N; YC = sum(v.y for v in hcr) / N
def W(p): return Vector((SC * (p.x - XC), SC * (p.y - YC) + Hc.y, SC * p.z + OFFZ))

def foot_ang(side, i):
    a = J[side + "Foot"][i]; b = J[side + "ToeBase"][i]
    v = b - a; return math.atan2(v.z, -v.y)
def toe_ang(side, i):
    a = J[side + "ToeBase"][i]; b = J[side + "Toe_End"][i]
    v = b - a; return math.atan2(v.z, -v.y)
flat_ang = {}
for s in ("Left", "Right"):
    zz = [W(v).z for v in J[s + "Foot"]]
    fl = [foot_ang(s, i) for i in range(N) if zz[i] - Z0 <= 0.004]
    flat_ang[s] = sum(fl) / len(fl)


# ------------------------------------------------------------ feet: scaled reference paths, with a perfectly steady ground speed while the foot is flat
# stance is built from ground-locked pivots (heel -> flat -> ball) so the foot cannot slide; the swing keeps the reference path
HEEL = (0.085, 0.084); BALL = (-0.143, 0.084)
def R2(v, a): return (v[0] * math.cos(a) - v[1] * math.sin(a), v[0] * math.sin(a) + v[1] * math.cos(a))
_hip = [W(hcr[i]) for i in range(N)]
_an = [W(J["LeftFoot"][i]) for i in range(N)]
_toe = [W(J["LeftToeBase"][i]) for i in range(N)]
_rel = [_hip[i].y - _an[i].y for i in range(N)]                  # ankle ahead of the pelvis (+ = forward)
TD = min(range(N), key=lambda i: -_rel[i])                        # touchdown: ankle at its most forward point
_k = [(TD + k) % N for k in range(N)]
TO = next(k for k in range(N - 1, 0, -1) if _toe[_k[k]].z <= 0.006)                  # toe-off: last sample the toe is on the floor
FS = next(k for k in range(N) if _an[_k[k]].z - Z0 <= 0.004)                          # flat foot begins
FE = max(k for k in range(N) if _an[_k[k]].z - Z0 <= 0.004 and k <= TO)               # flat foot ends
DUTY = TO / N; HS = FS / N; HO = FE / N
FA = _rel[TD]
HS_ANG = foot_ang("Left", TD) - flat_ang["Left"]
TOFF = -(foot_ang("Left", _k[TO]) - flat_ang["Left"])
STEP = V * CYCLE / 2.0
def stance(ph):
    F = FA - 2 * STEP * ph
    if ph < HS:
        th = HS_ANG * (1 - sm(ph / HS)); h = FA - HEEL[0] - 2 * STEP * ph
        o = R2(HEEL, th); return (h + o[0], o[1], th)
    if ph < HO: return (F, Z0, 0.0)
    th = -TOFF * sm((ph - HO) / (DUTY - HO)); b = FA + 0.143 - 2 * STEP * ph
    o = R2(BALL, th); return (b + o[0], o[1], th)
def sample_left(ph):
    """(ankle ahead of pelvis, ankle height, foot pitch, toe flex) of the left foot at cycle phase ph measured from touchdown"""
    ph %= 1.0
    if ph < DUTY:
        f, z, th = stance(ph); return f, z, th, (-0.9 * th if th < 0 else 0.0)
    L = 1.0 - DUTY; s = (ph - DUTY) / L
    i = (TD + int(round(ph * N))) % N
    ref = (_rel[i], _an[i].z, foot_ang("Left", i) - flat_ang["Left"], toe_ang("Left", i) - foot_ang("Left", i))
    e = stance(DUTY); st = stance(0.0)
    r0 = (_rel[_k[TO]], _an[_k[TO]].z, foot_ang("Left", _k[TO]) - flat_ang["Left"], toe_ang("Left", _k[TO]) - foot_ang("Left", _k[TO]))
    r1 = (_rel[TD], _an[TD].z, foot_ang("Left", TD) - flat_ang["Left"], toe_ang("Left", TD) - foot_ang("Left", TD))
    s0 = (e[0], e[1], e[2], (-0.9 * e[2] if e[2] < 0 else 0.0)); s1 = (st[0], st[1], st[2], 0.0)
    w0 = (1 - sm(s)) ** 2; w1 = sm(s) ** 2
    return tuple(ref[j] + (s0[j] - r0[j]) * w0 + (s1[j] - r1[j]) * w1 for j in range(4))
FPY = {"Left": [], "Right": []}; FPZ = {"Left": [], "Right": []}; FPP = {"Left": [], "Right": []}; FPT = {"Left": [], "Right": []}
for i in range(N):
    for s, off in (("Left", 0.0), ("Right", 0.5)):
        f, z, p, t = sample_left(i / N - TD / N + 0.0 - off)
        FPY[s].append(Hc.y - f); FPP[s].append(p); FPT[s].append(t)
        zok = max(0.084 * math.cos(p) - 0.143 * math.sin(p), 0.084 * math.cos(p) - 0.143 * math.sin(p) - 0.073 * math.sin(p + t)) + 0.002
        FPZ[s].append(max(z, zok))
FP = {s: (FPY[s], FPZ[s]) for s in ("Left", "Right")}

def seg_pitch(a, b):               # forward lean of segment a->b, forward = -Y, from vertical
    v = b - a; return math.atan2(-v.y, v.z)
def line_yaw(l, r): v = l - r; return math.atan2(v.y, v.x)
def line_roll(l, r): v = l - r; return -math.atan2(v.z, v.x)           # rotation about Y that reproduces the slope
def seg_dir(a, b): return (b - a).normalized()
RS = lambda n: rrest(n)
rest_p = {"Hips": seg_pitch(RS("Hips"), RS("Spine")), "Spine": seg_pitch(RS("Spine"), RS("Spine1")), "Spine1": seg_pitch(RS("Spine1"), RS("Spine2")),
          "Spine2": seg_pitch(RS("Spine2"), RS("Neck")), "Neck": seg_pitch(RS("Neck"), RS("Head"))}
rest_hipyaw = line_yaw(RS("LeftUpLeg"), RS("RightUpLeg")); rest_hiproll = line_roll(RS("LeftUpLeg"), RS("RightUpLeg"))
rest_shyaw = line_yaw(RS("LeftArm"), RS("RightArm")); rest_shroll = line_roll(RS("LeftArm"), RS("RightArm"))
# Khalid's own rest pose values so that "delta" rotations land on his rest posture
def kseg(a, b, base=0.0):
    v = P(b) - P(a); return math.atan2(-v.y, v.z)
def ik2(Hs, A, l1, l2):
    d = (A - Hs).length; ratio = d / (l1 + l2); d = min(d, l1 + l2 - 1e-4)
    u = (A - Hs).normalized(); a = (d * d + l1 * l1 - l2 * l2) / (2 * d); h = math.sqrt(max(0.0, l1 * l1 - a * a))
    perp = Vector((-u[1], u[0]))
    if perp[0] > 0: perp = -perp
    return Hs + u * a + perp * h, ratio

# ------------------------------------------------------------ pelvis height: reference bob (slightly reduced) but never beyond what Khalid's legs can reach
HZ = [W(hcr[i]).z for i in range(N)]
mz = sum(HZ) / N
HZ = [mz + HIP_LIFT + BOB_SCALE * (z - mz) for z in HZ]
HY = [Hc.y for i in range(N)]
HX = [SC * (hcr[i].x - XC) for i in range(N)]
REACH = 0.985
for i in range(N):
    lim = 9.0
    for s in ("Left", "Right"):
        ay, az = FP[s][0][i], FP[s][1][i]
        dy = ay - HY[i]
        lim = min(lim, az + math.sqrt(max((REACH * LTOT) ** 2 - dy * dy, 1e-6)))
    HZ[i] = min(HZ[i], lim)
for _ in range(3):                  # light smoothing (periodic) of the hip height, then re-limit
    HZ = [0.25 * HZ[i - 1] + 0.5 * HZ[i] + 0.25 * HZ[(i + 1) % N] for i in range(N)]
    for i in range(N):
        lim = 9.0
        for s in ("Left", "Right"):
            dy = FP[s][0][i] - HY[i]
            lim = min(lim, FP[s][1][i] + math.sqrt(max((REACH * LTOT) ** 2 - dy * dy, 1e-6)))
        HZ[i] = min(HZ[i], lim)

def build(idx):
    i = idx % N
    M = {}
    Hk = Vector((HX[i], HY[i], HZ[i]))
    dp = Hk - Hc
    # pelvis / spine / head rotations as deltas from the reference's rest posture
    pit = lambda a, b, r: (seg_pitch(J[a][i], J[b][i]) - r)
    w_h = pit("Hips", "Spine", rest_p["Hips"]) * LEAN_SCALE
    w_s = pit("Spine", "Spine1", rest_p["Spine"]) * LEAN_SCALE
    w_s1 = pit("Spine1", "Spine2", rest_p["Spine1"]) * LEAN_SCALE
    w_s2 = pit("Spine2", "Neck", rest_p["Spine2"]) * LEAN_SCALE
    w_n = pit("Neck", "Head", rest_p["Neck"])
    p_h, p_s, p_s1, p_s2 = w_h, w_s - w_h, w_s1 - w_s, w_s2 - w_s1       # relative to the parent bone
    p_n = HEAD_LEVEL * (w_n - w_s2 / LEAN_SCALE)                         # neck+head tilt relative to the chest
    yaw_h = line_yaw(J["LeftUpLeg"][i], J["RightUpLeg"][i]) - rest_hipyaw
    roll_h = line_roll(J["LeftUpLeg"][i], J["RightUpLeg"][i]) - rest_hiproll
    yaw_c = line_yaw(J["LeftArm"][i], J["RightArm"][i]) - rest_shyaw
    roll_c = line_roll(J["LeftArm"][i], J["RightArm"][i]) - rest_shroll
    Mp = T(dp) @ Rot(Hc, p_h, roll_h, yaw_h)
    for n in ("c_root_master.x", "c_root.x", "root.x", "c_thigh_b.l", "c_thigh_b.r"): M[n] = Mp
    dy_ = (yaw_c * YAW_SCALE - yaw_h) / 3.0; dr_ = (roll_c - roll_h) / 3.0
    M1 = Mp @ Rot(P("spine_01.x"), p_s, dr_, dy_)
    for n in ("c_spine_01.x", "spine_01.x"): M[n] = M1
    M2 = M1 @ Rot(P("spine_02.x"), p_s1, dr_, dy_)
    for n in ("c_spine_02.x", "spine_02.x"): M[n] = M2
    M3 = M2 @ Rot(P("spine_03.x"), p_s2, dr_, dy_)
    for n in ("c_spine_03.x", "spine_03.x"): M[n] = M3
    ph = p_n - D(14.0)                                               # keep the gaze forward instead of at the floor
    Mn = M3 @ Rot(P("neck.x"), ph * 0.5, 0.0, -yaw_c * YAW_SCALE * 0.45)
    for n in ("c_neck.x", "neck.x"): M[n] = Mn
    Mh = Mn @ Rot(P("head.x"), ph * 0.5, 0.0, -yaw_c * YAW_SCALE * 0.45)
    for n in ("head_scale_fix.x", "c_head.x", "head.x"): M[n] = Mh
    worst = 0.0; zmin_toe = 9.0
    for s, side in (("l", "Left"), ("r", "Right")):
        sg = 1.0 if s == "l" else -1.0
        # ---- arms: aim the upper arm and forearm along the reference directions (world space)
        for n in ("shoulder.", "c_shoulder.", "arm_twist.", "c_arm_twist_offset."): M[n + s] = M3
        du = seg_dir(J[side + "Arm"][i], J[side + "ForeArm"][i])
        df = seg_dir(J[side + "ForeArm"][i], J[side + "Hand"][i])
        du = Vector((du.x * ELBOW_OUT, du.y, du.z)).normalized(); df = Vector((df.x * HAND_IN, df.y, df.z)).normalized()
        Pa = P("arm_twist." + s); Pe = P("forearm." + s); Pw = P("hand." + s)
        d0u = (Pe - Pa).normalized(); d0f = (Pw - Pe).normalized()
        Rq = d0u.rotation_difference(du).to_matrix().to_4x4()
        Pm = M3 @ Pa
        Mu = T(Pm) @ Rq @ T(-Pa)
        M["arm_stretch." + s] = Mu
        Em = Mu @ Pe
        d1 = (Rq.to_3x3() @ d0f).normalized()
        Rf = d1.rotation_difference(df).to_matrix().to_4x4()
        Mf = T(Em) @ Rf @ T(-Em) @ Mu
        for n in ("forearm_stretch.", "forearm_twist.", "forearm."): M[n + s] = Mf
        for pb in rig.pose.bones:
            if pb.name.endswith("." + s) and (pb.name.startswith("hand") or "index" in pb.name or "thumb" in pb.name): M[pb.name] = Mf
        Mi1 = Mf @ Rot(P("index1." + s), 0.0, D(24) * sg, 0.0); M["index1." + s] = Mi1
        Mi2 = Mi1 @ Rot(P("c_index2." + s), 0.0, D(30) * sg, 0.0); M["c_index2." + s] = Mi2
        M["c_index3." + s] = Mi2 @ Rot(P("c_index3." + s), 0.0, D(20) * sg, 0.0)
        Mt1 = Mf @ Rot(P("thumb1." + s), 0.0, D(12) * sg, 0.0); M["thumb1." + s] = Mt1
        Mt2 = Mt1 @ Rot(P("c_thumb2." + s), 0.0, D(12) * sg, 0.0); M["c_thumb2." + s] = Mt2
        M["c_thumb3." + s] = Mt2 @ Rot(P("c_thumb3." + s), 0.0, D(10) * sg, 0.0)
        # ---- legs: IK to the scaled reference ankle
        H0, K0, A0 = P("c_thigh_fk." + s), P("leg_fk." + s), P("foot." + s)
        Hs = Mp @ H0
        fa = FPP[side][i]; ta = FPT[side][i]                     # foot pitch (+ toes up) and toe flex
        Aw = Vector((math.copysign(FOOT_X, A0.x), FPY[side][i], FPZ[side][i]))
        l1 = (K0 - H0).length; l2 = (A0 - K0).length
        K2, ratio = ik2(Vector((Hs.y, Hs.z)), Vector((Aw.y, Aw.z)), l1, l2)
        worst = max(worst, ratio)
        Kw = Vector(((Hs.x + Aw.x) / 2 + (K0.x - (H0.x + A0.x) / 2), K2[0], K2[1]))
        Rt = (K0 - H0).rotation_difference(Kw - Hs).to_matrix().to_4x4()
        Mt_ = T(Hs) @ Rt @ T(-H0)
        for n in ("c_thigh_fk.", "thigh_twist.", "thigh_stretch."): M[n + s] = Mt_
        Rs = (A0 - K0).rotation_difference(Aw - Kw).to_matrix().to_4x4()
        Ms = T(Kw) @ Rs @ T(-K0)
        for n in ("c_leg_fk.", "leg_fk.", "leg_stretch.", "leg_twist."): M[n + s] = Ms
        Mfoot = T(Aw - A0) @ Rot(A0, -fa, 0.0, -math.copysign(D(5), A0.x))
        M["foot." + s] = Mfoot
        M["toes_01." + s] = Mfoot @ Rot(P("toes_01." + s), -ta)
        ball_z = Aw.z + 0.143 * math.sin(fa) - 0.084 * math.cos(fa)
        zmin_toe = min(zmin_toe, ball_z)
    return M, {"ik_ratio": worst, "toe_z": zmin_toe}
def apply(phase):
    M, diag = build(int(round(phase * N)))
    cur = -1
    for pb in sorted(rig.pose.bones, key=depth):
        d = depth(pb)
        if d != cur: bpy.context.view_layer.update(); cur = d
        pb.matrix = M.get(pb.name, Matrix.Identity(4)) @ S[pb.name]
    bpy.context.view_layer.update()
    return diag
