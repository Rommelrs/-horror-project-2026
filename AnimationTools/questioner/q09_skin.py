# The Questioner - skinning.   Blender: exec(open(this).read())
#  - body / hands / head: automatic (bone heat) weights
#  - robe, sleeves, pants: weights transferred from a temporary full body (Q_WeightProxy) then smoothed (skirt less tearing)
#  - keffiyeh, teeth, eyes, sandals: weights set from position
import bpy, bmesh, math
from mathutils import Vector

HERE = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\AnimationTools\questioner"
RIG = "QuestionerRig"


def ensure_object_mode():
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")


def clear_skin(ob):
    ob.vertex_groups.clear()
    for m in [m for m in ob.modifiers if m.type == "ARMATURE"]:
        ob.modifiers.remove(m)
    ob.parent = None


def add_armature(ob):
    rig = bpy.data.objects[RIG]
    ob.parent = rig
    ob.matrix_parent_inverse = rig.matrix_world.inverted()
    m = ob.modifiers.new("Armature", "ARMATURE")
    m.object = rig


def auto_weights(objs):
    ensure_object_mode()
    rig = bpy.data.objects[RIG]
    rig.data.pose_position = "REST"
    for ob in objs:
        clear_skin(ob)
    bpy.ops.object.select_all(action="DESELECT")
    for ob in objs:
        ob.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")


def set_weights(ob, fn):
    """fn(co) -> {bone: weight}; replaces all groups"""
    clear_skin(ob)
    rig = bpy.data.objects[RIG]
    groups = {}
    for v in ob.data.vertices:
        w = fn(ob.matrix_world @ v.co)
        tot = sum(w.values())
        for b, x in w.items():
            if x <= 0:
                continue
            g = groups.get(b)
            if g is None:
                g = groups[b] = ob.vertex_groups.new(name=b)
            g.add([v.index], x / tot, "REPLACE")
    add_armature(ob)


def transfer_weights(src, dst):
    ensure_object_mode()
    clear_skin(dst)
    for b in src.vertex_groups:
        dst.vertex_groups.new(name=b.name)
    m = dst.modifiers.new("WT", "DATA_TRANSFER")
    m.object = src
    m.use_vert_data = True
    m.data_types_verts = {"VGROUP_WEIGHTS"}
    m.vert_mapping = "POLYINTERP_NEAREST"
    m.layers_vgroup_select_src = "ALL"
    m.layers_vgroup_select_dst = "NAME"
    bpy.ops.object.select_all(action="DESELECT")
    dst.select_set(True)
    bpy.context.view_layer.objects.active = dst
    bpy.ops.object.modifier_apply(modifier="WT")
    # drop empty groups
    used = set()
    for v in dst.data.vertices:
        for g in v.groups:
            if g.weight > 1e-4:
                used.add(g.group)
    for g in list(dst.vertex_groups):
        if g.index not in used:
            dst.vertex_groups.remove(g)
    add_armature(dst)


def closest_weights(src):
    """returns fn(co) -> weights interpolated from the closest face of src (rest pose)"""
    me = src.data
    VW = [{src.vertex_groups[g.group].name: g.weight for g in v.groups if g.weight > 1e-4} for v in me.vertices]
    def fn(co):
        ok, loc, nor, fi = src.closest_point_on_mesh(src.matrix_world.inverted() @ co)
        if not ok:
            return {"Hips": 1.0}
        poly = me.polygons[fi]
        acc = {}
        tot = 0
        for vi in poly.vertices:
            d = (me.vertices[vi].co - loc).length
            w = 1.0 / (d + 1e-4)
            tot += w
            for b, x in VW[vi].items():
                acc[b] = acc.get(b, 0) + x * w
        return {b: x / tot for b, x in acc.items()} or {"Hips": 1.0}
    return fn


