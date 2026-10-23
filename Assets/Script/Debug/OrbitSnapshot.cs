using System.Collections;
using System.IO;
using UnityEngine;

// Debug: renders `views` cameras orbiting `center` (all at the end of one frame) into one PNG strip, then removes itself.
public class OrbitSnapshot : MonoBehaviour
{
    public Vector3 center;
    public float distance = 2.2f, height = 0.2f, fov = 40f;
    public float[] yaws = { 0f, 60f, 120f, 180f, 240f, 300f };
    public string outputPath;
    public int size = 400;
    public bool done;

    IEnumerator Start()
    {
        yield return new WaitForEndOfFrame();
        var camGo = new GameObject("OrbitCam"); var cam = camGo.AddComponent<Camera>();
        cam.enabled = false; cam.fieldOfView = fov; cam.nearClipPlane = 0.05f;
        var rt = new RenderTexture(size, size, 24); cam.targetTexture = rt;
        var tex = new Texture2D(size * yaws.Length, size, TextureFormat.RGB24, false);
        for (int i = 0; i < yaws.Length; i++)
        {
            cam.transform.position = center + Quaternion.Euler(0f, yaws[i], 0f) * Vector3.forward * distance + Vector3.up * height;
            cam.transform.LookAt(center);
            cam.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, size, size), i * size, 0);
            RenderTexture.active = null;
        }
        tex.Apply();
        File.WriteAllBytes(outputPath, tex.EncodeToPNG());
        Destroy(camGo); done = true; Destroy(this);
    }
}
