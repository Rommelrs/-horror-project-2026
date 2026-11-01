# Two-handed pipe grip baked into Khalid's clips (exec'd by melee_anims_build.py after mocap_retarget.py).
#
# Per frame, after the mocap is retargeted onto Khalid's segments (pose_frame -> M), both arms are re-solved:
#   - the pipe's pose comes from pipe_pose(frame): grip point G (the right fist) + direction D (towards the pipe's head)
#   - each hand is turned so its fist closes round the pipe: thumb side along D (both thumbs towards the head, like a
#     bat grip), fingers continuing the reach from the shoulder; the wrist goes where the fist centre lands on the pipe
#     (right fist at G, left fist 12 cm below it)
#   - a two-bone IK puts upper arm / forearm there, the elbow staying on the side the mocap bent it
#   - index + thumb are curled into a fist
#   - optionally the head is held facing the way his body stood at the first frame (the batter mocap looks off sideways)
# Everything happens on the segment transforms (world deltas of rigid bone groups) before apply_pose, so the result is
# plain keyframes: no runtime IK in the game. The pipe then sits rigidly in the right fist (Unity: hand.r, thumb axis = +X
# = pipe axis, fist centre offset FIST below).
import math
from mathutils import Matrix, Vector, Quaternion

FIST = (0.065, 0.025)          # wrist -> fist centre: along the fingers, towards the palm (metres)
LEFT_BELOW = 0.12              # left fist this far below the right one along the pipe
CURL_INDEX = (75.0, 85.0, 55.0)
CURL_THUMB = (35.0, 30.0, 25.0)
HEAD_CLEAR = 0.14              # the pipe's axis stays this far from the middle of his skull (head ~10 cm radius + pipe)


def _pt(rig, name):
    if name.endswith(">"): return rig.data.bones[name[:-1]].tail_local.copy()
    return rig.data.bones[name].head_local.copy()


