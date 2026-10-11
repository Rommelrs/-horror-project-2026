# Builds the INTERIOR of the park public toilet (men's side) in Blender (run inside Blender with exec(open(path).read())).
# Own scene ("ToiletInterior") so the exterior model in the session is untouched. Low-poly PSX style: boxes, 8-12 sided cylinders, no bevels.
# Blender axes: the entrance wall is at -Y, the room runs toward +Y, X is the viewer's right when facing +Y, Z up, origin on the floor in the
# middle of the room. (FBX export turns that into Unity: x = -x, y = z, z = -y, i.e. the entrance wall is at Unity +Z.)
# Room: 3.6 m wide (x -1.8..1.8), 5.2 m deep (y -2.6..2.6), 2.8 m high. Back wall: three stalls (squat / western / closed door).
# Left wall (-X): sink, mirror, light, soap box, bin. Entrance door in the front wall at x 0.7..1.6.
# Output: one joined object "ParkToiletInterior" (UV0 = see assign_uv, UV1 made by the bake script) and COLS = list of box colliders.
import bpy, bmesh, math, random, json
from mathutils import Vector, Matrix

PROJ = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/"
random.seed(5)
PFX = "ParkToiletInt_"

# ------------------------------------------------------------------ own scene, clean only our own datablocks
sc = bpy.data.scenes.get("ToiletInterior")
if sc is None: sc = bpy.data.scenes.new("ToiletInterior")
for o in list(sc.objects): bpy.data.objects.remove(o, do_unlink=True)
for m in [m for m in bpy.data.materials if m.name.startswith(PFX)]: bpy.data.materials.remove(m)
for m in [m for m in bpy.data.meshes if m.name.startswith("TI_")]: bpy.data.meshes.remove(m)
bpy.context.window.scene = sc
scene = sc

# name: fallback colour (textures are assigned in Unity)
MATS = {"Plaster": (0.75, 0.68, 0.56), "Dado": (0.38, 0.43, 0.46), "Floor": (0.45, 0.41, 0.36), "Ceiling": (0.35, 0.32, 0.28),
        "Partition": (0.38, 0.43, 0.46), "Porcelain": (0.8, 0.77, 0.68), "Rust": (0.40, 0.33, 0.25), "Pipe": (0.42, 0.42, 0.40),
        "Metal": (0.39, 0.42, 0.40), "Mirror": (0.55, 0.57, 0.55), "Sand": (0.62, 0.52, 0.38), "Paper": (0.82, 0.8, 0.72),
        "Dark": (0.02, 0.02, 0.02), "Water": (0.2, 0.22, 0.2), "Tube": (1.0, 0.95, 0.8), "Door": (0.39, 0.42, 0.40)}
mats = {}
for n, c in MATS.items():
    m = bpy.data.materials.new(PFX + n); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*c, 1)
    mats[n] = m
# UV rule for wall pieces: (width of one texture in metres, z where the piece's texture starts, height of the texture in metres)
WALLUV = {"Dado": (2.6, 0.0, 1.3), "Plaster": (3.0, 1.3, 1.5)}
TPM = {"Floor": 0.6, "Ceiling": 0.5, "Porcelain": 1.5, "Rust": 2.0, "Pipe": 2.0, "Metal": 1.2, "Sand": 1.0, "Paper": 1.0}

parts = []          # (object, uv mode, material, post-matrix)
COLS = []           # (centre, size) in Blender coordinates

def link(o): scene.collection.objects.link(o); return o

def mesh_obj(name, bm, mat, uv="box", post=None):
    me = bpy.data.meshes.new("TI_" + name); bm.to_mesh(me); bm.free()
    o = link(bpy.data.objects.new(name, me)); me.materials.append(mats[mat]); parts.append((o, uv, mat, post)); return o

def cube_bm(center, size, rot=None):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts: v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    M = Matrix.Translation(center)
    if rot is not None: M = M @ rot
    bm.transform(M); return bm

def box(name, center, size, mat, uv="box", col=False, post=None, rot=None):
    center = Vector(center)
    if col: COLS.append((center.copy(), tuple(size)))
    return mesh_obj(name, cube_bm(center, size, rot), mat, uv, post)