def head_weights(proxy_fn):
    def fn(co):
        z, y = co.z, co.y
        w_head = ss(1.78, 1.83, z)
        base = proxy_fn(co) if w_head < 1 else {}
        out = {k: v * (1 - w_head) for k, v in base.items()}
        if w_head > 0:
            # the lower jaw (in front of the throat, below the upper gums) follows the Jaw bone
            jaw = ss(-0.040, -0.070, y) * (1 - ss(1.80, 1.845, z)) * ss(1.60, 1.64, z)
            out["Jaw"] = out.get("Jaw", 0) + w_head * jaw
            out["Head"] = out.get("Head", 0) + w_head * (1 - jaw)
        else:
            jaw = ss(-0.050, -0.075, y) * ss(1.60, 1.64, z)
            if jaw > 0:
                out = {k: v * (1 - jaw) for k, v in out.items()}
                out["Jaw"] = out.get("Jaw", 0) + jaw
        return out
    return fn


def smooth_weights(ob, iterations=6, factor=0.5, mask=None):
    """average each vertex's weights with its neighbours (optionally only where mask(co) is True)"""
    me = ob.data
    nb = [[] for _ in me.vertices]
    for e in me.edges:
        a, b = e.vertices
        nb[a].append(b)
        nb[b].append(a)
    names = [g.name for g in ob.vertex_groups]
    W = [{g.group: g.weight for g in v.groups} for v in me.vertices]
    sel = [mask(ob.matrix_world @ v.co) if mask else True for v in me.vertices]
    for _ in range(iterations):
        new = []
        for i, w in enumerate(W):
            if not sel[i] or not nb[i]:
                new.append(w)
                continue
            acc = {}
            for j in nb[i]:
                for k, x in W[j].items():
                    acc[k] = acc.get(k, 0) + x / len(nb[i])
            mix = {}
            for k in set(w) | set(acc):
                mix[k] = w.get(k, 0) * (1 - factor) + acc.get(k, 0) * factor
            new.append(mix)
        W = new
    for g in ob.vertex_groups:
        g.remove(list(range(len(me.vertices))))
    for i, w in enumerate(W):
        tot = sum(w.values()) or 1
        for k, x in w.items():
            if x / tot > 1e-3:
                ob.vertex_groups[k].add([i], x / tot, "REPLACE")


def limit_and_normalize(ob, limit=4):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=limit)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)


