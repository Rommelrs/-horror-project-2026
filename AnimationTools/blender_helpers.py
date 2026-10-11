# Run inside Blender (exec) to (re)install the helpers the Khalid run generator needs into bpy.app.driver_namespace.
# Blender forgets the driver namespace on restart; this file rebuilds it.
import bpy, math, os, re
ns = bpy.app.driver_namespace
BASE = "C:/Users/Zayed/AppData/Local/Temp/claude/D--Unity-Stuff-NEWHorrorProject--horror-project-2026/cf51bab8-e583-42ee-8ab4-15af6ac1a2cc/scratchpad/"
TOOLS = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/AnimationTools/"
GEN = BASE + "khalid_retarget.py" if os.path.exists(BASE + "khalid_retarget.py") else TOOLS + "khalid_retarget.py"

# ---- standing reference pose (idle, frame 484): every generated pose is a delta on top of it
def capture_stand():
    rig = bpy.data.objects["rig"]; sc = bpy.context.scene
    ad = rig.animation_data or rig.animation_data_create()
    old = (ad.action, ad.action_slot, ad.use_nla, sc.frame_current)
    ad.use_nla = False
    idle = bpy.data.actions["idle"]
    ad.action = idle; ad.action_slot = [s for s in idle.slots if s.name_display == "rig"][0]
    sc.frame_set(484); bpy.context.view_layer.update()
    ns["S_stand"] = {pb.name: pb.matrix.copy() for pb in rig.pose.bones}
    ad.action = old[0]
    if old[1] is not None: ad.action_slot = old[1]
    ad.use_nla = old[2]; sc.frame_set(old[3])
capture_stand()

# ---- FBX export of one action (rig + char_grp at the origin), used for the Unity import
ns["export_code"] = r'''
import bpy
def export_action(action_name, path):
    O = bpy.data.objects; sc = bpy.context.scene
    rig = O["rig"]; grp = O["char_grp"]; ad = rig.animation_data
    act = bpy.data.actions[action_name]; slot = [s for s in act.slots if s.name_display == "rig"][0]
    saved = dict(g_act=grp.animation_data.action if grp.animation_data else None, loc=grp.location.copy(), rot=grp.rotation_euler.copy(),
                 use_nla=ad.use_nla, act=ad.action, fs=sc.frame_start, fe=sc.frame_end, cur=sc.frame_current,
                 sel=[o for o in bpy.context.selected_objects], active=bpy.context.view_layer.objects.active)
    hid = (rig.hide_get(), grp.hide_get()); rig.hide_set(False); grp.hide_set(False)
    try:
        if grp.animation_data: grp.animation_data.action = None
        grp.location = (0, 0, 0); grp.rotation_euler = (0, 0, 0)
        ad.use_nla = False; ad.action = act; ad.action_slot = slot
        sc.frame_start = int(act.frame_range[0]); sc.frame_end = int(act.frame_range[1])
        for o in bpy.context.selected_objects: o.select_set(False)
        dummy = bpy.data.objects.new('zz_export_dummy', None); sc.collection.objects.link(dummy)
        dummy.select_set(True); grp.select_set(True); rig.select_set(True); bpy.context.view_layer.objects.active = rig
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'EMPTY', 'ARMATURE'},
            bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False, bake_anim_use_all_bones=True,
            bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
            add_leaf_bones=False, armature_nodetype='NULL', use_armature_deform_only=False,
            axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_NONE', bake_space_transform=False)
    finally:
        if 'dummy' in locals() and dummy.name in bpy.data.objects: bpy.data.objects.remove(dummy, do_unlink=True)
        rig.hide_set(hid[0]); grp.hide_set(hid[1])
        if grp.animation_data: grp.animation_data.action = saved["g_act"]
        grp.location = saved["loc"]; grp.rotation_euler = saved["rot"]
        ad.action = None; ad.use_nla = saved["use_nla"]
        sc.frame_start = saved["fs"]; sc.frame_end = saved["fe"]; sc.frame_set(saved["cur"])
        for o in bpy.context.selected_objects: o.select_set(False)
        for o in saved["sel"]: o.select_set(True)
        bpy.context.view_layer.objects.active = saved["active"]
ns_export = export_action
'''
exec(ns["export_code"], ns)

