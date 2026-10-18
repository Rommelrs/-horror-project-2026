# The Questioner v2 head - surface detail on the high-res SDF mesh: wrinkle / fold displacement, then skin paint into a
# colour attribute (cavity dirt from curvature, AO baked to vertices).  Later baked onto the game mesh.   exec(open(this).read())
import bpy, math, time
import numpy as np

HERE = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\AnimationTools\questioner"
if "UPPER_ARCH" not in globals():
    BUILD_FACE = False
    exec(open(HERE + r"\q14_face.py", encoding="utf8").read())
RUN_TEX = False
exec(open(HERE + r"\q11_tex.py", encoding="utf8").read())        # noise helpers: vnoise, fbm, ss, mix, C


# ---------------------------------------------------------------- mesh helpers
def arrays(ob):
    me = ob.data
    n = len(me.vertices)
    co = np.empty(n * 3)
    me.vertices.foreach_get("co", co)
    no = np.empty(n * 3)
    me.vertices.foreach_get("normal", no)
    ed = np.empty(len(me.edges) * 2, dtype=np.int64)
    me.edges.foreach_get("vertices", ed)
    return co.reshape(n, 3), no.reshape(n, 3), ed.reshape(-1, 2)


def neighbour_mean(x, ed, n):
    acc = np.zeros((n,) + x.shape[1:])
    cnt = np.zeros(n)
    np.add.at(acc, ed[:, 0], x[ed[:, 1]])
    np.add.at(acc, ed[:, 1], x[ed[:, 0]])
    np.add.at(cnt, ed[:, 0], 1)
    np.add.at(cnt, ed[:, 1], 1)
    cnt = np.maximum(cnt, 1)
    return acc / (cnt[:, None] if x.ndim > 1 else cnt)


def curvature(co, no, ed, smooth=2):
    """> 0 in creases / concave, < 0 on ridges (in metres of offset, roughly)"""
    n = len(co)
    lap = neighbour_mean(co, ed, n) - co
    k = (lap * no).sum(1)
    for _ in range(smooth):
        k = 0.5 * k + 0.5 * neighbour_mean(k, ed, n)
    return k


def set_co(ob, co):
    ob.data.vertices.foreach_set("co", co.ravel())
    ob.data.update()


def ridged(p, octaves=3):
    """sharp crease network in [0,1] (1 on the creases)"""
    return (1 - np.abs(2 * fbm(p, octaves) - 1)) ** 4


def seg_dist(P, a, b):
    a, b = np.array(a, float), np.array(b, float)
    ba = b - a
    t = np.clip(((P - a) @ ba) / (ba @ ba), 0, 1)
    return np.linalg.norm(P - (a + t[:, None] * ba), axis=1), t


