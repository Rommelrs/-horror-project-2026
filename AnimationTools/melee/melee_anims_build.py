# Background Blender (khalid_cutscene_anims.blend, NOT saved): build the pipe-melee clips from Mixamo mocap, retarget them
# onto Khalid and export to Assets/_TempKhalidClips/ (Unity: KhalidClipImporter.ImportAll("Assets/MainCharacter", loops)).
#
# v3 - TWO-HANDED (user: "why does he hold it in one hand?"). Composed in Mixamo armature space before the retarget:
#   stance / strafe walks = Mixamo "Standing Torch" idle + walks (calm, upright body; mirrored, which also swaps the strafe
#       directions) with BOTH ARMS replaced by the two-handed club stance (first frame of "Two Hand Club Combo": fists
#       stacked in front of the right chest, the weapon standing up in front of the right shoulder). The arms are copied
#       relative to the chest, so they ride on the walking torso. (v3 used the batter's hold from "Baseball Idle": the bat
#       lay back over his head and, with his head turned forward, across his face - user: "holding it weirdly".)
#   attack = "Baseball Base Hit - Single", turned 90 deg (the batter is captured side-on; turned, his chest faces forward
#       and the bat meets the ball straight in front of him), hips travel removed:
#       8 frames easing from the stance into the swing (source frame 8) - source 8..39: the load (turning right), the
#       swipe across (fast 24-37, contact ~31) and the follow-through, cut before his left hand lets go - 3 frames held -
#       24 frames easing back into the stance (smoothstep, per bone, parent-relative). 67 frames = 2.2 s.
#   Khalid_Melee_Idle / WalkF / WalkB / WalkL / WalkR (loops) + Khalid_Melee_Attack
# Old clips stay in the project unused (axe set: Khalid_Melee_SwingH / SwingB / Equip).
#   "D:/Blender/blender.exe" -b AnimationTools/khalid_cutscene_anims.blend --python AnimationTools/melee/melee_anims_build.py -- all [PREVIEW_DIR]
import bpy, os, sys, math
from mathutils import Matrix, Vector, Quaternion

TOOLS = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/AnimationTools/"
OUT = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/Assets/_TempKhalidClips/"
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["all"]
MODE = ARGS[0]
PREVIEW = ARGS[1] if len(ARGS) > 1 else None

BB_YAW = 90.0                     # turns the batter from side-on to facing forward (-Y)
# Retimed for weight, not slow motion (user: "feels slow motioned instead of actual heaviness"): the batter's slow even
# load becomes a quicker draw-back that decelerates into a held top (anticipation), the strike plays faster than the mocap,
# the follow-through settles and the recovery is shorter. Output frames (30 fps):
LOAD_IN = 6                       # stance -> source frame 8
LOAD_FROM, LOAD_TO, LOAD_N = 8, 23, 10        # draw-back, eased out into the top
TOP_HOLD = 4                      # anticipation
STRIKE_FROM, STRIKE_TO, STRIKE_N = 24, 37, 11 # the swing, ~1.3x the mocap
FOLLOW_TO, FOLLOW_N = 39, 5       # follow-through, decelerating
SETTLE = 3                        # weight settles
RECOVER = 18                      # back into the stance
CONTACT_SRC = 31

STANCE = {
    # out name: (torch fbx for the body)
    "Khalid_Melee_Idle": "Torch_Idle",
    "Khalid_Melee_WalkF": "Torch_Walk_Forward",
    "Khalid_Melee_WalkB": "Torch_Walk_Back",
    "Khalid_Melee_WalkL": "Torch_Walk_Right",      # mirrored
    "Khalid_Melee_WalkR": "Torch_Walk_Left",
}
ARM_KEYS = ("Shoulder", "Arm", "ForeArm", "Hand")  # every bone whose name has one of these (incl. fingers) is an arm bone


