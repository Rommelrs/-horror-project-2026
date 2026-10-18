# The Questioner v2 - keffiyeh hood by cloth simulation: a sheet pre-wrapped over the skull like a half tube, its front edge
# pinned in an arc around the face (forehead -> temples -> cheekbones), the rest falls over the head, shoulders and back.
# Colliders: decimated copy of the v2 head + body + robe (rest pose).   Blender: exec(open(this).read())
import bpy, bmesh, math, random
import numpy as np
from mathutils import Vector

HERE = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\AnimationTools\questioner"
V = Vector
HOOD_NAME = "Q_Hood2"
HOOD_W, HOOD_L = 1.04, 0.98            # sheet size (across the face, front -> back)
NU, NV = 58, 54                        # sim resolution
HOOD_C = V((0.0, -0.012, 1.905))       # axis of the initial half tube (runs front -> back)
FRONT_Y = -0.080
PIN_DEG = 108                          # front edge pinned between +-PIN_DEG around the face


def evaluated_copy(src_names, name, decimate=None):
    old = bpy.data.objects.get(name)
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    dg = bpy.context.evaluated_depsgraph_get()
    bm = bmesh.new()
    for n in src_names:
        o = bpy.data.objects.get(n)
        if o is None:
            continue
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
        me.transform(o.matrix_world)
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    if decimate:
        bpy.ops.object.select_all(action="DESELECT")
        ob.select_set(True)
        bpy.context.view_layer.objects.active = ob
        d = ob.modifiers.new("Dec", "DECIMATE")
        d.ratio = decimate
        bpy.ops.object.modifier_apply(modifier="Dec")
    return ob


# per side (x mirrored): where the hood's face edge lies; its length along the path is matched to the sheet width
EDGE_PATH = [(0.0, -0.090, 1.978), (0.032, -0.084, 1.975), (0.058, -0.066, 1.958), (0.078, -0.042, 1.925),
             (0.086, -0.020, 1.885), (0.082, 0.008, 1.835), (0.074, 0.026, 1.775), (0.070, 0.036, 1.715)]


def path_point(d, sx):
    pts = [V((p[0] * sx, p[1], p[2])) for p in EDGE_PATH]
    for a, b in zip(pts, pts[1:]):
        L = (b - a).length
        if d <= L:
            return a.lerp(b, d / L)
        d -= L
    return None


def head_surface(head, origin, direction):
    hit, loc, nor, idx = head.ray_cast(origin, direction)
    return loc if hit else None


def build_sheet(head):
    """grid NU x NV; u across (left side -> over the top -> right side), v from the face edge backward"""
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UVMap")
    verts = []
    pins = []
    # the face edge first: EDGE_PATH (forehead -> temple -> over the ear -> behind the jaw hinge -> neck side)
    E = []
    for i in range(NU + 1):
        u = i / NU
        d = abs(u - 0.5) * HOOD_W
        sx = 1 if u >= 0.5 else -1
        q = path_point(d, sx)
        if q is None:
            E.append(None)
            continue
        o = V((0.0, 0.012, min(max(q.z, 1.72), 1.92)))
        dirv = (q - o)
        hit = head_surface(head, o, dirv.normalized())
        E.append((hit + dirv.normalized() * 0.007) if hit is not None else q)
    # every column starts (front y) where its edge point is, so no fabric starts in front of the edge
    y0 = []
    last = FRONT_Y
    for i in range(NU + 1):
        if E[i] is not None:
            last = E[i].y
        y0.append(last)
    mid = NU // 2
    for i in range(mid, -1, -1):                 # left half: carry inward values outward too
        if E[i] is not None:
            last = E[i].y
        y0[i] = last if E[i] is None else E[i].y
    for j in range(NV + 1):
        v = j / NV
        row = []
        for i in range(NU + 1):
            u = i / NU
            arc = (u - 0.5) * HOOD_W                 # metres along the sheet across
            theta = arc / 0.118                      # radians around the tube axis (0 = top)
            theta = max(-2.6, min(2.6, theta))
            y = y0[i] + 0.010 + v * HOOD_L
            # tube radius grows toward the back so the sheet starts clear of the skull, then falls
            r = 0.120 + 0.02 * v
            p = V((HOOD_C.x + r * math.sin(theta), y, HOOD_C.z + r * math.cos(theta)))
            extra = abs(arc) - 2.6 * 0.118          # beyond the sides of the tube: hang straight down
            if extra > 0:
                p.z -= extra
            if E[i] is not None:
                if j == 0:
                    p = E[i].copy()
                    pins.append((i, j))
                elif j < 4:
                    p = E[i].lerp(p, j / 4)
            row.append(bm.verts.new(p))
        verts.append(row)
    for j in range(NV):
        for i in range(NU):
            f = bm.faces.new((verts[j][i], verts[j][i + 1], verts[j + 1][i + 1], verts[j + 1][i]))
            for l, (a, b) in zip(f.loops, ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))):
                l[uvl].uv = (a / NU, 1 - b / NV)
    bm.verts.index_update()
    pin_idx = [verts[j][i].index for (i, j) in pins]
    # second row also lightly pinned to hold the hem shape
    pin2 = [verts[1][i].index for (i, j) in pins]
    me = bpy.data.meshes.new(HOOD_NAME)
    bm.to_mesh(me)
    bm.free()
    old = bpy.data.objects.get(HOOD_NAME)
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    ob = bpy.data.objects.new(HOOD_NAME, me)
    bpy.context.scene.collection.objects.link(ob)
    g = ob.vertex_groups.new(name="Pin")
    g.add(pin_idx, 1.0, "REPLACE")
    g.add(pin2, 0.6, "REPLACE")
    for p in me.polygons:
        p.use_smooth = True
    return ob


