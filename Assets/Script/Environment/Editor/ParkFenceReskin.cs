using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The park's black spear-top railings (Environment7/FENCE: "LOW POLY FENCE" / "SMALL LOW POLY FENCE" runs) re-skinned as
// the white steel panel fence of the user's photos (2026-10-25): alpha-cut panels (arches, vases, spear finials, rust at the
// foot) between square white posts about every 2 m, each panel following the ground between its posts. Looks only - the
// old runs keep their MeshColliders (the boundary stays exactly where it was); only their renderers are switched off.
//   Apply: builds one mesh per run (Assets/Models/ParkFence/), puts them under "FENCE (white panels)" next to FENCE.
//   Revert: removes those and switches the old renderers back on.
// Texture: AnimationTools/alley_real/make_park_fence_texture.py (layout documented there).
public static class ParkFenceReskin
{
    const string FencePath = "Environment7/FENCE";
    const string GroupName = "FENCE (white panels)";
    const string TexPath = "Assets/Textures/AlleyReal/ParkFence.png";
    const string MatPath = "Assets/Textures/AlleyReal/ParkFence.mat";
    const string MeshDir = "Assets/Models/ParkFence";

    const float Bay = 2.1f;            // post spacing aimed for
    const float Post = 0.09f, PostH = 1.68f, PanelH = 1.75f;
    static readonly Vector2 PanelU = new Vector2(0f, 224f / 256f), PostU = new Vector2(230f / 256f, 254f / 256f);

    public static string LastReport = "";