class GripSolver:
    def __init__(self, rig, prep):
        self.rig, self.prep = rig, prep
        r = _pt(rig, "c_thigh_fk.r") - _pt(rig, "c_thigh_fk.l"); r.z = 0; r.normalize()
        self.right = r
        self.up = Vector((0, 0, 1))
        self.fwd = self.up.cross(self.right).normalized()
        self.neck_rest = _pt(rig, "neck.x")
        self.max_err = 0.0          # largest fist-to-target miss (m), for the build log
        self.track = {}             # frame index -> (G, D) in rig armature space, for previews
        self.hand = {}
        for s, side in (("r", 1.0), ("l", -1.0)):
            f0 = (_pt(rig, "hand." + s + ">") - _pt(rig, "hand." + s)).normalized()
            t0 = (_pt(rig, "c_thumb1_base." + s) - _pt(rig, "hand." + s))
            t0 = (t0 - f0 * t0.dot(f0)).normalized()
            p0 = (t0.cross(f0) * side).normalized()          # palm normal (right: t x f, left: f x t)
            self.hand[s] = (side, f0, t0, p0)

    # ---- helpers on the segment dict M (key / bone name -> world delta matrix, rig armature space)
    def joint(self, M, key):
        return M[key] @ self.prep[key][2]

    def bones_of(self, key):
        names = list(SEGS[key][0])
        if key in ("l_5", "r_5"):
            s = key[0]
            names += ["arm_twist." + s, "c_arm_twist_offset." + s]
        return names

    def apply(self, M, keys, D):
        done = set()
        for k in keys:
            for n in [k] + self.bones_of(k):
                if n in M and n not in done:
                    M[n] = D @ M[n]; done.add(n)

    def chain(self, s, frm):
        keys = ["%s_%d" % (s, i) for i in range(5, 14)]
        return keys[keys.index("%s_%d" % (s, frm)):]

    @staticmethod
    def rot_about(p, q):
        return Matrix.Translation(p) @ q.to_matrix().to_4x4() @ Matrix.Translation(-p)

    def chest_frame(self, M):
        """origin = neck base now; axes = the chest's (spine3) rotation applied to the rest body axes"""
        R = M["spine3"].to_3x3().normalized()
        return self.joint(M, "neck"), R @ self.right, R @ self.fwd, R @ self.up

    def root_frame(self, M):
        """origin = neck base now (follows crouch / step), axes = the body's rest facing (doesn't twist with the chest)"""
        return self.joint(M, "neck"), self.right.copy(), self.fwd.copy(), self.up.copy()

    @staticmethod
    def to_world(frame, local):
        o, x, y, z = frame
        return o + x * local[0] + y * local[1] + z * local[2]

    @staticmethod
    def dir_world(frame, local):
        o, x, y, z = frame
        return (x * local[0] + y * local[1] + z * local[2]).normalized()

    # ---- the solve
    def solve_arm(self, M, s, fist_point, D):
        side, f0, t0, p0 = self.hand[s]
        S = self.joint(M, s + "_5")
        # hand orientation: thumb along the pipe, fingers continuing the reach (perpendicular to the pipe)
        reach = fist_point - S
        f = reach - D * reach.dot(D)
        if f.length < 1e-4: f = self.fwd - D * self.fwd.dot(D)
        f.normalize()
        t = D.normalized()
        p = (t.cross(f) * side).normalized()
        B0 = Matrix((f0, t0, p0)).transposed()
        B1 = Matrix((f, t, p)).transposed()
        Rh = B1 @ B0.transposed()
        wrist = fist_point - f * FIST[0] - p * FIST[1]

        # two-bone IK (upper arm about the shoulder, forearm about the elbow)
        E, W = self.joint(M, s + "_6"), self.joint(M, s + "_7")
        L1, L2 = (E - S).length, (W - E).length
        toT = wrist - S
        d = max(abs(L1 - L2) + 1e-3, min(L1 + L2 - 1e-3, toT.length))
        x = toT.normalized()
        pole = (E - S) - x * (E - S).dot(x)
        if pole.length < 1e-5: pole = -self.up - x * (-self.up).dot(x)
        y = pole.normalized()
        a = math.acos(max(-1.0, min(1.0, (L1 * L1 + d * d - L2 * L2) / (2 * L1 * d))))
        E2 = S + (x * math.cos(a) + y * math.sin(a)) * L1
        self.apply(M, self.chain(s, 5), self.rot_about(S, (E - S).rotation_difference(E2 - S)))
        W1 = self.joint(M, s + "_7")
        T = S + x * d
        self.apply(M, self.chain(s, 6), self.rot_about(E2, (W1 - E2).rotation_difference(T - E2)))

        # hand: exact orientation at the (reached) wrist
        Wn = self.joint(M, s + "_7")
        Rcur = M[s + "_7"].to_3x3().normalized()
        dq = (Rh @ Rcur.inverted()).to_quaternion()
        self.apply(M, self.chain(s, 7), self.rot_about(Wn, dq))

        # fist: index and thumb curled towards the palm
        axis = f.cross(p).normalized()          # rotating the fingers about this moves them towards the palm
        for i, ang in zip((8, 9, 10), CURL_INDEX):
            k = "%s_%d" % (s, i)
            self.apply(M, self.chain(s, i)[:11 - i], self.rot_about(self.joint(M, k), Quaternion(axis, math.radians(ang))))
        for i, ang in zip((11, 12, 13), CURL_THUMB):
            k = "%s_%d" % (s, i)
            self.apply(M, self.chain(s, i)[:14 - i], self.rot_about(self.joint(M, k), Quaternion(axis, math.radians(ang))))

        # how close the fist ended up (the IK clamps at full reach)
        Rn = M[s + "_7"].to_3x3().normalized()
        fist = self.joint(M, s + "_7") + (Rn @ f0) * FIST[0] + (Rn @ p0) * FIST[1]
        err = (fist - fist_point).length
        if err > self.max_err: self.max_err, self.max_err_at = err, s

    def fist_of(self, M, s):
        side, f0, t0, p0 = self.hand[s]
        Rn = M[s + "_7"].to_3x3().normalized()
        return self.joint(M, s + "_7") + (Rn @ f0) * FIST[0] + (Rn @ p0) * FIST[1]

    def mocap_grip(self, M, fi=None):
        """real (filmed) two-handed hold: the arms stay exactly as captured - only the wrists turn (the least they can) so
        both thumbs lie along the line through the two fists (= the stick), then the fingers close. The pipe, rigid in the
        right fist along its thumb, then passes through the left fist."""
        turn = 0.0
        for _ in range(4):                        # turning a wrist moves its fist a little - converge
            D = (self.fist_of(M, "r") - self.fist_of(M, "l")).normalized()
            for s in ("r", "l"):
                side, f0, t0, p0 = self.hand[s]
                Rn = M[s + "_7"].to_3x3().normalized()
                dq = (Rn @ t0).rotation_difference(D)
                turn = max(turn, math.degrees(dq.angle)) if _ == 0 else turn
                self.apply(M, self.chain(s, 7), self.rot_about(self.joint(M, s + "_7"), dq))
        G = self.fist_of(M, "r")
        D = (G - self.fist_of(M, "l")).normalized()
        for s in ("r", "l"):
            side, f0, t0, p0 = self.hand[s]
            Rn = M[s + "_7"].to_3x3().normalized()
            axis = (Rn @ f0).cross(Rn @ p0).normalized()
            for i, ang in zip((8, 9, 10), CURL_INDEX):
                k = "%s_%d" % (s, i)
                self.apply(M, self.chain(s, i)[:11 - i], self.rot_about(self.joint(M, k), Quaternion(axis, math.radians(ang))))
            for i, ang in zip((11, 12, 13), CURL_THUMB):
                k = "%s_%d" % (s, i)
                self.apply(M, self.chain(s, i)[:14 - i], self.rot_about(self.joint(M, k), Quaternion(axis, math.radians(ang))))
        # left fist distance from the right thumb's line (what the rigid pipe will show)
        L = self.fist_of(M, "l"); d = L - G
        off = (d - D * d.dot(D)).length
        if off > self.max_err: self.max_err, self.max_err_at = off, "l off pipe"
        self.max_turn = max(getattr(self, "max_turn", 0.0), turn)
        if fi is not None: self.track[fi] = (G.copy(), D.copy())

    def hand_retarget(self, M, gR, gL, her_shoulders, fi=None, offset=None):
        """position-based hands for a filmed take: both fists go where hers were relative to her neck (in her chest's
        axes: x = her left, y = back, z = up - the canonical frame faces -Y), scaled by shoulder width, then both arms
        are solved to them with the thumbs along the stick. (Copying her joint angles instead throws Khalid's hands far
        too high: her upper arms are ~15 cm, his ~28.)"""
        if not hasattr(self, "khalid_sw"):
            self.khalid_sw = (self.prep["l_5"][2] - self.prep["r_5"][2]).length     # shoulder joints (rest)
            print("HAND_RETARGET khalid shoulders %.3f her %.3f -> scale %.3f" % (self.khalid_sw, her_shoulders, self.khalid_sw / her_shoulders))
        k = self.khalid_sw / her_shoulders
        Rc = M["spine3"].to_3x3().normalized()
        neck = self.joint(M, "neck")
        def place(g):
            v = self.right * (-g.x) + self.fwd * (-g.y) + self.up * g.z      # canonical (-Y facing) -> his rest axes
            return neck + Rc @ (v * k)
        TR, TL = place(gR), place(gL)
        D = (TR - TL).normalized()
        if offset is not None:
            # both fists moved together (his rest axes: x = right, y = forward, z = up, turned with his chest)
            TR = TR + Rc @ (self.right * offset[0] + self.fwd * offset[1] + self.up * offset[2])
        gap = max(0.09, (gR - gL).length)          # the hands' spacing on a real stick: not scaled
        # keep the pipe (15 cm below the right fist to 66 cm above it) out of his head: her head is smaller than his
        hc = self.joint(M, "head") + M["head"].to_3x3().normalized() @ (self.up * 0.10)
        a0 = TR - D * 0.15
        t = max(0.0, min(0.81, (hc - a0).dot(D)))
        away = a0 + D * t - hc
        if away.length < HEAD_CLEAR:
            push = (away.normalized() if away.length > 1e-4 else self.fwd) * (HEAD_CLEAR - away.length)
            TR = TR + push
            self.head_pushes = max(getattr(self, "head_pushes", 0.0), push.length)
        self.solve_arm(M, "r", TR, D)
        self.solve_arm(M, "l", TR - D * gap, D)
        if fi is not None: self.track[fi] = (TR.copy(), D.copy())

    def solve(self, M, G, D, fi=None):
        if fi is not None: self.track[fi] = (G.copy(), D.copy())
        self.solve_arm(M, "r", G, D)
        self.solve_arm(M, "l", G - D * LEFT_BELOW, D)

    def hold_head(self, M, R_head, R_neck, weight_neck=0.5):
        """head (and half the neck) turned to world rotations R_head / R_neck (3x3) - keeps his eyes where he faced"""
        for key, R, w in (("neck", R_neck, weight_neck), ("head", R_head, 1.0)):
            P = self.joint(M, key)
            Rcur = M[key].to_3x3().normalized()
            dq = Quaternion().slerp((R @ Rcur.inverted()).to_quaternion(), w)
            keys = ["neck", "head"] if key == "neck" else ["head"]
            Dm = self.rot_about(P, dq)
            done = set()
            for k in keys:
                for n in [k] + list(SEGS[k][0]):
                    if n in M and n not in done:
                        M[n] = Dm @ M[n]; done.add(n)


