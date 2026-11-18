# Thick canvas tarp lashed shut over Wardrobe (7) (Block 2 duct tape wardrobe).   Blender: exec(open(this).read())
# Built in its own scene "WardrobeCloth".  Frame (metres): origin = middle of the wardrobe's door face at the ground,
# X = across the doors, Y = INTO the wardrobe (outward = -Y), Z = up.  Wardrobe = box x +-W/2, y 0..D, z 0..H.
# Objects exported:  Cloth_Intact, Cloth_Cut (knife X-slash, four flaps peeled out around the tape), Ropes (three lashings
# with knots + hanging tails, kept in both states), TapeSpot (empty: where the tape sits in the pocket behind the cloth).
# Lighting is baked (Cycles diffuse, wardrobe + ground as occluders) per cloth version into lighting_*.npy; the albedo,
# blood and dirt are painted afterwards by make_wardrobe_cloth_textures.py (system python) and multiplied with it.
import bpy, bmesh, math, random, os, json
import numpy as np
from mathutils import Vector

ROOT = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026"
HERE = os.path.join(ROOT, "AnimationTools", "wardrobe_cloth")
OUT_FBX = os.path.join(ROOT, "Assets", "Models", "WardrobeCloth", "WardrobeCloth.fbx")
os.makedirs(os.path.dirname(OUT_FBX), exist_ok=True)

W, D, H = 2.112, 0.90, 2.90          # Wardrobe (7): mesh 1.76 x 0.9 x 2.9, z scale 1.2
EPS = 0.012                           # cloth sits this far off the wood
SW = 0.22                             # how far the cloth wraps round each side
TB = 0.30                             # how far it lies over the top
HW = W / 2 + EPS
AMAX = HW + SW
HTOP = H + EPS
STEP = 0.045
ROPES = [(0.70, 0.030, -0.62), (1.95, -0.025, 0.62), (2.58, 0.018, -0.25)]   # (height, tilt per metre, knot x)
RR = 0.016                            # rope radius
XT, ZT = 0.18, 1.33                   # the tape in its pocket
TAPE_Y = -0.048
BULGE = 0.105
# knife X-slash: four slash ends around the tape (angle deg, length m) and how far each flap peels (deg)
SLASH = [(38, 0.32), (146, 0.29), (213, 0.33), (321, 0.30)]
PEEL = [168, 150, 172, 142]

rng = random.Random(7)
BAND_PHASE = [(rng.uniform(0, 6.3), rng.uniform(0, 6.3), rng.uniform(0, 6.3), rng.uniform(-0.25, 0.25)) for _ in range(4)]


def rope_z(i, x):
    z, tilt, _ = ROPES[i]
    return z + tilt * x


