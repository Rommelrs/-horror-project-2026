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
HEAD_OFF = 14.0
REFFILE = "ref_dump.json"
FWD_KEEP = 1.0
TWIST_FOLLOW = 0.9                # share of the upper-arm rotation the twist bones follow (0 = old behaviour: stuck to the torso)
STANCE_STRAIGHT = 0.0             # 0..1: hips ride up so the leg that is on the ground stays straight from landing to push-off (inverted-pendulum vault)
LIFT_UNDELAYED = 0                # 1: SWING_DELAY only delays the forward swing, not the heel lift
HZ_SMOOTH = 0                     # extra smoothing passes on the hip height curve (removes the vault/drop sawtooth)
KEYED_LEGS = 0                    # 1: legs + hip height come from the designed key-pose system below (overrides all the older leg/hip hacks)
K_D = 0.30                        # ground contact per foot (fraction of the cycle)
K_ALPHA = 20.0                    # leg angle at heel strike (deg, hip->ankle ahead of vertical)
K_FLEX_C = 5.0                    # visible knee bend at heel strike (deg)
K_FLEX_M = 22.0                   # visible knee bend at mid-stance
K_FLEX_T = 8.0                    # visible knee bend at toe-off
K_MID = 0.45                      # where in the stance the bend peaks
K_P0 = 15.0                       # heel-strike toes-up angle
K_PT = 35.0                       # toe-off heel-raise angle
K_HS = 0.10                       # heel rocker ends (fraction of stance)
K_HO = 0.55                       # heel-off starts (fraction of stance)
K_A = 0.0                         # >0: hip height is one smooth wave per step (BU half-amplitude), lowest at K_PLOW of the stance; the knee follows by IK
K_PLOW = 0.45
K_TAN_MAX = 100000.0              # cap (deg per unit swing) on the swing-curve slopes at toe-off / heel strike
K_SWING = [(0.22, -6.0, 70.0), (0.45, 8.0, 98.0), (0.68, 28.0, 80.0), (0.86, 30.0, 25.0)]   # swing keys (s, visible thigh deg, visible knee bend deg)
K_SWPITCH = [(0.25, -40.0), (0.55, -20.0), (0.82, 10.0)]                                       # swing foot pitch keys (s, deg vs flat, + = toes up)
FLIGHT_LIFT = 0.0                 # BU: the whole body (hips + both airborne feet) rises in a small arc while both feet are off the ground
HEEL_LAND = 0.0                   # 0..1: in the late swing the foot turns toes-up so it lands on the heel
HEEL_LAND_FROM = 0.55
LATE_STRAIGHT = 0.0               # 0..1: in the late swing the front leg reaches out until it is straight (LATE_RATIO of full length) before touching down
LATE_FROM = 0.6
LATE_RATIO = 0.97
SWING_LIFT = 0.0                  # extra ankle height (BU) while the swing foot passes under the body (ground clearance)
SWING_LIFT_AT = 0.62              # where in the swing (0..1) that lift peaks
STRAIGHT_FROM = 0.004             # ankle height above flat at which the stance leg starts being held straight (raise to include heel strike)
SWING_DELAY = 0.0                 # >0: the swing foot trails behind the body longer and snaps forward late (the runner's shin stays horizontal behind a low thigh)
DUTY_ADD = 0.0                    # lengthens the ground contact (flat foot + toe-off) so the foot can land further ahead without losing the push-off reach behind
ELBOW_FLAT = 0.0                  # 0..1: pulls the elbow bend towards a constant ELBOW_TARGET (degrees) instead of the reference's opening/closing
ELBOW_TARGET = 92.0
HAND_CROSS = 0.0                  # degrees the forward forearm turns in across the chest
HAND_LIFT = 0.0                   # extra elbow bend (deg) with the arm forward, raising the hand
SH_PROTRACT = 0.0                 # degrees the shoulder girdle rolls forward/back with the arm swing
SH_ELEV = 0.0                     # degrees the shoulder lifts with the arm forward
ARM_ABD = 0.0                     # degrees: opens both arm segments sideways, away from the torso
ARM_BIAS = 0.0                    # degrees: rotates both arm segments forward (+) so the swing is centred where it should be
LAND_REACH = 0.0                  # 0..1: foot also gets forward to the landing spot earlier in the late swing
HIP_BUMP = 0.0                    # hips rise a little around each foot strike so the landing leg can be almost straight
LAND_LOW = 0.0                    # 0..1: foot comes down earlier in the late swing so the leg is extended before touchdown
FA_SHIFT = 0.0                    # shifts the whole foot path back relative to the pelvis (more hip extension at push-off, less reach at landing)
KICK_SCALE = 1.0                  # < 1 lowers the swing foot (heel kick) relative to the reference
ARM_SWING = 1.0                   # < 1 shrinks the upper-arm swing but keeps the elbow bend
YS = 1.0                          # stride scale: shrinks the reference's foot travel to the game's run speed
ELBOW_OUT = 0.45                  # the reference flares the elbows out; Khalid keeps them closer to his sides
HAND_IN = 0.55
YAW_SCALE = 0.75                  # reference twists the chest a lot
PELVIS_YAW = 1.0                  # scales the pelvis twist (the waist turns with the legs, the spine winds up against the chest)
PELVIS_ROLL = 1.0                 # scales the pelvis drop side to side
SWAY_SCALE = 1.0                  # scales the sideways shift of the pelvis over the stance foot
BACK_BEND = 0.0                   # degrees: the lower back bends sideways over the stance leg (counters the pelvis drop)
IRL_UPPER = 0                     # 1: torso, head, arms, shoulders and pelvis come from the key tables measured off IRL Run.mov (side view, 24 video frames per cycle, t=0 = left heel strike)
# measured per video frame t=0..23 (smoothed); the left arm, right arm = same table shifted half a cycle
IRL_ARM = [-80, -74, -62, -46, -30, -14, 2, 16, 27, 32, 32, 29, 24, 17, 8, -4, -18, -34, -50, -64, -74, -80, -83, -83]   # upper arm swing (deg, + forward)
IRL_FLEX = [118, 112, 104, 98, 95, 95, 98, 104, 112, 118, 120, 120, 118, 115, 108, 100, 95, 95, 98, 104, 110, 116, 120, 120]  # elbow bend (deg)
IRL_LEAN = [7, 6.5, 5.5, 4.5, 4, 4, 4.5, 5.5, 6.5, 7, 7, 7, 7, 6.5, 5.5, 4.5, 4, 4, 4.5, 5.5, 6.5, 7, 7, 7]               # trunk lean, shoulders ahead of hips (deg)
IRL_ARM_SCALE = 1.0               # < 1 calms the arm swing around its centre
IRL_ARM_ROT = 45.0                # forearms turned in across the body (shoulder internal rotation, deg)
IRL_ARM_ABD = 12.0                # upper arms held slightly away from the sides
IRL_NECK = 6.0                    # neck carried forward (deg)
IRL_HEAD = 4.0                    # chin up relative to level (deg)
IRL_CHEST_YAW = 8.0               # chest turns with the arms (deg at full swing)
IRL_SH = 10.0                     # shoulder girdle follows the arm (deg protraction at full swing)
PELVIS_SYNTH = 0                 # 1: pelvis twist / drop / sway are smooth waves timed to Khalid's own feet instead of the reference's (which is off-phase after the leg edits)
PELVIS_YAW_DEG = 10.0             # twist: hip of the forward leg turned forward, peak at each touchdown
PELVIS_ROLL_DEG = 6.0             # drop: swing-leg side of the pelvis lower, peak at mid-stance
SWAY_BU = 0.022                   # sideways shift over the stance foot, peak at mid-stance
HIP_LIFT = 0.02                   # slightly less crouched than the reference
Z0 = 0.084

