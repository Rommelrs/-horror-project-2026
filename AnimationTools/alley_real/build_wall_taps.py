# Low-poly PS2-style outdoor wall taps for the Alley 1 "real look", after the user's photo (galvanised pipe out of the
# house wall, a red-wheel gate valve, a brass hose tap at the bottom and a black PE hose running off along the wall foot).
# Coordinates are written in the Unity frame (U): origin on the wall face at ground level, +Y up, +Z out of the wall into
# the alley, +X along the wall (the way the hose runs). Exec inside Blender (own scene "AlleyTaps"); exports
# Assets/Models/AlleyTaps/AlleyTaps.fbx with:
#   WallTap_A    pipe out of the wall at 0.95 m, valve, tap, hose dropping to the foot and turning along +X (0.4 m)
#   WallTap_B    riser out of the sand with a wall bracket, valve low down, tap at 0.62 m (no hose)
#   WallHose_Run 1 m of hose along +X at the wall foot (scaled along X to the length needed)
#   WallHose_End the hose bending away from the wall and down into the sand
#   WallHose_Clip a strap holding the hose to the wall
# UVs index the 4 bands of AlleyPipes.png (make_pipe_texture.py).
import bpy, bmesh, math, os
from mathutils import Vector, Matrix

ROOT = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026"
OUT = ROOT + "/Assets/Models/AlleyTaps/AlleyTaps.fbx"
GALV, HOSE, RED, BRASS = 0, 1, 2, 3    # bands of AlleyPipes.png, v from the bottom

scene = bpy.data.scenes.get("AlleyTaps") or bpy.data.scenes.new("AlleyTaps")
bpy.context.window.scene = scene
for o in list(scene.objects): bpy.data.objects.remove(o, do_unlink=True)


def U(x, y, z):
    # Unity (x, y, z) -> Blender: Unity +Z (out of the wall) is Blender -Y, Unity up is Blender Z, and the FBX
    # export/import mirrors X (checked on the imported bounds)
    return Vector((-x, -z, y))


def rounded(pts, r=0.05, steps=3):
    # replace each inner corner of a polyline with a short quadratic curve (a bent pipe, not a kink)
    pts = [Vector(p) for p in pts]
    out = [pts[0]]
    for i in range(1, len(pts) - 1):
        a, b, c = pts[i - 1], pts[i], pts[i + 1]
        d = min(r, (b - a).length * 0.45, (c - b).length * 0.45)
        p0 = b + (a - b).normalized() * d; p2 = b + (c - b).normalized() * d
        for s in range(steps + 1):
            t = s / steps
            out.append(p0 * (1 - t) ** 2 + b * 2 * t * (1 - t) + p2 * t * t)
    out.append(pts[-1])
    return out


def tube(bm, uvl, upts, radius, band, sides=6, caps=True, smooth=0.0):
    # a pipe swept along points given in the Unity frame
    pts = [U(*p) for p in upts]
    if smooth > 0: pts = rounded(pts, smooth)
    # parallel-transported frame so the rings don't twist
    t0 = (pts[1] - pts[0]).normalized()
    ref = Vector((0, 0, 1)) if abs(t0.z) < 0.9 else Vector((1, 0, 0))
    n = t0.cross(ref).normalized()
    rings, dist, prev_t = [], 0.0, t0
    for i, p in enumerate(pts):
        if i == 0: t = t0
        elif i == len(pts) - 1: t = (p - pts[i - 1]).normalized()
        else: t = ((p - pts[i - 1]).normalized() + (pts[i + 1] - p).normalized()).normalized()
        if i > 0:
            dist += (p - pts[i - 1]).length
            axis = prev_t.cross(t)
            if axis.length > 1e-6:
                ang = prev_t.angle(t)

                n = Matrix.Rotation(ang, 3, axis.normalized()) @ n
        prev_t = t
        b = t.cross(n).normalized()
        ring = [bm.verts.new(p + (n * math.cos(a) + b * math.sin(a)) * radius) for a in (2 * math.pi * k / sides for k in range(sides))]
        rings.append((ring, dist))
    v0 = band * 0.25 + 0.02
    for (r0, d0), (r1, d1) in zip(rings, rings[1:]):
        for k in range(sides):
            k1 = (k + 1) % sides
            f = bm.faces.new((r0[k], r0[k1], r1[k1], r1[k]))
            for loop, (u, v) in zip(f.loops, [(d0 / 0.5, k), (d0 / 0.5, k + 1), (d1 / 0.5, k + 1), (d1 / 0.5, k)]):
                loop[uvl].uv = (u, v0 + 0.21 * v / sides)
    if caps:
        for ring, _ in (rings[0], rings[-1]):
            f = bm.faces.new(ring)     # winding fixed by recalc_face_normals in finish()
            for loop in f.loops: loop[uvl].uv = (0.1, v0 + 0.1)
    bm.normal_update()


