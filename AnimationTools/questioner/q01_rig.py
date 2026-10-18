# The Questioner (new) - skeleton.   Blender: exec(open(this).read())
# Front = -Y (exports to Unity +Z), up = +Z, metres.  Standing height ~2.05 m, arms in an A-pose (~42 deg below horizontal).
# Mixamo-style bone names so the model could later be made Humanoid in Unity, plus Jaw and finger bones.
import bpy, math
from mathutils import Vector, Matrix

RIG = "QuestionerRig"

# ---------------------------------------------------------------- joint layout
J = {}
J["Root"] = Vector((0, 0, 0))
# spine (slightly S-curved, very thin creature)
J["Hips"] = Vector((0, 0.010, 1.040))
J["Spine"] = Vector((0, 0.018, 1.165))
J["Spine1"] = Vector((0, 0.020, 1.300))
J["Spine2"] = Vector((0, 0.012, 1.450))
J["Neck"] = Vector((0, 0.010, 1.600))
J["Neck1"] = Vector((0, 0.004, 1.705))
J["Head"] = Vector((0, -0.004, 1.800))
J["HeadTop"] = Vector((0, -0.010, 2.050))
J["Jaw"] = Vector((0, -0.020, 1.835))          # jaw hinge (just in front of the ear line)
J["JawTip"] = Vector((0, -0.110, 1.690))
# legs
for s, sx in (("Left", 1), ("Right", -1)):
    J[s + "UpLeg"] = Vector((0.105 * sx, 0.012, 1.075))
    J[s + "Leg"] = Vector((0.112 * sx, 0.000, 0.580))
    J[s + "Foot"] = Vector((0.118 * sx, 0.030, 0.090))
    J[s + "ToeBase"] = Vector((0.124 * sx, -0.140, 0.022))
    J[s + "ToeEnd"] = Vector((0.126 * sx, -0.225, 0.018))
# arms, A-pose
ARM_DROP = math.radians(42)
for s, sx in (("Left", 1), ("Right", -1)):
    sh = Vector((0.045 * sx, 0.005, 1.575))            # clavicle start
    J[s + "Shoulder"] = sh
    up = Vector((0.200 * sx, 0.020, 1.585))             # shoulder joint
    J[s + "Arm"] = up
    d = Vector((math.cos(ARM_DROP) * sx, 0.0, -math.sin(ARM_DROP)))
    J[s + "ForeArm"] = up + d * 0.375
    J[s + "Hand"] = J[s + "ForeArm"] + d * 0.365 + Vector((0, -0.010, 0))
    hand_dir = d
    J[s + "HandEnd"] = J[s + "Hand"] + hand_dir * 0.115   # knuckle line centre
    # fingers: spread front (-Y, index) to back (+Y, pinky); long bony fingers
    across = Vector((0, -1, 0))                         # index side
    palm_n = hand_dir.cross(across).normalized() * sx   # roughly points down/back - only used to curl slightly
    lengths = {"Index": (0.062, 0.046, 0.040), "Middle": (0.068, 0.050, 0.044), "Ring": (0.064, 0.047, 0.041), "Pinky": (0.052, 0.038, 0.034)}
    offs = {"Index": 0.030, "Middle": 0.010, "Ring": -0.010, "Pinky": -0.028}
    base_len = {"Index": 0.108, "Middle": 0.115, "Ring": 0.110, "Pinky": 0.098}
    for f in ("Index", "Middle", "Ring", "Pinky"):
        p = J[s + "Hand"] + hand_dir * base_len[f] + across * offs[f]
        fd = (hand_dir + across * offs[f] * 1.2).normalized()
        for i, L in enumerate(lengths[f]):
            J["%sHand%s%d" % (s, f, i + 1)] = p.copy()
            p = p + fd * L
        J["%sHand%s4" % (s, f)] = p
    # thumb from the wrist, toward the front and the palm side
    tb = J[s + "Hand"] + hand_dir * 0.025 + across * 0.022 + Vector((0, 0, -0.010))
    td = (hand_dir * 0.6 + across * 0.75 + Vector((0, 0, -0.15))).normalized()
    p = tb
    for i, L in enumerate((0.045, 0.040, 0.034)):
        J["%sHandThumb%d" % (s, i + 1)] = p.copy()
        p = p + td * L
    J["%sHandThumb4" % s] = p

# bone: (name, head joint, tail joint, parent, connected)
BONES = [("Root", "Root", None, None, False),
         ("Hips", "Hips", "Spine", "Root", False),
         ("Spine", "Spine", "Spine1", "Hips", True),
         ("Spine1", "Spine1", "Spine2", "Spine", True),
         ("Spine2", "Spine2", "Neck", "Spine1", True),
         ("Neck", "Neck", "Neck1", "Spine2", True),
         ("Neck1", "Neck1", "Head", "Neck", True),
         ("Head", "Head", "HeadTop", "Neck1", True),
         ("Jaw", "Jaw", "JawTip", "Head", False)]
for s in ("Left", "Right"):
    BONES += [(s + "UpLeg", s + "UpLeg", s + "Leg", "Hips", False),
              (s + "Leg", s + "Leg", s + "Foot", s + "UpLeg", True),
              (s + "Foot", s + "Foot", s + "ToeBase", s + "Leg", True),
              (s + "ToeBase", s + "ToeBase", s + "ToeEnd", s + "Foot", True),
              (s + "Shoulder", s + "Shoulder", s + "Arm", "Spine2", False),
              (s + "Arm", s + "Arm", s + "ForeArm", s + "Shoulder", True),
              (s + "ForeArm", s + "ForeArm", s + "Hand", s + "Arm", True),
              (s + "Hand", s + "Hand", s + "HandEnd", s + "ForeArm", True)]
    for f in ("Thumb", "Index", "Middle", "Ring", "Pinky"):
        for i in (1, 2, 3):
            n = "%sHand%s%d" % (s, f, i)
            BONES.append((n, n, "%sHand%s%d" % (s, f, i + 1), s + "Hand" if i == 1 else "%sHand%s%d" % (s, f, i - 1), i > 1))


def build_rig():
    old = bpy.data.objects.get(RIG)
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    arm_data = bpy.data.armatures.new(RIG)
    ob = bpy.data.objects.new(RIG, arm_data)
    bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    for o in bpy.context.selected_objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm_data.edit_bones
    for name, h, t, parent, conn in BONES:
        b = eb.new(name)
        b.head = J[h]
        b.tail = J[t] if t else J[h] + Vector((0, 0, 0.15))
        if parent:
            b.parent = eb[parent]
            b.use_connect = conn
    # rolls: Z axis toward the front (-Y) for spine/legs/arms, fingers follow the hand
    for b in eb:
        b.align_roll(Vector((0, -1, 0)))
    for b in eb:
        if "Hand" in b.name and b.name[-1].isdigit():
            b.roll = eb[b.name.split("Hand")[0] + "Hand"].roll
    bpy.ops.object.mode_set(mode="OBJECT")
    arm_data.display_type = "STICK"
    ob.show_in_front = True
    for b in arm_data.bones:
        b.use_deform = b.name != "Root"
    return ob

if globals().get("BUILD_RIG", True):
    rig = build_rig()
    result = {"bones": len(rig.data.bones)}
