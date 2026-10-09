import bpy, math
from mathutils import Matrix, Vector
ns = bpy.app.driver_namespace
S = ns["S_stand"]; rig = bpy.data.objects["rig"]
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

# ---------------------------------------------------------------- parameters
FPS = 30.0
PERIOD = 21                       # frames per full cycle (two steps) -> 0.70 s -> 171 steps/min
CYCLE = PERIOD / FPS
V = 2.727                         # ground speed in Blender units/s (= 3.0 m/s game sprint / 1.1 FBX import scale)
STEP = V * CYCLE / 2.0            # foot travel per unit phase = 2*STEP
DUTY = 0.31                       # fraction of the cycle one foot is on the ground
FA = 0.22                         # how far the ankle is ahead of the hip at touchdown
HS, HO = 0.07, 0.165              # end of heel-strike roll, start of heel-off (fractions of the cycle)
TOEOFF = D(36)                    # foot pitch at toe-off
HEEL = (0.085, 0.084); BALL = (-0.143, 0.084); Y0, Z0 = 0.035, 0.084
KICK = 0.12; KNEE = 0.03; HANG = D(25)     # swing: heel lift (restrained), late knee drive
KF_CONTACT, KF_LOAD, KF_TOEOFF = 24.0, 46.0, 14.0   # knee flexion targets in stance (degrees)
LEAN_P, LEAN_S = 4.5, 3.5         # pelvis and per-spine-bone forward pitch (deg)  -> trunk ~7 deg
YAW_P, YAW_S = 5.5, (3.0, 4.0, 5.0)   # pelvis yaw, spine yaw (counter-rotation: shoulders turn more than pelvis)
ROLL = 3.5; SWAY = 0.017; OSC = 1.0
ARM_A0, ARM_A1 = -D(4.5), D(26.0)   # upper arm: offset, half-range (local, after lean)
ELB_BACK, ELB_FWD = 50.0, 60.0     # elbow bend (beta) at back swing / forward swing (deg)
FOOT_X = 0.112

def R2(v, a): return (v[0] * math.cos(a) - v[1] * math.sin(a), v[0] * math.sin(a) + v[1] * math.cos(a))
def stance(ph):
    F = FA - 2 * STEP * ph
    if ph < HS:
        th = D(7) * (1 - sm(ph / HS)); h = FA - HEEL[0] - 2 * STEP * ph
        o = R2(HEEL, th); return (h + o[0], o[1], th)
    if ph < HO: return (F, Z0, 0.0)
    th = -TOEOFF * sm((ph - HO) / (DUTY - HO)); b = FA + 0.143 - 2 * STEP * ph
    o = R2(BALL, th); return (b + o[0], o[1], th)
def foot(ph):
    ph %= 1.0
    if ph < DUTY: return stance(ph)
    L = 1.0 - DUTY; s = (ph - DUTY) / L; e = 1e-3
    p0 = stance(DUTY); p1 = stance(0.0)
    d0 = [(stance(DUTY)[i] - stance(DUTY - e)[i]) / e * L for i in (0, 1)]
    d1 = [(stance(e)[i] - stance(0.0)[i]) / e * L for i in (0, 1)]
    h00 = 2 * s ** 3 - 3 * s ** 2 + 1; h10 = s ** 3 - 2 * s ** 2 + s; h01 = -2 * s ** 3 + 3 * s ** 2; h11 = s ** 3 - s ** 2
    f = h00 * p0[0] + h10 * d0[0] + h01 * p1[0] + h11 * d1[0]
    z = h00 * p0[1] + h10 * d0[1] + h01 * p1[1] + h11 * d1[1] + KICK * math.sin(math.pi * s ** 0.8) ** 1.1 + KNEE * (s ** 1.5) * math.sin(math.pi * s)
    th = p0[2] + (p1[2] - p0[2]) * sm(s)
    w = 1.0 - sm(s / 0.22) * (1.0 - sm((s - 0.72) / 0.28))          # toe flex still present early in the swing
    tip_a = th + w * ((-0.9 * th) if th < 0 else 0.0)
    zmin = max(0.084 * math.cos(th) - 0.143 * math.sin(th), 0.084 * math.cos(th) - 0.143 * math.sin(th) - 0.073 * math.sin(tip_a)) + 0.004
    return (f, max(z, zmin), th)
def ik2(Hs, A, l1, l2):
    d = (A - Hs).length; ratio = d / (l1 + l2); d = min(d, l1 + l2 - 1e-4)
    u = (A - Hs).normalized(); a = (d * d + l1 * l1 - l2 * l2) / (2 * d); h = math.sqrt(max(0.0, l1 * l1 - a * a))
    perp = Vector((-u[1], u[0]))
    if perp[0] > 0: perp = -perp
    return Hs + u * a + perp * h, ratio

