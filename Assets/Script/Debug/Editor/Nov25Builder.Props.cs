using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 2026-11-25 list, scene props: 18 planks, 19 wires, 23 gates, 30 tanks, 31 ladder, 42 alley 2 exit, 49 wrapper, 58 boxes, 59 OFF switch
public static partial class Nov25Builder
{
    const string TexDir = "Assets/Textures/Wave2/", MeshDir2 = "Assets/Models/Wave2/", MatDir = "Assets/Materials/Wave2/";

    static Material TexMat(string name, string tex, float tile = 1f, bool transparent = false)
    {
        Directory.CreateDirectory(MatDir);
        string tp = TexDir + tex + ".png";
        var ti = (TextureImporter)AssetImporter.GetAtPath(tp);
        if (ti != null && ti.filterMode != FilterMode.Point)
        {
            ti.filterMode = FilterMode.Point; ti.mipmapEnabled = true; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.alphaIsTransparency = transparent; ti.SaveAndReimport();
        }
        string p = MatDir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, p); }
        m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
        m.mainTextureScale = new Vector2(tile, tile);
        m.SetFloat("_Glossiness", 0.12f); m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Mesh SaveMesh(Mesh m, string name)
    {
        Directory.CreateDirectory(MeshDir2);
        string p = MeshDir2 + name + ".asset";
        m.name = name;
        var e = AssetDatabase.LoadAssetAtPath<Mesh>(p);
        if (e != null) { e.Clear(); EditorUtility.CopySerialized(m, e); EditorUtility.SetDirty(e); return e; }
        AssetDatabase.CreateAsset(m, p);
        return m;
    }

    static Mesh Tube(IList<Vector3> pts, float radius, int sides)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        float len = 0f;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 d = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
            Vector3 a = Vector3.Cross(d, Mathf.Abs(d.y) > 0.9f ? Vector3.right : Vector3.up).normalized, b = Vector3.Cross(d, a);
            if (i > 0) len += (pts[i] - pts[i - 1]).magnitude;
            for (int k = 0; k <= sides; k++)
            {
                float t = k * Mathf.PI * 2f / sides;
                v.Add(pts[i] + (a * Mathf.Cos(t) + b * Mathf.Sin(t)) * radius);
                uv.Add(new Vector2(k / (float)sides, len * 2f));
            }
        }
        int w = sides + 1;
        for (int i = 0; i + 1 < pts.Count; i++)
            for (int k = 0; k < sides; k++)
            {
                int q = i * w + k;
                tri.AddRange(new[] { q, q + 1, q + w, q + 1, q + w + 1, q + w });
            }
        var m = new Mesh();
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
        // outward
        var n = m.normals;
        if (n.Length > 0 && Vector3.Dot(n[0], v[0] - pts[0]) < 0f) { tri.Reverse(); m.SetTriangles(tri, 0); m.RecalculateNormals(); }
        return m;
    }

    static List<Vector3> Sag(Vector3 a, Vector3 b, float sag, int n = 10)
    {
        var l = new List<Vector3>();
        for (int i = 0; i <= n; i++) { float t = i / (float)n; l.Add(Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1f - t)); }
        return l;
    }

    static GameObject MeshGo(string name, Transform parent, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static GameObject BoxGo(string name, Transform parent, Vector3 worldPos, Quaternion rot, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name; go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(worldPos, rot);
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static float GroundY(Vector3 p, float up = 2.5f)
    {
        float best = float.NegativeInfinity;
        foreach (var h in Physics.RaycastAll(p + Vector3.up * up, Vector3.down, 14f, ~0, QueryTriggerInteraction.Ignore))
            if (h.collider.GetComponentInParent<Enemy>() == null && h.collider.GetComponentInParent<Player>() == null && h.point.y > best) best = h.point.y;
        return best > -900f ? best : p.y;
    }

    static Transform Fresh(string parentPath, string name)
    {
        var parent = AgentTools.Find(parentPath);
        var old = parent.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = Vector3.zero; t.rotation = Quaternion.identity;
        Dirty(t.gameObject);
        return t;
    }

    // ------------------------------------------------------------------ 31

    public static string Ladder()
    {
        var old = AgentTools.Find("Scene/Park/BUILDING 1/pCylinder21");
        var mf = old.GetComponent<MeshFilter>();
        var vs = mf.sharedMesh.vertices.Select(x => old.TransformPoint(x)).ToArray();
        float y0 = vs.Min(a => a.y), y1 = vs.Max(a => a.y);
        // the two rails, at the bottom and at the top
        Vector3 c = Vector3.zero; foreach (var a in vs) c += a; c /= vs.Length;
        float sxx = 0, szz = 0, sxz = 0;
        foreach (var a in vs.Where(a => a.y < y0 + 0.15f || a.y > y1 - 0.15f)) { float dx = a.x - c.x, dz = a.z - c.z; sxx += dx * dx; szz += dz * dz; sxz += dx * dz; }
        float ang = 0.5f * Mathf.Atan2(2f * sxz, sxx - szz);
        Vector3 axis = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
        Vector3 Rail(bool top, float s)
        {
            var g = vs.Where(a => (top ? a.y > y1 - 0.15f : a.y < y0 + 0.15f)).ToArray();
            float mid = (g.Min(a => Vector3.Dot(a, axis)) + g.Max(a => Vector3.Dot(a, axis))) * 0.5f;
            var h = g.Where(a => Mathf.Sign(Vector3.Dot(a, axis) - mid) == s).ToArray();
            Vector3 r = Vector3.zero; foreach (var a in h) r += a; return r / Mathf.Max(1, h.Length);
        }
        Vector3 b0 = Rail(false, -1), b1 = Rail(false, 1), t0 = Rail(true, -1), t1 = Rail(true, 1);
        var mat = TexMat("LadderYellow", "LadderYellow", 3f);
        var root = Fresh("Environment7", "Park Ladder (yellow)");
        void Bar(string n, Vector3 a, Vector3 b, float thick) =>
            BoxGo(n, root, (a + b) * 0.5f, Quaternion.LookRotation(b - a, Vector3.Cross(axis, Vector3.up)), new Vector3(thick, thick, (b - a).magnitude), mat);
        Bar("Rail L", b0, t0, 0.1f); Bar("Rail R", b1, t1, 0.1f);
        float len = (t0 - b0).magnitude;
        int rungs = Mathf.RoundToInt(len / 0.3f);
        for (int i = 1; i < rungs; i++)
        {
            float u = i / (float)rungs;
            Bar("Rung " + i, Vector3.Lerp(b0, t0, u), Vector3.Lerp(b1, t1, u), 0.065f);
        }
        old.GetComponent<MeshRenderer>().enabled = false;       // its collider stays
        Dirty(old.gameObject);
        return "ladder " + b0 + b1 + " to " + t0 + t1 + " width " + (b1 - b0).magnitude;
    }

    // ------------------------------------------------------------------ 59

    public static string OffSwitch()
    {
        var root = Fresh("Alley 2 Gameplay", "OFF Switch Board");
        var sw = AgentTools.Find("Environment7/BREAKER SWITCH").GetComponent<Renderer>().bounds;
        float wallX = -46.0f;
        if (Physics.Raycast(new Vector3(sw.center.x - 1.5f, sw.center.y + 0.8f, sw.center.z), Vector3.right, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore)) wallX = hit.point.x;
        var board = TexMat("OffBoard", "OffBoard");
        var steel = PlainMat("SwitchSteel", new Color(0.2f, 0.2f, 0.19f), 0.25f);
        var rubber = PlainMat("CableBlack", new Color(0.05f, 0.05f, 0.05f), 0.15f);
        // a steel plate on the wall behind the switch, the board above it with the arrow pointing down at it
        BoxGo("Plate", root, new Vector3(wallX - 0.015f, sw.center.y + 0.4f, sw.center.z), Quaternion.identity, new Vector3(0.03f, 1.75f, 0.86f), steel);
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.name = "Board"; q.transform.SetParent(root, false);
        q.transform.position = new Vector3(wallX - 0.036f, sw.center.y + 0.72f, sw.center.z);
        q.transform.rotation = Quaternion.LookRotation(Vector3.right);
        q.transform.localScale = new Vector3(0.78f, 1.04f, 1f);
        q.GetComponent<Renderer>().sharedMaterial = board;
        // conduit: from the box along the wall and down the alley to the broken cabinet that sparks
        var cab = AgentTools.Find("Environment7/Electric Cabinet").position;
        Vector3 from = new Vector3(wallX - 0.05f, sw.center.y - 0.32f, sw.center.z);
        Vector3 end = new Vector3(wallX - 0.05f, sw.center.y - 0.25f, cab.z + 0.8f);
        var pts = new List<Vector3> { from + Vector3.up * 0.2f, from };
        int seg = Mathf.CeilToInt(Mathf.Abs(end.z - from.z) / 2.2f);
        for (int i = 0; i < seg; i++)
        {
            var s = Sag(Vector3.Lerp(from, end, i / (float)seg), Vector3.Lerp(from, end, (i + 1) / (float)seg), 0.1f, 6);
            s.RemoveAt(0); pts.AddRange(s);
        }
        MeshGo("Cable", root, SaveMesh(Tube(pts, 0.03f, 6), "OffSwitchCable"), rubber);
        for (int i = 0; i <= seg; i++) BoxGo("Clip", root, Vector3.Lerp(from, end, i / (float)seg) + Vector3.right * 0.02f, Quaternion.identity, new Vector3(0.07f, 0.05f, 0.06f), steel);
        // a small red light over the board, so it is the one thing glowing at that end of the alley
        var lamp = new GameObject("Red lamp");
        lamp.transform.SetParent(root, false);
        lamp.transform.position = new Vector3(wallX - 0.16f, sw.center.y + 1.36f, sw.center.z);
        var glass = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(glass.GetComponent<Collider>());
        glass.name = "Bulb"; glass.transform.SetParent(lamp.transform, false); glass.transform.localScale = new Vector3(0.12f, 0.14f, 0.12f);
        var red = PlainMat("SwitchLampRed", new Color(0.8f, 0.06f, 0.03f), 0.5f);
        red.EnableKeyword("_EMISSION"); red.SetColor("_EmissionColor", new Color(2.2f, 0.12f, 0.06f)); EditorUtility.SetDirty(red);
        glass.GetComponent<Renderer>().sharedMaterial = red;
        BoxGo("Base", lamp.transform, lamp.transform.position + Vector3.right * 0.09f, Quaternion.identity, new Vector3(0.08f, 0.16f, 0.16f), steel);
        var l = lamp.AddComponent<Light>();
        l.type = LightType.Point; l.range = 4.2f; l.intensity = 1.6f; l.color = new Color(1f, 0.16f, 0.1f); l.shadows = LightShadows.None;
        return "board at x " + wallX + " y " + sw.center.y;
    }

    // ------------------------------------------------------------------ 19

    public static string WhisperWires()
    {
        var root = Fresh("Alley 3 Gameplay/WallHoleInteractable", "Wires To The Door");
        const float wallZ = -76.44f;
        Vector3 hole = new Vector3(-18.61f, 3.13f, wallZ);
        // the lock plate on the sliding gate
        Vector3 plate = new Vector3(-16.55f, 2.95f, -75.1f);
        float gateX = -16.35f;
        // the box hangs on the wall beside the latch, not on the gate: the gate slides
        plate = new Vector3(-16.95f, 2.62f, wallZ + 0.09f);
        var red = PlainMat("WireRed", new Color(0.55f, 0.05f, 0.04f), 0.3f);
        var black = PlainMat("CableBlack", new Color(0.05f, 0.05f, 0.05f), 0.15f);
        var yellow = PlainMat("WireYellow", new Color(0.72f, 0.55f, 0.08f), 0.3f);
        var steel = PlainMat("SwitchSteel", new Color(0.2f, 0.2f, 0.19f), 0.25f);
        Vector3 corner = new Vector3(-17.35f, 2.8f, wallZ + 0.05f);
        var mats = new[] { red, black, yellow };
        for (int i = 0; i < 3; i++)
        {
            float off = (i - 1) * 0.07f;
            Vector3 start = hole + new Vector3(0.12f, -0.12f + off * 0.6f, -0.18f), rim = hole + new Vector3(0.34f, -0.05f + off, 0.05f);
            Vector3 mid = Vector3.Lerp(rim, corner, 0.5f) + Vector3.up * off;
            Vector3 cor = corner + Vector3.up * off, pl = plate + new Vector3(-0.1f, 0.08f + off, 0f);
            var pts = new List<Vector3> { start, Vector3.Lerp(start, rim, 0.6f) + Vector3.forward * 0.06f };
            pts.AddRange(Sag(rim, mid, 0.16f + i * 0.05f, 8)); pts.AddRange(Sag(mid, cor, 0.13f + i * 0.04f, 8)); pts.AddRange(Sag(cor, pl, 0.14f + i * 0.04f, 8));
            MeshGo("Wire " + i, root, SaveMesh(Tube(pts, 0.022f, 5), "WhisperWire" + i), mats[i]);
        }
        BoxGo("Clip", root, Vector3.Lerp(hole + Vector3.right * 0.34f, corner, 0.5f) + Vector3.back * 0.03f, Quaternion.identity, new Vector3(0.07f, 0.26f, 0.06f), steel);
        BoxGo("Clip", root, corner + Vector3.back * 0.04f, Quaternion.identity, new Vector3(0.1f, 0.28f, 0.08f), steel);
        // the little box on the door they go into, with a red light: whoever is behind the hole works the lock
        BoxGo("Box", root, plate, Quaternion.identity, new Vector3(0.3f, 0.42f, 0.18f), steel);
        // a conduit on from it to the latch
        BoxGo("Conduit", root, new Vector3((plate.x + gateX) * 0.5f, plate.y - 0.1f, plate.z - 0.02f), Quaternion.identity, new Vector3(gateX - plate.x + 0.1f, 0.06f, 0.06f), steel);
        var led = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(led.GetComponent<Collider>());
        led.name = "Lamp"; led.transform.SetParent(root, false); led.transform.position = plate + new Vector3(0f, 0.1f, 0.1f); led.transform.localScale = Vector3.one * 0.075f;
        led.GetComponent<Renderer>().sharedMaterial = PlainMat("SwitchLampRed", new Color(0.8f, 0.06f, 0.03f), 0.5f);
        // the box and its wires ride with the gate when it slides? they are on the post side: the gate is shut whenever he can read them
        return "wires to " + plate;
    }

    // ------------------------------------------------------------------ 18

    public static string SecondBridgePlanks()
    {
        var root = Fresh("Between Alley 2-3 Gameplay/Props", "Second Hole Planks");
        var src = AgentTools.Find("Between Alley 2-3 Gameplay/Props/WoodenPlank (1)");
        var spots = new[] { (new Vector3(80.4f, 0f, -70.2f), 84f, 7f), (new Vector3(79.2f, 0f, -72.6f), 61f, -9f), (new Vector3(80.6f, 0f, -74.2f), 112f, 12f), (new Vector3(77.6f, 0f, -70.9f), 20f, -6f) };
        var sb = "";
        foreach (var (p, yaw, roll) in spots)
        {
            var go = Object.Instantiate(src.gameObject, root);
            go.name = "WoodenPlank (second hole)";
            foreach (var c in go.GetComponentsInChildren<Component>(true)) if (c is UniqueID || c is SaveablePickup) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var l in go.GetComponentsInChildren<LightPoolDecal>(true)) Object.DestroyImmediate(l.gameObject);
            var rs = go.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.Euler(0f, yaw, roll) * src.rotation;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float y = GroundY(new Vector3(p.x, 0f, p.z), 0.6f);
            go.transform.position += new Vector3(p.x, y + 0.12f + b.extents.y * 0.5f, p.z) - b.center;
            sb += " " + y.ToString("F1");
        }
        return "hole floor" + sb;
    }

    // ------------------------------------------------------------------ 30

    public static string TankColliders()
    {
        var s = "";
        foreach (var n in new[] { "Scene/Park/TANK", "Scene/Park/TANK (1)" })
        {
            var t = AgentTools.Find(n);
            var box = t.GetComponent<BoxCollider>();
            if (box != null) Object.DestroyImmediate(box);
            var mc = t.GetComponent<MeshCollider>() ?? t.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = t.GetComponent<MeshFilter>().sharedMesh; mc.convex = false;
            s += n + " tris " + mc.sharedMesh.triangles.Length / 3 + "; ";
            Dirty(t.gameObject);
        }
        return s;
    }

    // ------------------------------------------------------------------ 42

    public static string Alley2Exit()
    {
        var d = AgentTools.Find("Alley 2 Gameplay/DoorInteractable (Entrance) (2)").GetComponent<DoorInteractable>();
        Undo.RecordObject(d.enterPoint, "exit");
        d.enterPoint.rotation = Quaternion.Euler(0f, 270f, 0f);       // out of the alley = away from the gate (it faced back into it)
        Dirty(d.gameObject);
        return "enter " + d.enterPoint.position + " yaw " + d.enterPoint.eulerAngles.y + ", exit yaw " + d.exitPoint.eulerAngles.y;
    }

    // ------------------------------------------------------------------ 23

    public static string GateLooks()
    {
        var root = Fresh("------------Gameplay------------", "Gate Looks");
        var sheet = TexMat("SealedSheet", "SealedSheet", 1f);
        var steel = PlainMat("GateChain", new Color(0.27f, 0.2f, 0.15f), 0.2f);
        var yellow = TexMat("LadderYellow", "LadderYellow", 3f);
        var log = "";
        foreach (var d in Object.FindObjectsByType<DoorInteractable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            string path = AgentTools.PathOf(d.transform);
            if (!path.Contains("/Alley ") || path.Contains("CombinationLock") || path.Contains("Whispering") || path.Contains("Blocks") || path.Contains("BackMain")) continue;
            var ts = d.GetComponentsInChildren<DoorInteractableTrigger>(true);
            if (ts.Length < 2) continue;
            DoorInteractableTrigger enter = null, exit = null;
            foreach (var t in ts) { if (new SerializedObject(t).FindProperty("doorTriggerType").enumValueIndex == 0) enter = t; else exit = t; }
            if (enter == null || exit == null) continue;
            // the gate itself: the mesh panel between the two triggers
            Vector3 mid = (enter.transform.position + exit.transform.position) * 0.5f;
            Renderer panel = null; float best = 9f;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.name.StartsWith("transform3") && !r.name.StartsWith("pCube8")) continue;
                float q = (r.bounds.ClosestPoint(mid + Vector3.up) - mid - Vector3.up).sqrMagnitude;
                if (q < best) { best = q; panel = r; }
            }
            if (panel == null || best > 2f) { log += "no gate: " + d.name + "\n"; continue; }
            Vector3 c = panel.bounds.center;
            Vector3 n = panel.bounds.size.x < panel.bounds.size.z ? Vector3.right : Vector3.forward;
            Vector3 along = Vector3.Cross(Vector3.up, n);
            float ground = GroundY(new Vector3(c.x, mid.y, c.z) + n * 0.5f, 0.6f);
            Vector3 foot = new Vector3(c.x, ground, c.z);
            float panelW = Mathf.Max(panel.bounds.size.x, panel.bounds.size.z);

            bool oneWay = d.oneWayUntilOpened, sealedFor = !oneWay && enter.gameObject.activeSelf != exit.gameObject.activeSelf;
            bool key = d.requiresKey;
            if (!oneWay && !sealedFor && !key) { log += "plain: " + path + "\n"; continue; }
            var holder = new GameObject((key ? "Locked - " : oneWay ? "One way - " : "Sealed - ") + d.name).transform;
            holder.SetParent(root, false);
            var look = holder.gameObject.AddComponent<DoorLook>();
            var so = new SerializedObject(look);
            so.FindProperty("door").objectReferenceValue = d;
            if (key)
            {
                var ch = new GameObject("Chain").transform; ch.SetParent(holder, false);
                Chain(ch, foot + Vector3.up * 1.15f);
                var cr = ch.GetChild(0);
                cr.rotation = n == Vector3.right ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
                cr.localScale = Vector3.one * 2.6f;
                // the lock's own plate, painted red, on both sides: this one wants a key
                var redPlate = PlainMat("LockPlateRed", new Color(0.5f, 0.07f, 0.05f), 0.25f);
                var hole = PlainMat("CableBlack", new Color(0.05f, 0.05f, 0.05f), 0.15f);
                foreach (float s in new[] { 1f, -1f })
                {
                    var pr = Quaternion.LookRotation(n * s, Vector3.up);
                    Vector3 at = foot + Vector3.up * 1.5f + n * s * 0.13f;
                    BoxGo("Lock plate", ch, at, pr, new Vector3(0.5f, 0.42f, 0.05f), redPlate);
                    BoxGo("Keyhole", ch, at + n * s * 0.028f, pr, new Vector3(0.05f, 0.11f, 0.01f), hole);
                    BoxGo("Keyhole", ch, at + n * s * 0.028f + Vector3.up * 0.05f, pr * Quaternion.Euler(0f, 0f, 45f), new Vector3(0.075f, 0.075f, 0.01f), hole);
                }
                so.FindProperty("whileLocked").objectReferenceValue = ch.gameObject;
            }
            else
            {
                var blocked = oneWay ? (d.blockedSide == DoorTriggerType.Enter ? enter : exit) : (enter.gameObject.activeSelf ? exit : enter);
                float side = Mathf.Sign(Vector3.Dot(blocked.transform.position - c, n));
                Vector3 b = n * side;
                var seal = new GameObject("Sealed side").transform; seal.SetParent(holder, false);
                // sheet metal over the seam, two welded straps across it and their bolts: nothing to pull on this side
                var rot = Quaternion.LookRotation(b, Vector3.up);
                var mesh = SaveMesh(SheetMesh(), "SealedSheet");
                var sh = MeshGo("Sheet", seal, mesh, sheet);
                sh.transform.SetPositionAndRotation(foot + Vector3.up * 1.2f + b * 0.17f, rot);
                sh.transform.localScale = new Vector3(Mathf.Min(2.3f, panelW * 0.62f), 1.75f, 1f);
                foreach (float y in new[] { 0.75f, 1.65f })
                {
                    BoxGo("Strap", seal, foot + Vector3.up * y + b * 0.2f, rot * Quaternion.Euler(0f, 0f, y < 1f ? 1.2f : -1.4f), new Vector3(panelW * 0.98f, 0.12f, 0.03f), steel);
                    foreach (float u in new[] { -0.42f, -0.14f, 0.14f, 0.42f })
                        BoxGo("Bolt", seal, foot + Vector3.up * y + b * 0.225f + along * u * Mathf.Min(2.9f, panelW * 0.8f), rot * Quaternion.Euler(0f, 0f, 30f), new Vector3(0.05f, 0.05f, 0.03f), steel);
                }
                if (oneWay) so.FindProperty("untilOpened").objectReferenceValue = seal.gameObject;
                // the other side: a yellow slide bolt, where he opens it
                var bolt = new GameObject("Slide bolt").transform; bolt.SetParent(holder, false);
                BoxGo("Bar", bolt, foot + Vector3.up * 1.1f - b * 0.16f, rot, new Vector3(0.95f, 0.075f, 0.05f), yellow);
                BoxGo("Handle", bolt, foot + Vector3.up * 1.1f - b * 0.23f + along * 0.22f, rot, new Vector3(0.05f, 0.05f, 0.16f), yellow);
                BoxGo("Bracket", bolt, foot + Vector3.up * 1.1f - b * 0.15f - along * 0.3f, rot, new Vector3(0.1f, 0.15f, 0.07f), steel);
                BoxGo("Bracket", bolt, foot + Vector3.up * 1.1f - b * 0.15f + along * 0.36f, rot, new Vector3(0.1f, 0.15f, 0.07f), steel);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            log += holder.name + " at " + foot + " n " + n + "\n";
        }
        return log;
    }

    static Mesh SheetMesh()
    {
        // corrugated: a strip of shallow waves, both faces
        const int w = 16;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        for (int f = 0; f < 2; f++)
            for (int i = 0; i <= w; i++)
            {
                float u = i / (float)w, z = Mathf.Sin(u * Mathf.PI * 2f * 8f) * 0.02f + (i % 2) * 0.012f;
                v.Add(new Vector3(u - 0.5f, -0.5f, z)); v.Add(new Vector3(u - 0.5f, 0.5f, z));
                uv.Add(new Vector2(u, 0f)); uv.Add(new Vector2(u, 1f));
            }
        for (int f = 0; f < 2; f++)
            for (int i = 0; i < w; i++)
            {
                int q = f * (w + 1) * 2 + i * 2;
                if (f == 0) tri.AddRange(new[] { q, q + 2, q + 1, q + 1, q + 2, q + 3 });
                else tri.AddRange(new[] { q, q + 1, q + 2, q + 1, q + 3, q + 2 });
            }
        var m = new Mesh(); m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // ------------------------------------------------------------------ 58

    public static string BoxAmbushBuild()
    {
        var root = Fresh("Alley 2 Gameplay/EnemyGroup4", "Box Ambush");
        var src = AgentTools.Find("BagScenarios/BagHintScenario/CardboardBox");
        var s = "";
        foreach (var enemyName in new[] { "QuestionerEnemy", "BagBearerEnemy2" })
        {
            var enemy = AgentTools.Find("Alley 2 Gameplay/EnemyGroup4/" + enemyName);
            Vector3 c = enemy.position; c.y = GroundY(c, 1.2f);
            var pile = new GameObject("Boxes - " + enemyName).transform;
            pile.SetParent(root, false); pile.position = c;
            var rng = new System.Random(enemyName.Length * 31);
            float R() => (float)rng.NextDouble();
            var list = new List<Rigidbody>();
            // a ring of cartons round him, three high and closed over the top: he can't be seen in it
            for (int layer = 0; layer < 4; layer++)
            {
                int n = layer < 3 ? 5 : 2;
                float rad = layer < 3 ? 0.78f - layer * 0.04f : 0.3f;
                for (int i = 0; i < n; i++)
                {
                    float a = (i + (layer % 2) * 0.5f) / n * Mathf.PI * 2f + (R() - 0.5f) * 0.2f;
                    var go = Object.Instantiate(src.gameObject, pile);
                    go.name = "CardboardBox";
                    foreach (var comp in go.GetComponents<Component>()) if (comp is UniqueID) Object.DestroyImmediate(comp);
                    go.transform.localScale = new Vector3(1.15f + R() * 0.25f, 1.16f, 1.1f + R() * 0.25f);
                    go.transform.position = c + new Vector3(Mathf.Cos(a) * rad, 0.3f + layer * 0.6f, Mathf.Sin(a) * rad);
                    go.transform.rotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f + (R() - 0.5f) * 25f, 0f);
                    list.Add(go.GetComponent<Rigidbody>());
                }
            }
            var am = pile.gameObject.AddComponent<BoxAmbush>();
            var so = new SerializedObject(am);
            so.FindProperty("enemy").objectReferenceValue = enemy.gameObject;
            so.FindProperty("trigger").objectReferenceValue = AgentTools.Find("Alley 2 Gameplay/EnemyGroup4/Trigger").GetComponent<SaveableTrigger>();
            var arr = so.FindProperty("boxes"); arr.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            var boxClips = new SerializedObject(src.GetComponent<Box>()).FindProperty("collideClips");
            var cl = so.FindProperty("burstClips"); cl.arraySize = boxClips.arraySize;
            for (int i = 0; i < boxClips.arraySize; i++) cl.GetArrayElementAtIndex(i).objectReferenceValue = boxClips.GetArrayElementAtIndex(i).objectReferenceValue;
            so.ApplyModifiedPropertiesWithoutUndo();
            s += enemyName + " " + c + " " + list.Count + " boxes; ";
        }
        return s;
    }

    // ------------------------------------------------------------------ 25

    public static string TableShardBuild()
    {
        var kd = AgentTools.Find("CinematicPreSequenceController/KnockDownTrigger").GetComponent<CinematicKnockdownSequence>();
        var root = Fresh("Cutscenes/CinematicPreSequenceController", "Table Shard");
        var mat = TexMat("TableShard", "TableShard", 1f, true);
        mat.SetFloat("_Mode", 2f); mat.SetInt("_SrcBlend", 5); mat.SetInt("_DstBlend", 10); mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON"); mat.EnableKeyword("_ALPHABLEND_ON"); mat.renderQueue = 3000; mat.SetFloat("_Glossiness", 0.6f);
        var edge = PlainMat("TableShardEdge", new Color(0.24f, 0.36f, 0.31f), 0.6f);
        // the pane that is left round the hole he fell through: feet at both sides, the middle open up to the Runner's hips
        float[] xs = { -0.95f, -0.78f, -0.62f, -0.5f, -0.36f, -0.2f, -0.05f, 0.12f, 0.26f, 0.4f, 0.55f, 0.68f, 0.82f, 0.98f };
        float[] lo = { 0.02f, 0f, 0.18f, 0.46f, 0.52f, 0.47f, 0.55f, 0.49f, 0.54f, 0.48f, 0.4f, 0.12f, 0f, 0.05f };
        float[] hi = { 1.25f, 1.44f, 1.36f, 1.55f, 1.49f, 1.6f, 1.52f, 1.58f, 1.47f, 1.56f, 1.42f, 1.5f, 1.3f, 1.16f };
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>(); var rim = new List<int>();
        for (int f = 0; f < 2; f++)
            for (int i = 0; i < xs.Length; i++)
                foreach (float y in new[] { lo[i], hi[i] })
                {
                    v.Add(new Vector3(xs[i], y, f == 0 ? -0.006f : 0.006f));
                    uv.Add(new Vector2(xs[i] * 0.5f + 0.5f, y / 1.6f));
                }
        int n = xs.Length * 2;
        for (int i = 0; i + 1 < xs.Length; i++)
        {
            int a = i * 2;
            tri.AddRange(new[] { a, a + 1, a + 2, a + 2, a + 1, a + 3 });
            tri.AddRange(new[] { n + a, n + a + 2, n + a + 1, n + a + 2, n + a + 3, n + a + 1 });
            rim.AddRange(new[] { a, a + 2, n + a, a + 2, n + a + 2, n + a, a, n + a, a + 2, a + 2, n + a, n + a + 2 });
            rim.AddRange(new[] { a + 1, n + a + 1, a + 3, a + 3, n + a + 1, n + a + 3, a + 1, a + 3, n + a + 1, a + 3, n + a + 3, n + a + 1 });
        }
        var m = new Mesh { subMeshCount = 2 };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.SetTriangles(rim, 1); m.RecalculateNormals(); m.RecalculateBounds();
        var go = MeshGo("Pane", root, SaveMesh(m, "TableShard"), mat);
        go.GetComponent<MeshRenderer>().sharedMaterials = new[] { mat, edge };
        go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // half a metre in front of where he lies, across the line to the Runner
        Vector3 at = new Vector3(-11.38f, 0f, -76.72f); at.y = GroundY(at + Vector3.up, 1f);
        root.SetPositionAndRotation(at, Quaternion.Euler(-5f, 7f, 0f));
        root.gameObject.SetActive(false);
        var so = new SerializedObject(kd);
        so.FindProperty("tableShard").objectReferenceValue = root;
        so.ApplyModifiedPropertiesWithoutUndo();
        Dirty(kd.gameObject);
        return "shard at " + at;
    }

    // ------------------------------------------------------------------ 46: the "closed" mark goes once the "open" one is drawn

    public static string ClosedMarks()
    {
        var log = "";
        foreach (var s in Object.FindObjectsByType<SaveableUIImage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SaveableUIImage.MarkerData open = null;
            foreach (var m in s.markers) if (m.marker != null && m.marker.name.Contains("DoorOpen")) open = m;
            if (open == null) continue;
            foreach (var m in s.markers)
            {
                if (m == open || m.marker == null || !(m.marker.name.Contains("ClosedDoor") || m.marker.name.Contains("Lock_Block"))) continue;
                bool asked = s.name == "Alley2_LockedSideDoor" || s.name == "Alley3_LockCombination";
                log += s.name + ": " + m.marker.name + " hiddenBy " + (m.hiddenBy ? m.hiddenBy.name : "-") + (asked ? " -> " + open.marker.name : "") + "\n";
                if (!asked) continue;
                Undo.RecordObject(s, "marks");
                m.hiddenBy = open.marker;
                EditorUtility.SetDirty(s); Dirty(s.gameObject);
            }
        }
        return log;
    }

    // ------------------------------------------------------------------ 49 (+ 26)

    public static string WrapperAtGate()
    {
        var t = AgentTools.Find("Alley 3 Gameplay/Adel Wrapper Drop");
        var seq = AgentTools.Find("Alley 3 Gameplay/Adel Door Sequence").GetComponent<AdelDoorSequence>();
        Vector3 p = new Vector3(-19.3f, 1f, -73.6f); p.y = GroundY(p, 1.5f);
        t.position = p;
        var col = t.GetComponent<SphereCollider>(); col.radius = 1.5f;
        var walk = t.Find("WalkToPosition"); walk.position = new Vector3(-20.3f, p.y, -73.6f);
        var drop = t.GetComponent<AdelWrapperDrop>();
        var so = new SerializedObject(drop);
        so.FindProperty("doorSequence").objectReferenceValue = seq;
        so.ApplyModifiedPropertiesWithoutUndo();
        var ss = new SerializedObject(seq);
        ss.FindProperty("closeShot").vector3Value = new Vector3(-22.6f, 1.6f, -75.85f);
        ss.ApplyModifiedPropertiesWithoutUndo();
        Dirty(t.gameObject);
        return "wrapper " + t.position + " visual " + t.Find("Visual").position;
    }
}