def smooth(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


class Src:
    """an imported Mixamo clip: per-frame armature-space bone matrices, optionally mirrored / turned / held in place"""
    def __init__(self, name, mirror=False, yaw=0.0, strip=False):
        bpy.ops.import_scene.fbx(filepath=TOOLS + "mixamo/" + name + ".fbx")
        self.arm = [o for o in bpy.context.selected_objects if o.type == 'ARMATURE'][0]
        self.arm.name = "SRC_" + name
        for o in bpy.context.selected_objects: o.hide_render = True
        act = self.arm.animation_data.action
        self.f0, self.f1 = int(act.frame_range[0]), int(act.frame_range[1])
        sc = bpy.context.scene
        mw = self.arm.matrix_world
        mwi = mw.inverted()
        S = Matrix.Diagonal((-1.0, 1.0, 1.0, 1.0))
        names = [pb.name for pb in self.arm.pose.bones]
        hips = [n for n in names if n.endswith("Hips")][0]
        raw = {}
        for f in range(self.f0, self.f1 + 1):
            sc.frame_set(f)
            raw[f] = {pb.name: pb.matrix.copy() for pb in self.arm.pose.bones}

        def partner(n):
            if not mirror: return n
            if "Left" in n: return n.replace("Left", "Right")
            if "Right" in n: return n.replace("Right", "Left")
            return n

        pivot = (mw @ raw[self.f0][hips]).translation.copy()
        pivot.z = 0
        R = Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(yaw), 4, 'Z') @ Matrix.Translation(-pivot)
        self.poses = {}
        base = None
        for f in range(self.f0, self.f1 + 1):
            p = {n: (S @ raw[f][partner(n)] @ S if mirror else raw[f][n].copy()) for n in names}
            if yaw:
                p = {n: mwi @ (R @ (mw @ m)) for n, m in p.items()}
            if strip:
                h = mw @ p[hips]
                if base is None: base = h.translation.copy()
                d = Vector((base.x - h.translation.x, base.y - h.translation.y, 0.0))
                T = Matrix.Translation(d)
                p = {n: mwi @ (T @ (mw @ m)) for n, m in p.items()}
            self.poses[f] = p

    def at(self, f):
        return self.poses[max(self.f0, min(self.f1, f))]

    def sample(self, arm, order, f):
        """pose at a fractional source frame (parent-relative blend of the two neighbours)"""
        a = int(math.floor(f))
        t = f - a
        return self.at(a) if t < 1e-3 else blend(arm, order, self.at(a), self.at(a + 1), t)


def parent_name(arm, n):
    pb = arm.pose.bones[n].parent
    return pb.name if pb else None


def to_local(arm, pose):
    out = {}
    for n, m in pose.items():
        par = parent_name(arm, n)
        out[n] = (pose[par].inverted() @ m) if par else m.copy()
    return out


def from_local(arm, local, order):
    out = {}
    for n in order:
        par = parent_name(arm, n)
        out[n] = (out[par] @ local[n]) if par else local[n].copy()
    return out


def blend(arm, order, pa, pb_, t):
    """per-bone blend of two poses in parent-relative space (slerped rotations), recomposed parent-first"""
    la, lb = to_local(arm, pa), to_local(arm, pb_)
    lo = {}
    for n in order:
        a, b = la[n].decompose(), lb[n].decompose()
        loc = a[0].lerp(b[0], t)
        rot = a[1].slerp(b[1], t) if a[1].dot(b[1]) >= 0 else a[1].slerp(-b[1], t)
        sca = a[2].lerp(b[2], t)
        lo[n] = Matrix.LocRotScale(loc, rot, sca)
    return from_local(arm, lo, order)


def with_arms(arm, order, body, arms_pose):
    """the body pose with every arm bone taken (parent-relative) from arms_pose"""
    lb, la = to_local(arm, body), to_local(arm, arms_pose)
    lo = {n: (la[n] if any(k in n.split(":")[-1] for k in ARM_KEYS) else lb[n]) for n in order}
    return from_local(arm, lo, order)


def depth(arm, n):
    d = 0
    pb = arm.pose.bones[n]
    while pb.parent: pb = pb.parent; d += 1
    return d


def bake(arm, poses, name):
    """key 'arm' with one pose per frame (1..len) as a new action; returns frame range"""
    sc = bpy.context.scene
    act = bpy.data.actions.new(name)
    arm.animation_data.action = act
    order = sorted((pb.name for pb in arm.pose.bones), key=lambda n: depth(arm, n))
    levels = sorted(set(depth(arm, n) for n in order))
    for i, pose in enumerate(poses):
        f = i + 1
        sc.frame_set(f)
        for d in levels:
            for n in order:
                if depth(arm, n) == d: arm.pose.bones[n].matrix = pose[n]
            bpy.context.view_layer.update()
        for pb in arm.pose.bones:
            pb.keyframe_insert("location", frame=f); pb.keyframe_insert("rotation_quaternion", frame=f); pb.keyframe_insert("scale", frame=f)
    act.use_frame_range = True; act.frame_start = 1; act.frame_end = len(poses)
    return 1, len(poses)