def setup_collider(ob, thickness=0.004):
    m = ob.modifiers.get("Collision") or ob.modifiers.new("Collision", "COLLISION")
    ob.collision.thickness_outer = thickness
    ob.collision.cloth_friction = 8.0
    ob.collision.damping = 0.4
    return m


def simulate(cloth_ob, frames=90):
    sc = bpy.context.scene
    m = cloth_ob.modifiers.new("Cloth", "CLOTH")
    s = m.settings
    s.quality = 8
    s.mass = 0.12
    s.air_damping = 2.0
    s.tension_stiffness = 18
    s.compression_stiffness = 18
    s.shear_stiffness = 6
    s.bending_stiffness = 0.6
    s.tension_damping = 5
    s.vertex_group_mass = "Pin"
    s.pin_stiffness = 1.0
    c = m.collision_settings
    c.use_collision = True
    c.distance_min = 0.004
    c.use_self_collision = True
    c.self_distance_min = 0.003
    c.collision_quality = 3
    m.point_cache.frame_start = 1
    m.point_cache.frame_end = frames
    sc.frame_start = 1
    sc.frame_end = frames
    for f in range(1, frames + 1):
        sc.frame_set(f)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(cloth_ob.evaluated_get(dg))
    old_me = cloth_ob.data
    cloth_ob.modifiers.remove(m)
    cloth_ob.data = me
    me.name = HOOD_NAME
    bpy.data.meshes.remove(old_me)
    sc.frame_set(1)
    return cloth_ob


def run_hood(frames=90):
    rig = bpy.data.objects.get("QuestionerRig")
    if rig:
        rig.data.pose_position = "REST"
    # the WIP head lives under FacePivot: keep the pivot neutral during the sim
    pv = bpy.data.objects.get("FacePivot")
    if pv:
        pv.rotation_euler = (0, 0, 0)
    bpy.context.view_layer.update()
    head_col = evaluated_copy(["Q_HeadHR", "Q_Teeth2"], "Q_ColHead", decimate=0.06)
    body_col = evaluated_copy(["Q_Body", "Q_Thobe", "Q_SleeveL", "Q_SleeveR"], "Q_ColBody")
    for o in (head_col, body_col):
        setup_collider(o)
        o.hide_render = True
    cloth = build_sheet(head_col)
    simulate(cloth, frames)
    for o in (head_col, body_col):
        bpy.data.objects.remove(o, do_unlink=True)
    return cloth


if globals().get("RUN_HOOD", True):
    import time
    t0 = time.time()
    hood = run_hood(globals().get("HOOD_FRAMES", 90))
    attach(hood)
    result = {"hood_verts": len(hood.data.vertices), "sec": round(time.time() - t0, 1)}