def cyl(name, center, radius, depth, mat, segs=8, axis='Z', scale_xy=(1, 1), col=False, r2=None):
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=radius, radius2=radius if r2 is None else r2, depth=depth)
    for v in bm.verts: v.co.x *= scale_xy[0]; v.co.y *= scale_xy[1]
    R = {'Z': Matrix.Identity(4), 'X': Matrix.Rotation(math.radians(90), 4, 'Y'), 'Y': Matrix.Rotation(math.radians(90), 4, 'X')}[axis]
    bm.transform(Matrix.Translation(center) @ R)
    if col:
        ext = {'Z': (2 * radius * scale_xy[0], 2 * radius * scale_xy[1], depth), 'X': (depth, 2 * radius, 2 * radius), 'Y': (2 * radius, depth, 2 * radius)}[axis]
        COLS.append((Vector(center), ext))
    return mesh_obj(name, bm, mat, "box")

def seg(name, p0, p1, w, mat, col=False):
    """Thin square rod between two points."""
    p0, p1 = Vector(p0), Vector(p1); d = p1 - p0; L = d.length
    R = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
    return mesh_obj(name, cube_bm((p0 + p1) / 2, (w, w, L), R), mat, "box")

# ------------------------------------------------------------------ dimensions
HX, HY, H = 1.8, 2.6, 2.8      # inner half width, half depth, height
T = 0.2                         # wall thickness
SEAM = 1.3                      # painted dado up to here
DX0, DX1, DH = 0.7, 1.6, 2.05   # entrance opening

# ------------------------------------------------------------------ shell: floor, ceiling, walls (dado + plaster halves)
box("Floor", (0, 0, -0.1), (2 * HX + 2 * T, 2 * HY + 2 * T, 0.2), "Floor", col=True)
box("Ceiling", (0, 0, H + 0.1), (2 * HX + 2 * T, 2 * HY + 2 * T, 0.2), "Ceiling", col=True)

def wall(name, x0, x1, y0, y1, z0, z1, col=True):
    """Wall block split at the dado seam into a painted lower and a plaster upper part."""
    for suffix, a, b, mat in (("D", z0, min(z1, SEAM), "Dado"), ("P", max(z0, SEAM), z1, "Plaster")):
        if b - a > 1e-4:
            box(name + suffix, ((x0 + x1) / 2, (y0 + y1) / 2, (a + b) / 2), (x1 - x0, y1 - y0, b - a), mat, uv="wall")
    if col: COLS.append((Vector(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)), (x1 - x0, y1 - y0, z1 - z0)))

wall("WallBack", -HX - T, HX + T, HY, HY + T, 0, H)
wall("WallLeft", -HX - T, -HX, -HY - T, HY, 0, H)
wall("WallRight", HX, HX + T, -HY - T, HY, 0, H)
wall("WallFrontL", -HX - T, DX0, -HY - T, -HY, 0, H)
wall("WallFrontR", DX1, HX + T, -HY - T, -HY, 0, H)
wall("WallFrontTop", DX0, DX1, -HY - T, -HY, DH, H, col=False)
COLS.append((Vector(((DX0 + DX1) / 2, -HY - T / 2, (DH + H) / 2)), (DX1 - DX0, T, H - DH)))

# entrance door: steel door recessed in the opening, jambs, handle
box("EntryDoor", ((DX0 + DX1) / 2, -HY - 0.12, DH / 2), (DX1 - DX0 - 0.04, 0.05, DH - 0.02), "Door", uv="norm", col=True)
for sx in (DX0 + 0.01, DX1 - 0.01): box("Jamb", (sx, -HY - 0.09, DH / 2), (0.04, 0.1, DH), "Rust")
box("JambTop", ((DX0 + DX1) / 2, -HY - 0.09, DH - 0.01), (DX1 - DX0, 0.1, 0.05), "Rust")
box("EntryHandlePlate", (DX0 + 0.14, -HY - 0.085, 1.0), (0.05, 0.012, 0.22), "Rust")
box("EntryHandle", (DX0 + 0.14, -HY - 0.07, 1.0), (0.025, 0.04, 0.12), "Pipe")
box("EntryKick", ((DX0 + DX1) / 2, -HY - 0.093, 0.14), (0.78, 0.01, 0.2), "Rust")        # dented kick plate

# ------------------------------------------------------------------ light fixtures (ceiling tubes + one over the mirror)
for i, y in enumerate((-0.9, 1.4)):
    box("TubeHousing%d" % i, (0.1, y, H - 0.045), (0.2, 1.3, 0.09), "Metal")
    box("Tube%d" % i, (0.1, y, H - 0.1), (0.07, 1.2, 0.035), "Tube")
    for dy in (-0.62, 0.62): box("TubeCap", (0.1, y + dy, H - 0.1), (0.09, 0.03, 0.05), "Rust")