# ---------------------------------------------------------------- wrinkles / folds (displacement along the normal, metres)
def wrinkle_field(P, N):
    x, y, z = P[:, 0], P[:, 1], P[:, 2]
    ax = np.abs(x)
    front = ss(-0.04, -0.08, y)                                     # face side of the head
    h = np.zeros(len(P))
    # forehead: deep horizontal furrows bending down over the brow, plus the frown lines between the brows
    # forehead: 5 long, uneven horizontal creases that sag at the temples (each its own wobble, start and end)
    fore = front * ss(-0.55, -0.80, N[:, 1]) * (1 - ss(0.050, 0.064, ax))
    rng = np.random.default_rng(21)
    for k in range(5):
        z0 = 1.957 + 0.0078 * k + rng.uniform(-0.0015, 0.0015)
        ph, amp, x0, x1 = rng.uniform(0, 6), rng.uniform(0.0012, 0.0028), rng.uniform(-0.050, -0.020), rng.uniform(0.020, 0.050)
        zl = z0 + amp * np.sin(x * 70 + ph) + 0.0055 * (x / 0.05) ** 2 + 0.0015 * (fbm(P * np.array((40, 40, 1)) + k, 2) - 0.5)
        span = ss(x0 - 0.012, x0 + 0.004, x) * (1 - ss(x1 - 0.004, x1 + 0.012, x))
        dz = z - zl
        depth = rng.uniform(0.0011, 0.0017)
        h += fore * span * (-depth * np.exp(-(dz / 0.0011) ** 2) + 0.35 * depth * np.exp(-((dz - 0.0030) / 0.0018) ** 2))
    for sx in (1, -1):
        d, t = seg_dist(P, (0.006 * sx, -0.090, 1.944), (0.010 * sx, -0.086, 1.975))
        h -= 0.0014 * np.exp(-(d / 0.0018) ** 2)
    # eyes: crow's feet fanning from the outer corners, folds under the eyes, lid creases
    for sx in (1, -1):
        ec = np.array(mirror(EYE_C, sx))
        corner = ec + np.array((0.014 * sx, -0.008, -0.002))
        q = P - corner
        r = np.linalg.norm(q, axis=1)
        ang = np.arctan2(q[:, 2], q[:, 0] * sx)
        fan = -np.clip(np.sin(ang * 9 + fbm(P * 60, 2) * 3), 0, 1) ** 4
        h += fan * 0.0008 * ss(0.004, 0.010, r) * (1 - ss(0.02, 0.032, r)) * (q[:, 0] * sx > -0.002)
        q2 = P - (ec + np.array((0, -0.006, -0.003)))
        rr = np.linalg.norm(q2[:, [0, 2]] / np.array((1.0, 0.8)), axis=1)
        arcs = -np.clip(np.sin(rr * 2 * math.pi / 0.0045), 0, 1) ** 3
        h += arcs * 0.0007 * ss(0.012, 0.016, rr) * (1 - ss(0.026, 0.032, rr)) * (q2[:, 2] < 0.002) * front
    # long vertical folds down the cheeks and the sides of the jaw (stretched skin)
    xw = ax + 0.004 * (fbm(P * np.array((8, 8, 30)), 2) - 0.5)
    folds = -np.clip(np.sin(xw * 2 * math.pi / 0.011 + z * 40), 0, 1) ** 3
    cheek = ss(1.66, 1.72, z) * (1 - ss(1.88, 1.90, z)) * ss(0.022, 0.036, ax) * front
    h += folds * 0.0018 * cheek
    # nasolabial folds: from the nose wings down past the mouth corners
    for sx in (1, -1):
        d, t = seg_dist(P, (0.016 * sx, -0.105, 1.866), (0.056 * sx, -0.070, 1.830))
        h -= 0.0016 * np.exp(-(d / 0.0024) ** 2)
        h += 0.0010 * np.exp(-((d - 0.004) / 0.0025) ** 2)          # the bulge above the fold
    # lower jaw: long stretched creases running down the jaw, on its outer skin
    sj = (P - JAW_O) @ JAW_A
    dj = (P - JAW_O) @ (-JAW_F)
    jaw = ss(-0.01, 0.02, sj) * (1 - ss(0.22, 0.25, sj)) * ss(0.004, 0.012, dj)
    streak = ridged(np.stack([x * 130, dj * 130, sj * 14], -1), 3)
    h -= jaw * streak * 0.0011
    # neck: tendons already in the shape; add vertical stringy cords and horizontal skin rings
    neck = ss(1.60, 1.63, z) * (1 - ss(1.83, 1.86, z)) * (1 - ss(-0.06, -0.08, y))
    th = np.arctan2(x, -(y - 0.012))
    cords = -np.clip(np.sin(th * 14 + fbm(P * np.array((30, 30, 4)), 2) * 4), 0, 1) ** 4
    rings = -np.clip(np.sin(z * 2 * math.pi / 0.013 + fbm(P * 20, 2) * 9), 0, 1) ** 5 * ss(0.4, 0.65, fbm(P * 40 + 5, 2))
    h += neck * (cords * 0.0010 + rings * 0.0006)
    # ribs/sternum skin: thin, a few stretch lines
    # everywhere: leathery crease network at two scales + pores
    h -= ridged(P * np.array((110, 110, 45))) * 0.00060
    h -= ridged(P * 170 + 7) * 0.00028
    h += (fbm(P * 600, 2) - 0.5) * 0.00012
    # inside the mouth: fleshy ridges running along the jaw
    return h


# ---------------------------------------------------------------- masks from the SDF parts
def cavity_dist(P):
    d = np.full(len(P), 1.0)
    for s, rx, depth in sample(CAVITY, 0.004):
        half = (depth + CAV_OUT) / 2
        d = np.minimum(d, Slab(jaw_pt(s, 0, half - CAV_OUT), rx, half, 0.0022, 0.008, JAW_R).dist(P.astype(np.float32)))
    d = np.minimum(d, Ellipsoid((0, -0.078, 1.826), (0.046, 0.036, 0.014)).dist(P.astype(np.float32)))
    d = np.minimum(d, Capsule((0, -0.060, 1.828), (0, -0.016, 1.770), 0.020, 0.014).dist(P.astype(np.float32)))
    return d


