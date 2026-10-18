# The Questioner - head, neck and upper chest (metaballs), teeth and eyes.   Blender: exec(open(this).read())
# Face looks toward -Y.  The mouth hangs wide open (the "split mouth" of the concept), lined with long uneven teeth.
import bpy, bmesh, math, random
from mathutils import Vector, Quaternion, Matrix

HERE = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\AnimationTools\questioner"
BUILD_HANDS = False
exec(open(HERE + r"\q04_meta.py", encoding="utf8").read())

V = Vector
HEAD_DEC = globals().get("HEAD_DEC", 0.08)

# --- face landmarks (shared with the teeth)
MOUTH_TOP = 1.838        # upper gum line (front)
MOUTH_BOT = 1.692        # lower gum line (front)
MOUTH_HALF_W = 0.034
FRONT_Y = -0.100         # front of the gums


def build_head():
    m = Meta("Q_Head", resolution=0.004)
    # ---------------- cranium (mostly under the keffiyeh)
    m.ellipsoid((0, 0.018, 1.945), 0.05, (1.42, 1.86, 1.95), stiff=2.0)
    m.ellipsoid((0, -0.040, 1.975), 0.04, (1.45, 1.1, 1.2))          # forehead
    m.capsule((-0.046, -0.074, 1.950), (0.046, -0.074, 1.950), 0.013)   # heavy brow ridge
    for sx in (1, -1):
        m.ball((0.030 * sx, -0.080, 1.958), 0.011)                     # furrowed brow knots
        m.ellipsoid((0.056 * sx, -0.055, 1.895), 0.013, (1.0, 1.6, 0.7), stiff=1.6)   # cheekbones
        m.ball((0.064 * sx, 0.000, 1.880), 0.014)                      # temples / jaw muscle area
        m.ball((0.034 * sx, -0.080, 1.915), 0.0215, neg=True, stiff=2.6)  # deep eye sockets
        m.ball((0.050 * sx, -0.066, 1.835), 0.017, neg=True, stiff=1.4)   # sunken cheeks
        m.ellipsoid((0.073 * sx, 0.010, 1.915), 0.012, (0.5, 1.2, 1.6))   # ears (small, shrivelled)
    # ---------------- nose: long, drooping, bulbous tip
    m.capsule((0, -0.084, 1.945), (0, -0.124, 1.870), 0.0125)
    m.ball((0, -0.103, 1.912), 0.0115)                                # bony hump
    m.ball((0, -0.129, 1.858), 0.0170)
    for sx in (1, -1):
        m.ball((0.018 * sx, -0.112, 1.856), 0.0120)                   # nostril wings
        m.ball((0.009 * sx, -0.122, 1.846), 0.0060, neg=True, stiff=3)  # nostril holes
    # ---------------- upper jaw (maxilla) with the gum arch
    m.ellipsoid((0, -0.080, 1.848), 0.03, (1.45, 1.0, 0.55))
    # ---------------- hanging lower jaw
    for sx in (1, -1):
        hinge = V((0.060 * sx, -0.012, 1.852))
        chin = V((0.026 * sx, -0.100, 1.668))
        m.capsule(hinge, chin, 0.0135)                                # mandible rami (long)
        # stretched cheek skin walls either side of the opening
        m.ellipsoid((0.043 * sx, -0.074, 1.770), 0.017, (0.70, 1.55, 3.6))
    m.ball((0, -0.104, 1.660), 0.021)                                 # chin
    m.ellipsoid((0, -0.088, 1.685), 0.025, (1.35, 1.0, 0.6))           # lower gum arch base
    # ---------------- the mouth hole (front only) and throat
    m.ellipsoid((0, -0.120, 1.765), 0.03, (1.05, 1.25, 2.45), neg=True, stiff=2.8)
    m.ellipsoid((0, -0.088, 1.765), 0.024, (0.95, 1.0, 2.1), neg=True, stiff=2.2)
    m.ellipsoid((0, -0.050, 1.762), 0.028, (1.1, 0.45, 2.2), stiff=2.0)       # back of the throat
    # ---------------- neck: thin, stringy tendons, adam's apple
    m.capsule((0, 0.018, 1.845), (0, 0.012, 1.600), 0.034)
    for sx in (1, -1):
        m.capsule((0.050 * sx, 0.006, 1.860), (0.014 * sx, -0.040, 1.590), 0.0105)   # sternocleidomastoid
        m.capsule((0.030 * sx, 0.030, 1.840), (0.060 * sx, 0.025, 1.600), 0.010)     # back neck cords
    m.ball((0, -0.034, 1.716), 0.012)                                 # adam's apple
    m.capsule((0, -0.030, 1.700), (0, -0.026, 1.620), 0.010)          # trachea
    # ---------------- collarbones, sternum, starved rib cage (shows in the robe's V)
    for sx in (1, -1):
        m.capsule((0.016 * sx, -0.042, 1.585), (0.120 * sx, -0.025, 1.596), 0.0072, stiff=1.5)
    m.ellipsoid((0, 0.008, 1.470), 0.06, (2.05, 1.30, 1.85))
    m.capsule((0, -0.062, 1.575), (0, -0.064, 1.380), 0.010)          # sternum ridge
    for k in range(5):
        z = 1.545 - 0.040 * k
        for sx in (1, -1):
            m.capsule((0.018 * sx, -0.066, z), (0.100 * sx, -0.050, z - 0.030), 0.0072, stiff=1.8)
            m.capsule((0.100 * sx, -0.050, z - 0.030), (0.135 * sx, 0.000, z - 0.055), 0.0068, stiff=1.8)
    ob = m.to_mesh(voxel=0.0028, decimate=None)
    sculpt(ob, HEAD_SCULPT)
    if HEAD_DEC:
        decimate(ob, HEAD_DEC)
    return ob


