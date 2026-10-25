using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Every "House Door" (instances of House Door.fbx, a Tripo double door) re-skinned as the black wrought-iron door of the
// user's photo (2026-10-25): a steel frame, a main leaf with the scrollwork, a narrow fixed leaf, hinges, a lock box and
// lever. Looks only - each door keeps its transform, BoxCollider and whatever is stuck to it; only its MeshFilter mesh and
// material are swapped. The new mesh is built here in the old mesh's local frame (front = local -X, up = +Y, width
// along Z, the object scaled ~2.81), with the frame face on the old door's front plane.
//   Apply: builds Assets/Models/IronDoor/IronDoor.asset + material, swaps every House Door in the open scene.
//   Revert: the Tripo mesh and material back.
// Texture: AnimationTools/alley_real/make_iron_door_texture.py (layout documented there).
public static class HouseDoorReskin
{
    const string OldFbx = "Assets/Prefab/Props/House Doors/House Door.fbx";
    const string OldMat = "Assets/Prefab/Props/House Doors/tripo_material_b9e6037a-134e-42d3-b1e3-1d7aa01eae74.mat";
    const string MeshPath = "Assets/Models/IronDoor/IronDoor.asset";
    const string TexPath = "Assets/Textures/AlleyReal/IronDoor.png";
    const string MatPath = "Assets/Textures/AlleyReal/IronDoor.mat";

    // door size in metres (the old door's bounds)
    const float W = 1.52f, H = 2.70f;
    const float FrameFace = 0.07f, FrameDepth = 0.12f;
    const float LeafFront = 0.03f, LeafThick = 0.04f;    // leaves set back behind the frame face
    const float MainRight = 0.265f;                       // where the main leaf meets the narrow one

    // texture regions (u0, u1) of IronDoor.png, 256 px wide
    static readonly Vector2 MainUV = new Vector2(0f, 168f / 256f), NarrowUV = new Vector2(168f / 256f, 240f / 256f), PlainUV = new Vector2(241f / 256f, 255f / 256f);

    public static string LastReport = "";

    [MenuItem("Tools/Environment/House Doors/Apply Iron Door")]
    public static void Apply()
    {
        var old = OldMesh(); if (old == null) return;
        var mesh = BuildMesh(old);
        var mat = Material();
        var doors = Doors(old, mesh);
        foreach (var mf in doors)
        {
            Undo.RecordObject(mf, "Iron doors"); mf.sharedMesh = mesh;
            var r = mf.GetComponent<MeshRenderer>(); Undo.RecordObject(r, "Iron doors"); r.sharedMaterial = mat;
        }
        if (doors.Count > 0) EditorSceneManager.MarkSceneDirty(doors[0].gameObject.scene);
        LastReport = doors.Count + " house doors -> iron door";
        Debug.Log("[HouseDoorReskin] " + LastReport);
    }

    [MenuItem("Tools/Environment/House Doors/Revert")]
    public static void Revert()
    {
        var old = OldMesh(); if (old == null) return;
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(OldMat);
        var doors = Doors(old, mesh);
        foreach (var mf in doors)
        {
            Undo.RecordObject(mf, "Iron doors revert"); mf.sharedMesh = old;
            var r = mf.GetComponent<MeshRenderer>(); Undo.RecordObject(r, "Iron doors revert"); r.sharedMaterial = mat;
        }
        if (doors.Count > 0) EditorSceneManager.MarkSceneDirty(doors[0].gameObject.scene);
        Debug.Log("[HouseDoorReskin] reverted " + doors.Count + " house doors");
    }

    static Mesh OldMesh()
    {
        var m = AssetDatabase.LoadAllAssetsAtPath(OldFbx).OfType<Mesh>().FirstOrDefault();
        if (m == null) Debug.LogError("[HouseDoorReskin] no mesh in " + OldFbx);
        return m;
    }

    // the House Doors of the open scene: named so, showing the old or the new mesh
    static List<MeshFilter> Doors(Mesh old, Mesh mesh)
    {
        return Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(mf => mf.name.StartsWith("House Door") && (mf.sharedMesh == old || (mesh != null && mf.sharedMesh == mesh))).ToList();
    }

