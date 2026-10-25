using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The park back alley (behind the park toilet: the lane between the shop backs and the BACK SHOP HOUSE, and the
// forecourt beside the toilet) after the user's photos of a real alley (2026-10-25): white rendered walls (a grey painted
// band on the house side), octagon-and-dot pavers in pink and grey, steel grid manhole covers, spray graffiti over a
// concrete planter, black conduits, grey meter boxes, windows with exhaust fans. Everything is added on top - nothing
// existing is moved, re-materialled or hidden - so Revert just deletes the group. Only the planter and the stub block
// have colliders (against the wall in the wide forecourt, out of the walking line).
//   Walls: thin skins 1.5 cm in front of the lane-side faces, found by casting rays across the lane (columns every 10 cm,
//   samples every 5 cm up, edges refined), so they follow every building's own face, height and corners. Doors leave
//   holes; props in front (drum, fuse box, brooms, AC unit) stay in front.
//   Ground: pavers 2.5 cm over the terrain and over the road strip (its own faces, a kerb skin on its edge), no
//   collider (you still walk on the old ground, at the same height).
// Textures: AnimationTools/park_alley/make_park_alley_textures.py.
public static class ParkAlleyRealLook
{
    const string Dir = "Assets/Textures/ParkAlley";
    const string MeshDir = "Assets/Models/ParkAlley";
    const string Group = "Park Back Alley (real look)";
    const string BushFbx = "Assets/Models/AlleyBushes/AlleyBushes.fbx";
    const string BushMat = "Assets/Textures/AlleyReal/AlleyBush_Green.mat";
    // the paved area: the forecourt beside the toilet, and the lane up to the north street
    static readonly Rect Forecourt = Rect.MinMaxRect(-233.0f, 68.3f, -228.16f, 75.36f);
    static readonly Rect Lane = Rect.MinMaxRect(-230.27f, 75.36f, -228.16f, 106.8f);
    const float LaneX = -229.2f;            // the lane's middle, where the wall scans start
    const float Lift = 0.025f;              // pavers over the ground
    const float SkinOut = 0.015f;           // wall skins in front of the walls
    const float Col = 0.1f, Step = 0.05f, YLow = 2.62f, YFirst = 2.70f, YTop = 13f;
    const float PlaneTol = 0.05f;           // faces this close count as one (pilasters, plinths a few cm proud)

    public static string LastReport = "";

    [MenuItem("Tools/Environment/Park Back Alley Real Look/Apply")]
    public static void Apply()
    {
        var env = GameObject.Find("------------Environment----------");
        if (env == null) { Debug.LogError("[ParkAlleyRealLook] open the Game scene."); return; }
        ImportSettings();
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/Models", "ParkAlley");

        var old = GameObject.Find(Group);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        // at the scene root, identity: the meshes are built in world space
        var group = new GameObject(Group).transform;
        Undo.RegisterCreatedObjectUndo(group.gameObject, "Park alley real look");

        var white = WallMat("ParkAlleyWall_White", 0f);
        var band = WallMat("ParkAlleyWall_Band", 1.25f);
        var report = new List<string>();
        report.Add(Skin(group, "Skin West (shop backs)", new Vector3(LaneX, 0, 75.0f), Vector3.forward, 32.2f, Vector3.left, white));
        report.Add(Skin(group, "Skin East (house)", new Vector3(LaneX, 0, 68.6f), Vector3.forward, 38.6f, Vector3.right, band));
        report.Add(Skin(group, "Skin South (salon side)", new Vector3(-237.9f, 0, 74.0f), Vector3.right, 7.7f, Vector3.forward, white));
        // the house's street face at the lane mouth (the corner you see walking in)
        report.Add(Skin(group, "Skin House Front (street)", new Vector3(-228.4f, 0, 67.6f), Vector3.right, 15.4f, Vector3.forward, band));
        Pavers(group);
        Props(group, report);

        LastReport = string.Join("\n", report);
        EditorSceneManager.MarkSceneDirty(env.scene);
        Debug.Log("[ParkAlleyRealLook] applied\n" + LastReport);
    }

    [MenuItem("Tools/Environment/Park Back Alley Real Look/Revert")]
    public static void Revert()
    {
        var g = GameObject.Find(Group);
        if (g != null) { var scene = g.scene; Undo.DestroyObjectImmediate(g); EditorSceneManager.MarkSceneDirty(scene); }
        Debug.Log("[ParkAlleyRealLook] reverted");
    }

    // ── textures / materials ──

    static void ImportSettings()
    {
        foreach (var name in new[] { "ParkAlleyRender", "ParkAlleyFoot", "ParkAlleyPavers", "ParkAlleyManhole", "ParkAlleyGraffiti", "ParkAlleyBits" })
        {
            var path = Dir + "/" + name + ".png";
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null) { Debug.LogError("[ParkAlleyRealLook] missing " + path + " (run make_park_alley_textures.py)"); continue; }
            bool alpha = name == "ParkAlleyFoot" || name == "ParkAlleyGraffiti";
            bool tiled = name == "ParkAlleyRender" || name == "ParkAlleyPavers";
            ti.maxTextureSize = 256;
            ti.filterMode = FilterMode.Point;
            ti.wrapModeU = tiled || name == "ParkAlleyFoot" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.wrapModeV = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.alphaIsTransparency = alpha;
            ti.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.mipmapEnabled = true;
            ti.mipMapsPreserveCoverage = name == "ParkAlleyGraffiti";
            ti.alphaTestReferenceValue = 0.45f;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
    }

    static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/" + name + ".png");

    static Material Mat(string name, string shader, string tex, Color color)
    {
        string path = Dir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
        else m.shader = Shader.Find(shader);
        m.mainTexture = tex != null ? Tex(tex) : null;
        m.color = color;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material WallMat(string name, float bandHeight)
    {
        var m = Mat(name, "Custom/ParkAlleyWall", "ParkAlleyRender", Color.white);
        m.SetTexture("_FootTex", Tex("ParkAlleyFoot"));
        m.SetFloat("_PanelWidth", 3.2f);
        m.SetFloat("_BaseY", 2.8f);
        m.SetFloat("_FootHeight", 0.8f);
        m.SetFloat("_BandHeight", bandHeight);
        m.SetColor("_BandColor", new Color(0.86f, 0.83f, 0.77f));
        return m;
    }

    // ── what a ray across the lane meets ──
    // Its first wall face (plane = distance along the ray direction), a door (a hole: no skin, the door shows), or nothing.
    // Props (small colliders), the ground, triggers and the rubble pile are looked through.
    const int Miss = 0, Wall = 1, Hole = 2;

    static bool Under(Transform t, string part)
    {
        for (int i = 0; t != null && i < 4; i++, t = t.parent) if (t.name.Contains(part)) return true;
        return false;
    }

    static bool IsGround(Collider c) => c is TerrainCollider || c.name.StartsWith("Park Road");

    static int Probe(Vector3 o, Vector3 dir, out float plane)
    {
        plane = 0f;
        foreach (var h in Physics.RaycastAll(o, dir, 4.5f, ~0, QueryTriggerInteraction.Ignore).OrderBy(x => x.distance))
        {
            var c = h.collider;
            if (IsGround(c) || Under(c.transform, "Rubble") || c.name == "Blocker") continue;
            if (Under(c.transform, "Door")) return Hole;
            var b = c.bounds;
            if (b.size.x * b.size.y * b.size.z < 3f) continue;
            if (Vector3.Dot(h.normal, -dir) < 0.9f) return Miss;    // a slanted or side face: left as it is
            plane = Vector3.Dot(h.point, dir);
            return Wall;
        }
        return Miss;
    }

    static bool OnPlane(Vector3 o, Vector3 dir, float plane) => Probe(o, dir, out float p) == Wall && Mathf.Abs(p - plane) < PlaneTol;

    // ── wall skins ──
    // Columns every 10 cm along the scan line; in each, rays every 5 cm up give the wall faces as height intervals on
    // their planes (top and bottom refined to the millimetre). Touching columns on the same plane with nearly the same
    // interval make a run, drawn as a ribbon (follows sloping tops). Run ends are refined too; where the face steps to
    // another plane a side quad closes the step, and a free end (outer corner) is stretched 2 cm round the corner.

    class Span { public float plane, y0, y1; }
    class Run { public float plane; public int first; public List<Span> spans = new List<Span>(); public float a0, a1; }

    static string Skin(Transform group, string name, Vector3 start, Vector3 along, float length, Vector3 dir, Material mat)
    {
        int n = Mathf.CeilToInt(length / Col);
        var cols = new List<Span>[n];
        for (int i = 0; i < n; i++)
        {
            cols[i] = new List<Span>();
            Vector3 c = start + along * ((i + 0.5f) * Col);
            Span cur = null; float lastY = 0f;
            for (float y = YFirst; y <= YTop; y += Step)
            {
                int k = Probe(new Vector3(c.x, y, c.z), dir, out float p);
                bool same = cur != null && k == Wall && Mathf.Abs(p - cur.plane) < PlaneTol;
                if (cur != null && !same)
                {
                    cur.y1 = Refine(lastY, y, t => OnPlane(new Vector3(c.x, t, c.z), dir, cur.plane));
                    cols[i].Add(cur); cur = null;
                }
                // a span lies on the face nearest the lane (a plinth or parapet a few cm proud of the wall above it counts
                // as the same face), so the skin goes in front of all of it
                if (same) cur.plane = Mathf.Min(cur.plane, p);
                if (cur == null && k == Wall)
                {
                    cur = new Span { plane = p };
                    // from the ground up: carry it under the ground; above something else: refine where it starts
                    cur.y0 = y <= YFirst + 0.001f ? YLow : Refine(y, y - Step, t => OnPlane(new Vector3(c.x, t, c.z), dir, p));
                }
                lastY = y;
            }
            if (cur != null) { cur.y1 = lastY; cols[i].Add(cur); }
        }

        // runs
        var runs = new List<Run>(); var open = new List<Run>();
        for (int i = 0; i < n; i++)
        {
            var next = new List<Run>();
            foreach (var s in cols[i])
            {
                var r = open.FirstOrDefault(o => !next.Contains(o) && Mathf.Abs(o.plane - s.plane) < PlaneTol
                    && Mathf.Abs(o.spans[o.spans.Count - 1].y0 - s.y0) < 0.06f && Mathf.Abs(o.spans[o.spans.Count - 1].y1 - s.y1) < 0.06f);
                if (r == null) { r = new Run { plane = s.plane, first = i }; runs.Add(r); }
                r.spans.Add(s); r.plane = Mathf.Min(r.plane, s.plane); next.Add(r);
            }
            open = next;
        }

        var verts = new List<Vector3>(); var tris = new List<int>();
        int steps = 0;
        foreach (var r in runs)
        {
            if (r.spans.Count < 3) continue;     // slivers (pipes, edges of props) are left alone
            float mid = (r.spans.Average(s => s.y0) + r.spans.Average(s => s.y1)) * 0.5f;
            int last = r.first + r.spans.Count - 1;
            float aFirst = (r.first + 0.5f) * Col, aLast = (last + 0.5f) * Col;
            System.Func<float, bool> on = a => { Vector3 c = start + along * a; return OnPlane(new Vector3(c.x, mid, c.z), dir, r.plane); };
            r.a0 = r.first > 0 ? Refine(aFirst, aFirst - Col, on) : 0f;
            r.a1 = last < n - 1 ? Refine(aLast, aLast + Col, on) : length;
            // a free end turns an outer corner: stretch over the skin's own offset so no sliver of old wall shows
            if (r.first > 0 && EndKind(start, along, dir, r.a0 - 0.03f, mid) == Miss) r.a0 -= 0.02f;
            if (last < n - 1 && EndKind(start, along, dir, r.a1 + 0.03f, mid) == Miss) r.a1 += 0.02f;

            // ribbon: a vertex pair at each column centre, the refined ends at the run's ends
            var a = new List<float> { r.a0 }; var lo = new List<float> { r.spans[0].y0 }; var hi = new List<float> { r.spans[0].y1 };
            for (int j = 0; j < r.spans.Count; j++)
            {
                float aj = (r.first + j + 0.5f) * Col;
                if (aj <= r.a0 + 0.005f || aj >= r.a1 - 0.005f) continue;
                a.Add(aj); lo.Add(r.spans[j].y0); hi.Add(r.spans[j].y1);
            }
            a.Add(r.a1); lo.Add(r.spans[r.spans.Count - 1].y0); hi.Add(r.spans[r.spans.Count - 1].y1);
            Vector3 face = dir * (r.plane - SkinOut);
            for (int j = 0; j + 1 < a.Count; j++)
            {
                Vector3 p0 = Flat(start + along * a[j], dir) + face, p1 = Flat(start + along * a[j + 1], dir) + face;
                Quad(verts, tris, p0 + Vector3.up * lo[j], p1 + Vector3.up * lo[j + 1], p1 + Vector3.up * hi[j + 1], p0 + Vector3.up * hi[j], -dir);
            }
        }

        // steps between runs on different planes, at the same place along: a side quad over the height both share
        foreach (var r in runs.Where(x => x.spans.Count >= 3))
            foreach (var o in runs.Where(x => x != r && x.spans.Count >= 3 && Mathf.Abs(x.a0 - r.a1) < 0.08f && Mathf.Abs(x.plane - r.plane) > PlaneTol))
            {
                float y0 = Mathf.Max(r.spans.Last().y0, o.spans[0].y0), y1 = Mathf.Min(r.spans.Last().y1, o.spans[0].y1);
                if (y1 - y0 < 0.1f) continue;
                float at = (r.a1 + o.a0) * 0.5f;
                // the side faces the deeper run; nudge it that way off the real side face
                at += (o.plane > r.plane ? 1f : -1f) * SkinOut;
                Vector3 b = Flat(start + along * at, dir);
                Vector3 pa = b + dir * (Mathf.Min(r.plane, o.plane) - SkinOut), pb = b + dir * (Mathf.Max(r.plane, o.plane) - SkinOut);
                Quad(verts, tris, pa + Vector3.up * y0, pb + Vector3.up * y0, pb + Vector3.up * y1, pa + Vector3.up * y1, along, true);
                steps++;
            }

        var go = MeshObject(group, name, verts, tris, mat);
        go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return name + ": " + runs.Count(r => r.spans.Count >= 3) + " runs, " + steps + " steps, " + verts.Count + " verts";
    }

    static int EndKind(Vector3 start, Vector3 along, Vector3 dir, float a, float y)
    {
        Vector3 c = start + along * a;
        return Probe(new Vector3(c.x, y, c.z), dir, out _);
    }

    // the scan line's point with its coordinate along dir zeroed (the plane distance is added back)
    static Vector3 Flat(Vector3 p, Vector3 dir) { p.y = 0; return p - dir * Vector3.Dot(p, dir); }

    // binary search between a value where `ok` holds and one where it does not
    static float Refine(float good, float bad, System.Func<float, bool> ok)
    {
        for (int i = 0; i < 10; i++) { float m = (good + bad) * 0.5f; if (ok(m)) good = m; else bad = m; }
        return good;
    }

    // ── pavers ──

    static float GroundAt(float x, float z)
    {
        foreach (var h in Physics.RaycastAll(new Vector3(x, 9f, z), Vector3.down, 9f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            if (IsGround(h.collider)) return h.point.y;
        return 2.8f;
    }

    static List<float> Steps(params float[] edges)
    {
        var l = new List<float> { edges[0] };
        for (int i = 0; i + 1 < edges.Length; i++)
        {
            int k = Mathf.Max(1, Mathf.CeilToInt((edges[i + 1] - edges[i]) / 0.3f));
            for (int j = 1; j <= k; j++) l.Add(Mathf.Lerp(edges[i], edges[i + 1], j / (float)k));
        }
        return l;
    }

    // The lane is terrain with a strip of the park road (14 cm higher, vertical edges) along the shop side and over most
    // of the forecourt. The pavers follow both exactly - a grid over the terrain, the road's own top faces clipped to the
    // paved area - and the road's edge faces get a concrete kerb skin, so you walk where they are drawn (no ramps that
    // the road edge pokes through).
    static void Pavers(Transform group)
    {
        var verts = new List<Vector3>(); var tris = new List<int>();
        var kv = new List<Vector3>(); var kt = new List<int>();
        // over the terrain: a grid, one x grid for both rects so their shared edge has no cracks (hidden under the road
        // where the road is)
        var xs = Steps(Forecourt.xMin, Lane.xMin, Lane.xMax);
        foreach (var rect in new[] { Forecourt, Lane })
        {
            var rx = xs.Where(x => x >= rect.xMin - 0.001f && x <= rect.xMax + 0.001f).ToList();
            var rz = Steps(rect.yMin, rect.yMax);
            int b = verts.Count;
            foreach (var z in rz) foreach (var x in rx) verts.Add(new Vector3(x, TerrainAt(x, z) + Lift, z));
            for (int j = 0; j + 1 < rz.Count; j++)
                for (int i = 0; i + 1 < rx.Count; i++)
                {
                    int v = b + j * rx.Count + i;
                    tris.AddRange(new[] { v, v + rx.Count, v + 1, v + 1, v + rx.Count, v + rx.Count + 1 });
                }
        }
        // over the road: its top faces, clipped to the paved area; its edge faces: the kerb
        var road = GameObject.Find("Park Road_new");
        var mf = road != null ? road.GetComponent<MeshFilter>() : null;
        if (mf != null)
        {
            var m = mf.sharedMesh; var rv = m.vertices.Select(v => road.transform.TransformPoint(v)).ToArray(); var rt = m.triangles;
            for (int t = 0; t < rt.Length; t += 3)
            {
                Vector3 a = rv[rt[t]], b = rv[rt[t + 1]], c = rv[rt[t + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                foreach (var rect in new[] { Forecourt, Lane })
                {
                    var poly = new List<Vector3> { a, b, c };
                    poly = Clip(poly, Vector3.right, rect.xMin); poly = Clip(poly, Vector3.left, -rect.xMax);
                    poly = Clip(poly, Vector3.forward, rect.yMin); poly = Clip(poly, Vector3.back, -rect.yMax);
                    if (n.y > 0.9f) AddPoly(verts, tris, poly.Select(p => p + Vector3.up * Lift).ToList());
                    else if (Mathf.Abs(n.y) < 0.3f)
                    {
                        // the visible part of the edge: from the pavers on the terrain up to the pavers on the road
                        poly = Clip(poly, Vector3.up, TerrainAt(a.x, a.z) + Lift - 0.01f); poly = Clip(poly, Vector3.down, -(Mathf.Max(a.y, Mathf.Max(b.y, c.y)) + Lift));
                        AddPoly(kv, kt, poly.Select(p => p + n * 0.01f).ToList());
                    }
                }
            }
        }
        var uvs = verts.Select(v => new Vector2(v.x, v.z) / 2.4f).ToList();
        var go = MeshObject(group, "Pavers", verts, tris, Mat("ParkAlleyPavers", "Legacy Shaders/Diffuse", "ParkAlleyPavers", PaverTint), uvs);
        go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (kv.Count > 0)
        {
            var kuv = kv.Select(v => new Vector2(v.x + v.z, v.y) / 1.6f).ToList();
            MeshObject(group, "Kerb", kv, kt, Mat("ParkAlleyConcrete", "Legacy Shaders/Diffuse", "ParkAlleyRender", ConcreteTint), kuv)
                .GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    static readonly Color PaverTint = new Color(0.86f, 0.84f, 0.82f);
    static readonly Color ConcreteTint = new Color(0.70f, 0.68f, 0.64f);

    static float TerrainAt(float x, float z)
    {
        foreach (var h in Physics.RaycastAll(new Vector3(x, 9f, z), Vector3.down, 9f, ~0, QueryTriggerInteraction.Ignore))
            if (h.collider is TerrainCollider) return h.point.y;
        return GroundAt(x, z);
    }

    // keep the part of a convex polygon where dot(p, n) >= d
    static List<Vector3> Clip(List<Vector3> poly, Vector3 n, float d)
    {
        var o = new List<Vector3>();
        for (int i = 0; i < poly.Count; i++)
        {
            Vector3 p = poly[i], q = poly[(i + 1) % poly.Count];
            float dp = Vector3.Dot(p, n) - d, dq = Vector3.Dot(q, n) - d;
            if (dp >= 0) o.Add(p);
            if ((dp >= 0) != (dq >= 0)) o.Add(Vector3.Lerp(p, q, dp / (dp - dq)));
        }
        return o;
    }

    static void AddPoly(List<Vector3> v, List<int> t, List<Vector3> poly)
    {
        if (poly.Count < 3) return;
        int b = v.Count; v.AddRange(poly);
        for (int i = 1; i + 1 < poly.Count; i++) t.AddRange(new[] { b, b + i, b + i + 1 });
    }

    // ── props ──

    static void Props(Transform group, List<string> report)
    {
        var bits = Mat("ParkAlleyBits", "Legacy Shaders/Diffuse", "ParkAlleyBits", Color.white);
        var manhole = Mat("ParkAlleyManhole", "Legacy Shaders/Diffuse", "ParkAlleyManhole", Color.white);
        var concrete = Mat("ParkAlleyConcrete", "Legacy Shaders/Diffuse", "ParkAlleyRender", ConcreteTint);
        var soil = Mat("ParkAlleySoil", "Legacy Shaders/Diffuse", null, new Color(0.24f, 0.20f, 0.16f));
        var black = Mat("ParkAlleyConduit", "Legacy Shaders/Diffuse", null, new Color(0.07f, 0.07f, 0.07f));
        var graffiti = Mat("ParkAlleyGraffiti", "Legacy Shaders/Transparent/Cutout/Diffuse", "ParkAlleyGraffiti", Color.white);
        graffiti.SetFloat("_Cutoff", 0.45f);
        var props = new GameObject("Props").transform; props.SetParent(group, false);

        // manhole covers: two down the lane (long side along it), one in the forecourt
        int covers = 0;
        foreach (var (x, z, alongZ) in new[] { (-229.215f, 88.5f, true), (-229.215f, 97.6f, true), (-231.6f, 71.4f, false) })
        {
            float y = GroundAt(x, z) + Lift;
            var rot = Quaternion.Euler(0, alongZ ? 90f : 0f, 0);
            if (Physics.CheckBox(new Vector3(x, y + 0.3f, z), new Vector3(0.48f, 0.25f, 0.36f), rot, ~0, QueryTriggerInteraction.Ignore)) { report.Add("manhole at " + x + "," + z + " blocked - skipped"); continue; }
            // top = the whole texture, the 1.5 cm rim = its concrete edge
            Box(props, "Manhole " + covers++, new Vector3(x, y + 0.0075f, z), new Vector3(0.93f, 0.015f, 0.70f), rot, manhole,
                new Rect(0, 0, 1, 1), new Rect(0.01f, 0.4f, 0.03f, 0.2f), top: true);
        }

        // west wall (shop backs): a conduit from the ground to the roof edge, a window with an exhaust fan up high
        float wPlane = WallPlane(new Vector3(LaneX, 4f, 91.3f), Vector3.left);
        Conduit(props, "Conduit West", new Vector3(-wPlane + 0.05f, 0, 91.3f), GroundAt(-230.0f, 91.3f), WallTop(new Vector3(LaneX, 4f, 91.3f), Vector3.left, wPlane) - 0.03f, black);
        float wWin = WallPlane(new Vector3(LaneX, 6.4f, 84.2f), Vector3.left);
        Box(props, "Fan Window West", new Vector3(-wWin + 0.025f, 6.4f, 84.2f), new Vector3(0.62f, 1.3f, 0.05f), Quaternion.LookRotation(Vector3.right), bits,
            new Rect(0, 0, 0.5f, 1), new Rect(0.02f, 0.4f, 0.03f, 0.2f));

        // east wall (house): a meter box with its conduit, a fan window in the upper wall
        float ePlane = WallPlane(new Vector3(LaneX, 3.6f, 99.2f), Vector3.right);
        float eg = GroundAt(-228.4f, 99.2f);
        var boxC = new Vector3(ePlane - 0.09f - 0.015f, eg + 1.55f, 99.2f);
        Box(props, "Meter Box East", boxC, new Vector3(0.48f, 0.62f, 0.18f), Quaternion.LookRotation(Vector3.left), bits,
            new Rect(0.5f, 0, 0.5f, 1), new Rect(0.53f, 0.3f, 0.03f, 0.4f));
        Conduit(props, "Conduit East low", new Vector3(ePlane - 0.05f, 0, 99.05f), eg, boxC.y - 0.31f, black);
        Conduit(props, "Conduit East high", new Vector3(ePlane - 0.05f, 0, 99.05f), boxC.y + 0.31f, WallTop(new Vector3(LaneX, 3.6f, 99.05f), Vector3.right, ePlane) - 0.02f, black);
        float eUp = WallPlane(new Vector3(LaneX, 5.9f, 88f), Vector3.right);
        if (eUp != 0) Box(props, "Fan Window East", new Vector3(eUp - 0.025f, 5.9f, 88f), new Vector3(0.62f, 1.3f, 0.05f), Quaternion.LookRotation(Vector3.left), bits,
            new Rect(0, 0, 0.5f, 1), new Rect(0.02f, 0.4f, 0.03f, 0.2f));

        // the salon's side over the forecourt: graffiti, the planter with a shrub, a concrete stub, a meter box + conduit
        float sPlane = WallPlane(new Vector3(-232.6f, 4f, 74.0f), Vector3.forward);    // z of the face
        float sg = GroundAt(-232.6f, sPlane - 0.3f);
        var faceOut = Quaternion.LookRotation(Vector3.back);
        MeshObject(props, "Graffiti", new List<Vector3> { new Vector3(-231.3f, sg + 0.15f, sPlane - 0.025f), new Vector3(-234.5f, sg + 0.15f, sPlane - 0.025f), new Vector3(-234.5f, sg + 3.35f, sPlane - 0.025f), new Vector3(-231.3f, sg + 3.35f, sPlane - 0.025f) },
            new List<int> { 0, 1, 2, 0, 2, 3 }, graffiti, new List<Vector2> { new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1) });
        var planter = Box(props, "Planter", new Vector3(-233.0f, sg + 0.2f, sPlane - 0.215f), new Vector3(0.80f, 0.40f, 0.42f), faceOut, concrete, null, null);
        planter.AddComponent<BoxCollider>();
        Box(props, "Planter Soil", new Vector3(-233.0f, sg + 0.38f, sPlane - 0.215f), new Vector3(0.68f, 0.03f, 0.30f), faceOut, soil, null, null);
        var bushMeshes = AssetDatabase.LoadAllAssetsAtPath(BushFbx).OfType<Mesh>().OrderBy(m => m.name).ToArray();
        var bushMat = AssetDatabase.LoadAssetAtPath<Material>(BushMat);
        if (bushMeshes.Length > 0 && bushMat != null)
        {
            var shrub = new GameObject("Planter Shrub");
            shrub.transform.SetParent(props, false);
            shrub.transform.position = new Vector3(-233.0f, sg + 0.42f, sPlane - 0.36f);
            shrub.transform.rotation = faceOut;
            shrub.transform.localScale = Vector3.one * 0.3f;
            shrub.AddComponent<MeshFilter>().sharedMesh = bushMeshes.Last();
            shrub.AddComponent<MeshRenderer>().sharedMaterial = bushMat;
        }
        var stub = Box(props, "Concrete Stub", new Vector3(-232.15f, sg + 0.28f, sPlane - 0.19f), new Vector3(0.34f, 0.56f, 0.38f), faceOut, concrete, null, null);
        stub.AddComponent<BoxCollider>();
        var sBox = new Vector3(-230.95f, sg + 1.5f, sPlane - 0.105f);
        Box(props, "Meter Box Salon", sBox, new Vector3(0.48f, 0.62f, 0.18f), faceOut, bits, new Rect(0.5f, 0, 0.5f, 1), new Rect(0.53f, 0.3f, 0.03f, 0.4f));
        Conduit(props, "Conduit Salon", new Vector3(-230.8f, 0, sPlane - 0.05f), sBox.y + 0.31f, WallTop(new Vector3(-230.8f, 4f, 74.0f), Vector3.forward, sPlane) - 0.03f, black);
        Conduit(props, "Conduit Salon low", new Vector3(-230.8f, 0, sPlane - 0.05f), sg, sBox.y - 0.31f, black);
        report.Add("manholes " + covers + ", planes west " + (-wPlane).ToString("F2") + " east " + ePlane.ToString("F2") + " east upper " + eUp.ToString("F2") + " salon side z " + sPlane.ToString("F2"));
    }

    // the plane coordinate (along dir) of the wall a ray from p meets; 0 if none
    static float WallPlane(Vector3 p, Vector3 dir) => Probe(p, dir, out float plane) == Wall ? plane : 0f;

    static float WallTop(Vector3 p, Vector3 dir, float plane)
    {
        float y = p.y;
        while (y < YTop && OnPlane(new Vector3(p.x, y + 0.25f, p.z), dir, plane)) y += 0.25f;
        Vector3 q = p;
        return Refine(y, y + 0.25f, t => OnPlane(new Vector3(q.x, t, q.z), dir, plane));
    }

    // a black conduit up a wall (8 sides, 2.2 cm radius) with a clamp every 1.4 m
    static void Conduit(Transform parent, string name, Vector3 xz, float y0, float y1, Material mat)
    {
        if (y1 - y0 < 0.1f) return;
        var verts = new List<Vector3>(); var tris = new List<int>();
        const int sides = 8; const float r = 0.022f;
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * r, d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * r;
            Vector3 b = new Vector3(xz.x, y0, xz.z), t = new Vector3(xz.x, y1, xz.z);
            Quad(verts, tris, b + d1, b + d0, t + d0, t + d1, (d0 + d1).normalized);
        }
        for (float y = y0 + 0.7f; y < y1 - 0.3f; y += 1.4f)
            AddBox(verts, tris, new Vector3(xz.x, y, xz.z), new Vector3(0.07f, 0.035f, 0.07f));
        MeshObject(parent, name, verts, tris, mat);
    }

    // ── mesh helpers ──

    static void Quad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, bool twoSided = false)
    {
        // wind so the face looks along `normal`
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0) { var s = b; b = d; d = s; }
        int i = v.Count;
        v.AddRange(new[] { a, b, c, d });
        t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        if (twoSided) Quad(v, t, a, d, c, b, -normal);
    }

    static void AddBox(List<Vector3> v, List<int> t, Vector3 c, Vector3 s)
    {
        Vector3 h = s * 0.5f;
        Vector3[] p = { c + new Vector3(-h.x, -h.y, -h.z), c + new Vector3(h.x, -h.y, -h.z), c + new Vector3(h.x, h.y, -h.z), c + new Vector3(-h.x, h.y, -h.z),
                        c + new Vector3(-h.x, -h.y, h.z), c + new Vector3(h.x, -h.y, h.z), c + new Vector3(h.x, h.y, h.z), c + new Vector3(-h.x, h.y, h.z) };
        Quad(v, t, p[0], p[1], p[2], p[3], Vector3.back); Quad(v, t, p[5], p[4], p[7], p[6], Vector3.forward);
        Quad(v, t, p[4], p[0], p[3], p[7], Vector3.left); Quad(v, t, p[1], p[5], p[6], p[2], Vector3.right);
        Quad(v, t, p[3], p[2], p[6], p[7], Vector3.up); Quad(v, t, p[4], p[5], p[1], p[0], Vector3.down);
    }

    // a box in its own frame (local +Z = out of the wall, or up for `top` boxes): `face` UVs on the front (or top), `side`
    // UVs on the rest; null = UVs by size (1.6 m per tile, for plaster / concrete)
    static GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size, Quaternion rot, Material mat, Rect? face, Rect? side, bool top = false)
    {
        var verts = new List<Vector3>(); var tris = new List<int>(); var uvs = new List<Vector2>();
        Vector3 h = size * 0.5f;
        void F(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, bool front)
        {
            Quad(verts, tris, a, b, c, d, n);
            // Quad may swap b and d; give UVs by position within the face
            for (int k = verts.Count - 4; k < verts.Count; k++)
            {
                Vector3 p = verts[k];
                // the face's own right / up as seen from outside it
                Vector3 right = Mathf.Abs(n.y) < 0.5f ? Vector3.Cross(n, Vector3.up).normalized : Vector3.right;
                Vector3 up = Vector3.Cross(right, n);
                if (top && Mathf.Abs(n.y) > 0.5f) { right = Vector3.right; up = Vector3.forward; }
                float u = Vector3.Dot(p, right) / (Mathf.Abs(Vector3.Dot(size, right)) > 0 ? Mathf.Abs(Vector3.Dot(size, right)) : 1) + 0.5f;
                float vv = Vector3.Dot(p, up) / (Mathf.Abs(Vector3.Dot(size, up)) > 0 ? Mathf.Abs(Vector3.Dot(size, up)) : 1) + 0.5f;
                Rect? rr = front ? face : side;
                if (rr.HasValue) uvs.Add(new Vector2(rr.Value.x + u * rr.Value.width, rr.Value.y + vv * rr.Value.height));
                else uvs.Add(new Vector2(u * Mathf.Abs(Vector3.Dot(size, right)), vv * Mathf.Abs(Vector3.Dot(size, up))) / 1.6f);
            }
        }
        Vector3[] p = { new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z),
                        new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z) };
        F(p[5], p[4], p[7], p[6], Vector3.forward, !top);
        F(p[0], p[1], p[2], p[3], Vector3.back, false);
        F(p[4], p[0], p[3], p[7], Vector3.left, false);
        F(p[1], p[5], p[6], p[2], Vector3.right, false);
        F(p[3], p[2], p[6], p[7], Vector3.up, top);
        F(p[4], p[5], p[1], p[0], Vector3.down, false);
        var go = MeshObject(parent, name, verts, tris, mat, uvs, false);
        go.transform.position = centre; go.transform.rotation = rot;
        return go;
    }

    static GameObject MeshObject(Transform parent, string name, List<Vector3> verts, List<int> tris, Material mat, List<Vector2> uvs = null, bool worldSpace = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity; go.transform.localScale = Vector3.one;
        // world-space vertices relative to the object's (identity, origin) placement under any parent
        var mesh = new Mesh { name = "ParkAlley_" + name.Replace(" ", "").Replace("(", "_").Replace(")", "") };
        mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        if (uvs != null) mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        string path = MeshDir + "/" + mesh.name + ".asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }
}
