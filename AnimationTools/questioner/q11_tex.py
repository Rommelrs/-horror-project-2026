# The Questioner - texture painting.   Blender: exec(open(this).read())
# 1) rasterise each mesh in UV space -> per texel rest-pose position, normal, part id
# 2) paint albedo procedurally (numpy noise) per material
# 3) bake ambient occlusion with Cycles (all parts present), multiply in, dilate seams, save PNGs into the Unity project
import bpy, bmesh, math, os
import numpy as np
from mathutils import Vector

HERE = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\AnimationTools\questioner"
OUT = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\Assets\Models\TheQuestioner\Textures"
os.makedirs(OUT, exist_ok=True)
BUILD_RIG = False
exec(open(HERE + r"\q01_rig.py", encoding="utf8").read())
RES = globals().get("TEX_RES", 1024)

# ---------------------------------------------------------------- noise
_rng = np.random.default_rng(1234)
_PERM = _rng.permutation(4096).astype(np.int64)
_GRAD = _rng.random(4096)


def _hash(ix, iy, iz):
    h = (ix * 73856093) ^ (iy * 19349663) ^ (iz * 83492791)
    return _GRAD[_PERM[np.abs(h) % 4096]]


def vnoise(p):
    """3D value noise, p (..., 3) -> (...) in [0,1]"""
    f = np.floor(p)
    t = p - f
    t = t * t * (3 - 2 * t)
    i = f.astype(np.int64)
    out = 0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                w = (t[..., 0] if dx else 1 - t[..., 0]) * (t[..., 1] if dy else 1 - t[..., 1]) * (t[..., 2] if dz else 1 - t[..., 2])
                out = out + w * _hash(i[..., 0] + dx, i[..., 1] + dy, i[..., 2] + dz)
    return out


def fbm(p, octaves=4, lac=2.0, gain=0.5):
    a, s, tot = 1.0, 0.0, 0.0
    q = p
    for o in range(octaves):
        s = s + a * vnoise(q + o * 17.3)
        tot += a
        a *= gain
        q = q * lac
    return s / tot


def ss(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def mix(a, b, t):
    t = np.asarray(t)[..., None] if np.ndim(t) else t
    return a * (1 - t) + b * t


def C(*rgb):
    return np.array(rgb, dtype=np.float64)

# ---------------------------------------------------------------- rasteriser


def islands(me):
    """vertex island id per vertex"""
    n = len(me.vertices)
    parent = list(range(n))
    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    for e in me.edges:
        a, b = find(e.vertices[0]), find(e.vertices[1])
        if a != b:
            parent[a] = b
    return np.array([find(i) for i in range(n)])


def rasterise(ob, res=RES):
    me = ob.data
    me.calc_loop_triangles()
    uvl = me.uv_layers["UVMap"].data
    co = np.array([v.co[:] for v in me.vertices])
    vn = np.array([v.normal[:] for v in me.vertices])
    isl = islands(me)
    P = np.zeros((res, res, 3))
    N = np.zeros((res, res, 3))
    UVP = np.zeros((res, res, 2))
    ISL = -np.ones((res, res), dtype=np.int64)
    M = np.zeros((res, res), dtype=bool)
    for tri in me.loop_triangles:
        uv = np.array([uvl[l].uv[:] for l in tri.loops]) * res
        vs = list(tri.vertices)
        x0, y0 = np.floor(uv.min(0)).astype(int)
        x1, y1 = np.ceil(uv.max(0)).astype(int)
        x0, y0 = max(x0, 0), max(y0, 0)
        x1, y1 = min(x1, res - 1), min(y1, res - 1)
        if x1 < x0 or y1 < y0:
            continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        a, b, c = uv
        den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(den) < 1e-12:
            continue
        w0 = ((b[1] - c[1]) * (xs - c[0]) + (c[0] - b[0]) * (ys - c[1])) / den
        w1 = ((c[1] - a[1]) * (xs - c[0]) + (a[0] - c[0]) * (ys - c[1])) / den
        w2 = 1 - w0 - w1
        inside = (w0 >= -0.02) & (w1 >= -0.02) & (w2 >= -0.02)
        if not inside.any():
            continue
        W = np.stack([w0, w1, w2], -1)[inside]
        iy = (ys[inside] - 0.5).astype(int)
        ix = (xs[inside] - 0.5).astype(int)
        P[iy, ix] = W @ co[vs]
        N[iy, ix] = W @ vn[vs]
        UVP[iy, ix] = np.stack([xs[inside], ys[inside]], -1) / res
        ISL[iy, ix] = isl[vs[0]]
        M[iy, ix] = True
    nl = np.linalg.norm(N, axis=-1, keepdims=True)
    N = N / np.maximum(nl, 1e-9)
    return P, N, UVP, ISL, M, isl, co


def dilate(img, mask, steps=12):
    img = img.copy()
    m = mask.copy()
    for _ in range(steps):
        acc = np.zeros_like(img)
        cnt = np.zeros(m.shape)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1)):
            sh_m = np.roll(np.roll(m, dy, 0), dx, 1)
            sh_i = np.roll(np.roll(img, dy, 0), dx, 1)
            acc += sh_i * sh_m[..., None]
            cnt += sh_m
        grow = (~m) & (cnt > 0)
        img[grow] = acc[grow] / cnt[grow][:, None]
        m = m | grow
    return img


