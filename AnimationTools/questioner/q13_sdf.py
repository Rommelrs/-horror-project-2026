# Signed-distance-field modelling: numpy SDF on a voxel grid -> OpenVDB file -> Blender mesh (Volume to Mesh).   exec(open(this).read())
# Shapes are combined with smooth union / smooth subtraction, each op only touches the voxels near its shape.
import bpy, os, math
import numpy as np
import openvdb as vdb
from mathutils import Vector, Matrix, Euler

VDB_DIR = r"D:\Unity Stuff\NEWHorrorProject\-horror-project-2026\AnimationTools\questioner\vdb"
os.makedirs(VDB_DIR, exist_ok=True)


def smin(a, b, k):
    if k <= 0:
        return np.minimum(a, b)
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0, 1)
    return b * (1 - h) + a * h - k * h * (1 - h)


def smax(a, b, k):
    return -smin(-a, -b, k)


def rot(rx=0, ry=0, rz=0):
    """3x3 rotation (degrees, XYZ euler) as numpy: local -> world"""
    return np.array(Euler((math.radians(rx), math.radians(ry), math.radians(rz))).to_matrix(), dtype=np.float32)


class Ball:
    def __init__(self, c, r):
        self.c, self.r = np.array(c, np.float32), r
    def bbox(self):
        return self.c - self.r, self.c + self.r
    def dist(self, P):
        return np.linalg.norm(P - self.c, axis=-1) - self.r


class Ellipsoid:
    def __init__(self, c, radii, R=None):
        self.c = np.array(c, np.float32)
        self.rd = np.array(radii, np.float32)
        self.R = R
    def bbox(self):
        m = float(self.rd.max())
        return self.c - m, self.c + m
    def dist(self, P):
        q = P - self.c
        if self.R is not None:
            q = q @ self.R              # world -> local (R is local->world, rows dot)
        k0 = np.linalg.norm(q / self.rd, axis=-1)
        k1 = np.linalg.norm(q / (self.rd * self.rd), axis=-1)
        return k0 * (k0 - 1.0) / np.maximum(k1, 1e-9)


class Capsule:
    """segment a-b, radius ra at a -> rb at b"""
    def __init__(self, a, b, ra, rb=None):
        self.a, self.b = np.array(a, np.float32), np.array(b, np.float32)
        self.ra, self.rb = ra, (ra if rb is None else rb)
    def bbox(self):
        r = max(self.ra, self.rb)
        return np.minimum(self.a, self.b) - r, np.maximum(self.a, self.b) + r
    def dist(self, P):
        ba = self.b - self.a
        t = np.clip(((P - self.a) @ ba) / float(ba @ ba), 0, 1)
        r = self.ra + (self.rb - self.ra) * t
        return np.linalg.norm(P - (self.a + t[..., None] * ba), axis=-1) - r


class Box:
    def __init__(self, c, half, R=None, round_=0.0):
        self.c, self.h, self.R, self.rr = np.array(c, np.float32), np.array(half, np.float32), R, round_
    def bbox(self):
        m = float(np.linalg.norm(self.h)) + self.rr
        return self.c - m, self.c + m
    def dist(self, P):
        q = P - self.c
        if self.R is not None:
            q = q @ self.R
        q = np.abs(q) - (self.h - self.rr)
        return np.linalg.norm(np.maximum(q, 0), axis=-1) + np.minimum(q.max(-1), 0) - self.rr


class Slab:
    """rounded rectangle in the XY plane (half sizes hx, hy, corner radius rr) extruded +-hz along Z"""
    def __init__(self, c, hx, hy, hz, rr, R=None):
        self.c = np.array(c, np.float32)
        self.hx, self.hy, self.hz, self.rr = hx, hy, hz, min(rr, hx, hy)
        self.R = R
    def bbox(self):
        m = math.sqrt(self.hx ** 2 + self.hy ** 2 + self.hz ** 2)
        return self.c - m, self.c + m
    def dist(self, P):
        q = P - self.c
        if self.R is not None:
            q = q @ self.R
        qx = np.abs(q[..., 0]) - (self.hx - self.rr)
        qy = np.abs(q[..., 1]) - (self.hy - self.rr)
        d2 = np.sqrt(np.maximum(qx, 0) ** 2 + np.maximum(qy, 0) ** 2) + np.minimum(np.maximum(qx, qy), 0) - self.rr
        dz = np.abs(q[..., 2]) - self.hz
        w0, w1 = np.maximum(d2, 0), np.maximum(dz, 0)
        return np.minimum(np.maximum(d2, dz), 0) + np.sqrt(w0 * w0 + w1 * w1)


