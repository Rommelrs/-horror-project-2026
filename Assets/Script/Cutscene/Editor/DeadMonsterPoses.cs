using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Turns one frame of an enemy's animation into a plain (not skinned) mesh: a dead body that costs nothing to draw.
/// Used for the bodies behind the store (exit cutscene). Also renders pose previews so a frame can be picked by eye.
/// </summary>
public static class DeadMonsterPoses
{
    public const string QuestionerPrefab = "Assets/Prefab/Enemy/QuestionerPuppet.prefab";
    public const string RunnerPrefab = "Assets/Prefab/Enemy/RunnerPuppet.prefab";

    /// <summary>A limb swung around the vertical axis after the clip frame was applied (it stays lying on the ground): one pose becomes several.</summary>
    public struct Tweak { public string bone; public float yaw; public Tweak(string b, float yaw) { bone = b; this.yaw = yaw; } }

    public class Baked { public Mesh mesh; public Material[] materials; }

    static AnimationClip FindClip(Animator an, string clipName)
    {
        foreach (var c in an.runtimeAnimatorController.animationClips) if (c.name == clipName) return c;
        return null;
    }

    /// <summary>Instantiates the prefab into 'scene', poses it and returns the instance (caller destroys it).</summary>
    public static GameObject Pose(UnityEngine.SceneManagement.Scene scene, string prefabPath, string clipName, float normTime, Tweak[] tweaks)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var go = Object.Instantiate(prefab);
        go.hideFlags = HideFlags.HideAndDontSave;
        EditorSceneManager.MoveGameObjectToScene(go, scene);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
        var an = go.GetComponentInChildren<Animator>(true);
        var clip = FindClip(an, clipName);
        if (clip == null) { Debug.LogError("[DeadMonsterPoses] no clip " + clipName + " on " + prefabPath); return go; }
        clip.SampleAnimation(an.gameObject, Mathf.Clamp01(normTime) * clip.length);
        if (tweaks != null)
            foreach (var tw in tweaks)
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    if (t.name == tw.bone) { t.RotateAround(t.position, Vector3.up, tw.yaw); break; }
        return go;
    }

    /// <summary>Bakes every skinned mesh of a posed instance into one mesh, lying on y = 0 and centred on x/z.</summary>
    public static Baked Bake(GameObject posed, float scale, bool mirror)
    {
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>();
        var mats = new List<Material>(); var subTris = new List<List<int>>();
        foreach (var smr in posed.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (!smr.enabled || smr.sharedMesh == null) continue;
            var m = new Mesh();
            smr.BakeMesh(m, true);
            var toWorld = smr.transform.localToWorldMatrix;   // BakeMesh(useScale: true) gives true local space
            int baseIndex = verts.Count;
            var mv = m.vertices; var mn = m.normals; var mu = m.uv;
            for (int i = 0; i < mv.Length; i++)
            {
                verts.Add(toWorld.MultiplyPoint3x4(mv[i]) * scale);
                norms.Add(mn.Length == mv.Length ? toWorld.MultiplyVector(mn[i]).normalized : Vector3.up);
                uvs.Add(mu.Length == mv.Length ? mu[i] : Vector2.zero);
            }
            var sm = smr.sharedMaterials;
            for (int s = 0; s < m.subMeshCount; s++)
            {
                var mat = sm[Mathf.Min(s, sm.Length - 1)];
                int slot = mats.IndexOf(mat);
                if (slot < 0) { slot = mats.Count; mats.Add(mat); subTris.Add(new List<int>()); }
                var tr = m.GetTriangles(s);
                for (int i = 0; i < tr.Length; i++) subTris[slot].Add(tr[i] + baseIndex);
            }
            Object.DestroyImmediate(m);
        }
        if (verts.Count == 0) return null;

        // lie on the ground, centred
        var min = verts[0]; var max = verts[0];
        foreach (var v in verts) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
        var shift = new Vector3(-(min.x + max.x) * 0.5f, -min.y, -(min.z + max.z) * 0.5f);
        for (int i = 0; i < verts.Count; i++)
        {
            var v = verts[i] + shift; var n = norms[i];
            if (mirror) { v.x = -v.x; n.x = -n.x; }
            verts[i] = v; norms[i] = n;
        }
        if (mirror)
            foreach (var tr in subTris)
                for (int i = 0; i + 2 < tr.Count; i += 3) { int a = tr[i]; tr[i] = tr[i + 2]; tr[i + 2] = a; }

        var mesh = new Mesh();
        mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs);
        mesh.subMeshCount = subTris.Count;
        for (int s = 0; s < subTris.Count; s++) mesh.SetTriangles(subTris[s], s);
        mesh.RecalculateBounds();
        return new Baked { mesh = mesh, materials = mats.ToArray() };
    }

    /// <summary>Renders one posed body from two sides into Screenshots/StoreRemake/deadpose_NAME.png.</summary>
    public static void Preview(string name, string prefabPath, string clipName, float normTime, Tweak[] tweaks = null, bool mirror = false)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var posed = Pose(scene, prefabPath, clipName, normTime, tweaks);
            var baked = Bake(posed, 1f, mirror);
            Object.DestroyImmediate(posed);
            if (baked == null) { Debug.LogError("[DeadMonsterPoses] nothing baked for " + name); return; }

            var body = new GameObject("body") { hideFlags = HideFlags.HideAndDontSave };
            EditorSceneManager.MoveGameObjectToScene(body, scene);
            body.AddComponent<MeshFilter>().sharedMesh = baked.mesh;
            body.AddComponent<MeshRenderer>().sharedMaterials = baked.materials;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.hideFlags = HideFlags.HideAndDontSave;
            EditorSceneManager.MoveGameObjectToScene(floor, scene);
            floor.transform.localScale = Vector3.one * 2f;
            var fm = new Material(Shader.Find("Standard")); fm.color = new Color(0.45f, 0.42f, 0.38f); fm.SetFloat("_Glossiness", 0f);
            floor.GetComponent<Renderer>().sharedMaterial = fm;
            var lgo = new GameObject("sun") { hideFlags = HideFlags.HideAndDontSave };
            EditorSceneManager.MoveGameObjectToScene(lgo, scene);
            var l = lgo.AddComponent<Light>(); l.type = LightType.Directional; l.intensity = 1.2f; lgo.transform.rotation = Quaternion.Euler(55f, 30f, 0f);

            var cgo = new GameObject("cam") { hideFlags = HideFlags.HideAndDontSave };
            EditorSceneManager.MoveGameObjectToScene(cgo, scene);
            var cam = cgo.AddComponent<Camera>(); cam.scene = scene; cam.enabled = false; cam.fieldOfView = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.25f, 0.27f, 0.3f); cam.nearClipPlane = 0.05f; cam.farClipPlane = 50f;

            const int W = 640, H = 480;
            var sheet = new Texture2D(W * 2, H, TextureFormat.RGB24, false);
            var rt = new RenderTexture(W, H, 24); cam.targetTexture = rt;
            var b = baked.mesh.bounds;
            Vector3 look = new Vector3(0f, b.size.y * 0.4f, 0f);
            Vector3[] eyes = { new Vector3(2.4f, 2.3f, -2.4f), new Vector3(-0.3f, 4.6f, -0.3f) };
            for (int i = 0; i < 2; i++)
            {
                cgo.transform.position = eyes[i]; cgo.transform.LookAt(look);
                cam.Render();
                RenderTexture.active = rt;
                sheet.ReadPixels(new Rect(0, 0, W, H), i * W, 0);
            }
            RenderTexture.active = null; cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt);
            sheet.Apply();
            string dir = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Screenshots/StoreRemake");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "deadpose_" + name + ".png"), sheet.EncodeToPNG());
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "deadpose_log.txt"), name + " size " + b.size.ToString("F2") + " verts " + baked.mesh.vertexCount + System.Environment.NewLine);
            Object.DestroyImmediate(sheet); Object.DestroyImmediate(baked.mesh); Object.DestroyImmediate(fm);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
