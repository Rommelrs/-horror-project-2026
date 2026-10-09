import bpy, json, math
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=r"D:/Unity Stuff/Shamkha Refs/Running.fbx", automatic_bone_orientation=False)
sc = bpy.context.scene
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
act = bpy.data.actions[0]
arm.animation_data.action = act
names = ["Hips","Spine","Spine1","Spine2","Neck","Head","LeftShoulder","LeftArm","LeftForeArm","LeftHand","RightShoulder","RightArm","RightForeArm","RightHand","LeftUpLeg","LeftLeg","LeftFoot","LeftToeBase","LeftToe_End","RightUpLeg","RightLeg","RightFoot","RightToeBase","RightToe_End"]
mw = arm.matrix_world
out = {'rot_euler': [round(x, 4) for x in arm.matrix_world.to_euler()], 'scale': list(arm.matrix_world.to_scale()), 'frames': {}}
f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
for f in range(f0, f1 + 1):
    sc.frame_set(f)
    d = {}
    for n in names:
        pb = arm.pose.bones["mixamorig:" + n]
        h = mw @ pb.head; t = mw @ pb.tail
        d[n] = [[round(c, 5) for c in h], [round(c, 5) for c in t]]
    out['frames'][f] = d
# rest heads
out['rest'] = {n: [round(c, 5) for c in (mw @ arm.pose.bones["mixamorig:" + n].bone.head_local)] for n in names}
out['action_range'] = [f0, f1]
print("JSON_START"); print(json.dumps(out)); print("JSON_END")