REF = json.load(open(BASE + REFFILE))
RF = {int(k): v for k, v in REF["frames"].items()}
NREF = len(RF) - 1                          # frames 1..22 are the cycle (23 == 1)
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
def W(p): return Vector((SC * (p.x - XC), SC * YS * (p.y - YC) + Hc.y, SC * p.z + OFFZ))

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
TO = next(k for k in range(N - 1, 0, -1) if _toe[_k[k]].z <= 0.02)                  # toe-off: last sample the toe is on the floor
FS = next(k for k in range(N) if _an[_k[k]].z - Z0 <= 0.004)                          # flat foot begins
FE = max(k for k in range(N) if _an[_k[k]].z - Z0 <= 0.004 and k <= TO)               # flat foot ends
DUTY = TO / N; HS = FS / N; HO = FE / N
if DUTY < HO + 0.02: DUTY = HO + 0.02
DUTY0 = DUTY
HO += DUTY_ADD; DUTY += DUTY_ADD
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
    ref_ph = DUTY0 + (s ** (1.0 + SWING_DELAY)) * (1.0 - DUTY0)
    i = (TD + int(round(ref_ph * N))) % N
    KZ = lambda z: Z0 + KICK_SCALE * (z - Z0)
    iz = (TD + int(round((DUTY0 + (s ** (1.0 + SWING_DELAY * (1.0 - LIFT_UNDELAYED))) * (1.0 - DUTY0)) * N))) % N   # heel lifts on the reference's own timing even when the forward swing is delayed
    ref = (_rel[i], KZ(_an[iz].z), foot_ang("Left", iz) - flat_ang["Left"], toe_ang("Left", iz) - foot_ang("Left", iz))
    e = stance(DUTY); st = stance(0.0)
    r0 = (_rel[_k[TO]], KZ(_an[_k[TO]].z), foot_ang("Left", _k[TO]) - flat_ang["Left"], toe_ang("Left", _k[TO]) - foot_ang("Left", _k[TO]))
    r1 = (_rel[TD], KZ(_an[TD].z), foot_ang("Left", TD) - flat_ang["Left"], toe_ang("Left", TD) - foot_ang("Left", TD))
    s0 = (e[0], e[1], e[2], (-0.9 * e[2] if e[2] < 0 else 0.0)); s1 = (st[0], st[1], st[2], 0.0)
    w0 = (1 - sm(s)) ** 2; w1 = sm(s) ** 2
    out = [ref[j] + (s0[j] - r0[j]) * w0 + (s1[j] - r1[j]) * w1 for j in range(4)]
    out[1] = Z0 + (out[1] - Z0) * (1.0 - LAND_LOW * sm((s - 0.40) / 0.55))
    out[0] = out[0] + (s1[0] - out[0]) * LAND_REACH * sm((s - 0.45) / 0.5)
    out[1] += SWING_LIFT * max(0.0, 1.0 - ((s - SWING_LIFT_AT) / 0.38) ** 2) ** 2
    if HEEL_LAND > 0.0:
        wh = HEEL_LAND * sm((s - HEEL_LAND_FROM) / (0.92 - HEEL_LAND_FROM))
        out[2] = out[2] + (HS_ANG - out[2]) * wh
        out[3] = out[3] * (1.0 - wh)
    return tuple(out)
