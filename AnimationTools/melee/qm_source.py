# exec'd by melee_anims_build.py. A QuickMagic video-mocap take (3ds Max Biped skeleton "Bip001 ...", T-pose on frame 1) as
# a Src on a Mixamo skeleton, so the rest of the build (blend / with_arms / export -> retarget onto Khalid) treats it like
# any Mixamo clip. Same rule as mocap_retarget: every Mixamo bone gets the WORLD rotation change its Biped bone has from
# the T-pose (both rests are T-poses; arms / legs get a small rest correction lining the segment directions up); joints
# follow by forward kinematics, the hips travel scaled by the leg-length ratio. The take is turned (yaw) so he faces
# forward (-Y) at `face_frame`.
QM_DIR = "D:/Unity Stuff/Shamkha Refs/"
QM_MAP = {"Hips": "Pelvis", "Spine": "Spine", "Spine1": "Spine1", "Spine2": "Spine2", "Neck": "Neck", "Head": "Head"}
for _S, _s in (("Left", "L"), ("Right", "R")):
    QM_MAP.update({_S + "Shoulder": _s + " Clavicle", _S + "Arm": _s + " UpperArm", _S + "ForeArm": _s + " Forearm",
                   _S + "Hand": _s + " Hand", _S + "UpLeg": _s + " Thigh", _S + "Leg": _s + " Calf", _S + "Foot": _s + " Foot",
                   _S + "ToeBase": _s + " Toe0"})
    for _k, _f in (("Thumb", 0), ("Index", 1), ("Middle", 2), ("Ring", 3), ("Pinky", 4)):
        for _j, _suf in ((1, ""), (2, "1"), (3, "2")):
            QM_MAP["%sHand%s%d" % (_S, _k, _j)] = "%s Finger%d%s" % (_s, _f, _suf)
# segment direction (bone -> child) used for the rest correction; only limbs, like mocap_retarget
QM_CORRECT = {}
for _S in ("Left", "Right"):
    QM_CORRECT.update({_S + "Arm": _S + "ForeArm", _S + "ForeArm": _S + "Hand", _S + "UpLeg": _S + "Leg", _S + "Leg": _S + "Foot"})


def _binomial(vals, lerp):
    """5-tap binomial smoothing over a list (ends clamped): a running weighted mean, mix(a, b, t) blends two values"""
    out, n = [], len(vals)
    for i in range(n):
        acc, wsum = None, 0.0
        for off, w in ((-2, 1), (-1, 4), (0, 6), (1, 4), (2, 1)):
            v = vals[max(0, min(n - 1, i + off))]
            wsum += w
            acc = v if acc is None else lerp(acc, v, w / wsum)
        out.append(acc)
    return out