# ---------------------------------------------------------------- pelvis: height follows leg compression while on the ground, ballistic arc in flight
H0L, K0L, A0L = P("c_thigh_fk.l"), P("leg_fk.l"), P("foot.l")
L1, L2 = (K0L - H0L).length, (A0L - K0L).length
Hc = (P("c_thigh_fk.l") + P("c_thigh_fk.r")) / 2
def pelvis_terms(phL):
    a = 2 * math.pi * (phL + 0.085)
    pm = phL % 0.5
    osc = D(OSC) * math.cos(4 * math.pi * (phL - 0.165))
    return dict(sway=SWAY * math.sin(a), roll=-D(ROLL) * math.sin(a), yaw=-D(YAW_P) * math.cos(2 * math.pi * phL), osc=osc, pm=pm)
def Mp_nobob(phL):
    t = pelvis_terms(phL)
    return T((t["sway"], 0.0, 0.0)) @ Rot(Hc, D(LEAN_P) + t["osc"], t["roll"], t["yaw"])
def kf_profile(ps):
    if ps < 0.30: return KF_CONTACT + (KF_LOAD - KF_CONTACT) * sm(ps / 0.30)
    return KF_LOAD + (KF_TOEOFF - KF_LOAD) * sm((ps - 0.30) / 0.70)
def z_req(phi):
    Hs0 = Mp_nobob(phi) @ H0L
    f, z, th = foot(phi)
    d = math.cos(D(kf_profile(phi / DUTY)) / 2.0) * (L1 + L2)
    dy = (Y0 - f) - Hs0.y
    return z + math.sqrt(max(d * d - dy * dy, 1e-6)) - Hs0.z
G_BU = 9.81 / 1.1                      # gravity in Blender units (1 BU = 1.1 m in the game)
PH_LOW = 0.125                         # phase of the lowest hip point (about mid-stance)
ZC, ZT, ZL = -0.012, -0.006, -0.046      # hip height offsets at touchdown / toe-off / mid-stance (chosen so knee bend is ~28 / 22 / 44 deg)
TF = (0.5 - DUTY) * CYCLE              # flight time in seconds
VZ = (ZC - ZT) / TF + 0.5 * G_BU * TF  # take-off vertical speed so that the ballistic arc lands exactly at the next touchdown height
VL = VZ - G_BU * TF                    # landing vertical speed (negative)
def herm(z0, m0, z1, m1, s):
    h00 = 2 * s ** 3 - 3 * s ** 2 + 1; h10 = s ** 3 - 2 * s ** 2 + s; h01 = -2 * s ** 3 + 3 * s ** 2; h11 = s ** 3 - s ** 2
    return h00 * z0 + h10 * m0 + h01 * z1 + h11 * m1
def bob(phL):
    pm = phL % 0.5
    if pm < PH_LOW:
        return herm(ZC, VL * CYCLE * PH_LOW, ZL, 0.0, pm / PH_LOW)
    if pm < DUTY:
        L = DUTY - PH_LOW
        return herm(ZL, 0.0, ZT, VZ * CYCLE * L, (pm - PH_LOW) / L)
    t = (pm - DUTY) * CYCLE
    return ZT + VZ * t - 0.5 * G_BU * t * t