def render_preview(out_name, f0, f1):
    sc = bpy.context.scene
    for o in bpy.data.objects:
        if o.type == 'MESH' and o.name != "SM_MainCharacter": o.hide_render = True
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'TEXTURE'
    sc.render.resolution_x, sc.render.resolution_y, sc.render.resolution_percentage = 240, 320, 100
    cam = bpy.data.objects["mx_cam"]; sc.camera = cam; cam.data.lens = 50
    # stand-in pipe on the baked path (grip point G = right fist, from 15 cm below to the head 66 cm above)
    rig = bpy.data.objects["rig"]
    if "PV_Pipe" not in bpy.data.objects:
        bpy.ops.mesh.primitive_cylinder_add(radius=0.02, depth=0.81, vertices=10)
        pv = bpy.context.active_object; pv.name = "PV_Pipe"
        pv.data.transform(Matrix.Translation((0, 0, 0.255)))
    pv = bpy.data.objects["PV_Pipe"]; pv.hide_render = False      # (the mesh-hiding loop above catches it from the 2nd clip on)
    track = LAST_TRACK.get(out_name, {})
    n = 20 if "Attack" in out_name else 10 if "Raise" in out_name else 6
    for i in range(n):
        f = f0 + round((f1 - f0) * i / max(1, n - 1))
        sc.frame_set(f)
        if (f - f0) in track:
            G, D = track[f - f0]
            Gw = rig.matrix_world @ G
            Dw = (rig.matrix_world.to_3x3() @ D).normalized()
            pv.location = Gw
            pv.rotation_mode = 'QUATERNION'
            pv.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(Dw)
        for view, off, c in (("front", (1.5, -2.1, 0.35), (0, 0, 1.25)), ("side", (-2.6, -0.2, 0.2), (0, 0, 1.25)),
                             ("back", (0.9, 2.4, 0.55), (0, 0, 1.25))):
            c = Vector(c)
            cam.location = c + Vector(off)
            cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
            sc.render.filepath = os.path.join(PREVIEW, "%s_%s_%02d.png" % (out_name, view, i))
            bpy.ops.render.render(write_still=True)


def export(holder, poses, out_name, hook):
    f0, f1 = bake(holder.arm, poses, holder.arm.name + "_" + out_name)
    info = retarget_with(holder.arm.name, out_name, hook)
    act = bpy.data.actions[out_name]
    cb = act.layers[0].strips[0].channelbag(act.slots[0])
    for fc in cb.fcurves:                      # no cycling
        for m in list(fc.modifiers): fc.modifiers.remove(m)
        fc.update()
    act.use_frame_range = True; act.frame_start = f0; act.frame_end = f1
    if PREVIEW:
        bpy.data.objects["rig"].animation_data.action = act
        render_preview(out_name, f0, f1)
    os.makedirs(OUT, exist_ok=True)
    export_action(out_name, OUT + out_name + ".fbx")
    print("MELEE_ANIM_DONE", out_name, info["frames"], os.path.getsize(OUT + out_name + ".fbx"))


exec(open(TOOLS + "mocap_retarget.py").read(), globals())
exec(open(TOOLS + "melee/grip_bake.py").read(), globals())
exec(open(TOOLS + "melee/qm_source.py").read(), globals())
exec(open(TOOLS + "blender_helpers.py").read(), globals())


def stance_hook(fi, M, sv):
    """both fists on the pipe, held up by his right shoulder - in his body-facing axes, riding the walk's bob"""
    fr = sv.root_frame(M)
    sv.solve(M, sv.to_world(fr, PIPE_STANCE[0]), sv.dir_world(fr, PIPE_STANCE[1]), fi)


_head0 = {}


def attack_hook(fi, M, sv):
    """the swing's pipe path (body-facing axes, following his crouch / step), head kept facing forward"""
    if fi == 0:
        _head0["head"] = M["head"].to_3x3().normalized().copy()
        _head0["neck"] = M["neck"].to_3x3().normalized().copy()
    fr = sv.root_frame(M)
    g, d = attack_pipe(fi)
    sv.solve(M, sv.to_world(fr, g), sv.dir_world(fr, d), fi)
    sv.hold_head(M, _head0["head"], _head0["neck"])
