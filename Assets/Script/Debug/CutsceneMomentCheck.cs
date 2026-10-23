using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Playables;

// Debug: plays the coward meeting, jumps the given timeline to `time`, lets it run `settle` seconds (so smoothed
// weights like the grab reach their value), freezes time and saves orbit views around the two characters plus a line
// of measurements (where the coward's wrists are against their targets). Then resumes.
public class CutsceneMomentCheck : MonoBehaviour
{
    public string timeline = "A";
    public float time = 16f, settle = 1.2f;
    public string outputPath;
    public float distance = 1.1f, height = 0.25f, centerHeight = 1.55f;
    public float[] yawOffsets = { 70f, 110f, -70f, -110f };
    public int size = 520;
    public string report;
    public bool done;

    IEnumerator Start()
    {
        var cut = FindFirstObjectByType<CowardMeetingCutscene>();
        cut.startFade = 0f;
        cut.DebugPlayNow();
        PlayableDirector d = timeline == "Open" ? cut.open : timeline == "End" ? cut.end : timeline == "B" ? cut.versionB : cut.versionA;
        // run the earlier timelines out quickly
        while (d.state != PlayState.Playing)
        {
            foreach (var other in new[] { cut.open, cut.versionA, cut.versionB })
                if (other != d && other.state == PlayState.Playing && other.time < other.duration - 0.5) other.time = other.duration - 0.05;
            yield return null;
        }
        d.time = Mathf.Max(0f, time - settle);
        if (string.IsNullOrEmpty(outputPath)) { done = true; yield break; }   // only jump there, keep playing
        float until = Time.time + settle;
        while (Time.time < until) yield return null;
        Time.timeScale = 0f;
        yield return null; yield return new WaitForEndOfFrame();

        var p = Player.instance.transform;
        var c = cut.coward.transform;
        var center = (p.position + c.position) * 0.5f + Vector3.up * centerHeight;
        float yaw = Quaternion.LookRotation(c.position - p.position).eulerAngles.y;
        var camGo = new GameObject("CheckCam"); var cam = camGo.AddComponent<Camera>();
        cam.enabled = false; cam.fieldOfView = 40f; cam.nearClipPlane = 0.05f;
        var rt = new RenderTexture(size, size, 24); cam.targetTexture = rt;
        var tex = new Texture2D(size * yawOffsets.Length, size, TextureFormat.RGB24, false);
        for (int i = 0; i < yawOffsets.Length; i++)
        {
            cam.transform.position = center + Quaternion.Euler(0f, yaw + yawOffsets[i], 0f) * Vector3.forward * distance + Vector3.up * height;
            cam.transform.LookAt(center);
            cam.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, size, size), i * size, 0);
            RenderTexture.active = null;
        }
        tex.Apply();
        File.WriteAllBytes(outputPath, tex.EncodeToPNG());
        Destroy(camGo);

        var ik = cut.coward.GetComponent<CutsceneArmIK>();
        report = d.name + " " + d.time.ToString("F2") + " apart " + Vector3.Distance(p.position, c.position).ToString("F2");
        if (ik != null)
            foreach (var a in new[] { ik.left, ik.right })
                if (a.target != null)
                {
                    Vector3 t = a.target.position + (a.targetSpace != null ? a.targetSpace.TransformDirection(a.targetOffset) : a.targetOffset);
                    report += " | wrist-target " + (a.hand.position - t).magnitude.ToString("F3") + " wrist h " + (a.hand.position.y - p.position.y).ToString("F2")
                        + " elbow h " + (a.lower.position.y - p.position.y).ToString("F2") + " w " + ik.weight.ToString("F2");
                }
        Time.timeScale = 1f;
        done = true;
    }
}
