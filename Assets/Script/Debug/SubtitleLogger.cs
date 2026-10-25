using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

// Debug: logs every change of the subtitle text on screen (the Texts under SubtitleManager) with the playing timeline and
// its time, to `outputPath` when destroyed or on Save(). For checking which lines a cutscene actually shows, and when.
public class SubtitleLogger : MonoBehaviour
{
    public string outputPath;

    readonly StringBuilder log = new StringBuilder("unscaledTime,timeline,time,text\n");
    Text[] texts;
    string[] last;
    bool saved;

    void Update()
    {
        if (texts == null)
        {
            if (SubtitleManager.instance == null) return;
            texts = SubtitleManager.instance.GetComponentsInChildren<Text>(true);
            last = new string[texts.Length];
        }
        PlayableDirector playing = null;
        foreach (var d in FindObjectsByType<PlayableDirector>(FindObjectsSortMode.None))
            if (d.state == PlayState.Playing) { playing = d; break; }
        for (int i = 0; i < texts.Length; i++)
        {
            string t = texts[i] != null ? texts[i].text : "";
            if (t == last[i]) continue;
            last[i] = t;
            log.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0:F2},{1},{2:F2},\"{3}\"\n", Time.unscaledTime,
                playing != null ? playing.name : "-", playing != null ? playing.time : -1, t.Replace("\"", "'").Replace("\n", " "));
        }
    }

    public void Save()
    {
        if (saved || string.IsNullOrEmpty(outputPath)) return;
        saved = true;
        File.WriteAllText(outputPath, log.ToString(), Encoding.UTF8);
    }

    void OnDestroy() => Save();
}
