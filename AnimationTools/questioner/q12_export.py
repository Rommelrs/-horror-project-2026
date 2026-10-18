# The Questioner - final retarget with posture, NLA strips per clip, joined mesh, FBX export.   Blender: exec(open(this).read())
import bpy, os

HERE = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\AnimationTools\questioner"
OUT = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\Assets\Models\TheQuestioner"
exec(open(HERE + r"\q02_retarget.py", encoding="utf8").read())

# v2: head thrown further back (concept: screaming upward, the long jaw hanging in front of the throat)
POSTURE = {"Spine1": (4, 0, 0), "Spine2": (6, 0, 0), "Neck": (16, 0, 0), "Neck1": (12, 0, 0), "Head": globals().get("HEAD_TILT", (-30, 0, 0))}
POSE_EXTRA = {}
for s, sg in (("Left", -1), ("Right", 1)):
    for f, angs in (("Index", (18, 32, 22)), ("Middle", (20, 34, 24)), ("Ring", (22, 36, 24)), ("Pinky", (26, 38, 26)), ("Thumb", (8, 14, 10))):
        for i, ang in zip((1, 2, 3), angs):
            POSE_EXTRA["%sHand%s%d" % (s, f, i)] = (0, 0, ang * sg)


def final_mesh():
    O = bpy.data.objects
    parts = [O[n] for n in ("Q_HeadMesh", "Q_Limbs", "Q_Clothes", "Q_Keffiyeh") if n in O]
    if not parts:
        return O["Q_Mesh"]
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = "Q_Mesh"
    ob.data.name = "Q_Mesh"
    arms = [m for m in ob.modifiers if m.type == "ARMATURE"]
    for m in arms[1:]:
        ob.modifiers.remove(m)
    return ob


def setup_nla(names):
    rig = bpy.data.objects[NEW]
    ad = rig.animation_data or rig.animation_data_create()
    ad.action = None
    for t in list(ad.nla_tracks):
        ad.nla_tracks.remove(t)
    for n in names:
        act = bpy.data.actions[n]
        tr = ad.nla_tracks.new()
        tr.name = n
        st = tr.strips.new(n, int(act.frame_range[0]), act)
        st.name = n
        try:
            st.action_slot = act.slots[0]
        except Exception:
            pass


def export(path):
    rig = bpy.data.objects[NEW]
    mesh = bpy.data.objects["Q_Mesh"]
    rig.data.pose_position = "POSE"
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             add_leaf_bones=False, use_armature_deform_only=False, armature_nodetype="NULL",
                             bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=True,
                             bake_anim_use_all_bones=True, bake_anim_force_startend_keying=True, bake_anim_step=1.0,
                             bake_anim_simplify_factor=0.0, mesh_smooth_type="FACE", use_tspace=False,
                             path_mode="COPY", embed_textures=False)


if globals().get("RUN_EXPORT", True):
    os.makedirs(OUT, exist_ok=True)
    made, hs = run(posture=POSTURE, extra=POSE_EXTRA)
    names = [m[0] for m in made]
    mesh = final_mesh()
    setup_nla(names)
    export(os.path.join(OUT, "TheQuestioner.fbx"))
    result = {"clips": made, "tris": sum(len(p.vertices) - 2 for p in mesh.data.polygons), "mats": [s.material.name for s in mesh.material_slots]}