def box(bm, uvl, center, size, band):
    cx, cy, cz = center; sx, sy, sz = (s / 2 for s in size)
    c = [U(cx + dx * sx, cy + dy * sy, cz + dz * sz) for dx in (-1, 1) for dy in (-1, 1) for dz in (-1, 1)]
    v = [bm.verts.new(p) for p in c]
    idx = lambda dx, dy, dz: ((dx > 0) * 4 + (dy > 0) * 2 + (dz > 0))
    quads = [((1, -1, -1), (1, 1, -1), (1, 1, 1), (1, -1, 1)), ((-1, -1, 1), (-1, 1, 1), (-1, 1, -1), (-1, -1, -1)),
             ((-1, 1, -1), (-1, 1, 1), (1, 1, 1), (1, 1, -1)), ((-1, -1, 1), (-1, -1, -1), (1, -1, -1), (1, -1, 1)),
             ((-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)), ((1, -1, -1), (-1, -1, -1), (-1, 1, -1), (1, 1, -1))]
    v0 = band * 0.25 + 0.02
    for q in quads:
        try: f = bm.faces.new([v[idx(*c)] for c in q])
        except ValueError: continue
        for loop, uv in zip(f.loops, [(0, v0), (0.3, v0), (0.3, v0 + 0.2), (0, v0 + 0.2)]): loop[uvl].uv = uv
    bm.normal_update()


def finish(bm, name):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)   # Unity frame flips handedness; make every face point out
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me); scene.collection.objects.link(ob)
    return ob


def valve(bm, uvl, y, z, wheel_out=0.085):
    # gate valve inline on a vertical pipe at height y: brass body, stem out of the wall, red hand wheel
    tube(bm, uvl, [(0, y + 0.045, z), (0, y - 0.045, z)], 0.029, BRASS)
    box(bm, uvl, (0, y, z + 0.025), (0.034, 0.04, 0.03), BRASS)
    tube(bm, uvl, [(0, y, z + 0.03), (0, y, z + wheel_out)], 0.007, BRASS, sides=4)
    tube(bm, uvl, [(0, y, z + wheel_out - 0.008), (0, y, z + wheel_out + 0.006)], 0.042, RED, sides=8)
    for a in (0, math.pi / 2):   # spokes so it reads as a wheel, not a disc
        dx, dy = math.cos(a) * 0.044, math.sin(a) * 0.044
        tube(bm, uvl, [(-dx, y - dy, z + wheel_out + 0.009), (dx, y + dy, z + wheel_out + 0.009)], 0.006, RED, sides=4)


def hose_tap(bm, uvl, y, z):
    # brass bib tap out of an elbow at the foot of the riser, spout bent down, small cross handle on top
    box(bm, uvl, (0, y, z), (0.052, 0.052, 0.052), GALV)
    tube(bm, uvl, [(0, y, z), (0, y, z + 0.075)], 0.022, BRASS)
    tube(bm, uvl, [(0, y, z + 0.07), (0, y, z + 0.1), (0, y - 0.06, z + 0.105)], 0.015, BRASS, smooth=0.025)
    tube(bm, uvl, [(0, y + 0.015, z + 0.05), (0, y + 0.055, z + 0.05)], 0.006, BRASS, sides=4)
    tube(bm, uvl, [(-0.03, y + 0.058, z + 0.05), (0.03, y + 0.058, z + 0.05)], 0.007, BRASS, sides=4)
    return (0, y - 0.06, z + 0.105)     # spout tip


