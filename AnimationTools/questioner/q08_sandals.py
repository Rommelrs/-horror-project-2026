# The Questioner - worn leather sandals (sole + two wide straps over the forefoot).   Blender: exec(open(this).read())
import bpy, bmesh, math
from mathutils import Vector
V = Vector
HERE = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\AnimationTools\questioner"
BUILD_RIG = False
exec(open(HERE + r"\q01_rig.py", encoding="utf8").read())


def foot_top(body, x, y):
    hit, loc, n, i = body.ray_cast(V((x, y, 0.5)), V((0, 0, -1)))
    return loc.z if hit else 0.03


def build_sandal(side):
    s = side
    sx = 1 if s == "Left" else -1
    cx = J[s + "Foot"].x + 0.004 * sx
    body = bpy.data.objects["Q_Body"]
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    # sole outline (y from heel to toe, half widths), slightly turned out
    prof = [(0.095, 0.020), (0.085, 0.034), (0.060, 0.040), (0.020, 0.040), (-0.030, 0.042), (-0.080, 0.050),
            (-0.130, 0.056), (-0.180, 0.054), (-0.220, 0.046), (-0.248, 0.032), (-0.262, 0.012)]
    outline = [V((cx + w * sx, y, 0)) for y, w in prof] + [V((cx - w * sx, y, 0)) for y, w in reversed(prof)]
    top = [bm.verts.new(p + V((0, 0, 0.016))) for p in outline]
    bot = [bm.verts.new(p + V((0, 0, 0.001))) for p in outline]
    n = len(outline)
    ft = bm.faces.new(top)
    fb = bm.faces.new(list(reversed(bot)))
    for i in range(n):
        bm.faces.new((bot[i], bot[(i + 1) % n], top[(i + 1) % n], top[i]))
    # straps
    for yc, wy in ((-0.055, 0.034), (-0.125, 0.030)):
        w = dict(prof)
        half = 0.050
        hz = foot_top(body, cx, yc) + 0.006
        segs = 10
        rows = []
        for k in range(segs + 1):
            a = math.pi * k / segs
            x = cx + math.cos(a) * (half + 0.004) * sx
            z = 0.014 + math.sin(a) * (hz - 0.014)
            # follow the foot's top a bit flatter than a half circle
            z = 0.014 + (hz - 0.014) * math.sin(a) ** 0.55
            rows.append([bm.verts.new(V((x, yc - wy / 2, z))), bm.verts.new(V((x, yc + wy / 2, z)))])
        for k in range(segs):
            f = bm.faces.new((rows[k][0], rows[k + 1][0], rows[k + 1][1], rows[k][1]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # strap normals outward (away from the foot)
    bm.normal_update()
    me = bpy.data.meshes.new("Q_Sandal" + s[0])
    bm.to_mesh(me)
    bm.free()
    old = bpy.data.objects.get("Q_Sandal" + s[0])
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    ob = bpy.data.objects.new("Q_Sandal" + s[0], me)
    bpy.context.scene.collection.objects.link(ob)
    return ob

if globals().get("BUILD_SANDALS", True):
    a = build_sandal("Left")
    b = build_sandal("Right")
    result = {"L": len(a.data.polygons), "R": len(b.data.polygons)}