def ss(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def head_weights_v2(proxy_fn):
    """v2 head: everything above the neck -> Head; the hanging lower jaw (jaw frame of q14) -> Jaw; neck/chest from the proxy"""
    import numpy as _np
    ss_ = [r[0] for r in LOWER_FACE]
    def fn(co):
        P = _np.array(co[:])
        s = float((P - JAW_O) @ JAW_A)
        dep = float((P - JAW_O) @ (-JAW_F))
        rx = float(_np.interp(s, ss_, [r[1] for r in LOWER_FACE]))
        ry = float(_np.interp(s, ss_, [r[2] for r in LOWER_FACE]))
        if -0.01 < s < 0.24 and dep < 2 * ry + 0.010 and abs(P[0]) < rx + 0.012 and dep > -0.03:
            t = ss(-0.005, 0.030, s)
            return {"Jaw": t, "Head": 1 - t}
        w_head = ss(1.78, 1.84, co.z)
        if co.y < -0.035:                       # face side above the jaw
            w_head = max(w_head, ss(1.80, 1.83, co.z))
        if w_head >= 1:
            return {"Head": 1.0}
        out = {k: v * (1 - w_head) for k, v in proxy_fn(co).items()}
        if w_head > 0:
            out["Head"] = out.get("Head", 0) + w_head
        return out
    return fn


def teeth_weights_v2(co):
    """upper teeth (hang from the arch) -> Head, the rows on the lower jaw -> Jaw"""
    return {"Head": 1.0} if co.z > 1.835 else {"Jaw": 1.0}


def keffiyeh_weights(co):
    z = co.z
    if z > 1.80:
        return {"Head": 1.0}
    if z > 1.70:
        t = ss(1.70, 1.80, z)
        return {"Head": t, "Neck1": 1 - t}
    if z > 1.60:
        t = ss(1.60, 1.70, z)
        return {"Neck1": t * 0.6, "Neck": t * 0.4, "Spine2": 1 - t}
    t = ss(1.35, 1.60, z)
    return {"Spine2": 0.3 + 0.7 * t, "Spine1": 0.7 * (1 - t)}


def tail_weights(co):
    z = co.z
    if z > 1.72:
        t = ss(1.72, 1.80, z)
        return {"Head": t, "Neck1": 1 - t}
    if z > 1.55:
        t = ss(1.55, 1.72, z)
        return {"Neck1": t * 0.5, "Neck": t * 0.5, "Spine2": 1 - t}
    if z > 1.30:
        t = ss(1.30, 1.55, z)
        return {"Spine2": t, "Spine1": 1 - t}
    t = ss(1.10, 1.30, z)
    return {"Spine1": t, "Spine": 1 - t}


def teeth_weights(co):
    t = ss(1.74, 1.79, co.z)
    return {"Head": t, "Jaw": 1 - t}


def sandal_weights(side):
    def fn(co):
        t = ss(-0.150, -0.110, co.y)          # toward the heel -> foot
        return {side + "Foot": t, side + "ToeBase": 1 - t}
    return fn


def run():
    ensure_object_mode()
    O = bpy.data.objects
    proxy = O.get("Q_WeightProxy")
    if proxy is None:
        # temporary full body as the source for clothing / neck weights
        g = {"BUILD_BODY": True, "BODY_NAME": "Q_WeightProxy"}
        exec(open(HERE + r"\q03_body.py", encoding="utf8").read(), g)
        proxy = O["Q_WeightProxy"]
    auto_weights([proxy, O["Q_Body"], O["Q_HandL"], O["Q_HandR"]])
    pf = closest_weights(proxy)
    for n in ("Q_Thobe", "Q_SleeveL", "Q_SleeveR", "Q_PantsL", "Q_PantsR"):
        set_weights(O[n], pf)
    # skirt: blur the left/right split between the legs so the robe stretches instead of tearing
    smooth_weights(O["Q_Thobe"], iterations=10, factor=0.6, mask=lambda c: c.z < 1.0)
    smooth_weights(O["Q_Thobe"], iterations=3, factor=0.5)
    V2 = "JAW_O" in globals()
    set_weights(O["Q_Head"], head_weights_v2(pf) if V2 else head_weights(pf))
    smooth_weights(O["Q_Head"], iterations=3, factor=0.5)
    set_weights(O["Q_KeffiyehHood"], keffiyeh_weights)
    if "Q_KeffiyehTail" in O:
        set_weights(O["Q_KeffiyehTail"], tail_weights)
    set_weights(O["Q_Teeth"], teeth_weights_v2 if V2 else teeth_weights)
    set_weights(O["Q_Eyes"], lambda c: {"Head": 1.0})
    set_weights(O["Q_SandalL"], sandal_weights("Left"))
    set_weights(O["Q_SandalR"], sandal_weights("Right"))
    names = [n for n in ["Q_Body", "Q_HandL", "Q_HandR", "Q_Head", "Q_Teeth", "Q_Eyes", "Q_Thobe", "Q_SleeveL", "Q_SleeveR",
             "Q_PantsL", "Q_PantsR", "Q_KeffiyehHood", "Q_KeffiyehTail", "Q_SandalL", "Q_SandalR"] if n in O]
    for n in names:
        limit_and_normalize(O[n])
    unweighted = {}
    for n in names:
        o = O[n]
        unweighted[n] = sum(1 for v in o.data.vertices if not any(g.weight > 1e-4 for g in v.groups))
    bpy.data.objects.remove(proxy, do_unlink=True)
    return unweighted

if globals().get("RUN_SKIN", True):
    result = run()
