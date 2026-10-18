# The Questioner - base body (skin modifier over a joint graph built from the rig joints).   Blender: exec(open(this).read())
# Gaunt, 2.05 m, long arms (fingertips at the knee), long bony fingers and toes.  Output object: Q_Body (modifiers applied).
import bpy, bmesh, math
from mathutils import Vector

HERE = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\AnimationTools\questioner"
BUILD_RIG = False
exec(open(HERE + r"\q01_rig.py", encoding="utf8").read())     # defines J (joints)

SUBDIV = 1
nodes = {}      # name -> (pos, (rx, ry))
edges = []

def N(name, pos, r):
    nodes[name] = (Vector(pos), (r, r) if isinstance(r, (int, float)) else r)

def E(a, b):
    edges.append((a, b))

def lerp(a, b, t):
    return a + (b - a) * t

# ---------------- torso / neck
N("pelvis", (0, 0.018, 0.985), (0.118, 0.092))
N("hips", J["Hips"], (0.122, 0.096))
N("waist", J["Spine"], (0.098, 0.078))
N("belly", J["Spine1"], (0.108, 0.080))
N("chest", J["Spine2"], (0.135, 0.090))
N("upchest", (0, 0.010, 1.540), (0.142, 0.084))
N("neckbase", (0, 0.012, 1.612), (0.058, 0.052))
N("neck", J["Neck1"], (0.040, 0.043))
N("necktop", (0, -0.002, 1.790), (0.044, 0.048))
for a, b in [("pelvis", "hips"), ("hips", "waist"), ("waist", "belly"), ("belly", "chest"), ("chest", "upchest"),
             ("upchest", "neckbase"), ("neckbase", "neck"), ("neck", "necktop")]:
    E(a, b)

for s in ("Left", "Right"):
    p = s[0]
    # ---------------- arm
    shoulder = J[s + "Arm"]
    elbow = J[s + "ForeArm"]
    wrist = J[s + "Hand"]
    N(p + "delt", lerp(J[s + "Shoulder"], shoulder, 0.75) + Vector((0, 0.005, -0.005)), (0.050, 0.048))
    N(p + "sh", shoulder, (0.044, 0.046))
    N(p + "uarm", lerp(shoulder, elbow, 0.5), (0.033, 0.036))
    N(p + "elbow", elbow, (0.032, 0.034))
    N(p + "farm", lerp(elbow, wrist, 0.35), (0.031, 0.027))
    N(p + "farm2", lerp(elbow, wrist, 0.72), (0.024, 0.019))
    N(p + "wrist", wrist, (0.021, 0.015))
    E("upchest", p + "delt")
    for a_, b_ in [(p + "delt", p + "sh"), (p + "sh", p + "uarm"), (p + "uarm", p + "elbow"), (p + "elbow", p + "farm"),
                   (p + "farm", p + "farm2"), (p + "farm2", p + "wrist")]:
        E(a_, b_)
    # (the hands are separate metaball meshes, q04_meta.py)
    # ---------------- leg
    hipj = J[s + "UpLeg"]
    knee = J[s + "Leg"]
    ankle = J[s + "Foot"]
    ball = J[s + "ToeBase"]
    N(p + "hip", hipj + Vector((0, 0.004, -0.02)), (0.070, 0.074))
    N(p + "thigh", lerp(hipj, knee, 0.45), (0.054, 0.058))
    N(p + "thigh2", lerp(hipj, knee, 0.80), (0.044, 0.046))
    N(p + "knee", knee, (0.043, 0.042))
    N(p + "calf", lerp(knee, ankle, 0.28), (0.037, 0.043))
    N(p + "shin", lerp(knee, ankle, 0.65), (0.028, 0.030))
    N(p + "ankle", ankle, (0.025, 0.028))
    N(p + "heel", Vector((ankle.x, 0.062, 0.032)), (0.026, 0.024))
    N(p + "arch", lerp(ankle, ball, 0.5) + Vector((0, 0, -0.025)), (0.034, 0.018))
    N(p + "ball", ball + Vector((0, 0, 0.002)), (0.040, 0.015))
    E("pelvis", p + "hip")
    for a, b in [(p + "hip", p + "thigh"), (p + "thigh", p + "thigh2"), (p + "thigh2", p + "knee"), (p + "knee", p + "calf"),
                 (p + "calf", p + "shin"), (p + "shin", p + "ankle"), (p + "ankle", p + "heel"), (p + "ankle", p + "arch"),
                 (p + "arch", p + "ball")]:
        E(a, b)
    sx = 1 if s == "Left" else -1
    # five long gnarled toes fanning out of the ball
    for k, (dx, ln, r) in enumerate([(-0.030, 0.085, 0.0115), (-0.012, 0.080, 0.009), (0.004, 0.072, 0.0085), (0.019, 0.064, 0.008), (0.032, 0.054, 0.0075)]):
        base = ball + Vector((dx * sx, -0.010, 0.0))
        mid = base + Vector((dx * sx * 0.15, -ln * 0.55, 0.004))
        tip = base + Vector((dx * sx * 0.25, -ln, -0.004))
        a, b, c = "%stoe%da" % (p, k), "%stoe%db" % (p, k), "%stoe%dc" % (p, k)
        N(a, base, r)
        N(b, mid, r * 0.95)
        N(c, tip, r * 0.6)
        E(p + "ball", a); E(a, b); E(b, c)


BODY_NAME = globals().get("BODY_NAME", "Q_Body")


def build():
    old = bpy.data.objects.get(BODY_NAME)
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    names = list(nodes.keys())
    index = {n: i for i, n in enumerate(names)}
    me = bpy.data.meshes.new(BODY_NAME)
    me.from_pydata([nodes[n][0] for n in names], [(index[a], index[b]) for a, b in edges], [])
    ob = bpy.data.objects.new(BODY_NAME, me)
    bpy.context.scene.collection.objects.link(ob)
    skin = ob.modifiers.new("Skin", "SKIN")
    skin.use_smooth_shade = True
    skin.branch_smoothing = 0.6
    sv = me.skin_vertices[0].data
    for n, i in index.items():
        sv[i].radius = nodes[n][1]
        sv[i].use_root = (n == "pelvis")
    sub = ob.modifiers.new("Subdiv", "SUBSURF")
    sub.levels = SUBDIV
    sub.render_levels = SUBDIV
    # apply
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier="Skin")
    bpy.ops.object.modifier_apply(modifier="Subdiv")
    for poly in ob.data.polygons:
        poly.use_smooth = True
    return ob

if globals().get("BUILD_BODY", True):
  body = build()
  result = {"verts": len(body.data.vertices), "faces": len(body.data.polygons), "dims": [round(v, 3) for v in body.dimensions]}