box("MirrorLightHousing", (-HX + 0.07, -0.5, 2.22), (0.12, 0.56, 0.07), "Metal")
box("MirrorLightTube", (-HX + 0.14, -0.5, 2.22), (0.04, 0.5, 0.035), "Tube")

# ------------------------------------------------------------------ sink wall: mirror, basin, tap, pipes, soap box, bin
SY = -0.5
box("MirrorFrame", (-HX + 0.02, SY, 1.55), (0.035, 0.78, 0.98), "Rust")
box("MirrorGlass", (-HX + 0.045, SY, 1.55), (0.012, 0.70, 0.90), "Mirror", uv="norm")
# hollow basin: floor + four rim walls (so you can look into it), small dark plug hole
BX, BZ0, BZ1 = -HX + 0.27, 0.68, 0.85
box("BasinFloor", (BX, SY, BZ0 + 0.03), (0.5, 0.68, 0.06), "Porcelain", col=True)
box("BasinRimF", (BX + 0.225, SY, (BZ0 + BZ1) / 2), (0.05, 0.68, BZ1 - BZ0), "Porcelain")
box("BasinRimB", (BX - 0.225, SY, (BZ0 + BZ1) / 2), (0.05, 0.68, BZ1 - BZ0), "Porcelain")
box("BasinRimL", (BX, SY - 0.315, (BZ0 + BZ1) / 2), (0.4, 0.05, BZ1 - BZ0), "Porcelain")
box("BasinRimR", (BX, SY + 0.315, (BZ0 + BZ1) / 2), (0.4, 0.05, BZ1 - BZ0), "Porcelain")
cyl("BasinPlug", (BX + 0.02, SY, BZ0 + 0.062), 0.025, 0.004, "Dark")
box("BasinBack", (-HX + 0.03, SY, 0.92), (0.05, 0.68, 0.14), "Porcelain")
cyl("TapBody", (-HX + 0.1, SY, 0.93), 0.016, 0.1, "Pipe")
cyl("TapSpout", (-HX + 0.17, SY, 0.985), 0.013, 0.14, "Pipe", axis='X')
for dy in (-0.11, 0.11):
    cyl("TapHandle", (-HX + 0.1, SY + dy, 0.9), 0.012, 0.07, "Pipe", axis='Y')
    box("TapKnob", (-HX + 0.1, SY + dy * 1.4, 0.9), (0.03, 0.025, 0.03), "Rust")
cyl("TrapDown", (-HX + 0.18, SY, 0.6), 0.022, 0.26, "Pipe")
cyl("TrapBend", (-HX + 0.1, SY, 0.45), 0.022, 0.2, "Pipe", axis='X')
cyl("TrapUp", (-HX + 0.18, SY, 0.4), 0.022, 0.1, "Pipe")
cyl("TrapWall", (-HX + 0.04, SY, 0.45), 0.024, 0.08, "Rust", axis='X')
for dy in (-0.22, 0.22):
    seg("BasinBrace", (-HX + 0.03, SY + dy, 0.5), (-HX + 0.38, SY + dy, 0.68), 0.03, "Rust")
cyl("SupplyPipe", (-HX + 0.05, SY + 0.28, 0.5), 0.014, 0.9, "Pipe")
box("SoapBox", (-HX + 0.06, -1.3, 1.28), (0.12, 0.2, 0.28), "Metal")
box("SoapBoxFace", (-HX + 0.125, -1.3, 1.33), (0.015, 0.14, 0.12), "Dark")
cyl("Bin", (-1.45, -1.85, 0.25), 0.17, 0.5, "Metal", segs=8, r2=0.2, col=True)
cyl("BinInside", (-1.45, -1.85, 0.505), 0.16, 0.01, "Dark", segs=8)
for i in range(4):
    box("BinPaper", (-1.45 + random.uniform(-0.1, 0.1), -1.85 + random.uniform(-0.1, 0.1), 0.52 + random.uniform(0, 0.05)), (0.1, 0.09, 0.06), "Paper",
        rot=Matrix.Rotation(random.uniform(0, 3), 4, 'Z') @ Matrix.Rotation(random.uniform(-0.5, 0.5), 4, 'X'))