def save_png(rgb, alpha, name):
    res = rgb.shape[0]
    img = bpy.data.images.get(name)
    if img:
        bpy.data.images.remove(img)
    img = bpy.data.images.new(name, res, res, alpha=alpha is not None)
    a = alpha if alpha is not None else np.ones(rgb.shape[:2])
    px = np.concatenate([np.clip(rgb, 0, 1), a[..., None]], -1).astype(np.float32)
    img.pixels.foreach_set(px.ravel())
    img.filepath_raw = os.path.join(OUT, name + ".png")
    img.file_format = "PNG"
    img.save()
    return img

# ---------------------------------------------------------------- ambient occlusion bake (Cycles)


def bake_ao(ob, res=RES, samples=48, distance=0.07):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.samples = samples
    sc.cycles.device = "CPU"
    sc.render.bake.margin = 6
    sc.world = sc.world or bpy.data.worlds.new("World")
    sc.world.light_settings.distance = distance
    rig = bpy.data.objects.get("QuestionerRig")
    if rig:
        rig.data.pose_position = "REST"
    name = ob.name + "_AO"
    img = bpy.data.images.get(name)
    if img:
        bpy.data.images.remove(img)
    img = bpy.data.images.new(name, res, res, alpha=False, float_buffer=False)
    mats = [s.material for s in ob.material_slots]
    nodes_added = []
    for m in mats:
        m.use_nodes = True
        nt = m.node_tree
        n = nt.nodes.new("ShaderNodeTexImage")
        n.image = img
        nt.nodes.active = n
        nodes_added.append((m, n))
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.bake(type="AO", margin=6, use_clear=True)
    px = np.array(img.pixels[:]).reshape(res, res, 4)[..., 0]
    for m, n in nodes_added:
        m.node_tree.nodes.remove(n)
    return px

# ---------------------------------------------------------------- painters


def skin_albedo(P, N, rough=1.0):
    n1 = fbm(P * 9.0, 5)
    n2 = fbm(P * 23.0 + 5, 4)
    n3 = fbm(P * 55.0 + 9, 3)
    dark = C(0.30, 0.23, 0.17)
    mid = C(0.46, 0.38, 0.30)
    pale = C(0.62, 0.54, 0.44)
    red = C(0.42, 0.24, 0.18)
    col = mix(dark, mid, ss(0.30, 0.62, n1))
    col = mix(col, pale, ss(0.55, 0.85, n2) * 0.55)
    col = mix(col, red, ss(0.62, 0.80, fbm(P * 14 + 3, 3)) * 0.35)
    # mottling / liver spots
    col = mix(col, C(0.30, 0.21, 0.15), ss(0.70, 0.80, n3) * 0.55)
    # veins: thin ridged lines
    rid = np.abs(fbm(P * 16 + 11, 3) - 0.5)
    vein = (1 - ss(0.0, 0.018, rid)) * ss(0.35, 0.6, fbm(P * 4 + 2, 2))
    col = mix(col, C(0.24, 0.16, 0.20), vein * 0.55)
    # grime toward the ground and on the top of surfaces: dusty
    col = mix(col, C(0.50, 0.42, 0.32), ss(0.4, 0.9, N[..., 2]) * 0.25)
    return col