FPY = {"Left": [], "Right": []}; FPZ = {"Left": [], "Right": []}; FPP = {"Left": [], "Right": []}; FPT = {"Left": [], "Right": []}
for i in range(N):
    for s, off in (("Left", 0.0), ("Right", 0.5)):
        f, z, p, t = sample_left(i / N - TD / N + 0.0 - off)
        FPY[s].append(Hc.y - f + FA_SHIFT); FPP[s].append(p); FPT[s].append(t)
        zok = max(0.084 * math.cos(p) - 0.143 * math.sin(p), 0.084 * math.cos(p) - 0.143 * math.sin(p) - 0.073 * math.sin(p + t), 0.084 * math.cos(p) + 0.085 * math.sin(p)) + 0.002
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
HX = [SWAY_SCALE * SC * (hcr[i].x - XC) for i in range(N)]
_MIDST = TD + DUTY * N / 2.0                                         # left mid-stance (dense index)
if PELVIS_SYNTH:
    HX = [SWAY_BU * math.cos(2 * math.pi * (i - _MIDST) / N) for i in range(N)]
REACH = 0.985
if FLIGHT_LIFT > 0.0 and DUTY < 0.5:
    for i in range(N):
        phL = (i / N - TD / N) % 1.0
        for st in (0.0, 0.5):
            u = (phL - st - DUTY) / (0.5 - DUTY)
            if 0.0 < u < 1.0:
                b = FLIGHT_LIFT * math.sin(math.pi * u)
                HZ[i] += b
                FPZ["Left"][i] += b; FPZ["Right"][i] += b
    FP = {s_: (FPY[s_], FPZ[s_]) for s_ in ("Left", "Right")}
