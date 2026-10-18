# Retarget the old Questioner clips (Rigify rig "Main.001") onto the new QuestionerRig.   Blender: exec(open(this).read())
# Direction based: every mapped new bone points along the same old joint-to-joint segment as the old bone (bone lengths and
# rest pose may differ), roll follows the old bone. Only Hips gets a position (old hip motion scaled by the leg-length ratio).
# Unmapped bones (Jaw, fingers) get POSE_EXTRA rotations (procedural posture), POSTURE adds the hunch of the concept art.
import bpy, math
from mathutils import Vector, Matrix, Quaternion, Euler

OLD = "Main.001"
NEW = "QuestionerRig"
FPS = 30

CLIPS = [("Main.001|Main|Questioner_Idle", "Questioner_Idle"),
         ("Main.001|Main|Questioner_Walk 1", "Questioner_Walk1"),
         ("Main.001|Main|Questioner_Walk 2 ", "Questioner_Walk2"),
         ("Main.001|Main|Questioner_Attack", "Questioner_Attack"),
         ("Main.001|Main|Questioner_Hurt", "Questioner_Hurt"),
         ("Main.001|Main|Questioner_Die", "Questioner_Die"),
         ("Main.001|Main|Questioner_Weakpoint Hit", "Questioner_WeakpointHit"),
         ("Main.001|Main|Questioner_Sidewalking right", "Questioner_SidewalkRight"),
         ("Main.001|Main|Questioner_Sidewalking left", "Questioner_SidewalkLeft"),
         ("Main.001|Main|Questioner_Waking up from weakpoint hit", "Questioner_WakeUp")]

# new bone: (old bone giving the roll, old joint the segment starts at, old joint it points to)   joint = (bone, "head"/"tail")
MAP = {"Hips": ("DEF-spine", ("DEF-spine", "head"), ("DEF-spine.001", "head")),
       "Spine": ("DEF-spine.001", ("DEF-spine.001", "head"), ("DEF-spine.002", "head")),
       "Spine1": ("DEF-spine.002", ("DEF-spine.002", "head"), ("DEF-spine.003", "head")),
       "Spine2": ("DEF-spine.003", ("DEF-spine.003", "head"), ("DEF-spine.004", "head")),
       "Neck": ("DEF-spine.004", ("DEF-spine.004", "head"), ("DEF-spine.005", "head")),
       "Neck1": ("DEF-spine.005", ("DEF-spine.005", "head"), ("DEF-spine.006", "head")),
       "Head": ("DEF-spine.006", ("DEF-spine.006", "head"), ("DEF-spine.006", "tail"))}
for s, o in (("Left", "L"), ("Right", "R")):
    MAP.update({s + "Shoulder": ("DEF-shoulder." + o, ("DEF-shoulder." + o, "head"), ("DEF-upper_arm." + o, "head")),
                s + "Arm": ("DEF-upper_arm." + o, ("DEF-upper_arm." + o, "head"), ("DEF-forearm." + o, "head")),
                s + "ForeArm": ("DEF-forearm." + o, ("DEF-forearm." + o, "head"), ("DEF-hand." + o, "head")),
                s + "Hand": ("DEF-hand." + o, ("DEF-hand." + o, "head"), ("DEF-hand." + o, "tail")),
                s + "UpLeg": ("DEF-thigh." + o, ("DEF-thigh." + o, "head"), ("DEF-shin." + o, "head")),
                s + "Leg": ("DEF-shin." + o, ("DEF-shin." + o, "head"), ("DEF-foot." + o, "head")),
                s + "Foot": ("DEF-foot." + o, ("DEF-foot." + o, "head"), ("DEF-toe." + o, "head")),
                s + "ToeBase": ("DEF-toe." + o, ("DEF-toe." + o, "head"), ("DEF-toe." + o, "tail"))})

# extra local rotations (degrees, bone-local XYZ euler) layered on top of the transferred motion
POSTURE = {}          # mapped bones, e.g. {"Neck": (20, 0, 0)}
POSE_EXTRA = {}       # unmapped bones (fingers / jaw)

def q_world_rot(m):
    return m.to_3x3().normalized().to_quaternion()

