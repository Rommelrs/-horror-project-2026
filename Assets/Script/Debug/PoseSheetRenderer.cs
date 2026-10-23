using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Debug: poses an Animator with each clip in turn (at `sampleTime`) and renders it from the side and 3/4 front, one
// column per clip, into a PNG. Waits a frame per pose so the skinned meshes are updated before rendering.
public class PoseSheetRenderer : MonoBehaviour
{
    public Animator target;
    public AnimationClip[] clips = new AnimationClip[0];
    public float sampleTime = 0.5f;
    public string outputPath;
    public bool done;

    IEnumerator Start()
    {
        var root = target.transform;
        var camGo = new GameObject("PoseSheetCam"); var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 40; cam.nearClipPlane = 0.05f; cam.enabled = false;
        var rt = new RenderTexture(360, 480, 24); cam.targetTexture = rt;
        var tex = new Texture2D(360 * clips.Length, 960, TextureFormat.RGB24, false);
        for (int i = 0; i < clips.Length; i++)
        {
            var pl = AnimationPlayableUtilities.PlayClip(target, clips[i], out var g);
            pl.SetTime(sampleTime); pl.SetSpeed(0);
            yield return null; yield return null;
            yield return new WaitForEndOfFrame();
            Vector3[] dirs = { root.right, (root.forward + root.right * 0.6f).normalized };
            for (int v = 0; v < 2; v++)
            {
                cam.transform.position = root.position + Vector3.up * 1.1f + dirs[v] * 3.4f;
                cam.transform.LookAt(root.position + Vector3.up * 1.0f);
                cam.Render(); RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 360, 480), i * 360, (1 - v) * 480);
                RenderTexture.active = null;
            }
            g.Destroy();
        }
        tex.Apply();
        File.WriteAllBytes(outputPath, tex.EncodeToPNG());
        Destroy(camGo);
        done = true;
    }
}
