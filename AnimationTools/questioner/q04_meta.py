# Metaball sculpting helpers + the hands.   Blender: exec(open(this).read())
# A metaball "part" is built from balls / capsules / ellipsoids (negative ones carve), converted to a mesh, voxel-remeshed and
# decimated to a game budget.  Used for the hands (here) and the head (q05_head.py).
import bpy, math
from mathutils import Vector, Quaternion, Matrix

HERE = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\AnimationTools\questioner"
BUILD_RIG = False
exec(open(HERE + r"\q01_rig.py", encoding="utf8").read())


class Meta:
    def __init__(self, name, resolution=0.004, threshold=0.6):
        old = bpy.data.objects.get(name)
        if old:
            bpy.data.objects.remove(old, do_unlink=True)
        mb = bpy.data.metaballs.get(name)
        if mb:
            bpy.data.metaballs.remove(mb)
        self.mb = bpy.data.metaballs.new(name)
        self.mb.resolution = resolution
        self.mb.render_resolution = resolution
        self.mb.threshold = threshold
        self.ob = bpy.data.objects.new(name, self.mb)
        bpy.context.scene.collection.objects.link(self.ob)
        self.name = name

    def ball(self, co, r, stiff=2.0, neg=False):
        e = self.mb.elements.new(type="BALL")
        e.co = Vector(co)
        e.radius = r * 2.2               # radii are the wanted surface radius (a lone element ends at ~0.45 r)
        e.stiffness = stiff
        e.use_negative = neg
        return e

    def ellipsoid(self, co, r, size, rot=None, stiff=2.0, neg=False):
        e = self.mb.elements.new(type="ELLIPSOID")
        e.co = Vector(co)
        e.radius = r * 2.2
        e.size_x, e.size_y, e.size_z = size
        if rot is not None:
            e.rotation = rot
        e.stiffness = stiff
        e.use_negative = neg
        return e

    def capsule(self, a, b, r, stiff=2.0, neg=False):
        a, b = Vector(a), Vector(b)
        e = self.mb.elements.new(type="CAPSULE")
        e.co = (a + b) / 2
        e.radius = r * 1.93
        e.size_x = (b - a).length / 2
        e.rotation = Vector((1, 0, 0)).rotation_difference((b - a).normalized())
        e.stiffness = stiff
        e.use_negative = neg
        return e

    def to_mesh(self, voxel=0.004, decimate=None, smooth=True):
        """convert, remesh, optionally decimate (ratio) -> returns the mesh object"""
        bpy.ops.object.select_all(action="DESELECT")
        self.ob.select_set(True)
        bpy.context.view_layer.objects.active = self.ob
        bpy.context.view_layer.update()
        bpy.ops.object.convert(target="MESH")
        ob = bpy.context.view_layer.objects.active
        ob.name = self.name
        if voxel:
            r = ob.modifiers.new("Remesh", "REMESH")
            r.mode = "VOXEL"
            r.voxel_size = voxel
            r.adaptivity = 0.0
            bpy.ops.object.modifier_apply(modifier="Remesh")
        if decimate:
            d = ob.modifiers.new("Decimate", "DECIMATE")
            d.ratio = decimate
            d.use_collapse_triangulate = True
            bpy.ops.object.modifier_apply(modifier="Decimate")
        for p in ob.data.polygons:
            p.use_smooth = smooth
        return ob