def paint_head(P, N, ISL, isl, co):
    col = skin_albedo(P, N)
    x, y, z = P[..., 0], P[..., 1], P[..., 2]
    # wrinkles: forehead and neck rings
    wr = 0.5 + 0.5 * np.sin(z * 520 + fbm(P * 30, 2) * 8)
    col = mix(col, col * 0.62, (wr > 0.82) * ss(1.93, 1.97, z) * (1 - ss(2.01, 2.03, z)) * 0.8)
    col = mix(col, col * 0.68, (wr > 0.85) * ss(1.60, 1.64, z) * (1 - ss(1.76, 1.79, z)) * 0.7)
    # sunken eye sockets: dark, bruised
    for sx in (1, -1):
        d = np.sqrt(((x - 0.034 * sx) / 1.25) ** 2 + (y + 0.075) ** 2 + (z - 1.920) ** 2)
        col = mix(col, C(0.10, 0.06, 0.06), (1 - ss(0.010, 0.034, d)) * 0.85)
        col = mix(col, C(0.30, 0.12, 0.14), (1 - ss(0.020, 0.045, d)) * ss(0.012, 0.03, d) * 0.4)
    # mouth: lips / gums red, the inside dark red to black down the throat
    in_mouth = (np.abs(x) < 0.052) & (z > 1.665) & (z < 1.855) & (y > -0.125)
    depth = ss(-0.100, -0.045, y)
    lip = in_mouth * (1 - ss(0.0, 0.025, np.abs(y + 0.105))) * 0.7
    col = mix(col, C(0.38, 0.10, 0.09), lip)
    col = mix(col, mix(C(0.33, 0.06, 0.05), C(0.03, 0.01, 0.01), depth), in_mouth * ss(-0.112, -0.095, y))
    # nose tip / nostrils
    nd = np.sqrt(x ** 2 + (y + 0.13) ** 2 + (z - 1.86) ** 2)
    col = mix(col, C(0.48, 0.24, 0.20), (1 - ss(0.0, 0.025, nd)) * 0.5)
    for sx in (1, -1):
        d = np.sqrt((x - 0.009 * sx) ** 2 + (y + 0.122) ** 2 + (z - 1.846) ** 2)
        col = mix(col, C(0.05, 0.02, 0.02), 1 - ss(0.003, 0.009, d))
    # teeth and eyes (small islands)
    ids, counts = np.unique(isl, return_counts=True)
    for iid, cnt in zip(ids, counts):
        vc = co[isl == iid]
        c = vc.mean(0)
        m = ISL == iid
        if not m.any():
            continue
        if cnt < 30 and abs(c[0]) < 0.06 and 1.66 < c[2] < 1.86:          # a tooth
            t_rel = np.clip(np.abs(z[m] - c[2]) / 0.015, 0, 1)
            base = mix(C(0.78, 0.68, 0.45), C(0.50, 0.38, 0.20), fbm(P[m] * 120, 2))
            base = mix(base, C(0.30, 0.18, 0.10), fbm(P[m] * 60 + 3, 2) * 0.35)
            col[m] = base
        elif 40 < cnt < 140 and abs(abs(c[0]) - 0.034) < 0.01 and abs(c[2] - 1.922) < 0.01:   # an eye
            q = P[m] - c
            front = -q[..., 1] / 0.0125
            ang = np.sqrt(q[..., 0] ** 2 + (q[..., 2] - 0.0015) ** 2) / 0.0125
            sclera = mix(C(0.42, 0.33, 0.22), C(0.40, 0.12, 0.10), ss(0.45, 0.85, fbm(P[m] * 300, 2)) * 0.7)
            iris = (front > 0.3) & (ang < 0.62)
            e = sclera.copy()
            e[iris] = mix(C(0.12, 0.08, 0.05), C(0.02, 0.01, 0.01), ss(0.0, 0.25, ang[iris]))
            hl = (front > 0.8) & (np.abs(q[..., 0] - 0.0035) < 0.0018) & (np.abs(q[..., 2] - 0.0045) < 0.0018)
            e[hl] = C(0.70, 0.66, 0.60)
            col[m] = e
    return col


