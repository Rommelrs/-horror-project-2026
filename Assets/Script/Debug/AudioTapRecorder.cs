using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Playables;

// Debug: records what passes through this point of an AudioSource's filter chain (put it on the source's GameObject;
// it taps after the filters above it) to `outputPath` .wav, plus a CSV of each frame's playing timeline time, frame
// time and DSP time next to it, whether the source is playing / virtual (silenced by the voice limit) and how many
// sources are playing. For finding skips/repeats in timeline audio. Saves on Save() or when destroyed.
public class AudioTapRecorder : MonoBehaviour
{
    public string outputPath;
    [Tooltip("The source whose playing/virtual state is logged (default: the one on this GameObject). On the listener, "
           + "the tap records the final mix.")]
    public AudioSource watch;

    readonly List<float> samples = new List<float>();
    readonly StringBuilder frames = new StringBuilder("unscaledTime,dt,dspTime,samplesSoFar,timeline,time,sourcePlaying,sourceVirtual,playingSources\n");
    int channels = 2, rate;
    bool saved;

    void Awake() => rate = AudioSettings.outputSampleRate;

    void OnAudioFilterRead(float[] data, int ch)
    {
        lock (samples) { samples.AddRange(data); channels = ch; }
    }

    void Update()
    {
        PlayableDirector playing = null;
        foreach (var d in FindObjectsByType<PlayableDirector>(FindObjectsSortMode.None))
            if (d.state == PlayState.Playing) { playing = d; break; }
        int n; lock (samples) n = samples.Count / Mathf.Max(1, channels);
        int playingSources = 0;
        foreach (var a in FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) if (a.isPlaying) playingSources++;
        var source = watch != null ? watch : GetComponent<AudioSource>();
        frames.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0:F4},{1:F4},{2:F4},{3},{4},{5:F4},{6},{7},{8}\n",
            Time.unscaledTime, Time.unscaledDeltaTime, AudioSettings.dspTime, n,
            playing != null ? playing.name : "-", playing != null ? playing.time : -1,
            source != null && source.isPlaying ? 1 : 0, source != null && source.isVirtual ? 1 : 0, playingSources);
    }

    public void Save()
    {
        if (saved || string.IsNullOrEmpty(outputPath)) return;
        saved = true;
        float[] data; int ch;
        lock (samples) { data = samples.ToArray(); ch = channels; }
        using (var w = new BinaryWriter(File.Create(outputPath)))
        {
            int bytes = data.Length * 2;
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes); w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16); w.Write((short)1); w.Write((short)ch); w.Write(rate); w.Write(rate * ch * 2); w.Write((short)(ch * 2)); w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(bytes);
            foreach (var s in data) w.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767f));
        }
        File.WriteAllText(Path.ChangeExtension(outputPath, ".csv"), frames.ToString());
    }

    void OnDestroy() => Save();
}
