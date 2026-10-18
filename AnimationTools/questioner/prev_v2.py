# preview material on the v2 low-res head/teeth/eyes (albedo + tangent normal map) and a debug sheet. exec in Blender.
import bpy
BUILD_FACE = False
exec(open(r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\AnimationTools\questioner\q14_face.py", encoding="utf8").read())
m = bpy.data.materials.get("M_V2_HeadPrev") or bpy.data.materials.new("M_V2_HeadPrev")
m.use_nodes = True
nt = m.node_tree
nt.nodes.clear()
out = nt.nodes.new("ShaderNodeOutputMaterial")
b = nt.nodes.new("ShaderNodeBsdfPrincipled")
ta = nt.nodes.new("ShaderNodeTexImage"); ta.image = bpy.data.images["T_Questioner_Head"]
tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = bpy.data.images["T_Questioner_Head_N"]
nm = nt.nodes.new("ShaderNodeNormalMap")
nt.links.new(ta.outputs[0], b.inputs["Base Color"])
nt.links.new(tn.outputs[0], nm.inputs["Color"])
nt.links.new(b.outputs[0], out.inputs[0])
b.inputs["Roughness"].default_value = 0.7
b.inputs["Specular IOR Level"].default_value = 0.25
for n in ("Q_Head", "Q_Teeth", "Q_Eyes"):
    o = bpy.data.objects[n]
    o.data.materials.clear()
    o.data.materials.append(m)
    attach(o)
for n in ("Q_KeffiyehHood",):
    if n in bpy.data.objects:
        attach(bpy.data.objects[n])
TAGP = globals().get("TAGP", "dbg")
only_visible(["Q_Head", "Q_Teeth", "Q_Eyes"])
face_snap(TAGP + "_alb", "ufront", res=(500, 500))
nt.links.new(nm.outputs[0], b.inputs["Normal"])
face_snap(TAGP + "_both", "ufront", res=(500, 500))
face_snap(TAGP + "_both34", "q34", res=(500, 600))
for n in ("Q_Head", "Q_Teeth", "Q_Eyes", "Q_KeffiyehHood"):
    o = bpy.data.objects.get(n)
    if o and o.parent:
        mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
bpy.data.objects["FacePivot"].rotation_euler = (0, 0, 0)
