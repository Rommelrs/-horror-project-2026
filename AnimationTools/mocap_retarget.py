# Faithful rotation retarget of an imported Mixamo armature onto Khalid's rig ("rig").
# Per segment: Khalid's bone gets the SAME world-space rotation change as the Mixamo bone has relative to its own rest (T-)pose,
# after a one-time rest-pose correction C that lines Khalid's rest segment up with Mixamo's rest segment.
#     R_khalid(t) = R_mixamo(t) * R_mixamo_rest^-1 * C * R_khalid_rest
# Only the hips position is scaled (leg-length ratio). No IK, no foot locking, no curves/offsets added to the motion.
import bpy, math
from mathutils import Matrix, Vector, Quaternion

MX = "mixamorig:"
# (Khalid bones that move as one rigid segment, Khalid joint at the segment start, Khalid point the segment points to,
#  Mixamo bone, Mixamo point the segment points to)    points: bone name = its head, "name>" = its tail
def side(s, S):
    return [
        (["c_thigh_fk." + s, "thigh_stretch." + s, "thigh_twist." + s], "c_thigh_fk." + s, "leg_fk." + s, S + "UpLeg", S + "Leg"),
        (["c_leg_fk." + s, "leg_fk." + s, "leg_stretch." + s, "leg_twist." + s], "leg_fk." + s, "foot." + s, S + "Leg", S + "Foot"),
        (["foot." + s], "foot." + s, "toes_01." + s, S + "Foot", S + "ToeBase"),
        (["toes_01." + s], "toes_01." + s, "toes_01." + s + ">", S + "ToeBase", S + "Toe_End"),
        (["c_shoulder." + s, "shoulder." + s], "shoulder." + s, "arm_twist." + s, S + "Shoulder", S + "Arm"),
        (["arm_stretch." + s], "arm_twist." + s, "forearm." + s, S + "Arm", S + "ForeArm"),
        (["forearm_stretch." + s, "forearm_twist." + s, "forearm." + s], "forearm." + s, "hand." + s, S + "ForeArm", S + "Hand"),
        (["hand." + s, "c_index1_base." + s, "index1_rot." + s, "c_index1." + s, "c_thumb1_base." + s, "thumb1_rot." + s, "c_thumb1." + s], "hand." + s, "hand." + s + ">", S + "Hand", S + "HandMiddle1"),
        (["index1." + s, "index2_rot." + s], "index1." + s, "c_index2." + s, S + "HandIndex1", S + "HandIndex2"),
        (["c_index2." + s, "index3_rot." + s], "c_index2." + s, "c_index3." + s, S + "HandIndex2", S + "HandIndex3"),
        (["c_index3." + s], "c_index3." + s, "c_index3." + s + ">", S + "HandIndex3", S + "HandIndex4"),
        (["thumb1." + s, "thumb2_rot." + s], "thumb1." + s, "c_thumb2." + s, S + "HandThumb1", S + "HandThumb2"),
        (["c_thumb2." + s, "thumb3_rot." + s], "c_thumb2." + s, "c_thumb3." + s, S + "HandThumb2", S + "HandThumb3"),
        (["c_thumb3." + s], "c_thumb3." + s, "c_thumb3." + s + ">", S + "HandThumb3", S + "HandThumb4"),
    ]
SEGS = {
    "pelvis": (["c_root_master.x", "c_root.x", "root.x", "c_thigh_b.l", "c_thigh_b.r"], None, "spine_01.x", "Hips", "Spine"),
    "spine1": (["c_spine_01.x", "spine_01.x"], "spine_01.x", "spine_02.x", "Spine", "Spine1"),
    "spine2": (["c_spine_02.x", "spine_02.x"], "spine_02.x", "spine_03.x", "Spine1", "Spine2"),
    "spine3": (["c_spine_03.x", "spine_03.x"], "spine_03.x", "neck.x", "Spine2", "Neck"),
    "neck": (["c_neck.x", "neck.x"], "neck.x", "head.x", "Neck", "Head"),
    "head": (["head_scale_fix.x", "c_head.x", "head.x"], "head.x", "head.x>", "Head", "HeadTop_End"),
}
for s, S in (("l", "Left"), ("r", "Right")):
    for k, seg in enumerate(side(s, S)): SEGS["%s_%d" % (s, k)] = seg
