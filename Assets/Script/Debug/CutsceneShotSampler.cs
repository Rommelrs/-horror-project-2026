using System.Collections;
using System.Collections.Generic;
using System.IO;
using Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

// Debug: scrubs a running CowardMeetingCutscene to chosen moments ("Open:12.5", "A:3", "End:8.1") with time frozen, and
// saves what the Game view shows for each one (composer damping off so the framing is the settled one). Moments must be
// in playing order; jumping forward past the end of a timeline lets the cutscene move on to the next one.
public class CutsceneShotSampler : MonoBehaviour
{
    public string outputDir;
    public string[] moments = new string[0];
    public int width = 640;
    public bool done;

    static readonly string[] Order = { "Open", "A", "B", "End" };

    void Start() { StartCoroutine(Co_Sample()); }

    IEnumerator Co_Sample()
    {
        Directory.CreateDirectory(outputDir);
        var cut = FindFirstObjectByType<CowardMeetingCutscene>();
        var directors = new Dictionary<string, PlayableDirector> { { "Open", cut.open }, { "A", cut.versionA }, { "B", cut.versionB }, { "End", cut.end } };
        foreach (var vc in FindObjectsByType<CinemachineVirtualCamera>(FindObjectsSortMode.None))
        {
            var comp = vc.GetCinemachineComponent<CinemachineComposer>();
            if (comp != null) { comp.m_HorizontalDamping = 0f; comp.m_VerticalDamping = 0f; }
        }
        Time.timeScale = 0f;

        int index = 0;
        foreach (var m in moments)
        {
            var parts = m.Split(':');
            var d = directors[parts[0]];
            float t = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);

            // run the earlier timelines out until this one is the playing one
            for (int guard = 0; guard < 10 && d.state != PlayState.Playing; guard++)
            {
                foreach (var name in Order)
                {
                    var other = directors[name];
                    if (other != null && other != d && other.state == PlayState.Playing) { other.time = other.duration; other.Evaluate(); }
                }
                Time.timeScale = 1f;
                yield return null; yield return null;
                Time.timeScale = 0f;
            }

            d.time = t; d.Evaluate();
            for (int f = 0; f < 4; f++) { d.time = t; d.Evaluate(); yield return null; }
            yield return new WaitForEndOfFrame();

            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            int h = Mathf.RoundToInt(width * shot.height / (float)shot.width);
            var rt = RenderTexture.GetTemporary(width, h);
            Graphics.Blit(shot, rt);
            var small = new Texture2D(width, h, TextureFormat.RGB24, false);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            small.ReadPixels(new Rect(0, 0, width, h), 0, 0); small.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(Path.Combine(outputDir, index.ToString("000") + "_" + parts[0] + "_" + t.ToString("00.00") + ".jpg"), small.EncodeToJPG(88));
            Destroy(shot); Destroy(small);
            index++;
        }
        Time.timeScale = 1f;
        done = true;
    }
}
