# The Questioner's slow turn on the spot (store storage-room cutscene): Q_Turn180, 168 frames at 30 fps (5.6 s), half a
# turn over his LEFT shoulder in six shuffling steps.
#   blender -b TheQuestioner_v2.blend --python q21_turn.py -- [preview DIR]
# Writes Assets/Models/TheQuestioner/TheQuestioner_Turn.fbx (armature only, same bone paths as the model).
#
# The clip is IN PLACE: the game turns his root object by turn_yaw(frame) (same table in StorageMeetingCutscene.TurnProfile)
# and the clip holds the planted foot still against that, so nothing slides. In rig space that means each foot swings about
# the root by (its own yaw - root yaw).
import bpy, math, sys, os
from mathutils import Vector, Matrix

HERE = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/AnimationTools/questioner/"
_src = open(HERE + "q20_moves.py").read().split("# ---------------- the clips ----------------")[0]
exec(_src)
OUT_FBX = ROOT + "Assets/Models/TheQuestioner/TheQuestioner_Turn.fbx"

N = 168
STEP_DEG = 60.0
# (foot, lifts at frame, planted again at frame)
STEPS = [("Left", 14, 38), ("Right", 46, 68), ("Left", 76, 96), ("Right", 102, 122), ("Left", 128, 146), ("Right", 150, 163)]
WIDEN = 5          # the body starts turning a little before the foot leaves the ground and ends a little after


