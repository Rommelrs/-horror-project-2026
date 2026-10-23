using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Playables;

// Debug: records what the Game view shows while a cutscene plays in real time (screen grab incl. UI/subtitles), one
// JPG every `interval` seconds of game time, named by the playing director + its time, plus a CSV of actor positions.
// Add it at runtime (e.g. from an editor command), set outputDir, it removes itself after `maxSeconds`.
public class CutsceneFrameRecorder : MonoBehaviour
{
    public string outputDir;
    public float interval = 0.25f;
    public float maxSeconds = 120f;
    public int width = 480;
    public Transform[] tracked = new Transform[0];

    float next, started;
    StringBuilder csv = new StringBuilder("file,director,time,camPos,camRot,tracked\n");

    void Start()
    {
        Directory.CreateDirectory(outputDir);
        started = Time.time;
        StartCoroutine(Co_Record());
    }

    IEnumerator Co_Record()
    {
        var eof = new WaitForEndOfFrame();
        int index = 0;
        while (Time.time - started < maxSeconds)
        {
            yield return eof;
            if (Time.time < next) continue;
            next = Time.time + interval;

            PlayableDirector playing = null;
            foreach (var d in FindObjectsByType<PlayableDirector>(FindObjectsSortMode.None))
                if (d.state == PlayState.Playing) { playing = d; break; }
            if (playing == null && index > 0 && !CowardMeetingCutscene.IsPlaying) break;

            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            int h = Mathf.RoundToInt(width * shot.height / (float)shot.width);
            var rt = RenderTexture.GetTemporary(width, h);
            Graphics.Blit(shot, rt);
            var small = new Texture2D(width, h, TextureFormat.RGB24, false);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            small.ReadPixels(new Rect(0, 0, width, h), 0, 0); small.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);

            string dir = playing != null ? playing.name : "none";
            string t = playing != null ? playing.time.ToString("00.00") : "00.00";
            string file = index.ToString("0000") + "_" + dir + "_" + t + ".jpg";
            File.WriteAllBytes(Path.Combine(outputDir, file), small.EncodeToJPG(85));
            Destroy(shot); Destroy(small);

            var cam = Camera.main != null ? Camera.main.transform : null;
            csv.Append(file).Append(',').Append(dir).Append(',').Append(t).Append(',')
               .Append(cam != null ? V(cam.position) : "").Append(',').Append(cam != null ? V(cam.eulerAngles) : "").Append(',');
            foreach (var tr in tracked) if (tr != null) csv.Append(tr.name).Append('=').Append(V(tr.position)).Append(' ');
            csv.Append('\n');
            index++;
        }
        File.WriteAllText(Path.Combine(outputDir, "frames.csv"), csv.ToString());
        Destroy(this);
    }

    static string V(Vector3 v) => v.x.ToString("F2") + " " + v.y.ToString("F2") + " " + v.z.ToString("F2");
}