PARENT = {"pelvis": None, "spine1": "pelvis", "spine2": "spine1", "spine3": "spine2", "neck": "spine3", "head": "neck"}
for s in ("l", "r"):
    PARENT.update({s + "_0": "pelvis", s + "_1": s + "_0", s + "_2": s + "_1", s + "_3": s + "_2",
                   s + "_4": "spine3", s + "_5": s + "_4", s + "_6": s + "_5", s + "_7": s + "_6",
                   s + "_8": s + "_7", s + "_9": s + "_8", s + "_10": s + "_9", s + "_11": s + "_7", s + "_12": s + "_11", s + "_13": s + "_12"})
ORDER = ["pelvis", "spine1", "spine2", "spine3", "neck", "head"] + ["%s_%d" % (s, k) for s in ("l", "r") for k in range(14)]
TWIST_FOLLOW = 0.9          # Khalid's upper-arm twist bones carry ~half the sleeve skin and follow 90% of the upper-arm rotation (rig convention)

def _kpt(rig, name):        # Khalid rest point (armature space)
    if name.endswith(">"): return rig.data.bones[name[:-1]].tail_local.copy()
    return rig.data.bones[name].head_local.copy()
def _mrest(arm, name):      # Mixamo rest point (world)
    if name.endswith(">"): return arm.matrix_world @ arm.data.bones[MX + name[:-1]].tail_local
    return arm.matrix_world @ arm.data.bones[MX + name].head_local
def _mpose(arm, name): return arm.matrix_world @ arm.pose.bones[MX + name].head
def _rot(m): return m.to_3x3().normalized().to_quaternion()

def prepare(arm, rig):
    W = rig.matrix_world.inverted()
    prep = {}
    for key, (kb, kj, kt, mb, mt) in SEGS.items():
        rm_rest = _rot(W @ arm.matrix_world @ arm.data.bones[MX + mb].matrix_local)
        if key == "pelvis":
            k0 = (_kpt(rig, "c_thigh_fk.l") + _kpt(rig, "c_thigh_fk.r")) / 2; m0 = (_mrest(arm, "LeftUpLeg") + _mrest(arm, "RightUpLeg")) / 2
        else:
            k0 = _kpt(rig, kj); m0 = _mrest(arm, mb)
        dk = (_kpt(rig, kt) - k0).normalized(); dm = (W.to_3x3() @ (_mrest(arm, mt) - m0)).normalized()
        # rest-pose correction only where the two rest POSES differ: Mixamo is a T-pose with straight legs, Khalid's arms hang ~10 deg
        # lower with a slightly bent elbow. Pelvis/spine/neck/head/clavicles/feet/toes stand the same way on both (upright, feet flat),
        # their segment vectors only differ because the bones sit differently in the body, so they get no correction.
        C = dk.rotation_difference(dm) if key[1:] in ("_0", "_1", "_5", "_6") else Quaternion()
        prep[key] = (rm_rest, C, k0)
    for s in ("l", "r"):                                  # hand + fingers keep their rest angle to the forearm
        for k in range(7, 14):
            rm_rest, C, k0 = prep["%s_%d" % (s, k)]
            prep["%s_%d" % (s, k)] = (rm_rest, prep[s + "_6"][1], k0)
    lk = (_kpt(rig, "leg_fk.l") - _kpt(rig, "c_thigh_fk.l")).length + (_kpt(rig, "foot.l") - _kpt(rig, "leg_fk.l")).length
    lm = (_mrest(arm, "LeftLeg") - _mrest(arm, "LeftUpLeg")).length + (_mrest(arm, "LeftFoot") - _mrest(arm, "LeftLeg")).length
    prep["_scale"] = lk / lm
    prep["_hip_rest_m"] = W @ ((_mrest(arm, "LeftUpLeg") + _mrest(arm, "RightUpLeg")) / 2)
    return prep