# ---- load the generator with parameter overrides (each override replaces its "NAME = value" line)
def load_variant(over):
    s = open(GEN).read()
    for k, v in over.items():
        s, n = re.subn(r'(?m)^%s = [^\n#]*' % k, lambda m, k=k, v=v: '%s = %s ' % (k, v), s, count=1)
        assert n == 1, k
    G = {'__builtins__': __builtins__}; exec(s, G); return G
ns['load_variant'] = load_variant

# ---- bake a generated cycle into an action (+1 closing key, quaternion continuity, CYCLES) and export it
def bake_clip(params, NAME, path=None):
    G = load_variant(params)
    rig = bpy.data.objects['rig']; ad = rig.animation_data
    old_nla = ad.use_nla; ad.use_nla = False
    if NAME in bpy.data.actions: bpy.data.actions.remove(bpy.data.actions[NAME])
    act = bpy.data.actions.new(NAME); act.use_fake_user = True
    ad.action = act
    K = G['KEYS']; prev = {}
    for f in range(1, K + 2):
        G['apply'](((f - 1) % K) / float(K))
        for pb in rig.pose.bones:
            q = pb.rotation_quaternion
            if pb.name in prev and q.dot(prev[pb.name]) < 0: pb.rotation_quaternion = (-q.w, -q.x, -q.y, -q.z)
            prev[pb.name] = pb.rotation_quaternion.copy()
            for prop in ('location', 'rotation_quaternion', 'scale'): pb.keyframe_insert(prop, frame=f, group=pb.name)
    ad.use_nla = old_nla
    cb = act.layers[0].strips[0].channelbag(act.slots[0])
    for fc in cb.fcurves:
        fc.modifiers.new('CYCLES'); fc.update()
    if path:
        os.makedirs(os.path.dirname(path), exist_ok=True)
        ns['export_action'](NAME, path)
    ad.action = act
    return {"curves": len(cb.fcurves), "contact_s": [G['TD'] / G['SD'] / 30.0, (G['TD'] / G['SD'] + K / 2.0) / 30.0]}
ns['bake_clip'] = bake_clip

