# The Questioner - keffiyeh (shemagh): hood wrapped over the skull (ray cast onto Q_Head), hanging to the shoulders and in a
# point down the back, plus the long tail draped down the left side of the chest.   Blender: exec(open(this).read())
import bpy, bmesh, math, random
from mathutils import Vector, Matrix
V = Vector

HC = V((0, 0.006, 1.915))          # head centre for the wrap


def head_point(head, d, off):
    o = HC + d * 0.5
    hit, loc, nor, idx = head.ray_cast(o, -d)
    if not hit:
        return HC + d * 0.10
    return loc + d * off


def build_hood(seed=2):
    rnd = random.Random(seed)
    head = bpy.data.objects["Q_Head"]
    NPHI = 36                       # columns around (closed loop, the face gap is cut out)
    thetas = [86, 74, 62, 50, 38, 27, 16, 6, -4]          # wrapped part (deg elevation)
    drops = 7                                             # hanging rows below the wrap
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    grid = []
    for ti, th in enumerate(thetas):
        row = []
        for j in range(NPHI + 1):
            phi = 2 * math.pi * j / NPHI
            t = math.radians(th)
            d = V((math.sin(phi) * math.cos(t), -math.cos(phi) * math.cos(t), math.sin(t))).normalized()
            # loose fabric: tighter on top, puffier at the sides, a soft fold pattern
            side = abs(math.sin(phi))
            off = 0.016 + 0.014 * side * (1 - max(th, 0) / 90) + 0.005 * math.sin(phi * 7 + ti)
            row.append(head_point(head, d, off))
        grid.append(row)
    # hanging part
    eq = grid[-1]
    for k in range(1, drops + 1):
        row = []
        for j in range(NPHI + 1):
            phi = 2 * math.pi * j / NPHI
            p = eq[j]
            back = (1 - math.cos(phi)) / 2          # 0 front .. 1 back
            side = abs(math.sin(phi))
            z_end = 1.66 - 0.06 * side - 0.26 * back ** 2.2          # front 1.66, sides ~1.60, back point 1.40
            tk = k / drops
            z = p.z + (z_end - p.z) * tk
            flare = 0.24 * side ** 2 + 0.16 * back - 0.06 * (1 - back) * (1 - side)
            radial = V((p.x - HC.x, p.y - HC.y, 0))
            rl = radial.length
            radial = radial.normalized() if rl > 1e-6 else V((0, 1, 0))
            r_new = rl + (p.z - z) * flare
            q = V((HC.x, HC.y, 0)) + radial * r_new
            # gentle hanging folds
            q += radial * 0.008 * math.sin(phi * 9 + k * 0.7) * tk
            row.append(V((q.x, q.y, z)))
        grid.append(row)
    verts = [[bm.verts.new(p) for p in row] for row in grid]
    nrows = len(grid)
    nwrap = len(thetas)
    for r in range(nrows - 1):
        for j in range(NPHI):
            phi = 2 * math.pi * (j + 0.5) / NPHI
            dphi = math.degrees(abs((phi + math.pi) % (2 * math.pi) - math.pi))       # 0 = front
            th = thetas[r] if r < nwrap else -30
            # face opening: everything in front below the brow line
            if th < 33 and dphi < 38:
                continue
            if r >= nwrap - 1 and dphi < 44:
                continue
            f = bm.faces.new((verts[r][j], verts[r][j + 1], verts[r + 1][j + 1], verts[r + 1][j]))
            for l, (ri, ci) in zip(f.loops, ((r, j), (r, j + 1), (r + 1, j + 1), (r + 1, j))):
                l[uv].uv = (ci / NPHI, 1 - ri / (nrows - 1))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    # ragged lower edge
    for v in bm.verts:
        if v.is_boundary and v.co.z < 1.75:
            v.co.z -= 0.025 * rnd.random() ** 2
    bm.normal_update()
    # make the normals point outward
    for f in bm.faces:
        if (f.calc_center_median() - HC).dot(f.normal) < 0:
            f.normal_flip()
    me = bpy.data.meshes.new("Q_KeffiyehHood")
    bm.to_mesh(me)
    bm.free()
    old = bpy.data.objects.get("Q_KeffiyehHood")
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    ob = bpy.data.objects.new("Q_KeffiyehHood", me)
    bpy.context.scene.collection.objects.link(ob)
    for p in me.polygons:
        p.use_smooth = True
    return ob


def strip(name, path, widths, normal_hint, folds=0.010, seed=4, segs_across=5):
    """cloth band along a polyline 'path' (points), width per point, lying across 'normal_hint' (surface normal it rests on)"""
    rnd = random.Random(seed)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    rows = []
    n = len(path)
    for i, p in enumerate(path):
        t = (path[min(i + 1, n - 1)] - path[max(i - 1, 0)]).normalized()
        across = t.cross(normal_hint).normalized()
        nrm = across.cross(t).normalized()
        row = []
        for k in range(segs_across + 1):
            u = k / segs_across - 0.5
            q = p + across * u * widths[i] + nrm * folds * math.sin(u * 9.0 + i * 1.3) * (0.4 + 0.6 * i / n)
            row.append(bm.verts.new(q))
        rows.append(row)
    total = sum((path[i + 1] - path[i]).length for i in range(n - 1))
    acc = [0.0]
    for i in range(n - 1):
        acc.append(acc[-1] + (path[i + 1] - path[i]).length)
    for i in range(n - 1):
        for k in range(segs_across):
            f = bm.faces.new((rows[i][k], rows[i][k + 1], rows[i + 1][k + 1], rows[i + 1][k]))
            for l, (ri, ci) in zip(f.loops, ((i, k), (i, k + 1), (i + 1, k + 1), (i + 1, k))):
                l[uv].uv = (ci / segs_across, 1 - acc[ri] / total)
            if f.normal.dot(normal_hint) < 0:
                f.normal_flip()
    # ragged end
    for v in rows[-1]:
        v.co += (path[-1] - path[-2]).normalized() * 0.03 * rnd.random()
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


def build_tail():
    # from the left side of the hood, over the collarbone, down the front of the chest to the waist
    path = [V((0.078, -0.035, 1.800)), V((0.092, -0.070, 1.700)), V((0.104, -0.118, 1.610)), V((0.098, -0.140, 1.530)),
            V((0.090, -0.148, 1.440)), V((0.082, -0.150, 1.350)), V((0.076, -0.152, 1.260)), V((0.072, -0.154, 1.170)),
            V((0.070, -0.156, 1.090))]
    widths = [0.10, 0.13, 0.16, 0.16, 0.15, 0.145, 0.14, 0.13, 0.12]
    return strip("Q_KeffiyehTail", path, widths, V((0, -1, 0.15)).normalized(), folds=0.012)


if globals().get("BUILD_KEFF", True):
    hood = build_hood()
    tail = build_tail()
    result = {"hood": len(hood.data.polygons), "tail": len(tail.data.polygons)}