def pose_frame(arm, rig, prep):
    W = rig.matrix_world.inverted()
    M = {}
    for key in ORDER:
        kb, kj, kt, mb, mt = SEGS[key]
        rm_rest, C, k0 = prep[key]
        rm = _rot(W @ arm.matrix_world @ arm.pose.bones[MX + mb].matrix)
        dq = rm @ rm_rest.inverted() @ C                  # world rotation change of this segment
        R = dq.to_matrix().to_4x4()
        if key == "pelvis":
            hm = W @ ((_mpose(arm, "LeftUpLeg") + _mpose(arm, "RightUpLeg")) / 2)
            newp = k0 + prep["_scale"] * (hm - prep["_hip_rest_m"])
        else:
            newp = M[PARENT[key]] @ k0                    # joint follows its parent segment (forward kinematics)
        Mk = Matrix.Translation(newp) @ R @ Matrix.Translation(-k0)
        M[key] = Mk
        for b in kb: M[b] = Mk
        if key in ("l_5", "r_5"):                         # upper-arm twist bones
            s = key[0]
            q = Quaternion().slerp(dq, TWIST_FOLLOW).to_matrix().to_4x4()
            Mt = Matrix.Translation(newp) @ q @ Matrix.Translation(-k0)
            for b in ("arm_twist." + s, "c_arm_twist_offset." + s): M[b] = Mt
    return M

def _depth(pb):
    n = 0
    while pb.parent: pb = pb.parent; n += 1
    return n

def apply_pose(rig, M):
    cur = -1
    for pb in sorted(rig.pose.bones, key=_depth):
        d = _depth(pb)
        if d != cur: bpy.context.view_layer.update(); cur = d
        pb.matrix = M.get(pb.name, Matrix.Identity(4)) @ rig.data.bones[pb.name].matrix_local
    bpy.context.view_layer.update()

def retarget(arm_name, action_name, rig_name="rig"):
    sc = bpy.context.scene
    arm = bpy.data.objects[arm_name]; rig = bpy.data.objects[rig_name]
    src = arm.animation_data.action
    f0, f1 = int(src.frame_range[0]), int(src.frame_range[1])
    ad = rig.animation_data or rig.animation_data_create()
    old = (ad.action, ad.action_slot, ad.use_nla, sc.frame_current)
    ad.use_nla = False; ad.action = None
    prep = prepare(arm, rig)
    if action_name in bpy.data.actions: bpy.data.actions.remove(bpy.data.actions[action_name])
    act = bpy.data.actions.new(action_name); act.use_fake_user = True
    ad.action = act
    prev = {}
    for f in range(f0, f1 + 1):
        sc.frame_set(f)                                   # evaluates the Mixamo armature at its own frame
        ad.action = None
        apply_pose(rig, pose_frame(arm, rig, prep))
        for pb in rig.pose.bones:
            q = pb.rotation_quaternion
            if pb.name in prev and q.dot(prev[pb.name]) < 0: pb.rotation_quaternion = (-q.w, -q.x, -q.y, -q.z)
            prev[pb.name] = pb.rotation_quaternion.copy()
        ad.action = act
        for pb in rig.pose.bones:
            for prop in ("location", "rotation_quaternion", "scale"): pb.keyframe_insert(prop, frame=f, group=pb.name)
    cb = act.layers[0].strips[0].channelbag(act.slots[0])
    for fc in cb.fcurves:
        fc.modifiers.new('CYCLES'); fc.update()
    ad.use_nla = old[2]
    sc.frame_set(old[3])
    return {"frames": [f0, f1], "scale": prep["_scale"], "rest_corrections_deg": {k: round(math.degrees(v[1].angle), 1) for k, v in prep.items() if not k.startswith("_")}}
