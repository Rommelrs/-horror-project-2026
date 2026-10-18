# The Questioner v2 - put the new SDF head / teeth / eyes / cloth-sim hood into the game parts and make the head textures.
#   1. Q_Head   = decimated copy of Q_HeadHR (detail kept around eyes, nose, mouth), smart UV in the top-left 86 %
#   2. Q_Teeth  = Q_Teeth2, every tooth mapped onto one of 12 tooth cells in the bottom strip (painted procedurally)
#   3. Q_Eyes   = Q_Eyes2, both eyes share the top-right square (painted procedurally)
#   4. T_Questioner_Head (albedo, 2048) + T_Questioner_Head_N (normal): HR -> LR Cycles bakes + the painted cells
#   5. Q_KeffiyehHood = decimated cloth sim (UV = the sheet), old tail removed
# Then run q09 (skin), q10 (V2: keeps these UVs), q11 (V2: only limbs/cloth/hood), q12 (export).   exec(open(this).read())
import bpy, bmesh, math, os
import numpy as np

HERE = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\AnimationTools\questioner"
TEXDIR = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\Assets\Models\TheQuestioner\Textures"
BUILD_FACE = False
exec(open(HERE + r"\q14_face.py", encoding="utf8").read())
RUN_DETAIL = False
exec(open(HERE + r"\q16_detail.py", encoding="utf8").read())
HEAD_RES = globals().get("HEAD_RES", 2048)
HEAD_TRIS = globals().get("HEAD_TRIS", 8000)
HOOD_RATIO = globals().get("HOOD_RATIO", 0.5)
HEAD_BOX = (0.0, 0.14, 0.86)        # u0, v0, size of the head square
TEETH_STRIP = (0.0, 0.0, 1.0, 0.13)  # u0, v0, w, h : 12 tooth cells
TOOTH_CELLS = 12
EYE_BOX = (0.87, 0.87, 0.12)


def unparent_keep(ob):
    mw = ob.matrix_world.copy()
    ob.parent = None
    ob.matrix_world = mw


def remove(name):
    o = bpy.data.objects.get(name)
    if o:
        bpy.data.objects.remove(o, do_unlink=True)


def activate(ob):
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob


# ---------------------------------------------------------------- 1. low-res head
def detail_weight(P):
    """1 where small features must survive decimation (eyes, nose, gums, rims), low on the hidden chest / cranium"""
    w = np.full(len(P), 0.45)
    for sx in (1, -1):
        d = np.linalg.norm(P - np.array(mirror(EYE_C, sx)), axis=1)
        w = np.maximum(w, 1 - ss(0.016, 0.026, d))
    w = np.maximum(w, 1 - ss(0.010, 0.022, np.linalg.norm(P - np.array(NOSE_TIP), axis=1)))
    w = np.maximum(w, (1 - ss(0.003, 0.010, gum_dist(P))) * 0.9)
    w = np.where(P[:, 2] < 1.60, 0.15, w)
    return w


def make_head_lr():
    hr = bpy.data.objects["Q_HeadHR"]
    unparent_keep(hr)
    remove("Q_Head")
    lr = hr.copy()
    lr.data = hr.data.copy()
    lr.name = "Q_Head"
    lr.data.name = "Q_Head"
    bpy.context.scene.collection.objects.link(lr)
    for a in list(lr.data.color_attributes):
        lr.data.color_attributes.remove(a)
    co, no, ed = arrays(lr)
    g = lr.vertex_groups.new(name="Detail")
    w = detail_weight(co)
    for val in np.unique(np.round(w, 2)):
        idx = np.nonzero(np.round(w, 2) == val)[0].tolist()
        if idx:
            g.add(idx, float(val), "REPLACE")
    activate(lr)
    tris = sum(len(p.vertices) - 2 for p in lr.data.polygons)
    d = lr.modifiers.new("Dec", "DECIMATE")
    d.ratio = HEAD_TRIS / tris
    d.use_collapse_triangulate = True
    d.vertex_group = "Detail"
    d.invert_vertex_group = True          # (Blender: inverted weight 1 = protected from collapsing)
    d.vertex_group_factor = 2.0
    bpy.ops.object.modifier_apply(modifier="Dec")
    lr.vertex_groups.clear()
    for p in lr.data.polygons:
        p.use_smooth = True
    lr.data.materials.clear()
    # UV: smart project, then into the head square
    lr.data.uv_layers.new(name="UVMap")
    activate(lr)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.003, area_weight=0.0, scale_to_bounds=False)
    bpy.ops.uv.select_all(action="SELECT")
    try:
        bpy.ops.uv.pack_islands(udim_source="CLOSEST_UDIM", rotate=True, shape_method="CONCAVE", margin_method="FRACTION", margin=0.0025)
    except TypeError:
        bpy.ops.uv.pack_islands(rotate=True, margin=0.0025)
    bpy.ops.object.mode_set(mode="OBJECT")
    u0, v0, s = HEAD_BOX
    uv = lr.data.uv_layers["UVMap"].data
    a = np.empty(len(uv) * 2)
    uv.foreach_get("uv", a)
    a = a.reshape(-1, 2) * s + np.array((u0, v0))
    uv.foreach_set("uv", a.ravel())
    return hr, lr


