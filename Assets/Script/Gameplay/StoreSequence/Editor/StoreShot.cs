using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Editor helper: renders one scene only (the Store and the Game copy of the store overlap in space) to a PNG.</summary>
public static class StoreShot
{
    public static void Take(string sceneName, Vector3 pos, Vector3 lookAt, string file, float fov = 60f, float ambient = 0.55f)
    {
        var sc = SceneManager.GetSceneByName(sceneName);
        var go = new GameObject("TmpShotCam") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.scene = sc;
        cam.transform.position = pos;
        cam.transform.LookAt(lookAt);
        cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 60f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
        var amb = RenderSettings.ambientLight; var mode = RenderSettings.ambientMode; bool fog = RenderSettings.fog;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(ambient, ambient, ambient); RenderSettings.fog = false;
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
        RenderTexture.active = prev;
        RenderSettings.ambientMode = mode; RenderSettings.ambientLight = amb; RenderSettings.fog = fog;
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots/StoreRemake");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG());
        Object.DestroyImmediate(tex); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
    }
}