def gum_dist(P):
    d = np.full(len(P), 1.0)
    P32 = P.astype(np.float32)
    for sx in (1, -1):
        d = np.minimum(d, Chain([(p[0] * sx * 0.97, p[1] + 0.003, p[2] + 0.001) for p in UPPER_ARCH], [0.0058] * len(UPPER_ARCH)).dist(P32))
        rim = rim_points(sx)
        d = np.minimum(d, Chain(rim, [0.0060] * len(rim)).dist(P32))
    return d


# ---------------------------------------------------------------- paint
SKIN_DARK = C(0.20, 0.14, 0.10)
SKIN_MID = C(0.46, 0.38, 0.30)
SKIN_PALE = C(0.63, 0.55, 0.45)
SKIN_RED = C(0.42, 0.24, 0.18)
GRIME = C(0.14, 0.10, 0.07)


def paint(P, N, K, AO):
    x, y, z = P[:, 0], P[:, 1], P[:, 2]
    ax = np.abs(x)
    n1, n2, n3 = fbm(P * 9, 4), fbm(P * 28 + 3, 3), fbm(P * 80 + 9, 2)
    col = mix(SKIN_MID * 0.9, SKIN_PALE, ss(0.35, 0.75, n1))
    col = mix(col, SKIN_MID * 0.75, ss(0.45, 0.75, n2) * 0.5)
    # blotches: reddish raw patches and dark liver spots, speckled dirt
    col = mix(col, SKIN_RED, ss(0.64, 0.80, fbm(P * 14 + 21, 3)) * 0.40)
    col = mix(col, GRIME, ss(0.72, 0.82, fbm(P * 45 + 33, 3)) * 0.65)
    col = mix(col, GRIME * 0.8, (n3 > 0.80) * 0.6)
    # cracked, dirty leather: the same crease network as the displacement, dark; plus grime speckles
    crack = np.clip(ridged(P * np.array((110, 110, 45))) * 0.7 + ridged(P * 170 + 7) * 0.3, 0, 1)
    col = mix(col, SKIN_DARK * 0.6, crack * 0.38)
    col = mix(col, GRIME * 0.8, ss(0.76, 0.86, fbm(P * 230 + 17, 2)) * 0.40)
    col = mix(col, SKIN_PALE * 1.05, ss(0.70, 0.85, fbm(P * 120 + 41, 2)) * 0.25)
    sj = (P - JAW_O) @ JAW_A
    dj = (P - JAW_O) @ (-JAW_F)
    jaw = ss(-0.01, 0.02, sj) * (1 - ss(0.22, 0.25, sj)) * ss(0.004, 0.012, dj)
    col = mix(col, SKIN_DARK * 0.6, jaw * ridged(np.stack([x * 130, dj * 130, sj * 14], -1), 3) * 0.55)
    # crevices dark, ridges pale (curvature in metres per edge ~ +-0.0003)
    kk = K / 0.00035
    col = mix(col, SKIN_DARK * 0.6, ss(0.15, 1.0, kk) * 0.85)
    col = mix(col, SKIN_PALE * 1.08, ss(-0.2, -1.0, kk) * 0.45)
    # sunken dark eye sockets, red-rimmed lids
    for sx in (1, -1):
        ec = np.array(mirror(EYE_C, sx))
        d = np.linalg.norm((P - ec) / np.array((1.2, 1.0, 1.1)), axis=1)
        col = mix(col, C(0.08, 0.05, 0.04), (1 - ss(0.014, 0.034, d)) * 0.88)
        col = mix(col, C(0.20, 0.09, 0.08), (1 - ss(0.0135, 0.017, d)) * ss(0.012, 0.0135, d) * 0.5)
    # nostrils, raw nose tip
    nd = np.linalg.norm(P - np.array(NOSE_TIP), axis=1)
    col = mix(col, C(0.40, 0.22, 0.17), (1 - ss(0.004, 0.014, nd)) * 0.35)
    for sx in (1, -1):
        d = np.linalg.norm(P - np.array((0.0062 * sx, -0.115, 1.8615)), axis=1)
        col = mix(col, C(0.03, 0.01, 0.01), 1 - ss(0.003, 0.0075, d))
    # mouth: wet dark-red flesh, black down the throat, gums
    cd = cavity_dist(P)
    inside = 1 - ss(0.0035, 0.0075, cd)
    dep = (P - JAW_O) @ (-JAW_F)                                    # behind the jaw's front face
    depth = ss(0.016, 0.055, dep) * 0.62 + ss(1.80, 1.835, z) * ss(-0.09, -0.04, y) + ss(-0.05, -0.02, y) * ss(1.76, 1.80, z)
    flesh = mix(C(0.36, 0.09, 0.07), C(0.20, 0.05, 0.04), fbm(P * 60, 3))
    flesh = mix(flesh, C(0.48, 0.18, 0.14), ss(0.6, 0.85, fbm(P * np.array((90, 90, 25)), 2)) * 0.5)
    flesh = mix(flesh, C(0.03, 0.008, 0.006), np.clip(depth, 0, 1))
    col = mix(col, flesh, inside)
    gd = gum_dist(P)
    gum = mix(C(0.27, 0.10, 0.075), C(0.16, 0.05, 0.04), fbm(P * 120, 2))
    col = mix(col, gum, (1 - ss(0.0010, 0.0050, gd)) * 0.95)
    # dried blood: around the gums/rims and running down the jaw and neck
    near_mouth = (1 - ss(0.002, 0.016, np.minimum(gd, np.abs(cd))))
    drip = ss(0.55, 0.75, fbm(np.stack([x * 140, y * 140, z * 12], -1), 3))
    blood = np.clip(near_mouth * 0.7 + drip * ss(1.80, 1.62, z) * front_mask(P) * 0.5, 0, 1) * (1 - inside)
    col = mix(col, C(0.22, 0.05, 0.04), blood * ss(0.35, 0.65, fbm(P * 35 + 50, 3)))
    # ambient occlusion (baked to vertices) - strong in the creases, keeps the mouth black
    col = col * (0.35 + 0.65 * AO[:, None] ** 1.3)
    # smoothness (goes to the albedo alpha in Unity): dry skin, wet gums / rims, matte deep throat
    gloss = 0.10 + 0.06 * ss(0.5, 0.8, fbm(P * 40 + 3, 2))
    lerp = lambda a, b, t: a + (b - a) * t                          # 1-D (mix() is for colours)
    gloss = lerp(gloss, 0.30, (1 - ss(0.0010, 0.0050, gd)))
    gloss = lerp(gloss, 0.18 * (1 - np.clip(depth * 1.6, 0, 1)) + 0.03, inside)
    gloss = lerp(gloss, 0.22, blood * 0.5)
    global LAST_GLOSS
    LAST_GLOSS = np.clip(gloss, 0, 1)
    return np.clip(col, 0, 1)