# ---------------------------------------------------------------- 2./3. teeth and eyes UVs (cells painted procedurally)
def teeth_uv(ob, tv, segs=6):
    """per vertex: t (0 gum .. 1 tip) and tooth id -> cell; angle around from the vertex order inside its ring"""
    me = ob.data
    uvl = me.uv_layers.get("UVMap") or me.uv_layers.new(name="UVMap")
    u0, v0, w, h = TEETH_STRIP
    cw = w / TOOTH_CELLS
    for poly in me.polygons:
        for li in poly.loop_indices:
            vi = me.loops[li].vertex_index
            t, tid = tv[vi]
            cell = int(tid) % TOOTH_CELLS
            k = (vi % (segs * 6 + 1)) % segs
            ring_pos = 0.5 + 0.5 * math.cos(2 * math.pi * k / segs) if t < 0.999 else 0.5
            uvl.data[li].uv = (u0 + cw * (cell + 0.08 + 0.84 * ring_pos), v0 + h * (0.06 + 0.88 * (t + 0.3) / 1.3))


def eyes_uv(ob):
    me = ob.data
    uvl = me.uv_layers.get("UVMap") or me.uv_layers.new(name="UVMap")
    u0, v0, s = EYE_BOX
    for poly in me.polygons:
        for li in poly.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            sx = 1 if co.x > 0 else -1
            q = (np.array(co[:]) - np.array(mirror(EYE_C, sx))) / EYE_R
            # front hemisphere projected straight on (the back is never seen)
            uvl.data[li].uv = (u0 + s * (0.5 + 0.48 * q[0] * sx), v0 + s * (0.5 + 0.48 * q[2]))


def paint_tooth_cells(img_rgb, res):
    u0, v0, w, h = TEETH_STRIP
    cw = w / TOOTH_CELLS
    ys, xs = np.mgrid[0:res, 0:res]
    U = (xs + 0.5) / res
    Vv = (ys + 0.5) / res
    inside = (U >= u0) & (U < u0 + w) & (Vv >= v0) & (Vv < v0 + h)
    cell = np.clip(((U - u0) / cw).astype(int), 0, TOOTH_CELLS - 1)
    t = ((Vv - v0) / h - 0.06) / 0.88 * 1.3 - 0.3
    ring = ((U - u0) / cw - cell - 0.08) / 0.84
    rng = np.random.default_rng(3)
    shade = 0.82 + 0.25 * rng.random(TOOTH_CELLS)
    rotten = rng.random(TOOTH_CELLS) > 0.8
    P3 = np.stack([U * 40, Vv * 40, cell * 1.7], -1)
    root = C(0.30, 0.13, 0.09)
    body = C(0.56, 0.46, 0.30)
    tip = C(0.76, 0.68, 0.52)
    col = mix(root, body, ss(-0.08, 0.12, t))
    col = mix(col, tip, ss(0.40, 0.95, t))
    streaks = fbm(np.stack([U * 900, Vv * 25, cell * 1.7], -1), 2)
    col = mix(col, C(0.34, 0.22, 0.10), ss(0.45, 0.75, streaks) * 0.45)                # vertical stain streaks
    col = mix(col, C(0.20, 0.12, 0.05), (np.abs(ring - 0.5) > 0.38) * 0.35)           # darker sides (fake curvature)
    col = mix(col, C(0.18, 0.10, 0.05), rotten[cell] * 0.6)
    col = col * shade[cell][..., None]
    img_rgb[inside] = col[inside]
    return inside


