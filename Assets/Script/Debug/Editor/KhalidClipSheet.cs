using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool: renders a Khalid clip as a strip of poses (edit mode, no fog), to judge an animation without playing.
/// The skinned meshes are baked per pose (SkinnedMeshRenderer does not re-skin between renders in one editor frame).
/// </summary>
public static class KhalidClipSheet
{
    public static void Render(string clipPath, string outPng, float from, float to, int frames, float yaw, int w = 220, int h = 330, bool closeUp = false)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) { Debug.LogWarning("[ClipSheet] no clip at " + clipPath); return; }
        var p = Object.FindFirstObjectByType<Player>(FindObjectsInactive.Include);
        var copy = Object.Instantiate(p.GetComponentInChildren<Animator>(true).gameObject);
        copy.hideFlags = HideFlags.DontSave;
        foreach (var mb in copy.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
        copy.GetComponent<Animator>().enabled = false;
        foreach (var t in copy.GetComponentsInChildren<Transform>(true)) if (t.name == "Pistol 2" || t.name == "MeleePipe") t.gameObject.SetActive(false);
        var smrs = new List<SkinnedMeshRenderer>(copy.GetComponentsInChildren<SkinnedMeshRenderer>(false));
        var bakes = new List<GameObject>();
        var meshes = new List<UnityEngine.Mesh>();
        foreach (var s in smrs)
        {
            var g = new GameObject("bake") { hideFlags = HideFlags.DontSave };
            var mf = g.AddComponent<MeshFilter>();
            g.AddComponent<MeshRenderer>().sharedMaterials = s.sharedMaterials;
            var m = new UnityEngine.Mesh();
            mf.sharedMesh = m; meshes.Add(m); bakes.Add(g); s.enabled = false;
        }
        var camGo = new GameObject("tmpcam") { hideFlags = HideFlags.DontSave };
        var cam = camGo.AddComponent<Camera>();
        cam.enabled = false; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.45f, 0.45f, 0.5f);
        cam.fieldOfView = closeUp ? 12f : 28f;
        var lightGo = new GameObject("tmplight") { hideFlags = HideFlags.DontSave };
        var li = lightGo.AddComponent<Light>(); li.type = LightType.Directional; li.intensity = 1.1f;
        lightGo.transform.rotation = Quaternion.Euler(35f, 20f, 0f);
        bool fog = RenderSettings.fog; RenderSettings.fog = false;
        var amb = RenderSettings.ambientLight; var am = RenderSettings.ambientMode;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = Color.white * 0.75f;
        var rt = new RenderTexture(w, h, 24);
        var sheet = new Texture2D(w * frames, h, TextureFormat.RGB24, false);
        Vector3 origin = new Vector3(0f, -200f, 0f);
        for (int i = 0; i < frames; i++)
        {
            float tt = Mathf.Lerp(from, to, frames > 1 ? i / (frames - 1f) : 0f);
            copy.transform.SetPositionAndRotation(origin, Quaternion.Euler(0f, yaw, 0f));
            clip.SampleAnimation(copy, tt);
            for (int k = 0; k < smrs.Count; k++)
            {
                smrs[k].BakeMesh(meshes[k]);
                bakes[k].transform.SetPositionAndRotation(smrs[k].transform.position, smrs[k].transform.rotation);
            }
            if (closeUp) { camGo.transform.position = origin + new Vector3(0f, 1.6f, -5.2f); camGo.transform.LookAt(origin + new Vector3(0f, 1.45f, 0f)); }
            else { camGo.transform.position = origin + new Vector3(0f, 1.15f, -5.2f); camGo.transform.LookAt(origin + new Vector3(0f, 0.9f, 0f)); }
            cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var t = new Texture2D(w, h, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, w, h), 0, 0); t.Apply();
            RenderTexture.active = null;
            sheet.SetPixels(i * w, 0, w, h, t.GetPixels());
            Object.DestroyImmediate(t);
        }
        sheet.Apply();
        System.IO.File.WriteAllBytes(outPng, sheet.EncodeToPNG());
        RenderSettings.fog = fog; RenderSettings.ambientLight = amb; RenderSettings.ambientMode = am;
        cam.targetTexture = null; rt.Release();
        Object.DestroyImmediate(sheet); Object.DestroyImmediate(camGo); Object.DestroyImmediate(lightGo); Object.DestroyImmediate(copy);
        foreach (var g in bakes) Object.DestroyImmediate(g);
        foreach (var m in meshes) Object.DestroyImmediate(m);
    }
}