# ------------------------------------------------------------------ stalls (back wall)
PX = (-0.6, 0.6)                 # divider positions
YF = 1.0                         # stall front line
ZB, ZT = 0.2, 1.95               # panel bottom (raised off the floor) / top
for i, x in enumerate(PX):
    box("Divider%d" % i, (x, (YF + HY) / 2, (ZB + ZT) / 2), (0.04, HY - YF, ZT - ZB), "Partition", uv="norm", col=True)
    box("Post%d" % i, (x, YF, ZT / 2), (0.06, 0.06, ZT), "Rust")
    box("PostFoot%d" % i, (x, YF, 0.03), (0.1, 0.1, 0.06), "Rust")
    cyl("RailSide%d" % i, (x, (YF + HY) / 2, ZT + 0.02), 0.014, HY - YF, "Pipe", axis='Y')
cyl("RailFront", (0, YF, ZT + 0.02), 0.014, 2 * HX, "Pipe", axis='X')
for x in (-HX + 0.04, HX - 0.04): box("RailBracket", (x, YF, ZT + 0.01), (0.07, 0.07, 0.07), "Rust")
# stall partitions' door panels (hinged; the post matrix swings the door about its hinge after UV assignment)
def stall_door(name, hinge_x, side, angle_deg, closed):
    w, h, th = 1.0, 1.55, 0.035
    cx = side * w / 2                      # local: hinge at origin, door extends along `side` (+1 / -1)
    zc = ZB + 0.12 + h / 2
    post = Matrix.Translation((hinge_x, YF, 0)) @ Matrix.Rotation(math.radians(angle_deg) * -side, 4, 'Z')
    box(name, (cx, 0, zc), (w, th, h), "Partition", uv="norm", post=post)
    for k in range(4):        # louvre slots, low on the door
        box(name + "Slot", (cx, -th / 2 - 0.002, ZB + 0.12 + 0.12 + k * 0.045), (0.5, 0.006, 0.022), "Dark", post=post)
    for hz in (zc + 0.5, zc - 0.5): box(name + "Hinge", (side * 0.02, 0, hz), (0.05, 0.05, 0.1), "Rust", post=post)
    box(name + "Latch", (side * (w - 0.1), -th / 2 - 0.015, zc + 0.05), (0.09, 0.025, 0.04), "Rust", post=post)
    if closed:
        COLS.append((Vector((hinge_x + cx, YF, zc)), (w, 0.1, h)))
# stall 1 open ~80 deg (hinged at the left wall), stall 2 open ~38 deg (hinged on the right divider), stall 3 closed
stall_door("DoorStall1", -HX + 0.09, +1, 80, False)
stall_door("DoorStall2", PX[1] - 0.03, -1, 38, False)
stall_door("DoorStall3", PX[1] + 0.03, +1, 0, True)

# ---- stall 1: squat toilet + hand shower (shattaf)
SX = -1.2
box("SquatPan", (SX, 2.0, 0.035), (0.52, 0.82, 0.07), "Porcelain", col=True)
box("SquatHole", (SX, 2.22, 0.072), (0.17, 0.3, 0.012), "Water")
for dx in (-0.14, 0.14): box("SquatPad", (SX + dx, 1.72, 0.08), (0.15, 0.24, 0.02), "Porcelain")
box("SquatRim", (SX, 2.38, 0.085), (0.44, 0.06, 0.03), "Porcelain")
cyl("SquatPipe", (SX + 0.36, 2.52, 0.4), 0.02, 0.8, "Pipe")
cyl("SquatTap", (SX + 0.36, 2.5, 0.78), 0.016, 0.1, "Pipe", axis='Y')
box("ShowerHolder1", (SX + 0.36, 2.54, 0.88), (0.05, 0.06, 0.1), "Rust")
for k in range(9):        # hanging hose
    a = k / 8.0; p0 = Vector((SX + 0.36 - 0.1 * math.sin(a * 3.0), 2.5 - 0.06 * a, 0.86 - 0.5 * a ** 0.7))
    a2 = (k + 1) / 8.0; p1 = Vector((SX + 0.36 - 0.1 * math.sin(a2 * 3.0), 2.5 - 0.06 * a2, 0.86 - 0.5 * a2 ** 0.7))
    if k < 8: seg("Hose1", p0, p1, 0.014, "Pipe")

