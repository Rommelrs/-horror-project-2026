using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// House number plates on every house of Blocks 1-3, numbered like the in-game map (Assets/Sprites/The true final version of
/// ALSARAB MAP.jpg: Abu Khalid 417, Abu Faisal 203, Umm Rashid 241, Umm Saeed 158, the muezzin's house 290, Abu Nasser 362,
/// Abu Salem 375; the houses the map leaves blank got numbers of their own). Weathered blue enamel plates (the Gulf municipal
/// style, "منزل / HOUSE", Western + Arabic-Indic digits), on the wall beside each house door, street side.
/// The map lines up 1:1 with the world (MapCamera at (-35, -6.78), ortho 147.9): each door was matched to its map box.
/// Atlas = Assets/Models/HousePlates/HousePlates_Atlas.png (made by a PIL script, cell order = Numbers). Re-runnable.
/// </summary>
public static class HouseNumberPlatesBuilder
{
    const string Dir = "Assets/Models/HousePlates";
    const string RootName = "House Number Plates";
    // atlas cell order (must match the texture)
    static readonly int[] Numbers = { 158, 164, 172, 185, 196, 203, 211, 237, 241, 284, 287, 290, 293, 296, 299, 344, 348, 351, 357, 362, 366, 371, 375, 409, 417 };
    const int Cols = 4, Rows = 8;
    const float PlateW = 0.46f, PlateH = 0.23f, PlateD = 0.014f;
    const float PlateHeight = 1.85f;      // plate centre above the door's sill
    const float BesideDoor = 0.5f;        // plate centre beyond the door edge, along the wall

    // door -> house number, and the street side of its wall (read off the top-down renders: some walls are two-sided, so a ray
    // can't tell). Doors under Environment7 unless a path is given.
    static readonly (string door, int number, string street)[] Doors =
    {
        // Block 3 (Alley 3), north row west->east on the map
        ("House Door (27)", 196, "-z"), ("House Door (28)", 196, "+z"),
        ("House Door (26)", 211, "-z"), ("House Door (23)", 211, "-x"),
        ("House Door (25)", 203, "-z"), ("House Door (24)", 203, "+x"),     // Abu Faisal
        ("House Door (22)", 409, "+z"),
        ("Environmental Storytelling/Door Locked/House Door", 417, "+x"),    // Abu Khalid (lantern + doorbell)
        // Block 3, south row (on Al-Sarab Street)
        ("House Door (17)", 237, "+z"), ("House Door (18)", 158, "+z"),      // Umm Saeed
        ("House Door (29)", 164, "+z"), ("House Door (19)", 172, "+z"), ("House Door (21)", 172, "-x"), ("House Door (20)", 185, "+z"),
        // Block 2 (Alley 2)
        ("House Door (16)", 348, "-z"), ("House Door (15)", 351, "-z"), ("House Door (9)", 351, "-x"),
        ("House Door (14)", 362, "-z"), ("House Door (8)", 362, "+x"), ("House Door (6)", 362, "-x"),   // Abu Nasser
        ("House Door (13)", 366, "-z"), ("House Door (10)", 357, "-z"), ("House Door (7)", 371, "-x"),
        ("House Door (5)", 375, "-z"),                                       // Abu Salem
        // Block 1 (Alley 1)
        ("House Door (12)", 284, "+x"), ("House Door (4)", 284, "+z"),
        ("House Door (11)", 290, "+x"), ("House Door (1)", 290, "-x"),       // the muezzin's house
        ("House Door (3)", 296, "-z"), ("House Door (2)", 287, "+z"), ("House Door", 299, "-z"),
    };
    // houses with no door model: a gateway point on their street wall
    static readonly (string name, Vector2 xz, string street, int number)[] Gateways =
    {
        ("Umm Rashid gateway", new Vector2(128.5f, -41.12f), "+z", 241),     // Umm Rashid (LockDoor 1 (28), red path, no door)
        ("Block 2 south-west gateway", new Vector2(49.5f, 79.26f), "+z", 344),
        ("Block 1 east house wall", new Vector2(-181.2f, -21f), "+x", 293),
    };

    static Vector3 Dir3(string s) => (s[0] == '-' ? -1f : 1f) * (s[1] == 'x' ? Vector3.right : Vector3.forward);