def paint_limbs(P, N, ISL, isl, co):
    col = skin_albedo(P, N)
    x, y, z = P[..., 0], P[..., 1], P[..., 2]
    # dirty feet / hands
    col = mix(col, C(0.24, 0.17, 0.12), (1 - ss(0.03, 0.16, z)) * 0.55)
    # nails: near each finger / thumb tip and toe tip, dark yellow-black
    tips = []
    for s in ("Left", "Right"):
        for f in ("Thumb", "Index", "Middle", "Ring", "Pinky"):
            a = np.array(J["%sHand%s3" % (s, f)][:])
            b = np.array(J["%sHand%s4" % (s, f)][:])
            tips.append(b + (b - a) / np.linalg.norm(b - a) * 0.008)
    for t in tips:
        d = np.linalg.norm(P - t, axis=-1)
        col = mix(col, mix(C(0.20, 0.15, 0.08), C(0.05, 0.04, 0.03), fbm(P * 200, 2)), 1 - ss(0.010, 0.016, d))
    # sandals (their own islands near the ground)
    ids, counts = np.unique(isl, return_counts=True)
    for iid, cnt in zip(ids, counts):
        vc = co[isl == iid]
        if vc[:, 2].max() < 0.09 and cnt >= 30:
            m = ISL == iid
            leather = mix(C(0.20, 0.12, 0.07), C(0.32, 0.20, 0.12), fbm(P[m] * 60, 3))
            leather = mix(leather, C(0.10, 0.07, 0.05), ss(0.6, 0.8, fbm(P[m] * 25 + 4, 2)) * 0.6)
            col[m] = leather
    return col


def paint_cloth(P, N, UVP):
    x, y, z = P[..., 0], P[..., 1], P[..., 2]
    u, v = UVP[..., 0], UVP[..., 1]
    is_pants = (u > 0.62) & (v < 0.495)
    base = mix(C(0.56, 0.51, 0.42), C(0.66, 0.61, 0.51), fbm(P * 6, 3))
    base[is_pants] = mix(C(0.46, 0.42, 0.35), C(0.55, 0.51, 0.43), fbm(P[is_pants] * 6, 3))
    # weave
    wv = 0.5 + 0.5 * np.sin(u * 2400) * np.sin(v * 2400)
    col = base * (0.93 + 0.07 * wv[..., None])
    # grime: lots lower down, stains everywhere
    low = 1 - ss(0.25, 1.15, z)
    col = mix(col, C(0.46, 0.37, 0.27), np.clip(low * 0.75 + (fbm(P * 4 + 7, 4) - 0.5) * 0.6, 0, 1))
    stain = ss(0.55, 0.75, fbm(P * 7 + 21, 4))
    col = mix(col, C(0.42, 0.32, 0.20), stain * 0.55)
    col = mix(col, C(0.55, 0.47, 0.30), ss(0.62, 0.8, fbm(P * 12 + 33, 3)) * 0.35)        # yellowed sweat patches
    # old blood: down the chest from the mouth and on the hem
    chest = ss(1.55, 1.15, z) * (1 - ss(0.0, 0.07, np.abs(x + 0.01 * np.sin(z * 30)))) * (y < 0) * ss(1.0, 1.2, z)
    blood = np.clip(chest * ss(0.35, 0.65, fbm(P * 18 + 40, 3)) + (1 - ss(0.25, 0.55, z)) * ss(0.62, 0.78, fbm(P * 9 + 50, 3)), 0, 1)
    col = mix(col, C(0.25, 0.07, 0.05), blood * 0.85)
    # vertical drip streaks
    streak = ss(0.6, 0.9, fbm(np.stack([x * 60, y * 60, z * 3], -1), 3))
    col = mix(col, col * 0.7, streak * 0.4)
    # alpha: rips and holes, more near the hems; frayed borders of the existing tatters come from the geometry
    hole_n = fbm(P * 11 + 80, 4)
    hem_t = (1 - ss(0.30, 0.75, z))
    sleeve_cuff = (u > 0.62) & (v > 0.5) & (v < 0.7)
    a = np.ones(z.shape)
    a[hole_n > 0.86 - 0.18 * hem_t] = 0
    a[(fbm(P * 40 + 90, 3) > 0.72) & (hem_t > 0.6)] = 0
    a[sleeve_cuff & (fbm(P * 30 + 95, 3) > 0.66)] = 0
    # dark ragged edge around every hole
    edge = (hole_n > 0.82 - 0.18 * hem_t) & (a > 0)
    col[edge] = col[edge] * 0.55
    return col, a