# ---- render a sheet of poses (side / front / rear, orthographic) into the scratchpad
def sheetG(G, ks, name, view, per=21):
    sc = bpy.context.scene; rig = bpy.data.objects['rig']; ad = rig.animation_data
    old_action = ad.action; old_slot = getattr(ad, 'action_slot', None); old_nla = ad.use_nla
    ad.action = None; ad.use_nla = False
    cd = bpy.data.cameras.new('tmpcam'); cd.type = 'ORTHO'; cd.ortho_scale = 2.2
    co = bpy.data.objects.new('tmpcam', cd); sc.collection.objects.link(co)
    if view == 'side': co.location = (5, 0, 1.0); co.rotation_euler = (math.radians(90), 0, math.radians(90))
    elif view == 'front': co.location = (0, -5, 1.0); co.rotation_euler = (math.radians(90), 0, 0)
    else: co.location = (0, 5, 1.0); co.rotation_euler = (math.radians(90), 0, math.radians(180))
    oldcam = sc.camera; sc.camera = co
    sc.render.resolution_x = 300; sc.render.resolution_y = 330
    paths = []
    for k in ks:
        G['apply'](k / per); p = BASE + f"rt_{view}_{k}.png"; sc.render.filepath = p
        bpy.ops.render.render(write_still=True); paths.append(p)
    imgs = [bpy.data.images.load(p) for p in paths]
    w, h = imgs[0].size; W = w * 4; rows = (len(imgs) + 3) // 4; sh = [0.0] * (W * h * rows * 4)
    for i, im in enumerate(imgs):
        px = list(im.pixels); cx = (i % 4) * w; cy = (rows - 1 - i // 4) * h
        for y in range(h):
            o = ((cy + y) * W + cx) * 4; sh[o:o + w * 4] = px[y * w * 4:(y + 1) * w * 4]
    out = bpy.data.images.new('sheet', W, h * rows); out.pixels = sh
    out.filepath_raw = BASE + name; out.file_format = 'PNG'; out.save()
    for im in imgs: bpy.data.images.remove(im)
    bpy.data.images.remove(out)
    sc.camera = oldcam; bpy.data.objects.remove(co); bpy.data.cameras.remove(cd)
    ad.action = old_action
    if old_slot is not None: ad.action_slot = old_slot
    ad.use_nla = old_nla
ns['sheetG'] = sheetG

# ---- per-frame leg measurements: (leg angle + ahead, hip-ankle / leg length, thigh bone angle, knee bone flex, ankle z) + hip z
def leg_metrics(params, keys=25):
    G = load_variant(params); rig = bpy.data.objects["rig"]; S = ns['S_stand']
    L = (S['leg_fk.l'].translation - S['c_thigh_fk.l'].translation).length + (S['foot.l'].translation - S['leg_fk.l'].translation).length
    rows = []
    for k in range(keys):
        G['apply'](k / keys); row = {"f": k + 1}
        for s in ("l", "r"):
            H = rig.matrix_world @ rig.pose.bones['c_thigh_fk.' + s].head
            K = rig.matrix_world @ rig.pose.bones['leg_fk.' + s].head
            A = rig.matrix_world @ rig.pose.bones['foot.' + s].head
            ang = math.degrees(math.atan2(-(A.y - H.y), H.z - A.z))
            th = math.degrees(math.atan2(-(K.y - H.y), H.z - K.z)); sh = math.degrees(math.atan2(-(A.y - K.y), K.z - A.z))
            row[s] = [round(ang, 1), round((A - H).length / L, 3), round(th, 1), round(th - sh, 1), round(A.z, 3)]
        row["hz"] = round((rig.matrix_world @ rig.pose.bones['c_thigh_fk.l'].head).z, 3)
        rows.append(row)
    return rows
ns['leg_metrics'] = leg_metrics

# ---- parameter sets of the keyed clips that are rebuilt from the generator
def irl_swing_keys():
    k = [(0.08, -28, 6), (0.16, -33, 6), (0.26, -36, 8), (0.32, -35, 14)]
    vid = [(14, -28, 35), (15, -23, 54), (16, -16, 67), (17, 0, 88), (18, 21, 102), (19, 33, 103), (20, 40, 92), (21, 41, 72), (22, 33, 40), (23, 24, 14)]
    for t, th, fl in vid: k.append((round(0.32 + (t - 13) / 11.0 * 0.68, 3), th, fl))
    return k
def irl_jog_swing_keys():
    k = [(0.04, -38, 4)]
    vid = [(13, -38, 12), (14, -28, 35), (15, -23, 54), (16, -16, 67), (17, 0, 88), (18, 21, 102), (19, 33, 103), (20, 40, 92), (21, 41, 72), (22, 33, 40), (23, 24, 14)]
    for t, th, fl in vid: k.append((round(0.074 + (t - 12) / 12.0 * 0.926, 3), th, fl))
    return k
_lean = [7, 6.5, 5.5, 4.5, 4, 4, 4.5, 5.5, 6.5, 7, 7, 7, 7, 6.5, 5.5, 4.5, 4, 4, 4.5, 5.5, 6.5, 7, 7, 7]
ns['IRL_PARAMS'] = {"V": 2.8182, "REFFILE": '"ref_dump_running.json"', "KEYS": 25, "KEYED_LEGS": 1, "K_D": 0.325, "K_ALPHA": 16.0,
    "K_FLEX_C": 2.0, "K_FLEX_M": 8.0, "K_FLEX_T": 4.0, "K_MID": 0.4, "K_P0": 8.0, "K_PT": 62.0, "K_HS": 0.12, "K_HO": 0.55, "K_A": 0.012, "K_PLOW": 0.45,
    "K_TAN_MAX": 250.0, "K_SWING": irl_swing_keys(), "IRL_UPPER": 1, "IRL_ARM_SCALE": 0.92, "IRL_ARM_ROT": 22.0, "IRL_ARM_ABD": 16.0,
    "IRL_LEAN": [v + 4.0 for v in _lean], "PELVIS_YAW_DEG": 6.0, "PELVIS_ROLL_DEG": 3.5, "SWAY_BU": 0.02, "BACK_BEND": 2.0, "YAW_SCALE": 1.0, "TWIST_FOLLOW": 0.9}
ns['IRL_JOG_PARAMS'] = dict(ns['IRL_PARAMS'], V=2.0909, K_D=0.44, K_SWING=irl_jog_swing_keys())
