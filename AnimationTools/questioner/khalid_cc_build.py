# Khalid's side of the Questioner fight (spec: grabbed struggle, break free, bitten, shove). Poses keyed on a Mixamo skeleton
# (his calm Torch idle body, arms by two-bone IK in his own front / right / up axes), blended frame by frame, then retargeted
# onto Khalid by the melee pipeline (melee_anims_build.export -> mocap_retarget) and exported to Assets/_TempKhalidClips/.
#   "D:/Blender/blender.exe" -b AnimationTools/khalid_cutscene_anims.blend --python AnimationTools/questioner/khalid_cc_build.py -- none [PREVIEW_DIR]
# Unity: KhalidClipImporter.ImportAll("Assets/MainCharacter", "Khalid_CC_Grabbed"), then Tools > Questioner > Build Spec Moveset
# (CloseCombat layer). Clips (30 fps):
#   Khalid_CC_Shove     24 f: hands to the chest (4), shove out at chest height (8), held (12), back (24)
#   Khalid_CC_Grabbed   36 f loop: leaning back, face turned away from the split mouth, gripping the Questioner's forearms
#   Khalid_CC_BreakFree 18 f: from the struggle into a shove (6), recovering (18)
#   Khalid_CC_Bitten    27 f: head snaps away, hand to the bitten neck, recoils (8), recovering (27)
#   Khalid_Switch_Draw  30 f: the right hand to the back pocket and back (right arm only, own layer WeaponSwitch)
import bpy, sys, math
from mathutils import Vector, Matrix

TOOLS = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/AnimationTools/"
exec(open(TOOLS + "melee/melee_anims_build.py").read(), globals())     # MODE "none": only the helpers + pipeline

base = Src("Torch_Idle", mirror=True)
A = base.arm
order = sorted((pb.name for pb in A.pose.bones), key=lambda n: depth(A, n))
mw = A.matrix_world.copy(); mwi = mw.inverted()
X_ = [n for n in order if n.endswith("Hips")][0].replace("Hips", "")
def N(s): return X_ + s

W0 = {n: mw @ m for n, m in base.at(base.f0).items()}
UP = Vector((0, 0, 1))
RIGHT = (W0[N("RightArm")].translation - W0[N("LeftArm")].translation); RIGHT.z = 0; RIGHT.normalize()
FWD = UP.cross(RIGHT).normalized()
ORG = W0[N("Hips")].translation.copy(); ORG.z = 0
DESC = {n: descendants(A, n, order) for n in order}


def P(r, f, u):
    """a point in his own axes: r = his right, f = forward, u = up (metres from the ground under his hips)"""
    return ORG + RIGHT * r + FWD * f + UP * u


def D(r, f, u): return (RIGHT * r + FWD * f + UP * u).normalized()


def rot(W, n, axis, deg, pivot=None):
    h = W[n].translation.copy() if pivot is None else pivot
    T = Matrix.Translation(h) @ Matrix.Rotation(math.radians(deg), 4, axis) @ Matrix.Translation(-h)
    for k in DESC[n]: W[k] = T @ W[k]


def turn(W, n, q):
    h = W[n].translation.copy()
    T = Matrix.Translation(h) @ q.to_matrix().to_4x4() @ Matrix.Translation(-h)
    for k in DESC[n]: W[k] = T @ W[k]


def aim(W, n, d):
    y = W[n].to_3x3().col[1].normalized()
    turn(W, n, y.rotation_difference(d.normalized()))


def ik(W, up, lo, end, target, pole):
    a = W[up].translation.copy()
    l1 = (W[lo].translation - a).length
    l2 = (W[end].translation - W[lo].translation).length
    d = target - a
    dist = max(min(d.length, (l1 + l2) * 0.995), abs(l1 - l2) + 1e-3)
    dn = d.normalized()
    A_ = math.acos(max(-1, min(1, (l1 * l1 + dist * dist - l2 * l2) / (2 * l1 * dist))))
    pv = pole - a; pv = pv - dn * pv.dot(dn)
    if pv.length < 1e-6: pv = -UP.copy()
    pv.normalize()
    elbow = a + dn * math.cos(A_) * l1 + pv * math.sin(A_) * l1
    aim(W, up, elbow - a)
    aim(W, lo, (a + dn * dist) - W[lo].translation)


