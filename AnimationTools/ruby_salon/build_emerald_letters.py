# 3D channel letters for the Emerald Salon storefront.  Blender: exec(open(this).read())
# Positions use the reference photo's pixel grid (same as BuildRubySalon.cs.txt): x_m = (px - CX) * S, z_m = (GY - py) * S,
# front faces -Y (= Unity +Z, out of the sign).  Outputs:
#   Assets/Models/RubySalon/EmeraldLetters.fbx       slot 0 = lit face, slot 1 = returns (sides)
#   AnimationTools/ruby_salon/letters_mask.json      front-face triangles in photo px, used by make_ruby_textures.py
#                                                    to paint the glow halo on the sign panel exactly behind the letters
import bpy, bmesh, os, json

HERE = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\AnimationTools\ruby_salon"
OUT = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\Assets\Models\RubySalon"
FONTS = r"C:\Windows\Fonts"
S, CX, GY = 0.0135, 130.0, 377.0
Z_SIGN = 0.22          # sign face (Unity local z)
DEPTH = 0.045          # letter depth
GAP = 0.004

# صالون الزمرد in logical order, hand-shaped with Arabic Presentation Forms-B, reversed into visual order
AR = "".join(reversed(["\uFEBB", "\uFE8E", "\uFEDF", "\uFEEE", "\uFEE5", " ", "\uFE8D", "\uFEDF", "\uFEB0", "\uFEE3", "\uFEAE", "\uFEA9"]))
LINES = [
    # name, text, font, photo-px box (x0, x1, y0, y1), vertical stretch (the long English name is squeezed like real channel letters)
    ("EmeraldLetters_Arabic", AR, "tahomabd.ttf", (50, 206, 93, 123), 1.0),
    ("EmeraldLetters_English", "EMERALD SALON", "ARLRDBD.TTF", (44, 212, 127, 157), 1.45),
]

for o in [o for o in bpy.data.objects if o.name.startswith("EmeraldLetters")]:
    bpy.data.objects.remove(o, do_unlink=True)

def mat(name, col):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = col
    return m
M_FACE = mat("M_EmeraldLetters_Face", (1, 1, 0.95, 1))
M_SIDE = mat("M_EmeraldLetters_Side", (0.1, 0.3, 0.2, 1))

def bounds(ob):
    dg = bpy.context.evaluated_depsgraph_get()
    me = ob.evaluated_get(dg).to_mesh()
    xs = [(ob.matrix_world @ v.co).x for v in me.vertices]
    zs = [(ob.matrix_world @ v.co).z for v in me.vertices]
    ob.evaluated_get(dg).to_mesh_clear()
    return min(xs), max(xs), min(zs), max(zs)

objs = []
for name, text, font, (x0, x1, y0, y1), stretch in LINES:
    cu = bpy.data.curves.new(name, "FONT")
    cu.body = text
    cu.font = bpy.data.fonts.load(os.path.join(FONTS, font), check_existing=True)
    cu.align_x = "CENTER"
    cu.resolution_u = 5
    ob = bpy.data.objects.new(name, cu)
    bpy.context.scene.collection.objects.link(ob)
    ob.rotation_euler = (1.5707963, 0, 0)
    bpy.context.view_layer.update()
    bx0, bx1, bz0, bz1 = bounds(ob)
    tw, th = (x1 - x0) * S, (y1 - y0) * S
    k = min(tw / (bx1 - bx0), th / ((bz1 - bz0) * stretch))
    ob.scale = (k, k * stretch, k)          # local Y = height after the 90 deg X rotation
    cu.extrude = DEPTH / 2 / k
    bpy.context.view_layer.update()
    bx0, bx1, bz0, bz1 = bounds(ob)
    cx_m = ((x0 + x1) / 2 - CX) * S
    cz_m = (GY - (y0 + y1) / 2) * S
    ob.location = (ob.location.x + cx_m - (bx0 + bx1) / 2, -(Z_SIGN + GAP + DEPTH / 2), ob.location.z + cz_m - (bz0 + bz1) / 2)
    bpy.context.view_layer.update()
    # to mesh, bake transforms
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.convert(target="MESH")
    ob = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    me = ob.data
    me.materials.clear(); me.materials.append(M_FACE); me.materials.append(M_SIDE)
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.normal.y > 0.9], context="FACES")     # back faces sit on the panel
    for f in bm.faces:
        f.material_index = 0 if f.normal.y < -0.9 else 1
        f.smooth = False
    bm.to_mesh(me); bm.free()
    objs.append(ob)

# ---- front-face triangles in photo px for the texture halo
tris = []
for ob in objs:
    me = ob.data
    me.calc_loop_triangles()
    for lt in me.loop_triangles:
        if lt.material_index != 0:
            continue
        pts = []
        for vi in lt.vertices:
            w = ob.matrix_world @ me.vertices[vi].co
            pts.append([w.x / S + CX, GY - w.z / S])
        tris.append(pts)
with open(os.path.join(HERE, "letters_mask.json"), "w") as f:
    json.dump(tris, f)

bpy.ops.object.select_all(action="DESELECT")
for ob in objs: ob.select_set(True)
bpy.context.view_layer.objects.active = objs[0]
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "EmeraldLetters.fbx"), use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                         bake_space_transform=True, object_types={"MESH"}, axis_forward="-Z", axis_up="Y")
result = {"tris": len(tris), "objs": [(o.name, len(o.data.vertices), [round(v, 3) for v in o.dimensions]) for o in objs]}
