# Low-poly PS2-style wall-top bushes for the Alley 1 "real look" (like the bougainvillea / trees spilling over the walls
# in the user's photos): crossed leaf cards in a mound on the wall top plus cards hanging down the alley side of the wall.
# Each card shows a whole leaf-clump texture (AlleyLeaves.png / AlleyBougainvillea.png, alpha tested, drawn both sides).
# Local frame (Unity, after export): origin on the wall's top edge, +Y up, +Z out over the alley, X along the wall.
# Exec inside Blender (own scene "AlleyBushes"); exports Assets/Models/AlleyBushes/AlleyBushes.fbx. Deterministic seed.
import bpy, bmesh, math, random, os
from mathutils import Vector, Matrix

ROOT = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026"
OUT = ROOT + "/Assets/Models/AlleyBushes/AlleyBushes.fbx"

scene = bpy.data.scenes.get("AlleyBushes") or bpy.data.scenes.new("AlleyBushes")
bpy.context.window.scene = scene
for o in list(scene.objects): bpy.data.objects.remove(o, do_unlink=True)


# atlas regions (make_alley_textures.py): two round clumps in the left column, the hanging drape in the right one
CLUMPS = [(0.0, 0.5, 0.5, 1.0), (0.0, 0.0, 0.5, 0.5)]
DRAPE = (0.5, 0.0, 1.0, 1.0)


def card(bm, uvl, center, width, height, yaw, pitch, roll=0.0, region=(0, 0, 1, 1)):
    # a quad centred on `center`, facing +Y (Blender) before rotation; Blender Z is up
    rot = Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Rotation(pitch, 4, 'X') @ Matrix.Rotation(roll, 4, 'Y')
    hw, hh = width / 2, height / 2
    corners = [Vector((-hw, 0, -hh)), Vector((hw, 0, -hh)), Vector((hw, 0, hh)), Vector((-hw, 0, hh))]
    verts = [bm.verts.new(center + rot @ c) for c in corners]
    f = bm.faces.new(verts)
    u0, v0, u1, v1 = region
    for loop, uv in zip(f.loops, [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]):
        loop[uvl].uv = uv
    return f


def bush(name, seed, length):
    # Blender axes here: X along the wall, -Y out over the alley (becomes Unity +Z on export), Z up
    rnd = random.Random(seed)
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new("UVMap")
    # the mound on the wall top: many small ragged clumps at mixed angles (some lying over the top, some upright,
    # some leaning out over the alley), so it has volume from every side
    n = int(10 + length * 7)
    for i in range(n):
        x = (rnd.random() - 0.5) * length
        # sunk into the wall-top edge (no gap under it), mostly on the alley side
        c = Vector((x, rnd.uniform(-0.4, 0.15), rnd.uniform(-0.15, 0.45)))
        s = rnd.uniform(0.55, 0.95)
        pitch = rnd.choice([rnd.uniform(-0.4, 0.4), rnd.uniform(1.0, 1.4)])
        card(bm, uvl, c, s, s, rnd.uniform(0, math.pi), pitch, rnd.uniform(-0.3, 0.3), rnd.choice(CLUMPS))
    # spilling down the alley side: a few drapes of different lengths just off the wall face, their tops tucked under
    # the clumps, plus clumps where they start so no card edge shows
    m = int(1 + length * 1.2)
    for i in range(m):
        x = (rnd.random() - 0.5) * length * 0.8
        drop = rnd.uniform(1.0, 1.8)
        w = rnd.uniform(0.45, 0.75)
        c = Vector((x, -rnd.uniform(0.06, 0.16), 0.2 - drop / 2))
        card(bm, uvl, c, w, drop, rnd.uniform(-0.3, 0.3), rnd.uniform(-0.15, 0.05), rnd.uniform(-0.15, 0.15), DRAPE)
        cs = rnd.uniform(0.45, 0.65)
        card(bm, uvl, Vector((x, -0.22, 0.12)), cs, cs, rnd.uniform(-0.4, 0.4), rnd.uniform(-0.3, 0.1), 0.0, rnd.choice(CLUMPS))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me)
    scene.collection.objects.link(ob)
    return ob


made = [bush("WallBush_A", 3, 1.6), bush("WallBush_B", 7, 2.4), bush("WallBush_C", 12, 1.1)]
for i, ob in enumerate(made): ob.location.x = i * 4  # apart in the file; Unity uses each mesh at its own origin

os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.object.select_all(action='DESELECT')
for ob in made: ob.select_set(True)
bpy.context.view_layer.objects.active = made[0]
for ob in made: ob.location.x = 0
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False, use_mesh_modifiers=True)
result = {"exported": OUT, "objects": [(o.name, len(o.data.polygons)) for o in made]}