exec(bpy.app.driver_namespace["export_code"], globals())

# ---- "qm": the user's own QuickMagic takes (Shamkha Refs/*_GirlFBX.fbx, Biped skeleton):
#   asdsad_GirlFBX            f8-40 carrying the pipe upright, f40-72 raising it (-> Hold, Raise)
#   Walking while having pipe f18-58 one stride carrying it (-> HoldWalk, upper body over his own walk)
#   Runninng while having pipe f25-51 (not used: one accelerating stride; HoldRun is the carry pose held steady)
#   Attack animation          three attacks from the ready pose; f75-100 ready between attacks (-> Idle), f100-160 the
#                             second attack (-> Attack)
# Body = her motion (rotation retarget); hands = POSITION retarget (hand_retarget: her fists relative to her neck,
# scaled to his shoulders, arms solved to them, thumbs along the stick, fingers closed).
QM_TAKE1 = "asdsad_GirlFBX.fbx"
QM_WALK = "Walking while having pipe_GirlFBX.fbx"
QM_RUN = "Runninng while having pipe_GirlFBX.fbx"
QM_ATTACK = "Attack animation_GirlFBX.fbx"
QM_HOLD = (8, 40, 10)             # carry window (attack take's idle and the carry idle are breathing loops on one frame)
QM_HOLD_FRAME = 24                # the carry pose the carry idle breathes on
QM_READY_FRAME = 91               # the ready pose the ready idle breathes on (and the strafe walks carry)
QM_BREATH = {"Khalid_Melee_Hold": 3.4, "Khalid_Melee_Idle": 2.8}   # seconds per breath (one breath = one loop)
QM_RAISE = (40, 72)
QM_STRIDE = (18, 58, 8)           # carry-walk loop
QM_RUNSTRIDE = (25, 51, 8)        # carry-run loop (she's up to speed from ~f25)
QM_READY = (75, 100, 8)           # ready loop (attack take)
QM_SWING = (100, 160)             # the attack (attack take)
QM_CARRY_OFFSET = Vector((0.0, 0.06, -0.18))   # user: carry the pipe lower, at his chest (both fists; his right, forward, up)
QM_RAISE_FADE = 0.4               # the raise starts from that lowered carry and leaves it over this much of the clip
QM_WALK_UPPER = QM_READY_FRAME    # the strafe walks carry the ready idle's exact upper body
UPPER_KEYS = ARM_KEYS + ("Spine", "Neck", "Head")


def loop_frames(a, b, K):
    """frames a..b as a seamless loop: (frame, partner frame, weight) - the last K cross-fade into the first ones"""
    L = (b - a + 1) - K
    out = []
    for i in range(L):
        j = i - (L - K)
        out.append((a + K + i, a + j, smooth((j + 1) / K)) if j >= 0 else (a + K + i, None, 0.0))
    return out


def take_poses(src, order, spec):
    """poses + hand data for a list of (frame, partner, weight)"""
    poses, hands = [], []
    for f, g, w in spec:
        p, h = src.at(f), src.hand[f]
        if g is not None:
            p = blend(src.arm, order, p, src.at(g), w)
            h = (h[0].lerp(src.hand[g][0], w), h[1].lerp(src.hand[g][1], w))
        poses.append(p); hands.append(h)
    return poses, hands


DEBUG_HANDS = False


def descendants(arm, root_bone, order):
    out = []
    for n in order:
        b = n
        while b is not None:
            if b == root_bone: out.append(n); break
            b = parent_name(arm, b)
    return out


