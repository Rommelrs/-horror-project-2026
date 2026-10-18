# The Questioner v2 head - SDF sculpt of the concept face: tall wrinkled skull, hollow eyes under heavy lids, thin hooked nose,
# protruding upper jaw right under the nose, and the "split" lower face: a long tapering wedge hanging in front of the throat,
# cut open from the front by a huge mouth whose rims (lined with teeth) converge at a pointed chin.   Blender: exec(open(this).read())
# Head upright, face toward -Y (rest pose of QuestionerRig).
import bpy, math
import numpy as np

HERE = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\AnimationTools\questioner"
exec(open(HERE + r"\q13_sdf.py", encoding="utf8").read())
exec(open(HERE + r"\q00_helpers.py", encoding="utf8").read())

# ---------------------------------------------------------------- landmarks (shared with teeth / eyes / painting)
EYE_C = (0.033, -0.060, 1.920)        # eyeball centre (x mirrored)
EYE_R = 0.0122
NOSE_TIP = (0.0, -0.123, 1.872)
# upper gum arch (front -> back), teeth hang below it
UPPER_ARCH = [(0.000, -0.112, 1.849), (0.017, -0.108, 1.849), (0.031, -0.098, 1.848), (0.043, -0.083, 1.846),
              (0.052, -0.064, 1.844), (0.057, -0.044, 1.843)]
# lower jaw: hinged at the mouth corners, swung forward-down JAW_ANGLE below the line of the upper teeth (the side "split").
# Built in a jaw frame: origin JAW_O (between the corners), A = down the jaw, F = the open front face of the jaw.
JAW_O = np.array((0.0, -0.040, 1.844))
JAW_ANGLE = 78.0
_ja = math.radians(JAW_ANGLE)
JAW_A = np.array((0.0, -math.cos(_ja), -math.sin(_ja)))
JAW_F = np.array((0.0, -math.sin(_ja), math.cos(_ja)))
JAW_R = np.stack([np.array((1.0, 0, 0)), -JAW_F, JAW_A], 1).astype(np.float32)     # local (x, depth, s) -> world
# sections along the jaw: (s, half width, half depth, corner rounding); the front face lies on the A axis
LOWER_FACE = [(0.000, 0.045, 0.034, 0.026), (0.030, 0.053, 0.040, 0.022), (0.080, 0.055, 0.040, 0.016),
              (0.120, 0.049, 0.036, 0.014), (0.155, 0.040, 0.031, 0.013), (0.180, 0.030, 0.025, 0.012),
              (0.198, 0.019, 0.018, 0.010), (0.212, 0.009, 0.010, 0.007)]
# mouth cavity sunk into the jaw's front: (s, half width, depth behind the front); pokes CAV_OUT out of the front
CAVITY = [(-0.010, 0.045, 0.034), (0.030, 0.047, 0.046), (0.080, 0.046, 0.048), (0.120, 0.040, 0.044),
          (0.150, 0.031, 0.038), (0.175, 0.022, 0.028), (0.192, 0.012, 0.020), (0.202, 0.005, 0.010)]
CAV_OUT = 0.012
RIM_WALL = 0.0085


def jaw_pt(s, x=0.0, depth=0.0):
    """world point in the jaw frame: s along the jaw, x across, depth behind the front face"""
    return JAW_O + JAW_A * s + np.array((x, 0, 0)) - JAW_F * depth


def rim_points(sx, n=None):
    """centre line of one lower gum rim (top -> chin)"""
    return [tuple(jaw_pt(s, sx * (rx + RIM_WALL * 0.5), 0.004)) for s, rx, d in CAVITY[1:-1]]


def mirror(p, sx):
    return (p[0] * sx, p[1], p[2])


def sample(table, step):
    """linearly resample a (z, ...) table from top to bottom every 'step' metres of z"""
    out = []
    for i in range(len(table) - 1):
        a, b = np.array(table[i]), np.array(table[i + 1])
        n = max(1, int(round(abs(a[0] - b[0]) / step)))
        for k in range(n):
            out.append(tuple(a + (b - a) * k / n))
    out.append(tuple(table[-1]))
    return out