class QMSrc(Src):
    """face_frames: (a, b) = average facing over those frames is 'forward'; strip: hips stay over one floor spot;
    smooth: passes of 5-tap binomial smoothing (video-tracking noise). Also keeps, per frame, both fists relative to
    her neck in her chest's own axes (self.hand[f] = (right, left), metres) for the position-based hand retarget."""
    def __init__(self, fbx, f0, f1, face_frames, strip=False, smooth=1, skeleton="Torch_Idle"):
        sc = bpy.context.scene
        # the Mixamo skeleton (rest pose only)
        bpy.ops.import_scene.fbx(filepath=TOOLS + "mixamo/" + skeleton + ".fbx")
        self.arm = [o for o in bpy.context.selected_objects if o.type == 'ARMATURE'][0]
        for o in bpy.context.selected_objects: o.hide_render = True
        self.arm.name = "SRC_QM_" + fbx.split(".")[0][:20]
        self.arm.animation_data.action = None
        mx = self.arm
        # the take
        bpy.ops.import_scene.fbx(filepath=QM_DIR + fbx)
        bip = [o for o in bpy.context.selected_objects if o.type == 'ARMATURE'][0]
        for o in bpy.context.selected_objects: o.hide_render = True     # (not hide_viewport: that stops it evaluating)
        B = "Bip001 "
        bb = bip.data.bones
        us = bip.scale.x
        # her rest T-pose turned to face -Y (each QuickMagic file stores its rest turned differently)
        lr = bb[B + "L Thigh"].head_local - bb[B + "R Thigh"].head_local
        rf = lr.cross(Vector((0, 0, 1)))
        Rref = Matrix.Rotation(math.atan2(-1.0, 0.0) - math.atan2(rf.y, rf.x), 3, 'Z')
        Rb0 = {n: Rref @ bb[B + n].matrix_local.to_3x3().normalized() for n in QM_MAP.values()}
        def rest_pt(n): return Rref @ (bb[B + n].head_local * us)
        def bpt(n): return bip.matrix_world @ bip.pose.bones[B + n].head
        lb = (rest_pt("L Calf") - rest_pt("L Thigh")).length + (rest_pt("L Foot") - rest_pt("L Calf")).length
        mw = mx.matrix_world.copy(); mwi = mw.inverted()
        Ro = mw.to_3x3().normalized(); Roi = Ro.inverted()
        X = "mixamorig:"
        bones = mx.data.bones
        rest = {b.name: b.matrix_local.copy() for b in bones}
        def mrest_w(n): return mw @ bones[X + n].head_local
        lm = (mrest_w("LeftLeg") - mrest_w("LeftUpLeg")).length + (mrest_w("LeftFoot") - mrest_w("LeftLeg")).length
        scale = lm / lb
        hips_rest_w = mrest_w("Hips")
        self.shoulder_w = (rest_pt("L UpperArm") - rest_pt("R UpperArm")).length
        # rest corrections (world): Mixamo segment direction -> Biped segment direction, both in their T-poses
        C = {}
        for m, child in QM_CORRECT.items():
            dm = (mrest_w(child) - mrest_w(m)).normalized()
            db = (rest_pt(QM_MAP[child]) - rest_pt(QM_MAP[m])).normalized()
            C[m] = dm.rotation_difference(db).to_matrix()
        for S in ("Left", "Right"):                      # hand + fingers keep their rest angle to the forearm
            for m in QM_MAP:
                if m.startswith(S + "Hand"): C[m] = C[S + "ForeArm"]
        # forward = her average facing over face_frames -> -Y
        fsum = Vector((0, 0, 0)); zs = []
        for f in range(face_frames[0], face_frames[1] + 1):
            sc.frame_set(f)
            v = (bpt("L Thigh") - bpt("R Thigh")).cross(Vector((0, 0, 1))); v.z = 0; fsum += v.normalized()
        for f in range(f0, f1 + 1):
            sc.frame_set(f); zs.append(bpt("Pelvis").z)
        zs.sort(); stand_z = zs[len(zs) // 2]
        yaw = math.atan2(-1.0, 0.0) - math.atan2(fsum.y, fsum.x)
        Y = Matrix.Rotation(yaw, 3, 'Z')
        sc.frame_set(face_frames[0])
        pivot = bpt("Pelvis").copy(); pivot.z = 0
        pel0 = Vector((pivot.x, pivot.y, stand_z))
        def yawed(v): return Y @ (v - pivot) + pivot
        order = sorted((b.name for b in bones), key=lambda n: depth(mx, n))
        self.f0, self.f1 = f0, f1
        self.poses = {}
        self.hand = {}
        for f in range(f0, f1 + 1):
            sc.frame_set(f)
            bw = bip.matrix_world.copy()
            p = {}
            for n in order:
                short = n[len(X):]
                par = bones[n].parent
                if short in QM_MAP:
                    Rt = (bw @ bip.pose.bones[B + QM_MAP[short]].matrix).to_3x3().normalized()
                    dq = Y @ Rt @ Rb0[QM_MAP[short]].inverted()
                    Rw = dq @ C.get(short, Matrix.Identity(3)) @ Ro @ rest[n].to_3x3().normalized()
                    Ra = Roi @ Rw
                elif par is not None:                    # end bones ride their parent
                    Ra = p[par.name].to_3x3().normalized() @ (rest[par.name].inverted() @ rest[n]).to_3x3().normalized()
                else:
                    Ra = rest[n].to_3x3().normalized()
                if par is None:
                    d = Y @ (bpt("Pelvis") - pel0)
                    if strip: d.x = d.y = 0.0
                    head = mwi @ (hips_rest_w + scale * d)
                else:
                    head = (p[par.name] @ (rest[par.name].inverted() @ rest[n])).translation
                p[n] = Matrix.Translation(head) @ Ra.to_4x4()
            self.poses[f] = p
            # fists (palm centre) relative to her neck, in her chest's axes
            Rc = Y @ (bw @ bip.pose.bones[B + "Spine2"].matrix).to_3x3().normalized() @ Rb0["Spine2"].inverted()
            neck = yawed(bpt("Neck"))
            fist = {s: yawed((bpt(s + " Hand") + bpt(s + " Finger2")) * 0.5) for s in ("R", "L")}
            self.hand[f] = tuple(Rc.inverted() @ (fist[s] - neck) for s in ("R", "L"))
        frames = list(range(f0, f1 + 1))
        for _ in range(smooth):
            # local rotations (and the hips' position), then the hand data
            locs = [to_local(mx, self.poses[f]) for f in frames]
            sm = {}
            for n in order:
                seq = [l[n] for l in locs]
                def mix(a, b, t):
                    la, lb_ = a.decompose(), b.decompose()
                    qb = lb_[1] if la[1].dot(lb_[1]) >= 0 else -lb_[1]
                    return Matrix.LocRotScale(la[0].lerp(lb_[0], t), la[1].slerp(qb, t), la[2].lerp(lb_[2], t))
                sm[n] = _binomial(seq, mix)
            for i, f in enumerate(frames):
                self.poses[f] = from_local(mx, {n: sm[n][i] for n in order}, order)
            hs = _binomial([self.hand[f] for f in frames], lambda a, b, t: (a[0].lerp(b[0], t), a[1].lerp(b[1], t)))
            for i, f in enumerate(frames): self.hand[f] = hs[i]
        print("QM_SRC", fbx, "frames", f0, f1, "yaw %.1f deg" % math.degrees(yaw), "hip scale %.3f" % scale,
              "shoulders %.3f" % self.shoulder_w, flush=True)
