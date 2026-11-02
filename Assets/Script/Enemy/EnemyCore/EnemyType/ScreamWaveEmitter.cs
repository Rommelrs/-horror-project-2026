using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The visible half of the Questioner's scream: shells of rippling air (Custom/QuestionerScreamWave) rolling out from his mouth,
/// one every `interval` seconds while Emit() keeps being called. Pooled, built at runtime (no prefab needed).
/// </summary>
public class ScreamWaveEmitter : MonoBehaviour
{
    public Material material;
    public Transform origin;
    [Tooltip("Radius a shell reaches (m) and how long it takes (s).")]
    public float maxRadius = 4.5f;
    public float lifetime = 0.85f;
    public float interval = 0.22f;

    class Shell { public Transform t; public Material m; public float born; public Vector3 at; }
    readonly List<Shell> shells = new List<Shell>();
    static Mesh sphere;
    float nextShell;

    /// <summary>Call every frame while he screams: spawns a shell every `interval`.</summary>
    public void Emit()
    {
        if (material == null || Time.time < nextShell) return;
        nextShell = Time.time + interval;
        Spawn();
    }

    Vector3 Mouth => origin != null ? origin.position + transform.forward * 0.12f : transform.position + Vector3.up * 1.75f + transform.forward * 0.25f;

    void Spawn()
    {
        Shell s = null;
        foreach (var x in shells) if (!x.t.gameObject.activeSelf) { s = x; break; }
        if (s == null)
        {
            var go = new GameObject("ScreamShell");
            go.AddComponent<MeshFilter>().sharedMesh = Sphere();
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            s = new Shell { t = go.transform, m = new Material(material) };
            mr.sharedMaterial = s.m;
            shells.Add(s);
        }
        s.born = Time.time;
        s.at = Mouth;
        s.t.position = s.at;
        s.t.localScale = Vector3.one * 0.2f;
        s.t.gameObject.SetActive(true);
    }

    void LateUpdate()
    {
        foreach (var s in shells)
        {
            if (!s.t.gameObject.activeSelf) continue;
            float u = (Time.time - s.born) / lifetime;
            if (u >= 1f) { s.t.gameObject.SetActive(false); continue; }
            float e = 1f - (1f - u) * (1f - u);          // fast out, slowing
            s.t.position = s.at;
            s.t.localScale = Vector3.one * Mathf.Lerp(0.2f, maxRadius * 2f, e);
            s.m.SetFloat("_Fade", Mathf.Pow(1f - u, 1.4f) * Mathf.Clamp01(u / 0.08f));
        }
    }

    void OnDisable()
    {
        foreach (var s in shells) if (s.t != null) s.t.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        foreach (var s in shells)
        {
            if (s.t != null) Destroy(s.t.gameObject);
            if (s.m != null) Destroy(s.m);
        }
    }

    /// <summary>Unit-diameter UV sphere (the same size as Unity's primitive).</summary>
    static Mesh Sphere()
    {
        if (sphere != null) return sphere;
        const int lon = 32, lat = 20;
        var v = new List<Vector3>();
        var nrm = new List<Vector3>();
        var tri = new List<int>();
        for (int y = 0; y <= lat; y++)
        {
            float a = Mathf.PI * y / lat;
            for (int x = 0; x <= lon; x++)
            {
                float b = 2f * Mathf.PI * x / lon;
                var p = new Vector3(Mathf.Sin(a) * Mathf.Cos(b), Mathf.Cos(a), Mathf.Sin(a) * Mathf.Sin(b));
                v.Add(p * 0.5f);
                nrm.Add(p);
            }
        }
        for (int y = 0; y < lat; y++)
            for (int x = 0; x < lon; x++)
            {
                int i = y * (lon + 1) + x;
                tri.Add(i); tri.Add(i + lon + 1); tri.Add(i + 1);
                tri.Add(i + 1); tri.Add(i + lon + 1); tri.Add(i + lon + 2);
            }
        sphere = new Mesh { name = "ScreamShell" };
        sphere.SetVertices(v);
        sphere.SetNormals(nrm);
        sphere.SetTriangles(tri, 0);
        sphere.RecalculateBounds();
        return sphere;
    }
}