# ---- stall 2: western toilet + tank + paper roll + hand shower
WX = 0.0
cyl("BowlBase", (WX, 2.28, 0.14), 0.11, 0.28, "Porcelain", segs=8, scale_xy=(1.0, 1.3), col=True)
cyl("Bowl", (WX, 2.18, 0.32), 0.17, 0.16, "Porcelain", segs=10, scale_xy=(1.0, 1.25), r2=0.2)
cyl("Seat", (WX, 2.15, 0.42), 0.205, 0.025, "Porcelain", segs=10, scale_xy=(1.0, 1.28))
cyl("SeatHole", (WX, 2.15, 0.434), 0.12, 0.004, "Water", segs=10, scale_xy=(1.0, 1.2))
box("Tank", (WX, 2.46, 0.72), (0.4, 0.17, 0.42), "Porcelain", col=True)
box("TankLid", (WX, 2.46, 0.94), (0.42, 0.19, 0.03), "Porcelain")
cyl("FlushButton", (WX + 0.1, 2.46, 0.968), 0.02, 0.015, "Pipe")
cyl("FlushPipe", (WX, 2.46, 0.38), 0.025, 0.28, "Pipe")
box("PaperHolder", (PX[1] - 0.05, 2.3, 0.72), (0.04, 0.12, 0.05), "Rust")
cyl("PaperRoll", (PX[1] - 0.1, 2.3, 0.77), 0.065, 0.11, "Paper", axis='X', segs=8)
cyl("SquatTap2", (PX[0] + 0.1, 2.5, 0.62), 0.016, 0.1, "Pipe", axis='Y')
box("ShowerHolder2", (PX[0] + 0.1, 2.54, 0.76), (0.05, 0.06, 0.1), "Rust")
for k in range(8):
    a = k / 7.0; a2 = (k + 1) / 7.0
    f = lambda t: Vector((PX[0] + 0.1 + 0.08 * math.sin(t * 3.2), 2.48 - 0.05 * t, 0.74 - 0.45 * t ** 0.7))
    if k < 7: seg("Hose2", f(a), f(a2), 0.014, "Pipe")

# ---- stall 3 (closed): a bit of the squat pan can't be seen; nothing inside needs modelling

# ------------------------------------------------------------------ floor drain with grate
DXc, DYc = 0.45, -0.35
box("DrainFrame", (DXc, DYc, 0.006), (0.34, 0.34, 0.012), "Rust")
box("DrainHole", (DXc, DYc, 0.0125), (0.28, 0.28, 0.003), "Dark")
for i in range(-3, 4):
    box("GrateA", (DXc + i * 0.04, DYc, 0.0165), (0.012, 0.28, 0.012), "Pipe")
    box("GrateB", (DXc, DYc + i * 0.04, 0.0165), (0.28, 0.012, 0.012), "Pipe")

# ------------------------------------------------------------------ ventilation blocks (back wall, one per stall) with diamond lattice
def vent(name, center, normal_sign, axis='Y'):
    hw, hz = 0.24, 0.12
    cx, cy, cz = center
    box(name + "Frame", (cx, cy - normal_sign * 0.02, cz), (2 * hw + 0.08, 0.05, 2 * hz + 0.08), "Plaster")
    box(name + "Hole", (cx, cy - normal_sign * 0.045, cz), (2 * hw, 0.01, 2 * hz), "Dark")
    for sgn in (1, -1):
        for k in range(-7, 8):
            d = k * 0.07
            # line: z = sgn*(x - d) clipped to the opening
            x_lo = max(-hw, min(d - hz, d + hz)); x_hi = min(hw, max(d - hz, d + hz))
            if x_hi - x_lo < 0.03: continue
            p0 = Vector((cx + x_lo, cy - normal_sign * 0.05, cz + sgn * (x_lo - d)))
            p1 = Vector((cx + x_hi, cy - normal_sign * 0.05, cz + sgn * (x_hi - d)))
            seg(name + "Bar", p0, p1, 0.016, "Rust")
for i, x in enumerate((-1.2, 0.0, 1.2)):
    vent("Vent%d" % i, (x, HY, 2.28), 1)
    box("VentSill%d" % i, (x, HY - 0.035, 2.12), (0.6, 0.07, 0.025), "Plaster")

# ------------------------------------------------------------------ pipes along the walls
cyl("PipeVert", (-HX + 0.05, HY - 0.06, H / 2), 0.03, H, "Pipe", col=False)
for z in (0.35, 1.25, 2.2): box("PipeClampV", (-HX + 0.05, HY - 0.06, z), (0.09, 0.09, 0.05), "Rust")
cyl("PipeHoriz", (0, HY - 0.06, 2.62), 0.028, 2 * HX - 0.1, "Pipe", axis='X')
for x in (-1.2, -0.2, 0.9, 1.6): box("PipeClampH", (x, HY - 0.06, 2.62), (0.05, 0.09, 0.09), "Rust")
cyl("PipeCeil", (HX - 0.05, 0, H - 0.07), 0.025, 2 * HY - 0.2, "Pipe", axis='Y')
for y in (-1.8, -0.4, 1.0, 2.2): box("PipeClampC", (HX - 0.05, y, H - 0.07), (0.09, 0.05, 0.05), "Rust")