def build_hand(side):
    s = side
    sx = 1 if s == "Left" else -1
    m = Meta("Q_Hand" + s[0], resolution=0.0035)
    wrist = J[s + "Hand"]
    hd = (J[s + "HandEnd"] - wrist).normalized()
    across = Vector((0, -1, 0))                               # toward the index finger
    up = hd.cross(across).normalized() * -sx                  # back of the hand
    if up.z < 0:
        up = -up
    # wrist knob + palm slab
    m.capsule(wrist - hd * 0.03, wrist + hd * 0.01, 0.019, stiff=2.2)
    m.ball(wrist + across * 0.012 + up * 0.004, 0.012, stiff=1.5)       # ulna knob
    palm_c = wrist + hd * 0.055 + up * 0.002
    rot = Matrix((hd, across, hd.cross(across))).transposed().to_quaternion()
    m.ellipsoid(palm_c + hd * 0.006, 0.03, (1.75, 1.15, 0.36), rot=rot, stiff=2.0)
    for f, base_r in (("Index", 0.0088), ("Middle", 0.0092), ("Ring", 0.0086), ("Pinky", 0.0078)):
        k1 = J["%sHand%s1" % (s, f)]
        # metacarpal ridge on the back of the hand (bony)
        m.capsule(wrist + hd * 0.02 + (k1 - wrist).project(across) * 0.35 + up * 0.009, k1 + up * 0.006 - hd * 0.006, 0.0062, stiff=1.6)
        pts = [J["%sHand%s%d" % (s, f, i)] for i in (1, 2, 3, 4)]
        for i in range(3):
            a, b = pts[i], pts[i + 1]
            r = base_r * (1.0, 0.86, 0.74)[i]
            m.capsule(a, b, r * 0.80, stiff=2.0)              # thin phalanx
            m.ball(a, r * 1.10, stiff=2.0)                     # swollen joint
        d = (pts[3] - pts[2]).normalized()
        m.capsule(pts[3] - d * 0.004 + up * 0.002, pts[3] + d * 0.020 - up * 0.004, 0.0034, stiff=2.0)   # long nail / claw
    # thumb
    t = [J["%sHandThumb%d" % (s, i)] for i in (1, 2, 3, 4)]
    m.capsule(wrist + hd * 0.012 + across * 0.010, t[0], 0.011, stiff=2.0)
    for i in range(3):
        r = (0.0098, 0.0088, 0.0075)[i]
        m.capsule(t[i], t[i + 1], r * 0.82)
        m.ball(t[i], r * 1.08)
    d = (t[3] - t[2]).normalized()
    m.capsule(t[3] - d * 0.004, t[3] + d * 0.018, 0.0034)
    return m.to_mesh(voxel=0.0025, decimate=0.10)

if globals().get("BUILD_HANDS", True):
    hl = build_hand("Left")
    hr = build_hand("Right")
    result = {"L": len(hl.data.polygons), "R": len(hr.data.polygons)}


# ---------------------------------------------------------------- procedural sculpting on a dense mesh
import numpy as np

def _coords(ob):
    n = len(ob.data.vertices)
    co = np.empty(n * 3, dtype=np.float64)
    ob.data.vertices.foreach_get("co", co)
    return co.reshape(n, 3)

def _normals(ob):
    n = len(ob.data.vertices)
    no = np.empty(n * 3, dtype=np.float64)
    ob.data.vertices.foreach_get("normal", no)
    return no.reshape(n, 3)

def _falloff(d, r, power=2.0):
    t = np.clip(1.0 - d / r, 0.0, 1.0)
    return (t * t * (3 - 2 * t)) ** (power / 2.0)

def sculpt(ob, ops):
    """ops: list of dicts
         {"op": "move", "c": center, "r": radius, "v": vector}                       grab-move
         {"op": "normal", "c": center, "r": radius, "amt": metres}                   inflate (+) / deflate (-)
         {"op": "toward", "c": center, "r": radius, "target": point, "amt": 0..1}    pull toward a point
         {"op": "scale", "c": center, "r": radius, "s": (sx, sy, sz)}               scale about the centre
       optional "p": falloff power, "box": (sx, sy, sz) to stretch the influence region"""
    co = _coords(ob)
    for o in ops:
        no = _normals(ob)
        c = np.array(o["c"], dtype=np.float64)
        box = np.array(o.get("box", (1, 1, 1)), dtype=np.float64)
        d = np.linalg.norm((co - c) / box, axis=1)
        w = _falloff(d, o["r"], o.get("p", 2.0))[:, None]
        if o["op"] == "move":
            co = co + w * np.array(o["v"])
        elif o["op"] == "normal":
            co = co + w * no * o["amt"]
        elif o["op"] == "toward":
            co = co + w * (np.array(o["target"]) - co) * o["amt"]
        elif o["op"] == "scale":
            co = co + w * (c + (co - c) * np.array(o["s"]) - co)
        ob.data.vertices.foreach_set("co", co.ravel())
        ob.data.update()
    return ob

def smooth_mesh(ob, iterations=2, factor=0.5):
    m = ob.modifiers.new("Smooth", "SMOOTH")
    m.iterations = iterations
    m.factor = factor
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier="Smooth")

def decimate(ob, ratio):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    d = ob.modifiers.new("Decimate", "DECIMATE")
    d.ratio = ratio
    d.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier="Decimate")