def smooth(e0, e1, x):
    t = min(1.0, max(0.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def b0(a):   # bottom hem, a bit uneven
    return 0.30 + 0.025 * math.sin(a * 6.1) + 0.012 * math.sin(a * 17.0 + 1.3)


def tbend(a):   # how far the top flap reaches
    return TB + 0.03 * math.sin(a * 4.3 + 0.7)


def front_disp(x, z):
    """outward offset of the cloth from the door face"""
    env = 1.0
    for i in range(len(ROPES)):
        env *= smooth(0.0, 0.17, abs(z - rope_z(i, x)))
    band = sum(1 for i in range(len(ROPES)) if z > rope_z(i, x))
    ph = BAND_PHASE[band]
    # folds differ from band to band and lean a little (pulled toward the knots)
    xl = x + ph[3] * (z - 1.5)
    folds = (0.012 * math.sin(2 * math.pi * xl / 0.33 + ph[0] + 0.9 * math.sin(z * 2.3 + ph[1])) +
             0.007 * math.sin(2 * math.pi * xl / 0.19 + ph[1] + 1.1 * math.sin(z * 3.1 + 1)) +
             0.011 * math.sin(2 * math.pi * xl / 0.61 + ph[2] + 0.5 * math.sin(z * 0.9 + 2)) +
             0.004 * math.sin(x * 41 + z * 23) * math.sin(z * 17 - x * 9))
    folds *= 0.7 + 0.6 * (0.5 + 0.5 * math.sin(x * 2.3 + ph[2] + z * 1.7))
    d = 0.006 + env * (0.016 + folds)
    z0 = rope_z(0, x)
    if z < z0:      # the hem below the lowest rope hangs loose and flares
        f = (z0 - z) / z0
        d += f * (0.03 + 0.03 * math.sin(2 * math.pi * x / 0.27 + 0.5)) * smooth(0.0, 0.1, z0 - z)
    g = math.exp(-(((x - XT) / 0.17) ** 2 + ((z - ZT) / 0.15) ** 2) * 0.5)
    d += BULGE * g * env
    # pulled tight over the edges of the wardrobe
    d *= 0.25 + 0.75 * smooth(0.0, 0.09, HW - abs(x)) * smooth(0.0, 0.09, HTOP - z)
    return max(0.003, d)


def surf(a, b):
    """point + outward normal on the wrapped cloth (front / sides / top)"""
    side = abs(a) > HW + 1e-6
    top = b > HTOP + 1e-6
    s = 1.0 if a >= 0 else -1.0
    if not side and not top:
        d = front_disp(a, b)
        return Vector((a, -EPS - d, b)), (0, -1, 0)
    if side and not top:
        k = abs(a) - HW
        d = 0.004 + 0.008 * (0.5 + 0.5 * math.sin(b * 9 + k * 20)) * smooth(0, 0.06, k)
        if b < rope_z(0, 0):
            d += 0.02 * (rope_z(0, 0) - b) / rope_z(0, 0)
        return Vector((s * (W / 2 + EPS + d), -EPS + k, b)), (s, 0, 0)
    if top and not side:
        k = b - HTOP
        d = 0.004 + 0.006 * (0.5 + 0.5 * math.sin(a * 11))
        return Vector((a, -EPS + k, H + EPS + d)), (0, 0, 1)
    return Vector((s * (W / 2 + EPS), -EPS + abs(a) - HW, H + EPS + (b - HTOP))), (0, 0, 1)


# ---------- the knife cut ----------
def slash_ends():
    return [(XT + L * math.cos(math.radians(t)), ZT + L * math.sin(math.radians(t))) for t, L in SLASH]


def inside_quad(x, z, ends):
    n = len(ends)
    for k in range(n):
        x0, z0 = ends[k]; x1, z1 = ends[(k + 1) % n]
        if (x1 - x0) * (z - z0) - (z1 - z0) * (x - x0) < 0:
            return False
    return True


def sector(x, z):
    ang = math.degrees(math.atan2(z - ZT, x - XT)) % 360
    ts = [t for t, _ in SLASH]
    for k in range(4):
        t0 = ts[k]; t1 = ts[(k + 1) % 4] + (360 if k == 3 else 0)
        a2 = ang if ang >= t0 else ang + 360
        if t0 <= a2 < t1:
            return k
    return 0


def fold(x, z, k, ends):
    """flap k (between slash k and k+1) peeled out around its hinge (the line between the two slash ends), curling"""
    (x0, z0), (x1, z1) = ends[k], ends[(k + 1) % 4]
    hx, hz = x1 - x0, z1 - z0
    L = math.hypot(hx, hz); hx /= L; hz /= L
    tpar = (x - x0) * hx + (z - z0) * hz
    fx, fz = x0 + hx * tpar, z0 + hz * tpar            # foot on the hinge
    t = math.hypot(x - fx, z - fz)
    tmax = abs((XT - x0) * hz - (ZT - z0) * hx)
    kap = math.radians(PEEL[k]) / max(0.05, tmax)
    ix, iz = (x - fx) / max(1e-6, t), (z - fz) / max(1e-6, t)
    base, _ = surf(fx, fz)
    r = math.sin(kap * t) / kap
    o = (1 - math.cos(kap * t)) / kap
    # the cut edge is a bit ragged
    o += 0.004 * math.sin(x * 90 + z * 70)
    return Vector((base.x + ix * r, base.y - o, base.z + iz * r))


# ---------- scene ----------
scn = bpy.data.scenes.get("WardrobeCloth") or bpy.data.scenes.new("WardrobeCloth")
if bpy.context.window is not None:
    bpy.context.window.scene = scn
for o in list(scn.objects):
    bpy.data.objects.remove(o, do_unlink=True)
for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
    for d in list(coll):
        if d.name.startswith("WC_") and d.users == 0:
            coll.remove(d)


def link(obj):
    scn.collection.objects.link(obj)
    return obj


FINE, FINE_R = 0.02, 0.36       # finer grid round the knife cut (cut version only)
USE_FINE = [False]


def graded(lo, hi, c):
    """samples from lo to hi: STEP apart, FINE apart within FINE_R of c"""
    out = [lo]
    while out[-1] < hi - 1e-6:
        x = out[-1]
        st = FINE if USE_FINE[0] and abs(x - c) < FINE_R else STEP
        out.append(min(hi, x + st))
    if hi - out[-2] < STEP * 0.3 and len(out) > 2:
        out.pop(-2)
    return out


def a_samples():
    front = graded(-HW, HW, XT)
    ns = max(2, int(round(SW / STEP)))
    side = [HW + SW * i / ns for i in range(1, ns + 1)]
    return [-s for s in reversed(side)] + front + side


def build_cloth(name, cut):
    USE_FINE[0] = cut
    A = a_samples()
    zref = graded(b0(XT), HTOP, ZT)
    T = [(z - zref[0]) / (zref[-1] - zref[0]) for z in zref]
    n1 = len(T) - 1
    n2 = max(3, int(round(TB / STEP)))
    ends = slash_ends()
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    grid = {}
    meta = {}
    bmin, bmax = 0.25, HTOP + TB + 0.04
    for i, a in enumerate(A):
        for j in range(n1 + n2 + 1):
            if j <= n1:
                b = b0(a) + (HTOP - b0(a)) * T[j]
            else:
                b = HTOP + tbend(a) * (j - n1) / n2
            p, _ = surf(a, b)
            grid[i, j] = p
            front = abs(a) <= HW + 1e-6 and b <= HTOP + 1e-6
            ins = cut and front and inside_quad(a, b, ends)
            meta[i, j] = (a, b, ins, sector(a, b) if ins else -1)
    verts = {}

    def vert(i, j, k):
        a, b, ins, sec = meta[i, j]
        key = (i, j, k if ins else -1)
        if key not in verts:
            p = fold(a, b, k, ends) if ins else grid[i, j]
            verts[key] = bm.verts.new(p)
        return verts[key]

    for i in range(len(A) - 1):
        for j in range(n1 + n2):
            ids = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]
            sides = [abs(meta[c][0]) > HW + 1e-6 for c in ids]
            tops = [meta[c][1] > HTOP + 1e-6 for c in ids]
            if any(s and t for s, t in zip(sides, tops)) or (any(sides) and any(tops)):
                continue
            secs = {meta[c][3] for c in ids if meta[c][2]}
            if len(secs) > 1:
                continue                       # straddles a slash: cut open
            k = secs.pop() if secs else -1
            if k >= 0:
                # a face with every corner inside the cut near the middle is gone (the knife went through)
                cx = sum(meta[c][0] for c in ids) / 4; cz = sum(meta[c][1] for c in ids) / 4
                if math.hypot(cx - XT, cz - ZT) < 0.03:
                    continue
            f = bm.faces.new([vert(c[0], c[1], k) for c in ids])
            for loop, c in zip(f.loops, ids):
                a, b = meta[c][0], meta[c][1]
                loop[uv].uv = ((a + AMAX) / (2 * AMAX), (b - bmin) / (bmax - bmin))
    bm.normal_update()
    # outward normals (front faces must point to -Y)
    fr = [f for f in bm.faces if abs(f.calc_center_median().z - 2.3) < 0.1 and abs(f.calc_center_median().x) < 0.5]
    if fr and sum(f.normal.y for f in fr) > 0:
        for f in bm.faces:
            f.normal_flip()
    me = bpy.data.meshes.new("WC_" + name)
    bm.to_mesh(me)
    bm.free()
    ob = link(bpy.data.objects.new(name, me))
    return ob, (bmin, bmax)


# ---------- ropes ----------
def tube(bm, uv, pts, radius, closed=False, sides=6, vrep=0.15, rad_fn=None):
    n = len(pts)
    tang = []
    for i in range(n):
        if closed:
            t = pts[(i + 1) % n] - pts[i - 1]
        else:
            t = pts[min(n - 1, i + 1)] - pts[max(0, i - 1)]
        tang.append(t.normalized())
    up = Vector((0, 0, 1)) if abs(tang[0].z) < 0.9 else Vector((1, 0, 0))
    nrm = (up - tang[0] * up.dot(tang[0])).normalized()
    rings = []
    s = 0.0
    for i in range(n):
        if i > 0:
            s += (pts[i] - pts[i - 1]).length
            t = tang[i]
            nrm = (nrm - t * nrm.dot(t)).normalized()
        bi = tang[i].cross(nrm)
        r = radius * (rad_fn(i, n) if rad_fn else 1.0)
        ring = []
        for k in range(sides + 1):
            ang = 2 * math.pi * k / sides
            ring.append(bm.verts.new(pts[i] + (nrm * math.cos(ang) + bi * math.sin(ang)) * r))
        rings.append((ring, s))
    segs = n if closed else n - 1
    for i in range(segs):
        (r0, s0), (r1, s1) = rings[i], rings[(i + 1) % n]
        if closed and i == n - 1:
            s1 = s0 + (pts[0] - pts[-1]).length
        for k in range(sides):
            f = bm.faces.new([r0[k], r0[k + 1], r1[k + 1], r1[k]])
            for loop, (u, v) in zip(f.loops, [(k / sides, s0 / vrep), ((k + 1) / sides, s0 / vrep),
                                              ((k + 1) / sides, s1 / vrep), (k / sides, s1 / vrep)]):
                loop[uv].uv = (u, v)
    if not closed:
        for ring, end in ((rings[0][0], True), (rings[-1][0], False)):
            c = bm.verts.new(sum((v.co for v in ring[:-1]), Vector()) / sides)
            for k in range(sides):
                f = bm.faces.new([ring[k], ring[k + 1], c] if not end else [ring[k + 1], ring[k], c])
                for loop in f.loops:
                    loop[uv].uv = (0.5, 0.0)


def rope_loop(i):
    """closed lashing round the wardrobe at rope i"""
    yf = -(EPS + 0.006) - RR + 0.004
    xs = W / 2 + EPS + 0.006 + RR - 0.003
    yb = D + RR
    rc = 0.04
    pts = []

    def seg(p0, p1, n):
        for k in range(n):
            pts.append(p0.lerp(p1, k / n))
    corners = [Vector((-xs, yf, 0)), Vector((xs, yf, 0)), Vector((xs, yb, 0)), Vector((-xs, yb, 0))]
    for c in range(4):
        p0, p1 = corners[c], corners[(c + 1) % 4]
        d = (p1 - p0).normalized()
        a0, a1 = p0 + d * rc, p1 - d * rc
        seg(a0, a1, max(2, int((a1 - a0).length / 0.04)))
        # rounded corner
        p2 = corners[(c + 2) % 4]
        d2 = (p2 - p1).normalized()
        for k in range(3):
            t = (k + 0.5) / 3
            q = (p1 - d * rc).lerp(p1, t).lerp(p1.lerp(p1 + d2 * rc, t), t)
            pts.append(q)
    out = []
    for p in pts:
        z = rope_z(i, max(-HW, min(HW, p.x)))
        z += 0.012 * math.sin(p.y * 5 + i)       # it does not sit perfectly level round the back
        out.append(Vector((p.x, p.y, z)))
    return out


def knot_and_tails(bm, uv, i):
    _, _, xk = ROPES[i]
    zk = rope_z(i, xk)
    yf = -(EPS + 0.006) - RR + 0.004
    # a fat wrapped knot: a short coil round the lashing plus a loop standing out
    coil = []
    for k in range(40):
        t = k / 39
        ang = t * 2.5 * 2 * math.pi
        coil.append(Vector((xk - 0.045 + 0.09 * t, yf + 0.027 * math.cos(ang), zk + 0.027 * math.sin(ang))))
    tube(bm, uv, coil, RR * 0.95)
    loop = []
    for k in range(18):
        ang = k / 18 * 2 * math.pi
        loop.append(Vector((xk + 0.015 + 0.042 * math.cos(ang), yf - 0.038 - 0.03 * math.sin(ang) * 0.6, zk + 0.008 + 0.04 * math.sin(ang))))
    tube(bm, uv, loop, RR * 0.9, closed=True)
    # two tails hanging down over the cloth
    for side, length, drift in ((-1, 0.36 + 0.05 * i, -0.05), (1, 0.27 + 0.04 * i, 0.07)):
        pts = []
        x0, z0 = xk + side * 0.025, zk - 0.02
        steps = int(length / 0.03)
        for k in range(steps + 1):
            t = k / steps
            x = x0 + drift * t * t + 0.012 * math.sin(t * 7 + i)
            z = z0 - length * t
            yc = -EPS - front_disp(x, z) - RR * 0.9 - 0.004
            y = (yf - 0.02) * (1 - smooth(0, 0.25, t)) + yc * smooth(0, 0.25, t)
            pts.append(Vector((x, min(y, yc), z)))
        tube(bm, uv, pts, RR * 0.9, rad_fn=lambda q, n: 1.0 + (0.45 if q >= n - 2 else 0.0))   # frayed end


def build_ropes():
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    for i in range(len(ROPES)):
        tube(bm, uv, rope_loop(i), RR, closed=True)
        knot_and_tails(bm, uv, i)
    me = bpy.data.meshes.new("WC_Ropes")
    bm.to_mesh(me)
    bm.free()
    return link(bpy.data.objects.new("Ropes", me))


def occluders():
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, D / 2, H / 2))
    box = bpy.context.active_object
    box.scale = (W, D, H); box.name = "BakeWardrobe"
    bpy.ops.mesh.primitive_plane_add(size=12, location=(0, 0, 0))
    gr = bpy.context.active_object; gr.name = "BakeGround"
    return [box, gr]