    [MenuItem("Tools/Environment/Park Fence/Apply White Panels")]
    public static void Apply()
    {
        var env = GameObject.Find("------------Environment----------");
        var fence = env != null ? env.transform.Find(FencePath) : null;
        if (fence == null) { Debug.LogError("[ParkFenceReskin] " + FencePath + " not found - open the Game scene."); return; }
        var old = fence.parent.Find(GroupName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var group = new GameObject(GroupName).transform;
        group.SetParent(fence.parent, false);
        Undo.RegisterCreatedObjectUndo(group.gameObject, "Park fence");
        var mat = Material();
        System.IO.Directory.CreateDirectory(MeshDir);

        var report = new List<string>();
        foreach (Transform run in fence)
        {
            var r = run.GetComponent<MeshRenderer>();
            if (r == null || !run.gameObject.activeInHierarchy) continue;
            var b = r.bounds;
            bool alongX = b.size.x >= b.size.z;
            Vector3 along = alongX ? Vector3.right : Vector3.forward;
            float len = alongX ? b.size.x : b.size.z;
            Vector3 a = new Vector3(b.center.x, b.min.y, b.center.z) - along * len / 2f;
            var go = new GameObject(run.name + " (white)");
            go.transform.SetParent(group, false);
            go.transform.position = a;
            var mesh = BuildRun(a, along, len, b.min.y, out int panels);
            string path = MeshDir + "/" + run.name.Replace(" ", "_") + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { existing.Clear(); EditorUtility.CopySerialized(mesh, existing); mesh = existing; }
            else AssetDatabase.CreateAsset(mesh, path);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            Undo.RecordObject(r, "Park fence"); r.enabled = false;
            report.Add(run.name + ": " + len.ToString("F1") + " m, " + panels + " panels");
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(fence.gameObject.scene);
        LastReport = string.Join("\n", report);
        Debug.Log("[ParkFenceReskin] applied\n" + LastReport);
    }

    [MenuItem("Tools/Environment/Park Fence/Revert")]
    public static void Revert()
    {
        var env = GameObject.Find("------------Environment----------");
        var fence = env != null ? env.transform.Find(FencePath) : null;
        if (fence == null) { Debug.LogError("[ParkFenceReskin] " + FencePath + " not found - open the Game scene."); return; }
        var group = fence.parent.Find(GroupName);
        if (group != null) Undo.DestroyObjectImmediate(group.gameObject);
        foreach (var r in fence.GetComponentsInChildren<MeshRenderer>(true)) { Undo.RecordObject(r, "Park fence revert"); r.enabled = true; }
        EditorSceneManager.MarkSceneDirty(fence.gameObject.scene);
        Debug.Log("[ParkFenceReskin] reverted");
    }

    static Material Material()
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(TexPath);
        if (ti != null)
        {
            ti.maxTextureSize = 256; ti.filterMode = FilterMode.Point; ti.wrapMode = TextureWrapMode.Clamp;
            ti.alphaIsTransparency = true; ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.mipmapEnabled = true; ti.mipMapsPreserveCoverage = true; ti.alphaTestReferenceValue = 0.5f;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
        else Debug.LogError("[ParkFenceReskin] missing " + TexPath + " (run make_park_fence_texture.py)");
        var m = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (m == null) { m = new Material(Shader.Find("Custom/FoliageCutout")); AssetDatabase.CreateAsset(m, MatPath); }
        m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
        m.color = Color.white;
        m.SetFloat("_Cutoff", 0.5f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ground height at p: the terrain under it (the old runs are flat meshes, the new posts stand on the ground)
    static float Ground(Vector3 p, float fallback)
    {
        foreach (var hit in Physics.RaycastAll(new Vector3(p.x, fallback + 6f, p.z), Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            if (hit.collider is TerrainCollider) return hit.point.y;
        return fallback;
    }

    // vertices relative to the run start `a` (the GameObject sits there)
    static Mesh BuildRun(Vector3 a, Vector3 along, float len, float baseY, out int panels)
    {
        panels = Mathf.Max(1, Mathf.RoundToInt(len / Bay));
        var side = Vector3.Cross(Vector3.up, along);
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();

        void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal, Vector2 u, float v0, float v1)
        {
            int i = v.Count;
            v.Add(p0 - a); v.Add(p1 - a); v.Add(p2 - a); v.Add(p3 - a);
            for (int k = 0; k < 4; k++) n.Add(normal);
            uv.Add(new Vector2(u.x, v0)); uv.Add(new Vector2(u.y, v0)); uv.Add(new Vector2(u.y, v1)); uv.Add(new Vector2(u.x, v1));
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), normal) < 0f) tri.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            else tri.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }

        void Box(Vector3 c, Vector3 half, float vTop)
        {
            Vector3 x = along * half.x, y = Vector3.up * half.y, z = side * half.z;
            Quad(c - x - y + z, c + x - y + z, c + x + y + z, c - x + y + z, side, PostU, 0f, vTop);
            Quad(c + x - y - z, c - x - y - z, c - x + y - z, c + x + y - z, -side, PostU, 0f, vTop);
            Quad(c + x - y + z, c + x - y - z, c + x + y - z, c + x + y + z, along, PostU, 0f, vTop);
            Quad(c - x - y - z, c - x - y + z, c - x + y + z, c - x + y - z, -along, PostU, 0f, vTop);
            Quad(c - x + y + z, c + x + y + z, c + x + y - z, c - x + y - z, Vector3.up, PostU, vTop * 0.9f, vTop);
        }

        var posts = new Vector3[panels + 1];
        for (int i = 0; i <= panels; i++)
        {
            var p = a + along * (len * i / panels);
            p.y = Ground(p, baseY);
            posts[i] = p;
            Box(p + Vector3.up * (PostH / 2f - 0.05f), new Vector3(Post / 2f, PostH / 2f + 0.05f, Post / 2f), PostH / PanelH);
            Box(p + Vector3.up * 0.01f, new Vector3(0.09f, 0.012f, 0.09f), 0.05f);                     // base plate
        }
        for (int i = 0; i < panels; i++)
        {
            // panel hung between the posts, its foot following the ground from one post to the next
            Vector3 p0 = posts[i] + along * (Post / 2f + 0.02f), p1 = posts[i + 1] - along * (Post / 2f + 0.02f);
            Quad(p0, p1, p1 + Vector3.up * PanelH, p0 + Vector3.up * PanelH, side, PanelU, 0f, 1f);
        }
        var mesh = new Mesh { name = "ParkFenceRun" };
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
