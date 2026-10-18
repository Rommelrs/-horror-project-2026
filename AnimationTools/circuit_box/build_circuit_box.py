# Wall circuit box (hinged door, breakers inside, red beacon on top, conduit) + its key.   Blender: exec(open(this).read())
# Front faces -Y (= Unity +Z).  Back plate on y = 0, box protrudes to -Y.  Blender +X = viewer's right when looking at the front.
# Exports (slot names = materials, remapped to Unity materials by name):
#   Assets/Models/CircuitBox/CircuitBox.fbx      CB_Body, CB_Interior, CB_Door (origin = hinge, hinge on the viewer's LEFT),
#                                                CB_Lamp (beacon base), CB_Dome (beacon lens, origin = lens centre), CB_Conduit
#   Assets/Models/CircuitBox/CircuitBoxKey.fbx   CircuitBoxKey  (lies flat: length along X, thickness along Z, bow toward -X)
import bpy, bmesh, os, math
from mathutils import Vector, Matrix

OUT = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\Assets\Models\CircuitBox"
os.makedirs(OUT, exist_ok=True)

W, H, D, T = 0.46, 0.62, 0.17, 0.012

for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
for c in list(bpy.data.curves):
    bpy.data.curves.remove(c)
for m in list(bpy.data.meshes):
    bpy.data.meshes.remove(m)

MATS = {}
def mat(name, col):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = col
    MATS[name] = m
    return m
for n, c in [("M_CB_Paint", (0.62, 0.63, 0.6, 1)), ("M_CB_Plate", (0.6, 0.62, 0.63, 1)), ("M_CB_Dark", (0.05, 0.05, 0.055, 1)),
             ("M_CB_Chrome", (0.75, 0.76, 0.78, 1)), ("M_CB_Labels", (1, 1, 1, 1)), ("M_CB_Breaker", (0.82, 0.83, 0.82, 1)),
             ("M_CB_Red", (0.75, 0.08, 0.06, 1)), ("M_CB_Yellow", (0.9, 0.72, 0.1, 1)), ("M_CB_Brass", (0.7, 0.5, 0.2, 1)),
             ("M_CB_Conduit", (0.35, 0.37, 0.38, 1)), ("M_CB_Lamp", (1, 0.05, 0.03, 1)),
             ("M_CBKey_Red", (0.7, 0.05, 0.05, 1)), ("M_CBKey_Steel", (0.78, 0.78, 0.8, 1)), ("M_CBKey_Tag", (1, 1, 1, 1))]:
    mat(n, c)