    [MenuItem("Tools/Environment/Build House Number Plates (Game scene)")]
    public static void Build()
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");
        if (!scene.isLoaded) { Debug.LogError("[Plates] open the Game scene first"); return; }
        if (EditorApplication.isPlaying) { Debug.LogError("[Plates] stop Play mode first"); return; }
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var env = all.First(t => t.name == "------------Environment----------");
        var env7 = all.First(t => t.name == "Environment7" && t.parent == env);

        // texture, material, meshes
        string texPath = Dir + "/HousePlates_Atlas.png";
        var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
        ti.textureType = TextureImporterType.Default; ti.npotScale = TextureImporterNPOTScale.None; ti.mipmapEnabled = true;
        ti.wrapMode = TextureWrapMode.Clamp; ti.filterMode = FilterMode.Bilinear; ti.maxTextureSize = 2048;
        ti.SaveAndReimport();
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        string matPath = Dir + "/HousePlate.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(Shader.Find("Legacy Shaders/Diffuse")); AssetDatabase.CreateAsset(mat, matPath); }
        mat.shader = Shader.Find("Legacy Shaders/Diffuse");   // same as the iron house doors
        mat.mainTexture = tex; mat.color = new Color(0.92f, 0.92f, 0.92f);
        EditorUtility.SetDirty(mat);

        string meshPath = Dir + "/HousePlateMeshes.asset";
        AssetDatabase.DeleteAsset(meshPath);
        var holder = ScriptableObject.CreateInstance<HousePlateMeshHolder>();
        AssetDatabase.CreateAsset(holder, meshPath);
        var meshes = new Dictionary<int, Mesh>();
        for (int i = 0; i < Numbers.Length; i++)
        {
            var m = PlateMesh(i); m.name = "HousePlate_" + Numbers[i];
            AssetDatabase.AddObjectToAsset(m, holder);
            meshes[Numbers[i]] = m;
        }

        var old = env.Find(RootName);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject(RootName).transform;
        root.SetParent(env, false);