def front_mask(P):
    return ss(-0.03, -0.07, P[:, 1])


def bake_ao_vertices(ob, samples=64, distance=0.05):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.samples = samples
    sc.cycles.device = "CPU"
    w = sc.world or bpy.data.worlds.new("World")
    sc.world = w
    w.light_settings.distance = distance
    me = ob.data
    if "AO" in me.color_attributes:
        me.color_attributes.remove(me.color_attributes["AO"])
    attr = me.color_attributes.new("AO", "FLOAT_COLOR", "POINT")
    me.color_attributes.active_color = attr
    sc.render.bake.target = "VERTEX_COLORS"
    # only the head itself occludes (the hood / clothes must not darken the skin paint)
    keep = {ob.name, "Q_Teeth2", "Q_Eyes2"}
    saved = {o.name: o.hide_render for o in bpy.data.objects}
    for o in bpy.data.objects:
        o.hide_render = o.name not in keep
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.bake(type="AO")
    for n, v in saved.items():
        bpy.data.objects[n].hide_render = v
    sc.render.bake.target = "IMAGE_TEXTURES"
    a = np.empty(len(me.vertices) * 4, dtype=np.float32)
    attr.data.foreach_get("color", a)
    return a.reshape(-1, 4)[:, 0].astype(np.float64)


def write_colors(ob, col, name="Col"):
    me = ob.data
    if name in me.color_attributes:
        me.color_attributes.remove(me.color_attributes[name])
    attr = me.color_attributes.new(name, "FLOAT_COLOR", "POINT")
    lin = np.clip(col, 0, 1) ** 2.2                       # painted in sRGB, the attribute is linear
    rgba = np.concatenate([lin, np.ones((len(col), 1))], 1).astype(np.float32)
    attr.data.foreach_set("color", rgba.ravel())
    me.color_attributes.active_color = attr
    return attr