class Part:
    """one mesh object built out of boxes / cylinders / quads / tori, each primitive tagged with a material"""
    def __init__(self, name, mats):
        self.name, self.mats = name, mats
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
    def _mi(self, m):
        return self.mats.index(m)
    def _tag(self, faces, m, smooth=False):
        for f in faces:
            f.material_index = self._mi(m)
            f.smooth = smooth
    def box(self, cx, cy, cz, sx, sy, sz, m, uvscale=1.0):
        r = bmesh.ops.create_cube(self.bm, size=1.0)
        vs = r["verts"]
        bmesh.ops.scale(self.bm, vec=(sx, sy, sz), verts=vs)
        bmesh.ops.translate(self.bm, vec=(cx, cy, cz), verts=vs)
        fs = {f for v in vs for f in v.link_faces}
        self._tag(fs, m)
        for f in fs:
            n = max(range(3), key=lambda i: abs(f.normal[i]))
            ax = [a for a in range(3) if a != n]
            for l in f.loops:
                l[self.uv].uv = (l.vert.co[ax[0]] * uvscale + 0.5, l.vert.co[ax[1]] * uvscale + 0.5)
    def cyl(self, cx, cy, cz, r, length, axis, m, seg=14, uvscale=1.0):
        """cylinder centred at c, along axis 'x' / 'y' / 'z'"""
        r_ = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=length)
        vs = r_["verts"]
        rot = {"z": Matrix.Identity(3), "x": Matrix.Rotation(math.pi / 2, 3, "Y"), "y": Matrix.Rotation(math.pi / 2, 3, "X")}[axis]
        bmesh.ops.rotate(self.bm, cent=(0, 0, 0), matrix=rot, verts=vs)
        bmesh.ops.translate(self.bm, vec=(cx, cy, cz), verts=vs)
        fs = {f for v in vs for f in v.link_faces}
        self._tag(fs, m)
        for f in fs:
            for l in f.loops:
                l[self.uv].uv = (l.vert.co.x * uvscale + l.vert.co.y * 0.3 + 0.5, l.vert.co.z * uvscale + l.vert.co.y * 0.3 + 0.5)
    def torus(self, cx, cy, cz, R, r, m, scale=(1, 1, 1), seg=24, rseg=8):
        """ring lying in XY, tube section r, major radius R (scaled per axis)"""
        grid = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            row = []
            for j in range(rseg):
                b = 2 * math.pi * j / rseg
                rr = R + r * math.cos(b)
                row.append(self.bm.verts.new((cx + rr * math.cos(a) * scale[0], cy + rr * math.sin(a) * scale[1], cz + r * math.sin(b) * scale[2])))
            grid.append(row)
        fs = []
        for i in range(seg):
            for j in range(rseg):
                f = self.bm.faces.new((grid[i][j], grid[(i + 1) % seg][j], grid[(i + 1) % seg][(j + 1) % rseg], grid[i][(j + 1) % rseg]))
                fs.append(f)
                for l in f.loops:
                    l[self.uv].uv = (0.5, 0.5)
        self._tag(fs, m, smooth=True)
    def dome(self, cx, cy, cz, r, m, seg=16, rings=6):
        """upper hemisphere, centre of the flat base at c"""
        grid = []
        for k in range(rings + 1):
            th = (math.pi / 2) * k / rings
            row = []
            for i in range(seg):
                ph = 2 * math.pi * i / seg
                row.append(self.bm.verts.new((cx + r * math.sin(th) * math.cos(ph), cy + r * math.sin(th) * math.sin(ph), cz + r * math.cos(th))))
            grid.append(row)
        fs = []
        for k in range(rings):
            for i in range(seg):
                i2 = (i + 1) % seg
                if k == 0:
                    f = self.bm.faces.new((grid[0][i], grid[1][i], grid[1][i2]))
                else:
                    f = self.bm.faces.new((grid[k][i], grid[k + 1][i], grid[k + 1][i2], grid[k][i2]))
                fs.append(f)
                for l in f.loops:
                    l[self.uv].uv = (0.5, 0.5)
        self._tag(fs, m, smooth=True)
    def quad(self, cx, y, cz, sx, sz, m, uv, inside=False):
        """vertical quad facing -Y (front) or, with inside=True, +Y; uv = (u0, v0, u1, v1) atlas rectangle, v up.
        Seen from the facing side the picture reads upright and un-mirrored."""
        x0, x1, z0, z1 = cx - sx / 2, cx + sx / 2, cz - sz / 2, cz + sz / 2
        if not inside:
            pts = [(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)]
        else:
            pts = [(x1, y, z0), (x0, y, z0), (x0, y, z1), (x1, y, z1)]
        us = [(uv[0], uv[1]), (uv[2], uv[1]), (uv[2], uv[3]), (uv[0], uv[3])]
        f = self.bm.faces.new([self.bm.verts.new(p) for p in pts])
        self._tag([f], m)
        for l, u in zip(f.loops, us):
            l[self.uv].uv = u
    def build(self, pivot=(0, 0, 0)):
        me = bpy.data.meshes.new(self.name)
        self.bm.normal_update()
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(MATS[m])
        ob = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(ob)
        me.transform(Matrix.Translation(-Vector(pivot)))
        ob.location = pivot
        return ob