HEAD_SCULPT = []
for sx in (1, -1):
    HEAD_SCULPT += [
        {"op": "normal", "c": (0.034 * sx, -0.084, 1.921), "r": 0.033, "amt": -0.018, "p": 1.3},      # sink the eye sockets
        {"op": "move", "c": (0.034 * sx, -0.090, 1.944), "r": 0.020, "v": (0, -0.004, -0.002)},       # brow overhangs the eye
        {"op": "normal", "c": (0.050 * sx, -0.070, 1.830), "r": 0.030, "amt": -0.008},                # hollow cheeks
        {"op": "normal", "c": (0.066 * sx, -0.030, 1.950), "r": 0.030, "amt": -0.006},                # sunken temples
    ]
HEAD_SCULPT += [
    {"op": "move", "c": (0, -0.128, 1.860), "r": 0.040, "v": (0, -0.016, -0.014)},                 # long drooping nose
    {"op": "normal", "c": (0, -0.084, 1.955), "r": 0.020, "amt": 0.004},                           # furrow knot
]


def tooth(bm, base, direction, length, radius, twist=0.0):
    """tapered 5-sided tooth from base along direction"""
    d = direction.normalized()
    q = V((0, 0, 1)).rotation_difference(d)
    res = bmesh.ops.create_cone(bm, cap_ends=True, segments=5, radius1=radius, radius2=radius * 0.25, depth=length)
    vs = res["verts"]
    bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=Matrix.Rotation(twist, 3, "Z"), verts=vs)
    bmesh.ops.translate(bm, vec=(0, 0, length / 2), verts=vs)
    bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=q.to_matrix(), verts=vs)
    bmesh.ops.translate(bm, vec=base, verts=vs)


def build_teeth(seed=7):
    rnd = random.Random(seed)
    old = bpy.data.objects.get("Q_Teeth")
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    bm = bmesh.new()
    # upper and lower arches: points on an arc, teeth point into the mouth opening
    def arch(z, y_front, half_w, n, down):
        for i in range(n):
            if rnd.random() < 0.12:
                continue                     # a missing tooth here and there
            t = -1 + 2 * (i + 0.5) / n
            x = t * half_w
            y = y_front + 0.030 * t * t
            ln = rnd.uniform(0.014, 0.026) * (1.15 if abs(t) < 0.4 else 1.0)
            r = rnd.uniform(0.0050, 0.0068)
            d = V((rnd.uniform(-0.25, 0.25), rnd.uniform(-0.25, 0.15), -1 if down else 1))
            tooth(bm, V((x, y, z)), d, ln, r, rnd.uniform(0, 1))
    arch(MOUTH_TOP - 0.004, FRONT_Y + 0.004, MOUTH_HALF_W + 0.006, 13, True)
    arch(MOUTH_BOT + 0.004, FRONT_Y + 0.008, MOUTH_HALF_W + 0.002, 13, False)
    # teeth along both edges of the split, pointing inward (like the concept's long mouth)
    for sx in (1, -1):
        for k in range(8):
            z = MOUTH_BOT + 0.018 + (MOUTH_TOP - MOUTH_BOT - 0.036) * k / 7.0
            x = (MOUTH_HALF_W + 0.006) * sx
            ln = rnd.uniform(0.012, 0.020)
            d = V((-sx, rnd.uniform(-0.4, 0.1), rnd.uniform(-0.5, 0.5)))
            tooth(bm, V((x, FRONT_Y + 0.016, z)), d, ln, rnd.uniform(0.0042, 0.0058), rnd.uniform(0, 1))
    me = bpy.data.meshes.new("Q_Teeth")
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new("Q_Teeth", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def eye_seat(sx):
    """y of the socket floor in front of the eye centre (ray cast from the front)"""
    head = bpy.data.objects.get("Q_Head")
    if head is None:
        return -0.058
    hit, loc, nor, idx = head.ray_cast(V((0.034 * sx, -0.30, 1.922)), V((0, 1, 0)))
    return loc.y if hit else -0.058


def build_eyes():
    old = bpy.data.objects.get("Q_Eyes")
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    bm = bmesh.new()
    for sx in (1, -1):
        res = bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=7, radius=0.0125)
        bmesh.ops.translate(bm, vec=(0.034 * sx, eye_seat(sx) + 0.0060, 1.922), verts=res["verts"])
    me = bpy.data.meshes.new("Q_Eyes")
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new("Q_Eyes", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob

if globals().get("BUILD_HEAD", True):
    head = build_head()
    teeth = build_teeth()
    eyes = build_eyes()
    result = {"head_tris": sum(len(p.vertices) - 2 for p in head.data.polygons), "teeth_tris": sum(len(p.vertices) - 2 for p in teeth.data.polygons)}