Z = 0.075     # pipe centre off the wall face (wall clips / fittings make up the gap)
HY = 0.045    # hose centre height at the wall foot
HZ = 0.05     # hose centre off the wall face


def tap_a():
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new("UVMap")
    top = 0.95
    box(bm, uvl, (0, top, 0.006), (0.1, 0.1, 0.012), GALV)                # plate where it leaves the wall
    tube(bm, uvl, [(0, top, 0.0), (0, top, Z)], 0.02, GALV)
    box(bm, uvl, (0, top, Z), (0.054, 0.054, 0.054), GALV)                 # elbow
    tube(bm, uvl, [(0, top, Z), (0, 0.40, Z)], 0.02, GALV)
    tube(bm, uvl, [(0, 0.63, Z), (0, 0.58, Z)], 0.026, GALV)               # coupling
    box(bm, uvl, (0, 0.6, Z * 0.5), (0.03, 0.022, Z), GALV)                # wall clip
    valve(bm, uvl, 0.8, Z)
    sx, sy, sz = hose_tap(bm, uvl, 0.40, Z)
    tube(bm, uvl, [(sx, sy + 0.01, sz), (sx, sy - 0.035, sz)], 0.021, GALV)                # hose clamp on the spout
    # hose: down off the spout, curling to the wall foot, then along +X
    tube(bm, uvl, [(sx, sy - 0.01, sz), (0, 0.16, sz + 0.005), (0.03, HY, 0.11), (0.2, HY, HZ + 0.005), (0.4, HY, HZ)],
         0.016, HOSE, smooth=0.07)
    return finish(bm, "WallTap_A")


def tap_b():
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new("UVMap")
    top = 0.62
    tube(bm, uvl, [(0, -0.08, Z), (0, top, Z)], 0.02, GALV)
    tube(bm, uvl, [(0, 0.03, Z), (0, -0.02, Z)], 0.034, GALV, sides=6)     # collar where it comes out of the sand
    box(bm, uvl, (0, 0.47, Z * 0.5), (0.03, 0.022, Z), GALV)               # wall bracket
    box(bm, uvl, (0, 0.47, 0.004), (0.07, 0.04, 0.008), GALV)
    valve(bm, uvl, 0.28, Z)
    hose_tap(bm, uvl, top, Z)
    return finish(bm, "WallTap_B")


def hose_run():
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new("UVMap")
    tube(bm, uvl, [(0, HY, HZ), (1, HY, HZ)], 0.016, HOSE, caps=False)
    return finish(bm, "WallHose_Run")


def hose_end():
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new("UVMap")
    tube(bm, uvl, [(0, HY, HZ), (0.14, HY, HZ + 0.01), (0.3, 0.03, 0.12), (0.36, -0.12, 0.15)], 0.016, HOSE, smooth=0.09)
    return finish(bm, "WallHose_End")


def hose_clip():
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new("UVMap")
    # a thin strap over the hose, nailed to the wall just above it
    box(bm, uvl, (0, HY + 0.018, HZ * 0.5 + 0.005), (0.012, 0.005, HZ + 0.01), GALV)
    box(bm, uvl, (0, HY + 0.005, HZ + 0.018), (0.012, 0.03, 0.005), GALV)
    return finish(bm, "WallHose_Clip")


made = [tap_a(), tap_b(), hose_run(), hose_end(), hose_clip()]
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.object.select_all(action='DESELECT')
for ob in made: ob.select_set(True)
bpy.context.view_layer.objects.active = made[0]
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False, use_mesh_modifiers=True)
result = {"exported": OUT, "objects": [(o.name, len(o.data.polygons)) for o in made]}