def shemagh(uu, vv, cells_u, cells_v):
    """red/white shemagh check: alternating red squares (with a white dot) and white squares crossed by thin red threads"""
    gu = uu * cells_u
    gv = vv * cells_v
    cu = np.floor(gu).astype(int)
    cv = np.floor(gv).astype(int)
    fu = gu - cu
    fv = gv - cv
    checker = ((cu + cv) % 2 == 0)
    dot = (np.abs(fu - 0.5) < 0.16) & (np.abs(fv - 0.5) < 0.16)
    thread = (np.abs(fu - 0.5) < 0.07) | (np.abs(fv - 0.5) < 0.07)
    red = np.where(checker, ~dot, thread)
    # every 8th row/column a doubled red stripe (the woven bands of a real shemagh)
    band = ((cu % 8 == 0) & (fu < 0.5)) | ((cv % 8 == 0) & (fv < 0.5))
    return red | band


def paint_keffiyeh(P, N, UVP):
    u, v = UVP[..., 0], UVP[..., 1]
    hood = v >= 0.275
    hu = u
    hv = (v - 0.28) / 0.72
    tu = 1 - u                       # along the tail (0 at the head)
    tv = v / 0.26                    # across
    red = np.zeros(u.shape, dtype=bool)
    red[hood] = shemagh(hu[hood], hv[hood], 120, 48)
    red[~hood] = shemagh(tu[~hood], tv[~hood], 110, 20)
    # border bands: hood lower edge, tail sides
    band = np.zeros(u.shape, dtype=bool)
    band[hood] = (hv[hood] < 0.06) & (hv[hood] > 0.025)
    band[~hood] = ((tv[~hood] < 0.10) & (tv[~hood] > 0.05)) | ((tv[~hood] > 0.90) & (tv[~hood] < 0.95))
    red |= band
    white = mix(C(0.82, 0.78, 0.68), C(0.70, 0.64, 0.52), fbm(P * 9, 3))
    redc = mix(C(0.58, 0.12, 0.11), C(0.42, 0.09, 0.08), fbm(P * 13 + 2, 3))
    col = np.where(red[..., None], redc, white)
    # dirt, faded, darker lower
    col = mix(col, C(0.40, 0.30, 0.22), np.clip((fbm(P * 5 + 6, 4) - 0.45) * 1.6, 0, 0.7))
    col = mix(col, col * 0.75, ss(1.75, 1.40, P[..., 2]) * 0.5)
    # alpha: fringe (tassels) on the very ends + some rips
    a = np.ones(u.shape)
    fringe_hood = hood & (hv < 0.025) & (np.sin(hu * 900) > -0.2)
    fringe_tail = (~hood) & (tu > 0.975) & (np.sin(tv * 120) > -0.1)
    a[(hood & (hv < 0.025)) & ~fringe_hood] = 0
    a[(~hood & (tu > 0.975)) & ~fringe_tail] = 0
    a[fbm(P * 14 + 60, 3) > 0.80] = 0
    return col, a