M = "M_CB_"
# ---------------------------------------------------------------- body
body = Part("CB_Body", [M + "Paint", M + "Dark"])
body.box(0, -T / 2, 0, W, T, H, M + "Paint")                                   # back
body.box(-W / 2 + T / 2, -D / 2, 0, T, D, H, M + "Paint")                       # sides
body.box(W / 2 - T / 2, -D / 2, 0, T, D, H, M + "Paint")
body.box(0, -D / 2, H / 2 - T / 2, W - 2 * T, D, T, M + "Paint")                # top / bottom
body.box(0, -D / 2, -H / 2 + T / 2, W - 2 * T, D, T, M + "Paint")
body.box(0, -D + 0.004, H / 2 - 0.011, W, 0.008, 0.022, M + "Paint")            # front lip (inward flange)
body.box(0, -D + 0.004, -H / 2 + 0.011, W, 0.008, 0.022, M + "Paint")
body.box(-W / 2 + 0.011, -D + 0.004, 0, 0.022, 0.008, H, M + "Paint")
body.box(W / 2 - 0.011, -D + 0.004, 0, 0.022, 0.008, H, M + "Paint")
for z in (H / 2 + 0.02, -H / 2 - 0.02):                                         # mounting tabs + screws
    body.box(0, -T / 2, z, 0.12, T, 0.044, M + "Paint")
    body.cyl(0, -T - 0.003, z, 0.007, 0.006, "y", M + "Dark", 10)
body_ob = body.build()

# ---------------------------------------------------------------- interior
inn = Part("CB_Interior", [M + "Plate", M + "Chrome", M + "Breaker", M + "Dark", M + "Red", M + "Brass", M + "Yellow"])
Y0 = -0.0185                                                                      # front of the mounting plate
inn.box(0, -0.016, 0, W - 0.05, 0.004, H - 0.05, M + "Plate")
for z, count in [(0.19, 11), (0.06, 8)]:
    inn.box(0, Y0 - 0.002, z, W - 0.09, 0.006, 0.035, M + "Chrome")               # DIN rail
    x = -0.185
    for i in range(count):
        inn.box(x + 0.015, Y0 - 0.0305, z, 0.03, 0.047, 0.062, M + "Breaker")
        inn.box(x + 0.015, Y0 - 0.057, z + 0.012, 0.012, 0.012, 0.022, M + ("Red" if (z > 0.1 and i in (3, 7)) else "Dark"))
        inn.box(x + 0.015, Y0 - 0.055, z - 0.016, 0.022, 0.004, 0.008, M + "Dark")   # label strip
        x += 0.034
inn.box(0.165, Y0 - 0.034, 0.06, 0.06, 0.055, 0.085, M + "Breaker")               # main isolator (rotary handle) at the right of row 2
inn.box(0.165, Y0 - 0.066, 0.06, 0.04, 0.012, 0.04, M + "Yellow")
inn.box(0.165, Y0 - 0.074, 0.06, 0.012, 0.016, 0.034, M + "Red")
inn.box(-0.11, Y0 - 0.012, -0.06, 0.16, 0.014, 0.02, M + "Brass")                # terminal bar
for i in range(8):
    inn.box(-0.185 + i * 0.02, Y0 - 0.018, -0.06, 0.008, 0.012, 0.012, M + "Dark")
for i, c in enumerate(["Red", "Dark", "Yellow", "Dark", "Red", "Dark", "Yellow", "Dark", "Red"]):
    inn.cyl(-0.19 + i * 0.045, Y0 - 0.0105, 0.255, 0.0032, 0.1, "z", M + c, 6)    # wires up to the top
    inn.cyl(-0.19 + i * 0.045, Y0 - 0.0105, 0.125, 0.0032, 0.09, "z", M + c, 6)
