# The Questioner - group the parts into 4 meshes (one texture atlas each) and lay out their UVs.   Blender: exec(open(this).read())
#   Q_HeadMesh  = head + teeth + eyes                    -> M_Q_Head      (smart project)
#   Q_Limbs     = forearms/shins/feet + hands + sandals  -> M_Q_Limbs     (smart project)
#   Q_Clothes   = thobe + sleeves + pants                -> M_Q_Cloth     (parametric UVs packed into regions)
#   Q_Keffiyeh  = hood + tail                            -> M_Q_Keffiyeh  (parametric UVs, hood top / tail strip below)
import bpy, bmesh, math

O = bpy.data.objects


def mat(name, col):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = col
    return m


def ensure_uv(ob):
    if not ob.data.uv_layers:
        ob.data.uv_layers.new(name="UVMap")
    else:
        ob.data.uv_layers[0].name = "UVMap"


def remap_uv(ob, fn):
    uv = ob.data.uv_layers["UVMap"].data
    for l in uv:
        l.uv = fn(l.uv.x, l.uv.y)


def join(names, new_name, material):
    objs = [O[n] for n in names if n in O]
    for o in objs:
        ensure_uv(o)
        o.data.materials.clear()
        o.data.materials.append(material)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = new_name
    ob.data.name = new_name
    # one armature modifier is enough
    arms = [m for m in ob.modifiers if m.type == "ARMATURE"]
    for m in arms[1:]:
        ob.modifiers.remove(m)
    return ob


def smart_uv(ob, margin=0.006):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=margin, area_weight=0.0, scale_to_bounds=True)
    bpy.ops.object.mode_set(mode="OBJECT")


def run():
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    mh = mat("M_Q_Head", (0.55, 0.42, 0.33, 1))
    ml = mat("M_Q_Limbs", (0.55, 0.42, 0.33, 1))
    mc = mat("M_Q_Cloth", (0.72, 0.66, 0.55, 1))
    mk = mat("M_Q_Keffiyeh", (0.7, 0.25, 0.22, 1))
    # cloth atlas: thobe left 62 %, sleeves top right, pants bottom right (u around, v down the garment)
    remap_uv(O["Q_Thobe"], lambda u, v: (u * 0.615, 1 - v))
    remap_uv(O["Q_SleeveL"], lambda u, v: (0.625 + u * 0.18, 1 - v * 0.48))
    remap_uv(O["Q_SleeveR"], lambda u, v: (0.815 + u * 0.18, 1 - v * 0.48))
    remap_uv(O["Q_PantsL"], lambda u, v: (0.625 + u * 0.18, 0.49 - v * 0.48))
    remap_uv(O["Q_PantsR"], lambda u, v: (0.815 + u * 0.18, 0.49 - v * 0.48))
    # keffiyeh atlas: hood in the top 72 %, the tail laid along x in the bottom strip
    V2 = globals().get("V2", False)
    if not V2:
        remap_uv(O["Q_KeffiyehHood"], lambda u, v: (u, 0.28 + v * 0.72))
        remap_uv(O["Q_KeffiyehTail"], lambda u, v: (1 - v, u * 0.26))
    head = join(["Q_Head", "Q_Teeth", "Q_Eyes"], "Q_HeadMesh", mh)
    limbs = join(["Q_Body", "Q_HandL", "Q_HandR", "Q_SandalL", "Q_SandalR"], "Q_Limbs", ml)
    cloth = join(["Q_Thobe", "Q_SleeveL", "Q_SleeveR", "Q_PantsL", "Q_PantsR"], "Q_Clothes", mc)
    keff = join(["Q_KeffiyehHood", "Q_KeffiyehTail"], "Q_Keffiyeh", mk)
    if not V2:
        smart_uv(head)              # v2: the head / teeth / eyes UVs are laid out (and baked) by q18
    smart_uv(limbs)
    return {o.name: (len(o.data.polygons), len(o.vertex_groups)) for o in (head, limbs, cloth, keff)}

if globals().get("RUN_UV", True):
    result = run()