def build(phL):
    M = {}
    c = lambda ph: math.cos(2 * math.pi * ph); s_ = lambda ph: math.sin(2 * math.pi * ph)
    t = pelvis_terms(phL)
    Mp = T((t["sway"], 0.0, bob(phL))) @ Rot(Hc, D(LEAN_P) + t["osc"], t["roll"], t["yaw"])
    for n in ("c_root_master.x", "c_root.x", "root.x", "c_thigh_b.l", "c_thigh_b.r"): M[n] = Mp
    M1 = Mp @ Rot(P("spine_01.x"), D(LEAN_S), D(0.8) * s_(phL), D(YAW_S[0]) * c(phL))
    for n in ("c_spine_01.x", "spine_01.x"): M[n] = M1
    M2 = M1 @ Rot(P("spine_02.x"), D(LEAN_S), D(0.8) * s_(phL), D(YAW_S[1]) * c(phL))
    for n in ("c_spine_02.x", "spine_02.x"): M[n] = M2
    M3 = M2 @ Rot(P("spine_03.x"), D(LEAN_S), D(0.8) * s_(phL), D(YAW_S[2]) * c(phL))
    for n in ("c_spine_03.x", "spine_03.x"): M[n] = M3
    # head: cancels the pelvis pitch wobble and most of the trunk twist, with a small nod on touchdown
    tw = (-YAW_P + sum(YAW_S)) * c(phL)
    nod = D(1.2) * math.exp(-(((phL % 0.5) - 0.05) / 0.05) ** 2)
    Mn = M3 @ Rot(P("neck.x"), D(-8.5) - t["osc"] * 0.5, 0.0, -D(0.45 * (-YAW_P + sum(YAW_S))) * c(phL))
    for n in ("c_neck.x", "neck.x"): M[n] = Mn
    Mh = Mn @ Rot(P("head.x"), D(-6.0) - t["osc"] * 0.5 + nod, 0.0, -D(0.5 * (-YAW_P + sum(YAW_S))) * c(phL))
    for n in ("head_scale_fix.x", "c_head.x", "head.x"): M[n] = Mh
    zmin_toe = 9.0; worst = 0.0
    for s, sgn in (("l", 0.0), ("r", 0.5)):
        phs = phL + sgn
        sg = 1.0 if s == "l" else -1.0
        fw = 0.5 * (1.0 - c(phs - 0.05))                      # 0 arm back ... 1 arm forward (arm opposes the same-side leg)
        alpha = ARM_A0 + ARM_A1 * c(phs - 0.05)
        beta = -(D(ELB_BACK) + D(ELB_FWD - ELB_BACK) * fw)
        inw = -1.0 if s == "l" else 1.0
        cross = fw - 0.25
        if cross < 0: cross *= 0.3
        Mcl = M3 @ Rot(P("shoulder." + s), 0.0, 0.0, -sg * D(3.0) * (fw - 0.5) * 2.0)   # shoulder girdle glides with the arm
        for n in ("shoulder.", "c_shoulder.", "arm_twist.", "c_arm_twist_offset."): M[n + s] = Mcl
        Mu = Mcl @ Rot(P("arm_twist." + s), alpha, sg * D(2.0), inw * D(7.0) * cross)
        M["arm_stretch." + s] = Mu
        Mf = Mu @ Rot(P("forearm." + s), beta, 0.0, inw * D(10.0) * cross)
        for n in ("forearm_stretch.", "forearm_twist.", "forearm."): M[n + s] = Mf
        for pb in rig.pose.bones:
            if pb.name.endswith("." + s) and (pb.name.startswith("hand") or "index" in pb.name or "thumb" in pb.name): M[pb.name] = Mf
        Mi1 = Mf @ Rot(P("index1." + s), 0.0, D(24) * sg, 0.0); M["index1." + s] = Mi1       # loosely closed hands
        Mi2 = Mi1 @ Rot(P("c_index2." + s), 0.0, D(30) * sg, 0.0); M["c_index2." + s] = Mi2
        M["c_index3." + s] = Mi2 @ Rot(P("c_index3." + s), 0.0, D(20) * sg, 0.0)
        Mt1 = Mf @ Rot(P("thumb1." + s), 0.0, D(12) * sg, 0.0); M["thumb1." + s] = Mt1
        Mt2 = Mt1 @ Rot(P("c_thumb2." + s), 0.0, D(12) * sg, 0.0); M["c_thumb2." + s] = Mt2
        M["c_thumb3." + s] = Mt2 @ Rot(P("c_thumb3." + s), 0.0, D(10) * sg, 0.0)
        H0, K0, A0 = P("c_thigh_fk." + s), P("leg_fk." + s), P("foot." + s)
        Hs = Mp @ H0
        f, z, th = foot(phs)
        Aw = Vector((math.copysign(FOOT_X, A0.x), Y0 - f, z))
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
        psi = Rs.to_3x3().to_euler().x
        ph1 = phs % 1.0
        ang = -th; swing_w = 0.0
        if ph1 >= DUTY:
            ss = (ph1 - DUTY) / (1.0 - DUTY)
            swing_w = sm(ss / 0.22) * (1.0 - sm((ss - 0.72) / 0.28))
            ang = ang + (psi + HANG - ang) * swing_w
        Mfoot = T(Aw - A0) @ Rot(A0, ang, 0.0, -math.copysign(D(5), A0.x))
        M["foot." + s] = Mfoot
        M["toes_01." + s] = Mfoot @ Rot(P("toes_01." + s), ((th * 0.9) if th < 0 else 0.0) * (1.0 - swing_w))
        ball_z = z + 0.143 * math.sin(th) - 0.084 * math.cos(th)
        zmin_toe = min(zmin_toe, ball_z, ball_z + 0.073 * math.sin(th + ((-0.9 * th) if th < 0 else 0.0)))
    return M, {"ik_ratio": worst, "toe_z": zmin_toe}
def apply(phL):
    M, diag = build(phL)
    cur = -1
    for pb in sorted(rig.pose.bones, key=depth):
        d = depth(pb)
        if d != cur: bpy.context.view_layer.update(); cur = d
        pb.matrix = M.get(pb.name, Matrix.Identity(4)) @ S[pb.name]
    bpy.context.view_layer.update()
    return diag