def retarget_with(arm_name, action_name, hook, rig_name="rig"):
    """mocap_retarget.retarget with a hook(frame_index, M, solver) run on the segments before they're applied"""
    import bpy
    sc = bpy.context.scene
    arm = bpy.data.objects[arm_name]; rig = bpy.data.objects[rig_name]
    src = arm.animation_data.action
    f0, f1 = int(src.frame_range[0]), int(src.frame_range[1])
    ad = rig.animation_data or rig.animation_data_create()
    old = (ad.action, ad.use_nla, sc.frame_current)
    ad.use_nla = False; ad.action = None
    prep = prepare(arm, rig)
    solver = GripSolver(rig, prep)
    if action_name in bpy.data.actions: bpy.data.actions.remove(bpy.data.actions[action_name])
    act = bpy.data.actions.new(action_name); act.use_fake_user = True
    ad.action = act
    prev = {}
    for f in range(f0, f1 + 1):
        sc.frame_set(f)
        ad.action = None
        M = pose_frame(arm, rig, prep)
        hook(f - f0, M, solver)
        # how far the left fist is from the right thumb's line (= the rigid pipe in game), from the segments...
        Rr = M["r_7"].to_3x3().normalized()
        G = solver.fist_of(M, "r"); ax = (Rr @ solver.hand["r"][2]).normalized(); dl = solver.fist_of(M, "l") - G
        line_seg = (dl - ax * dl.dot(ax)).length
        solver.max_line = max(getattr(solver, "max_line", 0.0), line_seg)
        LAST_TRACK[action_name] = solver.track
        apply_pose(rig, M)
        # ...and from the posed bones themselves (what gets exported)
        def fist_pb(sd):
            pb = rig.pose.bones["hand." + sd]; R = pb.matrix.to_3x3().normalized() @ rig.data.bones["hand." + sd].matrix_local.to_3x3().normalized().inverted()
            side, f0_, t0_, p0_ = solver.hand[sd]
            return pb.head + (R @ f0_) * FIST[0] + (R @ p0_) * FIST[1], (R @ t0_).normalized()
        (Gp, axp), (Lp, _) = fist_pb("r"), fist_pb("l")
        dl = Lp - Gp
        solver.max_line_pb = max(getattr(solver, "max_line_pb", 0.0), (dl - axp * dl.dot(axp)).length)
        for pb in rig.pose.bones:
            q = pb.rotation_quaternion
            if pb.name in prev and q.dot(prev[pb.name]) < 0: pb.rotation_quaternion = (-q.w, -q.x, -q.y, -q.z)
            prev[pb.name] = pb.rotation_quaternion.copy()
        ad.action = act
        for pb in rig.pose.bones:
            for prop in ("location", "rotation_quaternion", "scale"): pb.keyframe_insert(prop, frame=f, group=pb.name)
    ad.use_nla = old[1]
    sc.frame_set(old[2])
    print("GRIP_ERR", action_name, "max fist miss %.3f m (%s hand)" % (solver.max_err, getattr(solver, "max_err_at", "-")),
          "max wrist turn %.0f deg" % getattr(solver, "max_turn", 0.0),
          "| left fist off pipe line: segments %.3f m, posed bones %.3f m" % (getattr(solver, "max_line", 0.0), getattr(solver, "max_line_pb", 0.0)),
          "| head push %.3f m" % getattr(solver, "head_pushes", 0.0))
    return {"frames": [f0, f1]}