# palm normal of each hand in its local axes: the T-pose palms face the floor
PALM = {}
for s in ("Left", "Right"):
    R = (A.matrix_world @ A.data.bones[N(s + "Hand")].matrix_local).to_3x3().normalized()
    PALM[s] = max((Vector(v) for v in ((1, 0, 0), (-1, 0, 0), (0, 0, 1), (0, 0, -1))), key=lambda v: (R @ v).dot(-UP))


def orient_hand(W, s, fingers, palm):
    n = N(s + "Hand")
    aim(W, n, fingers)
    R = W[n].to_3x3().normalized()
    y = R.col[1].normalized()
    a = (R @ PALM[s]); a = (a - y * a.dot(y)).normalized()
    w = palm - y * palm.dot(y)
    if w.length < 1e-5: return
    w.normalize()
    ang = math.atan2(a.cross(w).dot(y), a.dot(w))
    turn(W, n, Matrix.Rotation(ang, 3, y).to_quaternion())


def hand(W, s, wrist, fingers, palm, elbow_out=0.35, elbow_down=0.5, elbow_back=0.15):
    sx = 1 if s == "Right" else -1
    sh = W[N(s + "Arm")].translation
    pole = sh + RIGHT * (elbow_out * sx) - UP * elbow_down - FWD * elbow_back
    ik(W, N(s + "Arm"), N(s + "ForeArm"), N(s + "Hand"), wrist, pole)
    orient_hand(W, s, fingers, palm)


def pose_of(W): return {n: mwi @ W[n] for n in order}


def spine(W, pitch=0.0, yaw=0.0, roll=0.0):
    """pitch + = lean forward; yaw + = turn to his right; spread over the three spine bones"""
    for n, k in (("Spine", 0.3), ("Spine1", 0.35), ("Spine2", 0.35)):
        if pitch: rot(W, N(n), RIGHT, pitch * k)
        if yaw: rot(W, N(n), -UP, yaw * k)
        if roll: rot(W, N(n), FWD, roll * k)


def head(W, pitch=0.0, yaw=0.0):
    for n, k in (("Neck", 0.4), ("Head", 0.6)):
        if pitch: rot(W, N(n), RIGHT, pitch * k)
        if yaw: rot(W, N(n), -UP, yaw * k)


# ---------------- key poses ----------------

def k_idle():
    W = dict(W0)
    hand(W, "Right", P(0.24, 0.02, 0.86), D(0.05, 0.1, -1), D(-1, 0, 0), elbow_back=0.4)
    hand(W, "Left", P(-0.24, 0.02, 0.86), D(-0.05, 0.1, -1), D(1, 0, 0), elbow_back=0.4)
    return W


def k_chest(lean=-3):
    W = dict(W0); spine(W, lean)
    for s, sx in (("Right", 1), ("Left", -1)):
        hand(W, s, P(0.17 * sx, 0.22, 1.27), D(0.1 * sx, 0.2, 1), D(0, 1, 0), elbow_out=0.4, elbow_down=0.6, elbow_back=0.3)
    return W


def k_push(reach=0.62, lean=12):
    W = dict(W0); spine(W, lean); head(W, 4)
    for s, sx in (("Right", 1), ("Left", -1)):
        hand(W, s, P(0.17 * sx, reach, 1.30), D(0.05 * sx, 0.15, 1), D(0, 1, 0), elbow_out=0.5, elbow_down=0.6, elbow_back=0.0)
    return W


def k_grabbed(twist=0.0, push=0.0, turn_away=28.0):
    W = dict(W0); spine(W, -9, yaw=twist); head(W, -8, yaw=turn_away)
    for s, sx in (("Right", 1), ("Left", -1)):
        hand(W, s, P(0.13 * sx, 0.30 + push, 1.22), D(-0.6 * sx, 0.6, 0.2), D(-0.3 * sx, 0, -1), elbow_out=0.55, elbow_down=0.45, elbow_back=0.2)
    return W