class Chain:
    """poly-capsule through points with per-point radii (hard union of the segments)"""
    def __init__(self, pts, radii):
        self.segs = [Capsule(pts[i], pts[i + 1], radii[i], radii[i + 1]) for i in range(len(pts) - 1)]
    def bbox(self):
        lo = np.min([s.bbox()[0] for s in self.segs], 0)
        hi = np.max([s.bbox()[1] for s in self.segs], 0)
        return lo, hi
    def dist(self, P):
        d = self.segs[0].dist(P)
        for s in self.segs[1:]:
            d = np.minimum(d, s.dist(P))
        return d


class SDF:
    def __init__(self, lo, hi, vs):
        self.vs = vs
        self.lo = np.floor(np.array(lo) / vs).astype(int)
        self.n = np.ceil(np.array(hi) / vs).astype(int) - self.lo + 1
        self.D = np.full(tuple(self.n), 0.05, dtype=np.float32)

    def block(self, bmin, bmax):
        i0 = np.maximum(np.floor(np.array(bmin) / self.vs).astype(int) - self.lo, 0)
        i1 = np.minimum(np.ceil(np.array(bmax) / self.vs).astype(int) - self.lo + 1, self.n)
        if np.any(i1 <= i0):
            return None, None
        ax = [((np.arange(i0[k], i1[k]) + self.lo[k]) * self.vs).astype(np.float32) for k in range(3)]
        P = np.stack(np.meshgrid(*ax, indexing="ij"), -1)
        return tuple(slice(i0[k], i1[k]) for k in range(3)), P

    def op(self, shape, mode="add", k=0.0):
        bmin, bmax = shape.bbox()
        pad = 2 * k + 3 * self.vs
        sl, P = self.block(np.array(bmin) - pad, np.array(bmax) + pad)
        if sl is None:
            return
        d = shape.dist(P).astype(np.float32)
        a = self.D[sl]
        if mode == "add":
            self.D[sl] = smin(a, d, k)
        elif mode == "sub":
            self.D[sl] = smax(a, -d, k)
        return self

    def add(self, shape, k=0.0):
        return self.op(shape, "add", k)

    def sub(self, shape, k=0.0):
        return self.op(shape, "sub", k)

    def displace(self, bmin, bmax, fn):
        """D += fn(P) inside a box (small amplitude surface detail)"""
        sl, P = self.block(bmin, bmax)
        if sl is not None:
            self.D[sl] += fn(P).astype(np.float32)

    def to_mesh(self, name, smooth=True):
        import glob, time
        for f in glob.glob(os.path.join(VDB_DIR, name + "_*.vdb")):
            try:
                os.remove(f)
            except OSError:
                pass
        path = os.path.join(VDB_DIR, "%s_%d.vdb" % (name, int(time.time() * 1000)))   # unique: Blender caches volume files
        g = vdb.FloatGrid()
        g.copyFromArray(-self.D, ijk=tuple(int(x) for x in self.lo))
        g.transform = vdb.createLinearTransform(voxelSize=self.vs)
        g.name = "density"
        vdb.write(path, grids=[g])
        for n in (name, name + "_Vol"):
            o = bpy.data.objects.get(n)
            if o:
                bpy.data.objects.remove(o, do_unlink=True)
        vol = bpy.data.volumes.new(name + "_Vol")
        vol.filepath = path
        vo = bpy.data.objects.new(name + "_Vol", vol)
        bpy.context.scene.collection.objects.link(vo)
        tmp = bpy.data.objects.new(name + "_Tmp", bpy.data.meshes.new(name + "_Tmp"))
        bpy.context.scene.collection.objects.link(tmp)
        m = tmp.modifiers.new("V2M", "VOLUME_TO_MESH")
        m.object = vo
        m.grid_name = "density"
        m.threshold = 1e-6
        m.resolution_mode = "GRID"
        m.adaptivity = 0.0
        dg = bpy.context.evaluated_depsgraph_get()
        me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
        me.name = name
        bpy.data.objects.remove(tmp, do_unlink=True)
        bpy.data.objects.remove(vo, do_unlink=True)
        bpy.data.volumes.remove(vol)
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        for p in me.polygons:
            p.use_smooth = smooth
        return ob
