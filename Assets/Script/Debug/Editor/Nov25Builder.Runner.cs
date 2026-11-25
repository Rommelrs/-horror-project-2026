using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 11 + 15: the Runner's knees - his weak point - are raw, skinned and bloody, so they read before the fight
public static partial class Nov25Builder
{
    const string RunnerPrefab = "Assets/Prefab/Enemy/RunnerEnemy.prefab";

    /// <summary>Where the knees are on the texture (bind pose), for painting.</summary>
    public static string KneeReport()
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RunnerPrefab));
        go.transform.SetPositionAndRotation(new Vector3(0f, 500f, 0f), Quaternion.identity);
        var sb = new StringBuilder();
        try
        {
            var r = go.GetComponent<RunnerEnemy>();
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var baked = new Mesh(); smr.BakeMesh(baked, true);
                sb.AppendLine(smr.name + " verts " + baked.vertexCount + " mats " + string.Join(",", smr.sharedMaterials.Select(m => m ? m.name + ":" + (m.mainTexture ? m.mainTexture.name : "-") : "-")) + " active " + smr.gameObject.activeInHierarchy);
                var v = baked.vertices; var uv = smr.sharedMesh.uv; var n = baked.normals;
                foreach (var k in new[] { r.leftKneeTransform, r.rightKneeTransform })
                {
                    sb.AppendLine("  knee " + k.name + " at " + (k.position - go.transform.position) + " child " + (k.childCount > 0 ? k.GetChild(0).name + " " + (k.GetChild(0).position - go.transform.position) : "-") + " scale " + k.lossyScale);
                    for (int i = 0; i < v.Length; i++)
                    {
                        Vector3 w = smr.transform.TransformPoint(v[i]);
                        if ((w - k.position).magnitude > 0.17f) continue;
                        Vector3 wn = smr.transform.TransformDirection(n[i]);
                        sb.AppendLine("    d " + (w - k.position).ToString("F3") + " n " + wn.ToString("F2") + " uv " + uv[i].ToString("F3"));
                    }
                }
            }
        }
        finally { Object.DestroyImmediate(go); }
        File.WriteAllText(AgentTools.Dir + "knees.txt", sb.ToString());
        return "ok";
    }

    public static string RunnerKnees()
    {
        const string tex = "Assets/Prefab/Enemy/TheRunner.fbm/Diffuse_TheRunner_Knees.png";
        AssetDatabase.ImportAsset(tex);
        var ti = (TextureImporter)AssetImporter.GetAtPath(tex);
        ti.filterMode = FilterMode.Point; ti.maxTextureSize = 256; ti.mipmapEnabled = true; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.SaveAndReimport();
        const string mp = "Assets/Prefab/Enemy/Materials/M_TheRunner_Knees.mat";
        var old = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefab/Enemy/Materials/M_TheRunner_DoubleSided.mat");
        var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (m == null) { m = new Material(old); AssetDatabase.CreateAsset(m, mp); }
        m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(tex);
        EditorUtility.SetDirty(m);
        int n = 0;
        foreach (var path in new[] { RunnerPrefab, "Assets/Prefab/Enemy/RunnerPuppet.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) n += Swap(r, old, m);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
        foreach (var c in Object.FindObjectsByType<RunnerEnemy>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(x => x.transform)
            .Concat(Object.FindObjectsByType<RunnerPuppet>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(x => x.transform)))
            foreach (var r in c.GetComponentsInChildren<Renderer>(true)) { int k = Swap(r, old, m); if (k > 0) { n += k; Dirty(r.gameObject); } }
        AssetDatabase.SaveAssets();
        return "renderers " + n;
    }

    static int Swap(Renderer r, Material old, Material m)
    {
        var mats = r.sharedMaterials; bool ch = false;
        for (int i = 0; i < mats.Length; i++)
            if (mats[i] != null && (mats[i] == old || mats[i].name.StartsWith("Diffuse_TheRunner_128px"))) { mats[i] = m; ch = true; }
        if (ch) { r.sharedMaterials = mats; EditorUtility.SetDirty(r); }
        return ch ? 1 : 0;
    }

    public static string KneeShot(string file)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RunnerPrefab));
        go.transform.SetPositionAndRotation(new Vector3(0f, 500f, 0f), Quaternion.identity);
        var l = new GameObject("l").AddComponent<Light>(); l.type = LightType.Directional; l.transform.rotation = Quaternion.Euler(35f, 200f, 0f); l.intensity = 1f;
        try
        {
            var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var s in go.GetComponentsInChildren<SkinnedMeshRenderer>()) b.Encapsulate(s.bounds);
            Vector3 c = go.transform.position;
            AgentTools.Shot(file + "_f.png", c + go.transform.forward * 2.6f + Vector3.up * 1f, c + Vector3.up * 0.9f, 45f, 700, 700);
            AgentTools.Shot(file + "_s.png", c + go.transform.right * 1.5f + go.transform.forward * 1f + Vector3.up * 0.6f, c + Vector3.up * 0.5f, 45f, 700, 700);
        }
        finally { Object.DestroyImmediate(go); Object.DestroyImmediate(l.gameObject); }
        return "ok";
    }
}