def breathing(src, order, frame, seconds, fps=30):
    """one perfectly looping breath on a single filmed pose: chest lifts and opens, shoulders rise, the neck counters so
    the head stays level (the hands, retargeted relative to his neck in his chest's axes, ride the chest with the pipe)"""
    A = src.arm
    mw, mwi = A.matrix_world, A.matrix_world.inverted()
    base = src.at(frame)
    n = int(round(seconds * fps))
    X = "mixamorig:"
    subs = {k: descendants(A, X + k, order) for k in ("Spine1", "Spine2", "Neck", "LeftShoulder", "RightShoulder")}
    # (bone, world axis, degrees at full inhale) - he faces -Y, x = his left, z = up
    moves = [("Spine1", Vector((1, 0, 0)), -2.0), ("Spine2", Vector((1, 0, 0)), -3.2), ("Neck", Vector((1, 0, 0)), 4.2),
             ("LeftShoulder", Vector((0, 1, 0)), -3.0), ("RightShoulder", Vector((0, 1, 0)), 3.0)]
    poses = []
    for k in range(n):
        b = 0.5 * (1.0 - math.cos(2.0 * math.pi * k / n))       # 0 exhaled .. 1 inhaled .. 0
        p = {nm: (mw @ m) for nm, m in base.items()}              # world
        for bone, axis, deg in moves:
            piv = p[X + bone].translation.copy()
            R = Matrix.Translation(piv) @ Matrix.Rotation(math.radians(deg * b), 4, axis) @ Matrix.Translation(-piv)
            for d in subs[bone]: p[d] = R @ p[d]
        poses.append({nm: mwi @ m for nm, m in p.items()})
    return poses, [src.hand[frame]] * n


def hands_hook(hands, sw, offsets=None):
    def hook(fi, M, sv):
        gR, gL = hands[min(fi, len(hands) - 1)]
        sv.hand_retarget(M, gR, gL, sw, fi, offsets[min(fi, len(offsets) - 1)] if offsets else None)
        # the root master stays at rest (hips / spine carry the motion instead): Khalid's arms and head hang off c_traj,
        # and in game the carry layer lays them over his own walk - PlayerMeleeWeapon moves them by how far the walk's
        # root master is from this rest, which keeps them exactly on the shoulders / neck
        M["c_root_master.x"] = Matrix.Identity(4)
        if DEBUG_HANDS and fi % 10 == 0:
            n = sv.joint(M, "neck")
            print("HANDDBG fi %d gR %s gL %s | TR-neck %s  shoulderR %s" % (fi, tuple(round(v, 2) for v in gR), tuple(round(v, 2) for v in gL),
                  tuple(round(v, 2) for v in sv.track[fi][0] - n), tuple(round(v, 2) for v in sv.joint(M, "r_5") - n)))
    return hook


if MODE == "qm":
    import traceback
    t1 = QMSrc(QM_TAKE1, 2, 126, (8, 40))
    wk = QMSrc(QM_WALK, 1, 65, (18, 58), strip=True)
    at = QMSrc(QM_ATTACK, 2, 241, (100, 160))
    order = sorted((pb.name for pb in t1.arm.pose.bones), key=lambda n: depth(t1.arm, n))
    # the run carry: the standing carry pose held steady (her run take is one accelerating stride - looped it jerked);
    # his own run's bounce and forward lean move it in game
    jobs = [("Khalid_Melee_Hold", t1, "breath", QM_HOLD_FRAME),
            ("Khalid_Melee_Raise", t1, [(f, None, 0.0) for f in range(QM_RAISE[0], QM_RAISE[1] + 1)]),
            ("Khalid_Melee_HoldWalk", wk, loop_frames(*QM_STRIDE)),
            ("Khalid_Melee_HoldRun", t1, [(QM_HOLD_FRAME, None, 0.0)] * 20),
            ("Khalid_Melee_Idle", at, "breath", QM_READY_FRAME),
            ("Khalid_Melee_Attack", at, [(f, None, 0.0) for f in range(QM_SWING[0], QM_SWING[1] + 1)])]
    only = ARGS[2].split(",") if len(ARGS) > 2 else None
    jobs = [j if len(j) == 4 else j + (None,) for j in jobs]
    for out_name, src, spec, extra in jobs + [(n, None, None, None) for n in STANCE if n != "Khalid_Melee_Idle"]:
        if only and out_name not in only: continue
        try:
            print("MELEE_CLIP", out_name, flush=True)
            if spec == "breath":
                poses, hands = breathing(src, order, extra, QM_BREATH[out_name])
            elif src is not None:
                poses, hands = take_poses(src, order, spec)
            if src is not None:
                n = len(poses)
                if out_name in ("Khalid_Melee_Hold", "Khalid_Melee_HoldWalk", "Khalid_Melee_HoldRun"):
                    offs = [QM_CARRY_OFFSET] * n
                elif out_name == "Khalid_Melee_Raise":
                    offs = [QM_CARRY_OFFSET * (1.0 - smooth(i / max(1, n - 1) / QM_RAISE_FADE)) for i in range(n)]
                else:
                    offs = None
                export(src, poses, out_name, hands_hook(hands, src.shoulder_w, offs))
            else:
                # strafe walks: legs + hips from his walk (held in place, at the ready stance's hip height), and the WHOLE
                # upper body (spine, neck, head, arms, hands) exactly the ready stance's - every clip in the strafe blend
                # tree then shares one upper body, so mixing them can't pull his arms off his shoulders (his arms and head
                # hang off c_traj, not the spine)
                body = Src(STANCE[out_name], mirror=True, strip=True)
                up = at.at(QM_WALK_UPPER)
                h = at.hand[QM_WALK_UPPER]
                mw_ = body.arm.matrix_world; mwi_ = mw_.inverted()
                hips = [n for n in order if n.endswith("Hips")][0]
                hz = (mw_ @ up[hips]).translation.z
                poses = []
                for f in range(body.f0, body.f1 + 1):
                    p = dict(body.at(f))
                    for n in order:
                        if any(k in n.split(":")[-1] for k in UPPER_KEYS): p[n] = up[n].copy()
                    hw = mw_ @ p[hips]
                    hw.translation.z = hz
                    p[hips] = mwi_ @ hw
                    poses.append(p)
                export(body, poses, out_name, hands_hook([h], at.shoulder_w))
        except Exception:
            print("MELEE_FAIL", out_name, flush=True)
            traceback.print_exc()
            sys.stdout.flush()
    raise SystemExit

