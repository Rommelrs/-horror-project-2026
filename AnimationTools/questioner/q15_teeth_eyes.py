# The Questioner v2 - teeth (dense rows of uneven yellow pegs along the upper arch and both lower rims) and eyeballs.
# Needs q14_face.py landmarks.   Blender: exec(open(this).read())
import bpy, bmesh, math, random
import numpy as np
from mathutils import Vector, Matrix

HERE = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\AnimationTools\questioner"
if "UPPER_ARCH" not in globals():
    BUILD_FACE = False
    exec(open(HERE + r"\q14_face.py", encoding="utf8").read())
V = Vector


def add_tooth(bm, base, direction, length, width, depth, rnd, curve=0.0, side_dir=None, segs=6):
    """peg tooth: root buried at 'base', grows along 'direction'. Widest near the gum, rounded worn tip, slight curve."""
    d = V(direction).normalized()
    s = V(side_dir) if side_dir is not None else d.orthogonal()
    s = (s - d * s.dot(d)).normalized()
    f = d.cross(s).normalized()
    prof = [(-0.30, 0.85), (0.0, 1.0), (0.30, 1.04), (0.60, 0.96), (0.84, 0.76), (0.96, 0.50)]
    rings = []
    TOOTH_ID[0] += 1
    for t, k in prof:
        c = V(base) + d * (t * length) + f * (curve * length * max(t, 0) ** 2)
        ring = []
        for i in range(segs):
            a = 2 * math.pi * i / segs
            jit = 1.0 + rnd.uniform(-0.06, 0.06)
            v = bm.verts.new(c + s * (math.cos(a) * width * k * jit) + f * (math.sin(a) * depth * k * jit))
            TOOTH_T[v] = (t, TOOTH_ID[0])
            ring.append(v)
        rings.append(ring)
    tip = bm.verts.new(V(base) + d * length + f * (curve * length) + s * rnd.uniform(-0.15, 0.15) * width)
    TOOTH_T[tip] = (1.0, TOOTH_ID[0])
    for r in range(len(rings) - 1):
        for i in range(segs):
            j = (i + 1) % segs
            bm.faces.new((rings[r][i], rings[r][j], rings[r + 1][j], rings[r + 1][i]))
    for i in range(segs):
        bm.faces.new((rings[-1][i], rings[-1][(i + 1) % segs], tip))
    bm.faces.new(list(reversed(rings[0])))


def resample_polyline(pts, n):
    pts = [np.array(p, float) for p in pts]
    seg = [np.linalg.norm(pts[i + 1] - pts[i]) for i in range(len(pts) - 1)]
    tot = sum(seg)
    out = []
    for k in range(n):
        t = tot * (k + 0.5) / n
        i = 0
        while i < len(seg) - 1 and t > seg[i]:
            t -= seg[i]
            i += 1
        out.append(pts[i] + (pts[i + 1] - pts[i]) * min(t / seg[i], 1.0))
    return out


TOOTH_T = {}
TOOTH_ID = [0]


def build_teeth(seed=11):
    rnd = random.Random(seed)
    TOOTH_T.clear()
    TOOTH_ID[0] = 0
    old = bpy.data.objects.get("Q_Teeth2")
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    bm = bmesh.new()
    # ---------------- upper arch: big, crowded, uneven teeth hanging down & a bit forward, plus a crooked inner row
    for sx in (1, -1):
        arch = [mirror(p, sx) for p in UPPER_ARCH]
        for row, n, inset, scale in ((0, 9, 0.0, 1.0), (1, 5, 0.006, 0.7)):
            pts = resample_polyline(arch, n)
            for k, p in enumerate(pts):
                if rnd.random() < (0.10 if row == 0 else 0.3):
                    continue
                t = k / (n - 1)                              # 0 front .. 1 corner
                ln = (0.0215 - 0.0085 * t) * rnd.uniform(0.6, 1.25) * scale
                w = (0.0060 - 0.0016 * t) * rnd.uniform(0.85, 1.15) * scale
                tang = V(pts[min(k + 1, n - 1)] - pts[max(k - 1, 0)]).normalized()
                out = V((p[0], p[1] + 0.07, 0)).normalized() * -1   # away from the arch centre (forward / outward)
                d = V((0, 0, -1)) + out * rnd.uniform(0.1, 0.45) + tang * rnd.uniform(-0.25, 0.25)
                base = V(p) + V((0, 0, -0.0025)) - out * (0.002 + inset) + tang * rnd.uniform(-0.002, 0.002)
                add_tooth(bm, base, d, ln, w, w * rnd.uniform(0.65, 0.85), rnd, curve=rnd.uniform(-0.2, 0.08), side_dir=tang)
    # ---------------- lower rims: dense rows pointing into the opening and up
    for sx in (1, -1):
        rim = rim_points(sx)
        n = 23
        pts = resample_polyline(rim, n)
        for k, p in enumerate(pts):
            if rnd.random() < 0.07:
                continue
            t = k / (n - 1)                                  # 0 top .. 1 chin
            ln = (0.0170 - 0.0045 * t) * rnd.uniform(0.8, 1.2)
            w = (0.0043 - 0.0011 * t) * rnd.uniform(0.9, 1.1)
            tang = V(pts[min(k + 1, n - 1)] - pts[max(k - 1, 0)]).normalized()
            inward = V((-sx, 0, 0))
            F = V(JAW_F)
            d = inward * 0.60 + F * 0.75 + V(JAW_A) * -0.15 + V((rnd.uniform(-0.1, 0.1), 0, 0)) + F * rnd.uniform(-0.2, 0.2)
            base = V(p) + inward * 0.0015 - F * 0.001
            add_tooth(bm, base, d, ln, w, w * 0.8, rnd, curve=rnd.uniform(-0.2, 0.2), side_dir=tang)
    me = bpy.data.meshes.new("Q_Teeth2")
    bm.normal_update()
    bm.verts.index_update()
    global TOOTH_TV
    TOOTH_TV = np.zeros((len(bm.verts), 2))
    for v, (tt, tid) in TOOTH_T.items():
        TOOTH_TV[v.index] = (tt, tid)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new("Q_Teeth2", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def build_eyes2():
    old = bpy.data.objects.get("Q_Eyes2")
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    bm = bmesh.new()
    for sx in (1, -1):
        res = bmesh.ops.create_uvsphere(bm, u_segments=14, v_segments=10, radius=EYE_R)
        bmesh.ops.translate(bm, vec=mirror(EYE_C, sx), verts=res["verts"])
    me = bpy.data.meshes.new("Q_Eyes2")
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new("Q_Eyes2", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def preview_mats(teeth, eyes):
    clay(teeth, (0.34, 0.24, 0.11, 1), "M_ClayTeeth")
    clay(eyes, (0.05, 0.03, 0.015, 1), "M_ClayEye")
    m = bpy.data.materials["M_ClayEye"]
    b = [n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"][0]
    b.inputs["Roughness"].default_value = 0.15


if globals().get("BUILD_TEETH", True):
    teeth = build_teeth()
    eyes = build_eyes2()
    preview_mats(teeth, eyes)
    attach(teeth)
    attach(eyes)
    result = {"teeth_tris": sum(len(p.vertices) - 2 for p in teeth.data.polygons)}