def smooth(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


def foot_yaw(s, f):
    return sum(STEP_DEG * smooth((f - a) / float(b - a)) for (n, a, b) in STEPS if n == s)


def turn_yaw(f):
    return sum(0.5 * STEP_DEG * smooth((f - (a - WIDEN)) / float(b - a + 2 * WIDEN)) for (n, a, b) in STEPS)


def swing(s, f):
    """0..1..0 while this foot is in the air"""
    for (n, a, b) in STEPS:
        if n == s and a <= f <= b: return math.sin(math.pi * (f - a) / float(b - a))
    return 0.0


def lean_to(f):
    """-1 = weight on the right foot (left one stepping), +1 = on the left"""
    v = 0.0
    for (n, a, b) in STEPS:
        k = smooth((f - (a - 9)) / 9.0) * (1.0 - smooth((f - (b - 3)) / 9.0))
        v += k * (-1.0 if n == "Left" else 1.0)
    return v


def lead(f):
    """the head and shoulders go first, then the body catches up; a small settle at the end"""
    return smooth(f / 26.0) * (1.0 - smooth((f - 112) / 44.0)) - 0.12 * smooth((f - 150) / 8.0) * (1.0 - smooth((f - 158) / 10.0))


def Rz(deg):
    return Matrix.Rotation(math.radians(deg), 4, Z)


IDLE0 = capture("Questioner_Idle", 1)
apply(IDLE0)
REF = {}
for s in ("Left", "Right"):
    REF[s] = dict(ankle=P[s + "Foot"].head.copy(), hip=P[s + "UpLeg"].head.copy(), knee=P[s + "Leg"].head.copy(),
                  rot=P[s + "Foot"].matrix.to_3x3().copy())
MID = (REF["Left"]["ankle"] + REF["Right"]["ankle"]) * 0.5
idle_len = int(bpy.data.actions["Questioner_Idle"].frame_range[1]) - 1


def pose(f):
    th = turn_yaw(f)
    w = lean_to(f)
    # weight over the standing foot, a touch lower while a foot is in the air
    stand = REF["Left"]["ankle"] if w > 0 else REF["Right"]["ankle"]
    side = "Left" if w > 0 else "Right"
    d_stand = foot_yaw(side, f) - th
    target = (Rz(d_stand) @ stand)
    shift = Vector((target.x - MID.x, target.y - MID.y, 0.0)) * (0.42 * abs(w))
    air = max(swing("Left", f), swing("Right", f))
    move("Hips", shift + Vector((0, 0, -0.012 * abs(w) - 0.006 * air)))
    rot("Hips", Y, -2.2 * w)                              # hips tip over the standing leg
    rot("Spine", Y, 1.6 * w); rot("Spine1", Y, 1.2 * w)   # and the chest balances back
    # upper body leads into the turn (+Z = to his left)
    k = lead(f)
    rot("Spine", Z, 2.0 * k); rot("Spine1", Z, 3.5 * k); rot("Spine2", Z, 4.5 * k)
    rot("Neck", Z, 5.0 * k); rot("Head", Z, 9.0 * k)
    # arms hang and drag a little behind the turn
    for s in ("Left", "Right"):
        rot(s + "Arm", Z, -3.0 * k)
    # legs: each foot where it stands in the world, seen from the turning root
    for s in ("Left", "Right"):
        r = REF[s]
        d = foot_yaw(s, f) - th
        up = swing(s, f)
        ankle = Rz(d) @ r["ankle"] + Z * (0.05 * up)
        hip = P[s + "UpLeg"].head.copy()
        pole = hip + (Rz(d * 0.6) @ (r["knee"] - r["hip"])) + (Rz(d) @ FWD) * (0.10 * up)
        ik(s + "UpLeg", s + "Leg", s + "Foot", ankle, pole)
        pb = P[s + "Foot"]
        M = (Rz(d).to_3x3() @ r["rot"]).to_4x4()
        M.translation = pb.matrix.translation.copy()
        pb.matrix = M
        ev()
        if up > 0:
            rot(s + "Foot", (Rz(d).to_3x3() @ X), 16.0 * up)        # toes hang while it is lifted


keys = []
for f in range(0, N + 1):
    base = capture("Questioner_Idle", 1 + (f % idle_len))
    keys.append((f, base, (lambda ff=f: pose(ff))))
act = build("Q_Turn180", keys)

# ---- check: a planted foot must not move in the world
set_action(act)
worst = 0.0
for s in ("Left", "Right"):
    last = None
    for f in range(0, N + 1):
        scene.frame_set(f); ev()
        planted = swing(s, f) == 0.0
        wp = Rz(turn_yaw(f)) @ P[s + "Foot"].head
        if planted and last is not None: worst = max(worst, (wp - last).length)
        last = wp if planted else None
print("TURN total", turn_yaw(N), "worst planted-foot drift per frame (m)", round(worst, 4))
set_action(None)

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
if "preview" in argv:
    dirpath = argv[argv.index("preview") + 1]
    os.makedirs(dirpath, exist_ok=True)
    mesh = bpy.data.objects["Q_Mesh"]
    for o in bpy.data.objects:
        if o.type == "MESH": o.hide_render = o is not mesh
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.render.resolution_x, scene.render.resolution_y = 300, 400
    cam_data = bpy.data.cameras.new("PrevCam"); cam = bpy.data.objects.new("PrevCam", cam_data)
    scene.collection.objects.link(cam); scene.camera = cam; cam_data.lens = 50
    # a floor grid to judge sliding
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=12, y_subdivisions=12, size=3.0)
    grid = bpy.context.active_object; grid.display_type = "WIRE"
    mod = grid.modifiers.new("w", "WIREFRAME"); mod.thickness = 0.006
    set_action(act)
    views = {"front": Vector((1.6, -3.9, 1.5)), "top": Vector((0.0, -0.9, 5.2))}
    for f in range(0, N + 1, 12):
        scene.frame_set(f)
        rig.rotation_euler = (0, 0, math.radians(turn_yaw(f)))       # what the game does to his root
        for vn, vp in views.items():
            cam.location = vp
            tgt = Vector((0, 0, 0.95)) if vn == "front" else Vector((0, 0, 0))
            cam.rotation_euler = (tgt - vp).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = os.path.join(dirpath, "turn_%03d_%s.png" % (f, vn))
            bpy.ops.render.render(write_still=True)
    rig.rotation_euler = (0, 0, 0)
    set_action(None)
else:
    ad = rig.animation_data
    for t in list(ad.nla_tracks): ad.nla_tracks.remove(t)
    tr = ad.nla_tracks.new(); tr.name = "Q_Turn180"
    st = tr.strips.new("Q_Turn180", int(act.frame_range[0]), act); st.name = "Q_Turn180"
    try: st.action_slot = act.slots[0]
    except Exception: pass
    rig.data.pose_position = "POSE"
    dummy = bpy.data.objects.new("MovesDummy", None)
    scene.collection.objects.link(dummy)
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True); dummy.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=OUT_FBX, use_selection=True, object_types={"ARMATURE", "EMPTY"},
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             add_leaf_bones=False, use_armature_deform_only=False, armature_nodetype="NULL",
                             bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=True,
                             bake_anim_use_all_bones=True, bake_anim_force_startend_keying=True, bake_anim_step=1.0,
                             bake_anim_simplify_factor=0.0)
    print("EXPORTED", OUT_FBX)