def build_sdf(vs=0.0025):
    S = SDF((-0.165, -0.175, 1.320), (0.165, 0.160, 2.085), vs)
    # ------------------------------------------------ cranium + forehead
    S.add(Ellipsoid((0, 0.020, 1.958), (0.071, 0.097, 0.088)))
    S.add(Ellipsoid((0, -0.036, 1.968), (0.060, 0.054, 0.056)), 0.02)
    for sx in (1, -1):
        S.sub(Ellipsoid((0.079 * sx, -0.036, 1.935), (0.016, 0.030, 0.024)), 0.016)          # sunken temples
    # ------------------------------------------------ neck: thick sinewy column, cords sunk into it, adam's apple
    S.add(Capsule((0, 0.020, 1.880), (0, 0.010, 1.600), 0.039, 0.041), 0.02)
    for sx in (1, -1):
        S.add(Capsule((0.046 * sx, 0.010, 1.868), (0.013 * sx, -0.030, 1.596), 0.0090, 0.0085), 0.012)   # sternocleidomastoid
        S.add(Capsule((0.026 * sx, 0.044, 1.850), (0.040 * sx, 0.036, 1.610), 0.0085), 0.014)
    S.add(Ball((0, -0.030, 1.712), 0.0090), 0.008)
    S.add(Capsule((0, -0.026, 1.695), (0, -0.024, 1.620), 0.0085), 0.008)
    # ------------------------------------------------ midface mass, cheekbones, brow
    S.add(Ellipsoid((0, -0.050, 1.886), (0.056, 0.046, 0.054)), 0.022)
    for sx in (1, -1):
        S.add(Capsule((0.036 * sx, -0.080, 1.894), (0.066 * sx, -0.040, 1.888), 0.0110, 0.0090), 0.014)   # zygomatic arch
        S.add(Capsule((0.012 * sx, -0.087, 1.945), (0.048 * sx, -0.076, 1.947), 0.0095, 0.0078), 0.014)  # heavy brow ridge
    # ------------------------------------------------ upper jaw: a rounded muzzle under the nose
    S.add(Ellipsoid((0, -0.090, 1.861), (0.045, 0.026, 0.019)), 0.022)
    for sx in (1, -1):   # cheek flesh filling the mouth corner down to the top of the jaw
        S.add(Ellipsoid((0.050 * sx, -0.046, 1.850), (0.011, 0.020, 0.026)), 0.022)
    # ------------------------------------------------ lower jaw: tapering wedge from the mouth corners to a pointed chin
    for s, rx, ry, rr in sample(LOWER_FACE, 0.003):
        S.add(Slab(jaw_pt(s, 0, ry), rx, ry, 0.0020, min(rx, ry) * 0.75, JAW_R), 0.010)
    # throat skin from under the jaw to the neck, hollow cheeks
    S.add(Capsule((0, -0.030, 1.800), (0, -0.040, 1.690), 0.020, 0.014), 0.022)
    for sx in (1, -1):
        S.sub(Ellipsoid((0.052 * sx, -0.092, 1.862), (0.014, 0.016, 0.020)), 0.012)
    # ------------------------------------------------ the mouth: deep cavity open across the jaw's whole front, + throat
    for s, rx, depth in sample(CAVITY, 0.0025):
        half = (depth + CAV_OUT) / 2
        S.sub(Slab(jaw_pt(s, 0, half - CAV_OUT), rx, half, 0.0016, 0.008, JAW_R), 0.004)
    S.sub(Ellipsoid((0, -0.078, 1.826), (0.046, 0.036, 0.014)), 0.006)                 # under the upper jaw (palate)
    S.sub(Capsule((0, -0.060, 1.828), (0, -0.016, 1.770), 0.020, 0.014), 0.010)       # throat
    for sx in (1, -1):
        S.add(Chain([(p[0] * sx * 0.97, p[1] + 0.003, p[2] + 0.001) for p in UPPER_ARCH], [0.0058] * len(UPPER_ARCH)), 0.007)   # upper gum
        rim = rim_points(sx)
        S.add(Chain(rim, [0.0060] * len(rim)), 0.005)                                       # lower gum ridges
    # ------------------------------------------------ eyes: deep socket, lid mass over the eyeball, almond slit
    for sx in (1, -1):
        ec = np.array(mirror(EYE_C, sx))
        S.sub(Ellipsoid(ec + (0, -0.020, -0.001), (0.021, 0.020, 0.016)), 0.008)            # socket
        S.add(Ball(ec, EYE_R + 0.0034), 0.006)                                            # lids
        S.add(Capsule(ec + (-0.013 * sx, -0.010, 0.008), ec + (0.014 * sx, -0.007, 0.006), 0.0042), 0.005)   # heavy upper lid fold
        S.add(Capsule(ec + (-0.011 * sx, -0.008, -0.010), ec + (0.012 * sx, -0.006, -0.011), 0.0045), 0.006)  # eye bag
        S.sub(Ball(ec, EYE_R + 0.0004))                                                   # eyeball room
        S.sub(Ellipsoid(ec + (0.001 * sx, -0.015, -0.0008), (0.0115, 0.012, 0.0050), rot(0, -10 * sx, 0)), 0.0012)   # opening
    # ------------------------------------------------ nose: thin bridge, bony hump, drooping pointed tip
    S.add(Capsule((0, -0.086, 1.940), (0, -0.115, 1.882), 0.0075, 0.0090), 0.012)
    S.add(Ball((0, -0.100, 1.917), 0.0070), 0.006)
    S.add(Ball(NOSE_TIP, 0.0074), 0.007)
    S.add(Ball((0, -0.118, 1.866), 0.0066), 0.005)
    for sx in (1, -1):
        S.add(Ball((0.0115 * sx, -0.106, 1.866), 0.0068), 0.008)                           # alae
        S.sub(Ellipsoid((0.0062 * sx, -0.115, 1.8615), (0.0036, 0.0055, 0.0026)), 0.0015)  # nostrils
        S.sub(Capsule((0.020 * sx, -0.106, 1.868), (0.040 * sx, -0.090, 1.836), 0.0022), 0.003)   # nasolabial crease
    # ------------------------------------------------ collarbones, starved chest, ribs (shows in the robe's V)
    for sx in (1, -1):
        S.add(Capsule((0.016 * sx, -0.036, 1.588), (0.120 * sx, -0.020, 1.598), 0.0075), 0.016)
    S.add(Ellipsoid((0, 0.008, 1.470), (0.122, 0.078, 0.110)), 0.02)
    S.add(Capsule((0, -0.066, 1.575), (0, -0.068, 1.385), 0.0090), 0.006)
    for k in range(5):
        z = 1.545 - 0.040 * k
        for sx in (1, -1):
            S.add(Capsule((0.018 * sx, -0.070, z), (0.100 * sx, -0.054, z - 0.030), 0.0065), 0.008)
            S.add(Capsule((0.100 * sx, -0.054, z - 0.030), (0.135 * sx, -0.004, z - 0.055), 0.0062), 0.008)
    return S