    static Material Material()
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(TexPath);
        if (ti != null)
        {
            ti.maxTextureSize = 512; ti.filterMode = FilterMode.Point; ti.wrapMode = TextureWrapMode.Clamp;
            ti.mipmapEnabled = true; ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
        else Debug.LogError("[HouseDoorReskin] missing " + TexPath + " (run make_iron_door_texture.py)");
        var m = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (m == null) { m = new Material(Shader.Find("Legacy Shaders/Diffuse")); AssetDatabase.CreateAsset(m, MatPath); }
        m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ── mesh ──
    // Built in door space (metres): x = to the right as you face the door, y = up from the middle of its height,
    // z = out of the door towards you (0 = frame face). Converted to the old mesh's local space at the end.

    class Builder
    {
        public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> tri = new List<int>();
        public System.Func<Vector3, Vector3> toLocal, dirToLocal;

        // one quad: corners in door space, listed bottom-left, bottom-right, top-right, top-left as seen from outside
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Rect r)
        {
            int i = v.Count;
            Vector3 nl = dirToLocal(normal).normalized;
            foreach (var p in new[] { a, b, c, d }) { v.Add(toLocal(p)); n.Add(nl); }
            uv.Add(new Vector2(r.xMin, r.yMin)); uv.Add(new Vector2(r.xMax, r.yMin)); uv.Add(new Vector2(r.xMax, r.yMax)); uv.Add(new Vector2(r.xMin, r.yMax));
            // wind each triangle so Unity sees its front on the normal's side
            bool flip = Vector3.Dot(Vector3.Cross(v[i + 1] - v[i], v[i + 2] - v[i]), nl) < 0f;
            if (flip) tri.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            else tri.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }

        // a box; `front` = UV rect for its +z face, everything else plain steel
        public void Box(Vector3 c, Vector3 s, Rect front)
        {
            Vector3 h = s * 0.5f;
            Vector3 P(float x, float y, float z) => c + new Vector3(x * h.x, y * h.y, z * h.z);
            Rect plain(float len) => new Rect(PlainUV.x, 0f, PlainUV.y - PlainUV.x, Mathf.Clamp01(len / H));
            Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), Vector3.forward, front);                    // front
            Quad(P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), Vector3.back, plain(s.y));                // back
            Quad(P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), Vector3.right, plain(s.y));                   // right
            Quad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), Vector3.left, plain(s.y));                // left
            Quad(P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), P(-1, 1, -1), Vector3.up, plain(s.x));                      // top
            Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), Vector3.down, plain(s.x));                // bottom
        }
    }

    static Mesh BuildMesh(Mesh old)
    {
        var ob = old.bounds;
        float s = ob.size.z / W;                   // local units per metre (the instances are scaled by 1/s)
        float frontX = ob.min.x, cy = ob.center.y, cz = ob.center.z;
        var b = new Builder
        {
            // out of the door = local -X, up = +Y, to the right (seen from the front, looking along +X) = local -Z
            toLocal = p => new Vector3(frontX - p.z * s, cy + p.y * s, cz - p.x * s),
            dirToLocal = d => new Vector3(-d.z, d.y, -d.x),
        };
        Rect plainRect = new Rect(PlainUV.x, 0f, PlainUV.y - PlainUV.x, 1f);
        float half = W / 2f, top = H / 2f, inner = half - FrameFace;
        float fz = -FrameDepth / 2f;
        // frame: jambs, head, a low sill
        b.Box(new Vector3(-half + FrameFace / 2f, 0f, fz), new Vector3(FrameFace, H, FrameDepth), plainRect);
        b.Box(new Vector3(half - FrameFace / 2f, 0f, fz), new Vector3(FrameFace, H, FrameDepth), plainRect);
        b.Box(new Vector3(0f, top - FrameFace / 2f, fz), new Vector3(inner * 2f, FrameFace, FrameDepth), plainRect);
        b.Box(new Vector3(0f, -top + 0.015f, fz), new Vector3(inner * 2f, 0.03f, FrameDepth), plainRect);
        // leaves
        float y0 = -top + 0.03f, y1 = top - FrameFace, lz = -LeafFront - LeafThick / 2f;
        float mainL = -inner, mainR = MainRight, narL = MainRight + 0.01f, narR = inner;
        b.Box(new Vector3((mainL + mainR) / 2f, (y0 + y1) / 2f, lz), new Vector3(mainR - mainL, y1 - y0, LeafThick), new Rect(MainUV.x, 0f, MainUV.y - MainUV.x, 1f));
        b.Box(new Vector3((narL + narR) / 2f, (y0 + y1) / 2f, lz), new Vector3(narR - narL, y1 - y0, LeafThick), new Rect(NarrowUV.x, 0f, NarrowUV.y - NarrowUV.x, 1f));
        // a strip over the gap between the leaves
        b.Box(new Vector3(MainRight + 0.012f, (y0 + y1) / 2f, -LeafFront + 0.008f), new Vector3(0.028f, y1 - y0, 0.016f), plainRect);
        // lock box and lever on the main leaf, ~1 m up
        float lockY = -top + 1.0f;
        b.Box(new Vector3(MainRight - 0.07f, lockY, -LeafFront + 0.015f), new Vector3(0.08f, 0.17f, 0.03f), plainRect);
        b.Box(new Vector3(MainRight - 0.07f, lockY + 0.04f, 0.008f), new Vector3(0.02f, 0.02f, 0.016f), plainRect);
        b.Box(new Vector3(MainRight - 0.12f, lockY + 0.04f, 0.024f), new Vector3(0.12f, 0.022f, 0.018f), plainRect);
        // hinge knuckles on the outer edges of both leaves
        foreach (float hy in new[] { -top + 0.3f, 0f, top - 0.35f })
            foreach (float hx in new[] { mainL, narR })
                b.Box(new Vector3(hx, hy, -LeafFront + 0.01f), new Vector3(0.035f, 0.12f, 0.03f), plainRect);

        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh { name = "IronDoor" };
        mesh.Clear();
        mesh.SetVertices(b.v); mesh.SetNormals(b.n); mesh.SetUVs(0, b.uv); mesh.SetTriangles(b.tri, 0);
        mesh.RecalculateBounds();
        if (isNew)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(MeshPath));
            AssetDatabase.CreateAsset(mesh, MeshPath);
        }
        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssets();
        return mesh;
    }
}