# ------------------------------------------------------------------ litter, sand drifts, wet mess
for i in range(16):
    x = random.uniform(-1.6, 1.6); y = random.uniform(-2.3, 2.3)
    s = random.uniform(0.05, 0.13)
    box("Litter", (x, y, 0.004), (s, s * random.uniform(0.6, 1.2), 0.006), "Paper", rot=Matrix.Rotation(random.uniform(0, 6.28), 4, 'Z'))
for i in range(4):
    x = random.choice((-1.55, 1.55, -0.9, 0.2)) + random.uniform(-0.1, 0.1); y = random.uniform(-2.3, 0.3)
    box("PaperBall", (x, y, 0.03), (0.06, 0.06, 0.05), "Paper", rot=Matrix.Rotation(random.uniform(0, 6.28), 4, 'Z') @ Matrix.Rotation(0.5, 4, 'X'))
for x, y, r in ((-1.55, -2.35, 0.32), (1.55, 2.35, 0.3), (1.15, -2.2, 0.45), (-1.6, 1.0, 0.28), (0.2, 2.45, 0.22)):
    cyl("SandPile", (x, y, 0.012), r, 0.024, "Sand", segs=7, r2=r * 0.55)

# ------------------------------------------------------------------ UV0 --------------------------------------------------------
def assign_uv(o, mode, mat):
    me = o.data
    if "UVMain" not in me.uv_layers: me.uv_layers.new(name="UVMain")
    bm = bmesh.new(); bm.from_mesh(me); uvl = bm.loops.layers.uv["UVMain"]
    co = [v.co for v in bm.verts]
    mn = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
    mx = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
    ext = mx - mn
    tpm = TPM.get(mat, 1.0)
    for f in bm.faces:
        ax = max(range(3), key=lambda i: abs(f.normal[i]))
        for l in f.loops:
            p = l.vert.co
            if mode == "wall":
                W_, z0, hh = WALLUV[mat]
                pos = p.y if ax == 0 else p.x
                u, v = pos / W_, (p.z - z0) / hh
            elif mode == "norm":
                thin = min(range(3), key=lambda i: ext[i])
                if thin == 2: u, v = (p.x - mn.x) / max(ext.x, 1e-6), (p.y - mn.y) / max(ext.y, 1e-6)
                elif thin == 1: u, v = (p.x - mn.x) / max(ext.x, 1e-6), (p.z - mn.z) / max(ext.z, 1e-6)
                else: u, v = (p.y - mn.y) / max(ext.y, 1e-6), (p.z - mn.z) / max(ext.z, 1e-6)
            else:
                u, v = {0: (p.y, p.z), 1: (p.x, p.z), 2: (p.x, p.y)}[ax]; u *= tpm; v *= tpm
            l[uvl].uv = (u, v)
    bm.to_mesh(me); bm.free()

empty = [o.name for o, _, _, _ in parts if len(o.data.vertices) == 0]
if empty: raise RuntimeError("empty parts: %s" % empty)
for o, mode, mat, post in parts:
    bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(o.data); bm.free()
    assign_uv(o, mode, mat)
    if post is not None: o.data.transform(post)       # swing doors etc. after their UVs were laid out flat

# ------------------------------------------------------------------ join
for o in bpy.context.selected_objects: o.select_set(False)
for o, _, _, _ in parts: o.select_set(True)
bpy.context.view_layer.objects.active = parts[0][0]
bpy.ops.object.join()
room = bpy.context.view_layer.objects.active; room.name = "ParkToiletInterior"; room.data.name = "TI_ParkToiletInterior"
# one material slot per material, in a stable order
bpy.ops.object.select_all(action='DESELECT'); room.select_set(True)

# colliders, in Unity coordinates (x = -x, y = z, z = -y)
cols = [{"c": [-c.x, c.z, -c.y], "s": [s[0], s[2], s[1]]} for c, s in COLS]
open(PROJ + "AnimationTools/interior_colliders.json", "w").write(json.dumps(cols))
result = {"verts": len(room.data.vertices), "faces": len(room.data.polygons), "materials": [m.name for m in room.data.materials],
          "colliders": len(cols)}