intact, uvr = build_cloth("Cloth_Intact", cut=False)
cutc, _ = build_cloth("Cloth_Cut", cut=True)
ropes = build_ropes()
tape = link(bpy.data.objects.new("TapeSpot", None))
tape.location = (XT, TAPE_Y, ZT)
tape.empty_display_size = 0.08
occ = occluders()

# ---------- bake the lighting (one cloth version at a time) ----------
scn.render.engine = 'CYCLES'
scn.cycles.device = 'CPU'
scn.cycles.samples = 48
world = bpy.data.worlds.get("WC_World") or bpy.data.worlds.new("WC_World")
scn.world = world
try:
    world.use_nodes = True
except Exception:
    pass
bg = world.node_tree.nodes.get("Background")
bg.inputs[0].default_value = (0.55, 0.55, 0.55, 1)
bg.inputs[1].default_value = 1.0
sun_data = bpy.data.lights.get("WC_Sun") or bpy.data.lights.new("WC_Sun", 'SUN')
sun_data.energy = 2.2
sun_data.angle = math.radians(8)
sun = link(bpy.data.objects.new("BakeSun", sun_data))
sun.rotation_euler = (math.radians(50), 0, math.radians(-25))     # from high up, front-left


def bake_lighting(ob, out_name, other):
    other.hide_render = True
    ob.hide_render = False
    mat = bpy.data.materials.new("WC_bake_" + out_name)
    try:
        mat.use_nodes = True
    except Exception:
        pass
    img = bpy.data.images.new("WC_light_" + out_name, 512, 512, float_buffer=True)
    nt = mat.node_tree
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    nt.nodes.active = tex
    ob.data.materials.clear()
    ob.data.materials.append(mat)
    for o in scn.objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.bake(type='DIFFUSE', pass_filter={'DIRECT', 'INDIRECT'}, margin=6, use_clear=True)
    px = np.empty(512 * 512 * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    np.save(os.path.join(HERE, "lighting_%s.npy" % out_name), px.reshape(512, 512, 4)[:, :, :3])
    ob.data.materials.clear()
    other.hide_render = False


bpy.context.view_layer.update()
bake_lighting(intact, "intact", cutc)
bake_lighting(cutc, "cut", intact)

# ---------- thickness, materials, export ----------
for ob in (intact, cutc):
    mod = ob.modifiers.new("Thick", 'SOLIDIFY')
    mod.thickness = 0.022
    mod.offset = -1.0
    mod.use_rim = True
    for o in scn.objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.modifier_apply(modifier="Thick")
    for p in ob.data.polygons:
        p.use_smooth = True


def plain_mat(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    return m


intact.data.materials.append(plain_mat("WardrobeCloth_Intact"))
cutc.data.materials.append(plain_mat("WardrobeCloth_Cut"))
ropes.data.materials.append(plain_mat("WardrobeRope"))

for o in occ + [sun]:
    o.hide_set(True) if bpy.context.window is not None else None
for o in scn.objects:
    o.select_set(o.name in ("Cloth_Intact", "Cloth_Cut", "Ropes", "TapeSpot"))
bpy.ops.export_scene.fbx(filepath=OUT_FBX, use_selection=True, object_types={'MESH', 'EMPTY'},
                         axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                         bake_space_transform=True, mesh_smooth_type='FACE', path_mode='STRIP')

json.dump({"AMAX": AMAX, "HW": HW, "bmin": uvr[0], "bmax": uvr[1], "ropes": ROPES, "XT": XT, "ZT": ZT, "W": W, "H": H,
           "HTOP": HTOP, "slash": SLASH},
          open(os.path.join(HERE, "layout.json"), "w"), indent=1)
result = {"intact_faces": len(intact.data.polygons), "cut_faces": len(cutc.data.polygons), "rope_faces": len(ropes.data.polygons)}
