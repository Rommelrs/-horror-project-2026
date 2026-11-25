using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 10: the bear trap and its bag bearer, in the Gym
public static partial class Nov25Builder
{
    const string TrapPrefab = "Assets/Prefab/BearTrap.prefab";

    // half a hoop of flat bar (hinge along x), with teeth standing up from it when it lies open
    static Mesh JawMesh(float side)
    {
        const float R = 0.27f, bar = 0.016f, h = 0.036f; const int seg = 14;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = v.Count; v.AddRange(new[] { a, b, c, d });
            uv.AddRange(new[] { new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0.3f), new Vector2(0, 0.3f) });
            tri.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3, i, i + 2, i + 1, i, i + 3, i + 2 });
        }
        Vector3 P(float t, float r, float y) { float a = Mathf.Lerp(0f, Mathf.PI, t); return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r * side); }
        for (int i = 0; i < seg; i++)
        {
            float t0 = i / (float)seg, t1 = (i + 1) / (float)seg;
            Vector3 a0 = P(t0, R - bar, 0f), a1 = P(t1, R - bar, 0f), b0 = P(t0, R + bar, 0f), b1 = P(t1, R + bar, 0f);
            Vector3 u = Vector3.up * h;
            Quad(a0, a1, b1, b0); Quad(a0 + u, a1 + u, b1 + u, b0 + u); Quad(a0, a1, a1 + u, a0 + u); Quad(b0, b1, b1 + u, b0 + u);
        }
        // teeth: on the inner edge, pointing up and in - they bite when the hoop swings up
        for (int i = 1; i < seg; i++)
        {
            float t = i / (float)seg, w = 0.028f;
            Vector3 c = P(t, R, h), l = P(t - w, R, h), r = P(t + w, R, h), top = P(t, R - 0.012f, h + 0.06f);
            Vector3 inw = (P(t, R - bar * 1.2f, h) - c);
            Quad(l + inw, r + inw, top, top); Quad(l - inw, r - inw, top, top); Quad(l + inw, l - inw, top, top); Quad(r + inw, r - inw, top, top);
        }
        var m = new Mesh(); m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    public static string TrapBuild()
    {
        var rust = TexMat("TrapRust", "TrapRust");
        rust.SetFloat("_Metallic", 0.45f); rust.SetFloat("_Glossiness", 0.28f);
        var root = new GameObject("BearTrap");
        var model = new GameObject("Model").transform; model.SetParent(root.transform, false);
        GameObject Jaw(string n, float side)
        {
            var g = MeshGo(n, model, SaveMesh(JawMesh(side), "BearTrap" + n), rust);
            g.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            return g;
        }
        var ja = Jaw("JawA", 1f); var jb = Jaw("JawB", -1f);
        BoxGo("Spine", model, new Vector3(0f, 0.016f, 0f), Quaternion.identity, new Vector3(0.94f, 0.024f, 0.05f), rust);
        BoxGo("Cross", model, new Vector3(0f, 0.014f, 0f), Quaternion.identity, new Vector3(0.05f, 0.02f, 0.5f), rust);
        // the pan he steps on, and a spring at each end squeezing the hinges
        var pan = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(pan.GetComponent<Collider>());
        pan.name = "Pan"; pan.transform.SetParent(model, false); pan.transform.localPosition = new Vector3(0f, 0.04f, 0f); pan.transform.localScale = new Vector3(0.15f, 0.006f, 0.15f); pan.GetComponent<Renderer>().sharedMaterial = rust;
        foreach (float s in new[] { 1f, -1f })
        {
            BoxGo("Spring low", model, new Vector3(s * 0.4f, 0.04f, 0f), Quaternion.Euler(0f, 0f, s * 4f), new Vector3(0.26f, 0.014f, 0.06f), rust);
            BoxGo("Spring high", model, new Vector3(s * 0.4f, 0.085f, 0f), Quaternion.Euler(0f, 0f, s * -14f), new Vector3(0.26f, 0.014f, 0.06f), rust);
            BoxGo("Spring eye", model, new Vector3(s * 0.27f, 0.06f, 0f), Quaternion.identity, new Vector3(0.03f, 0.09f, 0.075f), rust);
            BoxGo("Spring bend", model, new Vector3(s * 0.53f, 0.06f, 0f), Quaternion.identity, new Vector3(0.02f, 0.055f, 0.06f), rust);
        }
        // a length of chain to a stake
        var link = LinkMesh();
        for (int i = 0; i < 13; i++)
        {
            var g = MeshGo("Link", model, link, rust);
            g.transform.localPosition = new Vector3(0.52f + i * 0.048f, 0.014f, Mathf.Sin(i * 0.7f) * 0.05f);
            g.transform.localRotation = Quaternion.Euler(0f, 90f + Mathf.Cos(i * 0.7f) * 30f, i % 2 * 90f);
        }
        BoxGo("Stake", model, new Vector3(1.16f, 0.05f, 0f), Quaternion.Euler(0f, 0f, -12f), new Vector3(0.03f, 0.2f, 0.03f), rust);
        var col = root.AddComponent<SphereCollider>(); col.isTrigger = true; col.radius = 0.34f; col.center = new Vector3(0f, 0.4f, 0f);
        root.layer = 2;
        var trap = root.AddComponent<BearTrap>();
        var so = new SerializedObject(trap);
        so.FindProperty("jawA").objectReferenceValue = ja.transform; so.FindProperty("jawB").objectReferenceValue = jb.transform;
        so.FindProperty("snapClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Melee/PipeHitWall_EL.mp3");
        so.FindProperty("openClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Melee/PipeHitWall_1.wav");
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, TrapPrefab);
        Object.DestroyImmediate(root);
        return "trap prefab";
    }

    /// <summary>Gym: the trap at `at`, the bag bearer `ahead` m in front (dir), Player start marker behind it.</summary>
    public static string TrapInGym(Vector3 at, Vector3 dir, float ahead)
    {
        var old = GameObject.Find("Bear Trap Lesson"); if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("Bear Trap Lesson");
        at.y = GroundY(at, 3f);
        var trap = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TrapPrefab));
        trap.transform.SetParent(root.transform, false);
        trap.transform.SetPositionAndRotation(at, Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 90f, 0f));
        var bb = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/BagBearerEnemy.prefab"));
        bb.transform.SetParent(root.transform, false);
        Vector3 bp = at + dir.normalized * ahead; bp.y = GroundY(bp, 3f);
        bb.transform.SetPositionAndRotation(bp, Quaternion.LookRotation(-dir));
        bb.SetActive(false);
        var so = new SerializedObject(trap.GetComponent<BearTrap>());
        so.FindProperty("enemy").objectReferenceValue = bb;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(root.scene);
        return "trap " + at + " bag bearer " + bp;
    }
}