def paint_hood2(P, N, UVP):
    """v2 cloth-sim hood, UV = the flat sheet (1.04 m across x 0.98 m): staggered rust-red blocks on dirty cream (like the
    concept), red border bands near the hems, fringe along the edges, stains, fading, a few holes"""
    u, v = UVP[..., 0], UVP[..., 1]
    cu_n, cv_n = 120, 113
    # hand-woven: the grid wanders a little and each block has its own size
    gu = u * cu_n + 0.35 * (fbm(P * 18 + 4, 2) - 0.5) * 2
    gv = v * cv_n + 0.35 * (fbm(P * 18 + 9, 2) - 0.5) * 2
    cu, cv = np.floor(gu).astype(int), np.floor(gv).astype(int)
    fu, fv = gu - cu, gv - cv
    jit = (fbm(P * 260, 2) - 0.5) * 0.22 + (_hash(np.floor(gu).astype(np.int64), np.floor(gv).astype(np.int64), 3) - 0.5) * 0.10
    block = (np.abs(fu - 0.5) < 0.39 + jit) & (np.abs(fv - 0.5) < 0.39 - jit)
    red = ((cu + cv) % 2 == 0) & block
    # the white cells carry a small red tick on alternate rows (houndstooth-like look of the concept)
    tick = ((cu + cv) % 2 == 1) & (cv % 2 == 0) & (np.abs(fu - 0.5) < 0.12) & (np.abs(fv - 0.5) < 0.12)
    red = red | tick
    # border bands 2-4 cm in from the hems
    e = np.minimum(np.minimum(u, 1 - u) * 1.04, np.minimum(v, 1 - v) * 0.98)
    band = ((e > 0.022) & (e < 0.030)) | ((e > 0.036) & (e < 0.040))
    red = red | band
    white = mix(C(0.80, 0.72, 0.62), C(0.66, 0.58, 0.48), fbm(P * 9, 3))
    redc = mix(C(0.47, 0.11, 0.09), C(0.33, 0.08, 0.07), fbm(P * 13 + 2, 3))
    col = np.where(red[..., None], redc, white)
    # dirt: brown stains, grime toward the hanging ends, faded/sun-bleached top, old blood near the face edge
    col = mix(col, C(0.36, 0.27, 0.19), np.clip((fbm(P * 5 + 6, 4) - 0.42) * 1.8, 0, 0.75))
    col = mix(col, C(0.28, 0.20, 0.14), ss(1.70, 1.35, P[..., 2]) * 0.55)
    col = mix(col, col * 0.6 + C(0.25, 0.05, 0.04) * 0.4, ss(0.62, 0.78, fbm(P * 16 + 70, 3)) * ss(0.12, 0.02, v) * 0.8)
    col = col * (0.88 + 0.12 * fbm(P * 120, 2))[..., None]        # thread noise
    # alpha: fringe along the hems, ragged holes and tears
    a = np.ones(u.shape)
    fringe = e < 0.012
    threads = np.sin(np.where(np.minimum(u, 1 - u) * 1.04 < np.minimum(v, 1 - v) * 0.98, v * 460, u * 480)) > -0.1
    a[fringe & ~threads] = 0
    a[fbm(P * 12 + 60, 3) > 0.79] = 0
    a[(fbm(P * 30 + 61, 3) > 0.70) & (e < 0.08)] = 0
    return col, a


def process(obname, painter, texname, alpha=False, ao_strength=0.7, top_light=0.12, res=None):
    res = res or RES
    ob = bpy.data.objects[obname]
    P, N, UVP, ISL, M, isl, co = rasterise(ob, res)
    if painter in (paint_head, paint_limbs):
        col = painter(P, N, ISL, isl, co)
        a = None
    else:
        col, a = painter(P, N, UVP)
    ao = bake_ao(ob, res)
    ao = np.clip(ao, 0, 1)
    shade = (1 - ao_strength) + ao_strength * ao ** 1.15
    shade = shade * ((1 - top_light) + top_light * (0.5 + 0.5 * N[..., 2]))
    col = col * shade[..., None]
    col = dilate(col, M, 10)
    if a is not None:
        a = dilate(a[..., None], M, 10)[..., 0]
        a = np.where(M, a, 1.0)
    save_png(col, a if alpha else None, texname)
    return float(M.mean())


def preview_material(obname, texname, alpha=False):
    ob = bpy.data.objects[obname]
    m = ob.material_slots[0].material
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        if n.type == "TEX_IMAGE":
            nt.nodes.remove(n)
    img = bpy.data.images.load(os.path.join(OUT, texname + ".png"), check_existing=False)
    img.reload()
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    bsdf = [n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"][0]
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if alpha:
        nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
        m.blend_method = "CLIP" if hasattr(m, "blend_method") else None
    bsdf.inputs["Roughness"].default_value = 0.85
    return m


if globals().get("RUN_TEX", True):
    cov = {}
    if globals().get("V2", False):
        # v2: the head textures come from q18 (HR bakes); the hood is the cloth-sim sheet
        cov["keff"] = process("Q_Keffiyeh", paint_hood2, "T_Questioner_Keffiyeh", alpha=True, res=2048)
        if globals().get("V2_ALL", False):
            cov["limbs"] = process("Q_Limbs", paint_limbs, "T_Questioner_Limbs")
            cov["cloth"] = process("Q_Clothes", paint_cloth, "T_Questioner_Cloth", alpha=True)
    else:
        cov["head"] = process("Q_HeadMesh", paint_head, "T_Questioner_Head")
        cov["limbs"] = process("Q_Limbs", paint_limbs, "T_Questioner_Limbs")
        cov["cloth"] = process("Q_Clothes", paint_cloth, "T_Questioner_Cloth", alpha=True)
        cov["keff"] = process("Q_Keffiyeh", paint_keffiyeh, "T_Questioner_Keffiyeh", alpha=True)
    result = cov
