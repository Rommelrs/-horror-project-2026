# Second half of the toilet pipeline (run in Blender after blender_build_toilet.py): UV1 unwrap, Cycles ambient-occlusion bake to
# Assets/Textures/ParkToilet/Toilet_AO.png, FBX export to Assets/Models/ParkToilet/ParkToilet.fbx, and a .blend copy.
import bpy, math, os, time
PROJ = "D:/Unity Stuff/NEWHorrorProject/-horror-project-2026/"
SIZE = 2048
SAMPLES = 48
t0 = time.time()
sc = bpy.context.scene
toilet = bpy.data.objects["ParkToilet"]
me = toilet.data

# ---- UV1: smart unwrap, equal texel density, packed
if "UVAO" not in me.uv_layers: me.uv_layers.new(name="UVAO")
me.uv_layers.active = me.uv_layers["UVAO"]
for o in bpy.context.selected_objects: o.select_set(False)
toilet.select_set(True); bpy.context.view_layer.objects.active = toilet
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.002, correct_aspect=True, scale_to_bounds=False)
bpy.ops.uv.average_islands_scale()
bpy.ops.uv.pack_islands(margin=0.002)
bpy.ops.object.mode_set(mode='OBJECT')
me.uv_layers["UVAO"].active_render = True        # the bake reads the active-render UV map
t_uv = time.time() - t0

# ---- AO image + an active image node in every material
if "ParkToilet_AO" in bpy.data.images: bpy.data.images.remove(bpy.data.images["ParkToilet_AO"])
ao = bpy.data.images.new("ParkToilet_AO", SIZE, SIZE, alpha=False); ao.colorspace_settings.name = 'Non-Color'
for m in me.materials:
    nt = m.node_tree
    for n in [n for n in nt.nodes if n.name == "AO_bake"]: nt.nodes.remove(n)
    n = nt.nodes.new("ShaderNodeTexImage"); n.name = "AO_bake"; n.image = ao; n.select = True; nt.nodes.active = n

# ---- bake
sc.render.engine = 'CYCLES'
sc.cycles.device = 'CPU'; sc.cycles.samples = SAMPLES; sc.cycles.use_denoising = False
if sc.world is None: sc.world = bpy.data.worlds.new("W")
try: sc.world.light_settings.distance = 1.0
except Exception as e: print("ao distance:", e)
sc.render.bake.margin = 6
bpy.ops.object.bake(type='AO', use_clear=True)
t_bake = time.time() - t0

ao_path = PROJ + "Assets/Textures/ParkToilet/Toilet_AO.png"
ao.filepath_raw = ao_path; ao.file_format = 'PNG'; ao.save()

# ---- restore preview settings, export
sc.render.engine = 'BLENDER_EEVEE'
os.makedirs(PROJ + "Assets/Models/ParkToilet", exist_ok=True)
for o in bpy.context.selected_objects: o.select_set(False)
toilet.select_set(True); bpy.context.view_layer.objects.active = toilet
bpy.ops.export_scene.fbx(filepath=PROJ + "Assets/Models/ParkToilet/ParkToilet.fbx", use_selection=True, object_types={'MESH'},
    axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True,
    mesh_smooth_type='FACE', use_mesh_modifiers=True, path_mode='STRIP')
bpy.ops.wm.save_as_mainfile(filepath=PROJ + "AnimationTools/ParkToilet.blend", copy=True)
result = {"uv_seconds": round(t_uv, 1), "bake_done_seconds": round(t_bake, 1), "total": round(time.time() - t0, 1), "ao": ao_path, "ao_mean": sum(ao.pixels[::4][:20000]) / 20000}
