# The Questioner - clothing: thobe (long robe) with sleeves, sirwal pants.   Blender: exec(open(this).read())
# Parametric ring surfaces: rings of points around a centre line, with hanging folds and a ragged (per-column) hem.
import bpy, bmesh, math, random
from mathutils import Vector, Matrix

HERE = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\AnimationTools\questioner"
BUILD_RIG = False
exec(open(HERE + r"\q01_rig.py", encoding="utf8").read())
V = Vector


def fold_noise(rnd, n_terms=5):
    terms = [(rnd.randint(3, 11), rnd.uniform(0, 6.283), rnd.uniform(0.4, 1.0)) for _ in range(n_terms)]
    s = sum(t[2] for t in terms)
    return lambda a: sum(w * math.sin(k * a + ph) for k, ph, w in terms) / s


def ring_surface(name, rings, cols, seed=1, hem=None, close_top=False, uv_scale=(1.0, 1.0), cut=None, tear=0.0, tear_dir=None):
    """rings: list of (centre, axisX, axisY, rx, ry, fold_amp, v) - ring points = centre + cos(a)*rx*axisX + sin(a)*ry*axisY,
       radius *= 1 + fold_amp * noise(a).  hem: function(col_index, angle) -> how many of the last rings this column keeps
       (ragged edge).  Returns the object; uv u = column / cols, v = ring parameter v."""
    rnd = random.Random(seed)
    fn = fold_noise(rnd)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    grid = []
    for (c, ax, ay, rx, ry, famp, vv) in rings:
        row = []
        for i in range(cols + 1):
            a = 2 * math.pi * i / cols
            f = 1 + famp * fn(a)
            p = c + ax * math.cos(a) * rx * f + ay * math.sin(a) * ry * f
            row.append(bm.verts.new(p))
        grid.append(row)
    nr = len(rings)
    keep = [nr] * cols
    if hem:
        keep = [hem(i, 2 * math.pi * (i + 0.5) / cols) for i in range(cols)]
    for r in range(nr - 1):
        for i in range(cols):
            if r + 1 >= keep[i]:
                continue
            if cut and cut(r, 2 * math.pi * (i + 0.5) / cols):
                continue
            f = bm.faces.new((grid[r][i], grid[r][i + 1], grid[r + 1][i + 1], grid[r + 1][i]))
            for l, (ri, ci) in zip(f.loops, ((r, i), (r, i + 1), (r + 1, i + 1), (r + 1, i))):
                l[uv].uv = (ci / cols * uv_scale[0], rings[ri][6] * uv_scale[1])
    # weld the seam column (i = cols duplicates i = 0)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    if tear > 0:
        # torn edge: open-boundary vertices of the lower half get pulled further along the garment (pointy tatters)
        td = tear_dir or V((0, 0, -1))
        for v in bm.verts:
            if v.is_boundary and v.co.z < rings[0][0].z - 0.15:
                v.co += td * tear * rnd.random() ** 1.5
    bm.normal_update()
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    old = bpy.data.objects.get(name)
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    for p in me.polygons:
        p.use_smooth = True
    return ob