LAST_TRACK = {}


# ---- the pipe's path (local: x = his right, y = forward, z = up; metres from the base of his neck)
def _n(v):
    v = Vector(v); return v.normalized()

# fists at chest height just right of centre, pipe tilted up and back over the right shoulder (both hands within reach:
# the left fist reaching across to 0.5 m from its shoulder - his arm + fist is ~0.58 m)
PIPE_STANCE = (Vector((0.10, 0.28, -0.30)), _n((0.30, -0.30, 0.90)))
ATTACK_KEYS = [
    # (output frame, grip point, direction, ease into this key)
    (0.0, PIPE_STANCE[0], PIPE_STANCE[1], False),
    (16.0, Vector((0.27, 0.02, -0.10)), _n((-0.25, -0.75, 0.6)), True),    # drawn back over the right shoulder
    (20.0, Vector((0.27, 0.02, -0.10)), _n((-0.25, -0.75, 0.6)), False),   # held at the top
    (22.6, Vector((0.30, 0.30, -0.24)), _n((0.85, 0.45, 0.12)), False),    # coming round, head out to his right
    (25.4, Vector((0.08, 0.47, -0.30)), _n((0.05, 1.0, -0.05)), False),    # contact, straight in front
    (28.0, Vector((-0.08, 0.40, -0.30)), _n((-0.85, 0.5, -0.1)), False),
    (31.0, Vector((-0.18, 0.26, -0.30)), _n((-0.9, -0.25, -0.15)), False),
    (36.0, Vector((-0.22, 0.12, -0.30)), _n((-0.6, -0.7, -0.2)), True),    # follow-through, settling
    (39.0, Vector((-0.22, 0.12, -0.30)), _n((-0.6, -0.7, -0.2)), False),
    (56.0, PIPE_STANCE[0], PIPE_STANCE[1], True),                                     # recovered
]


def _smooth(x): return x * x * (3 - 2 * x)


def attack_pipe(fi):
    keys = ATTACK_KEYS
    if fi <= keys[0][0]: return keys[0][1], keys[0][2]
    if fi >= keys[-1][0]: return keys[-1][1], keys[-1][2]
    for i in range(len(keys) - 1):
        t0, g0, d0, _ = keys[i]
        t1, g1, d1, ease = keys[i + 1]
        if t0 <= fi <= t1:
            u = (fi - t0) / (t1 - t0)
            if ease: u = _smooth(u)
            # Catmull-Rom through the neighbouring keys for the grip point (continuous speed through the strike)
            gm = keys[i - 1][1] if i > 0 else g0
            gp = keys[i + 2][1] if i + 2 < len(keys) else g1
            u2, u3 = u * u, u * u * u
            g = 0.5 * ((2 * g0) + (-gm + g1) * u + (2 * gm - 5 * g0 + 4 * g1 - gp) * u2 + (-gm + 3 * g0 - 3 * g1 + gp) * u3)
            d = Quaternion().slerp(d0.rotation_difference(d1), u) @ d0
            return g, d.normalized()
    return keys[-1][1], keys[-1][2]