for i in range(N):
    for c in (TD, TD + N // 2):
        d = ((i - c + N / 2) % N) - N / 2
        HZ[i] += HIP_BUMP * sm(1.0 - abs(d) / (0.09 * N))
if STANCE_STRAIGHT > 0.0:
    for i in range(N):
        for s_ in ("Left", "Right"):
            ay_, az_ = FP[s_][0][i], FP[s_][1][i]
            dy_ = ay_ - HY[i]
            w_ = 1.0 - sm((az_ - Z0 - STRAIGHT_FROM) / 0.06)
            lim_ = az_ + math.sqrt(max((REACH * LTOT) ** 2 - dy_ * dy_, 1e-6))
            HZ[i] += STANCE_STRAIGHT * w_ * (lim_ - HZ[i])
for i in range(N):
    lim = 9.0
    for s in ("Left", "Right"):
        ay, az = FP[s][0][i], FP[s][1][i]
        dy = ay - HY[i]
        lim = min(lim, az + math.sqrt(max((REACH * LTOT) ** 2 - dy * dy, 1e-6)))
    HZ[i] = min(HZ[i], lim)
for _ in range(3 + HZ_SMOOTH):     # smoothing (periodic) of the hip height, then re-limit
    HZ = [0.25 * HZ[i - 1] + 0.5 * HZ[i] + 0.25 * HZ[(i + 1) % N] for i in range(N)]
    for i in range(N):
        lim = 9.0
        for s in ("Left", "Right"):
            dy = FP[s][0][i] - HY[i]
            lim = min(lim, FP[s][1][i] + math.sqrt(max((REACH * LTOT) ** 2 - dy * dy, 1e-6)))
        HZ[i] = min(HZ[i], lim)

if KEYED_LEGS:
    # ---------------------------------------------------------------- designed legs (key poses from the real-run video)
    _L1, _L2 = L1, L2
    _rest_d = (H0L - A0L).length
    _OFFK = math.acos(max(-1.0, min(1.0, (_rest_d ** 2 - _L1 ** 2 - _L2 ** 2) / (2 * _L1 * _L2))))   # rest bend of the bone chain (looks straight)
    _vr = P("leg_fk.l") - H0L
    _THOFF = math.atan2(-_vr.y, -_vr.z)                                                     # rest thigh-bone tilt (looks vertical)
    _dx = FOOT_X - abs(H0L.x)
    def _dist(flexv): return math.sqrt(_L1 ** 2 + _L2 ** 2 + 2 * _L1 * _L2 * math.cos(math.radians(flexv) + _OFFK))
    _R = V * CYCLE                                                                           # ground travel per cycle (BU)
    _P0, _PT = math.radians(K_P0), math.radians(K_PT)
    def _flex_st(u):
        if u < K_MID: return K_FLEX_C + (K_FLEX_M - K_FLEX_C) * sm(u / K_MID)
        return K_FLEX_M + (K_FLEX_T - K_FLEX_M) * sm((u - K_MID) / (1.0 - K_MID))
    def _pitch_st(u):
        if u < K_HS: return _P0 * (1.0 - sm(u / K_HS))
        if u < K_HO: return 0.0
        return -_PT * sm((u - K_HO) / (1.0 - K_HO))
    # flat-foot ankle position at contact chosen so the leg is at K_ALPHA with K_FLEX_C bend at heel strike
    _dc = math.sqrt(max(_dist(K_FLEX_C) ** 2 - _dx ** 2, 1e-6))
    _fc = _dc * math.sin(math.radians(K_ALPHA))
    _a0 = _fc + 0.085 - 0.085 * math.cos(_P0) + 0.084 * math.sin(_P0)
    def _ankle_st(phi):                                                # (ahead of hip, height, pitch, toe flex) while the foot is on the ground
        u = phi / K_D; F = _a0 - _R * phi; th = _pitch_st(u)
        if th >= 0.0:
            o = R2((0.085, 0.084), th); return (F - 0.085 + o[0], o[1], th, 0.0)
        o = R2((-0.143, 0.084), th); return (F + 0.143 + o[0], o[1], th, -0.9 * th)
    def _hip_st(phi):
        f, z, th, t = _ankle_st(phi)
        d = _dist(_flex_st(phi / K_D))
        return z + math.sqrt(max(d * d - f * f - _dx * _dx, 1e-6))
    _e = 1e-4
    _h0, _h1 = _hip_st(0.0), _hip_st(K_D - _e)
    _m0 = (_hip_st(_e) - _h0) / _e; _m1 = (_hip_st(K_D - _e) - _hip_st(K_D - 2 * _e)) / _e
    def _herm(p0, m0, p1, m1, t, span):
        t2, t3 = t * t, t * t * t
        return (2 * t3 - 3 * t2 + 1) * p0 + (t3 - 2 * t2 + t) * m0 * span + (-2 * t3 + 3 * t2) * p1 + (t3 - t2) * m1 * span
    def _hip_of(p):                                                    # p = phase within one step (0..0.5), 0 = a heel strike
        p %= 0.5
        if K_A > 0.0:
            return _h0 + K_A * math.cos(2 * math.pi * (-K_D * K_PLOW) / 0.5) - K_A * math.cos(2 * math.pi * (p - K_D * K_PLOW) / 0.5)
        if p < K_D: return _hip_st(p)
        span = 0.5 - K_D
        return _herm(_h1, _m1, _h0, _m0, (p - K_D) / span, span)       # free flight back to the next heel strike
    def _ik_angles(f, z, h):                                           # bone-chain thigh angle and knee bend for an ankle at (f ahead, z) under a hip at h
        dz = h - z; dd = math.sqrt(f * f + dz * dz)
        dd = min(dd, _L1 + _L2 - 1e-5)
        lam = math.atan2(f, dz)
        beta = math.acos(max(-1.0, min(1.0, (_L1 ** 2 + dd ** 2 - _L2 ** 2) / (2 * _L1 * dd))))
        kap = math.pi - math.acos(max(-1.0, min(1.0, (_L1 ** 2 + _L2 ** 2 - dd ** 2) / (2 * _L1 * _L2))))
        return lam + beta, kap
    def _fk(thb, kap, h):
        kf, kz = _L1 * math.sin(thb), h - _L1 * math.cos(thb)
        ths = thb - kap
        return kf + _L2 * math.sin(ths), kz - _L2 * math.cos(ths)
    def _st_angles(phi):
        f, z, th, t = _ankle_st(phi); return _ik_angles(f, z, _hip_of(phi))
    # swing curves: from the toe-off pose to the next heel strike, through the video key poses, with matched speeds at both ends
    _TO = _st_angles(K_D - _e); _TOp = _st_angles(K_D - 2 * _e)
    _HSa = _st_angles(0.0); _HSn = _st_angles(_e)
    _sp = 1.0 - K_D
    _keys = [(0.0, _TO[0], _TO[1])] + [(s_, math.radians(tv) + _THOFF, math.radians(kv) + _OFFK) for (s_, tv, kv) in K_SWING] + [(1.0, _HSa[0], _HSa[1])]
    _tan0 = ((_TO[0] - _TOp[0]) / _e * _sp, (_TO[1] - _TOp[1]) / _e * _sp)
    _tan1 = ((_HSn[0] - _HSa[0]) / _e * _sp, (_HSn[1] - _HSa[1]) / _e * _sp)
    _TC = math.radians(K_TAN_MAX)                                                          # a leg at full stretch at toe-off has a near-infinite IK slope; cap it
    _tan0 = tuple(max(-_TC, min(_TC, x)) for x in _tan0); _tan1 = tuple(max(-_TC, min(_TC, x)) for x in _tan1)
    def _spline(keys, t0, t1, s_, j):
        for k in range(len(keys) - 1):
            if keys[k][0] <= s_ <= keys[k + 1][0]: break
        sa, sb = keys[k][0], keys[k + 1][0]; span = sb - sa
        pa, pb = keys[k][j], keys[k + 1][j]
        def tang(m):
            if m == 0: return t0
            if m == len(keys) - 1: return t1
            return (keys[m + 1][j] - keys[m - 1][j]) / (keys[m + 1][0] - keys[m - 1][0])
        return _herm(pa, tang(k), pb, tang(k + 1), (s_ - sa) / span, span)
    _pkeys = [(0.0, -_PT)] + [(s_, math.radians(v)) for (s_, v) in K_SWPITCH] + [(1.0, _P0)]
    for _s, _off in (("Left", 0.0), ("Right", 0.5)):
        for i in range(N):
            phi = (i / N - TD / N - _off) % 1.0
            h = _hip_of(phi)
            if phi < K_D:
                f, z, th, t = _ankle_st(phi)
            else:
                s_ = (phi - K_D) / _sp
                thb = _spline(_keys, _tan0[0], _tan1[0], s_, 1)
                kap = _spline(_keys, _tan0[1], _tan1[1], s_, 2)
                kap = max(kap, _OFFK + math.radians(2.0))                  # never past straight
                f, rz = _fk(thb, kap, h); z = rz
                th = _spline(_pkeys, 0.0, 0.0, s_, 1)
                t = 0.9 * _PT * (1.0 - sm(s_ / 0.3))
                zok = max(0.084 * math.cos(th) - 0.143 * math.sin(th), 0.084 * math.cos(th) + 0.085 * math.sin(th)) + 0.01
                z = max(z, zok)
            FPY[_s][i] = Hc.y - f; FPZ[_s][i] = z; FPP[_s][i] = th; FPT[_s][i] = t
    HZ = [_hip_of((i / N - TD / N) % 1.0) for i in range(N)]
    FP = {s_: (FPY[s_], FPZ[s_]) for s_ in ("Left", "Right")}

if LATE_STRAIGHT > 0.0:
    _dx = FOOT_X - abs(H0L.x)
    for _s, _off in (("Left", 0.0), ("Right", 0.5)):
        for i in range(N):
            ph = (i / N - TD / N - _off) % 1.0
            if ph < DUTY: continue
            sw = (ph - DUTY) / (1.0 - DUTY)
            w = LATE_STRAIGHT * sm((sw - LATE_FROM) / (1.0 - LATE_FROM) * 2.0)
            if w <= 0.0: continue
            hy, hz = HY[i], HZ[i]
            ay, az = FPY[_s][i], FPZ[_s][i]
            want = LATE_RATIO * LTOT
            dz = hz - az
            if want * want <= dz * dz + _dx * _dx: continue
            ahead = math.sqrt(want * want - dz * dz - _dx * _dx)      # ankle distance ahead of the hip for a straight leg at this height
            cur = hy - ay
            if cur < ahead and cur > 0.0:
                FPY[_s][i] = ay - (ahead - cur) * w
    FP = {s: (FPY[s], FPZ[s]) for s in ("Left", "Right")}

def irl_curve(tab, t):                                               # periodic Catmull-Rom through the per-video-frame measurements
    n = len(tab); t %= n; j = int(math.floor(t)); u = t - j
    return cr(tab[(j - 1) % n], tab[j % n], tab[(j + 1) % n], tab[(j + 2) % n], u)
if IRL_UPPER:
    _KD = K_D if KEYED_LEGS else DUTY
    def _wst(phi):                                                   # weight on a foot: 0 in the air, 1 at mid-stance
        phi %= 1.0
        return math.sin(math.pi * phi / _KD) ** 2 if phi < _KD else 0.0
    _AH = {s_: [Hc.y - FPY[s_][i] for i in range(N)] for s_ in ("Left", "Right")}      # ankle ahead of the hips
    _ADM = max(abs(_AH["Left"][i] - _AH["Right"][i]) for i in range(N))
    _WD = [_wst(i / N - TD / N) - _wst(i / N - TD / N - 0.5) for i in range(N)]        # + = weight on the left foot
    HX = [SWAY_BU * _WD[i] for i in range(N)]                                          # hips shift over the foot that carries the weight
_DU = {s: [(J[s + "ForeArm"][i] - J[s + "Arm"][i]).normalized() for i in range(N)] for s in ("Left", "Right")}
_DUM = {}
for s in ("Left", "Right"):
    m = Vector((0, 0, 0))
    for v in _DU[s]: m += v
    _DUM[s] = m.normalized()
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
    yaw_h = (line_yaw(J["LeftUpLeg"][i], J["RightUpLeg"][i]) - rest_hipyaw) * PELVIS_YAW
    roll_h = (line_roll(J["LeftUpLeg"][i], J["RightUpLeg"][i]) - rest_hiproll) * PELVIS_ROLL
    if PELVIS_SYNTH:
        yaw_h = -D(PELVIS_YAW_DEG) * math.cos(2 * math.pi * (i - TD) / N)
        roll_h = -D(PELVIS_ROLL_DEG) * math.cos(2 * math.pi * (i - _MIDST) / N)
    yaw_c = line_yaw(J["LeftArm"][i], J["RightArm"][i]) - rest_shyaw
    roll_c = line_roll(J["LeftArm"][i], J["RightArm"][i]) - rest_shroll
    if IRL_UPPER:
        tv = (i - TD) / N * len(IRL_ARM)                                 # video frame of the cycle (0 = left heel strike)
        lean = D(irl_curve(IRL_LEAN, tv))
        p_h = lean * 0.4; p_s = p_s1 = p_s2 = lean * 0.2                 # pelvis tilts forward, the spine adds the rest
        aL = irl_curve(IRL_ARM, tv); aR = irl_curve(IRL_ARM, tv + len(IRL_ARM) / 2.0)
        yaw_h = -D(PELVIS_YAW_DEG) * (_AH["Left"][i] - _AH["Right"][i]) / _ADM          # hip of the forward leg forward
        roll_h = -D(PELVIS_ROLL_DEG) * _WD[i]                                           # swing-leg side drops while the other foot carries the weight
        yaw_c = D(IRL_CHEST_YAW) * (aR - aL) / 110.0                                    # chest turns with the arms (right arm forward -> right shoulder forward)
        roll_c = 0.0
    Mp = T(dp) @ Rot(Hc, p_h, roll_h, yaw_h)
    for n in ("c_root_master.x", "c_root.x", "root.x", "c_thigh_b.l", "c_thigh_b.r"): M[n] = Mp
    dy_ = (yaw_c * YAW_SCALE - yaw_h) / 3.0; dr_ = (roll_c - roll_h) / 3.0
    bb = -D(BACK_BEND) * roll_h / D(7.0)                                 # side bend in the lower back, opposite to the pelvis drop
    M1 = Mp @ Rot(P("spine_01.x"), p_s, dr_ + bb, dy_)
    for n in ("c_spine_01.x", "spine_01.x"): M[n] = M1
    M2 = M1 @ Rot(P("spine_02.x"), p_s1, dr_ - bb, dy_)
    for n in ("c_spine_02.x", "spine_02.x"): M[n] = M2
    M3 = M2 @ Rot(P("spine_03.x"), p_s2, dr_, dy_)
    for n in ("c_spine_03.x", "spine_03.x"): M[n] = M3
    ph = p_n - D(HEAD_OFF)                                               # keep the gaze forward instead of at the floor
    ph_n = ph_h = ph * 0.5
    if IRL_UPPER:
        ph_n = D(IRL_NECK); ph_h = -(lean + D(IRL_NECK) + D(IRL_HEAD))   # neck carried forward, face level / chin a touch up
    Mn = M3 @ Rot(P("neck.x"), ph_n, 0.0, -yaw_c * YAW_SCALE * 0.45)
    for n in ("c_neck.x", "neck.x"): M[n] = Mn
    Mh = Mn @ Rot(P("head.x"), ph_h, 0.0, -yaw_c * YAW_SCALE * 0.45)
    for n in ("head_scale_fix.x", "c_head.x", "head.x"): M[n] = Mh
    worst = 0.0; zmin_toe = 9.0
    for s, side in (("l", "Left"), ("r", "Right")):
        sg = 1.0 if s == "l" else -1.0
        # ---- arms: aim the upper arm and forearm along the reference directions (world space)
        du = seg_dir(J[side + "Arm"][i], J[side + "ForeArm"][i])
        df = seg_dir(J[side + "ForeArm"][i], J[side + "Hand"][i])
        dyv = du.y - _DUM[side].y                                                 # < 0 : upper arm forward of its average direction
        w_f = sm(-dyv / 0.3)                                                      # 0 arm back .. 1 arm forward
        kk = ARM_SWING + (1.0 - ARM_SWING) * sm(-dyv / 0.25) * FWD_KEEP            # keep the forward reach (elbow forward), soften mostly the back swing
        du_s = (_DUM[side] + kk * (du - _DUM[side])).normalized()
        qa = du.rotation_difference(du_s); df = (qa @ df).normalized(); du = du_s
        if ELBOW_FLAT > 0.0:
            fl = du.angle(df); ax = du.cross(df)
            if ax.length > 1e-6:
                fl2 = fl + (D(ELBOW_TARGET) - fl) * ELBOW_FLAT
                fl2 += D(HAND_LIFT) * w_f                                     # the forward hand comes up to the chest
                df = (Matrix.Rotation(fl2, 3, ax.normalized()) @ du).normalized()
        if ARM_BIAS != 0.0:
            Rb = Matrix.Rotation(-D(ARM_BIAS), 3, 'X'); du = (Rb @ du).normalized(); df = (Rb @ df).normalized()
        du = Vector((du.x * ELBOW_OUT, du.y, du.z)).normalized()
        if ARM_ABD != 0.0:
            Ra = Matrix.Rotation(-sg * D(ARM_ABD), 3, 'Y'); du = (Ra @ du).normalized(); df = (Ra @ df).normalized()
        df = Vector((df.x * HAND_IN, df.y, df.z)).normalized()
        if HAND_CROSS != 0.0:                                                     # forward hand swings in across the chest
            Rc = Matrix.Rotation(-sg * D(HAND_CROSS) * w_f, 3, 'Z'); df = (Rc @ df).normalized()
        fwd = max(-1.0, min(1.0, -dyv / 0.5))                                     # shoulder girdle follows the arm: forward + a little up with the arm forward, back with it back
        sh_pro = SH_PROTRACT
        if IRL_UPPER:                                                             # arm from the measured swing / elbow tables, carried by the chest
            n_ = len(IRL_ARM); c0 = sum(IRL_ARM) / n_
            ta = tv + (0.0 if s == "l" else n_ / 2.0)
            a = c0 + IRL_ARM_SCALE * (irl_curve(IRL_ARM, ta) - c0) + math.degrees(lean)   # measured vs vertical; the leaning chest carries the arm back by the lean
            b = a + irl_curve(IRL_FLEX, ta)
            ab = D(IRL_ARM_ABD); ar = D(a); br = D(b)
            du0 = Vector((sg * math.sin(ab), -math.sin(ar) * math.cos(ab), -math.cos(ar) * math.cos(ab))).normalized()
            df0 = Vector((sg * math.sin(ab) * math.cos(br), -math.sin(br), -math.cos(br))).normalized()
            best = None
            for sgn in (1.0, -1.0):                                               # internal rotation: forearm turns in towards the body
                cand = (Matrix.Rotation(sgn * D(IRL_ARM_ROT), 3, du0) @ df0).normalized()
                if best is None or sg * cand.x < sg * best.x: best = cand
            R3 = M3.to_3x3()
            du = (R3 @ du0).normalized(); df = (R3 @ best).normalized()
            fwd = max(-1.0, min(1.0, (a - math.degrees(lean) - c0) / 55.0)); sh_pro = IRL_SH
        Pa = P("arm_twist." + s); Pe = P("forearm." + s); Pw = P("hand." + s)
        d0u = (Pe - Pa).normalized(); d0f = (Pw - Pe).normalized()
        Rq = d0u.rotation_difference(du).to_matrix().to_4x4()
        Msh = M3 @ Rot(P("shoulder." + s), 0.0, -sg * D(SH_ELEV) * max(fwd, 0.0), -sg * D(sh_pro) * fwd)
        for n in ("shoulder.", "c_shoulder."): M[n + s] = Msh
        Pm = Msh @ Pa
        Mu = T(Pm) @ Rq @ T(-Pa)
        M["arm_stretch." + s] = Mu
        # the twist bones carry about half the upper-arm skin and follow the arm in the original animations (~90% of its rotation)
        Rtw = Matrix.Identity(4).lerp(Rq, 0.0) if False else Rq.to_quaternion().slerp  # placeholder for readability
        qtw = Matrix.Identity(3).to_quaternion().slerp(Rq.to_quaternion(), TWIST_FOLLOW).to_matrix().to_4x4()
        Mtw = T(Pm) @ qtw @ T(-Pa)
        for n in ("arm_twist.", "c_arm_twist_offset."): M[n + s] = Mtw
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