def k_bitten(k=1.0):
    W = dict(W0); spine(W, -12 * k, yaw=10 * k, roll=-6 * k); head(W, -14 * k, yaw=40 * k)
    neck = W[N("Neck")].translation
    # left hand clamps the bitten side of his neck, the right arm comes up between them
    hand(W, "Left", neck - RIGHT * 0.09 + FWD * 0.03 - UP * 0.02, D(0.2, 0.1, 1), D(1, 0.3, 0), elbow_out=0.35, elbow_down=0.55, elbow_back=0.1)
    hand(W, "Right", P(0.10, 0.36, 1.36), D(-0.4, 0.4, 0.8), D(-0.2, 1, 0), elbow_out=0.4, elbow_down=0.6, elbow_back=0.0)
    return W


def k_pocket(k=1.0):
    """the right hand at his back pocket, torso turned a little to reach it (weapon switch)"""
    W = dict(W0)
    hand(W, "Left", P(-0.24, 0.02, 0.86), D(-0.05, 0.1, -1), D(1, 0, 0), elbow_back=0.4)
    hand(W, "Right", P(0.24 - 0.12 * k, 0.02 - 0.21 * k, 0.86 + 0.10 * k), D(-0.25, -0.2, -1), D(-0.5, 1, 0), elbow_out=0.7, elbow_down=0.2, elbow_back=0.8)
    return W


def k_drawn():
    """coming back from the pocket, what he took out by his right thigh"""
    W = dict(W0)
    hand(W, "Left", P(-0.24, 0.02, 0.86), D(-0.05, 0.1, -1), D(1, 0, 0), elbow_back=0.4)
    hand(W, "Right", P(0.27, 0.10, 0.97), D(0.05, 0.5, -1), D(-1, 0, 0), elbow_out=0.4, elbow_down=0.5, elbow_back=0.4)
    return W


def sequence(keys):
    """keys: [(frame, W)] -> per-frame poses, smoothstep blends between keys (parent-relative)"""
    poses = []
    for (fa, Wa), (fb, Wb) in zip(keys, keys[1:]):
        pa, pb_ = pose_of(Wa), pose_of(Wb)
        for f in range(fa, fb):
            poses.append(blend(A, order, pa, pb_, smooth((f - fa) / (fb - fa))))
    poses.append(pose_of(keys[-1][1]))
    return poses


def no_hook(fi, M, sv): pass


IDLE = k_idle()
CLIPS = {
    "Khalid_CC_Shove": [(0, IDLE), (4, k_chest()), (8, k_push()), (12, k_push(0.60, 11)), (24, IDLE)],
    "Khalid_CC_Grabbed": [(0, k_grabbed()), (9, k_grabbed(7, 0.04, 22)), (18, k_grabbed(0, -0.02, 32)), (27, k_grabbed(-7, 0.04, 24)), (36, k_grabbed())],
    "Khalid_CC_BreakFree": [(0, k_grabbed()), (3, k_chest(-6)), (6, k_push(0.66, 14)), (10, k_push(0.62, 10)), (18, IDLE)],
    "Khalid_CC_Bitten": [(0, k_grabbed()), (4, k_bitten(1.1)), (8, k_bitten(1.0)), (18, k_bitten(0.85)), (27, IDLE)],
    # the grab sequence: bitten and still held (ends in the bitten pose), then he shoves the Questioner off
    "Khalid_CC_BittenHold": [(0, k_grabbed()), (4, k_bitten(1.15)), (8, k_bitten(1.0)), (14, k_bitten(0.92)), (20, k_bitten(1.05)), (26, k_bitten(1.0))],
    # a weapon switch, 1 s: hand to the back pocket by frame 13, what's in the hand swaps there (0.45 s), and back
    "Khalid_Switch_Draw": [(0, IDLE), (7, k_pocket(0.6)), (13, k_pocket(1.0)), (16, k_pocket(1.0)), (23, k_drawn()), (30, IDLE)],
    "Khalid_CC_PushOff": [(0, k_bitten(1.0)), (4, k_chest(-8)), (7, k_push(0.70, 15)), (11, k_push(0.64, 10)), (22, IDLE)],
}
only = ARGS[2].split(",") if len(ARGS) > 2 else None
import traceback
for name, keys in CLIPS.items():
    if only and name not in only: continue
    try:
        export(base, sequence(keys), name, no_hook)
    except Exception:
        print("CC_FAIL", name, flush=True)
        traceback.print_exc()
        sys.stdout.flush()