# ---------------------------------------------------------------- preview: clay render of the WIP head vs the concept angles
def clay(ob, col=(0.42, 0.34, 0.27, 1), name="M_Clay"):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = [n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"][0]
    b.inputs["Base Color"].default_value = col
    b.inputs["Roughness"].default_value = 0.55
    ob.data.materials.clear()
    ob.data.materials.append(m)


def pivot():
    p = bpy.data.objects.get("FacePivot")
    if p is None:
        p = bpy.data.objects.new("FacePivot", None)
        bpy.context.scene.collection.objects.link(p)
        p.location = (0, 0, 1.80)
    return p


def attach(ob):
    p = pivot()
    rot_save = p.rotation_euler.copy()
    p.rotation_euler = (0, 0, 0)
    bpy.context.view_layer.update()
    ob.parent = p
    ob.matrix_parent_inverse = p.matrix_world.inverted()
    p.rotation_euler = rot_save


def lights_for(cam_loc, target, side=1):
    """key light up and to one side of the camera, weak fill on the other side, rim from behind"""
    for n in ("FaceKey", "FaceFill", "FaceRim"):
        o = bpy.data.objects.get(n)
        if o is None:
            ld = bpy.data.lights.new(n, "AREA")
            o = bpy.data.objects.new(n, ld)
            bpy.context.scene.collection.objects.link(o)
    cam_loc, target = Vector(cam_loc), Vector(target)
    fwd = (target - cam_loc).normalized()
    right = fwd.cross(Vector((0, 0, 1))).normalized()
    up = right.cross(fwd).normalized()
    setups = {"FaceKey": (cam_loc + right * 0.45 * side + up * 0.55 - fwd * 0.05, 14.0, 0.5),
              "FaceFill": (cam_loc - right * 0.6 * side + up * 0.1, 2.5, 0.8),
              "FaceRim": (target + fwd * 0.5 + up * 0.4 - right * 0.3 * side, 6.0, 0.4)}
    for n, (loc, energy, size) in setups.items():
        o = bpy.data.objects[n]
        o.location = loc
        o.rotation_euler = (target - loc).to_track_quat("-Z", "Y").to_euler()
        o.data.energy = energy
        o.data.size = size
        o.data.color = (1.0, 0.92, 0.82) if n == "FaceKey" else (0.8, 0.85, 1.0)


FACE_VIEWS = {
    # name: (pivot euler deg, camera, target, key side)
    "front": ((-24, -12, 28), (-0.10, -1.00, 1.66), (0.0, -0.08, 1.82), -1),
    "side": ((-22, 0, 0), (-1.05, -0.14, 1.80), (0.0, -0.10, 1.80), 1),
    "straight": ((0, 0, 0), (0.0, -1.00, 1.84), (0.0, -0.05, 1.81), -1),
    "q34": ((0, 0, 0), (0.50, -0.86, 1.88), (0.0, -0.05, 1.81), -1),
    "profile": ((0, 0, 0), (-1.00, -0.06, 1.84), (0.0, -0.06, 1.81), 1),
    # head tilted back like the planned in-game posture, seen by a player standing in front (eye height 1.6 m)
    "game": ((-20, 0, 0), (0.25, -1.10, 1.60), (0.0, -0.08, 1.80), -1),
    "gameside": ((-20, 0, 0), (-1.10, -0.15, 1.75), (0.0, -0.10, 1.78), 1),
    "ufront": ((0, 0, 0), (0.12, -0.60, 1.90), (0.0, -0.05, 1.88), -1),
    "uside": ((0, 0, 0), (-0.55, -0.08, 1.89), (0.0, -0.08, 1.88), 1),
}


def face_snap(name, view="front", res=(560, 760), lens=60):
    sc = bpy.context.scene
    p = pivot()
    e, cam, tgt, side = FACE_VIEWS[view]
    p.rotation_euler = tuple(math.radians(a) for a in e)
    lights_for(cam, tgt, side)
    try:
        sc.render.engine = "BLENDER_EEVEE"
    except Exception:
        sc.render.engine = "BLENDER_EEVEE_NEXT"
    w = sc.world or bpy.data.worlds.new("World")
    sc.world = w
    w.use_nodes = True
    bg = w.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (0.035, 0.032, 0.03, 1)
    bg.inputs[1].default_value = 1.0
    return snap(name, cam, tgt, lens=lens, res=res, engine=sc.render.engine)


def only_visible(names):
    """hide every mesh/volume except the given ones in renders"""
    for o in bpy.data.objects:
        if o.type in ("MESH", "VOLUME", "META"):
            o.hide_render = o.name not in names


def shots(tag, views=("front", "side", "straight", "profile"), res=(420, 570)):
    return [face_snap("%s_%s" % (tag, v), v, res=res) for v in views]


if globals().get("BUILD_FACE", True):
    import time
    t0 = time.time()
    S = build_sdf(globals().get("FACE_VS", 0.0025))
    hr = S.to_mesh("Q_HeadHR")
    clay(hr)
    attach(hr)
    result = {"verts": len(hr.data.vertices), "sec": round(time.time() - t0, 1)}