want = list(STANCE) + ["Khalid_Melee_Attack"] if MODE == "all" else MODE.split(",")
hold = Src("Club2H_Combo")
order = sorted((pb.name for pb in hold.arm.pose.bones), key=lambda n: depth(hold.arm, n))
HOLD_FRAME = hold.f0          # the combo's ready stance before its first swing
torch_idle = Src("Torch_Idle", mirror=True)
stance0 = with_arms(hold.arm, order, torch_idle.at(torch_idle.f0), hold.at(HOLD_FRAME))

print("MELEE_BUILD", want, flush=True)
import traceback
for out_name in want:
    try:
      print("MELEE_CLIP", out_name, flush=True)
      if out_name in STANCE:
          body = torch_idle if STANCE[out_name] == "Torch_Idle" else Src(STANCE[out_name], mirror=True)
          poses = [with_arms(hold.arm, order, body.at(f), hold.at(HOLD_FRAME)) for f in range(body.f0, body.f1 + 1)]
          export(body, poses, out_name, stance_hook)
      elif out_name == "Khalid_Melee_Attack":
          swing = Src("BB_HitSingle", yaw=BB_YAW, strip=True)
          A = hold.arm
          S_ = lambda f: swing.sample(A, order, f)
          poses = [blend(A, order, stance0, swing.at(LOAD_FROM), smooth(k / LOAD_IN)) for k in range(1, LOAD_IN + 1)]
          poses += [S_(LOAD_FROM + (LOAD_TO - LOAD_FROM) * (1 - (1 - k / LOAD_N) ** 2)) for k in range(1, LOAD_N + 1)]
          top = swing.at(LOAD_TO)
          poses += [top] * TOP_HOLD
          strike_start = len(poses)
          poses += [S_(STRIKE_FROM + (STRIKE_TO - STRIKE_FROM) * k / (STRIKE_N - 1)) for k in range(STRIKE_N)]
          contact = strike_start + (CONTACT_SRC - STRIKE_FROM) * (STRIKE_N - 1) / (STRIKE_TO - STRIKE_FROM)
          poses += [S_(STRIKE_TO + (FOLLOW_TO - STRIKE_TO) * math.sin(0.5 * math.pi * k / FOLLOW_N)) for k in range(1, FOLLOW_N + 1)]
          last = swing.at(FOLLOW_TO)
          poses += [last] * SETTLE
          recover_start = len(poses)
          poses += [blend(A, order, last, stance0, smooth(k / RECOVER)) for k in range(1, RECOVER + 1)]
          export(swing, poses, out_name, attack_hook)
          n = len(poses) - 1
          print("ATTACK_LAYOUT frames %d strike %.3f contact %.3f recover %.3f (normalized)" % (len(poses), strike_start / n, contact / n, recover_start / n))
    except Exception:
      print("MELEE_FAIL", out_name, flush=True)
      traceback.print_exc()
      sys.stdout.flush()