def paint_eye_box(img_rgb, res):
    u0, v0, s = EYE_BOX
    ys, xs = np.mgrid[0:res, 0:res]
    U = ((xs + 0.5) / res - u0) / s
    Vv = ((ys + 0.5) / res - v0) / s
    inside = (U >= -0.02) & (U <= 1.02) & (Vv >= -0.02) & (Vv <= 1.02)
    qx = (U - 0.5) / 0.48
    qz = (Vv - 0.5) / 0.48
    qy = -np.sqrt(np.clip(1 - qx ** 2 - qz ** 2, 0, 1))
    q = np.stack([qx, qy, qz], -1)
    look = np.array((0.16, -1.0, 0.10))             # glancing slightly up / outward, iris still inside the lid slit
    look /= np.linalg.norm(look)
    c = q @ look
    P3 = np.stack([U * 30, Vv * 30, U * 0], -1)
    sclera = mix(C(0.20, 0.15, 0.10), C(0.22, 0.08, 0.05), ss(0.5, 0.8, fbm(P3 * 4, 2)) * 0.6)
    sclera = mix(sclera, C(0.12, 0.07, 0.05), ss(0.5, -0.2, c))
    iris = mix(C(0.22, 0.13, 0.06), C(0.08, 0.045, 0.025), ss(0.95, 0.86, c))
    e = mix(sclera, iris, ss(0.78, 0.82, c))
    e = mix(e, C(0.01, 0.01, 0.01), ss(0.955, 0.965, c))
    img_rgb[inside] = e[inside]
    return inside


# ---------------------------------------------------------------- 4. bakes
def new_image(name, res, non_color=False, alpha=False):
    img = bpy.data.images.get(name)
    if img:
        bpy.data.images.remove(img)
    img = bpy.data.images.new(name, res, res, alpha=alpha, float_buffer=False)
    if non_color:
        img.colorspace_settings.name = "Non-Color"
    return img


def bake_material_hr(hr, attr="Col"):
    """HR: emission = a colour attribute (for the EMIT bake)"""
    m = bpy.data.materials.get("M_HR_Bake") or bpy.data.materials.new("M_HR_Bake")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    at = nt.nodes.new("ShaderNodeAttribute")
    at.attribute_name = attr
    nt.links.new(at.outputs["Color"], em.inputs["Color"])
    nt.links.new(em.outputs[0], out.inputs[0])
    hr.data.materials.clear()
    hr.data.materials.append(m)


def target_material(lr, img):
    m = bpy.data.materials.get("M_LR_Bake") or bpy.data.materials.new("M_LR_Bake")
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        if n.type == "TEX_IMAGE":
            nt.nodes.remove(n)
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    nt.nodes.active = tex
    lr.data.materials.clear()
    lr.data.materials.append(m)


def bake_hr_to_lr(hr, lr, kind, img):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 4 if kind != "AO" else 64
    sc.render.bake.target = "IMAGE_TEXTURES"
    target_material(lr, img)
    for o in (hr, lr):
        o.hide_render = False
        o.hide_set(False)
    bpy.ops.object.select_all(action="DESELECT")
    hr.select_set(True)
    lr.select_set(True)
    bpy.context.view_layer.objects.active = lr
    kw = dict(type=kind, use_selected_to_active=True, cage_extrusion=0.012, max_ray_distance=0.03, margin=8, use_clear=True)
    if kind == "NORMAL":
        kw["normal_space"] = "TANGENT"
    bpy.ops.object.bake(**kw)


def image_np(img):
    w, h = img.size
    a = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(h, w, 4)