inn.cyl(0.0, Y0 - 0.018, -0.115, 0.004, 0.04, "y", M + "Chrome", 8)               # key hook (nail + upturned tip)
inn.cyl(0.0, Y0 - 0.036, -0.108, 0.004, 0.016, "z", M + "Chrome", 8)
inn_ob = inn.build()

# ---------------------------------------------------------------- door (origin = hinge, viewer's left)
dw, dh = W + 0.018, H + 0.018                                                     # a little bigger than the box: its return lips wrap the outside
HX, HY = -dw / 2 - 0.002, -D - 0.006
door = Part("CB_Door", [M + "Paint", M + "Dark", M + "Chrome", M + "Labels"])
door.box(0, HY, 0, dw, 0.012, dh, M + "Paint")                                    # panel
door.box(0, HY - 0.008, 0, W - 0.06, 0.004, H - 0.06, M + "Paint")               # raised centre
for sx in (-1, 1):                                                                # return lips toward the wall
    door.box(sx * (dw / 2 - 0.004), HY + 0.021, 0, 0.008, 0.03, dh, M + "Paint")
for sz in (-1, 1):
    door.box(0, HY + 0.021, sz * (dh / 2 - 0.004), dw, 0.03, 0.008, M + "Paint")
FY = HY - 0.010                                                                   # front face of the raised centre
door.cyl(W / 2 - 0.085, FY - 0.004, 0.0, 0.027, 0.008, "y", M + "Dark", 18)       # lock escutcheon
door.cyl(W / 2 - 0.085, FY - 0.011, 0.0, 0.017, 0.012, "y", M + "Chrome", 16)    # lock barrel
door.box(W / 2 - 0.085, FY - 0.0175, 0.0, 0.003, 0.003, 0.016, M + "Dark")        # key slot
for hz in (0.2, 0.0, -0.2):                                                       # hinge knuckles
    door.cyl(HX, HY + 0.002, hz, 0.007, 0.06, "z", M + "Dark", 10)
door.quad(0.0, FY - 0.0007, 0.13, 0.16, 0.12, M + "Labels", (0.0, 0.625, 0.5, 1.0))         # DANGER sign   px [0,0,256,192]
door.quad(0.09, FY - 0.0007, -0.20, 0.10, 0.05, M + "Labels", (0.5, 0.75, 1.0, 1.0))       # ID plate      px [256,0,512,128]
door.quad(-0.09, FY - 0.0007, -0.20, 0.12, 0.06, M + "Labels", (0.5, 0.5, 1.0, 0.75))      # authorised    px [256,128,512,256]
door.quad(0.0, HY + 0.0062, 0.0, 0.2, 0.2, M + "Labels", (0.0, 0.125, 0.5, 0.625), inside=True)   # wiring diagram on the inside of the door
door_ob = door.build(pivot=(HX, HY, 0))

# ---------------------------------------------------------------- beacon lamp on top
BX, BY, BZ = 0.13, -D / 2, H / 2
lamp = Part("CB_Lamp", [M + "Dark", M + "Chrome"])
lamp.cyl(BX, BY, BZ + 0.007, 0.036, 0.014, "z", M + "Dark", 16)
lamp.cyl(BX, BY, BZ + 0.0165, 0.03, 0.005, "z", M + "Chrome", 16)
lamp_ob = lamp.build(pivot=(BX, BY, BZ))
dome = Part("CB_Dome", [M + "Lamp"])
dome.dome(BX, BY, BZ + 0.019, 0.029, M + "Lamp")
dome_ob = dome.build(pivot=(BX, BY, BZ + 0.019))

