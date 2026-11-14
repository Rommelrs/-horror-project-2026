using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The storage room falling apart: service pipes on the walls (a grey PVC drain under the ceiling with a leaking joint,
/// a galvanised water pipe with a tap), a place where the wall has been broken open and the pipes in it show, hollow
/// cement blocks, rubble, missing / hanging ceiling tiles and loose cables.
/// The surfaces themselves (peeling paint over cement, concrete floor, stained ceiling tiles) are textures made from the
/// location photos by scratchpad store/decay/make_decay.py; the positions here and there belong together.
/// Coordinates: Store fitting space (y = FloorY + height above the room's floor; StoreFittings.off moves it to the room).
/// </summary>
public static partial class StoreFittings
{
    static Material MGalv => Mat("SM_SrPipeGalv", "sr_pipe_galv.png", Color.white, 0.3f, false, false);
    static Material MPvc => Mat("SM_SrPipePvc", "sr_pipe_pvc.png", Color.white, 0.28f, false, false);
    static Material MRustPipe => Mat("SM_SrPipeRust", "sr_pipe_galv.png", new Color(0.62f, 0.44f, 0.34f), 0.08f, false, false);
    static Material MCmu => Mat("SM_SrCmu", "sr_cmu.png", Color.white, 0.02f, false, false);
    static Material MBlockwork => Mat("SM_SrBlockwork", "sr_blockwork.png", new Color(0.6f, 0.6f, 0.6f), 0.02f, false, false);
    static Material MPaintChip => Mat("SM_SrPaintChip", "sr_paint_chip.png", Color.white, 0.04f, false, false);
    static Material MPlasterEdge => Mat("SM_SrPlasterEdge", "sr_plaster_edge.png", Color.white, 0.02f, false, false);
    static Material MVoid => Flat("SM_SrVoid", new Color(0.03f, 0.03f, 0.035f), 0f);
    static Material MCable => Flat("SM_SrCable", new Color(0.045f, 0.045f, 0.05f), 0.3f);

    /// <summary>A pipe along a path with real bends (each corner is rounded) and a socket where a bend meets the straight.</summary>
    static void PipeRun(Acc a, Vector3[] path, float r, float tile, bool sockets = true, int seg = 10)
    {
        var pts = new List<Vector3>(); var joint = new List<bool>();
        float bend = r * 2.4f;
        pts.Add(path[0]); joint.Add(false);
        for (int i = 1; i + 1 < path.Length; i++)
        {
            Vector3 din = (path[i] - path[i - 1]).normalized, dout = (path[i + 1] - path[i]).normalized;
            float cut = Mathf.Min(bend, (path[i] - path[i - 1]).magnitude * 0.45f, (path[i + 1] - path[i]).magnitude * 0.45f);
            Vector3 a0 = path[i] - din * cut, a1 = path[i] + dout * cut;
            const int n = 5;
            for (int k = 0; k <= n; k++)
            {
                float t = k / (float)n;
                // quadratic bend through the corner
                Vector3 p = Vector3.Lerp(Vector3.Lerp(a0, path[i], t), Vector3.Lerp(path[i], a1, t), t);
                pts.Add(p); joint.Add(k == 0 || k == n);
            }
        }
        pts.Add(path[path.Length - 1]); joint.Add(false);
        float v = 0f;
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            float len = (pts[i + 1] - pts[i]).magnitude;
            if (len < 1e-4f) continue;
            Cyl(a, pts[i], pts[i + 1], r, r, seg, new Rect(0f, v, 1f, len / tile), i == 0, i + 2 == pts.Count);
            v += len / tile;
            if (sockets && joint[i] && i > 0)
            {
                Vector3 d = (pts[i + 1] - pts[i]).normalized;
                Cyl(a, pts[i] - d * r * 0.9f, pts[i] + d * r * 0.9f, r * 1.22f, r * 1.22f, seg, new Rect(0f, 0f, 1f, 0.2f));
            }
        }
    }

    /// <summary>A wall clip: a stand-off from the wall and a band round the pipe.</summary>
    static void PipeClip(Acc steel, Vector3 pipeCentre, Vector3 along, Vector3 toWall, float r, float standoff)
    {
        along = along.normalized; toWall = toWall.normalized;
        Cyl(steel, pipeCentre - along * 0.014f, pipeCentre + along * 0.014f, r + 0.006f, r + 0.006f, 10, new Rect(0.1f, 0.1f, 0.2f, 0.2f));
        Quaternion rot = Quaternion.LookRotation(toWall, Mathf.Abs(Vector3.Dot(along, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up);
        OBox(steel, pipeCentre + toWall * (r + (standoff - r) * 0.5f), new Vector3(0.022f, 0.022f, standoff - r), rot, 0.5f);
        OBox(steel, pipeCentre + toWall * (standoff - 0.003f), new Vector3(0.07f, 0.07f, 0.006f), rot, 0.5f);
    }

    /// <summary>A flat surface (wall, ceiling) with rectangular openings cut in it, one texture over all of it.
    /// Openings are in metres from the surface's lower left as seen from its front (u along Cross(up, -nrm), v along up).</summary>
    public static GameObject PlaneWithHoles(Transform parent, string name, Vector3 centre, Vector3 nrm, Vector3 upv, float len, float h, Material mat, params Rect[] holes)
    {
        var a = new Acc();
        Vector3 up = upv.normalized, right = Vector3.Cross(up, -nrm).normalized;
        Vector3 bl = centre - right * len * 0.5f - up * h * 0.5f;
        var us = new List<float> { 0f, len }; var vs = new List<float> { 0f, h };
        foreach (var r in holes) { us.Add(Mathf.Clamp(r.xMin, 0f, len)); us.Add(Mathf.Clamp(r.xMax, 0f, len)); vs.Add(Mathf.Clamp(r.yMin, 0f, h)); vs.Add(Mathf.Clamp(r.yMax, 0f, h)); }
        us.Sort(); vs.Sort();
        for (int i = 0; i + 1 < us.Count; i++)
            for (int k = 0; k + 1 < vs.Count; k++)
            {
                float u0 = us[i], u1 = us[i + 1], v0 = vs[k], v1 = vs[k + 1];
                if (u1 - u0 < 1e-4f || v1 - v0 < 1e-4f) continue;
                var mid = new Vector2((u0 + u1) * 0.5f, (v0 + v1) * 0.5f);
                bool open = false;
                foreach (var r in holes) if (r.Contains(mid)) open = true;
                if (open) continue;
                a.Quad(bl + right * u0 + up * v0, bl + right * u0 + up * v1, bl + right * u1 + up * v1, bl + right * u1 + up * v0, nrm,
                       new Vector2(u0 / len, v0 / h), new Vector2(u1 / len, v1 / h));
            }
        var holder = PropHolder(name, parent, new Bounds(centre, Vector3.one * 0.1f), false);
        a.Emit(name + " Mesh", holder.transform, mat, false);
        return holder;
    }

    /// <summary>The dark space behind an opening in the ceiling: four sides and a lid, seen from below.</summary>
    static void CeilingVoid(Acc dark, float xa, float xb, float za, float zb, float y, float height)
    {
        float cx = (xa + xb) * 0.5f, cz = (za + zb) * 0.5f, cy = y + height * 0.5f;
        dark.Face(new Vector3(cx, y + height, cz), Vector3.down, Vector3.forward, xb - xa, zb - za, Vector2.zero, Vector2.one);
        dark.Face(new Vector3(xa, cy, cz), Vector3.right, Vector3.up, zb - za, height, Vector2.zero, Vector2.one);
        dark.Face(new Vector3(xb, cy, cz), Vector3.left, Vector3.up, zb - za, height, Vector2.zero, Vector2.one);
        dark.Face(new Vector3(cx, cy, za), Vector3.forward, Vector3.up, xb - xa, height, Vector2.zero, Vector2.one);
        dark.Face(new Vector3(cx, cy, zb), Vector3.back, Vector3.up, xb - xa, height, Vector2.zero, Vector2.one);
    }

    /// <summary>Everything that pokes out of / hangs off the storage room's shell. x0..x1, z0..z1 = the wall faces, h = room height;
    /// eastHole = the opening cut in the east wall (PlaneWithHoles units), eastU0Z = the z where that wall's u starts.</summary>
    public static void StoreroomDecay(Transform parent, float x0, float x1, float z0, float z1, float h, Rect eastHole, float eastU0Z, Material ceilingMat)
    {
        var rnd = new System.Random(4170);
        float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
        float Y(float v) => FloorY + v;
        var go = PropHolder("Decay", parent, new Bounds(new Vector3((x0 + x1) * 0.5f, Y(h * 0.5f), (z0 + z1) * 0.5f), Vector3.one), false);
        Transform g = go.transform;
        var pvc = new Acc(); var galv = new Acc(); var steel = new Acc(); var rust = new Acc(); var cmu = new Acc(); var bw = new Acc();
        var dark = new Acc(); var chip = new Acc(); var edge = new Acc(); var red = new Acc(); var brass = new Acc(); var cable = new Acc(); var tile = new Acc();

        // ---------------- the grey PVC drain: down the south wall near the east corner, along under the ceiling, up into it
        const float pr = 0.055f, pOff = 0.075f;
        float zs = z0 + pOff;                                  // its centre line off the south wall
        float xr = x1 - 0.55f, xw = x1 - 8.86f, yRun = 3.27f;
        PipeRun(pvc, new[] { new Vector3(xr, Y(0.0f), zs), new Vector3(xr, Y(yRun), zs), new Vector3(xw, Y(yRun), zs), new Vector3(xw, Y(h + 0.25f), zs) }, pr, 0.6f);
        foreach (float u in new[] { 1.3f, 3.8f, 5.6f, 7.0f, 8.2f })
            PipeClip(steel, new Vector3(x1 - u, Y(yRun), zs), Vector3.right, Vector3.back, pr, pOff);
        foreach (float v in new[] { 0.45f, 1.7f, 2.75f })
            PipeClip(steel, new Vector3(xr, Y(v), zs), Vector3.up, Vector3.back, pr, pOff);
        // push-fit couplers; the one at u 3.1 has let go a little and drips
        foreach (float u in new[] { 2.0f, 3.1f, 4.9f, 6.4f })
        {
            Vector3 c = new Vector3(x1 - u, Y(yRun), zs);
            Cyl(pvc, c - Vector3.right * 0.06f, c + Vector3.right * 0.06f, pr * 1.2f, pr * 1.2f, 12, new Rect(0f, 0f, 1f, 0.2f));
        }
        Cyl(pvc, new Vector3(xr, Y(0f), zs), new Vector3(xr, Y(0.06f), zs), pr * 1.35f, pr * 1.2f, 12, new Rect(0f, 0f, 1f, 0.1f));   // floor collar
        Cyl(pvc, new Vector3(xr, Y(1.05f), zs), new Vector3(xr, Y(1.2f), zs), pr * 1.2f, pr * 1.2f, 12, new Rect(0f, 0f, 1f, 0.2f)); // access cap
        Cyl(pvc, new Vector3(xr, Y(1.125f), zs), new Vector3(xr, Y(1.125f), zs + 0.085f), pr * 0.8f, pr * 0.8f, 10, new Rect(0f, 0f, 1f, 0.1f));

        // ---------------- the galvanised water pipe: up the south wall in the corner, a branch round onto the east wall to a tap
        const float gr = 0.017f, gOff = 0.035f;
        float gz = z0 + gOff, gx = x1 - 0.2f, ex = x1 - gOff, tapZ = z0 + 1.64f, endZ = z0 + 2.09f, branchY = 0.55f;
        PipeRun(galv, new[] { new Vector3(gx, Y(0f), gz), new Vector3(gx, Y(h + 0.2f), gz) }, gr, 0.35f);
        PipeRun(galv, new[] { new Vector3(gx, Y(branchY), gz), new Vector3(ex, Y(branchY), gz), new Vector3(ex, Y(branchY), endZ), new Vector3(x1 + 0.1f, Y(branchY), endZ) }, gr, 0.35f);
        Cyl(galv, new Vector3(gx, Y(branchY - 0.03f), gz), new Vector3(gx, Y(branchY + 0.03f), gz), gr * 1.5f, gr * 1.5f, 10, new Rect(0f, 0f, 1f, 0.2f));   // the tee
        foreach (float v in new[] { 1.5f, 2.6f }) PipeClip(steel, new Vector3(gx, Y(v), gz), Vector3.up, Vector3.back, gr, gOff);
        foreach (float z in new[] { z0 + 0.56f, z0 + 1.26f, z0 + 1.9f }) PipeClip(steel, new Vector3(ex, Y(branchY), z), Vector3.forward, Vector3.right, gr, gOff);
        // the tap: a tee, a brass body turned into the room, a red lever
        Vector3 tp = new Vector3(ex, Y(branchY), tapZ);
        Cyl(galv, tp - Vector3.forward * 0.03f, tp + Vector3.forward * 0.03f, gr * 1.5f, gr * 1.5f, 10, new Rect(0f, 0f, 1f, 0.2f));
        Cyl(brass, tp, tp + Vector3.left * 0.075f, 0.014f, 0.014f, 8, new Rect(0.1f, 0.1f, 0.2f, 0.2f));
        Cyl(brass, tp + Vector3.left * 0.05f + Vector3.up * 0.0f, tp + Vector3.left * 0.05f + Vector3.up * 0.045f, 0.012f, 0.009f, 8, new Rect(0.1f, 0.1f, 0.2f, 0.2f));
        Cyl(brass, tp + Vector3.left * 0.075f, tp + Vector3.left * 0.12f + Vector3.down * 0.05f, 0.012f, 0.011f, 8, new Rect(0.1f, 0.1f, 0.2f, 0.2f));
        OBox(red, tp + Vector3.left * 0.05f + Vector3.up * 0.052f + Vector3.forward * 0.03f, new Vector3(0.018f, 0.008f, 0.1f), Quaternion.Euler(0f, 18f, 0f), 1f);

        // ---------------- the exit sign's conduit and a junction box
        float doorX = -216.25f;
        Cyl(steel, new Vector3(doorX + 0.18f, Y(2.94f), z0 + 0.05f), new Vector3(doorX + 0.18f, Y(h), z0 + 0.05f), 0.011f, 0.011f, 8, new Rect(0.1f, 0.1f, 0.2f, 0.2f));
        OBox(steel, new Vector3(doorX + 0.18f, Y(3.2f), z0 + 0.055f), new Vector3(0.09f, 0.09f, 0.05f), Quaternion.identity, 0.5f);

        // ---------------- the east wall broken open: blockwork behind the plaster, the pipes that run in the wall
        {
            float wx = x1;                                          // the wall face
            float hz0 = eastU0Z - eastHole.xMax, hz1 = eastU0Z - eastHole.xMin, hy0 = eastHole.yMin, hy1 = eastHole.yMax;   // u runs towards -z
            const float depth = 0.17f;
            Vector3 n = Vector3.left;
            // back + the four sides of the cavity
            bw.Quad(new Vector3(wx + depth, Y(hy0), hz1), new Vector3(wx + depth, Y(hy1), hz1), new Vector3(wx + depth, Y(hy1), hz0), new Vector3(wx + depth, Y(hy0), hz0), n,
                    new Vector2(0f, 0f), new Vector2((hz1 - hz0), (hy1 - hy0)));
            cmu.Quad(new Vector3(wx, Y(hy0), hz1), new Vector3(wx + depth, Y(hy0), hz1), new Vector3(wx + depth, Y(hy0), hz0), new Vector3(wx, Y(hy0), hz0), Vector3.up, new Vector2(0, 0), new Vector2(0.4f, 2.5f));
            cmu.Quad(new Vector3(wx, Y(hy1), hz1), new Vector3(wx + depth, Y(hy1), hz1), new Vector3(wx + depth, Y(hy1), hz0), new Vector3(wx, Y(hy1), hz0), Vector3.down, new Vector2(0, 0), new Vector2(0.4f, 2.5f));
            cmu.Quad(new Vector3(wx, Y(hy0), hz0), new Vector3(wx, Y(hy1), hz0), new Vector3(wx + depth, Y(hy1), hz0), new Vector3(wx + depth, Y(hy0), hz0), Vector3.forward, new Vector2(0, 0), new Vector2(0.4f, 2.5f));
            cmu.Quad(new Vector3(wx, Y(hy0), hz1), new Vector3(wx, Y(hy1), hz1), new Vector3(wx + depth, Y(hy1), hz1), new Vector3(wx + depth, Y(hy0), hz1), Vector3.back, new Vector2(0, 0), new Vector2(0.4f, 2.5f));
            // the broken rim: teeth of cement-faced plaster left standing round the opening (they hide its straight sides)
            void Rim(Vector3 from, Vector3 to, Vector3 inward)
            {
                float len = (to - from).magnitude; Vector3 dir = (to - from) / len;
                float t = 0f; float prevIn = R(0.05f, 0.14f);
                while (t < len - 1e-3f)
                {
                    float step = Mathf.Min(R(0.05f, 0.15f), len - t), nextIn = R(0.03f, 0.24f);
                    Vector3 p0 = from + dir * t, p1 = from + dir * (t + step);
                    Vector3 q0 = p0 + inward * prevIn + Vector3.right * R(0.0f, 0.03f), q1 = p1 + inward * nextIn + Vector3.right * R(0.0f, 0.03f);
                    Vector3 lift = Vector3.left * 0.002f;
                    cmu.Quad(p0 + lift, q0 + lift, q1 + lift, p1 + lift, n, new Vector2(t, 0f), new Vector2(t + step, 0.3f));
                    // its broken thickness
                    edge.Quad(q0 + lift, q0 + Vector3.right * 0.03f, q1 + Vector3.right * 0.03f, q1 + lift, inward, new Vector2(t * 3f, 0f), new Vector2((t + step) * 3f, 0.15f));
                    prevIn = nextIn; t += step;
                }
            }
            Rim(new Vector3(wx, Y(hy0), hz1), new Vector3(wx, Y(hy0), hz0), Vector3.up);
            Rim(new Vector3(wx, Y(hy1), hz0), new Vector3(wx, Y(hy1), hz1), Vector3.down);
            Rim(new Vector3(wx, Y(hy0), hz0), new Vector3(wx, Y(hy1), hz0), Vector3.forward);
            Rim(new Vector3(wx, Y(hy1), hz1), new Vector3(wx, Y(hy0), hz1), Vector3.back);
            // half-broken blocks still in the opening
            float mz = (hz0 + hz1) * 0.5f;
            OBox(cmu, new Vector3(wx + 0.09f, Y(hy0 + 0.1f), hz0 + 0.2f), new Vector3(0.39f, 0.19f, 0.16f), Quaternion.Euler(0f, 90f, 4f), 0.5f);
            OBox(cmu, new Vector3(wx + 0.07f, Y(hy0 + 0.085f), hz1 - 0.17f), new Vector3(0.3f, 0.17f, 0.15f), Quaternion.Euler(-6f, 82f, 0f), 0.5f);
            OBox(cmu, new Vector3(wx + 0.1f, Y(hy1 - 0.1f), mz + 0.2f), new Vector3(0.39f, 0.19f, 0.14f), Quaternion.Euler(0f, 90f, -3f), 0.5f);
            // the pipes in the wall: a rusted riser at the back, a supply pipe that has been pulled out of line and leans into the
            // room, a conduit with its wires hanging out
            float px = wx + 0.1f;
            PipeRun(rust, new[] { new Vector3(px, Y(hy0 - 0.25f), mz - 0.12f), new Vector3(px, Y(hy1 + 0.25f), mz - 0.12f) }, 0.032f, 0.4f, false);
            Cyl(rust, new Vector3(px, Y(hy0 + 0.52f), mz - 0.12f), new Vector3(px, Y(hy0 + 0.6f), mz - 0.12f), 0.042f, 0.042f, 10, new Rect(0f, 0f, 1f, 0.2f));
            PipeRun(galv, new[] { new Vector3(px - 0.02f, Y(hy0 - 0.2f), mz + 0.16f), new Vector3(px - 0.04f, Y(hy0 + 0.35f), mz + 0.17f), new Vector3(wx - 0.11f, Y(hy0 + 0.74f), mz + 0.2f),
                                  new Vector3(wx - 0.19f, Y(hy0 + 0.78f), mz + 0.05f) }, 0.014f, 0.35f);
            Cyl(galv, new Vector3(wx - 0.19f, Y(hy0 + 0.78f), mz + 0.05f), new Vector3(wx - 0.205f, Y(hy0 + 0.782f), mz + 0.02f), 0.02f, 0.02f, 8, new Rect(0f, 0f, 1f, 0.1f));     // a threaded end, nothing on it
            Cyl(steel, new Vector3(px + 0.02f, Y(hy1 + 0.2f), mz - 0.3f), new Vector3(px + 0.02f, Y(hy1 - 0.3f), mz - 0.3f), 0.011f, 0.011f, 8, new Rect(0.1f, 0.1f, 0.2f, 0.2f));
            Vector3 ce = new Vector3(px + 0.02f, Y(hy1 - 0.3f), mz - 0.3f);
            PipeRun(cable, new[] { ce, ce + new Vector3(-0.08f, -0.12f, 0.02f), ce + new Vector3(-0.2f, -0.42f, 0.05f), ce + new Vector3(-0.21f, -0.62f, 0.04f) }, 0.004f, 1f, false, 6);
            PipeRun(cable, new[] { ce, ce + new Vector3(-0.06f, -0.15f, -0.03f), ce + new Vector3(-0.15f, -0.3f, -0.08f), ce + new Vector3(-0.17f, -0.47f, -0.07f) }, 0.004f, 1f, false, 6);
            // what came out of the wall lies under it
            for (int i = 0; i < 22; i++)
            {
                float rz = R(hz0 - 0.25f, hz1 + 0.25f), dx = Mathf.Abs(R(-1f, 1f)) * R(0.05f, 0.75f);
                float s = R(0.05f, 0.2f) * (1f - dx * 0.5f);
                Vector3 size = new Vector3(s, s * R(0.4f, 0.8f), s * R(0.6f, 1.1f));
                OBox(cmu, new Vector3(wx - 0.02f - dx, Y(size.y * 0.42f), rz), size, Quaternion.Euler(R(-18f, 18f), R(0f, 360f), R(-18f, 18f)), 0.4f);
            }
            for (int i = 0; i < 26; i++)
            {
                float rz = R(hz0 - 0.4f, hz1 + 0.4f), dx = R(0.03f, 0.95f), s = R(0.04f, 0.16f);
                OBox(chip, new Vector3(wx - dx, Y(0.012f), rz), new Vector3(s, 0.014f, s * R(0.5f, 1f)), Quaternion.Euler(R(-6f, 6f), R(0f, 360f), R(-6f, 6f)), 0.5f);
            }
        }

        // ---------------- hollow cement blocks left by the tap (as on the real wall)
        {
            void Block(Vector3 c, Quaternion rot)
            {
                OBox(cmu, c, new Vector3(0.39f, 0.19f, 0.19f), rot, 0.5f);
                foreach (int sd in new[] { -1, 1 })          // the two cores, seen in the top face
                    dark.Face(c + rot * new Vector3(sd * 0.095f, 0.0955f, 0f), rot * Vector3.up, rot * Vector3.forward, 0.13f, 0.12f, Vector2.zero, Vector2.one);
            }
            PropHolder("Cement Blocks", g, new Bounds(new Vector3(x1 - 0.42f, Y(0.2f), z0 + 1.55f), new Vector3(0.5f, 0.4f, 0.75f)), true);
            Block(new Vector3(x1 - 0.42f, Y(0.095f), z0 + 1.35f), Quaternion.Euler(0f, 96f, 0f));
            Block(new Vector3(x1 - 0.4f, Y(0.285f), z0 + 1.38f), Quaternion.Euler(0f, 84f, 0f));
            Block(new Vector3(x1 - 0.62f, Y(0.095f), z0 + 1.86f), Quaternion.Euler(90f, 30f, 0f));     // on its side: the cores face the room
        }
        // paint that has flaked off, all along the foot of the walls
        for (int i = 0; i < 70; i++)
        {
            int wall = rnd.Next(4); float t = (float)rnd.NextDouble(), d = R(0.02f, 0.32f), s = R(0.025f, 0.09f);
            Vector3 p = wall == 0 ? new Vector3(Mathf.Lerp(x0, x1, t), 0f, z0 + d) : wall == 1 ? new Vector3(Mathf.Lerp(x0, x1, t), 0f, z1 - d)
                      : wall == 2 ? new Vector3(x0 + d, 0f, Mathf.Lerp(z0, z1, t)) : new Vector3(x1 - d, 0f, Mathf.Lerp(z0, z1, t));
            p.y = Y(0.008f);
            OBox(chip, p, new Vector3(s, 0.01f, s * R(0.5f, 1f)), Quaternion.Euler(R(-5f, 5f), R(0f, 360f), R(-5f, 5f)), 0.5f);
        }

        // ---------------- ceiling: tiles gone (painted into the ceiling texture), one hanging by an edge, cables down, bits on the floor
        {
            // the two-tile gap near the north-east corner: u 0 - 1.2 from the east wall, z0 + 6.0 .. 6.6
            float hx1 = x1, hx0 = x1 - 1.2f, hz0 = z0 + 6.0f, hz1 = z0 + 6.6f;
            // a stained tile from the ceiling texture, the same on both faces
            Rect cell = new Rect(1.8f / (x1 - x0), 0.6f / (z1 - z0), 0.6f / (x1 - x0), 0.6f / (z1 - z0));
            Quaternion hang = Quaternion.AngleAxis(-58f, Vector3.forward);            // hinged on its west edge, the east edge dropped
            Vector3 hinge = new Vector3(hx0 + 0.6f, Y(h - 0.012f), (hz0 + hz1) * 0.5f);
            OBox(tile, hinge + hang * new Vector3(0.295f, 0f, 0f), new Vector3(0.59f, 0.016f, 0.59f), hang, 1f, cell);
            // a length of bent grid bar hanging with it
            PipeRun(steel, new[] { new Vector3(hx1 - 0.6f, Y(h - 0.01f), hz0), new Vector3(hx1 - 0.45f, Y(h - 0.2f), hz0 + 0.03f), new Vector3(hx1 - 0.38f, Y(h - 0.46f), hz0 + 0.05f) }, 0.008f, 0.3f, false, 5);
            // cables out of the void
            Vector3 c0 = new Vector3(hx1 - 0.3f, Y(h + 0.05f), hz0 + 0.35f);
            PipeRun(cable, new[] { c0, c0 + new Vector3(0.02f, -0.35f, 0.02f), c0 + new Vector3(-0.05f, -0.8f, 0.06f), c0 + new Vector3(-0.02f, -1.05f, 0.1f) }, 0.006f, 1f, false, 6);
            PipeRun(cable, new[] { c0 + new Vector3(-0.1f, 0f, 0.1f), c0 + new Vector3(-0.14f, -0.3f, 0.08f), c0 + new Vector3(-0.1f, -0.58f, 0.02f) }, 0.005f, 1f, false, 6);
            // a rusted pipe crossing the void above the gap, and the same over the gap where the drain goes up
            PipeRun(rust, new[] { new Vector3(hx0 - 0.1f, Y(h + 0.16f), hz0 + 0.22f), new Vector3(hx1 + 0.05f, Y(h + 0.16f), hz0 + 0.22f) }, 0.028f, 0.4f, false);
            CeilingVoid(dark, hx0 - 0.25f, hx1 + 0.05f, hz0 - 0.25f, hz1 + 0.25f, Y(h + 0.012f), 0.42f);
            float ax1 = x1 - 8.4f, ax0 = x1 - 9.0f;
            CeilingVoid(dark, ax0 - 0.25f, ax1 + 0.25f, z0 - 0.05f, z0 + 0.85f, Y(h + 0.012f), 0.42f);
            // the hangers the grid hung from, still up there
            foreach (float hx in new[] { hx0 + 0.02f, hx0 + 0.6f, hx1 - 0.03f })
                Cyl(steel, new Vector3(hx, Y(h), hz0 + 0.3f), new Vector3(hx, Y(h + 0.42f), hz0 + 0.3f), 0.004f, 0.004f, 5, new Rect(0.1f, 0.1f, 0.2f, 0.2f));
            // the tile that fell: broken on the floor under the gap
            Vector3 fl = new Vector3(hx1 - 0.75f, 0f, hz0 - 0.15f);
            OBox(tile, new Vector3(fl.x, Y(0.012f), fl.z), new Vector3(0.36f, 0.016f, 0.59f), Quaternion.Euler(0f, 24f, 0f), 1f, new Rect(cell.x, cell.y, cell.width * 0.6f, cell.height));
            OBox(tile, new Vector3(fl.x + 0.33f, Y(0.03f), fl.z + 0.12f), new Vector3(0.22f, 0.016f, 0.33f), Quaternion.Euler(8f, -40f, 5f), 1f, new Rect(cell.x + cell.width * 0.6f, cell.y, cell.width * 0.4f, cell.height * 0.55f));
            OBox(tile, new Vector3(fl.x + 0.2f, Y(0.012f), fl.z - 0.36f), new Vector3(0.2f, 0.016f, 0.24f), Quaternion.Euler(0f, 70f, 0f), 1f, new Rect(cell.x + cell.width * 0.6f, cell.y + cell.height * 0.55f, cell.width * 0.4f, cell.height * 0.45f));
        }

        pvc.Emit("Decay Drain PVC", g, MPvc);
        galv.Emit("Decay Water Pipe", g, MGalv);
        steel.Emit("Decay Clips", g, MSteel);
        rust.Emit("Decay Rusted Pipe", g, MRustPipe);
        cmu.Emit("Decay Cement", g, MCmu);
        bw.Emit("Decay Blockwork", g, MBlockwork, false);
        dark.Emit("Decay Voids", g, MVoid, false);
        chip.Emit("Decay Paint Chips", g, MPaintChip, false);
        edge.Emit("Decay Plaster Edge", g, MPlasterEdge, false);
        red.Emit("Decay Tap Lever", g, Flat("SM_SrTapRed", new Color(0.6f, 0.07f, 0.05f), 0.4f), false);
        brass.Emit("Decay Tap", g, Flat("SM_SrBrass", new Color(0.5f, 0.39f, 0.18f), 0.6f), false);
        cable.Emit("Decay Cables", g, MCable, false);
        tile.Emit("Decay Ceiling Tiles", g, ceilingMat);
    }
}