def save_np(img, arr, path):
    img.pixels.foreach_set(arr.astype(np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()


def make_head_textures(hr, lr):
    bake_material_hr(hr)
    alb = new_image("T_Questioner_Head", HEAD_RES, alpha=True)
    bake_hr_to_lr(hr, lr, "EMIT", alb)
    nrm = new_image("T_Questioner_Head_N", HEAD_RES, non_color=True)
    bake_hr_to_lr(hr, lr, "NORMAL", nrm)
    bake_material_hr(hr, "Gloss")
    gl = new_image("Q_HeadGloss", HEAD_RES, non_color=True)
    bake_hr_to_lr(hr, lr, "EMIT", gl)
    bake_material_hr(hr, "AO")
    ao = new_image("T_Questioner_Head_AO", HEAD_RES // 2, non_color=True)
    bake_hr_to_lr(hr, lr, "EMIT", ao)
    bake_material_hr(hr, "Col")
    A = image_np(alb)
    N = image_np(nrm)
    G = image_np(gl)
    AOi = image_np(ao)
    # image rows run bottom -> top, same as our v
    rgb_lin = A[..., :3].copy()
    # paint cells (sRGB values) -> convert to the image's linear float representation
    cells = np.zeros(A.shape[:2] + (3,))
    m1 = paint_tooth_cells(cells, HEAD_RES)
    m2 = paint_eye_box(cells, HEAD_RES)
    m = m1 | m2
    rgb_lin[m] = np.clip(cells[m], 0, 1)             # byte images keep sRGB values in .pixels (no conversion)
    A[..., :3] = rgb_lin
    A[..., 3] = np.clip(G[..., 0], 0, 1)
    A[m1, 3] = 0.35                                   # teeth: a little wet
    A[m2, 3] = 0.38                                   # eyes: wet but dull
    N[m, 0:3] = (0.5, 0.5, 1.0)
    N[..., 3] = 1
    # occlusion: half resolution; the painted cells get none
    ys, xs = np.nonzero(m)
    AOi[ys // 2, xs // 2, 0:3] = 1.0
    AOi[..., 3] = 1
    alb.alpha_mode = "STRAIGHT"
    save_np(alb, A, os.path.join(TEXDIR, "T_Questioner_Head.png"))
    save_np(nrm, N, os.path.join(TEXDIR, "T_Questioner_Head_N.png"))
    save_np(ao, AOi, os.path.join(TEXDIR, "T_Questioner_Head_AO.png"))
    return alb, nrm


# ---------------------------------------------------------------- 5. hood
def make_hood():
    src = bpy.data.objects["Q_Hood2"]
    unparent_keep(src)
    remove("Q_KeffiyehHood")
    remove("Q_KeffiyehTail")
    hood = src.copy()
    hood.data = src.data.copy()
    hood.name = "Q_KeffiyehHood"
    hood.data.name = "Q_KeffiyehHood"
    bpy.context.scene.collection.objects.link(hood)
    hood.vertex_groups.clear()
    activate(hood)
    if HOOD_RATIO < 1:
        d = hood.modifiers.new("Dec", "DECIMATE")
        d.ratio = HOOD_RATIO
        d.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier="Dec")
    hood.data.materials.clear()
    for p in hood.data.polygons:
        p.use_smooth = True
    return hood


def swap_parts():
    """replace the v1 teeth/eyes with the v2 ones (same object names the rest of the pipeline expects)"""
    t2 = bpy.data.objects["Q_Teeth2"]
    e2 = bpy.data.objects["Q_Eyes2"]
    for o in (t2, e2):
        unparent_keep(o)
        o.data.materials.clear()
        for a in list(o.data.color_attributes):
            o.data.color_attributes.remove(a)
    remove("Q_Teeth")
    remove("Q_Eyes")
    t2.name = t2.data.name = "Q_Teeth"
    e2.name = e2.data.name = "Q_Eyes"
    return t2, e2


if globals().get("RUN_V2", True):
    hr, lr = make_head_lr()
    # tooth parameters come from q15 (TOOTH_TV) - rebuild the teeth if this is a fresh session
    if "TOOTH_TV" not in globals():
        exec(open(HERE + r"\q15_teeth_eyes.py", encoding="utf8").read())
    teeth, eyes = swap_parts()
    teeth_uv(teeth, TOOTH_TV)
    eyes_uv(eyes)
    alb, nrm = make_head_textures(hr, lr)
    hood = make_hood()
    result = {"head_tris": sum(len(p.vertices) - 2 for p in lr.data.polygons),
              "teeth_tris": sum(len(p.vertices) - 2 for p in teeth.data.polygons),
              "hood_tris": sum(len(p.vertices) - 2 for p in hood.data.polygons)}
