# The Questioner v2 - full rebuild in one call (Blender): exec(open(this).read())
#   opens TheQuestioner_v2_parts.blend (v1 body/clothes + rig + old Main armature with the source clips), then
#   q14 SDF head -> q15 teeth/eyes -> q16 wrinkles + paint -> q17 cloth-sim hood -> q18 game head + bakes ->
#   q09 skin -> q10 UV/groups -> q11 hood texture -> q12 retarget + FBX.  Saves _v2_groups.blend and _v2.blend.
# Options (set before exec): SKIP_HOOD=True to reuse the hood in the parts file, FACE_VS (default 0.0015).
import bpy, time

H = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\AnimationTools\questioner"
T = {}
t0 = time.time()
# (open TheQuestioner_v2_parts.blend in a separate call first - operators fail right after a load in the same script)
assert bpy.data.filepath.endswith("TheQuestioner_v2_parts.blend"), bpy.data.filepath


def run(name, g=None):
    g = {} if g is None else g
    exec(open(H + "\\" + name, encoding="utf8").read(), g)
    return g


g = {"FACE_VS": globals().get("FACE_VS", 0.0015)}
run("q14_face.py", g)                      # builds Q_HeadHR
run("q15_teeth_eyes.py", g)                # Q_Teeth2, Q_Eyes2 (+ TOOTH_TV)
run("q16_detail.py", g)                    # wrinkles, paint, gloss, AO on Q_HeadHR
T["face"] = round(time.time() - t0)
if not globals().get("SKIP_HOOD", False) or "Q_Hood2" not in bpy.data.objects:
    run("q17_hood.py", g)
T["hood"] = round(time.time() - t0)
run("q18_v2_build.py", g)                  # Q_Head / Q_Teeth / Q_Eyes / Q_KeffiyehHood + head textures
T["bake"] = round(time.time() - t0)
bpy.data.objects["FacePivot"].rotation_euler = (0, 0, 0)
bpy.ops.wm.save_as_mainfile(filepath=H + r"\TheQuestioner_v2_parts.blend", copy=True)
g9 = {k: g[k] for k in ("JAW_O", "JAW_A", "JAW_F", "LOWER_FACE")}
run("q09_skin.py", g9)
run("q10_uv.py", {"V2": True})
for n in ("Q_HeadMesh", "Q_Limbs", "Q_Clothes", "Q_Keffiyeh"):
    o = bpy.data.objects[n]
    o.hide_render = False
for n in ("Q_HeadHR", "Q_Hood2", "QuestionerRig_Sticks", "SM_Questioner_body.001"):
    o = bpy.data.objects.get(n)
    if o:
        o.hide_render = True
bpy.ops.wm.save_as_mainfile(filepath=H + r"\TheQuestioner_v2_groups.blend", copy=True)
run("q11_tex.py", {"V2": True, "V2_ALL": globals().get("V2_ALL", True)})
T["tex"] = round(time.time() - t0)
g12 = run("q12_export.py", {"HEAD_TILT": globals().get("HEAD_TILT", (-30, 0, 0))})
bpy.ops.wm.save_as_mainfile(filepath=H + r"\TheQuestioner_v2.blend", copy=True)
T["total"] = round(time.time() - t0)
result = {"times": T, "tris": g12["result"]["tris"], "mats": g12["result"]["mats"]}