        int placed = 0;
        foreach (var (doorName, number, street) in Doors)
        {
            Transform door = doorName.Contains("/")
                ? all.FirstOrDefault(t => Path(t).EndsWith(doorName))
                : env7.Find(doorName);
            if (door == null) { Debug.LogWarning("[Plates] door not found: " + doorName); continue; }
            var rs = door.GetComponentsInChildren<Renderer>(true);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            bool alongX = b.size.x > b.size.z;
            if (Place(root, door.name, b.center, b.min.y, b.size[alongX ? 0 : 2] * 0.5f, Dir3(street), number, meshes[number], mat, door)) placed++;
        }
        foreach (var (name, xz, street, number) in Gateways)
        {
            Vector3 outN = Dir3(street);
            var top = new Vector3(xz.x, 60f, xz.y) + outN * 1.2f;
            float groundY = Physics.Raycast(top, Vector3.down, out RaycastHit g, 100f, ~0, QueryTriggerInteraction.Ignore) ? g.point.y : 1f;
            if (Place(root, name, new Vector3(xz.x, groundY + 1.3f, xz.y), groundY, 0.9f, outN, number, meshes[number], mat, null)) placed++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Plates] placed " + placed + " plates");
    }

    static string Path(Transform t) { string s = t.name; for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s; return s; }

    /// <summary>On the street face of the door's wall: viewer's right of the door first, then left, then a step further along
    /// either way (past a lamp or a doorbell), then above the door.</summary>
    static bool Place(Transform root, string label, Vector3 doorCenter, float sillY, float halfWidth, Vector3 outN, int number, Mesh mesh, Material mat, Transform door)
    {
        Vector3 along = Mathf.Abs(outN.z) > 0.5f ? Vector3.right : Vector3.forward;
        float y = sillY + PlateHeight;
        float plane = Vector3.Dot(doorCenter, outN);
        Vector3 viewerRight = Vector3.Cross(Vector3.up, -outN);
        Vector3 dirRight = Vector3.Dot(viewerRight, along) >= 0 ? along : -along;
        float o1 = halfWidth + BesideDoor, o2 = o1 + 0.45f;
        var c0 = new Vector3(doorCenter.x, y, doorCenter.z);
        var spots = new List<Vector3>
        {
            c0 + dirRight * o1, c0 - dirRight * o1, c0 + dirRight * o2, c0 - dirRight * o2,
            new Vector3(doorCenter.x, sillY + 3.0f, doorCenter.z),
        };
        bool found = false; RaycastHit hit = default;
        foreach (var c in spots)
            if (Hit(c, outN, plane, door, out hit) && Clear(hit, door)) { found = true; break; }
        if (!found) { Debug.LogWarning("[Plates] no wall beside " + label + " for " + number); return false; }
        Vector3 n = outN;   // square to the wall, even where the mesh normal is a little off
        var go = new GameObject("Plate " + number + " (" + label + ")");
        go.transform.SetParent(root, false);
        go.transform.SetPositionAndRotation(hit.point + n * 0.004f, Quaternion.LookRotation(n));
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        Debug.Log("[Plates] " + number + " at " + label + " -> " + hit.point.ToString("F2") + " facing " + n.ToString("F1"));
        return true;
    }

    /// <summary>The first thing a ray from the street meets at this spot must be the wall itself, within a hand of the door's
    /// plane and facing the street (not a bin or a car in front of it, not the yard face of the wall).</summary>
    static bool Hit(Vector3 c, Vector3 outN, float plane, Transform door, out RaycastHit hit)
    {
        Vector3 o = c - outN * (Vector3.Dot(c, outN) - plane) + outN * 2f;
        foreach (var h in Physics.RaycastAll(o, -outN, 3f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
        {
            if (door != null && h.collider.transform.IsChildOf(door)) continue;
            hit = h;
            return Mathf.Abs(Vector3.Dot(h.point, outN) - plane) < 0.35f && Vector3.Dot(h.normal, outN) > 0.7f;
        }
        hit = default; return false;
    }

    // nothing else (lantern, doorbell, pipe) where the plate goes
    static bool Clear(RaycastHit h, Transform door)
    {
        var area = new Bounds(h.point + h.normal * 0.05f, new Vector3(PlateW + 0.1f, PlateH + 0.1f, PlateW + 0.1f));
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!r.enabled || r.bounds.size.magnitude > 6f) continue;            // walls, ground
            if (door != null && r.transform.IsChildOf(door)) continue;
            if (r.GetComponent<Collider>() == h.collider) continue;
            if (r is ParticleSystemRenderer) continue;
            if (r.bounds.Intersects(area)) return false;
        }
        return true;
    }

    static Mesh PlateMesh(int cell)
    {
        int col = cell % Cols, row = cell / Cols;
        float u0 = col / (float)Cols, u1 = (col + 1) / (float)Cols;
        float v1 = 1f - row / (float)Rows, v0 = 1f - (row + 1) / (float)Rows;
        float rimU = u0 + 0.004f, rimV = (v0 + v1) * 0.5f;   // the bare steel edge
        float w = PlateW * 0.5f, h = PlateH * 0.5f, d = PlateD;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>(); var nrm = new List<Vector3>();
        void Quad(Vector3 a, Vector3 b2, Vector3 c, Vector3 e, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ue, Vector3 n)
        {
            int i = v.Count; v.AddRange(new[] { a, b2, c, e }); uv.AddRange(new[] { ua, ub, uc, ue }); nrm.AddRange(new[] { n, n, n, n });
            tri.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        var rim = new Vector2(rimU, rimV);
        // front faces +Z (the wall normal); seen from the street, +X local is the viewer's left
        Quad(new Vector3(w, -h, d), new Vector3(w, h, d), new Vector3(-w, h, d), new Vector3(-w, -h, d),
             new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0), Vector3.forward);
        Quad(new Vector3(-w, h, 0), new Vector3(-w, h, d), new Vector3(w, h, d), new Vector3(w, h, 0), rim, rim, rim, rim, Vector3.up);
        Quad(new Vector3(w, -h, 0), new Vector3(w, -h, d), new Vector3(-w, -h, d), new Vector3(-w, -h, 0), rim, rim, rim, rim, Vector3.down);
        Quad(new Vector3(w, h, 0), new Vector3(w, h, d), new Vector3(w, -h, d), new Vector3(w, -h, 0), rim, rim, rim, rim, Vector3.right);
        Quad(new Vector3(-w, -h, 0), new Vector3(-w, -h, d), new Vector3(-w, h, d), new Vector3(-w, h, 0), rim, rim, rim, rim, Vector3.left);
        var m = new Mesh();
        m.SetVertices(v); m.SetUVs(0, uv); m.SetNormals(nrm); m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        return m;
    }
}