def ragged(rnd, base_keep, spread, strips=0.25):
    """per-column hem: mostly base_keep rings, random shorter/longer columns, some long torn strips"""
    def h(i, a):
        k = base_keep + rnd.randint(-spread, spread // 2)
        if rnd.random() < strips:
            k += rnd.randint(1, spread + 1)
        return k
    return h


# ---------------------------------------------------------------- thobe body
def build_thobe(seed=3):
    rnd = random.Random(seed)
    X, Y = V((1, 0, 0)), V((0, -1, 0))        # ring axes: +x side, -y front
    prof = [  # z, rx, ry, cx_y (centre y), fold amp
        (1.690, 0.058, 0.060, 0.010, 0.00),
        (1.650, 0.090, 0.072, 0.012, 0.01),
        (1.615, 0.155, 0.088, 0.012, 0.02),
        (1.585, 0.205, 0.098, 0.014, 0.03),
        (1.540, 0.195, 0.108, 0.016, 0.04),
        (1.460, 0.172, 0.112, 0.018, 0.05),
        (1.360, 0.158, 0.112, 0.020, 0.06),
        (1.250, 0.150, 0.110, 0.022, 0.06),
        (1.150, 0.155, 0.112, 0.022, 0.07),
        (1.050, 0.168, 0.118, 0.020, 0.07),
        (0.950, 0.182, 0.126, 0.018, 0.08),
        (0.850, 0.196, 0.134, 0.016, 0.09),
        (0.750, 0.208, 0.140, 0.014, 0.10),
        (0.650, 0.218, 0.146, 0.012, 0.11),
        (0.560, 0.226, 0.150, 0.010, 0.12),
        (0.480, 0.232, 0.154, 0.010, 0.12),
        (0.410, 0.236, 0.156, 0.010, 0.13),
        (0.340, 0.238, 0.158, 0.010, 0.13),
        (0.280, 0.240, 0.160, 0.010, 0.14),
    ]
    z0, z1 = prof[0][0], prof[-1][0]
    rings = [(V((0, cy, z)), X, Y, rx, ry, fa, (z0 - z) / (z0 - z1)) for z, rx, ry, cy, fa in prof]
    # hem: most columns end around 0.45 m, torn strips hang lower, a few are ripped short
    hem = ragged(rnd, base_keep=16, spread=3, strips=0.3)
    front = math.pi / 2                         # ring axis Y is -Y (front), so the front is at a = 90 deg
    vee = {0: 50, 1: 40, 2: 28, 3: 18, 4: 9}     # half-angle of the V opening per ring (deg)
    holes = [(10, 2.2), (11, 2.2), (12, 4.6), (9, 5.6), (13, 0.9), (12, 1.0)]   # (ring, angle) torn-out patches
    def cut(r, a):
        da = abs((a - front + math.pi) % (2 * math.pi) - math.pi)
        if r in vee and math.degrees(da) < vee[r]:
            return True
        for hr, ha in holes:
            if r == hr and abs((a - ha + math.pi) % (2 * math.pi) - math.pi) < 0.14:
                return True
        return False
    ob = ring_surface("Q_Thobe", rings, cols=48, seed=seed, hem=hem, cut=cut, tear=0.07)
    return ob


def build_sleeve(side, seed=5):
    rnd = random.Random(seed)
    s = side
    sh = J[s + "Arm"]
    el = J[s + "ForeArm"]
    wr = J[s + "Hand"]
    d = (el - sh).normalized()
    side_ax = V((0, 1, 0)).cross(d).normalized()       # perpendicular in the arm's vertical plane
    if side_ax.z < 0:
        side_ax = -side_ax                               # points "up" relative to the arm
    fwd = d.cross(side_ax).normalized()
    pts = []
    # (point along the arm, radius up, radius fwd, sag down, fold)
    span = [(sh - d * 0.07 + V((0, 0, 0.01)), 0.075, 0.072, 0.000, 0.03),
            (sh, 0.070, 0.068, 0.004, 0.04),
            (sh + (el - sh) * 0.45, 0.064, 0.060, 0.012, 0.06),
            (el, 0.062, 0.060, 0.018, 0.07),
            (el + (wr - el) * 0.30, 0.068, 0.064, 0.026, 0.09),
            (el + (wr - el) * 0.52, 0.078, 0.070, 0.034, 0.11),
            (el + (wr - el) * 0.68, 0.086, 0.076, 0.040, 0.12)]
    rings = []
    total = len(span) - 1
    for k, (p, ru, rf, sag, fa) in enumerate(span):
        c = p - side_ax * sag
        rings.append((c, side_ax, fwd, ru, rf, fa, k / total))
    hem = ragged(rnd, base_keep=6, spread=2, strips=0.35)
    return ring_surface("Q_Sleeve" + s[0], rings, cols=16, seed=seed, hem=hem, tear=0.05, tear_dir=-d.lerp(V((0, 0, -1)), 0.4).normalized() * -1)


def build_pants_leg(side, seed=9):
    rnd = random.Random(seed)
    s = side
    hip = J[s + "UpLeg"]
    knee = J[s + "Leg"]
    ank = J[s + "Foot"]
    X, Y = V((1, 0, 0)), V((0, -1, 0))
    prof = [(hip + V((-0.025 * (1 if s == 'Left' else -1), 0, -0.02)), 0.082, 0.090, 0.03),
            (hip + (knee - hip) * 0.35, 0.088, 0.090, 0.05),
            (hip + (knee - hip) * 0.75, 0.080, 0.082, 0.06),
            (knee, 0.077, 0.080, 0.07),
            (knee + (ank - knee) * 0.25, 0.076, 0.078, 0.09),
            (knee + (ank - knee) * 0.45, 0.074, 0.076, 0.11),
            (knee + (ank - knee) * 0.60, 0.072, 0.074, 0.13)]
    # pull the inner side toward the centre line at the top so both legs meet at the crotch (hidden by the robe)
    rings = []
    for k, (c, rx, ry, fa) in enumerate(prof):
        rings.append((c, X, Y, rx, ry, fa, k / (len(prof) - 1)))
    hem = ragged(rnd, base_keep=7, spread=1, strips=0.3)
    return ring_surface("Q_Pants" + s[0], rings, cols=18, seed=seed, hem=hem, tear=0.04)


if globals().get("BUILD_CLOTH", True):
    t = build_thobe()
    sl = build_sleeve("Left", 5)
    sr = build_sleeve("Right", 6)
    pl = build_pants_leg("Left", 9)
    pr = build_pants_leg("Right", 10)
    result = {o.name: len(o.data.polygons) for o in (t, sl, sr, pl, pr)}