def run(clips=CLIPS, posture=None, extra=None, hip_scale=None):
    posture = POSTURE if posture is None else posture
    extra = POSE_EXTRA if extra is None else extra
    old = bpy.data.objects[OLD]
    new = bpy.data.objects[NEW]
    scene = bpy.context.scene
    obones = old.data.bones
    nbones = new.data.bones
    OW = old.matrix_world
    NW_inv = new.matrix_world.inverted()

    def old_joint_rest(j):
        b = obones[j[0]]
        return OW @ (b.head_local if j[1] == "head" else b.tail_local)

    # rest offsets
    off = {}
    for nb, (src, j0, j1) in MAP.items():
        Rold = q_world_rot(OW @ obones[src].matrix_local)
        Rnew = q_world_rot(new.matrix_world @ nbones[nb].matrix_local)
        d_old = (old_joint_rest(j1) - old_joint_rest(j0)).normalized()
        y_new = Rnew @ Vector((0, 1, 0))
        q0 = y_new.rotation_difference(d_old)
        off[nb] = Rold.inverted() @ (q0 @ Rnew)

    # leg length ratio for the hip motion
    if hip_scale is None:
        old_leg = (old_joint_rest(("DEF-thigh.L", "head")) - old_joint_rest(("DEF-foot.L", "head"))).length
        new_leg = (nbones["LeftUpLeg"].head_local - nbones["LeftFoot"].head_local).length
        hip_scale = new_leg / old_leg
    old_hip_rest = old_joint_rest(("DEF-spine", "head"))
    old_ankle_z = old_joint_rest(("DEF-foot.L", "head")).z
    new_hip_rest = new.matrix_world @ nbones["Hips"].head_local
    new_ankle_z = (new.matrix_world @ nbones["LeftFoot"].head_local).z

    order = [b.name for b in nbones]   # parents come before children in this list
    made = []
    for src_name, dst_name in clips:
        act = bpy.data.actions.get(src_name)
        if act is None:
            print("missing", src_name)
            continue
        old.animation_data.action = act
        f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
        frames = list(range(f0, f1 + 1))
        samples = {b: [] for b in order}
        hip_loc = []
        for f in frames:
            scene.frame_set(f)
            pbs = old.pose.bones
            def J(j):
                pb = pbs[j[0]]
                return OW @ (pb.head if j[1] == "head" else pb.tail)
            Marm = {}
            for name in order:
                b = nbones[name]
                par = b.parent
                if par is None:
                    Marm[name] = b.matrix_local.copy()
                    continue
                rest_rel = par.matrix_local.inverted() @ b.matrix_local
                if name in MAP:
                    src, j0, j1 = MAP[name]
                    R = q_world_rot(OW @ pbs[src].matrix) @ off[name]
                    D = (J(j1) - J(j0))
                    if D.length > 1e-6:
                        R = (R @ Vector((0, 1, 0))).rotation_difference(D.normalized()) @ R
                    if name in posture:
                        R = R @ Euler([math.radians(v) for v in posture[name]]).to_quaternion()
                    Rarm = (NW_inv.to_3x3().to_quaternion() @ R)
                    if name == "Hips":
                        ph = J(("DEF-spine", "head"))
                        pos = Vector(((ph.x) * hip_scale, (ph.y) * hip_scale, new_ankle_z + (ph.z - old_ankle_z) * hip_scale))
                        pos = pos + Vector((new_hip_rest.x, new_hip_rest.y, 0)) - Vector((old_hip_rest.x, old_hip_rest.y, 0)) * hip_scale
                        head = NW_inv @ pos
                    else:
                        head = (Marm[par.name] @ rest_rel).translation
                    Marm[name] = Matrix.Translation(head) @ Rarm.to_matrix().to_4x4()
                else:
                    M = Marm[par.name] @ rest_rel
                    if name in extra:
                        M = M @ Euler([math.radians(v) for v in extra[name]]).to_matrix().to_4x4()
                    Marm[name] = M
            for name in order:
                b = nbones[name]
                if b.parent is None:
                    basis = Matrix.Identity(4)
                else:
                    basis = b.matrix_local.inverted() @ b.parent.matrix_local @ Marm[b.parent.name].inverted() @ Marm[name]
                samples[name].append(basis)
        # write the action
        na = bpy.data.actions.get(dst_name)
        if na:
            bpy.data.actions.remove(na)
        na = bpy.data.actions.new(dst_name)
        na.use_fake_user = True
        slot = na.slots.new(id_type="OBJECT", name=NEW)
        layer = na.layers.new("Layer")
        strip = layer.strips.new(type="KEYFRAME")
        cb = strip.channelbag(slot, ensure=True)
        for name in order:
            if nbones[name].parent is None:
                continue
            pb = new.pose.bones[name]
            pb.rotation_mode = "QUATERNION"
            mats = samples[name]
            quats = []
            prev = None
            for m in mats:
                q = m.to_quaternion()
                if prev is not None and prev.dot(q) < 0:
                    q = -q
                quats.append(q)
                prev = q
            path = 'pose.bones["%s"].rotation_quaternion' % name
            for i in range(4):
                fc = cb.fcurves.new(path, index=i, group_name=name)
                fc.keyframe_points.add(len(frames))
                co = []
                for f, q in zip(frames, quats):
                    co += [f, q[i]]
                fc.keyframe_points.foreach_set("co", co)
                fc.update()
            if name == "Hips":
                path = 'pose.bones["%s"].location' % name
                for i in range(3):
                    fc = cb.fcurves.new(path, index=i, group_name=name)
                    fc.keyframe_points.add(len(frames))
                    co = []
                    for f, m in zip(frames, mats):
                        co += [f, m.translation[i]]
                    fc.keyframe_points.foreach_set("co", co)
                    fc.update()
        for fc in cb.fcurves:
            for kp in fc.keyframe_points:
                kp.interpolation = "LINEAR"
        made.append((dst_name, f0, f1))
    return made, hip_scale

def show(action_name, frame=None):
    new = bpy.data.objects[NEW]
    if new.animation_data is None:
        new.animation_data_create()
    a = bpy.data.actions[action_name]
    new.animation_data.action = a
    new.animation_data.action_slot = a.slots[0]
    if frame is not None:
        bpy.context.scene.frame_set(frame)