def vc_material(ob, name="M_HeadVC"):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    b = [n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"][0]
    a = nt.nodes.get("ColAttr") or nt.nodes.new("ShaderNodeAttribute")
    a.name = "ColAttr"
    a.attribute_name = "Col"
    nt.links.new(a.outputs["Color"], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.70
    if "Specular IOR Level" in b.inputs:
        b.inputs["Specular IOR Level"].default_value = 0.25
    ob.data.materials.clear()
    ob.data.materials.append(m)


def detail_and_paint(ob, ao_samples=48):
    t0 = time.time()
    co, no, ed = arrays(ob)
    co = co + no * wrinkle_field(co, no)[:, None]
    set_co(ob, co)
    co, no, ed = arrays(ob)
    K = curvature(co, no, ed, smooth=1)
    AO = bake_ao_vertices(ob, samples=ao_samples)
    col = paint(co, no, K, AO)
    write_colors(ob, col)
    g = LAST_GLOSS
    write_colors(ob, np.stack([g, g, g], 1) ** (1 / 2.2), "Gloss")        # stored linear = gloss value
    ob.data.color_attributes.active_color = ob.data.color_attributes["Col"]
    vc_material(ob)
    return round(time.time() - t0, 1)


def paint_teeth(ob, tv):
    """tv[:, 0] = 0 at the gum .. 1 at the tip, tv[:, 1] = tooth id"""
    co, no, ed = arrays(ob)
    t, tid = tv[:, 0], tv[:, 1].astype(int)
    rng = np.random.default_rng(5)
    per = rng.random(tid.max() + 1)
    shade = 0.75 + 0.35 * rng.random(tid.max() + 1)
    root = C(0.24, 0.10, 0.07)
    body = C(0.50, 0.38, 0.20)
    tip = C(0.74, 0.65, 0.46)
    col = mix(root, body, ss(-0.05, 0.25, t))
    col = mix(col, tip, ss(0.45, 0.95, t))
    col = mix(col, C(0.28, 0.18, 0.08), ss(0.55, 0.85, fbm(co * 400, 2)) * 0.5)          # stains
    rot = per[tid] > 0.84                                                                 # a few rotten teeth
    col[rot] = mix(col[rot], C(0.20, 0.12, 0.06), 0.65)
    col = col * shade[tid][:, None]
    write_colors(ob, np.clip(col, 0, 1))
    vc_material(ob, "M_TeethVC")
    m = bpy.data.materials["M_TeethVC"]
    [n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"][0].inputs["Roughness"].default_value = 0.35


def paint_eyes(ob):
    co, no, ed = arrays(ob)
    col = np.zeros((len(co), 3))
    for sx in (1, -1):
        sel = np.sign(co[:, 0]) == sx
        q = co[sel] - np.array(mirror(EYE_C, sx))
        q = q / np.linalg.norm(q, axis=1)[:, None]
        look = np.array((0.30 * sx, -1.0, 0.42))
        look = look / np.linalg.norm(look)
        c = q @ look
        sclera = mix(C(0.34, 0.27, 0.17), C(0.34, 0.11, 0.08), ss(0.5, 0.8, fbm(co[sel] * 500, 2)) * 0.6)
        sclera = mix(sclera, C(0.25, 0.12, 0.08), ss(0.3, -0.4, c))
        iris = mix(C(0.20, 0.12, 0.06), C(0.09, 0.05, 0.03), ss(0.93, 0.85, c))
        e = mix(sclera, iris, ss(0.80, 0.84, c))
        e = mix(e, C(0.01, 0.01, 0.01), ss(0.955, 0.965, c))
        col[sel] = e
    write_colors(ob, col)
    vc_material(ob, "M_EyeVC")
    m = bpy.data.materials["M_EyeVC"]
    [n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"][0].inputs["Roughness"].default_value = 0.12


if globals().get("RUN_DETAIL", True):
    result = {"sec": detail_and_paint(bpy.data.objects["Q_HeadHR"])}
    if "TOOTH_TV" in globals():
        paint_teeth(bpy.data.objects["Q_Teeth2"], TOOTH_TV)
    paint_eyes(bpy.data.objects["Q_Eyes2"])