# ---------------------------------------------------------------- conduit (pipe up from the top and one down from the bottom, each bending into the wall)
con = Part("CB_Conduit", [M + "Conduit", M + "Dark"])
CR = 0.0125
def conduit(x, z0, z1):
    """vertical pipe from the box (z0) to z1, an elbow block and a short stub into the wall, two clamps"""
    zc, ln = (z0 + z1) / 2, abs(z1 - z0)
    con.cyl(x, -0.026, zc, CR, ln, "z", M + "Conduit", 12)
    con.cyl(x, -0.013, z1, CR, 0.026, "y", M + "Conduit", 12)                      # stub into the wall
    con.box(x, -0.026, z1, 0.034, 0.034, 0.034, M + "Conduit")                      # elbow block
    mid = (z0 + z1) / 2
    for cz in (mid, z0 + (z1 - z0) * 0.8):
        con.box(x, -0.0125, cz, 0.032, 0.025, 0.014, M + "Dark")                    # clamps against the wall
conduit(-0.12, H / 2 - 0.002, H / 2 + 0.5)
conduit(0.10, -H / 2 + 0.002, -H / 2 - 0.42)
con_ob = con.build()
con_slots = [s_.material.name for s_ in con_ob.material_slots]

# ---------------------------------------------------------------- export the box
bpy.ops.object.select_all(action="DESELECT")
box_objs = [body_ob, inn_ob, door_ob, lamp_ob, dome_ob, con_ob]
for o in box_objs:
    o.select_set(True)
bpy.context.view_layer.objects.active = body_ob
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "CircuitBox.fbx"), use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                         bake_space_transform=True, object_types={"MESH"}, axis_forward="-Z", axis_up="Y")
box_report = [(o.name, len(o.data.vertices), [round(v, 3) for v in o.dimensions]) for o in box_objs]

# ---------------------------------------------------------------- the key  (lies flat on XY, bow toward -X)
for o in box_objs:
    bpy.data.objects.remove(o, do_unlink=True)
KR, KS, KT = "M_CBKey_Red", "M_CBKey_Steel", "M_CBKey_Tag"
key = Part("CircuitBoxKey", [KR, KS, KT])
key.torus(0.006, 0, 0, 0.0185, 0.0058, KR, scale=(1.15, 0.85, 0.7))              # bow: oval red rubber ring
key.cyl(0.036, 0, 0, 0.0064, 0.012, "x", KS, 12)                                  # collar
key.cyl(0.062, 0, 0, 0.0046, 0.05, "x", KS, 10)                                   # shaft
key.box(0.1, 0, 0, 0.026, 0.0045, 0.01, KS)                                       # bit (three teeth)
for i in range(3):
    key.box(0.095 + i * 0.011, 0.0, 0.0, 0.006, 0.012, 0.0045, KS)
key.torus(-0.026, 0, 0, 0.0115, 0.0013, KS, scale=(1, 1, 1), seg=20, rseg=6)      # split ring
# yellow tag hanging off the ring, lying flat (top face carries the texture)
tx0, tx1, ty0, ty1, tz = -0.085, -0.042, -0.017, 0.017, 0.0
tv = [key.bm.verts.new(p) for p in [(tx0, ty0, tz + 0.0017), (tx1, ty0, tz + 0.0017), (tx1, ty1, tz + 0.0017), (tx0, ty1, tz + 0.0017),
                                    (tx0, ty0, tz - 0.0017), (tx1, ty0, tz - 0.0017), (tx1, ty1, tz - 0.0017), (tx0, ty1, tz - 0.0017)]]
faces = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
for idx in faces:
    f = key.bm.faces.new([tv[i] for i in idx])
    key._tag([f], KT)
    for l in f.loops:
        l[key.uv].uv = ((l.vert.co.x - tx0) / (tx1 - tx0), (l.vert.co.y - ty0) / (ty1 - ty0))
key_ob = key.build()
bpy.ops.object.select_all(action="DESELECT")
key_ob.select_set(True)
bpy.context.view_layer.objects.active = key_ob
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "CircuitBoxKey.fbx"), use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                         bake_space_transform=True, object_types={"MESH"}, axis_forward="-Z", axis_up="Y")
result = {"box": box_report, "conduit_slots": con_slots, "key": (len(key_ob.data.vertices), [round(v, 3) for v in key_ob.dimensions])}
